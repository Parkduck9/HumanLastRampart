using System;
using System.Collections.Generic;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;

namespace HumanLastRampart.Core.Campaign
{
    // 한 판(새 게임 ~ 패배 엔딩)의 진행 데이터 (C-004 v1~v1.3).
    // App이 새 게임 때 만들고 보관한다. 누가 어떤 필드를 바꾸는지는 C-004 3)·v1.1·v1.2·v1.3 표를 따른다.
    // 정의 데이터는 ID 문자열로만 가리킨다(저장 기능 대비).
    [Serializable]
    public sealed class CampaignState
    {
        public int BattleNumber = 1;                 // 다음(또는 진행 중) 전투 번호. 1부터. 빛을 잇는 자들: 이긴 밤 수 + 1(총 밤 번호). 난이도 자리는 BeaconRules.BattleSlot
        public int Gold;                             // 화폐 — 1회차는 골드 하나 (D-015 D2)
        public Difficulty Difficulty;                // v1.3: App이 새 게임 때 한 번 설정, 판 도중 변경 불가
        public int RngSeed;                          // v1.2: 새 게임 때 App이 정함. 전투·회수·날씨 무작위의 기준

        public List<WallSegmentState> Wall = new List<WallSegmentState>();   // v1.3: 정확히 4개 (WallIds)
        public List<HeroState> Heroes = new List<HeroState>();
        public List<SquadState> Squads = new List<SquadState>();
        public List<TrapPlacement> Traps = new List<TrapPlacement>();
        public List<TrapStockEntry> TrapStock = new List<TrapStockEntry>();  // v1.2: 회수해 보관 중인 함정
        public List<TrapStockEntry> TrapShop = new List<TrapStockEntry>();   // v1.3 (D-050): 상점에 남은 함정 재고. 시작값 App, 보충·구매 Meta.Prep
        public int TrapShopRestockedForBattle;      // v1.3: 상점 보충을 마지막으로 한 전투 번호 (정비 재진입 때 두 번 보충하지 않게)
        public List<TerrainWork> TerrainWorks = new List<TerrainWork>();   // v1.4 초안 (D-077, K-049): 지형 손질. 무작위 지형 자체는 RngSeed로 다시 만든다
        // v1.6 초안 (K-058, D-079): 강화·상점·용병. 바꾸는 쪽 = Meta.Prep(구매·장착·훈련·고용), App(급료·소모품은 전투 준비 때), Meta.Result(용병 손실·군의관)
        public List<OwnedItem> Items = new List<OwnedItem>();
        public List<TrainingState> Training = new List<TrainingState>();
        public List<MercenaryContract> Mercenaries = new List<MercenaryContract>();

        // v1.9 초안 (빛을 잇는 자들 Concept_Beacon_v1 9.3, W8): 요새×밤 구조. 예전 저장에 없으면 요새 1 · 밤 1 · 복구 0 (필드 기본값)
        public int FortressNumber = 1;               // 지금 지키는 요새 (1회차 1~2)
        public int NightNumber = 1;                  // 이 요새의 몇 번째 밤인가 (이긴 밤마다 +1, 졌을 때는 그대로)
        public float BeaconRepair;                   // 등대 복구 0~100 (BeaconRules)
        public int ConsecutiveNightLosses;           // 연속으로 진 밤 수. 이기면 0, 2가 되면 패배 엔딩
        public LampBrightness PlannedLamp;           // 다음 밤 등불 밝기 (정비 화면에서 고른다)
        public int PlannedBeamSide;                  // 다음 밤 처음 빛줄기 방향 (0 북·1 동·2 남·3 서, 정비 마당 등대에서 명령). 예전 저장에 없으면 0 = 북
        public List<GarrisonState> Garrisons = new List<GarrisonState>();   // 점화한 요새에 남긴 수비대
        public string LastNightRunId;                // 마지막으로 반영한 이긴 밤의 전투 실행 ID (두 번 반영 방지)
        public int LastNightFortress;                // 그 밤이 속한 요새·밤 번호 (결과 화면 제목용 — 반영 뒤 FortressNumber·NightNumber는 이미 넘어갔다)
        public int LastNightIndex;
        public float LastNightGain;                 // 그 밤에 늘어난 등대 복구 (결과 화면 표시용)
        public bool LastNightRelit;                  // 그 밤에 요새를 점화했는가
        public int LastNightGarrison;                // 점화로 남긴 수비대 수 (결과 화면 표시용)
        public int BeaconRepairBoughtToday;          // W11: 오늘 낮 산 등대 수리 횟수 (하루 한도, 낮이 시작될 때 0으로 돌린다)
        public int LastSupportGold;                  // 오늘 낮 받은 수비대 지원금 (정비 화면 표시용)
        [NonSerialized] public bool BeaconMapPreview;   // W10: 봉화선 지도가 정비에서 열린 '보기만' 모드 (App이 화면을 열 때 정한다. 저장하지 않는다)

        public TargetingSettings Targeting = TargetingSettings.CreateDefault();
        public StoryRequest NextStory;
        public BattleResult LastBattle;              // 가장 최근 전투 결과. Battle을 열기 직전 App이 비운다

        public BattleSetup ActiveBattleSetup;        // v1.1: App이 전투 준비 때 설정
        public string LastAppliedBattleRunId;        // v1.1: Result가 성공 반영 직후 한 번 기록 (C-007)
        public string LastAppliedRecoveryId;         // v1.2: 회수 결과 반영 직후 한 번 기록 (C-016)

        public int NextInstanceNumber;               // 고유 ID 발급용. NewInstanceId로만 쓴다

        // 이 판 안에서 겹치지 않는 고유 ID. 예: "squad_7", "hero_2", "trap_3", "recovery_4"
        public string NewInstanceId(string prefix)
        {
            NextInstanceNumber++;
            return prefix + "_" + NextInstanceNumber;
        }

        public WallSegmentState FindWall(string wallId) => Wall.Find(w => w.SegmentId == wallId);
        public SquadState FindSquad(string squadId) => Squads.Find(s => s.SquadId == squadId);
        public HeroState FindHero(string instanceId) => Heroes.Find(h => h.InstanceId == instanceId);
    }

    [Serializable]
    public sealed class WallSegmentState
    {
        public string SegmentId;   // v1.3: WallIds 중 하나
        public int MaxHp;
        public int Hp;
        public int BonusMaxHp;     // (개발 중 옛 저장은 Reinforced만 있고 이 값이 0일 수 있다 — 정식 저장 전이라 보정하지 않는다)
        // W11: 성벽 보강으로 늘어난 최대 체력 합 (새 요새로 넘어갈 때 걷어낸다)
        public int Reinforced;     // W11: 성벽 보강으로 산 횟수 (값이 사 갈수록 오른다). 보강은 MaxHp에 바로 더해 저장한다

        public WallSegmentState Clone() => (WallSegmentState)MemberwiseClone();
    }

    [Serializable]
    public sealed class HeroState
    {
        public string InstanceId;  // "hero_" 고유 ID
        public string UnitDefId;
        public int Hp;
        public bool IsAlive = true;
        public string PostId;      // 배치 지점 (C-010 PostDef). 미배치면 null

        public HeroState Clone() => (HeroState)MemberwiseClone();
    }

    [Serializable]
    public sealed class SquadState
    {
        public string SquadId;     // "squad_" 고유 ID. 결과 반영은 이 ID로 맞춘다
        public string UnitDefId;   // 병종. 같은 병종이 여러 분대일 수 있다
        public int Count;          // 인원 수. 0이 되면 반영하는 쪽이 목록에서 뺀다
        public string PostId;      // 배치 지점. 미배치면 null

        public SquadState Clone() => (SquadState)MemberwiseClone();
    }

    public enum StoryKind
    {
        Normal,
        DefeatEnding,
        RunComplete,   // 1회차 마지막 요새 점화 뒤 (W8)
    }

    // 점화한 요새에 남은 수비대 (W8, Concept_Beacon_v1 2.4). 낮마다 인원당 지원금
    [Serializable]
    public sealed class GarrisonState
    {
        public int FortressNumber;
        public int Count;

        public GarrisonState Clone() => (GarrisonState)MemberwiseClone();
    }

    [Serializable]
    public sealed class StoryRequest
    {
        public StoryKind Kind;
        public string StoryId;     // 형식은 C-009 (Codex 제안)

        public StoryRequest Clone() => (StoryRequest)MemberwiseClone();
    }

    // 설치한 함정 (C-008 r5, P1-C14 C-004 v1.2). 위치 세 가지를 따로 둔다.
    [Serializable]
    public sealed class TrapPlacement
    {
        public string TrapId;      // "trap_" 고유 ID. 이동·회수해도 바뀌지 않음
        public string TrapDefId;
        public string TerrainId;   // 함정은 자기 지형에 남는다
        public string SlotId;      // 처음 놓은 칸 — TrapGrid 칸 ID "cell_{성벽}_{열}_{행}" (C-008 r6, D-050). 이동해도 바뀌지 않음
        public float ActualX;      // 실제 위치 (전투 중 이동 반영)
        public float ActualZ;
        public bool Marked;        // 지도에 표시했는가
        public float MarkX;        // 표시 위치 — 자동으로 고쳐지지 않음
        public float MarkZ;

        public TrapPlacement Clone() => (TrapPlacement)MemberwiseClone();
    }

    [Serializable]
    public sealed class TrapStockEntry
    {
        public string TrapDefId;
        public int Count;

        public TrapStockEntry Clone() => (TrapStockEntry)MemberwiseClone();
    }
}