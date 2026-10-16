using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using HumanLastRampart.Combat.PerfLab;

// 실제 전투의 데이터 처리 (D-024 구조: 배열 + Burst Job + 공간 격자 + 투사체 풀 + 일괄 사건)
// PerfLab과 같은 흐름이지만 4면 성벽, 등장 시각, 사다리 등반, 성벽 위 싸움, 죽은 적은 다시 태어나지 않음.
namespace HumanLastRampart.Combat.Battle
{
    // 적 상태
    public static class EnemyState
    {
        public const byte Waiting = 0;    // 아직 등장 전
        public const byte Walking = 1;    // 들판에서 성벽으로
        public const byte AtWall = 2;     // 성벽 앞에서 성벽을 공격
        public const byte Climbing = 3;   // 사다리를 오르는 중
        public const byte OnWall = 4;     // 성벽 위에서 병사와 싸움
        public const byte Dead = 5;

        public static bool IsAlive(byte state) => state >= Walking && state <= OnWall;
    }

    // 성벽 4면의 방향. 0 북(+z) 1 동(+x) 2 남(-z) 3 서(-x) — WallSide 순서와 같다
    public static class WallGeometry
    {
        // 성 안에서 바깥을 보는 방향
        public static float3 Outward(int side)
        {
            switch (side)
            {
                case 0: return new float3(0f, 0f, 1f);
                case 1: return new float3(1f, 0f, 0f);
                case 2: return new float3(0f, 0f, -1f);
                default: return new float3(-1f, 0f, 0f);
            }
        }

        // 바깥을 볼 때 오른쪽
        public static float3 Right(int side)
        {
            float3 n = Outward(side);
            return new float3(n.z, 0f, -n.x);
        }

        // 시체 더미 칸: 성벽을 따라 BinWidth 폭으로 나눈 칸 번호 (면 × Bins + 칸)
        public static int PileIndex(float3 position, int side, float castleHalf, float binWidth, int bins)
        {
            float lateral = math.dot(position, Right(side)) + castleHalf;
            int bin = math.clamp((int)math.floor(lateral / binWidth), 0, bins - 1);
            return side * bins + bin;
        }
    }

    // 시체 더미 설정 (Battle.md 3.3 B10)
    public struct PileSpec
    {
        public int Bins;            // 면마다 칸 수
        public float BinWidth;      // 칸 폭(m)
        public float Depth;         // 성벽에서 이 거리 안에서 죽은 적만 쌓인다(m)
        public float PerKill;       // 한 마리당 높이(m) — BattleRulesDef.corpseHeightPerKill
        public float ClimbStart;    // 이 높이부터 밟고 올라온다(m)
        public float MaxHeight;     // 성벽 높이까지만
    }

    // 적 종류별 값 (EnemyDef에서 복사)
    public struct EnemyKind
    {
        public float WallDamage;     // 초당
        public float ClimbSpeed;     // m/초
        public float OnWallAttack;   // 초당 병사 피해
        public int RewardGold;
        public byte IsSpecial;
        public byte CarriesLadder;
        public float DrawSize;
        public float3 Tint;
        public byte SheetFrames;     // 시트 칸 수: 1 = 정적(0번만), 4 = 걷기만(공격도 걷기 칸), 8 = 걷기 4 + 공격 4
        // 따로 붙인 동작 시트 칸 수 (K-032, 0 = 없음). 그리기 구간은 PerfLabArt.AttackFrame0 등
        public byte AttackFrames;
        public byte DeployFrames;
        public byte ClimbFrames;
        public byte FallFrames;
        public byte HasLadder;       // 오르는 동안 사다리 소품을 그린다
    }

    // 무기 종류별 값 (WeaponDef에서 복사)
    public struct WeaponKindData
    {
        public int Damage;
        public float Range;
        public float Interval;
        public float AreaRadius;
        public float ProjectileSpeed;
        public float NearDistance;
        public float FarDistance;
        public float AccuracyNear;
        public float AccuracyMid;
        public float AccuracyFar;
        public float SpreadNear;
        public float SpreadMid;
        public float SpreadFar;
    }

    public struct Shot
    {
        public float3 From;
        public float3 To;
        public EnemyHandle Target;
        public int Weapon;
    }

    // 통계 칸 번호
    public static class BattleStat
    {
        public const int Kills = 0;
        public const int SpecialKills = 1;
        public const int Gold = 2;
        public const int ActiveShots = 3;
        public const int PoolOverflow = 4;
        public const int Count = 5;
    }

    // 면별 집계 칸 번호 (면 × SideStat.Count)
    public static class SideStat
    {
        public const int WallDamage = 0;      // 이번 프레임 성벽 피해 (실수)
        public const int GarrisonDamage = 1;  // 이번 프레임 병사 피해 (실수)
        public const int AtWall = 2;          // 성벽 앞 적 수
        public const int Climbing = 3;
        public const int OnWall = 4;
        public const int InField = 5;         // 들판에서 오는 적
        public const int Count = 6;
    }

    // ① 적 이동·등반 (적별 병렬)
    [BurstCompile(CompileSynchronously = true)]
    public struct EnemyStepJob : IJobParallelFor
    {
        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<byte> States;
        public NativeArray<float> Climb;
        [ReadOnly] public NativeArray<byte> Sides;
        [ReadOnly] public NativeArray<float> Speeds;
        [ReadOnly] public NativeArray<float> StopDistance;
        [ReadOnly] public NativeArray<int> KindOf;
        [ReadOnly] public NativeArray<EnemyKind> Kinds;
        [ReadOnly] public NativeArray<float> Piles;
        [ReadOnly] public NativeArray<float4> Craters;   // 터진 함정 구덩이 (x, z, 반경², 속도 배율) — D-050
        public int CraterCount;
        public PileSpec Pile;
        public float CastleHalf;
        public float WallHeight;
        public float DeltaTime;
        public float FunnelBase;    // 성벽에서 적이 퍼지는 반폭(m)
        public float FunnelSlope;   // 성벽에서 1m 멀어질 때 늘어나는 반폭

        public void Execute(int i)
        {
            byte state = States[i];
            if (state == EnemyState.AtWall)
            {
                // 성벽 앞 적: 시체 더미 위에 서고, 더미가 충분히 높으면 밟고 오르기 시작한다
                float3 n = WallGeometry.Outward(Sides[i]);
                float3 position = Positions[i];
                float pile = Piles[WallGeometry.PileIndex(position, Sides[i], CastleHalf, Pile.BinWidth, Pile.Bins)];
                if (pile >= Pile.ClimbStart)
                {
                    position += n * (CastleHalf + 0.6f - math.dot(position, n));
                    position.y = pile;
                    Climb[i] = pile;
                    States[i] = EnemyState.Climbing;
                }
                else
                {
                    position.y = math.dot(position, n) - CastleHalf <= Pile.Depth ? pile : 0f;
                }

                Positions[i] = position;
                return;
            }

            if (state == EnemyState.Walking)
            {
                float3 n = WallGeometry.Outward(Sides[i]);
                // 구덩이 안이면 느려진다 (들어올 때·나갈 때 모두, 겹치면 가장 느린 값)
                float slow = 1f;
                for (int c = 0; c < CraterCount; c++)
                {
                    float4 crater = Craters[c];
                    float dx = Positions[i].x - crater.x, dz = Positions[i].z - crater.y;
                    if (dx * dx + dz * dz <= crater.z) slow = math.min(slow, crater.w);
                }

                float speed = Speeds[i] * slow;
                // 깔때기 (D-065): 들판에 넓게 퍼져 있다가 성벽으로 모여든다. 깔때기 폭 안에서의 비율을 지키며 좌우로 좁혀 온다
                float3 r = WallGeometry.Right(Sides[i]);
                float fromWall = math.dot(Positions[i], n) - CastleHalf;
                float width = FunnelBase + math.max(0f, fromWall) * FunnelSlope;
                float lateralSpeed = -speed * FunnelSlope * math.dot(Positions[i], r) / math.max(width, 0.1f);
                float3 velocity = -n * speed + r * lateralSpeed;
                float3 position = Positions[i] + velocity * DeltaTime;
                Velocities[i] = velocity;
                float distance = math.dot(position, n);
                if (distance <= StopDistance[i])
                {
                    position += n * (StopDistance[i] - distance);
                    Velocities[i] = float3.zero;
                    States[i] = Kinds[KindOf[i]].CarriesLadder != 0 ? EnemyState.Climbing : EnemyState.AtWall;
                    if (States[i] == EnemyState.Climbing)
                    {
                        // 사다리는 성벽 바로 앞에 건다
                        float d = math.dot(position, n);
                        position += n * (CastleHalf + 0.6f - d);
                    }
                }

                Positions[i] = position;
                return;
            }

            if (state == EnemyState.Climbing)
            {
                float climb = Climb[i] + Kinds[KindOf[i]].ClimbSpeed * DeltaTime;
                Climb[i] = climb;
                float3 position = Positions[i];
                if (climb >= WallHeight)
                {
                    // 성벽 위로 올라섬
                    float3 n = WallGeometry.Outward(Sides[i]);
                    float d = math.dot(position, n);
                    position += n * (CastleHalf - 0.4f - d);
                    position.y = WallHeight;
                    States[i] = EnemyState.OnWall;
                }
                else
                {
                    position.y = climb;
                }

                Positions[i] = position;
            }
        }
    }

    // ② 면별 집계 (단일): 성벽 피해·병사 피해·상태별 수
    [BurstCompile(CompileSynchronously = true)]
    public struct SideTallyJob : IJob
    {
        [ReadOnly] public NativeArray<byte> States;
        [ReadOnly] public NativeArray<byte> Sides;
        [ReadOnly] public NativeArray<int> KindOf;
        [ReadOnly] public NativeArray<EnemyKind> Kinds;
        public NativeArray<float> Tally;   // 4 × SideStat.Count
        public float DeltaTime;

        public void Execute()
        {
            for (int t = 0; t < Tally.Length; t++)
            {
                Tally[t] = 0f;
            }

            for (int i = 0; i < States.Length; i++)
            {
                byte state = States[i];
                if (!EnemyState.IsAlive(state))
                {
                    continue;
                }

                int b = Sides[i] * SideStat.Count;
                EnemyKind kind = Kinds[KindOf[i]];
                switch (state)
                {
                    case EnemyState.Walking:
                        Tally[b + SideStat.InField] += 1f;
                        break;
                    case EnemyState.AtWall:
                        Tally[b + SideStat.AtWall] += 1f;
                        Tally[b + SideStat.WallDamage] += kind.WallDamage * DeltaTime;
                        break;
                    case EnemyState.Climbing:
                        Tally[b + SideStat.Climbing] += 1f;
                        break;
                    case EnemyState.OnWall:
                        Tally[b + SideStat.OnWall] += 1f;
                        Tally[b + SideStat.GarrisonDamage] += kind.OnWallAttack * DeltaTime;
                        break;
                }
            }
        }
    }

    // ③ 격자 칸 (살아 있는 적만)
    [BurstCompile(CompileSynchronously = true)]
    public struct BattleAssignCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> States;
        [WriteOnly] public NativeArray<int> CellOfEnemy;
        public GridSpec Grid;

        public void Execute(int i)
        {
            CellOfEnemy[i] = EnemyState.IsAlive(States[i]) ? Grid.IndexOf(Grid.CellOf(Positions[i])) : -1;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct BattleBuildGridJob : IJob
    {
        [ReadOnly] public NativeArray<int> CellOfEnemy;
        public NativeArray<int> CellStart;
        public NativeArray<int> CellCount;
        public NativeArray<int> Cursor;
        public NativeArray<int> SortedEnemies;

        public void Execute()
        {
            for (int c = 0; c < CellCount.Length; c++)
            {
                CellCount[c] = 0;
            }

            for (int i = 0; i < CellOfEnemy.Length; i++)
            {
                int cell = CellOfEnemy[i];
                if (cell >= 0)
                {
                    CellCount[cell]++;
                }
            }

            int running = 0;
            for (int c = 0; c < CellCount.Length; c++)
            {
                CellStart[c] = running;
                Cursor[c] = running;
                running += CellCount[c];
            }

            for (int i = 0; i < CellOfEnemy.Length; i++)
            {
                int cell = CellOfEnemy[i];
                if (cell >= 0)
                {
                    SortedEnemies[Cursor[cell]] = i;
                    Cursor[cell]++;
                }
            }
        }
    }

    // ④ 병사별 표적 고르기·발사 (병렬)
    // 성벽 위 적(바로 앞에서 병사를 치는 적)은 언제나 먼저. 그다음은 그 성벽의 명령(Battle.md 3.1-1, C-011, D-039):
    // 체력 낮은/높은 적부터, 가까운 적부터(성벽에 가까운), 오르는 적부터, 특수 적 먼저, 밀집한 곳 쏘기.
    // 대포는 명령을 따르지 않고 밀집한 곳을 쏜다 (D-039 C16-3 — 지도 조준이 생기기 전).
    // 명중률은 거리 구간별(D-028 C7). 빗나가면 조준점이 퍼짐만큼 흩어진다.
    [BurstCompile(CompileSynchronously = true)]
    public struct SoldierFireJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> SoldierPositions;
        [ReadOnly] public NativeArray<byte> SoldierAlive;
        [ReadOnly] public NativeArray<byte> SoldierAway;      // 전령으로 뛰는 중이면 쏘지 않는다 (C16-2)
        [ReadOnly] public NativeArray<byte> SoldierSide;
        [ReadOnly] public NativeArray<int> SoldierWeapon;
        public NativeArray<float> SoldierCooldowns;
        [ReadOnly] public NativeArray<float3> SoldierAim;     // 대포 조준점 (MAP-3)
        [ReadOnly] public NativeArray<byte> SoldierHasAim;
        public NativeArray<byte> SoldierIdle;                 // 조준점 근처에 적이 없어 쉼
        public NativeArray<float> SoldierFireTime;            // 마지막으로 쏜 시각 (사격 그림, K-032)
        public float Now;
        [ReadOnly] public NativeArray<byte> SidePriority;     // 성벽별 TargetPriority
        [ReadOnly] public NativeArray<WeaponKindData> Weapons;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<float3> EnemyVelocities;
        [ReadOnly] public NativeArray<byte> EnemyStates;
        [ReadOnly] public NativeArray<int> EnemyHp;
        [ReadOnly] public NativeArray<int> EnemyMaxHp;
        [ReadOnly] public NativeArray<int> EnemyGeneration;
        [ReadOnly] public NativeArray<int> EnemyIncoming;   // 이미 날아가는 중인 피해 (헛방 줄이기)
        [ReadOnly] public NativeArray<int> EnemyKindOf;
        [ReadOnly] public NativeArray<EnemyKind> Kinds;
        [ReadOnly] public NativeArray<int> EnemyGroup;        // 망원경으로 확인한 무리는 먼 거리 명중 감소 없음 (Battle.md 6.4)
        [ReadOnly] public NativeArray<byte> GroupSpotted;
        [ReadOnly] public NativeArray<int> CellOfEnemy;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> SortedEnemies;
        public NativeQueue<Shot>.ParallelWriter Shots;
        public GridSpec Grid;
        public float CastleHalf;
        public float DeltaTime;
        public float RetryInterval;
        public uint FrameSeed;
        public float IntervalMultiplier;      // 비: 공격 간격 길어짐 (공격 속도 −20%, D-028 C4)
        public float FarAccuracyMultiplier;   // 안개·밤: 먼 거리 명중 감소 (Battle.md 6.4)
        public byte FrontArc;                 // 1 = 자기 성벽 앞 90도·각도 순위(D-073). 0 = 예전처럼 둘레 전부 (측정 비교용)

        // TargetPriority 순서 (Core.Campaign.TargetPriority와 같게)
        const byte LowestHpRatio = 0, HighestHpRatio = 1, Nearest = 2, ClimbingFirst = 3, SpecialFirst = 4, DensestArea = 5;
        const float Cos30 = 0.8660254f, Cos60 = 0.5f;

        // 체력 비율을 10% 띠로 — 97%와 95%를 같은 적으로 보고 병사들이 나눠 쏘게 (D-073)
        static float BandOf(float ratio) => math.floor(ratio * 10f);

        public void Execute(int s)
        {
            if (SoldierAlive[s] == 0 || SoldierWeapon[s] < 0 || SoldierAway[s] != 0)
            {
                return;   // 죽은 병사, 대포 운용병(쏘지 않음), 전령
            }

            float cooldown = SoldierCooldowns[s] - DeltaTime;
            if (cooldown > 0f)
            {
                SoldierCooldowns[s] = cooldown;
                return;
            }

            WeaponKindData weapon = Weapons[SoldierWeapon[s]];
            if (weapon.AreaRadius > 0f && SoldierHasAim[s] != 0)
            {
                FireAtAim(s, weapon);
                return;
            }

            int side = SoldierSide[s];
            byte mode = weapon.AreaRadius > 0f ? DensestArea : SidePriority[side];
            float3 wallOut = WallGeometry.Outward(side);
            float3 from = SoldierPositions[s];
            float rangeSq = weapon.Range * weapon.Range;
            int2 minCell = Grid.CellOf(from - new float3(weapon.Range, 0f, weapon.Range));
            int2 maxCell = Grid.CellOf(from + new float3(weapon.Range, 0f, weapon.Range));

            int best = -1;
            int bestRank = int.MaxValue;
            float bestKey = float.MaxValue;
            float bestDistSq = float.MaxValue;
            uint bestScatter = uint.MaxValue;
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int cell = Grid.IndexOf(new int2(x, z));
                    int start = CellStart[cell];
                    int end = start + CellCount[cell];
                    for (int k = start; k < end; k++)
                    {
                        int e = SortedEnemies[k];
                        float3 d = EnemyPositions[e] - from;
                        float distSq = d.x * d.x + d.z * d.z;
                        if (distSq > rangeSq)
                        {
                            continue;
                        }

                        // 날아가는 피해만으로 죽을 적은 건너뛴다 — 모두가 다친 한 마리만 노리는 헛방 방지
                        int remaining = EnemyHp[e] - EnemyIncoming[e];
                        if (remaining <= 0)
                        {
                            continue;
                        }

                        byte state = EnemyStates[e];

                        // 자기 성벽 앞만 쏜다 (D-073): 성벽 바깥 방향에서 좌우 30도 안 = 1순위, 60도 = 2순위, 90도 = 3순위,
                        // 90도 넘게(옆·뒤 = 다른 성벽 쪽)는 쏘지 않는다. 성벽 위 적은 통로 옆이라 거의 90도 — 조금 넘어도 허용
                        float flat = math.sqrt(math.max(distSq, 0.0001f));
                        float cosine = (d.x * wallOut.x + d.z * wallOut.z) / flat;
                        bool onWall = state == EnemyState.OnWall;
                        if (FrontArc != 0 && cosine <= (onWall ? -0.2f : 0f))
                        {
                            continue;
                        }

                        // 성벽 앞에 붙은 적·오르는 적은 바로 발밑이라 옆으로 몇 m만 비켜도 60~90도가 된다 —
                        // 각도 띠로 미루면 멀리 정면만 쏘다 성벽을 내준다(측정: 쉬움 4·현실 3 패배). 성벽 앞(90도 안)이면 1순위 띠
                        bool atWall = state == EnemyState.AtWall || state == EnemyState.Climbing;
                        int band = FrontArc == 0 || atWall || cosine >= Cos30 ? 0 : cosine >= Cos60 ? 1 : 2;
                        float ratio = (float)remaining / math.max(1, EnemyMaxHp[e]);
                        // 순위: 성벽 위 적(0) → 각도 띠마다 [명령이 고른 적, 나머지] (1 + 띠×2 + 0/1)
                        int favored = 1;
                        float key;
                        switch (mode)
                        {
                            case HighestHpRatio:
                                key = -BandOf(ratio);
                                break;
                            case Nearest:
                                key = math.floor((math.dot(EnemyPositions[e], wallOut) - CastleHalf) / 3f);   // 성벽에 가까운 적 (3m 띠)
                                break;
                            case ClimbingFirst:
                                if (state == EnemyState.Climbing) favored = 0;
                                key = BandOf(ratio);
                                break;
                            case SpecialFirst:
                                if (Kinds[EnemyKindOf[e]].IsSpecial != 0) favored = 0;
                                key = BandOf(ratio);
                                break;
                            case DensestArea:
                                key = -CellCount[CellOfEnemy[e]];   // 같은 칸에 적이 많을수록
                                break;
                            default:
                                key = BandOf(ratio);
                                break;
                        }

                        int rank = onWall ? 0 : 1 + band * 2 + favored;
                        // 몰림 방지 (D-073): 키를 띠로 묶어 비슷한 적은 같은 값 → 병사마다 다른 순서(해시)로 골라 흩어 쏜다
                        uint scatter = math.hash(new uint2((uint)s, (uint)e));
                        if (rank < bestRank
                            || (rank == bestRank && (key < bestKey || (key == bestKey && scatter < bestScatter))))
                        {
                            best = e;
                            bestRank = rank;
                            bestKey = key;
                            bestDistSq = distSq;
                            bestScatter = scatter;
                        }
                    }
                }
            }
            if (best < 0)
            {
                SoldierCooldowns[s] = RetryInterval;
                return;
            }

            float distance = math.sqrt(bestDistSq);
            float3 aim = EnemyPositions[best] + EnemyVelocities[best] * (distance / math.max(weapon.ProjectileSpeed, 1f));

            float accuracy;
            float spread;
            if (distance <= weapon.NearDistance)
            {
                accuracy = weapon.AccuracyNear;
                spread = weapon.SpreadNear;
            }
            else if (distance >= weapon.FarDistance)
            {
                accuracy = weapon.AccuracyFar * (GroupSpotted[EnemyGroup[best]] != 0 ? 1f : FarAccuracyMultiplier);
                spread = weapon.SpreadFar;
            }
            else
            {
                accuracy = weapon.AccuracyMid;
                spread = weapon.SpreadMid;
            }

            // 오르는 적·성벽 위 적은 맞히기 쉽다 (Battle.md 6.4)
            byte bestState = EnemyStates[best];
            if (bestState == EnemyState.OnWall || bestState == EnemyState.Climbing)
            {
                accuracy = math.min(1f, accuracy + 0.2f);
            }

            var random = Random.CreateFromIndex(FrameSeed ^ (uint)(s * 7919 + 17));
            if (random.NextFloat() > accuracy)
            {
                float2 offset = random.NextFloat2Direction() * random.NextFloat(math.max(spread, 1.3f), math.max(spread, 1.3f) * 2f);
                aim += new float3(offset.x, 0f, offset.y);
            }

            Shots.Enqueue(new Shot
            {
                From = from + new float3(0f, 1f, 0f),
                To = new float3(aim.x, aim.y + 1f, aim.z),
                Target = new EnemyHandle { Index = best, Generation = EnemyGeneration[best] },
                Weapon = SoldierWeapon[s],
            });
            SoldierCooldowns[s] = weapon.Interval * IntervalMultiplier;
            SoldierFireTime[s] = Now;
        }

        // 지도에서 정한 지점으로 포격 (MAP-3). 지점 근처(범위 + 6m)에 적이 없으면 쉰다 (D-044 M2)
        void FireAtAim(int s, WeaponKindData weapon)
        {
            float3 aim = SoldierAim[s];
            float watch = weapon.AreaRadius + 6f;
            int2 minCell = Grid.CellOf(aim - new float3(watch, 0f, watch));
            int2 maxCell = Grid.CellOf(aim + new float3(watch, 0f, watch));
            int nearest = -1;
            float nearestSq = watch * watch;
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int cell = Grid.IndexOf(new int2(x, z));
                    int start = CellStart[cell];
                    int end = start + CellCount[cell];
                    for (int k = start; k < end; k++)
                    {
                        int e = SortedEnemies[k];
                        float3 d = EnemyPositions[e] - aim;
                        float distSq = d.x * d.x + d.z * d.z;
                        if (distSq <= nearestSq)
                        {
                            nearest = e;
                            nearestSq = distSq;
                        }
                    }
                }
            }

            if (nearest < 0)
            {
                SoldierIdle[s] = 1;
                SoldierCooldowns[s] = RetryInterval;
                return;
            }

            SoldierIdle[s] = 0;
            float3 from = SoldierPositions[s];
            float3 toAim = aim - from;
            float distance = math.sqrt(toAim.x * toAim.x + toAim.z * toAim.z);
            float accuracy = distance <= weapon.NearDistance ? weapon.AccuracyNear : distance >= weapon.FarDistance ? weapon.AccuracyFar * FarAccuracyMultiplier : weapon.AccuracyMid;
            float spread = distance <= weapon.NearDistance ? weapon.SpreadNear : distance >= weapon.FarDistance ? weapon.SpreadFar : weapon.SpreadMid;
            var random = Random.CreateFromIndex(FrameSeed ^ (uint)(s * 7919 + 17));
            float3 point = aim;
            if (random.NextFloat() > accuracy)
            {
                float2 offset = random.NextFloat2Direction() * random.NextFloat(math.max(spread, 1.3f), math.max(spread, 1.3f) * 2f);
                point += new float3(offset.x, 0f, offset.y);
            }

            Shots.Enqueue(new Shot
            {
                From = from + new float3(0f, 1f, 0f),
                To = new float3(point.x, 1f, point.z),
                Target = new EnemyHandle { Index = nearest, Generation = EnemyGeneration[nearest] },
                Weapon = SoldierWeapon[s],
            });
            SoldierCooldowns[s] = weapon.Interval * IntervalMultiplier;
            SoldierFireTime[s] = Now;
        }
    }

    // ⑤ 투사체 풀에서 슬롯 꺼내기 (단일)
    [BurstCompile(CompileSynchronously = true)]
    public struct AllocateShotsJob : IJob
    {
        public NativeQueue<Shot> Shots;
        public NativeArray<int> FreeSlots;
        public NativeArray<int> FreeCount;   // [0]
        public NativeArray<byte> ShotActive;
        public NativeArray<float3> ShotPositions;
        public NativeArray<float3> ShotVelocities;
        public NativeArray<float> ShotLife;
        public NativeArray<EnemyHandle> ShotTargets;
        public NativeArray<int> ShotWeapon;
        [ReadOnly] public NativeArray<WeaponKindData> Weapons;
        public NativeArray<int> EnemyIncoming;
        public NativeArray<int> Stats;

        public void Execute()
        {
            int free = FreeCount[0];
            while (Shots.TryDequeue(out Shot shot))
            {
                if (free == 0)
                {
                    Stats[BattleStat.PoolOverflow]++;
                    continue;
                }

                free--;
                int slot = FreeSlots[free];
                float speed = math.max(Weapons[shot.Weapon].ProjectileSpeed, 1f);
                float3 delta = shot.To - shot.From;
                float distance = math.max(math.length(delta), 0.01f);
                ShotActive[slot] = 1;
                ShotPositions[slot] = shot.From;
                ShotVelocities[slot] = delta / distance * speed;
                ShotLife[slot] = distance / speed;
                ShotTargets[slot] = shot.Target;
                ShotWeapon[slot] = shot.Weapon;
                EnemyIncoming[shot.Target.Index] += Weapons[shot.Weapon].Damage;
            }

            FreeCount[0] = free;
        }
    }

    // ⑥ 투사체 이동과 착지 판정 (슬롯별 병렬). 범위 무기(대포)는 반경 안 모든 적에게 피해
    [BurstCompile(CompileSynchronously = true)]
    public struct MoveShotsJob : IJobParallelFor
    {
        public NativeArray<byte> ShotActive;
        public NativeArray<float3> ShotPositions;
        [ReadOnly] public NativeArray<float3> ShotVelocities;
        public NativeArray<float> ShotLife;
        [ReadOnly] public NativeArray<EnemyHandle> ShotTargets;
        [ReadOnly] public NativeArray<int> ShotWeapon;
        public NativeArray<byte> ShotEnded;
        [ReadOnly] public NativeArray<WeaponKindData> Weapons;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<int> EnemyGeneration;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> SortedEnemies;
        public NativeQueue<HitEvent>.ParallelWriter Hits;
        public GridSpec Grid;
        public float DeltaTime;

        const float TargetHitRadius = 1.2f;
        const float SplashHitRadius = 0.9f;

        public void Execute(int slot)
        {
            if (ShotActive[slot] == 0)
            {
                return;
            }

            float life = ShotLife[slot];
            float3 position = ShotPositions[slot] + ShotVelocities[slot] * math.min(DeltaTime, math.max(life, 0f));
            ShotPositions[slot] = position;
            ShotLife[slot] = life - DeltaTime;
            if (life - DeltaTime > 0f)
            {
                return;
            }

            WeaponKindData weapon = Weapons[ShotWeapon[slot]];
            if (weapon.AreaRadius > 0f)
            {
                float radiusSq = weapon.AreaRadius * weapon.AreaRadius;
                int2 minCell = Grid.CellOf(position - new float3(weapon.AreaRadius, 0f, weapon.AreaRadius));
                int2 maxCell = Grid.CellOf(position + new float3(weapon.AreaRadius, 0f, weapon.AreaRadius));
                for (int z = minCell.y; z <= maxCell.y; z++)
                {
                    for (int x = minCell.x; x <= maxCell.x; x++)
                    {
                        int cell = Grid.IndexOf(new int2(x, z));
                        int start = CellStart[cell];
                        int end = start + CellCount[cell];
                        for (int k = start; k < end; k++)
                        {
                            int e = SortedEnemies[k];
                            float3 d = EnemyPositions[e] - position;
                            if (d.x * d.x + d.z * d.z <= radiusSq)
                            {
                                Hits.Enqueue(new HitEvent
                                {
                                    Enemy = new EnemyHandle { Index = e, Generation = EnemyGeneration[e] },
                                    Damage = weapon.Damage,
                                });
                            }
                        }
                    }
                }
            }
            else
            {
                EnemyHandle target = ShotTargets[slot];
                bool hit = false;
                if (EnemyGeneration[target.Index] == target.Generation)
                {
                    float3 d = EnemyPositions[target.Index] - position;
                    if (d.x * d.x + d.z * d.z <= TargetHitRadius * TargetHitRadius)
                    {
                        Hits.Enqueue(new HitEvent { Enemy = target, Damage = weapon.Damage });
                        hit = true;
                    }
                }

                if (!hit)
                {
                    int2 center = Grid.CellOf(position);
                    int nearest = -1;
                    float nearestSq = SplashHitRadius * SplashHitRadius;
                    for (int z = math.max(center.y - 1, 0); z <= math.min(center.y + 1, Grid.Rows - 1); z++)
                    {
                        for (int x = math.max(center.x - 1, 0); x <= math.min(center.x + 1, Grid.Columns - 1); x++)
                        {
                            int cell = Grid.IndexOf(new int2(x, z));
                            int start = CellStart[cell];
                            int end = start + CellCount[cell];
                            for (int k = start; k < end; k++)
                            {
                                int e = SortedEnemies[k];
                                float3 d = EnemyPositions[e] - position;
                                float distSq = d.x * d.x + d.z * d.z;
                                if (distSq <= nearestSq)
                                {
                                    nearest = e;
                                    nearestSq = distSq;
                                }
                            }
                        }
                    }

                    if (nearest >= 0)
                    {
                        Hits.Enqueue(new HitEvent
                        {
                            Enemy = new EnemyHandle { Index = nearest, Generation = EnemyGeneration[nearest] },
                            Damage = weapon.Damage,
                        });
                    }
                }
            }

            ShotActive[slot] = 0;
            ShotEnded[slot] = 1;
        }
    }

    // ⑦ 사건 처리 (단일): 피해 적용, 사망은 세대값으로 한 번만. 끝난 투사체 슬롯 반납
    [BurstCompile(CompileSynchronously = true)]
    public struct ApplyBattleHitsJob : IJob
    {
        public NativeQueue<HitEvent> Hits;
        public NativeArray<int> EnemyHp;
        public NativeArray<int> EnemyGeneration;
        public NativeArray<byte> EnemyStates;
        public NativeArray<int> EnemyIncoming;
        [ReadOnly] public NativeArray<int> KindOf;
        [ReadOnly] public NativeArray<EnemyKind> Kinds;
        [ReadOnly] public NativeArray<EnemyHandle> ShotTargets;
        [ReadOnly] public NativeArray<int> ShotWeapon;
        [ReadOnly] public NativeArray<WeaponKindData> Weapons;
        public NativeArray<byte> ShotEnded;
        public NativeArray<int> FreeSlots;
        public NativeArray<int> FreeCount;   // [0]
        public NativeArray<int> Stats;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<byte> EnemySides;
        public NativeArray<float> Piles;
        public PileSpec Pile;
        public float CastleHalf;

        public void Execute()
        {
            while (Hits.TryDequeue(out HitEvent hit))
            {
                int e = hit.Enemy.Index;
                if (EnemyGeneration[e] != hit.Enemy.Generation || !EnemyState.IsAlive(EnemyStates[e]))
                {
                    continue;
                }

                int hp = EnemyHp[e] - hit.Damage;
                EnemyHp[e] = math.max(hp, 0);
                if (hp > 0)
                {
                    continue;
                }

                EnemyStates[e] = EnemyState.Dead;
                EnemyGeneration[e] = EnemyGeneration[e] + 1;
                EnemyIncoming[e] = 0;

                // 성벽 가까이(오르다 떨어진 적 포함)에서 죽으면 그 칸 더미가 높아진다
                int side = EnemySides[e];
                float3 position = EnemyPositions[e];
                if (math.dot(position, WallGeometry.Outward(side)) - CastleHalf <= Pile.Depth)
                {
                    int p = WallGeometry.PileIndex(position, side, CastleHalf, Pile.BinWidth, Pile.Bins);
                    Piles[p] = math.min(Piles[p] + Pile.PerKill, Pile.MaxHeight);
                }
                EnemyKind kind = Kinds[KindOf[e]];
                Stats[BattleStat.Kills]++;
                Stats[BattleStat.Gold] += kind.RewardGold;
                if (kind.IsSpecial != 0)
                {
                    Stats[BattleStat.SpecialKills]++;
                }
            }

            int free = FreeCount[0];
            for (int slot = 0; slot < ShotEnded.Length; slot++)
            {
                if (ShotEnded[slot] == 0)
                {
                    continue;
                }

                ShotEnded[slot] = 0;
                // 착지한 투사체의 예약 피해를 노린 적에게서 뺀다 (그 적이 이미 죽었으면 세대가 달라 건너뜀)
                EnemyHandle target = ShotTargets[slot];
                if (EnemyGeneration[target.Index] == target.Generation)
                {
                    EnemyIncoming[target.Index] = math.max(0, EnemyIncoming[target.Index] - Weapons[ShotWeapon[slot]].Damage);
                }

                FreeSlots[free] = slot;
                free++;
            }

            FreeCount[0] = free;
            Stats[BattleStat.ActiveShots] = ShotEnded.Length - free;
        }
    }

    // ⑧ 적 그리기 데이터: 걷기·오르기 0~3번 칸, 성벽 공격·성벽 위 싸움 4~7번 칸. 등장 전·죽은 적은 크기 0
    // K-032: 따로 붙인 동작 시트가 있으면 그 구간 칸(공격·세우기·오르기·쓰러짐, PerfLabArt.*Frame0).
    // 죽은 적은 쓰러짐 그림이 있으면 FallSeconds 동안 쓰러짐 칸, 그 뒤 크기 0. 오르는 사다리 적은 Ladders에 사다리 소품
    [BurstCompile(CompileSynchronously = true)]
    public struct WriteBattleEnemyInstancesJob : IJobParallelFor
    {
        public NativeArray<float> FallStart;                    // 쓰러지기 시작한 시각 (0 = 아직)
        [WriteOnly] public NativeArray<PerfLabInstance> Ladders;
        public float FallSeconds;
        public float FallFps;
        public float DeployHeight;      // 오르기 시작 뒤 이 높이(m)까지는 세우기 칸
        public float LadderSize;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> States;
        [ReadOnly] public NativeArray<float> AnimPhases;
        [ReadOnly] public NativeArray<int> KindOf;
        [ReadOnly] public NativeArray<EnemyKind> Kinds;
        [ReadOnly] public NativeArray<byte> Sides;
        [ReadOnly] public NativeArray<int> EnemyGroup;
        [ReadOnly] public NativeArray<byte> GroupSpotted;     // 망원경으로 확인한 무리는 시야 밖이어도 보인다
        [WriteOnly] public NativeArray<PerfLabInstance> Instances;
        public float CastleHalf;
        public float WallScale;         // 성벽 위·사다리에서의 크기 비율 (병사와 맞춤, D-046)
        public float WallScaleBand;     // 성벽에서 이 거리 안(카메라에서 성벽에 가려지는 곳)에서 원래 크기 → WallScale
        public float VisibleDistance;   // 안개·밤 시야(성벽에서 m). 이 너머는 흐리게, 더 너머는 안 그림
        public float Brightness;        // 밤·비에 어둡게
        public float Time;
        public float WalkFps;
        public float AttackFps;
        public int FramesPerAction;

        public void Execute(int i)
        {
            byte state = States[i];
            EnemyKind kind = Kinds[KindOf[i]];
            Ladders[i] = new PerfLabInstance { PosFrame = new float4(Positions[i], 0f), TintSize = float4.zero };
            float frame;
            if (!EnemyState.IsAlive(state))
            {
                if (state != EnemyState.Dead || kind.FallFrames == 0)
                {
                    Instances[i] = new PerfLabInstance { PosFrame = new float4(Positions[i], 0f), TintSize = float4.zero };
                    return;
                }

                float start = FallStart[i];
                if (start <= 0f)
                {
                    start = math.max(Time, 1e-4f);
                    FallStart[i] = start;
                }

                float age = Time - start;
                if (age >= FallSeconds)
                {
                    Instances[i] = new PerfLabInstance { PosFrame = new float4(Positions[i], 0f), TintSize = float4.zero };
                    return;
                }

                frame = PerfLabArt.FallFrame0 + math.min(math.floor(age * FallFps), kind.FallFrames - 1);
            }
            else
            {
                bool attacking = state == EnemyState.AtWall || state == EnemyState.OnWall;
                float fps = attacking ? AttackFps : WalkFps;
                float cycle = math.floor(math.frac(Time * fps / FramesPerAction + AnimPhases[i]) * FramesPerAction);
                frame = cycle;
                if (attacking && kind.AttackFrames > 0)
                {
                    frame = PerfLabArt.AttackFrame0 + cycle % kind.AttackFrames;
                }
                else
                {
                    if (attacking && kind.SheetFrames >= FramesPerAction * 2)
                    {
                        frame += FramesPerAction;
                    }

                    if (kind.SheetFrames < FramesPerAction) frame = 0;
                }

                if (state == EnemyState.Climbing)
                {
                    float climbed = Positions[i].y;
                    if (kind.DeployFrames > 0 && climbed < DeployHeight)
                        frame = PerfLabArt.DeployFrame0 + math.min(math.floor(climbed / DeployHeight * kind.DeployFrames), kind.DeployFrames - 1);
                    else if (kind.ClimbFrames > 0)
                        frame = PerfLabArt.ClimbFrame0 + cycle % kind.ClimbFrames;
                }
            }

            float shade = Brightness;
            float fromWall = math.dot(Positions[i], WallGeometry.Outward(Sides[i])) - CastleHalf;
            float size = kind.DrawSize * math.lerp(WallScale, 1f, math.saturate((fromWall - 1f) / math.max(0.1f, WallScaleBand - 1f)));
            float beyond = fromWall - VisibleDistance;
            if (beyond > 0f && GroupSpotted[EnemyGroup[i]] == 0)
            {
                if (beyond > 20f)
                {
                    Instances[i] = new PerfLabInstance { PosFrame = new float4(Positions[i], 0f), TintSize = float4.zero };
                    return;
                }

                shade *= math.lerp(0.45f, 0.15f, beyond / 20f);   // 시야 밖: 검은 그림자처럼 흐려지다 사라짐
            }

            Instances[i] = new PerfLabInstance
            {
                PosFrame = new float4(Positions[i], frame),
                TintSize = new float4(kind.Tint * shade, size),
            };

            // 사다리 소품: 오르는 동안 성벽 바로 앞 땅에 세운다 (인물보다 조금 성벽 쪽)
            if (state == EnemyState.Climbing && kind.HasLadder != 0)
            {
                float3 foot = Positions[i] - WallGeometry.Outward(Sides[i]) * 0.15f;
                foot.y = 0f;
                Ladders[i] = new PerfLabInstance
                {
                    PosFrame = new float4(foot, 0f),
                    TintSize = new float4(shade, shade, shade, LadderSize),
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct WriteShotInstancesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> ShotActive;
        [ReadOnly] public NativeArray<float3> ShotPositions;
        [ReadOnly] public NativeArray<float3> ShotVelocities;
        [ReadOnly] public NativeArray<int> ShotWeapon;
        [ReadOnly] public NativeArray<WeaponKindData> Weapons;
        [WriteOnly] public NativeArray<PerfLabInstance> Instances;
        public float Brightness;

        // K-032: 프레임 번호 = 무기 번호(무기마다 따로 그리는 구간), 색 칸(rgb) = 날아가는 방향. 색은 그리기 쪽 _ShotColor
        public void Execute(int slot)
        {
            if (ShotActive[slot] == 0)
            {
                Instances[slot] = new PerfLabInstance { PosFrame = new float4(ShotPositions[slot], 0f), TintSize = float4.zero };
                return;
            }

            // 대포알은 크게, 화살·총알은 가늘게 (크기 1 넘음 = 대포알 — BattleEffectsView가 맞음 불꽃에서 뺀다)
            int weapon = ShotWeapon[slot];
            bool cannon = Weapons[weapon].AreaRadius > 0f;
            Instances[slot] = new PerfLabInstance
            {
                PosFrame = new float4(ShotPositions[slot], weapon),
                TintSize = new float4(math.normalizesafe(ShotVelocities[slot], new float3(1f, 0f, 0f)), cannon ? 1.6f : 0.45f),
            };
        }
    }
}
