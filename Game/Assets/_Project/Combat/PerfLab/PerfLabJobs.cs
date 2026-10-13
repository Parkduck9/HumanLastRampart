using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// 모든 Job은 Burst 동기 컴파일: 에디터에서 첫 실행 때 느린 일반 코드로 돌다가 바뀌는 구간이 측정에 섞이지 않게 한다.
namespace HumanLastRampart.Combat.PerfLab
{
    // 셰이더 PerfLabInstance와 같은 배치 (32바이트). 바꾸면 셰이더도 같이 바꾼다.
    [StructLayout(LayoutKind.Sequential)]
    public struct PerfLabInstance
    {
        public float4 PosFrame;  // xyz 발 위치, w 프레임 번호
        public float4 TintSize;  // rgb 색, a 크기. 0이면 그리지 않음

        public const int Stride = 32;
    }

    // 적을 가리키는 핸들: 슬롯 번호 + 세대값. 적이 죽고 같은 슬롯에 새 적이 생기면 세대가 올라간다.
    public struct EnemyHandle
    {
        public int Index;
        public int Generation;
    }

    public struct ArrowSpawn
    {
        public float3 From;
        public float3 To;
        public EnemyHandle Target;
    }

    public struct HitEvent
    {
        public EnemyHandle Enemy;
        public int Damage;
    }

    // 바닥을 정사각형 칸으로 나눈 공간 격자. 칸마다 적 번호를 모아 둔다 (계수 정렬).
    public struct GridSpec
    {
        public float MinX;
        public float MinZ;
        public float CellSize;
        public int Columns;
        public int Rows;

        public int2 CellOf(float3 position)
        {
            int x = (int)math.floor((position.x - MinX) / CellSize);
            int z = (int)math.floor((position.z - MinZ) / CellSize);
            return new int2(math.clamp(x, 0, Columns - 1), math.clamp(z, 0, Rows - 1));
        }

        public int IndexOf(int2 cell) => cell.y * Columns + cell.x;
    }

    // 통계 칸 번호 (PerfLabStats 배열)
    public static class PerfLabStat
    {
        public const int ActiveArrows = 0;
        public const int DeathsTotal = 1;
        public const int PoolOverflowTotal = 2;
        public const int HitsThisFrame = 3;
        public const int Count = 4;
    }

    // ① 적 행군: 성벽 쪽으로 직진, 각자 정해진 줄 위치에 닿으면 멈춰 공격 상태
    [BurstCompile(CompileSynchronously = true)]
    public struct EnemyMoveJob : IJobParallelFor
    {
        public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> Speeds;
        [ReadOnly] public NativeArray<float> StopZ;
        public NativeArray<byte> AtWall;
        public float DeltaTime;

        public void Execute(int index)
        {
            if (AtWall[index] != 0)
            {
                return;
            }

            float3 position = Positions[index];
            position.z -= Speeds[index] * DeltaTime;
            if (position.z <= StopZ[index])
            {
                position.z = StopZ[index];
                AtWall[index] = 1;
            }

            Positions[index] = position;
        }
    }

    // ② 격자 칸 계산 (병렬)
    [BurstCompile(CompileSynchronously = true)]
    public struct AssignCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [WriteOnly] public NativeArray<int> CellOfEnemy;
        public GridSpec Grid;

        public void Execute(int index)
        {
            CellOfEnemy[index] = Grid.IndexOf(Grid.CellOf(Positions[index]));
        }
    }

    // ② 격자 만들기 (단일): 칸별 개수 → 시작 위치 → 칸 순서로 적 번호 정렬
    [BurstCompile(CompileSynchronously = true)]
    public struct BuildGridJob : IJob
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
                CellCount[CellOfEnemy[i]]++;
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
                SortedEnemies[Cursor[cell]] = i;
                Cursor[cell]++;
            }
        }
    }

    // ③④ 표적 고르기와 발사 요청 (아군별 병렬)
    // 재장전이 끝난 아군만 사거리 안 칸을 조회하고, 체력 비율이 가장 낮은 적(같으면 가까운 적)을 고른다 (D-019).
    [BurstCompile(CompileSynchronously = true)]
    public struct TargetAndFireJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> AllyPositions;
        public NativeArray<float> AllyCooldowns;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<float> EnemySpeeds;
        [ReadOnly] public NativeArray<byte> EnemyAtWall;
        [ReadOnly] public NativeArray<int> EnemyHp;
        [ReadOnly] public NativeArray<int> EnemyGeneration;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> SortedEnemies;
        public NativeQueue<ArrowSpawn>.ParallelWriter Spawns;
        public GridSpec Grid;
        public float DeltaTime;
        public float Range;
        public float FireInterval;
        public float RetryInterval;
        public float ArrowSpeed;
        public int EnemyMaxHp;

        public void Execute(int index)
        {
            float cooldown = AllyCooldowns[index] - DeltaTime;
            if (cooldown > 0f)
            {
                AllyCooldowns[index] = cooldown;
                return;
            }

            float3 from = AllyPositions[index];
            float rangeSq = Range * Range;
            int2 minCell = Grid.CellOf(from - new float3(Range, 0f, Range));
            int2 maxCell = Grid.CellOf(from + new float3(Range, 0f, Range));

            int best = -1;
            float bestRatio = float.MaxValue;
            float bestDistSq = float.MaxValue;
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int cell = Grid.IndexOf(new int2(x, z));
                    int start = CellStart[cell];
                    int end = start + CellCount[cell];
                    for (int s = start; s < end; s++)
                    {
                        int enemy = SortedEnemies[s];
                        float3 d = EnemyPositions[enemy] - from;
                        float distSq = d.x * d.x + d.z * d.z;
                        if (distSq > rangeSq)
                        {
                            continue;
                        }

                        float ratio = (float)EnemyHp[enemy] / EnemyMaxHp;
                        if (ratio < bestRatio || (ratio == bestRatio && distSq < bestDistSq))
                        {
                            best = enemy;
                            bestRatio = ratio;
                            bestDistSq = distSq;
                        }
                    }
                }
            }

            if (best < 0)
            {
                AllyCooldowns[index] = RetryInterval;
                return;
            }

            // 비행 시간만큼 앞을 겨눈다 (적은 -z 방향으로 직진)
            float3 targetPos = EnemyPositions[best];
            float flight = math.sqrt(bestDistSq) / ArrowSpeed;
            if (EnemyAtWall[best] == 0)
            {
                targetPos.z -= EnemySpeeds[best] * flight;
            }

            Spawns.Enqueue(new ArrowSpawn
            {
                From = from,
                To = new float3(targetPos.x, 1f, targetPos.z),
                Target = new EnemyHandle { Index = best, Generation = EnemyGeneration[best] },
            });
            AllyCooldowns[index] = FireInterval;
        }
    }

    // ④ 화살 풀에서 빈 슬롯 꺼내기 (단일). 풀이 모자라면 발사를 버리지 않고 부족 횟수를 센다.
    [BurstCompile(CompileSynchronously = true)]
    public struct AllocateArrowsJob : IJob
    {
        public NativeQueue<ArrowSpawn> Spawns;
        public NativeArray<int> FreeSlots;
        public NativeArray<int> FreeCount;        // [0]
        public NativeArray<byte> ArrowActive;
        public NativeArray<float3> ArrowPositions;
        public NativeArray<float3> ArrowVelocities;
        public NativeArray<float> ArrowLife;
        public NativeArray<EnemyHandle> ArrowTargets;
        public NativeArray<int> Stats;
        public float ArrowSpeed;

        public void Execute()
        {
            int free = FreeCount[0];
            while (Spawns.TryDequeue(out ArrowSpawn spawn))
            {
                if (free == 0)
                {
                    Stats[PerfLabStat.PoolOverflowTotal]++;
                    continue;
                }

                free--;
                int slot = FreeSlots[free];
                float3 delta = spawn.To - spawn.From;
                float distance = math.max(math.length(delta), 0.01f);
                ArrowActive[slot] = 1;
                ArrowPositions[slot] = spawn.From;
                ArrowVelocities[slot] = delta / distance * ArrowSpeed;
                ArrowLife[slot] = distance / ArrowSpeed;
                ArrowTargets[slot] = spawn.Target;
            }

            FreeCount[0] = free;
        }
    }

    // ⑤ 화살 이동과 명중 (슬롯별 병렬). 착지 시점에 명중을 판정해 사건으로 보낸다.
    // 순서 3은 착지 지점 판정만 한다 (이동 구간 판정은 순서 4 비교 항목).
    [BurstCompile(CompileSynchronously = true)]
    public struct MoveArrowsJob : IJobParallelFor
    {
        public NativeArray<byte> ArrowActive;
        public NativeArray<float3> ArrowPositions;
        [ReadOnly] public NativeArray<float3> ArrowVelocities;
        public NativeArray<float> ArrowLife;
        [ReadOnly] public NativeArray<EnemyHandle> ArrowTargets;
        public NativeArray<byte> ArrowEnded;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<int> EnemyGeneration;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> SortedEnemies;
        public NativeQueue<HitEvent>.ParallelWriter Hits;
        public GridSpec Grid;
        public float DeltaTime;
        public float TargetHitRadius;
        public float SplashHitRadius;
        public int Damage;

        public void Execute(int slot)
        {
            if (ArrowActive[slot] == 0)
            {
                return;
            }

            float life = ArrowLife[slot] - DeltaTime;
            float3 position = ArrowPositions[slot] + ArrowVelocities[slot] * math.min(DeltaTime, math.max(ArrowLife[slot], 0f));
            ArrowPositions[slot] = position;
            ArrowLife[slot] = life;
            if (life > 0f)
            {
                return;
            }

            // 착지: 노린 적이 같은 세대로 가까이 있으면 그 적, 아니면 착지점 주변의 가장 가까운 적
            EnemyHandle target = ArrowTargets[slot];
            bool hit = false;
            if (EnemyGeneration[target.Index] == target.Generation)
            {
                float3 d = EnemyPositions[target.Index] - position;
                if (d.x * d.x + d.z * d.z <= TargetHitRadius * TargetHitRadius)
                {
                    Hits.Enqueue(new HitEvent { Enemy = target, Damage = Damage });
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
                        for (int s = start; s < end; s++)
                        {
                            int enemy = SortedEnemies[s];
                            float3 d = EnemyPositions[enemy] - position;
                            float distSq = d.x * d.x + d.z * d.z;
                            if (distSq <= nearestSq)
                            {
                                nearest = enemy;
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
                        Damage = Damage,
                    });
                }
            }

            ArrowActive[slot] = 0;
            ArrowEnded[slot] = 1;
        }
    }

    // ⑥ 사건 처리 (단일): 피해를 한꺼번에 적용하고, 사망은 세대값으로 한 번만 확정한다.
    // 죽은 적은 같은 슬롯에서 뒤쪽 출발선으로 다시 태어난다(세대 +1) — 동시 활성 수를 유지하는 시험용 규칙.
    // 끝난 화살 슬롯은 여기서 풀로 돌려준다.
    [BurstCompile(CompileSynchronously = true)]
    public struct ApplyHitsJob : IJob
    {
        public NativeQueue<HitEvent> Hits;
        public NativeArray<int> EnemyHp;
        public NativeArray<int> EnemyGeneration;
        public NativeArray<float3> EnemyPositions;
        public NativeArray<byte> EnemyAtWall;
        public NativeArray<Random> RandomState;   // [0]
        public NativeArray<byte> ArrowEnded;
        public NativeArray<int> FreeSlots;
        public NativeArray<int> FreeCount;        // [0]
        public NativeArray<int> Stats;
        public int EnemyMaxHp;
        public float RespawnMinX;
        public float RespawnMaxX;
        public float RespawnMinZ;
        public float RespawnMaxZ;

        public void Execute()
        {
            Random random = RandomState[0];
            int hits = 0;
            while (Hits.TryDequeue(out HitEvent hitEvent))
            {
                int enemy = hitEvent.Enemy.Index;
                if (EnemyGeneration[enemy] != hitEvent.Enemy.Generation)
                {
                    continue; // 이미 죽고 다시 태어난 적: 옛 화살은 무시
                }

                hits++;
                int hp = EnemyHp[enemy] - hitEvent.Damage;
                if (hp > 0)
                {
                    EnemyHp[enemy] = hp;
                    continue;
                }

                Stats[PerfLabStat.DeathsTotal]++;
                EnemyGeneration[enemy] = EnemyGeneration[enemy] + 1;
                EnemyHp[enemy] = EnemyMaxHp;
                EnemyAtWall[enemy] = 0;
                EnemyPositions[enemy] = new float3(
                    random.NextFloat(RespawnMinX, RespawnMaxX),
                    0f,
                    random.NextFloat(RespawnMinZ, RespawnMaxZ));
            }

            RandomState[0] = random;
            Stats[PerfLabStat.HitsThisFrame] = hits;

            int free = FreeCount[0];
            for (int slot = 0; slot < ArrowEnded.Length; slot++)
            {
                if (ArrowEnded[slot] == 0)
                {
                    continue;
                }

                ArrowEnded[slot] = 0;
                FreeSlots[free] = slot;
                free++;
            }

            FreeCount[0] = free;
            Stats[PerfLabStat.ActiveArrows] = ArrowEnded.Length - free;
        }
    }

    // ⑦ 적 그리기 데이터: 걷기는 0~3번 칸, 성벽에 닿으면 공격 4~7번 칸
    [BurstCompile(CompileSynchronously = true)]
    public struct WriteEnemyInstancesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> AnimPhases;
        [ReadOnly] public NativeArray<byte> AtWall;
        [WriteOnly] public NativeArray<PerfLabInstance> Instances;
        public float Time;
        public float WalkFps;
        public float AttackFps;
        public int FramesPerAction;
        public float4 TintSize;

        public void Execute(int index)
        {
            bool attacking = AtWall[index] != 0;
            float fps = attacking ? AttackFps : WalkFps;
            float frame = math.floor(math.frac(Time * fps / FramesPerAction + AnimPhases[index]) * FramesPerAction);
            if (attacking)
            {
                frame += FramesPerAction;
            }

            Instances[index] = new PerfLabInstance
            {
                PosFrame = new float4(Positions[index], frame),
                TintSize = TintSize,
            };
        }
    }

    // ⑦ 화살 그리기 데이터: 쓰지 않는 슬롯은 크기 0 (그려지지 않음)
    [BurstCompile(CompileSynchronously = true)]
    public struct WriteArrowInstancesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> ArrowActive;
        [ReadOnly] public NativeArray<float3> ArrowPositions;
        [WriteOnly] public NativeArray<PerfLabInstance> Instances;
        public float4 TintSize;

        public void Execute(int slot)
        {
            bool active = ArrowActive[slot] != 0;
            Instances[slot] = new PerfLabInstance
            {
                PosFrame = new float4(ArrowPositions[slot], 0f),
                TintSize = active ? TintSize : float4.zero,
            };
        }
    }
}
