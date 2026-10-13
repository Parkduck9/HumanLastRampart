using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;

namespace HumanLastRampart.Core.CombatContracts
{
    // 전투 준비 정보 (C-005 r2·v1.1·v1.2). App이 만들고 Combat이 읽는다.
    // 진행 데이터와 가변 객체를 공유하지 않도록 목록과 내부 객체까지 복사본이다.
    [Serializable]
    public sealed class BattleSetup
    {
        public string BattleRunId;                   // 이번 전투 실행의 고유 ID
        public int BattleNumber;                     // 시작하는 전투 번호
        public string WaveDefId;                     // 이 전투의 웨이브 (C-010 WaveDef)
        public string TerrainId;                     // v1.1: 지형 (P1-C14)

        public Difficulty Difficulty;                // v1.2
        public float EnemyCountMultiplier = 1f;      // v1.2: 현실 1.0, 쉬움 0.5 (D-031 K1)
        public int MaxConcurrentEnemies;             // v1.2: 현실 50,000, 쉬움 25,000
        public WeatherForecast Forecast;             // v1.2: 날씨·밤낮·변화 계획 (D-031 K4)

        public List<HeroState> Heroes = new List<HeroState>();
        public List<SquadState> Squads = new List<SquadState>();
        public List<WallSegmentState> Wall = new List<WallSegmentState>();
        public List<TrapPlacement> Traps = new List<TrapPlacement>();          // v1.1: 이 지형의 함정만
        public List<string> ParticipatingHeroIds = new List<string>();
        public TargetingSettings InitialTargeting = TargetingSettings.CreateDefault();
    }
}