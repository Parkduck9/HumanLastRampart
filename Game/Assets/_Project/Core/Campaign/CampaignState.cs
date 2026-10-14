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
        public int BattleNumber = 1;                 // 다음(또는 진행 중) 전투 번호. 1부터
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