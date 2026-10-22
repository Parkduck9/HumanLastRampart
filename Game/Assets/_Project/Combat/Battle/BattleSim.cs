using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 실제 전투 계산 (CY1-C1: P3-C01 성벽 내구도, P3-C04 자동 공격, P3-C05 웨이브, P3-C11 승패·결과)
    // 프레임 순서: BeginFrame(메인 스레드: 지난 프레임 집계 반영·등장·승패) → Schedule(작업 예약) → Complete(그리기 전)
    // CY2-C2: 시체 더미 — 성벽 앞에서 죽은 적이 칸마다 쌓이고, 높아지면 밟고 올라온다 (Battle.md 3.3 B10)
    // CY2-C3: 성벽별 명령(표적 우선순위)·전령·소리 지르기 — BattleSim.Commands.cs
    // CY2-C4: 날씨·밤낮 효과 — BattleSim.Weather.cs
    // 아직 없는 것: 대포 지도 조준·지도의 전체 명령, 마법, 함정
    public sealed partial class BattleSim : IDisposable
    {
        public const float WallHeight = 4f;
        // 성벽 위 비율 (D-046): 병사·대포는 작게 무조건 1줄. 적은 들판에선 원래 크기, 성벽 앞 가려진 구역에서 이 비율로 줄어든다
        public const float WallFigureScale = 0.25f;
        const float EnemyWallScale = 0.33f;
        // D-065 깔때기: 성벽 앞 반폭(성벽 끝 조금 안쪽) → 들판 끝(반폭 FunnelFieldHalfWidth)까지 넓어진다
        public static bool FrontArcOnly = true;   // D-073 자기 성벽 앞만 쏘기 (측정에서 끄고 비교)
        public static float FunnelFieldHalfWidth = 75f;    // D-071 들판 160m에 맞춰 반으로 (예전 320m에서 150m)   // 측정용으로 바꿀 수 있게 static
        float FunnelBase => CastleHalf * 0.95f;
        float FunnelSlope => Mathf.Max(0f, (FunnelFieldHalfWidth - FunnelBase) / Mathf.Max(1f, FieldSize - CastleHalf));               // 성벽 가까이에서 적 크기 비율 — 고블린 0.4m(병사 0.35m와 비슷), 오우거 0.66m
        public const float WallScaleBand = 30f;             // 성벽에서 이 거리(m) 안에서 점점 줄어든다 (8m는 가까운 고블린이 너무 커 보여 늘림, D-047)
        const float SoldierDrawSize = 1.4f * WallFigureScale;
        const float SoldierInset = 0.5f;                    // 성벽 바깥 모서리에서 안쪽으로 (대포·운용병. 병사 줄은 BattleSim.Slots SlotRowInset)
        const float SoldierRowDepth = 0.3f;                 // 줄 사이 (안쪽으로)
        const float CannonCrewOffset = 0.55f;               // 대포 운용병은 대포 양옆 (대포 1.5배 — 보이는 폭 약 0.6m, 사격구 폭 1.12m)
        const float CannonShooterOffset = 0.4f;             // 불붙이는 병사(오른쪽)는 횃불이 대포 심지에 닿게 조금 더 가까이
        // 대포 운용병 i의 대포 자리 기준 옆 거리 (+ = 바깥을 볼 때 오른쪽). 0 = 쏘는 병사(오른쪽), 1 = 왼쪽, 그 뒤는 바깥으로 번갈아
        static float CannonCrewAlong(int i) => i == 0 ? CannonShooterOffset : (i % 2 == 0 ? 1f : -1f) * CannonCrewOffset * (1 + i / 2);
        const float WalkInDistance = 2f;                    // 달리는 길 ↔ 자기 자리를 이 거리 동안 비스듬히 오간다 (K-039 의견 6)

        // 대포 자리 (쏘는 병사 → 성벽 위 대포 위치). 대포 분대는 대포 양옆에 서므로 쏘는 병사 자리와 다르다
        readonly Dictionary<int, float3> cannonSpots = new Dictionary<int, float3>();
        readonly HashSet<int> alignedCannonShooters = new HashSet<int>();   // 확장 점화 시트의 사격병만 설치 대포의 접점 깊이에 맞춤

        // 병종별 병사 구간 (그리기용): 병종 정의, 병사 배열 시작, 수
        readonly List<(UnitDef unit, int start, int count)> soldierGroups = new List<(UnitDef, int, int)>();
        public IReadOnlyList<(UnitDef unit, int start, int count)> SoldierGroups => soldierGroups;
        const float RetryInterval = 0.25f;
        const float FieldBand = 40f;            // 들판 끝에서 이만큼 안쪽까지 흩어져 나온다
        const float SoldierHpFallback = 10f;
        const float PileBinWidth = 2f;          // 시체 더미 칸 폭
        const float PileDepth = 8f;             // 성벽에서 이 거리 안에서 죽으면 쌓인다
        const float PileClimbRatio = 0.75f;     // 성벽 높이의 3/4부터 밟고 오른다

        public struct SideStatus
        {
            public string WallId;
            public int WallHp;
            public int WallMaxHp;
            public int GarrisonStart;
            public int GarrisonAlive;
            public int AtWall;
            public int Climbing;
            public int OnWall;
            public int InField;
            public float MaxPile;   // 이 벽 앞 가장 높은 시체 더미(m)
        }

        struct SpawnEntry
        {
            public int Slot;
            public float Time;
            public int Side;
            public float Lateral;
            public float Depth;
        }

        struct SquadInfo
        {
            public SquadState State;
            public int Side;
            public int FirstSoldier;
            public int Alive;
            public int Start;         // 이 묶음의 처음 인원 (분대를 10명 묶음으로 나눔, D-072)
            public bool Movable;      // 대포 분대는 옮기지 않는다
            public string UnitName;
            public float SoldierHp;
            public bool Mercenary;    // 용병 무리 (K-058) — 성벽 병력·70% 손실 패배 계산 밖, 옮기지 않음
        }

        // 결과
        public bool Ended { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public DefeatReason DefeatReason { get; private set; }
        public int DefeatSide { get; private set; } = -1;
        public float Elapsed { get; private set; }
        public int TotalEnemies => enemyCount;
        public int Spawned => spawnCursor;
        public int Kills => stats.IsCreated ? stats[BattleStat.Kills] : 0;
        public int Gold => stats.IsCreated ? stats[BattleStat.Gold] : 0;
        public int ActiveShots => stats.IsCreated ? stats[BattleStat.ActiveShots] : 0;
        public int SoldierCount => soldierCount;
        public float CastleHalf { get; }
        public float FieldSize { get; }
        public readonly SideStatus[] Sides = new SideStatus[4];

        // 그리기용 (BattleView가 읽는다)
        public NativeArray<PerfLabInstance> EnemyInstances => enemyInstances;
        public NativeArray<PerfLabInstance> ShotInstances => shotInstances;
        public NativeArray<PerfLabInstance> SoldierInstances => soldierInstances;
        public bool SoldiersDirty { get; set; } = true;
        public IReadOnlyList<(EnemyDef def, int start, int count)> EnemyGroups => enemyGroups;
        public int ShotCapacity => shotCapacity;
        public NativeArray<float> PileHeights => piles;
        public PileSpec Pile => pile;

        readonly BattleSetup setup;
        readonly List<(EnemyDef def, int start, int count)> enemyGroups = new List<(EnemyDef, int, int)>();
        readonly List<SquadInfo> squads = new List<SquadInfo>();
        readonly bool[] garrisonWarned = new bool[4];
        readonly SpawnEntry[] spawnOrder;
        readonly float[] wallHp = new float[4];
        readonly float[] garrisonDamage = new float[4];
        readonly int maxConcurrent;
        readonly uint seed;
        int spawnCursor;
        int enemyCount;
        int soldierCount;
        int shotCapacity;
        uint frameNumber;
        JobHandle frameJob;
        bool scheduled;

        // 적
        NativeArray<float3> ePos;
        NativeArray<float3> eVel;
        NativeArray<byte> eState;
        NativeArray<float> eClimb;
        NativeArray<byte> eSide;
        NativeArray<float> eSpeed;
        NativeArray<float> eStop;
        NativeArray<int> eKind;
        NativeArray<int> eHp;
        NativeArray<int> eMaxHp;
        NativeArray<int> eGen;
        NativeArray<int> eIncoming;
        NativeArray<float> eAnim;
        NativeArray<int> eGroup;            // 웨이브 무리 번호 (망원경, MAP-5)
        NativeArray<byte> eTier;            // 변형 단계 (setup.EnemyTier, 9.5). 등대 빛줄기가 끌 때를 위해 원래 값을 남긴다
        NativeArray<byte> eTierShown;       // 지금 적용 중인 단계 (보너스 꺼지면 0). 그리기 작업이 읽는다
        NativeArray<EnemyKind> kinds;
        NativeArray<PerfLabInstance> enemyInstances;

        // 병사
        NativeArray<float3> sPos;
        NativeArray<byte> sAlive;
        NativeArray<int> sWeapon;
        NativeArray<float> sCooldown;
        NativeArray<byte> sSide;
        NativeArray<byte> sAway;
        NativeArray<byte> sidePriority;   // 성벽별 TargetPriority (명령)
        NativeArray<WeaponKindData> weapons;
        NativeArray<PerfLabInstance> soldierInstances;

        // 격자
        GridSpec grid;
        NativeArray<int> cellOfEnemy;
        NativeArray<int> cellStart;
        NativeArray<int> cellCount;
        NativeArray<int> cellCursor;
        NativeArray<int> sorted;

        // 투사체
        NativeArray<byte> shotActive;
        NativeArray<byte> shotEnded;
        NativeArray<float3> shotPos;
        NativeArray<float3> shotVel;
        NativeArray<float> shotLife;
        NativeArray<EnemyHandle> shotTarget;
        NativeArray<int> shotWeapon;
        NativeArray<int> freeSlots;
        NativeArray<int> freeCount;
        NativeArray<PerfLabInstance> shotInstances;

        // 사건·집계
        NativeQueue<Shot> shotQueue;
        NativeQueue<HitEvent> hitQueue;
        NativeArray<int> stats;
        NativeArray<float> tally;

        // 시체 더미 (면 × 칸)
        PileSpec pile;
        NativeArray<float> piles;

        float garrisonLossRatio;
        BattleRulesDef rules;

        public BattleSim(BattleSetup setup, IDefinitionLookup defs)
        {
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            seed = StableHash(setup.BattleRunId);

            var terrain = defs.Get<TerrainDef>(setup.TerrainId);
            CastleHalf = terrain != null ? terrain.CastleHalfSize : 60f;
            FieldSize = terrain != null ? terrain.FieldSize : 160f;
            var rules = defs.All<BattleRulesDef>();
            garrisonLossRatio = rules.Count > 0 ? rules[0].GarrisonLossDefeatRatio : 0.7f;
            this.rules = rules.Count > 0 ? rules[0] : null;
            pile = new PileSpec
            {
                Bins = Mathf.Max(1, Mathf.CeilToInt(CastleHalf * 2f / PileBinWidth)),
                BinWidth = PileBinWidth,
                Depth = PileDepth,
                PerKill = rules.Count > 0 ? rules[0].CorpseHeightPerKill : 0.02f,
                ClimbStart = WallHeight * PileClimbRatio,
                MaxHeight = WallHeight,
            };
            maxConcurrent = setup.MaxConcurrentEnemies > 0 ? setup.MaxConcurrentEnemies : int.MaxValue;

            var p = Allocator.Persistent;
            spawnOrder = BuildEnemies(defs, p);
            BuildSoldiers(defs, p);
            BuildWalls();
            BuildGridAndShots(p);
            InitCommands(p);
            InitWeather();
            InitCannons(p);
            InitSpells(defs);
            InitBurn(p);   // 불바다 화상 (K-035)
            InitTelescope(p);
            InitTraps(defs, p);
            InitLamp();   // 등불 밝기·빛줄기 (BattleSim.Lamp.cs)
        }

        // ── 준비 ──────────────────────────────────────────────

        // 난이도 배율 (D-031 K1). 무리마다 따로 반올림하면 합이 어긋나므로(쉬움 1,501 등)
        // 전체 = 원래 합 × 배율(반올림)이 되게 나머지가 큰 무리부터 1씩 더한다. 무리마다 최소 1
        public static int[] ScaledGroupCounts(IReadOnlyList<WaveGroup> groups, float multiplier)
        {
            var counts = new int[groups.Count];
            var remainders = new float[groups.Count];
            int raw = 0, floorSum = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                float exact = groups[g].Count * multiplier;
                counts[g] = (int)math.floor(exact);
                remainders[g] = exact - counts[g];
                raw += groups[g].Count;
                floorSum += counts[g];
            }

            int left = (int)math.floor(raw * multiplier + 0.5f) - floorSum;
            var order = new List<int>();
            for (int g = 0; g < groups.Count; g++) order.Add(g);
            order.Sort((a, b) => remainders[b] != remainders[a] ? remainders[b].CompareTo(remainders[a]) : a.CompareTo(b));
            for (int k = 0; k < left && k < order.Count; k++) counts[order[k]]++;

            // 작은 무리(특수 적 1마리 등)는 최소 1 — 그만큼 가장 큰 무리에서 뺀다
            int excess = 0;
            for (int g = 0; g < counts.Length; g++)
            {
                if (counts[g] < 1) { excess += 1 - counts[g]; counts[g] = 1; }
            }

            while (excess > 0)
            {
                int largest = -1;
                for (int g = 0; g < counts.Length; g++) if (counts[g] > 1 && (largest < 0 || counts[g] > counts[largest])) largest = g;
                if (largest < 0) break;
                counts[largest]--;
                excess--;
            }

            return counts;
        }

        SpawnEntry[] BuildEnemies(IDefinitionLookup defs, Allocator p)
        {
            WaveDef wave = defs.Get<WaveDef>(setup.WaveDefId);
            var groups = new List<(WaveGroup group, int count)>();
            var kindIndex = new Dictionary<string, int>();
            var kindList = new List<EnemyKind>();
            var perKindCount = new List<int>();
            var kindDefs = new List<EnemyDef>();
            if (wave != null)
            {
                var valid = new List<WaveGroup>();
                foreach (WaveGroup group in wave.Groups)
                {
                    EnemyDef def = defs.Get<EnemyDef>(group.EnemyDefId);
                    if (def == null || group.Count <= 0)
                    {
                        Debug.LogWarning($"[Battle] 웨이브 {wave.Id}의 적 {group.EnemyDefId}를 찾지 못해 건너뜁니다.");
                        continue;
                    }

                    valid.Add(group);
                }

                int[] counts = ScaledGroupCounts(valid, setup.EnemyCountMultiplier * LampCountMul(setup.LampBrightness));   // 등불 밝기 (시작 때만)
                for (int g = 0; g < valid.Count; g++)
                {
                    WaveGroup group = valid[g];
                    EnemyDef def = defs.Get<EnemyDef>(group.EnemyDefId);
                    int count = counts[g];
                    groups.Add((group, count));
                    if (!kindIndex.ContainsKey(def.Id))
                    {
                        kindIndex[def.Id] = kindList.Count;
                        kindList.Add(MakeKind(def));
                        perKindCount.Add(0);
                        kindDefs.Add(def);
                    }

                    perKindCount[kindIndex[def.Id]] += count;
                }
            }

            PlanAttack(groups);   // 무리마다 나오는 면·시각 (D-053: 정면 북쪽, 우회는 늦게)

            // 종류별로 슬롯을 이어 붙인다 — 그리기를 종류별 한 번씩 하기 위해
            var kindStart = new int[kindList.Count];
            int total = 0;
            for (int k = 0; k < kindList.Count; k++)
            {
                kindStart[k] = total;
                enemyGroups.Add((kindDefs[k], total, perKindCount[k]));
                total += perKindCount[k];
            }

            enemyCount = total;
            int n = Mathf.Max(1, total);
            ePos = new NativeArray<float3>(n, p);
            eVel = new NativeArray<float3>(n, p);
            eState = new NativeArray<byte>(n, p);
            eClimb = new NativeArray<float>(n, p);
            eSide = new NativeArray<byte>(n, p);
            eSpeed = new NativeArray<float>(n, p);
            eStop = new NativeArray<float>(n, p);
            eKind = new NativeArray<int>(n, p);
            eHp = new NativeArray<int>(n, p);
            eMaxHp = new NativeArray<int>(n, p);
            eGen = new NativeArray<int>(n, p);
            eIncoming = new NativeArray<int>(n, p);
            eAnim = new NativeArray<float>(n, p);
            eGroup = new NativeArray<int>(n, p);
            eTier = new NativeArray<byte>(n, p);
            eTierShown = new NativeArray<byte>(n, p);
            enemyInstances = new NativeArray<PerfLabInstance>(n, p);
            kinds = new NativeArray<EnemyKind>(Mathf.Max(1, kindList.Count), p);
            for (int k = 0; k < kindList.Count; k++)
            {
                kinds[k] = kindList[k];
            }

            var random = new Unity.Mathematics.Random(seed);
            var entries = new List<SpawnEntry>(total);
            var cursor = (int[])kindStart.Clone();
            float funnelBase = FunnelBase, funnelSlope = FunnelSlope;
            int groupIndex = -1;
            int vanguardTotal = ResolveVanguardCount(wave);   // 선봉: 첫 무리에서 떼어 가까이 낸다 (BattleSim.Phases.cs)
            foreach (var (group, count) in groups)
            {
                groupIndex++;
                EnemyDef def = defs.Get<EnemyDef>(group.EnemyDefId);
                int k = kindIndex[def.Id];
                GroupPlan plan = attackPlan[groupIndex];
                WavePhase wavePhase = ResolvePhase(group, def);
                float rate = ResolveStreamRate(group);   // 나머지는 이 속도로 흘러나온다
                int vanguard = groupIndex == 0 ? Mathf.Min(count, vanguardTotal) : 0;
                if (vanguard > 0) { vanguardStart = cursor[k]; vanguardSpawned = vanguard; }
                float firstTime = float.MaxValue;
                waveGroups.Add((plan.Side, def.IsSpecial));
                groupSlots.Add((cursor[k], count));   // 한 무리의 슬롯은 이어져 있다 (발견 판정용)
                for (int j = 0; j < count; j++)
                {
                    int slot = cursor[k]++;
                    eKind[slot] = k;
                    eGroup[slot] = groupIndex;
                    int tier = EnemyTierRules.Clamp(setup.EnemyTier);   // 오우거·사다리 고블린 포함 전부 같은 규칙 (9.5)
                    eTier[slot] = (byte)tier;
                    eTierShown[slot] = (byte)tier;
                    eMaxHp[slot] = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, def.MaxHp) * EnemyTierRules.HpMultiplier(tier)));
                    eHp[slot] = eMaxHp[slot];
                    eSpeed[slot] = def.MoveSpeed * EnemyTierRules.SpeedMultiplier(tier) * random.NextFloat(0.85f, 1.15f);
                    float phase = random.NextFloat();
                    eAnim[slot] = phase;
                    eStop[slot] = CastleHalf + 1.5f + phase * 6f;   // 성벽 앞에 여러 줄로
                    eSide[slot] = (byte)plan.Side;
                    bool isVanguard = j < vanguard;
                    float depth = isVanguard ? CastleHalf + random.NextFloat(VanguardNear, VanguardFar) : random.NextFloat(FieldSize - FieldBand, FieldSize);
                    float halfWidth = funnelBase + (depth - CastleHalf) * funnelSlope;   // D-065: 들판에서는 넓게
                    // 선봉은 0~2초에 한꺼번에, 나머지는 무리 시작 시각부터 초당 rate마리씩 차례로 (약간의 흔들림)
                    float time = isVanguard
                        ? random.NextFloat(0f, VanguardSpawnSeconds)
                        : plan.SpawnAt + (j - vanguard + 0.5f + random.NextFloat(-0.4f, 0.4f)) / rate;
                    firstTime = Mathf.Min(firstTime, time);
                    entries.Add(new SpawnEntry
                    {
                        Slot = slot,
                        Time = time,
                        Side = plan.Side,
                        Lateral = random.NextFloat(-halfWidth, halfWidth),
                        Depth = depth,
                    });
                }

                groupPhase.Add(wavePhase);
                groupFirstTime.Add(firstTime);
                groupFirstSpawned.Add(float.MaxValue);
            }

            entries.Sort((a, b) => a.Time.CompareTo(b.Time));
            return entries.ToArray();
        }

        static EnemyKind MakeKind(EnemyDef def)
        {
            bool hasSheet = def.MovingSheet != null;
            return new EnemyKind
            {
                WallDamage = def.WallDamage,
                ClimbSpeed = Mathf.Max(0.05f, def.ClimbSpeed),
                OnWallAttack = def.OnWallAttack,
                RewardGold = def.RewardGold,
                IsSpecial = (byte)(def.IsSpecial ? 1 : 0),
                CarriesLadder = (byte)(def.CarriesLadder ? 1 : 0),
                DrawSize = def.IsSpecial ? (hasSheet ? 2.0f : 1.6f) : (hasSheet ? 1.2f : 0.8f),   // 사용자 의견 8: 적이 너무 커 보임 → 절반 (D-061)
                // 그림이 없으면 임시 그림에 색을 입힌다 (사다리 고블린·오우거는 Codex 정적 데모 그림, M-CX-049)
                Tint = hasSheet
                    ? new float3(1f, 1f, 1f)
                    : (def.IsSpecial ? new float3(0.55f, 0.3f, 0.75f) : new float3(0.85f, 0.25f, 0.2f)),
                // 오우거·사다리 고블린은 걷기 4칸 미리 연결(사용자 검토 전) — 공격 중에도 걷기 칸
                SheetFrames = (byte)(hasSheet ? Mathf.Min(PerfLabArt.FrameCount(def.MovingSheet), 255) : PerfLabArt.FramesPerAction * 2),
                // 따로 붙인 동작 시트 (K-032, BattleSim.Poses.cs). 원래 시트가 없으면 쓰지 않는다 (임시 그림 그대로)
                AttackFrames = hasSheet ? PoseFrames(def.AttackSheet) : (byte)0,
                DeployFrames = hasSheet ? PoseFrames(def.DeploySheet) : (byte)0,
                ClimbFrames = hasSheet ? PoseFrames(def.ClimbSheet) : (byte)0,
                FallFrames = hasSheet ? PoseFrames(def.DeathSheet) : (byte)0,
                HasLadder = (byte)(def.CarriesLadder && def.LadderSheet != null ? 1 : 0),
            };
        }

        void BuildSoldiers(IDefinitionLookup defs, Allocator p)
        {
            var weaponIndex = new Dictionary<string, int>();
            var weaponList = new List<WeaponKindData>();
            var placed = new List<(SquadState squad, PostDef post, int weapon, float hp)>();
            var placedUnits = new List<UnitDef>();
            // 용병 무리(K-058)도 분대처럼 선다 — 계약 ID로 가려 낸다
            var allSquads = new List<SquadState>(setup.Squads);
            if (setup.Mercenaries != null)
                foreach (SquadState merc in setup.Mercenaries) { allSquads.Add(merc); mercenaryIds.Add(merc.SquadId); }
            foreach (SquadState squad in allSquads)
            {
                PostDef post = string.IsNullOrEmpty(squad.PostId) ? null : defs.Get<PostDef>(squad.PostId);
                UnitDef unit = defs.Get<UnitDef>(squad.UnitDefId);
                WeaponDef weapon = unit != null ? defs.Get<WeaponDef>(unit.DefaultWeaponId) : null;
                if (post == null || weapon == null || squad.Count <= 0)
                {
                    continue;   // 미배치 분대·무기 없는 병종은 이번 전투에 서지 않는다
                }

                // 무기 값은 병종 강화(훈련·단체 아이템, K-058)까지 넣어 병종마다 — 용병은 정예 단계라 따로
                bool mercenary = mercenaryIds.Contains(squad.SquadId);
                string weaponKey = weapon.Id + '|' + unit.Id + (mercenary ? "|merc" : "");
                if (!weaponIndex.TryGetValue(weaponKey, out int w))
                {
                    w = weaponList.Count;
                    weaponIndex[weaponKey] = w;
                    weaponList.Add(MakeWeapon(weapon, Boost.UnitFor(unit.Id, mercenary)));
                    weaponUnits.Add(unit);   // 투사체 그림 (K-032)
                }

                placed.Add((squad, post, w, unit.MaxHp > 0 ? unit.MaxHp : SoldierHpFallback));
                placedUnits.Add(unit);
            }

            // 같은 병종 병사를 이어 붙인다 — 그리기를 병종별 한 번씩 (병종마다 그림, M-CX-049). 처음 나온 순서 유지
            var order = new List<int>();
            var unitOrder = new List<UnitDef>();
            foreach (UnitDef u in placedUnits) if (!unitOrder.Contains(u)) unitOrder.Add(u);
            foreach (UnitDef u in unitOrder)
                for (int k = 0; k < placed.Count; k++)
                    if (placedUnits[k] == u) order.Add(k);
            var sortedPlaced = new List<(SquadState squad, PostDef post, int weapon, float hp)>();
            foreach (int k in order) sortedPlaced.Add(placed[k]);
            int groupStart = 0;
            foreach (UnitDef u in unitOrder)
            {
                int count = 0;
                for (int k = 0; k < placed.Count; k++) if (placedUnits[k] == u) count += placed[k].squad.Count;
                soldierGroups.Add((u, groupStart, count));
                groupStart += count;
            }

            placed = sortedPlaced;

            weapons = new NativeArray<WeaponKindData>(Mathf.Max(1, weaponList.Count), p);
            for (int w = 0; w < weaponList.Count; w++)
            {
                weapons[w] = weaponList[w];
            }

            int total = 0;
            foreach (var entry in placed)
            {
                total += entry.squad.Count;
            }

            soldierCount = total;
            int n = Mathf.Max(1, total);
            sPos = new NativeArray<float3>(n, p);
            sAlive = new NativeArray<byte>(n, p);
            sWeapon = new NativeArray<int>(n, p);
            sCooldown = new NativeArray<float>(n, p);
            sSide = new NativeArray<byte>(n, p);
            sAway = new NativeArray<byte>(n, p);
            soldierInstances = new NativeArray<PerfLabInstance>(n, p);

            // 성벽마다 모든 지점 위치(성벽 따라 m) — 분대 구간 = 양옆 지점과의 가운데까지
            var postsBySide = new List<float>[4];
            for (int s = 0; s < 4; s++) postsBySide[s] = new List<float>();
            foreach (PostDef def in defs.All<PostDef>())
            {
                postsBySide[(int)def.Wall].Add((def.PositionAlongWall - 0.5f) * 2f * CastleHalf);
            }

            // 대포 사격구 자리(대포 지점, 운용병이 있을 때)를 먼저 모은다 — 사격구 안에는 돌출부·총안이 없다 (K-045)
            foreach (var (squad, post, w, _) in placed)
                if (weapons[w].AreaRadius > 0f && squad.Count > 0)
                    cannonBays[(int)post.Wall].Add((post.PositionAlongWall - 0.5f) * 2f * CastleHalf);
            AddSpareCannonBays();   // 대포를 옮겨 받을 빈 사격구 (K-060, BattleSim.Haul.cs)
            BuildWallSlots(n);

            var random = new Unity.Mathematics.Random(seed ^ 0x9E3779B9u);
            int index = 0;
            foreach (var (squad, post, w, hp) in placed)
            {
                int side = (int)post.Wall;
                float along = (post.PositionAlongWall - 0.5f) * 2f * CastleHalf;
                // 분대는 자기 구간(양옆 지점과의 가운데까지) 안의 총안부터 선다 (D-046, K-045). 앞 번호가 가까운 앞줄 —
                // 병사는 뒤 번호부터 잃으므로 뒷줄·먼 자리가 먼저 빈다
                float lo = -CastleHalf + 1f, hi = CastleHalf - 1f;
                foreach (float other in postsBySide[side])
                {
                    if (other < along - 0.01f) lo = Mathf.Max(lo, (other + along) * 0.5f);
                    else if (other > along + 0.01f) hi = Mathf.Min(hi, (other + along) * 0.5f);
                }

                // 10명 묶음으로 나눈다 (D-072 지도에서 묶음 이동). 대포 분대는 한 묶음 그대로
                bool cannonSquad = weapons[w].AreaRadius > 0f;
                if (cannonSquad) cannonSpots[index] = WallSpot(side, along, 0);   // 대포는 지점 자리, 운용병은 그 양옆
                int chunk = cannonSquad ? squad.Count : GroupSize;
                UnitDef unitDef = defs.Get<UnitDef>(squad.UnitDefId);
                if (cannonSquad && unitDef != null && PerfLabArt.IsExpandedIgnite(unitDef.IgniteSheet))
                    alignedCannonShooters.Add(index);
                string unitName = unitDef != null && !string.IsNullOrEmpty(unitDef.DisplayName) ? unitDef.DisplayName : "병사";
                for (int c = 0; c < squad.Count; c += chunk)
                {
                    int size = Mathf.Min(chunk, squad.Count - c);
                    bool mercenary = mercenaryIds.Contains(squad.SquadId);
                    squads.Add(new SquadInfo { State = squad, Side = side, FirstSoldier = index + c, Alive = size, Start = size, Movable = !cannonSquad && !mercenary, UnitName = mercenary ? unitName + " 용병" : unitName, SoldierHp = hp, Mercenary = mercenary });
                }
                for (int i = 0; i < squad.Count; i++)
                {
                    float3 position;
                    if (cannonSquad)
                    {
                        // 대포 오른쪽(쏘는 병사 — 불붙이기 그림이 왼쪽으로 횃불을 내밀므로 대포 쪽을 향함)·왼쪽, 더 있으면 그 바깥으로 번갈아
                        // (K-039 의견 2: 대포마다 2명. 사용자 2026-10-20: 왼쪽 병사가 바깥으로 불을 붙이던 것을 오른쪽 병사가 대포 쪽으로)
                        position = CannonCrewSpot(index - i, side, along, i);
                    }
                    else
                    {
                        position = ClaimSlot(index, side, lo, hi, along);   // 총안 자리 (K-045)
                    }

                    sPos[index] = position;
                    sAlive[index] = 1;
                    sSide[index] = (byte)side;
                    // 대포는 분대에 한 문: 맨 앞 병사만 쏘고 나머지는 운용병 (병력 수에는 들어간다, D-031 K2).
                    // 병사는 뒤에서부터 잃으므로 마지막 한 명이 남을 때까지 대포는 쏜다.
                    bool cannon = weapons[w].AreaRadius > 0f;
                    sWeapon[index] = cannon && i > 0 ? -1 : w;
                    sCooldown[index] = random.NextFloat(0f, weapons[w].Interval);
                    index++;
                }
            }

            InitPoseArt(p);
            InitRunnerArt(p);
            RefreshSoldierInstances();
        }

        // ── 성벽 위 자리 (K-039, 병사 자리는 K-045부터 BattleSim.Slots) ──────────

        // 성벽 위 한 자리: 성벽 따라 lateral(m), row = 0 바깥 줄(SoldierInset)부터 안쪽으로 — 대포·운용병 자리
        float3 WallSpot(int side, float lateral, int row) =>
            WallGeometry.Outward(side) * (CastleHalf - SoldierInset - row * SoldierRowDepth) + WallGeometry.Right(side) * lateral + new float3(0f, WallHeight, 0f);

        // 생성과 이동 도착에서 같은 자리를 사용한다. 뒤의 보조병·구형 시트 자리는 유지한다.
        float3 CannonCrewSpot(int shooter, int side, float bay, int crew)
        {
            float3 spot = WallSpot(side, bay + CannonCrewAlong(crew), 0);
            if (crew == 0 && alignedCannonShooters.Contains(shooter))
                spot += WallGeometry.Outward(side) * (CannonArt.InstalledForwardOffset - CannonArt.IgniterDepthClearance);
            return spot;
        }

        // 대포 위치 (쏘는 병사 번호로). 표에 없으면 그 병사 자리
        float3 CannonSpot(int shooter) => cannonSpots.TryGetValue(shooter, out float3 spot) ? spot : sPos[shooter];

        // 달리는 길(통로 안쪽)과 자기 자리 사이: distance = 자리까지 남은(또는 자리에서 온) 거리. WalkInDistance 안이면 자리 쪽으로 비스듬히
        static float3 WalkIn(float3 ring, float3 spot, float distance) =>
            math.lerp(spot, ring, math.smoothstep(0f, WalkInDistance, distance));

        // b = 병종 강화 (없으면 null = 정의 그대로)
        static WeaponKindData MakeWeapon(WeaponDef w, UnitBoost b) => new WeaponKindData
        {
            Damage = Mathf.Max(1, Mathf.RoundToInt(w.Damage * (b != null ? b.DamageMul : 1f))),
            Range = w.Range + (b != null ? b.RangeAdd : 0f),
            Interval = Mathf.Max(0.05f, w.Interval * (b != null ? b.IntervalMul : 1f)),
            AreaRadius = w.AreaRadius > 0f ? w.AreaRadius + (b != null ? b.AreaRadiusAdd : 0f) : 0f,
            BurnChance = b != null && w.AreaRadius <= 0f ? b.BurnChance : 0f,
            ProjectileSpeed = Mathf.Max(1f, w.ProjectileSpeed),
            NearDistance = w.NearDistance,
            FarDistance = w.FarDistance,
            AccuracyNear = Mathf.Clamp01(w.AccuracyNear + (b != null ? b.AccuracyNearAdd : 0f)),
            AccuracyMid = Mathf.Clamp01(w.AccuracyMid + (b != null ? (b.AccuracyNearAdd + b.AccuracyFarAdd) * 0.5f : 0f)),
            AccuracyFar = Mathf.Clamp01(w.AccuracyFar + (b != null ? b.AccuracyFarAdd : 0f)),
            SpreadNear = w.SpreadNear,
            SpreadMid = w.SpreadMid,
            SpreadFar = w.SpreadFar,
        };

        void BuildWalls()
        {
            for (int side = 0; side < 4; side++)
            {
                string id = WallIds.Of((WallSide)side);
                WallSegmentState wall = setup.Wall.Find(w => w.SegmentId == id);
                int garrison = 0;
                foreach (SquadInfo squad in squads)
                {
                    if (squad.Side == side && !squad.Mercenary) garrison += squad.Start;   // 용병은 70% 손실 계산 밖 (D-079 U9)
                }

                wallHp[side] = wall != null ? wall.Hp : 0;
                Sides[side] = new SideStatus
                {
                    WallId = id,
                    WallHp = wall != null ? wall.Hp : 0,
                    WallMaxHp = wall != null ? wall.MaxHp : 0,
                    GarrisonStart = garrison,
                    GarrisonAlive = garrison,
                };
            }
        }

        void BuildGridAndShots(Allocator p)
        {
            float extent = FieldSize + 10f;
            const float cellSize = 4f;
            int cellsPerAxis = (int)math.ceil(extent * 2f / cellSize);
            grid = new GridSpec { MinX = -extent, MinZ = -extent, CellSize = cellSize, Columns = cellsPerAxis, Rows = cellsPerAxis };
            int cells = cellsPerAxis * cellsPerAxis;
            int n = Mathf.Max(1, enemyCount);
            cellOfEnemy = new NativeArray<int>(n, p);
            cellStart = new NativeArray<int>(cells, p);
            cellCount = new NativeArray<int>(cells, p);
            cellCursor = new NativeArray<int>(cells, p);
            sorted = new NativeArray<int>(n, p);

            // 병사 수만큼 + 여유. 대포·화살이 한꺼번에 날아도 넘치지 않게
            shotCapacity = Mathf.Clamp(soldierCount * 4, 1024, 65536);
            shotActive = new NativeArray<byte>(shotCapacity, p);
            shotEnded = new NativeArray<byte>(shotCapacity, p);
            shotPos = new NativeArray<float3>(shotCapacity, p);
            shotVel = new NativeArray<float3>(shotCapacity, p);
            shotLife = new NativeArray<float>(shotCapacity, p);
            shotTarget = new NativeArray<EnemyHandle>(shotCapacity, p);
            shotWeapon = new NativeArray<int>(shotCapacity, p);
            freeSlots = new NativeArray<int>(shotCapacity, p);
            freeCount = new NativeArray<int>(1, p);
            shotInstances = new NativeArray<PerfLabInstance>(shotCapacity, p);
            for (int i = 0; i < shotCapacity; i++)
            {
                freeSlots[i] = shotCapacity - 1 - i;
            }

            freeCount[0] = shotCapacity;
            shotQueue = new NativeQueue<Shot>(p);
            hitQueue = new NativeQueue<HitEvent>(p);
            igniteQueue = new NativeQueue<int>(p);
            stats = new NativeArray<int>(BattleStat.Count, p);
            tally = new NativeArray<float>(4 * SideStat.Count, p);
            piles = new NativeArray<float>(4 * pile.Bins, p);
        }

        // ── 매 프레임 ────────────────────────────────────────

        // 메인 스레드: 지난 프레임 집계를 반영하고, 등장시키고, 승패를 본다. 작업이 끝난 뒤에만 부른다.
        public void BeginFrame(float dt)
        {
            if (Ended)
            {
                return;
            }

            RunPendingCommands();   // 작업이 끝난 지금 메뉴에서 고른 명령을 적용
            Elapsed += dt;
            UpdateLamp(dt);   // 밝기별 켠 시간·빛줄기 각도
            UpdateWeather();
            for (int side = 0; side < 4; side++)
            {
                int b = side * SideStat.Count;
                // 성벽 피해는 모두 여기서만 깎인다 (성벽 앞 공격 → 집계). 연습이면 실제 체력이 줄지 않는다 (D-063).
                // 적 이동·공격 동작과 성벽 위 적의 병사 피해는 그대로
                if (!setup.IsPractice)
                {
                    wallHp[side] = Mathf.Max(0f, wallHp[side] - tally[b + SideStat.WallDamage]);
                }

                garrisonDamage[side] += tally[b + SideStat.GarrisonDamage] * WallGuardMul(side);   // 방패 난간 (K-058)
                KillSoldiersFromDamage(side);

                SideStatus status = Sides[side];
                status.WallHp = Mathf.CeilToInt(wallHp[side]);
                status.AtWall = (int)tally[b + SideStat.AtWall];
                status.Climbing = (int)tally[b + SideStat.Climbing];
                status.OnWall = (int)tally[b + SideStat.OnWall];
                status.InField = (int)tally[b + SideStat.InField];
                float maxPile = 0f;
                for (int k = side * pile.Bins; k < (side + 1) * pile.Bins; k++) maxPile = Mathf.Max(maxPile, piles[k]);
                status.MaxPile = maxPile;
                Sides[side] = status;
            }

            ApplyArrowBurns();   // 불화살 화상 (K-058, BattleSim.Boosts.cs)
            UpdatePhases();      // 최후 돌격 검사 (BattleSim.Phases.cs)
            Spawn();
            UpdateMessengers(dt);
            UpdateGroupMoves(dt);
            UpdateSlotWalks(dt);   // 빈 총안 메우러 걷기 (K-045)
            UpdateFlank(dt);
            UpdateWizard(dt);
            UpdateSpells(dt);
            UpdateTraps(dt);
            CheckEnd();
        }

        void Spawn()
        {
            int alive = spawnCursor - Kills;
            while (spawnCursor < spawnOrder.Length && spawnOrder[spawnCursor].Time <= Elapsed && alive < maxConcurrent)
            {
                SpawnEntry entry = spawnOrder[spawnCursor++];
                float3 n = WallGeometry.Outward(entry.Side);
                float3 right = WallGeometry.Right(entry.Side);
                ePos[entry.Slot] = n * entry.Depth + right * entry.Lateral;
                eVel[entry.Slot] = -n * eSpeed[entry.Slot];
                eState[entry.Slot] = EnemyState.Walking;
                int spawnedGroup = eGroup[entry.Slot];
                if (groupFirstSpawned[spawnedGroup] == float.MaxValue) groupFirstSpawned[spawnedGroup] = Elapsed;   // 실제로 나온 첫 시각 (공성 단계 기준)
                alive++;
            }
        }

        // 성벽 위 적이 준 피해가 병사 한 명 체력만큼 쌓이면 그 벽에서 가장 큰 분대의 병사 한 명을 잃는다
        void KillSoldiersFromDamage(int side)
        {
            while (true)
            {
                int target = -1;
                for (int q = 0; q < squads.Count; q++)
                {
                    if (squads[q].Side == side && squads[q].Alive > 0 && (target < 0 || squads[q].Alive > squads[target].Alive))
                    {
                        target = q;
                    }
                }

                if (target < 0)
                {
                    garrisonDamage[side] = 0f;   // 이 벽에 남은 병사가 없다
                    return;
                }

                SquadInfo squad = squads[target];
                if (garrisonDamage[side] < squad.SoldierHp)
                {
                    return;
                }

                garrisonDamage[side] -= squad.SoldierHp;
                squad.Alive--;
                sAlive[squad.FirstSoldier + squad.Alive] = 0;   // 뒤쪽 병사부터
                MarkSoldierFallen(squad.FirstSoldier + squad.Alive);   // 쓰러짐 그림 (K-032)
                RefillAfterFall(squad.FirstSoldier + squad.Alive);     // 빈 총안을 가까운 뒷줄 병사가 메운다 (K-045)
                squads[target] = squad;
                if (squad.Mercenary) { SoldiersDirty = true; continue; }   // 용병은 성벽 병력 수에 안 들어간다
                SideStatus status = Sides[side];
                status.GarrisonAlive--;
                Sides[side] = status;
                SoldiersDirty = true;
            }
        }

        void CheckEnd()
        {
            // 패배: 성벽 체력 0 또는 그 벽 병력 70% 이상 손실 (D-026 B9, K2 대포 운용병 포함)
            for (int side = 0; side < 4; side++)
            {
                SideStatus status = Sides[side];
                if (status.WallMaxHp > 0 && wallHp[side] <= 0f)
                {
                    End(BattleOutcome.Defeat, DefeatReason.WallDestroyed, side);
                    return;
                }

                if (status.GarrisonStart > 0 && status.GarrisonStart - status.GarrisonAlive >= status.GarrisonStart * garrisonLossRatio)
                {
                    End(BattleOutcome.Defeat, DefeatReason.GarrisonLost, side);
                    return;
                }

                // 패배 선(70% 손실)의 절반 넘게 잃으면 한 번 경고 — 지도에서 묶음을 옮겨 보강할 수 있게 (밤사이 계획 5, D-072)
                if (!garrisonWarned[side] && status.GarrisonStart > 0
                    && status.GarrisonStart - status.GarrisonAlive >= status.GarrisonStart * garrisonLossRatio * 0.6f)
                {
                    garrisonWarned[side] = true;
                    Notice?.Invoke($"위험: {SideNames[side]} 병력이 {status.GarrisonAlive}/{status.GarrisonStart}명 — 다른 성벽 묶음을 보내 보강하세요");
                }
            }

            // 승리: 모든 웨이브를 처치 (Battle.md B2)
            if (spawnCursor >= spawnOrder.Length && Kills >= enemyCount)
            {
                End(BattleOutcome.Victory, DefeatReason.None, -1);
            }
        }

        void End(BattleOutcome outcome, DefeatReason reason, int side)
        {
            Ended = true;
            Outcome = outcome;
            DefeatReason = reason;
            DefeatSide = side;
        }

        // 개발용: 강제로 끝내기 ([시험] 버튼)
        public void ForceEnd(BattleOutcome outcome, DefeatReason reason, int side) => End(outcome, reason, side);

        public void Schedule(float dt, float time)
        {
            frameNumber++;
            int n = enemyCount;
            BeamField beam = CurrentBeam();   // 등대 빛줄기 (BattleSim.Lamp.cs)
            JobHandle h = new EnemyStepJob
            {
                Positions = ePos,
                Velocities = eVel,
                States = eState,
                Climb = eClimb,
                Sides = eSide,
                Speeds = eSpeed,
                StopDistance = eStop,
                KindOf = eKind,
                Kinds = kinds,
                Piles = piles,
                Craters = craters,
                CraterCount = craterCount,
                Beam = beam,
                Pile = pile,
                CastleHalf = CastleHalf,
                WallHeight = WallHeight,
                DeltaTime = dt,
                FunnelBase = FunnelBase,
                FunnelSlope = FunnelSlope,
                Terrain = TerrainData,   // 무작위 지형·지형 손질 (K-049, BattleSim.Terrain.cs)
            }.Schedule(n, 256);
            JobHandle terrainJob = ScheduleTerrain(h, dt);   // 토벽 허물기 (K-049)

            JobHandle tallyJob = new SideTallyJob
            {
                States = eState,
                Sides = eSide,
                KindOf = eKind,
                Kinds = kinds,
                Tally = tally,
                DeltaTime = dt,
            }.Schedule(h);

            h = new BattleAssignCellsJob { Positions = ePos, States = eState, CellOfEnemy = cellOfEnemy, Grid = grid }.Schedule(n, 512, h);
            h = new BattleBuildGridJob
            {
                CellOfEnemy = cellOfEnemy,
                CellStart = cellStart,
                CellCount = cellCount,
                Cursor = cellCursor,
                SortedEnemies = sorted,
            }.Schedule(h);

            h = new SoldierFireJob
            {
                SoldierPositions = sPos,
                SoldierAlive = sAlive,
                SoldierAway = sAway,
                SoldierSide = sSide,
                SoldierWeapon = sWeapon,
                SoldierCooldowns = sCooldown,
                SidePriority = sidePriority,
                SoldierAim = sAim,
                SoldierHasAim = sHasAim,
                SoldierCannonReach = sCannonReach,
                SoldierIdle = sIdle,
                SoldierFireTime = sFireTime,
                SoldierFireLateral = sFireLateral,
                Now = Elapsed,
                Weapons = weapons,
                EnemyPositions = ePos,
                EnemyVelocities = eVel,
                EnemyStates = eState,
                EnemyHp = eHp,
                EnemyMaxHp = eMaxHp,
                EnemyGeneration = eGen,
                EnemyIncoming = eIncoming,
                EnemyKindOf = eKind,
                Kinds = kinds,
                EnemyGroup = eGroup,
                GroupSpotted = groupSpotted,
                CellOfEnemy = cellOfEnemy,
                CellStart = cellStart,
                CellCount = cellCount,
                SortedEnemies = sorted,
                EnemyCovered = eCovered,   // 토벽 엄폐 (K-049)
                Shots = shotQueue.AsParallelWriter(),
                Grid = grid,
                CastleHalf = CastleHalf,
                DeltaTime = dt,
                RetryInterval = RetryInterval,
                FrameSeed = seed ^ (frameNumber * 2654435761u),
                IntervalMultiplier = IntervalMultiplier,
                HeroSide = Boost.HeroWallIntervalMul != 1f ? commanderSide : -1,   // 지휘 휘장 (K-058)
                HeroSideIntervalMultiplier = Boost.HeroWallIntervalMul,
                FarAccuracyMultiplier = FarAccuracyMultiplier,
                FrontArc = (byte)(FrontArcOnly ? 1 : 0),
                MuzzleHeight = SoldierDrawSize * MuzzleHeightRatio,   // 투사체 출발점 (K-035, BattleSim.Burn.cs)
                MuzzleForward = MuzzleForwardDistance,
                CannonMuzzleHeight = CannonMuzzleLift,
                CannonMuzzleForward = CannonMuzzleDistance,
                SoldierFuse = sFuse,                 // 대포 불붙이기 (K-046, BattleSim.Ignite.cs)
                SoldierPendingShot = sPendingShot,
                IgniteSeconds = CannonIgniteTotal,
            }.Schedule(soldierCount, 64, JobHandle.CombineDependencies(h, tallyJob));

            h = new AllocateShotsJob
            {
                Shots = shotQueue,
                FreeSlots = freeSlots,
                FreeCount = freeCount,
                ShotActive = shotActive,
                ShotPositions = shotPos,
                ShotVelocities = shotVel,
                ShotLife = shotLife,
                ShotTargets = shotTarget,
                ShotWeapon = shotWeapon,
                Weapons = weapons,
                EnemyIncoming = eIncoming,
                Stats = stats,
            }.Schedule(h);

            h = new MoveShotsJob
            {
                ShotActive = shotActive,
                ShotPositions = shotPos,
                ShotVelocities = shotVel,
                ShotLife = shotLife,
                ShotTargets = shotTarget,
                ShotWeapon = shotWeapon,
                ShotEnded = shotEnded,
                Weapons = weapons,
                EnemyPositions = ePos,
                EnemyGeneration = eGen,
                CellStart = cellStart,
                CellCount = cellCount,
                SortedEnemies = sorted,
                EnemyCovered = eCovered,   // 토벽 엄폐 (K-049)
                Hits = hitQueue.AsParallelWriter(),
                Grid = grid,
                DeltaTime = dt,
                Ignites = igniteQueue.AsParallelWriter(),   // 불화살 (K-058)
                FrameSeed = seed ^ (frameNumber * 40503u),
            }.Schedule(shotCapacity, 256, h);

            h = new ApplyBattleHitsJob
            {
                Hits = hitQueue,
                EnemyHp = eHp,
                EnemyGeneration = eGen,
                EnemyStates = eState,
                EnemyIncoming = eIncoming,
                KindOf = eKind,
                Kinds = kinds,
                ShotTargets = shotTarget,
                ShotWeapon = shotWeapon,
                Weapons = weapons,
                ShotEnded = shotEnded,
                FreeSlots = freeSlots,
                FreeCount = freeCount,
                Stats = stats,
                EnemyPositions = ePos,
                EnemySides = eSide,
                Piles = piles,
                Pile = pile,
                CastleHalf = CastleHalf,
                Beam = beam,
            }.Schedule(h);

            JobHandle enemyDraw = new WriteBattleEnemyInstancesJob
            {
                Positions = ePos,
                States = eState,
                AnimPhases = eAnim,
                KindOf = eKind,
                Kinds = kinds,
                Sides = eSide,
                EnemyGroup = eGroup,
                TierShown = eTierShown,   // 변형 단계 색·크기 (9.5)
                GroupSpotted = groupSpotted,
                Instances = enemyInstances,
                CastleHalf = CastleHalf,
                WallScale = EnemyWallScale,
                WallScaleBand = WallScaleBand,
                VisibleDistance = VisibleDistance,
                Brightness = Brightness,
                Time = time,
                WalkFps = 6f,
                AttackFps = 7f,
                FramesPerAction = PerfLabArt.FramesPerAction,
                FallStart = eFallStart,
                Ladders = ladderInstances,
                FallSeconds = FallSeconds,
                FallFps = FallFps,
                DeployHeight = DeployHeight,
                LadderSize = PerfLabArt.LadderDrawSize,
                Burn = eBurn,   // 화상 붉은 깜빡임 (K-035)
                TerrainHit = eTerrainHit,   // 토벽 때리는 적은 공격 칸 (K-051)
            }.Schedule(n, 256, h);

            JobHandle shotDraw = new WriteShotInstancesJob
            {
                ShotActive = shotActive,
                ShotPositions = shotPos,
                ShotVelocities = shotVel,
                ShotWeapon = shotWeapon,
                Weapons = weapons,
                Instances = shotInstances,
                Brightness = Brightness,
            }.Schedule(shotCapacity, 512, h);

            frameJob = JobHandle.CombineDependencies(enemyDraw, shotDraw, terrainJob);
            scheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        public void Complete()
        {
            if (scheduled)
            {
                frameJob.Complete();
                scheduled = false;
            }

            UpdateTierBeam();   // 강 밝기 빛 안에서는 변형 단계 효과를 끈다 (작업이 끝난 뒤에만 바꿀 수 있음)
        }

        public void RefreshSoldierInstances()
        {
            for (int i = 0; i < soldierCount; i++)
            {
                bool runner = WriteRunner(i);   // 달리기 그림으로 그리면 원래 그림은 숨김
                bool shown = SoldierPose(i, out float frame) && !runner && (cannonMoves.Count == 0 || !Hauling(i));   // 사격·쓰러짐 칸 (K-032). 대포를 끄는 병사는 끌기 그림으로 (K-060)
                soldierInstances[i] = new PerfLabInstance
                {
                    PosFrame = new float4(sPos[i], frame),
                    TintSize = shown ? new float4(new float3(1f, 1f, 1f) * Brightness, SoldierDrawSize) : float4.zero,
                };
            }
        }

        // ── 결과 (C-006) ─────────────────────────────────────

        public BattleResult BuildResult()
        {
            var result = new BattleResult
            {
                BattleRunId = setup.BattleRunId,
                BattleNumber = setup.BattleNumber,
                Outcome = Outcome,
                DefeatedEnemyCount = Kills,
                DefeatedSpecialCount = stats[BattleStat.SpecialKills],
                EarnedGold = Outcome == BattleOutcome.Victory ? Gold : 0,
                DefeatReason = Outcome == BattleOutcome.Defeat ? DefeatReason : DefeatReason.None,
                DefeatWallId = Outcome == BattleOutcome.Defeat && DefeatSide >= 0 ? WallIds.Of((WallSide)DefeatSide) : null,
            };

            BattleResultHelper.AddUnchangedHeroes(setup, result);

            // 10명 묶음(D-072)을 원래 분대별로 다시 더한다 — 결과 화면·진행 데이터는 분대 단위 그대로
            var lostBySquad = new Dictionary<string, int>();
            var squadOrder = new List<string>();
            foreach (SquadInfo squad in squads)
            {
                string id = squad.State.SquadId;
                if (!lostBySquad.ContainsKey(id)) { lostBySquad[id] = 0; squadOrder.Add(id); }
                lostBySquad[id] += squad.Start - squad.Alive;
            }

            foreach (string id in squadOrder)
            {
                if (lostBySquad[id] > 0)
                {
                    var loss = new SquadLoss { SquadId = id, LostCount = lostBySquad[id] };
                    if (mercenaryIds.Contains(id)) result.MercenaryLosses.Add(loss);   // 용병 손실은 따로 (K-058)
                    else result.SquadLosses.Add(loss);
                }
            }

            for (int side = 0; side < 4; side++)
            {
                WallSegmentState wall = setup.Wall.Find(w => w.SegmentId == Sides[side].WallId);
                int hpAfter = Mathf.Clamp(Mathf.CeilToInt(wallHp[side]), 0, wall != null ? wall.Hp : 0);
                if (wall != null && hpAfter < wall.Hp)
                {
                    result.WallOutcomes.Add(new WallOutcome { SegmentId = wall.SegmentId, HpAfter = hpAfter });
                }
            }

            CopyLampSeconds(result);   // 밝기별 켠 시간 (BattleSim.Lamp.cs)
            AddTrapResults(result);   // 터진 함정·밀려난 함정 (D-050, C-006 v1.1)
            return result;
        }

        static uint StableHash(string text)
        {
            // FNV-1a — 같은 전투 실행 ID면 같은 등장 배치
            uint hash = 2166136261u;
            if (text != null)
            {
                foreach (char c in text)
                {
                    hash = (hash ^ c) * 16777619u;
                }
            }

            return hash == 0 ? 1u : hash;
        }

        public void Dispose()
        {
            Complete();
            Release(ref ePos);
            Release(ref eVel);
            Release(ref eState);
            Release(ref eClimb);
            Release(ref eSide);
            Release(ref eSpeed);
            Release(ref eStop);
            Release(ref eKind);
            Release(ref eHp);
            Release(ref eMaxHp);
            Release(ref eGen);
            Release(ref eIncoming);
            Release(ref eAnim);
            Release(ref eGroup);
            Release(ref eTier);
            Release(ref eTierShown);
            Release(ref groupSpotted);
            Release(ref craters);
            ReleaseTerrain();   // K-049
            Release(ref kinds);
            Release(ref enemyInstances);
            Release(ref sPos);
            Release(ref sAlive);
            Release(ref sWeapon);
            Release(ref sCooldown);
            Release(ref sSide);
            Release(ref sAway);
            Release(ref sidePriority);
            Release(ref sAim);
            Release(ref sHasAim);
            Release(ref sCannonReach);
            Release(ref sIdle);
            Release(ref weapons);
            Release(ref soldierInstances);
            Release(ref runnerInstances);
            Release(ref cellOfEnemy);
            Release(ref cellStart);
            Release(ref cellCount);
            Release(ref cellCursor);
            Release(ref sorted);
            Release(ref shotActive);
            Release(ref shotEnded);
            Release(ref shotPos);
            Release(ref shotVel);
            Release(ref shotLife);
            Release(ref shotTarget);
            Release(ref shotWeapon);
            Release(ref freeSlots);
            Release(ref freeCount);
            Release(ref shotInstances);
            Release(ref stats);
            Release(ref tally);
            Release(ref piles);
            ReleasePoseArt();
            ReleaseBurn();
            if (shotQueue.IsCreated) shotQueue.Dispose();
            if (hitQueue.IsCreated) hitQueue.Dispose();
            if (igniteQueue.IsCreated) igniteQueue.Dispose();
        }

        static void Release<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }
    }

    // 시험 결과와 실제 결과가 함께 쓰는 결과 조각
    public static class BattleResultHelper
    {
        // 참가 영웅마다 정확히 한 항목 (C-006). 영웅은 아직 전투에 서지 않으므로 시작 상태 그대로
        public static void AddUnchangedHeroes(BattleSetup setup, BattleResult result)
        {
            foreach (string heroId in setup.ParticipatingHeroIds)
            {
                HeroState hero = setup.Heroes.Find(h => h.InstanceId == heroId);
                result.HeroOutcomes.Add(new HeroOutcome
                {
                    InstanceId = heroId,
                    HpAfter = hero != null ? hero.Hp : 0,
                    IsAliveAfter = hero != null && hero.IsAlive,
                });
            }
        }
    }
}
