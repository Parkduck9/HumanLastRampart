using System.Collections.Generic;
using HumanLastRampart.Core.Audio;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Settings;
using UnityEngine;

namespace HumanLastRampart.App.Audio
{
    // 소리 서비스 (표현 전용, App 소속): 시뮬레이션·결정적 난수와 무관하다 (자체 가벼운 난수 사용).
    // 화면 코드는 Core의 정적 입구 Sound만 부르고, 이 서비스가 부팅 때 Sound.Sink로 등록된다.
    // - 소리 은행(SoundBank)에서 열쇠로 찾고, 풀링한 AudioSource 몇 개만 돌려 쓴다 (PC 24 / 모바일 12, 할당 없음)
    // - 소리마다 쿨다운·동시 수, 전체 동시 수 제한 (AudioVoiceLimiter)
    // - Report: 한 프레임에 쌓인 같은 소리(화살 맞음·죽음)를 개수만 세어 LateUpdate에서 한 번, 개수만큼 크게
    // - 음악은 두 칸으로 교차 페이드, 주변 소리는 열쇠마다 반복 재생하며 크기만 맞춘다
    // - 은행이 비었거나 파일이 없으면 아무것도 하지 않고 경고도 내지 않는다
    public sealed class AudioService : MonoBehaviour, IAudioSink
    {
        public const int DesktopVoices = 24, MobileVoices = 12;
        const int AmbienceSlots = 3;
        const float MusicFadeSeconds = 1.5f, AmbienceFadeSeconds = 1.0f;

        static AudioService instance;
        static bool quitting;

        SoundBank bank;
        readonly Dictionary<string, int> index = new Dictionary<string, int>();
        bool[] playable;                     // 항목마다 파일이 하나라도 있는가
        AudioVoiceLimiter limiter;
        AudioAggregator aggregator;
        AudioSource[] voices;
        AudioSource[] musicSources;
        int musicActive = -1;                // 지금 커지는 쪽 칸 (-1 = 음악 없음)
        string musicCue;
        readonly AudioSource[] ambSources = new AudioSource[AmbienceSlots];
        readonly string[] ambCue = new string[AmbienceSlots];
        readonly float[] ambTarget = new float[AmbienceSlots];
        readonly float[] ambNow = new float[AmbienceSlots];
        float clock;
        uint rng = 2463534242u;
        bool anyPending;
        bool ready;
        bool mapMode;
        Transform listenerTransform;          // 서비스가 쥔 하나뿐인 AudioListener (화면이 바뀌어도 안 사라진다)
        Transform followed;                   // 따라갈 화면 카메라 (그 카메라의 리스너는 꺼 둔다)

        // 시험·진단용
        public int StartedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int VoiceCapacity => voices != null ? voices.Length : 0;
        public bool HasAnySound { get; private set; }
        public string CurrentMusic => musicCue;
        public void ResetCounters() { StartedCount = 0; DroppedCount = 0; }

        // ── 만들기·등록 ──

        // 지금 있는 서비스 (없으면 null, 만들지 않는다)
        public static AudioService Current => instance;

        // 놀이 중이면 서비스를 만들고(없을 때만) Sound.Sink로 등록한다. 끄는 중이면 null
        public static AudioService Ensure()
        {
            if (instance != null) return instance;
            if (!Application.isPlaying || quitting) return null;
            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            var service = go.AddComponent<AudioService>();
            service.Init(Resources.Load<SoundBank>(SoundBank.ResourcePath), Application.isMobilePlatform ? MobileVoices : DesktopVoices);
            return service;
        }

        // 화면 바뀔 때 음악 (GameFlow). 전투는 BattleAudioDirector가 낮·밤으로 직접 정한다
        public void OnScreen(ScreenId screen, bool victory)
        {
            mapMode = false;
            switch (screen)
            {
                case ScreenId.Story: SetMusic(AudioCues.MusicStory); break;
                case ScreenId.Battle: break;
                case ScreenId.Result:
                    SetMusic(null);
                    PlayAt(victory ? AudioCues.ResultStinger : AudioCues.ResultDefeat, Vector3.zero, 1f, false);
                    break;
                default: SetMusic(AudioCues.MusicDay); break;
            }

            if (screen != ScreenId.Battle) StopAmbience();
        }

        // ── 준비 ──

        // 은행을 받아 풀을 만든다. null이면 빈 은행(조용함). 다시 불러도 된다(이전 소리 부품은 지운다). 시험에서 직접 부른다
        public void Init(SoundBank soundBank, int voiceCount)
        {
            ready = false;
            for (int c = transform.childCount - 1; c >= 0; c--)
            {
                if (listenerTransform != null && transform.GetChild(c) == listenerTransform) continue;
                if (Application.isPlaying) Destroy(transform.GetChild(c).gameObject);
                else DestroyImmediate(transform.GetChild(c).gameObject);
            }
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
            for (int i = 0; i < AmbienceSlots; i++) { ambSources[i] = MakeSource("Ambience " + i); ambCue[i] = null; ambNow[i] = ambTarget[i] = 0f; }
            if (listenerTransform == null)
            {
                var lgo = new GameObject("Listener");
                lgo.transform.SetParent(transform, false);
                lgo.AddComponent<AudioListener>();
                listenerTransform = lgo.transform;
            }

            musicActive = -1;
            musicCue = null;
            anyPending = false;
            StartedCount = DroppedCount = 0;
            ready = true;
            if (Application.isPlaying) { instance = this; Sound.Sink = this; AdoptSceneListeners(); }
        }

        AudioSource MakeSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;   // 카메라 이동으로 높낮이가 흔들리지 않게
            return source;
        }

        void OnApplicationQuit() { quitting = true; }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (Sound.Sink == (IAudioSink)this) Sound.Sink = null;
        }

        // ── IAudioSink ──

        public void Play(string cue) => PlayAt(cue, Vector3.zero, 1f, false);
        public void PlayAt(string cue, Vector3 position, float gain) => PlayAt(cue, position, gain, true);
        public void Report(string cue, int count, Vector3 position) => ReportCue(cue, count, position);
        public void Music(string cue) => SetMusic(cue);
        public void Ambience(string cue, float level) => SetAmbience(cue, level);
        public void SetMapMode(bool on) { mapMode = on; }

        // 주변 소리 모두 서서히 끔 (전투가 아닌 화면)
        public void StopAmbience()
        {
            for (int s = 0; s < AmbienceSlots; s++) ambTarget[s] = 0f;
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
            bool spatial = e.Spatial3D && at3D && !mapMode;   // 지도에서는 카메라가 하늘 높이 올라가므로 거리 감쇠 없이
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
            if (string.IsNullOrEmpty(cue) || !index.TryGetValue(cue, out int i) || !playable[i])
            {
                musicActive = -1;
                return;
            }

            AudioClip clip = PickClip(bank.Entries[i]);
            if (clip == null) { musicActive = -1; return; }
            // 쉬고 있는 칸을 쓰고, 둘 다 돌고 있으면 줄어드는 쪽을 가져온다
            int next = !musicSources[0].isPlaying ? 0 : !musicSources[1].isPlaying ? 1 : 1 - Mathf.Max(0, musicActive);
            AudioSource source = musicSources[next];
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.volume = 0f;
            source.outputAudioMixerGroup = bank.GroupOf(AudioBus.Music);
            source.Play();
            musicActive = next;
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
                // 빈 칸, 또는 목표도 지금도 0인 칸만 다시 쓴다 (방금 켜서 아직 커지기 전인 칸을 뺏지 않는다)
                for (int s = 0; s < AmbienceSlots; s++)
                    if (ambCue[s] == null || (ambNow[s] <= 0.001f && ambTarget[s] <= 0.001f)) { slot = s; break; }
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

        // 시험용: 주변 소리 칸 상태
        public bool IsAmbienceActive(string cue)
        {
            for (int s = 0; s < AmbienceSlots; s++) if (ambCue[s] == cue) return true;
            return false;
        }

        // ── 매 프레임 ──

        // 화면 카메라에 붙은 AudioListener는 모두 꺼 두고 서비스의 하나만 쓴다 ('리스너 없음' 안내가 화면 전환 틈에 뜨지 않게).
        // 화면이 열린 직후(GameFlow) 한 번 부른다. 꺼 둔 카메라의 자리·방향을 매 프레임 따라간다
        public void AdoptSceneListeners()
        {
            if (listenerTransform == null) return;
            foreach (AudioListener found in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (found.transform == listenerTransform) continue;
                if (found.enabled) followed = found.transform;
                found.enabled = false;
            }
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        void LateUpdate()
        {
            if (followed != null && listenerTransform != null) listenerTransform.SetPositionAndRotation(followed.position, followed.rotation);
            if (anyPending) Flush();
        }

        // 시험에서 직접 부를 수 있다 (시각을 dt만큼 진행)
        public void Tick(float dt)
        {
            if (!ready) return;
            clock += dt;
            if (anyPending) Flush();

            float musicBus = BusVolume(AudioBus.Music);
            for (int k = 0; k < musicSources.Length; k++)
            {
                AudioSource source = musicSources[k];
                if (!source.isPlaying) continue;
                float target = k == musicActive ? MusicEntryVolume() * musicBus : 0f;
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
