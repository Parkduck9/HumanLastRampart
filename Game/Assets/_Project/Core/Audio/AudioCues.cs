using System.Collections.Generic;

namespace HumanLastRampart.Core.Audio
{
    // 소리 갈래 (AudioMixer 그룹 이름과 같다). Master는 AudioListener.volume(App)이 맡고, 나머지는 AudioService가 곱한다
    public enum AudioBus { Master = 0, Music = 1, Sfx = 2, Ui = 3, Ambience = 4 }

    // 소리 열쇠 목록 (한 곳). 소리 파일을 구하면 SoundBank 항목의 id에 이 이름을 그대로 적는다.
    // 설명·길이·반복 여부는 Docs/GDD/Audio_Cues_v1.md 와 같은 내용 (시험이 두 목록의 일치를 보장하지는 않으므로 함께 고친다)
    public static class AudioCues
    {
        // 전투 — 무기
        public const string BowVolley = "sfx_bow_volley";
        public const string CrossbowVolley = "sfx_crossbow_volley";
        public const string MusketVolley = "sfx_musket_volley";
        public const string CannonFire = "sfx_cannon_fire";
        public const string CannonImpact = "sfx_cannon_impact";
        public const string MagicCast = "sfx_magic_cast";
        public const string MagicExplosion = "sfx_magic_explosion";
        public const string TrapBurst = "sfx_trap_burst";
        public const string FirePool = "sfx_fire_pool";
        // 전투 — 맞음·죽음 (한 프레임 여러 개는 한 소리로 묶는다)
        public const string ArrowHit = "sfx_arrow_hit";
        public const string EnemyHit = "sfx_enemy_hit";
        public const string EnemyDeath = "sfx_enemy_death";
        public const string WallHit = "sfx_wall_hit";
        public const string WallBreach = "sfx_wall_breach";
        // 전투 — 흐름
        public const string WaveHorn = "sfx_wave_horn";
        public const string BeaconIgnite = "sfx_beacon_ignite";
        public const string BeaconBeamTurn = "sfx_beacon_beam_turn";
        public const string CourierRun = "sfx_courier_run";
        public const string ResultStinger = "sfx_result_stinger";   // 승리
        public const string ResultDefeat = "sfx_result_defeat";     // 패배
        // UI
        public const string UiClick = "ui_click";
        public const string UiConfirm = "ui_confirm";
        public const string UiCancel = "ui_cancel";
        public const string StoryTick = "ui_story_tick";
        // 음악 (반복)
        public const string MusicDay = "music_day";
        public const string MusicNight = "music_night";
        public const string MusicBattle = "music_battle";
        public const string MusicStory = "music_story";
        // 주변 소리 (반복)
        public const string AmbWind = "amb_wind";
        public const string AmbCrickets = "amb_crickets";
        public const string AmbCrowd = "amb_crowd_roar";

        public readonly struct Info
        {
            public readonly string Id;
            public readonly AudioBus Bus;
            public readonly bool Loop;
            public readonly float SuggestedSeconds;
            public readonly string Description;

            public Info(string id, AudioBus bus, bool loop, float seconds, string description)
            {
                Id = id; Bus = bus; Loop = loop; SuggestedSeconds = seconds; Description = description;
            }
        }

        // 전체 목록 (소리 구하기·검사용). 새 열쇠는 위 상수와 여기, 문서 세 곳에 함께 적는다
        public static readonly IReadOnlyList<Info> All = new[]
        {
            new Info(BowVolley, AudioBus.Sfx, false, 0.8f, "활 일제 사격 (여러 발을 한 소리로)"),
            new Info(CrossbowVolley, AudioBus.Sfx, false, 0.7f, "석궁 일제 사격"),
            new Info(MusketVolley, AudioBus.Sfx, false, 1.0f, "머스킷 일제 사격"),
            new Info(CannonFire, AudioBus.Sfx, false, 1.8f, "대포 발사 (낮고 큰 폭음)"),
            new Info(CannonImpact, AudioBus.Sfx, false, 1.5f, "대포알 착탄 폭발"),
            new Info(MagicCast, AudioBus.Sfx, false, 1.0f, "마법 시전 (모으는 소리)"),
            new Info(MagicExplosion, AudioBus.Sfx, false, 1.8f, "마법 폭발"),
            new Info(TrapBurst, AudioBus.Sfx, false, 1.2f, "함정 폭발"),
            new Info(FirePool, AudioBus.Sfx, false, 2.0f, "불바다 생길 때 타오르는 소리"),
            new Info(ArrowHit, AudioBus.Sfx, false, 0.4f, "화살이 땅·적에 맞는 소리 (한 프레임 묶음)"),
            new Info(EnemyHit, AudioBus.Sfx, false, 0.4f, "적이 맞는 소리 (묶음)"),
            new Info(EnemyDeath, AudioBus.Sfx, false, 0.8f, "적이 쓰러지는 소리 (묶음, 많을수록 크게)"),
            new Info(WallHit, AudioBus.Sfx, false, 0.7f, "성벽이 맞는 둔탁한 소리"),
            new Info(WallBreach, AudioBus.Sfx, false, 2.0f, "성벽이 무너지는 소리"),
            new Info(WaveHorn, AudioBus.Sfx, false, 3.0f, "단계 시작 뿔나팔 (선봉·본대·공성·최후 돌격)"),
            new Info(BeaconIgnite, AudioBus.Sfx, false, 2.5f, "봉화 점화"),
            new Info(BeaconBeamTurn, AudioBus.Sfx, true, 2.0f, "봉화 빛줄기 돌리는 윈치 소리 (반복 가능)"),
            new Info(CourierRun, AudioBus.Sfx, false, 0.8f, "전령이 뛰어가는 발소리"),
            new Info(ResultStinger, AudioBus.Music, false, 4.0f, "결과 화면 짧은 곡 (승리)"),
            new Info(ResultDefeat, AudioBus.Music, false, 8.0f, "결과 화면 짧은 곡 (패배)"),
            new Info(UiClick, AudioBus.Ui, false, 0.15f, "단추 누름"),
            new Info(UiConfirm, AudioBus.Ui, false, 0.4f, "확인·결정"),
            new Info(UiCancel, AudioBus.Ui, false, 0.3f, "취소·닫기"),
            new Info(StoryTick, AudioBus.Ui, false, 0.05f, "이야기 글자 한 칸 나옴"),
            new Info(MusicDay, AudioBus.Music, true, 90f, "낮 (준비·지도) 배경 음악"),
            new Info(MusicNight, AudioBus.Music, true, 90f, "밤 전투 배경 음악"),
            new Info(MusicBattle, AudioBus.Music, true, 90f, "낮 전투 배경 음악"),
            new Info(MusicStory, AudioBus.Music, true, 90f, "이야기 화면 배경 음악"),
            new Info(AmbWind, AudioBus.Ambience, true, 30f, "성벽 위 바람"),
            new Info(AmbCrickets, AudioBus.Ambience, true, 30f, "밤 귀뚜라미"),
            new Info(AmbCrowd, AudioBus.Ambience, true, 30f, "적 무리 함성 (적 수에 따라 커짐)"),
        };
    }
}
