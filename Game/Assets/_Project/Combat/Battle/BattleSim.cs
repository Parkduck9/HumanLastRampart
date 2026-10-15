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
        public static float FunnelFieldHalfWidth = 150f;   // 측정용으로 바꿀 수 있게 static
        float FunnelBase => CastleHalf * 0.95f;
        float FunnelSlope => Mathf.Max(0f, (FunnelFieldHalfWidth - FunnelBase) / Mathf.Max(1f, FieldSize - CastleHalf));               // 성벽 가까이에서 적 크기 비율 — 고블린 0.4m(병사 0.35m와 비슷), 오우거 0.66m
        public const float WallScaleBand = 30f;             // 성벽에서 이 거리(m) 안에서 점점 줄어든다 (8m는 가까운 고블린이 너무 커 보여 늘림, D-047)
        const float SoldierDrawSize = 1.4f * WallFigureScale;
        const float SoldierMaxSpacing = 0.45f;              // 인원이 적으면 이 간격, 많으면 자기 구간에 맞춰 촘촘히
        const float SoldierInset = 0.5f;                    // 성벽 바깥 모서리에서 안쪽으로

        // 병종별 병사 구간 (그리기용): 병종 정의, 병사 배열 시작, 수
        readonly List<(UnitDef unit, int start, int count)> soldierGroups = new List<(UnitDef, int, int)>();
        public IReadOnlyList<(UnitDef unit, int start, int count)> SoldierGroups => soldierGroups;
        const float RetryInterval = 0.25f;
        const float SpawnSpreadSeconds = 12f;   // 한 무리가 나오는 데 걸리는 시간
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
            public float SoldierHp;
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
            FieldSize = terrain != null ? terrain.FieldSize : 320f;
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
            InitTelescope(p);
            InitTraps(defs, p);
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

                int[] counts = ScaledGroupCounts(valid, setup.EnemyCountMultiplier);
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
            foreach (var (group, count) in groups)
            {
                groupIndex++;
                EnemyDef def = defs.Get<EnemyDef>(group.EnemyDefId);
                int k = kindIndex[def.Id];
                GroupPlan plan = attackPlan[groupIndex];
                // 무리가 나오는 데 걸리는 시간: 규칙에 속도가 있으면 수에 맞춰 길게 (작은 무리는 최소 12초)
                float spread = rules != null && rules.SpawnPerSecond > 0f ? Mathf.Max(SpawnSpreadSeconds, count / rules.SpawnPerSecond) : SpawnSpreadSeconds;
                waveGroups.Add((plan.Side, def.IsSpecial));
                groupSlots.Add((cursor[k], count));   // 한 무리의 슬롯은 이어져 있다 (발견 판정용)
                for (int j = 0; j < count; j++)
                {
                    int slot = cursor[k]++;
                    eKind[slot] = k;
                    eGroup[slot] = groupIndex;
                    eMaxHp[slot] = Mathf.Max(1, def.MaxHp);
                    eHp[slot] = eMaxHp[slot];
                    eSpeed[slot] = def.MoveSpeed * random.NextFloat(0.85f, 1.15f);
                    float phase = random.NextFloat();
                    eAnim[slot] = phase;
                    eStop[slot] = CastleHalf + 1.5f + phase * 6f;   // 성벽 앞에 여러 줄로
                    eSide[slot] = (byte)plan.Side;
                    float depth = random.NextFloat(FieldSize - FieldBand, FieldSize);
                    float halfWidth = funnelBase + (depth - CastleHalf) * funnelSlope;   // D-065: 들판에서는 넓게
                    entries.Add(new SpawnEntry
                    {
                        Slot = slot,
                        Time = plan.SpawnAt + random.NextFloat(0f, spread),
                        Side = plan.Side,
                        Lateral = random.NextFloat(-halfWidth, halfWidth),
                        Depth = depth,
                    });
                }
            }

            entries.Sort((a, b) => a.Time.CompareTo(b.Time));
            return entries.ToArray();
        }

        static EnemyKind MakeKind(EnemyDef def)
        {
            bool hasSheet = def.Sheet != null;
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
                SheetFrames = (byte)(hasSheet ? Mathf.Min(PerfLabArt.FrameCount(def.Sheet), 255) : PerfLabArt.FramesPerAction * 2),
            };
        }

        void BuildSoldiers(IDefinitionLookup defs, Allocator p)
        {
            var weaponIndex = new Dictionary<string, int>();
            var weaponList = new List<WeaponKindData>();
            var placed = new List<(SquadState squad, PostDef post, int weapon, float hp)>();
            var placedUnits = new List<UnitDef>();
            foreach (SquadState squad in setup.Squads)
            {
                PostDef post = string.IsNullOrEmpty(squad.PostId) ? null : defs.Get<PostDef>(squad.PostId);
                UnitDef unit = defs.Get<UnitDef>(squad.UnitDefId);
                WeaponDef weapon = unit != null ? defs.Get<WeaponDef>(unit.DefaultWeaponId) : null;
                if (post == null || weapon == null || squad.Count <= 0)
                {
                    continue;   // 미배치 분대·무기 없는 병종은 이번 전투에 서지 않는다
                }

                if (!weaponIndex.TryGetValue(weapon.Id, out int w))
                {
                    w = weaponList.Count;
                    weaponIndex[weapon.Id] = w;
                    weaponList.Add(MakeWeapon(weapon));
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

            var random = new Unity.Mathematics.Random(seed ^ 0x9E3779B9u);
            int index = 0;
            foreach (var (squad, post, w, hp) in placed)
            {
                int side = (int)post.Wall;
                float3 n3 = WallGeometry.Outward(side);
                float3 right = WallGeometry.Right(side);
                float along = (post.PositionAlongWall - 0.5f) * 2f * CastleHalf;
                // 분대는 자기 구간 안에 무조건 한 줄 (D-046). 인원이 많으면 간격을 좁힌다
                float lo = -CastleHalf + 1f, hi = CastleHalf - 1f;
                foreach (float other in postsBySide[side])
                {
                    if (other < along - 0.01f) lo = Mathf.Max(lo, (other + along) * 0.5f + 0.5f);
                    else if (other > along + 0.01f) hi = Mathf.Min(hi, (other + along) * 0.5f - 0.5f);
                }

                float spacing = squad.Count > 1 ? Mathf.Min(SoldierMaxSpacing, (hi - lo) / (squad.Count - 1)) : 0f;
                float halfSpan = (squad.Count - 1) * spacing * 0.5f;
                float center = Mathf.Clamp(along, lo + halfSpan, Mathf.Max(lo + halfSpan, hi - halfSpan));
                squads.Add(new SquadInfo { State = squad, Side = side, FirstSoldier = index, Alive = squad.Count, SoldierHp = hp });
                for (int i = 0; i < squad.Count; i++)
                {
                    float lateral = center - halfSpan + i * spacing;
                    float3 position = n3 * (CastleHalf - SoldierInset) + right * lateral + new float3(0f, WallHeight, 0f);                    sPos[index] = position;
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

            InitRunnerArt(p);
            RefreshSoldierInstances();
        }

        static WeaponKindData MakeWeapon(WeaponDef w) => new WeaponKindData
        {
            Damage = w.Damage,
            Range = w.Range,
            Interval = Mathf.Max(0.05f, w.Interval),
            AreaRadius = w.AreaRadius,
            ProjectileSpeed = Mathf.Max(1f, w.ProjectileSpeed),
            NearDistance = w.NearDistance,
            FarDistance = w.FarDistance,
            AccuracyNear = w.AccuracyNear,
            AccuracyMid = w.AccuracyMid,
            AccuracyFar = w.AccuracyFar,
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
                    if (squad.Side == side) garrison += squad.State.Count;
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

                garrisonDamage[side] += tally[b + SideStat.GarrisonDamage];
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

            Spawn();
            UpdateMessengers(dt);
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
                squads[target] = squad;
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
                Pile = pile,
                CastleHalf = CastleHalf,
                WallHeight = WallHeight,
                DeltaTime = dt,
                FunnelBase = FunnelBase,
                FunnelSlope = FunnelSlope,
            }.Schedule(n, 256);

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
                SoldierIdle = sIdle,
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
                Shots = shotQueue.AsParallelWriter(),
                Grid = grid,
                CastleHalf = CastleHalf,
                DeltaTime = dt,
                RetryInterval = RetryInterval,
                FrameSeed = seed ^ (frameNumber * 2654435761u),
                IntervalMultiplier = IntervalMultiplier,
                FarAccuracyMultiplier = FarAccuracyMultiplier,
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
                Hits = hitQueue.AsParallelWriter(),
                Grid = grid,
                DeltaTime = dt,
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
            }.Schedule(n, 256, h);

            JobHandle shotDraw = new WriteShotInstancesJob
            {
                ShotActive = shotActive,
                ShotPositions = shotPos,
                ShotWeapon = shotWeapon,
                Weapons = weapons,
                Instances = shotInstances,
                Brightness = Brightness,
            }.Schedule(shotCapacity, 512, h);

            frameJob = JobHandle.CombineDependencies(enemyDraw, shotDraw);
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
        }

        public void RefreshSoldierInstances()
        {
            for (int i = 0; i < soldierCount; i++)
            {
                bool runner = WriteRunner(i);   // 달리기 그림으로 그리면 원래 그림은 숨김
                soldierInstances[i] = new PerfLabInstance
                {
                    PosFrame = new float4(sPos[i], 0f),
                    TintSize = sAlive[i] != 0 && !runner ? new float4(new float3(1f, 1f, 1f) * Brightness, SoldierDrawSize) : float4.zero,
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

            foreach (SquadInfo squad in squads)
            {
                int lost = squad.State.Count - squad.Alive;
                if (lost > 0)
                {
                    result.SquadLosses.Add(new SquadLoss { SquadId = squad.State.SquadId, LostCount = lost });
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
            Release(ref groupSpotted);
            Release(ref craters);
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
            if (shotQueue.IsCreated) shotQueue.Dispose();
            if (hitQueue.IsCreated) hitQueue.Dispose();
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
