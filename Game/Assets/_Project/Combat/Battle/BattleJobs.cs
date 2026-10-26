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

        // 들판 면: 성 가운데에서 대각선으로 나눈 네 구역 중 어디인가 (0 북 · 1 동 · 2 남 · 3 서). 대포가 칠 수 있는 곳 가르기 (K-042)
        public static int FieldSide(float3 p)
        {
            if (math.abs(p.z) >= math.abs(p.x)) return p.z >= 0f ? 0 : 2;
            return p.x >= 0f ? 1 : 3;
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
        // 우두머리 (성벽손 오우거, BattleSim.Boss.cs). IsBoss = 0이면 아래 값은 모두 쓰지 않는다
        public byte IsBoss;
        public float UnlitPointMul;      // 빛 밖·통로 아래 한 대씩 쏘는 무기 피해 비율
        public float LitSpeedMul;        // 빛 안 걷기 배율 (빛줄기 느려짐 대신)
        public float ClimbAreaMul;       // 오르는 동안 범위 피해 배율
        public float KnockdownSeconds;   // 떨어진 뒤 쓰러짐 시간 (그리기가 쓰러짐 칸 진행에 쓴다)
        public float OnWallWallDamage;   // 통로 위에서 그 성벽에 주는 초당 피해
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
        public float BurnChance;      // 맞힌 적에게 화상 (불화살, K-058). 0 = 없음
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
        public BeamField Beam;   // 등대 빛줄기: 빛 안 적은 느려진다 (BattleSim.Lamp.cs)
        public PileSpec Pile;
        public float CastleHalf;
        public float WallHeight;
        public float DeltaTime;
        public float FunnelBase;    // 성벽에서 적이 퍼지는 반폭(m)
        public float FunnelSlope;   // 성벽에서 1m 멀어질 때 늘어나는 반폭
        public TerrainField Terrain;   // 무작위 지형·지형 손질 (K-049, BattleSim.Terrain.cs): 돌아감·넘음·허묾·엄폐
        public NativeArray<float> Down;                    // 우두머리 쓰러짐 남은 시간 (BattleSim.Boss.cs). 0 = 서 있음
        [ReadOnly] public NativeArray<float> TargetLateral;   // 우두머리가 가려는 성벽 따라 자리(m)

        // 우두머리 걷기: 쓰러져 있으면 제자리, 빛 안이면 빨라지고(빛줄기 느려짐은 안 받음), 정한 성벽 자리를 향해 모인다.
        // 성벽에 닿으면 사다리·시체 더미 없이 땅에서 바로 오르기 시작한다
        void StepBoss(int i, EnemyKind kind)
        {
            float3 position = Positions[i];
            float down = Down[i];
            if (down > 0f)
            {
                Down[i] = math.max(0f, down - DeltaTime);
                Velocities[i] = float3.zero;
                return;
            }

            float3 n = WallGeometry.Outward(Sides[i]);
            float slow = 1f;
            for (int c = 0; c < CraterCount; c++)
            {
                float4 crater = Craters[c];
                float dx = position.x - crater.x, dz = position.z - crater.y;
                if (dx * dx + dz * dz <= crater.z) slow = math.min(slow, crater.w);
            }

            float speed = Speeds[i] * slow * (Beam.Contains(position) ? kind.LitSpeedMul : 1f);
            float3 r = WallGeometry.Right(Sides[i]);
            float fromWall = math.dot(position, n) - CastleHalf;
            float lateral = math.dot(position, r);
            // 남은 거리에 비례해 좁혀 와 성벽에 닿을 때 정확히 그 자리 (깔때기와 같은 방식)
            float lateralSpeed = math.clamp((TargetLateral[i] - lateral) / math.max(fromWall, 1f), -1f, 1f) * speed;
            float3 velocity = -n * speed + r * lateralSpeed;
            if (Terrain.Active != 0) velocity = Terrain.Steer(i, position, velocity, Sides[i], fromWall);
            position += velocity * DeltaTime;
            Velocities[i] = velocity;
            float distance = math.dot(position, n);
            if (distance <= StopDistance[i])
            {
                position += n * (StopDistance[i] - distance);
                position.y = 0f;
                Velocities[i] = float3.zero;
                Climb[i] = 0f;
                States[i] = EnemyState.Climbing;
            }

            Positions[i] = position;
        }

        public void Execute(int i)
        {
            byte state = States[i];
            if (Terrain.Active != 0 && state != EnemyState.Walking) Terrain.Clear(i);
            if (state == EnemyState.Walking)
            {
                EnemyKind boss = Kinds[KindOf[i]];
                if (boss.IsBoss != 0)
                {
                    StepBoss(i, boss);
                    return;
                }
            }

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

                if (Beam.Contains(Positions[i])) slow *= Beam.SlowMul;
                float speed = Speeds[i] * slow;
                // 깔때기 (D-065): 들판에 넓게 퍼져 있다가 성벽으로 모여든다. 깔때기 폭 안에서의 비율을 지키며 좌우로 좁혀 온다
                float3 r = WallGeometry.Right(Sides[i]);
                float fromWall = math.dot(Positions[i], n) - CastleHalf;
                float width = FunnelBase + math.max(0f, fromWall) * FunnelSlope;
                float lateralSpeed = -speed * FunnelSlope * math.dot(Positions[i], r) / math.max(width, 0.1f);
                float3 velocity = -n * speed + r * lateralSpeed;
                if (Terrain.Active != 0) velocity = Terrain.Steer(i, Positions[i], velocity, Sides[i], fromWall);
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
                        if (kind.IsBoss != 0) Tally[b + SideStat.WallDamage] += kind.WallDamage * DeltaTime;   // 우두머리는 매달려서도 성벽을 부순다
                        break;
                    case EnemyState.OnWall:
                        Tally[b + SideStat.OnWall] += 1f;
                        Tally[b + SideStat.GarrisonDamage] += kind.OnWallAttack * DeltaTime;
                        Tally[b + SideStat.WallDamage] += kind.OnWallWallDamage * DeltaTime;   // 우두머리: 통로 위에서 그 성벽 체력도 깎는다 (일반 적은 0)
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
        [ReadOnly] public NativeArray<byte> SoldierCannonReach;   // 대포가 칠 수 있는 들판 면 (비트 0~3, K-042). 0 = 대포 아님
        public NativeArray<byte> SoldierIdle;                // 조준점 근처에 적이 없어 쉼
        public NativeArray<float> SoldierFireTime;            // 마지막으로 쏜 시각 (사격 그림, K-032)
        public NativeArray<float> SoldierFireLateral;         // 마지막으로 쏜 쪽 (성벽 따라 오른쪽 +) — 사격 그림 좌우 뒤집기
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
        [ReadOnly] public NativeArray<byte> EnemyCovered;     // 토벽 뒤 적 — 총·화살은 노리지 않는다 (K-049, D-077 ⑦)
        public NativeQueue<Shot>.ParallelWriter Shots;
        public GridSpec Grid;
        public float CastleHalf;
        public float DeltaTime;
        public float RetryInterval;
        public uint FrameSeed;
        public float IntervalMultiplier;      // 비: 공격 간격 길어짐 (공격 속도 −20%, D-028 C4)
        public int HeroSide;                  // 주인공이 서 있는 성벽 (지휘 휘장, K-058). -1 = 없음
        public float HeroSideIntervalMultiplier;   // 그 성벽 병사 공격 간격 배율 (1 = 강화 없음)

        float IntervalFor(int s) => IntervalMultiplier * (SoldierSide[s] == HeroSide ? HeroSideIntervalMultiplier : 1f);
        public float FarAccuracyMultiplier;   // 안개·밤: 먼 거리 명중 감소 (Battle.md 6.4)
        public byte FrontArc;                 // 1 = 자기 성벽 앞 90도·각도 순위(D-073). 0 = 예전처럼 둘레 전부 (측정 비교용)
        // 투사체 출발점 (K-035): 병사는 그림 속 활·총 높이 + 바깥쪽으로 조금, 대포는 포구 자리 (BattleSim이 그림 크기에서 정한다)
        public float MuzzleHeight;
        public float MuzzleForward;
        public float CannonMuzzleHeight;
        public float CannonMuzzleForward;
        // 대포 불붙이기 (K-046): 쏠 때가 되면 포탄을 정해 두고 IgniteSeconds 뒤에 낸다. 그동안 조준 고정, 쏘는 병사가 빠지면 취소
        public NativeArray<float> SoldierFuse;
        public NativeArray<Shot> SoldierPendingShot;
        public float IgniteSeconds;

        // 투사체를 낸다: 대포면 불붙이기 시작(나중에 발사), 아니면 바로. 재장전은 발사 시점부터 (불붙이기는 간격 안에 든다)
        void Launch(int s, WeaponKindData weapon, Shot shot)
        {
            if (weapon.AreaRadius > 0f && IgniteSeconds > 0f)
            {
                SoldierPendingShot[s] = shot;
                SoldierFuse[s] = IgniteSeconds;
                return;
            }

            Shots.Enqueue(shot);
            SoldierCooldowns[s] = weapon.Interval * IntervalFor(s);
            SoldierFireTime[s] = Now;
            SoldierFireLateral[s] = math.dot(shot.To - shot.From, WallGeometry.Right(SoldierSide[s]));   // + = 바깥을 볼 때 오른쪽으로 쏨 (사격 그림 좌우)
        }

        float3 Muzzle(int s, WeaponKindData weapon)
        {
            bool cannon = weapon.AreaRadius > 0f;
            float3 outward = WallGeometry.Outward(SoldierSide[s]);
            return SoldierPositions[s] + outward * (cannon ? CannonMuzzleForward : MuzzleForward)
                + new float3(0f, cannon ? CannonMuzzleHeight : MuzzleHeight, 0f);
        }

        // TargetPriority 순서 (Core.Campaign.TargetPriority와 같게)
        const byte LowestHpRatio = 0, HighestHpRatio = 1, Nearest = 2, ClimbingFirst = 3, SpecialFirst = 4, DensestArea = 5;
        const float Cos30 = 0.8660254f, Cos60 = 0.5f;
        public const float CannonFrontMargin = 2f;   // 대포는 자기 자리에서 성벽 바깥으로 이만큼 앞인 곳만 (BattleSim.CannonCanReach·지도 미리보기도 같은 값)

        // 체력 비율을 10% 띠로 — 97%와 95%를 같은 적으로 보고 병사들이 나눠 쏘게 (D-073)
        static float BandOf(float ratio) => math.floor(ratio * 10f);

        public void Execute(int s)
        {
            if (SoldierAlive[s] == 0 || SoldierWeapon[s] < 0 || SoldierAway[s] != 0)
            {
                SoldierFuse[s] = 0f;   // 불붙이던 대포병이 쓰러지거나 빠지면 취소 (K-046)
                return;   // 죽은 병사, 대포 운용병(쏘지 않음), 전령
            }

            // 불붙이는 중 (K-046): 도화선이 다 타면 정해 둔 포탄을 내고, 남은 간격만큼 재장전
            float fuse = SoldierFuse[s];
            if (fuse > 0f)
            {
                fuse -= DeltaTime;
                if (fuse > 0f)
                {
                    SoldierFuse[s] = fuse;
                    return;
                }

                SoldierFuse[s] = 0f;
                Shots.Enqueue(SoldierPendingShot[s]);
                SoldierCooldowns[s] = math.max(0f, Weapons[SoldierWeapon[s]].Interval * IntervalFor(s) - IgniteSeconds);
                SoldierFireTime[s] = Now;
                return;
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
            int reach = weapon.AreaRadius > 0f ? SoldierCannonReach[s] : 0;
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
                        // (우두머리는 화살 피해가 줄어 들어가므로 날아가는 피해를 그대로 빼면 안 된다 — 늘 표적)
                        int remaining = EnemyHp[e] - EnemyIncoming[e];
                        bool bossTarget = Kinds[EnemyKindOf[e]].IsBoss != 0;
                        if (remaining <= 0)
                        {
                            if (!bossTarget) continue;
                            remaining = math.max(1, EnemyHp[e]);
                        }

                        byte state = EnemyStates[e];
                        if (EnemyCovered[e] != 0 && weapon.AreaRadius <= 0f) continue;   // 토벽 엄폐 (대포는 넘어서 맞힌다)

                        // 자기 성벽 앞만 쏜다 (D-073): 성벽 바깥 방향에서 좌우 30도 안 = 1순위, 60도 = 2순위, 90도 = 3순위,
                        // 90도 넘게(옆·뒤 = 다른 성벽 쪽)는 쏘지 않는다. 성벽 위 적은 통로 옆이라 거의 90도 — 조금 넘어도 허용
                        float flat = math.sqrt(math.max(distSq, 0.0001f));
                        float cosine = (d.x * wallOut.x + d.z * wallOut.z) / flat;
                        bool onWall = state == EnemyState.OnWall;
                        if (reach != 0)
                        {
                            // 대포 (K-042): 자기 들판 + 모서리 2문만 옆 들판. 각도 대신 들판 면으로 가른다
                            if ((reach & (1 << WallGeometry.FieldSide(EnemyPositions[e]))) == 0) continue;
                            // 그래도 자기 성벽선 앞이어야 한다 (사용자 2026-10-20 #10: 옆 들판 적을 성 안쪽 위로 넘겨 거꾸로 쐈다)
                            if (d.x * wallOut.x + d.z * wallOut.z < CannonFrontMargin) continue;
                        }
                        else if (FrontArc != 0 && cosine <= (onWall ? -0.2f : 0f))
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

                        if (onWall && bossTarget) key = float.MinValue;   // 통로 위 우두머리는 모든 명령에서 1순위 (같은 통로 위 적보다도 먼저)
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
                // 표적 없음 → 겨누기 동작을 하지 않게 (사용자 2026-10-20 #8: 재시도 0.25초가 겨누기 구간 0.375초보다 짧아 헛동작 반복)
                SoldierIdle[s] = 1;
                return;
            }

            SoldierIdle[s] = 0;

            float distance = math.sqrt(bestDistSq);
            // 앞질러 겨눔: 날아가는 시간 (+ 대포는 불붙이는 시간 — 그동안 조준이 고정되므로, K-046)
            float leadTime = distance / math.max(weapon.ProjectileSpeed, 1f) + (weapon.AreaRadius > 0f ? IgniteSeconds : 0f);
            float3 aim = EnemyPositions[best] + EnemyVelocities[best] * leadTime;

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

            Launch(s, weapon, new Shot
            {
                From = Muzzle(s, weapon),
                To = new float3(aim.x, aim.y + 1f, aim.z),
                Target = new EnemyHandle { Index = best, Generation = EnemyGeneration[best] },
                Weapon = SoldierWeapon[s],
            });
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

            Launch(s, weapon, new Shot
            {
                From = Muzzle(s, weapon),
                To = new float3(point.x, 1f, point.z),
                Target = new EnemyHandle { Index = nearest, Generation = EnemyGeneration[nearest] },
                Weapon = SoldierWeapon[s],
            });
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

        // 병렬로 쌓인 큐는 꺼내는 순서가 작업 스레드 배치에 따라 달라진다 → 슬롯 번호(화상 굴림 해시에 쓰임)가 실행마다 바뀌었다.
        // 내용으로 줄을 세워 같은 씨앗·같은 입력이면 늘 같은 순서가 되게 한다 (같은 내용끼리는 바꿔 끼워도 결과가 같다)
        struct ShotOrder : System.Collections.Generic.IComparer<Shot>
        {
            public int Compare(Shot a, Shot b)
            {
                int c = a.Weapon.CompareTo(b.Weapon);
                if (c != 0) return c;
                c = a.From.x.CompareTo(b.From.x);
                if (c != 0) return c;
                c = a.From.z.CompareTo(b.From.z);
                if (c != 0) return c;
                c = a.From.y.CompareTo(b.From.y);
                if (c != 0) return c;
                c = a.To.x.CompareTo(b.To.x);
                if (c != 0) return c;
                c = a.To.z.CompareTo(b.To.z);
                if (c != 0) return c;
                c = a.To.y.CompareTo(b.To.y);
                if (c != 0) return c;
                c = a.Target.Index.CompareTo(b.Target.Index);
                if (c != 0) return c;
                return a.Target.Generation.CompareTo(b.Target.Generation);
            }
        }

        public void Execute()
        {
            int free = FreeCount[0];
            var ordered = new NativeList<Shot>(math.max(Shots.Count, 1), Allocator.Temp);
            while (Shots.TryDequeue(out Shot queued)) ordered.Add(queued);
            if (ordered.Length > 1) ordered.Sort(new ShotOrder());
            for (int i = 0; i < ordered.Length; i++)
            {
                Shot shot = ordered[i];
                if (free == 0)
                {
                    // 풀이 꽉 차면 정렬 뒤쪽(무기 번호·출발 위치가 큰 쪽)부터 버려진다 — 풀이 병사 수×4(최소 1024)라 드물다
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

            ordered.Dispose();
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
        [ReadOnly] public NativeArray<byte> EnemyCovered;     // 토벽 뒤 적 — 화살·총알이 닿지 않는다 (K-049). 범위 피해는 맞는다
        public NativeQueue<HitEvent>.ParallelWriter Hits;
        public GridSpec Grid;
        public float DeltaTime;
        public NativeQueue<int>.ParallelWriter Ignites;   // 불화살로 화상 입을 적 번호 (K-058) — 메인 스레드가 다음 프레임에 붙인다
        public uint FrameSeed;

        const float TargetHitRadius = 1.2f;

        // 화살·총알이 적 e를 맞혔을 때 화상 굴리기
        void RollBurn(int slot, int e, WeaponKindData weapon)
        {
            if (weapon.BurnChance <= 0f) return;
            uint h = math.hash(new uint2((uint)slot, FrameSeed ^ (uint)e));
            if ((h & 0xffffff) / 16777216f < weapon.BurnChance) Ignites.Enqueue(e);
        }
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
                                    Area = 1,
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
                if (EnemyGeneration[target.Index] == target.Generation && EnemyCovered[target.Index] == 0)
                {
                    float3 d = EnemyPositions[target.Index] - position;
                    if (d.x * d.x + d.z * d.z <= TargetHitRadius * TargetHitRadius)
                    {
                        Hits.Enqueue(new HitEvent { Enemy = target, Damage = weapon.Damage });
                        RollBurn(slot, target.Index, weapon);
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
                                if (EnemyCovered[e] != 0) continue;
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
                        RollBurn(slot, nearest, weapon);
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
        public BeamField Beam;   // 등대 빛줄기: 빛 안 적은 피해를 더 받는다 (BattleSim.Lamp.cs)

        public void Execute()
        {
            while (Hits.TryDequeue(out HitEvent hit))
            {
                int e = hit.Enemy.Index;
                if (EnemyGeneration[e] != hit.Enemy.Generation || !EnemyState.IsAlive(EnemyStates[e]))
                {
                    continue;
                }

                int damage = hit.Damage;
                EnemyKind kind = Kinds[KindOf[e]];
                bool lit = Beam.Contains(EnemyPositions[e]);
                if (kind.IsBoss != 0)
                {
                    // 우두머리 (BattleSim.Boss.cs): 쇠장갑과 안개 — 빛 밖·통로 아래에서는 화살·총이 조금만 든다.
                    // 오르는 동안 범위 피해는 매달려 피하지 못해 배로
                    byte bossState = EnemyStates[e];
                    if (hit.Area == 0)
                    {
                        if (!lit && bossState != EnemyState.OnWall) damage = math.max(1, (int)math.round(damage * kind.UnlitPointMul));
                    }
                    else if (bossState == EnemyState.Climbing)
                    {
                        damage = math.max(1, (int)math.round(damage * kind.ClimbAreaMul));
                    }
                }

                if (lit) damage = math.max(1, (int)math.round(damage * Beam.DamageMul));
                int hp = EnemyHp[e] - damage;
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
        [ReadOnly] public NativeArray<float> Burn;            // 화상 남은 시간 (K-035, BattleSim.Burn.cs)
        [ReadOnly] public NativeArray<int> TerrainHit;        // 토벽을 때리는 칸 (0 이상이면 때리는 중 — 공격 칸으로, K-051)
        [ReadOnly] public NativeArray<float> Down;            // 우두머리 쓰러짐 남은 시간 (쓰러짐 칸으로)
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> States;
        [ReadOnly] public NativeArray<float> AnimPhases;
        [ReadOnly] public NativeArray<int> KindOf;
        [ReadOnly] public NativeArray<EnemyKind> Kinds;
        [ReadOnly] public NativeArray<byte> Sides;
        [ReadOnly] public NativeArray<int> EnemyGroup;
        [ReadOnly] public NativeArray<byte> TierShown;        // 변형 단계 (0 홀림 · 1 물듦 · 2 변형). 빛줄기 안이면 0
        [ReadOnly] public NativeArray<byte> GroupSpotted;     // 망원경으로 확인한 무리는 시야 밖이어도 보인다
        [WriteOnly] public NativeArray<PerfLabInstance> Instances;
        public float CastleHalf;
        public float WallScale;        // 성벽 위·사다리에서의 크기 비율 (병사와 맞춤, D-046)
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
                // 우두머리는 기어오르는 동안 성벽을 움켜쥐고 부수는 중 — 공격 칸 (임시 그림, K-129 전)
                bool attacking = state == EnemyState.AtWall || state == EnemyState.OnWall || (state == EnemyState.Walking && TerrainHit[i] >= 0)
                    || (state == EnemyState.Climbing && kind.IsBoss != 0);
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

                // 우두머리 쓰러짐(떨어짐·함정): 쓰러짐 칸을 따라가다 마지막 칸에서 누워 있는다
                if (kind.IsBoss != 0 && Down[i] > 0f && kind.FallFrames > 0)
                {
                    float lying = math.max(0f, kind.KnockdownSeconds - Down[i]);
                    frame = PerfLabArt.FallFrame0 + math.min(math.floor(lying * FallFps), kind.FallFrames - 1);
                }
            }

            float shade = Brightness;
            float fromWall = math.dot(Positions[i], WallGeometry.Outward(Sides[i])) - CastleHalf;
            int tier = TierShown[i];
            float size = kind.DrawSize * EnemyTierRules.ScaleMultiplier(tier) * math.lerp(WallScale, 1f, math.saturate((fromWall - 1f) / math.max(0.1f, WallScaleBand - 1f)));
            float beyond = fromWall - VisibleDistance;
            if (beyond > 0f && GroupSpotted[EnemyGroup[i]] == 0)
            {
                if (beyond > 20f && kind.IsBoss == 0)   // 우두머리는 커서 안개 속에서도 어두운 그림자로 보인다
                {
                    Instances[i] = new PerfLabInstance { PosFrame = new float4(Positions[i], 0f), TintSize = float4.zero };
                    return;
                }

                shade *= math.lerp(0.45f, 0.15f, math.saturate(beyond / 20f));   // 시야 밖: 검은 그림자처럼 흐려지다 사라짐
            }

            // 화상 (K-035): 살아 있는 동안 붉게 깜빡인다
            // 변형 단계: 어둡고 살짝 보라. 눈 빛남(물듦)·안개 연기(변형)는 그림 카드 후 (EnemyTierRules 참고)
            float3 tint = kind.Tint * shade * EnemyTierRules.TintMultiplier(tier) * EnemyTierRules.WispPulse(tier, Time, i);
            if (Burn[i] > 0f && EnemyState.IsAlive(state))
            {
                float flicker = 0.5f + 0.5f * math.sin(Time * 18f + i * 1.7f);
                tint *= math.lerp(new float3(1f, 1f, 1f), new float3(1.25f, 0.45f, 0.3f), 0.45f + 0.55f * flicker);
            }

            Instances[i] = new PerfLabInstance
            {
                PosFrame = new float4(Positions[i], frame),
                TintSize = new float4(tint, size),
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
