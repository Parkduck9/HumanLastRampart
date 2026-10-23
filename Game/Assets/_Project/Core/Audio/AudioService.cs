using System.Collections.Generic;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Settings;
using UnityEngine;

namespace HumanLastRampart.Core.Audio
{
    // 소리 서비스 (표현 전용): 시뮬레이션·결정적 난수와 무관하다 (자체 가벼운 난수 사용). 화면 쪽 코드가 부른다.
    // - 소리 은행(SoundBank)에서 열쇠로 찾고, 풀링한 AudioSource 몇 개만 돌려 쓴다 (PC 24 / 모바일 12, 할당 없음)
    // - 소리마다 쿨다운·동시 수, 전체 동시 수 제한 (AudioVoiceLimiter)
    // - Report: 한 프레임에 쌓인 같은 소리(화살 맞음·죽음)를 개수만 세어 LateUpdate에서 한 번, 개수만큼 크게
    // - 음악은 두 칸으로 교차 페이드, 주변 소리는 열쇠마다 반복 재생하며 크기만 맞춘다
    // - 은행이 비었거나 파일이 없으면 아무것도 하지 않고 경고도 내지 않는다
    public sealed class AudioService : MonoBehaviour
    {
        public const int DesktopVoices = 24, MobileVoices = 12;
        const int AmbienceSlots = 3;
        const float MusicFadeSeconds = 1.5f, AmbienceFadeSeconds = 1.0f;

        static AudioService instance;

        SoundBank bank;
        readonly Dictionary<string, int> index = new Dictionary<string, int>();
        bool[] playable;                     // 항목마다 파일이 하나라도 있는가
        AudioVoiceLimiter limiter;
        AudioAggregator aggregator;
        AudioSource[] voices;
        AudioSource[] musicSources;
        int musicActive;                     // 지금 커지는 쪽 칸 (-1 = 음악 없음)
        string musicCue;
        float musicFade = 1f;
        readonly AudioSource[] ambSources = new AudioSource[AmbienceSlots];
        readonly string[] ambCue = new string[AmbienceSlots];
        readonly float[] ambTarget = new float[AmbienceSlots];
        readonly float[] ambNow = new float[AmbienceSlots];
        float clock;
        uint rng = 2463534242u;
        bool anyPending;
        bool ready;

        // 시험·진단용
        public int StartedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int VoiceCapacity => voices != null ? voices.Length : 0;
        public bool HasAnySound { get; private set; }
        public string CurrentMusic => musicCue;

        // ── 정적 진입점 (화면 코드는 여기만 부른다. 놀이 중이 아니면 아무 일도 안 한다) ──

        public static AudioService Instance => Get();

        static AudioService Get()
        {
            if (instance != null) return instance;
            if (!Application.isPlaying) return null;
            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioService>();
            instance.Init(Resources.Load<SoundBank>(SoundBank.ResourcePath), Application.isMobilePlatform ? MobileVoices : DesktopVoices);
            return instance;
        }

        public static void Play(string cue) => Get()?.PlayAt(cue, Vector3.zero, 1f, false);
        public static void PlayAtPoint(string cue, Vector3 position, float gain = 1f) => Get()?.PlayAt(cue, position, gain, true);
        public static void Report(string cue, int count, Vector3 position) => Get()?.ReportCue(cue, count, position);
        public static void Music(string cue) => Get()?.SetMusic(cue);
        public static void Ambience(string cue, float level) => Get()?.SetAmbience(cue, level);

        // 화면 바뀔 때 음악 (GameFlow). 전투는 BattleAudioDirector가 낮·밤으로 직접 정한다
        public static void OnScreen(ScreenId screen)
        {
            var service = Get();
            if (service == null) return;
            switch (screen)
            {
                case ScreenId.Story: service.SetMusic(AudioCues.MusicStory); break;
                case ScreenId.Battle: break;
                case ScreenId.Result: service.SetMusic(null); service.PlayAt(AudioCues.ResultStinger, Vector3.zero, 1f, false); break;
                default: service.SetMusic(AudioCues.MusicDay); break;
            }

            if (screen != ScreenId.Battle) { service.SetAmbience(AudioCues.AmbCrowd, 0f); service.SetAmbience(AudioCues.AmbCrickets, 0f); }
        }

        // ── 준비 ──

        // 은행을 받아 풀을 만든다. null이면 빈 은행(조용함). 시험에서 직접 부른다
        public void Init(SoundBank soundBank, int voiceCount)
        {
            bank = soundBank != null ? soundBank : ScriptableObject.CreateInstance<SoundBank>();
            index.Clear();
            int count = bank.Entries.Count;
            playable = new bool[count];
            HasAnySound = false;
            for (int i = 0; i < count; i++)
            {
                var e = bank.Entries[i];
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                index[e.Id] = i;
                if (e.Clips == null) continue;
                foreach (AudioClip clip in e.Clips) if (clip != null) { playable[i] = true; HasAnySound = true; break; }
            }

            limiter = new AudioVoiceLimiter(count, voiceCount);
            aggregator = new AudioAggregator(count);
            voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < voices.Length; i++) voices[i] = MakeSource("Voice " + i);
            musicSources = new[] { MakeSource("Music A"), MakeSource("Music B") };
            for (int i = 0; i < AmbienceSlots; i++) ambSources[i] = MakeSource("Ambience " + i);
            musicActive = -1;
            ready = true;
        }

        AudioSource MakeSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        void Awake()
        {
            if (instance == null) instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ── 한 번 소리 ──

        // 울렸으면 true. 은행에 없거나 파일이 없거나 제한에 걸리면 false (조용히)
        public bool PlayAt(string cue, Vector3 position, float gain, bool at3D)
        {
            if (!ready || string.IsNullOrEmpty(cue) || !index.TryGetValue(cue, out int i) || !playable[i]) return false;
            return StartVoice(i, position, gain, at3D);
        }

        bool StartVoice(int i, Vector3 position, float gain, bool at3D)
        {
            SoundBank.Entry e = bank.Entries[i];
            if (e.Loop) return false;   // 반복 소리는 음악·주변 소리 길로만
            AudioClip clip = PickClip(e);
            if (clip == null) return false;
            float pitch = Mathf.Lerp(e.PitchRange.x, e.PitchRange.y, Next01());
            if (pitch < 0.1f) pitch = 1f;
            if (!limiter.TryBegin(i, clock, e.Cooldown, e.MaxVoices, clip.length / pitch)) { DroppedCount++; return false; }

            AudioSource source = FreeVoice();
            if (source == null) { DroppedCount++; return false; }
            Configure(source, e);
            bool spatial = e.Spatial3D && at3D;
            source.spatialBlend = spatial ? 1f : 0f;
            if (spatial) source.transform.position = position;
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(e.Volume * BusVolume(e.Bus) * gain);
            source.Play();
            StartedCount++;
            return true;
        }

        void Configure(AudioSource source, SoundBank.Entry e)
        {
            source.loop = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = e.MinDistance;
            source.maxDistance = Mathf.Max(e.MinDistance + 1f, e.MaxDistance);
            source.outputAudioMixerGroup = bank.GroupOf(e.Bus);
        }

        AudioSource FreeVoice()
        {
            for (int i = 0; i < voices.Length; i++) if (!voices[i].isPlaying) return voices[i];
            return null;
        }

        AudioClip PickClip(SoundBank.Entry e)
        {
            if (e.Clips == null || e.Clips.Length == 0) return null;
            int start = (int)(Next01() * e.Clips.Length) % e.Clips.Length;
            for (int k = 0; k < e.Clips.Length; k++)
            {
                AudioClip clip = e.Clips[(start + k) % e.Clips.Length];
                if (clip != null) return clip;
            }

            return null;
        }

        // ── 묶음 소리 ──

        // 이번 프레임 같은 소리 count개 — LateUpdate에서 한 번만 울린다 (개수가 많을수록 조금 크게)
        public void ReportCue(string cue, int count, Vector3 position)
        {
            if (!ready || count <= 0 || string.IsNullOrEmpty(cue) || !index.TryGetValue(cue, out int i) || !playable[i]) return;
            aggregator.Add(i, count, position);
            anyPending = true;
        }

        void Flush()
        {
            anyPending = false;
            for (int i = 0; i < playable.Length; i++)
            {
                if (!aggregator.Take(i, out int count, out Vector3 center)) continue;
                StartVoice(i, center, AudioMath.VolleyGain(count), true);
            }
        }

        // ── 음악·주변 소리 ──

        // 같은 곡이면 그대로, 다르면 교차 페이드. null = 페이드 아웃
        public void SetMusic(string cue)
        {
            if (!ready || cue == musicCue) return;
            musicCue = cue;
            int next = 1 - Mathf.Max(0, musicActive);
            if (string.IsNullOrEmpty(cue) || !index.TryGetValue(cue, out int i) || !playable[i])
            {
                musicActive = -1;
                return;
            }

            SoundBank.Entry e = bank.Entries[i];
            AudioClip clip = PickClip(e);
            if (clip == null) { musicActive = -1; return; }
            AudioSource source = musicSources[next];
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.volume = 0f;
            source.outputAudioMixerGroup = bank.GroupOf(AudioBus.Music);
            source.Play();
            musicActive = next;
            musicFade = 0f;
        }

        // 주변 소리: 열쇠마다 반복. level 0이면 서서히 꺼진다
        public void SetAmbience(string cue, float level)
        {
            if (!ready || string.IsNullOrEmpty(cue) || !index.TryGetValue(cue, out int i) || !playable[i]) return;
            int slot = -1;
            for (int s = 0; s < AmbienceSlots; s++) if (ambCue[s] == cue) { slot = s; break; }
            if (slot < 0)
            {
                if (level <= 0.001f) return;
                for (int s = 0; s < AmbienceSlots; s++) if (ambCue[s] == null || ambNow[s] <= 0.001f) { slot = s; break; }
                if (slot < 0) return;
                AudioClip clip = PickClip(bank.Entries[i]);
                if (clip == null) return;
                ambCue[slot] = cue;
                ambNow[slot] = 0f;
                AudioSource source = ambSources[slot];
                source.clip = clip;
                source.loop = true;
                source.spatialBlend = 0f;
                source.pitch = 1f;
                source.volume = 0f;
                source.outputAudioMixerGroup = bank.GroupOf(AudioBus.Ambience);
                source.Play();
            }

            ambTarget[slot] = Mathf.Clamp01(level);
        }

        // ── 매 프레임 ──

        void Update() => Tick(Time.unscaledDeltaTime);

        void LateUpdate()
        {
            if (anyPending) Flush();
        }

        // 시험에서 직접 부를 수 있다 (시각을 dt만큼 진행)
        public void Tick(float dt)
        {
            if (!ready) return;
            clock += dt;
            if (anyPending) Flush();

            float musicBus = BusVolume(AudioBus.Music);
            musicFade = Mathf.Min(1f, musicFade + dt / MusicFadeSeconds);
            for (int k = 0; k < musicSources.Length; k++)
            {
                AudioSource source = musicSources[k];
                if (!source.isPlaying) continue;
                float entryVolume = MusicEntryVolume();
                float target = k == musicActive ? entryVolume * musicBus : 0f;
                source.volume = Mathf.MoveTowards(source.volume, target, dt / MusicFadeSeconds);
                if (k != musicActive && source.volume <= 0.001f) source.Stop();
            }

            float ambBus = BusVolume(AudioBus.Ambience);
            for (int s = 0; s < AmbienceSlots; s++)
            {
                if (ambCue[s] == null) continue;
                AudioSource source = ambSources[s];
                ambNow[s] = Mathf.MoveTowards(ambNow[s], ambTarget[s], dt / AmbienceFadeSeconds);
                float entryVolume = index.TryGetValue(ambCue[s], out int i) ? bank.Entries[i].Volume : 1f;
                source.volume = Mathf.Clamp01(ambNow[s] * entryVolume * ambBus);
                if (ambNow[s] <= 0.001f && ambTarget[s] <= 0.001f)
                {
                    source.Stop();
                    ambCue[s] = null;
                }
            }
        }

        float MusicEntryVolume() => musicCue != null && index.TryGetValue(musicCue, out int i) ? bank.Entries[i].Volume : 1f;

        // 갈래별 크기 (설정). Master는 AudioListener.volume(App)이 맡는다
        public static float BusVolume(AudioBus bus)
        {
            GameSettings.EnsureLoaded();
            switch (bus)
            {
                case AudioBus.Music: return GameSettings.MusicVolume;
                case AudioBus.Master: return 1f;
                default: return GameSettings.SfxVolume;   // 효과·UI·주변 소리는 효과 소리 크기를 따른다
            }
        }

        // 가벼운 xorshift (UnityEngine.Random·시뮬레이션 난수를 건드리지 않는다)
        float Next01()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }
    }
}
