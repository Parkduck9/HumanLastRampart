using System.Collections.Generic;
using System.IO;
using System.Text;
using HumanLastRampart.App.Audio;
using HumanLastRampart.Core.Audio;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.App.Editor
{
    // 소리 은행 만들기 (W24): CC0 원본(HLR-audio-src)에서 고른 파일을 Assets/_Project/Audio로 복사하고,
    // 가져오기 설정(효과음 = 모노·메모리에 풀어 둠, 음악 = 스트리밍)을 맞춘 뒤 SoundBank.asset과 LICENSES.md를 만든다.
    // 메뉴 HLR/Audio/Build Sound Bank 또는 batchmode -executeMethod HumanLastRampart.App.Editor.AudioBankBuilder.Build
    // 열쇠별 고른 파일이 이 표의 단 하나의 기록이다. 파일을 바꾸려면 표를 고치고 다시 돌린다.
    public static class AudioBankBuilder
    {
        const string SourceRoot = "C:/Unity/1/HLR-audio-src/";
        const string AudioDir = "Assets/_Project/Audio/";
        const string BankPath = "Assets/_Project/Audio/Resources/Audio/SoundBank.asset";

        const string Impact = "kenney/impact-sounds/Audio/", Rpg = "kenney/rpg-audio/Audio/", Iface = "kenney/interface-sounds/Audio/";
        const string KenneyImpactUrl = "https://kenney.nl/assets/impact-sounds", KenneyRpgUrl = "https://kenney.nl/assets/rpg-audio", KenneyIfaceUrl = "https://kenney.nl/assets/interface-sounds";

        // 음악·결과 곡 (원본 폴더, 파일, 올릴 이름, 쪽 주소)
        static readonly (string src, string name, string url, string author)[] Tracks =
        {
            ("oga/medieval-the-old-tower-inn/The_Old_Tower_Inn.mp3", "day_old_tower_inn.mp3", "https://opengameart.org/content/medieval-the-old-tower-inn", "RandomMind"),
            ("oga/prepare-to-fight/prepare_to_fight.mp3", "night_prepare_to_fight.mp3", "https://opengameart.org/content/prepare-to-fight", "basil"),
            ("oga/battle-theme-a/battleThemeA.mp3", "battle_theme_a.mp3", "https://opengameart.org/content/battle-theme-a", "cynicmusic"),
            ("oga/the-hope/Hope.mp3", "story_the_hope.mp3", "https://opengameart.org/content/the-hope", "tad"),
            ("oga/victory-theme-for-rpg/Victory1_1.mp3", "result_victory.mp3", "https://opengameart.org/content/victory-theme-for-rpg", "cynicmusic"),
            ("oga/laments-of-the-war/Lament of the War - MP3 Preview_0.mp3", "result_defeat_laments.mp3", "https://opengameart.org/content/laments-of-the-war", "cethiel"),
        };

        struct Spec
        {
            public string Cue; public string[] Files; public float Volume, PitchMin, PitchMax; public int MaxVoices; public float Cooldown;
            public bool Spatial; public AudioBus Bus; public bool Loop; public float MaxDistance;
        }

        static string[] Seq(string pack, string stem, int from, int to, string fmt = "{0}_{1:000}")
        {
            var list = new List<string>();
            for (int i = from; i <= to; i++) list.Add(pack + string.Format(fmt, stem, i) + ".ogg");
            return list.ToArray();
        }

        static string[] Cat(params string[][] parts)
        {
            var list = new List<string>();
            foreach (var p in parts) list.AddRange(p);
            return list.ToArray();
        }

        static Spec Sfx(string cue, string[] files, float vol, float p0, float p1, int voices, float cd, bool spatial = true, float maxDist = 120f, AudioBus bus = AudioBus.Sfx) =>
            new Spec { Cue = cue, Files = files, Volume = vol, PitchMin = p0, PitchMax = p1, MaxVoices = voices, Cooldown = cd, Spatial = spatial, Bus = bus, MaxDistance = maxDist };

        static List<Spec> Specs()
        {
            return new List<Spec>
            {
                // 전투 — 무기 (Kenney 충격음을 낮추거나 높여 겹쳐 쓴다)
                Sfx(AudioCues.BowVolley, new[] { Rpg + "knifeSlice.ogg", Rpg + "knifeSlice2.ogg", Rpg + "drawKnife1.ogg", Rpg + "drawKnife2.ogg", Rpg + "drawKnife3.ogg" }, 0.7f, 1.1f, 1.4f, 4, 0.08f),
                Sfx(AudioCues.CrossbowVolley, Cat(Seq(Impact, "impactWood_light", 0, 2), new[] { Rpg + "metalLatch.ogg" }), 0.7f, 1.1f, 1.4f, 3, 0.1f),
                Sfx(AudioCues.MusketVolley, Seq(Impact, "impactMetal_heavy", 0, 3), 0.8f, 1.4f, 1.8f, 3, 0.12f),
                Sfx(AudioCues.CannonFire, Cat(Seq(Impact, "impactPunch_heavy", 0, 4), Seq(Impact, "impactMetal_heavy", 4, 4)), 1f, 0.35f, 0.5f, 3, 0.1f, true, 200f),
                Sfx(AudioCues.CannonImpact, Seq(Impact, "impactSoft_heavy", 0, 4), 1f, 0.45f, 0.65f, 4, 0.08f, true, 200f),
                Sfx(AudioCues.MagicCast, new[] { Iface + "maximize_003.ogg", Iface + "maximize_005.ogg", Iface + "glass_002.ogg" }, 0.6f, 0.8f, 1f, 2, 0.15f),
                Sfx(AudioCues.MagicExplosion, Seq(Impact, "impactGlass_heavy", 0, 4), 0.9f, 0.5f, 0.7f, 3, 0.1f, true, 200f),
                Sfx(AudioCues.TrapBurst, Seq(Impact, "impactWood_heavy", 0, 4), 0.9f, 0.5f, 0.7f, 3, 0.1f, true, 160f),
                Sfx(AudioCues.FirePool, Seq(Impact, "impactSoft_heavy", 0, 4), 0.7f, 0.3f, 0.45f, 2, 0.3f),
                // 전투 — 맞음·죽음 (묶음 소리)
                Sfx(AudioCues.ArrowHit, Seq(Impact, "impactWood_light", 0, 4), 0.5f, 1.1f, 1.5f, 4, 0.05f),
                Sfx(AudioCues.EnemyHit, Seq(Impact, "impactPunch_medium", 0, 4), 0.5f, 0.8f, 1.1f, 3, 0.08f),
                Sfx(AudioCues.EnemyDeath, Seq(Impact, "impactSoft_medium", 0, 4), 0.7f, 0.6f, 0.9f, 4, 0.08f),
                Sfx(AudioCues.WallHit, Seq(Impact, "impactMining", 0, 4, "{0}_{1:000}"), 0.8f, 0.6f, 0.8f, 3, 0.15f),
                Sfx(AudioCues.WallBreach, Seq(Impact, "impactWood_heavy", 0, 4), 1f, 0.3f, 0.45f, 2, 0.5f, true, 200f),
                // 전투 — 흐름 (뿔나팔 자리는 낮춘 큰 종소리로 대신)
                Sfx(AudioCues.WaveHorn, new[] { "oga/their-coming-generic-horn-sound/theircoming_0.ogg", "oga/their-coming-generic-horn-sound/theircoming3_0.ogg" }, 0.9f, 0.9f, 1f, 1, 2f, false),
                Sfx(AudioCues.BeaconIgnite, Cat(new[] { Iface + "bong_001.ogg" }, Seq(Impact, "impactBell_heavy", 0, 0)), 0.8f, 0.9f, 1f, 1, 1f, false),
                Sfx(AudioCues.BeaconBeamTurn, new[] { Rpg + "creak1.ogg", Rpg + "creak2.ogg", Rpg + "creak3.ogg" }, 0.6f, 0.8f, 1f, 2, 0.5f, false),
                Sfx(AudioCues.CourierRun, Seq(Impact, "footstep_grass", 0, 4), 0.6f, 0.9f, 1.1f, 3, 0.15f),
                // UI
                Sfx(AudioCues.UiClick, Seq(Iface, "click", 1, 3, "{0}_{1:000}"), 0.6f, 0.95f, 1.05f, 4, 0.03f, false, 50f, AudioBus.Ui),
                Sfx(AudioCues.UiConfirm, new[] { Iface + "confirmation_001.ogg", Iface + "confirmation_002.ogg" }, 0.6f, 1f, 1f, 2, 0.1f, false, 50f, AudioBus.Ui),
                Sfx(AudioCues.UiCancel, new[] { Iface + "back_001.ogg", Iface + "close_001.ogg" }, 0.6f, 1f, 1f, 2, 0.1f, false, 50f, AudioBus.Ui),
                Sfx(AudioCues.StoryTick, new[] { Iface + "tick_001.ogg", Iface + "tick_002.ogg", Iface + "tick_004.ogg" }, 0.3f, 1f, 1.3f, 2, 0.04f, false, 50f, AudioBus.Ui),
                // 결과 곡
                Sfx(AudioCues.ResultStinger, new[] { "Music/result_victory.mp3" }, 0.9f, 1f, 1f, 1, 1f, false, 50f, AudioBus.Music),
                Sfx(AudioCues.ResultDefeat, new[] { "Music/result_defeat_laments.mp3" }, 0.9f, 1f, 1f, 1, 1f, false, 50f, AudioBus.Music),
                // 음악 (반복)
                Music(AudioCues.MusicDay, "Music/day_old_tower_inn.mp3", 0.6f),
                Music(AudioCues.MusicNight, "Music/night_prepare_to_fight.mp3", 0.6f),
                Music(AudioCues.MusicBattle, "Music/battle_theme_a.mp3", 0.6f),
                Music(AudioCues.MusicStory, "Music/story_the_hope.mp3", 0.6f),
                // 주변 소리: 알맞은 CC0 파일이 아직 없어 비워 둔다 (열쇠만 만들어 둠)
                new Spec { Cue = AudioCues.AmbWind, Files = new[] { "oga/wind-whoosh-loop/wind_woosh_loop.ogg" }, Volume = 0.5f, PitchMin = 1f, PitchMax = 1f, MaxVoices = 1, Bus = AudioBus.Ambience, Loop = true },
                new Spec { Cue = AudioCues.AmbCrickets, Files = new[] { "oga/crickets-ambient-noise-loopable/crickets_1.mp3" }, Volume = 0.5f, PitchMin = 1f, PitchMax = 1f, MaxVoices = 1, Bus = AudioBus.Ambience, Loop = true },
                new Spec { Cue = AudioCues.AmbCrowd, Files = new[] { "oga/crowd-shoutingspeaking-ambience/crowd_shouting_0.ogg" }, Volume = 0.6f, PitchMin = 1f, PitchMax = 1f, MaxVoices = 1, Bus = AudioBus.Ambience, Loop = true },
            };
        }

        static Spec Music(string cue, string file, float vol) =>
            new Spec { Cue = cue, Files = new[] { file }, Volume = vol, PitchMin = 1f, PitchMax = 1f, MaxVoices = 1, Bus = AudioBus.Music, Loop = true };

        [MenuItem("HLR/Audio/Build Sound Bank")]
        public static void Build()
        {
            Directory.CreateDirectory(AudioDir + "Sfx");
            Directory.CreateDirectory(AudioDir + "Music");
            Directory.CreateDirectory(Path.GetDirectoryName(BankPath));

            var licenses = new StringBuilder();
            licenses.AppendLine("# 소리 파일 출처와 사용 허락 (W24)");
            licenses.AppendLine();
            licenses.AppendLine("모든 파일은 CC0 1.0 (퍼블릭 도메인, 출처 표기 의무 없음, https://creativecommons.org/publicdomain/zero/1.0/) 이다.");
            licenses.AppendLine("원본 목록과 확인 기록은 HLR-audio-src/SOURCES.md. 이 파일은 `HumanLastRampart.App.Editor.AudioBankBuilder`가 만든다 (파일을 바꾸려면 그쪽 표를 고치고 다시 돌린다).");
            licenses.AppendLine();
            licenses.AppendLine("| 파일 | 출처 쪽 | 만든이 | 허락 |");
            licenses.AppendLine("|---|---|---|---|");

            var copied = new HashSet<string>();
            long bytes = 0;
            foreach (Spec spec in Specs())
            {
                foreach (string file in spec.Files)
                {
                    if (file.StartsWith("Music/")) continue;
                    string destName = Path.GetFileName(file);
                    string dest = AudioDir + "Sfx/" + destName;
                    if (!copied.Add(dest)) continue;
                    string src = SourceRoot + file;
                    if (!File.Exists(src)) { Debug.LogError("[소리 은행] 원본 없음: " + src); continue; }
                    File.Copy(src, dest, true);
                    bytes += new FileInfo(dest).Length;
                    if (file.StartsWith("oga/"))
                    {
                        licenses.AppendLine($"| Sfx/{destName} | https://opengameart.org/content/{file.Split('/')[1]} | 쪽 참고 | CC0 |");
                        continue;
                    }

                    string pack = file.StartsWith(Impact) ? KenneyImpactUrl : file.StartsWith(Rpg) ? KenneyRpgUrl : KenneyIfaceUrl;
                    licenses.AppendLine($"| Sfx/{destName} | {pack} | Kenney | CC0 |");
                }
            }

            foreach (var t in Tracks)
            {
                string dest = AudioDir + "Music/" + t.name;
                if (!File.Exists(SourceRoot + t.src)) { Debug.LogError("[소리 은행] 원본 없음: " + t.src); continue; }
                File.Copy(SourceRoot + t.src, dest, true);
                bytes += new FileInfo(dest).Length;
                licenses.AppendLine($"| Music/{t.name} | {t.url} | {t.author} | CC0 |");
            }

            File.WriteAllText(AudioDir + "LICENSES.md", licenses.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            foreach (string path in copied) ApplyImport(path, music: false);
            foreach (var t in Tracks) ApplyImport(AudioDir + "Music/" + t.name, music: true, inMemory: t.name.StartsWith("result_"));

            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            bank.Entries.Clear();
            int filled = 0;
            foreach (Spec spec in Specs())
            {
                var clips = new List<AudioClip>();
                foreach (string file in spec.Files)
                {
                    string path = file.StartsWith("Music/") ? AudioDir + file : AudioDir + "Sfx/" + Path.GetFileName(file);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null) clips.Add(clip); else Debug.LogError("[소리 은행] 가져오기 실패: " + path);
                }

                if (clips.Count > 0) filled++;
                bank.Entries.Add(new SoundBank.Entry
                {
                    Id = spec.Cue, Clips = clips.ToArray(), Volume = spec.Volume, PitchRange = new Vector2(spec.PitchMin, spec.PitchMax),
                    MaxVoices = Mathf.Max(1, spec.MaxVoices), Cooldown = spec.Cooldown, Spatial3D = spec.Spatial,
                    MinDistance = 10f, MaxDistance = spec.MaxDistance > 0f ? spec.MaxDistance : 120f, Bus = spec.Bus, Loop = spec.Loop,
                });
            }

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log($"[소리 은행] 항목 {bank.Entries.Count}개 중 소리 있음 {filled}개, 복사한 용량 {bytes / 1048576f:0.0}MB");
        }

        static void ApplyImport(string path, bool music, bool inMemory = false)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) { Debug.LogError("[소리 은행] 가져오기 설정 실패: " + path); return; }
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? (inMemory ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming) : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.5f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
