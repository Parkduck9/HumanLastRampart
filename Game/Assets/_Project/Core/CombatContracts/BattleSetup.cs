using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;

namespace HumanLastRampart.Core.CombatContracts
{
    // 전투 준비 정보 (C-005 r2·v1.1·v1.2·v1.5 초안). App이 만들고 Combat이 읽는다.
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
        public bool IsPractice;                      // v1.5 초안: 튜토리얼 연습 전투 (D-063). 성벽 실제 체력이 줄지 않고 결과는 진행 데이터에 반영하지 않는다. 기본 false

        public List<HeroState> Heroes = new List<HeroState>();
        public List<SquadState> Squads = new List<SquadState>();
        public List<WallSegmentState> Wall = new List<WallSegmentState>();
        public List<TrapPlacement> Traps = new List<TrapPlacement>();          // v1.1: 이 지형의 함정만
        public int TerrainSeed;                                                // v1.6 초안 (D-077, K-049): 무작위 지형 씨앗 = 진행 데이터 RngSeed
        public List<TerrainWork> TerrainWorks = new List<TerrainWork>();       // v1.6 초안: 이 지형의 지형 손질만
        public List<string> ParticipatingHeroIds = new List<string>();
        // v1.7 초안 (K-058, D-079): 강화 수치, 용병 무리(분대 모양 — SquadId = 계약 ID, 정원·70% 패배 계산 밖), 이번 전투에 쓰는 소모품
        public BattleBoosts Boosts = new BattleBoosts();
        public List<SquadState> Mercenaries = new List<SquadState>();
        public List<string> ConsumedItemIds = new List<string>();
        public TargetingSettings InitialTargeting = TargetingSettings.CreateDefault();

        // v1.8 초안 (빛을 잇는 자들, Concept_Beacon_v1 9장): 저장에 이 값이 없으면 0 = 보통 밝기·북쪽·1단계·보통 밤
        public LampBrightness LampBrightness;        // 밤을 시작할 때 등불 밝기 (적 수는 이 값으로 정한다. 전투 중 바꾸면 빛줄기만 바뀜)
        public int BeamSide;                         // 처음 빛줄기 방향 (WallGeometry 면 번호, 0 = 북)
        public float LampCountMultiplier;           // W11: 이 밤 등불 밝기의 적 수 배율 (전투 규칙 데이터에서 App이 채운다). 0 = 값이 없음 → BattleSim 기본표
        public int EnemyTier;                        // 적 변형 단계 0 홀림 · 1 물듦 · 2 변형 (9.5)
        public bool IsIgnitionNight;                 // 점화의 밤 — 요새의 마지막 밤 (9.3)
        public int FortressNumber;                   // W8: 화면 표시용 요새·밤 번호. 0 = 정보 없음 (BattleNumber는 전투 1~4번 자리라 밤 번호가 아니다)
        public int NightNumber;
    }

    // 등불 밝기 (9.4). 저장 하위 호환을 위해 보통이 0
    public enum LampBrightness
    {
        Normal,
        Low,
        High,
    }
}