using System.Collections.Generic;
using HumanLastRampart.Core.Traps;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 무작위 지형·지형 손질 전투 반응 (D-077, K-049 1단계)
    // - 지형: TerrainGenerator가 진행 데이터 씨앗 + 이 지형 손질로 만든 격자(칸 5m). 함정 지도와 같은 지형
    // - 막힘(언덕·토벽·나무·바위)을 처음 만난 적은 한 번 정한다: 50% 돌아감, 30% 넘음(막힘 칸 안에서 속도 −30%), 20% 토벽 허묾
    //   · 돌아감: 막힌 칸을 피해 성벽 앞(10m)까지 가장 짧은 길(면마다 미리 계산한 흐름 지도)을 따라간다
    //   · 허묾: 토벽 앞에 서서 몇 초 때린다 → 체력이 다하면 틈(Breach)이 되어 모두 지나간다. 흐름 지도는 다시 계산
    //   · 언덕은 허물 수 없어 허묾 → 넘음, 나무·바위는 늘 돌아감 (질문 Q1·Q2)
    //   · 돌아갈 길이 없으면(모든 길이 막힘, D-077 ⑥) 100% 넘거나 허묾 (30:20 비율)
    // - 엄폐: 성벽 쪽으로 한두 칸 안에 온전한 토벽이 있는 적은 총·화살(범위 아닌 무기)을 맞지 않는다. 대포·마법은 맞는다(사용자 확정)
    // 전투 중에는 칸 조회만 한다 (흐름 지도는 시작과 토벽이 무너질 때만 다시 계산)
    public sealed partial class BattleSim
    {
        // 측정·시험 비교용: false면 무작위 지형·지형 손질 없이 예전처럼 (정의 언덕만 그리고, 적 반응 없음)
        public static bool TerrainEnabled = true;

        TerrainLayout terrainLayout;
        NativeArray<byte> tCells;
        NativeArray<float2> tFlow;       // 면 × 칸
        NativeArray<byte> tReach;        // 면 × 칸
        NativeArray<float> tWallHp;      // 칸 (토벽만 의미)
        NativeArray<int> tDirty;         // [0] 토벽이 무너지면 1
        NativeArray<int> eTerrainHit;    // 적별: 이번 프레임 때리는 토벽 칸 (-1)
        NativeArray<byte> eCovered;      // 적별: 토벽 뒤 엄폐
        NativeArray<byte> eTerrainMode;  // 적별: 0 아직, 1 돌아감, 2 넘음, 3 허묾
        bool terrainActive;

        public TerrainLayout TerrainLayout => terrainLayout;
        public int TerrainVersion { get; private set; }   // 토벽이 무너질 때마다 늘어난다 (그리기가 다시 맞춘다)
        public byte TerrainKindAt(int cell) => tCells.IsCreated && cell >= 0 && cell < tCells.Length ? tCells[cell] : TerrainCell.Free;

        void InitTerrain(Allocator p)
        {
            terrainLayout = terrainDef == null ? TerrainLayout.Empty(null)
                : TerrainEnabled ? TerrainGenerator.Generate(terrainDef, setup.TerrainSeed, setup.TerrainWorks)
                : TerrainGenerator.FixedOnly(terrainDef);
            int cells = terrainLayout.Cells.Length;
            tCells = new NativeArray<byte>(terrainLayout.Cells, p);
            tFlow = new NativeArray<float2>(4 * cells, p);
            tReach = new NativeArray<byte>(4 * cells, p);
            tWallHp = new NativeArray<float>(cells, p);
            tDirty = new NativeArray<int>(1, p);
            int n = math.max(1, enemyCount);
            eTerrainHit = new NativeArray<int>(n, p);
            for (int i = 0; i < n; i++) eTerrainHit[i] = -1;
            eCovered = new NativeArray<byte>(n, p);
            eTerrainMode = new NativeArray<byte>(n, p);

            terrainActive = false;
            for (int c = 0; c < cells; c++)
            {
                if (TerrainCell.Blocks(tCells[c]) || tCells[c] == TerrainCell.Path) terrainActive = true;
                if (tCells[c] == TerrainCell.EarthWall) tWallHp[c] = TerrainRules.EarthWallHp;
                if (tCells[c] == TerrainCell.Trees) tWallHp[c] = TerrainRules.TreeFallPressure;   // 나무 덩이: 쓰러질 때까지 남은 '적 누적 초' (K-051)
            }

            if (!TerrainEnabled) terrainActive = false;
            if (terrainActive) BuildFlow();
        }

        // 작업이 끝난 뒤(BeginFrame) — 토벽이 무너졌으면 흐름 지도를 다시
        void UpdateTerrain()
        {
            if (!terrainActive || tDirty[0] == 0) return;
            tDirty[0] = 0;
            TerrainVersion++;
            BuildFlow();
        }

        // 적 이동 작업에 넘길 지형 묶음
        TerrainField TerrainData => new TerrainField
        {
            Cells = tCells,
            Flow = tFlow,
            Reach = tReach,
            HitCell = eTerrainHit,
            Covered = eCovered,
            Mode = eTerrainMode,
            MinX = -terrainLayout.Extent,
            CellSize = terrainLayout.CellSize,
            Size = terrainLayout.Size,
            Seed = seed,
            CastleHalf = CastleHalf,
            Active = (byte)(terrainActive ? 1 : 0),
        };

        // 적 이동 다음: 토벽을 때린 피해를 모아 무너뜨린다
        JobHandle ScheduleTerrain(JobHandle afterStep, float dt)
        {
            if (!terrainActive) return afterStep;
            return new TerrainHitJob
            {
                HitCell = eTerrainHit,
                Cells = tCells,
                WallHp = tWallHp,
                Dirty = tDirty,
                Damage = TerrainRules.BreakDamagePerSecond * dt,
                Push = dt,
            }.Schedule(afterStep);
        }

        // 흐름 지도: 면마다 성벽 앞(10m 안, 성벽 폭) 칸에서 거꾸로 퍼져 나가는 가장 짧은 길 (8방향, 막힌 칸은 못 지나감).
        // 칸마다 '다음에 갈 이웃 칸' 방향을 저장한다. 대각선은 양옆 두 칸이 모두 열려 있을 때만 (모서리 끼임 방지)
        // 계산용 배열은 한 번만 만들어 다시 쓴다 (K-061: 토벽·나무가 무너질 때마다 칸 수 × 8 힙 등 약 0.7MB를 새로 만들어
        // 불바다·폭발 순간 프레임 할당이 806KB까지 튀었다). 결과는 같다 — 비용은 면마다 처음부터 다시 채우고 힙은 비운다
        float[] flowCost;
        MinHeap flowHeap;

        void BuildFlow()
        {
            int size = terrainLayout.Size, cells = size * size;
            if (flowCost == null || flowCost.Length != cells)
            {
                flowCost = new float[cells];
                flowHeap = new MinHeap(cells);
            }

            float[] cost = flowCost;
            MinHeap heap = flowHeap;
            for (int side = 0; side < 4; side++)
            {
                float3 n = WallGeometry.Outward(side), r = WallGeometry.Right(side);
                for (int c = 0; c < cells; c++) cost[c] = float.MaxValue;
                heap.Clear();
                for (int c = 0; c < cells; c++)
                {
                    if (!Passable(tCells[c])) continue;
                    Vector2 center = terrainLayout.Center(c);
                    float depth = center.x * n.x + center.y * n.z - CastleHalf;
                    float lateral = math.abs(center.x * r.x + center.y * r.z);
                    if (depth > 0f && depth <= TerrainRules.FreeZone + terrainLayout.CellSize && lateral <= CastleHalf + terrainLayout.CellSize)
                    {
                        cost[c] = 0f;
                        heap.Push(c, 0f);
                    }
                }

                while (heap.TryPop(out int c, out float d))
                {
                    if (d > cost[c]) continue;
                    int ci = c % size, cj = c / size;
                    for (int k = 0; k < 8; k++)
                    {
                        int ni = ci + Dx[k], nj = cj + Dz[k];
                        if (ni < 0 || nj < 0 || ni >= size || nj >= size) continue;
                        int nc = nj * size + ni;
                        if (!Passable(tCells[nc])) continue;
                        if (k >= 4 && (!Passable(tCells[cj * size + ni]) || !Passable(tCells[nj * size + ci]))) continue;
                        float nd = d + (k >= 4 ? 1.4142f : 1f) * StepCost(tCells[nc]);   // 낸 길은 싸고 잔해는 비싸다 (K-051)
                        if (nd >= cost[nc]) continue;
                        cost[nc] = nd;
                        heap.Push(nc, nd);
                    }
                }

                int b = side * cells;
                for (int c = 0; c < cells; c++)
                {
                    if (cost[c] == float.MaxValue) { tReach[b + c] = 0; tFlow[b + c] = float2.zero; continue; }
                    tReach[b + c] = 1;
                    if (cost[c] == 0f) { tFlow[b + c] = new float2(-n.x, -n.z); continue; }
                    int ci = c % size, cj = c / size, best = -1;
                    float bestCost = cost[c];
                    for (int k = 0; k < 8; k++)
                    {
                        int ni = ci + Dx[k], nj = cj + Dz[k];
                        if (ni < 0 || nj < 0 || ni >= size || nj >= size) continue;
                        int nc = nj * size + ni;
                        if (k >= 4 && (!Passable(tCells[cj * size + ni]) || !Passable(tCells[nj * size + ci]))) continue;
                        if (cost[nc] < bestCost) { bestCost = cost[nc]; best = k; }
                    }

                    tFlow[b + c] = best < 0 ? new float2(-n.x, -n.z) : math.normalize(new float2(Dx[best], Dz[best]));
                }
            }
        }

        static bool Passable(byte kind) => !TerrainCell.Blocks(kind);
        static float StepCost(byte kind) => kind == TerrainCell.Path ? TerrainRules.PathCost : kind == TerrainCell.FallenTrees ? TerrainRules.DebrisCost : 1f;
        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

        // 지도(2번 화면) 표시: 막힌 칸 가운데와 색 (토벽 갈색·무너진 틈 옅은 갈색·나무 초록·바위 회색). 언덕은 3D 둔덕이 보인다
        public void ForEachTerrainMark(System.Action<Vector3, Color> add)
        {
            if (terrainLayout == null || !tCells.IsCreated) return;
            void Mark(List<int> list, Color color)
            {
                foreach (int c in list)
                {
                    Vector2 p = terrainLayout.Center(c);
                    byte kind = tCells[c];
                    add(new Vector3(p.x, 0f, p.y), kind == TerrainCell.Breach ? new Color(0.62f, 0.52f, 0.38f) : color);
                }
            }

            Mark(terrainLayout.EarthWallCells, new Color(0.55f, 0.36f, 0.18f));
            foreach (int c in terrainLayout.TreeCells)
            {
                // 쓰러진 나무 잔해는 누런 갈색 (K-051)
                Vector2 p = terrainLayout.Center(c);
                add(new Vector3(p.x, 0f, p.y), tCells[c] == TerrainCell.FallenTrees ? new Color(0.5f, 0.42f, 0.22f) : new Color(0.16f, 0.45f, 0.18f));
            }

            Mark(terrainLayout.RockCells, new Color(0.62f, 0.62f, 0.6f));
            Mark(terrainLayout.PathCells, new Color(0.72f, 0.6f, 0.42f));
        }

        void ReleaseTerrain()
        {
            Release(ref tCells);
            Release(ref tFlow);
            Release(ref tReach);
            Release(ref tWallHp);
            Release(ref tDirty);
            Release(ref eTerrainHit);
            Release(ref eCovered);
            Release(ref eTerrainMode);
        }

        // 흐름 지도 계산용 작은 최소 힙 (칸 번호, 비용)
        sealed class MinHeap
        {
            readonly int[] items;
            readonly float[] keys;
            int count;

            public MinHeap(int capacity)
            {
                items = new int[capacity * 8 + 8];
                keys = new float[capacity * 8 + 8];
            }

            public void Clear() => count = 0;

            public void Push(int item, float key)
            {
                if (count >= items.Length) return;
                int i = count++;
                items[i] = item;
                keys[i] = key;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (keys[parent] <= keys[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public bool TryPop(out int item, out float key)
            {
                if (count == 0) { item = -1; key = 0f; return false; }
                item = items[0];
                key = keys[0];
                count--;
                items[0] = items[count];
                keys[0] = keys[count];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < count && keys[l] < keys[m]) m = l;
                    if (r < count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }

                return true;
            }

            void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (keys[a], keys[b]) = (keys[b], keys[a]);
            }
        }
    }

    // 적 이동 작업이 읽는 지형 (칸 조회만 — 가볍게)
    public struct TerrainField
    {
        [ReadOnly] public NativeArray<byte> Cells;
        [ReadOnly] public NativeArray<float2> Flow;
        [ReadOnly] public NativeArray<byte> Reach;
        public NativeArray<int> HitCell;
        public NativeArray<byte> Covered;
        public NativeArray<byte> Mode;
        public float MinX;
        public float CellSize;
        public int Size;
        public uint Seed;
        public float CastleHalf;
        public byte Active;

        public const byte Undecided = 0, Detour = 1, Cross = 2, Break = 3;
        const float LookAhead = 3.5f;   // 이만큼 앞 칸이 막혔으면 반응한다

        public int CellOf(float3 p)
        {
            int i = (int)math.floor((p.x - MinX) / CellSize), j = (int)math.floor((p.z - MinX) / CellSize);
            return i < 0 || j < 0 || i >= Size || j >= Size ? -1 : j * Size + i;
        }

        public byte KindOf(int cell) => cell < 0 ? TerrainCell.Free : Cells[cell];

        // 들판에 있지 않은 적(성벽 앞·오르기·성벽 위·죽음): 지형 상태를 지운다
        public void Clear(int i)
        {
            HitCell[i] = -1;
            Covered[i] = 0;
        }

        // 들판을 걷는 적의 속도를 지형에 맞게 바꾼다. straight = 지금까지처럼 곧장(깔때기) 가는 속도
        public float3 Steer(int i, float3 position, float3 straight, int side, float fromWall)
        {
            HitCell[i] = -1;
            float3 n = WallGeometry.Outward(side);
            // 엄폐: 성벽 쪽으로 2.5m·5m 안에 온전한 토벽
            byte behind1 = KindOf(CellOf(position - n * (CellSize * 0.5f))), behind2 = KindOf(CellOf(position - n * CellSize));
            Covered[i] = (byte)(behind1 == TerrainCell.EarthWall || behind2 == TerrainCell.EarthWall ? 1 : 0);
            if (fromWall <= TerrainRules.FreeZone) return straight;   // 성벽 바로 앞은 지금처럼 곧장

            float speed = math.length(straight);
            float3 dir = speed > 1e-4f ? straight / speed : -n;
            int here = CellOf(position);
            int ahead = CellOf(position + dir * LookAhead);
            byte hereKind = KindOf(here), aheadKind = KindOf(ahead);
            bool inside = TerrainCell.Blocks(hereKind), blockedAhead = TerrainCell.Blocks(aheadKind);
            // 나무 덩이에 부딪혀 지나가는 적은 나무를 밀어 쓰러뜨린다 (K-051, D-077 ⑤) — 토벽 때리기와 구별해 음수로 적는다
            if (aheadKind == TerrainCell.Trees) HitCell[i] = -(ahead + 2);
            else if (hereKind == TerrainCell.Trees) HitCell[i] = -(here + 2);
            // 쓰러진 나무 잔해 위는 느리다
            if (hereKind == TerrainCell.FallenTrees) { straight *= TerrainRules.DebrisSpeed; speed *= TerrainRules.DebrisSpeed; }

            byte mode = Mode[i];
            if (mode == Undecided)
            {
                if (!inside && !blockedAhead) return straight;
                mode = Decide(i, side, here, inside, inside ? hereKind : aheadKind);
                Mode[i] = mode;
            }

            if (mode == Detour)
            {
                int f = side * Cells.Length + here;
                if (here >= 0 && !inside && Reach[f] != 0)
                {
                    float2 d = Flow[f];
                    return new float3(d.x, 0f, d.y) * speed;
                }

                if (here < 0) return straight;   // 격자 밖 (들판 끝 너머)
                // 가던 길이 끊겼다(새로 막힘·막힘 안에 들어섬): 넘거나 허문다
                mode = Fallback(i, inside ? hereKind : aheadKind);
                Mode[i] = mode;
            }

            if (mode == Break && aheadKind == TerrainCell.EarthWall && !inside)
            {
                HitCell[i] = ahead;   // 토벽 앞에 서서 때린다 (TerrainHitJob이 피해를 모은다)
                return float3.zero;
            }

            // 넘음 (허물 토벽이 없는 허묾도): 곧장, 막힘 칸 안에서는 느리게
            return inside ? straight * TerrainRules.CrossSpeed : straight;
        }

        byte Decide(int i, int side, int here, bool inside, byte kind)
        {
            float roll = Roll(i, 0x51u);
            byte mode = roll < TerrainRules.DetourChance ? Detour : roll < TerrainRules.DetourChance + TerrainRules.CrossChance ? Cross : Break;
            if (kind == TerrainCell.Trees || kind == TerrainCell.Rocks) mode = Detour;   // 나무·바위는 돌아간다 (쓰러진 나무 잔해가 되면 지나간다)
            if (kind == TerrainCell.Hill) mode = roll < TerrainRules.HillDetourChance ? Detour : Cross;   // 사용자 Q1: 언덕은 돌아감 70%·넘음 30%
            if (inside && mode == Detour) mode = Cross;                                  // 이미 막힘 안 — 그대로 넘는다
            if (mode == Detour && (here < 0 || Reach[side * Cells.Length + here] == 0)) mode = Fallback(i, kind);   // 모든 길이 막힘 (D-077 ⑥)
            return mode;
        }

        // 돌아갈 길이 없을 때: 100% 넘거나 허문다 (30:20). 토벽이 아니면 넘음
        byte Fallback(int i, byte kind)
        {
            if (kind != TerrainCell.EarthWall) return Cross;
            return Roll(i, 0xA7u) < TerrainRules.BlockedCrossShare ? Cross : Break;
        }

        float Roll(int i, uint salt) => (math.hash(new uint3((uint)i, Seed, salt)) & 0xFFFFFF) / 16777216f;
    }

    // 토벽 허물기 (단일): 이번 프레임 때린 적 수만큼 체력을 깎고, 다하면 틈으로 바꾼다
    [BurstCompile(CompileSynchronously = true)]
    public struct TerrainHitJob : IJob
    {
        [ReadOnly] public NativeArray<int> HitCell;
        public NativeArray<byte> Cells;
        public NativeArray<float> WallHp;
        public NativeArray<int> Dirty;
        public float Damage;   // 한 마리가 이번 프레임에 주는 피해
        public float Push;     // 나무 덩이에 부딪힌 한 마리가 이번 프레임에 쌓는 '누적 초' (K-051)

        public void Execute()
        {
            for (int i = 0; i < HitCell.Length; i++)
            {
                int c = HitCell[i];
                if (c <= -2)
                {
                    // 나무 덩이: 많이 부딪히면 쓰러진 나무 잔해가 된다 (길이 열린다)
                    int tree = -c - 2;
                    if (Cells[tree] != TerrainCell.Trees) continue;
                    float left = WallHp[tree] - Push;
                    WallHp[tree] = left;
                    if (left <= 0f)
                    {
                        Cells[tree] = TerrainCell.FallenTrees;
                        Dirty[0] = 1;
                    }

                    continue;
                }

                if (c < 0 || Cells[c] != TerrainCell.EarthWall) continue;
                float hp = WallHp[c] - Damage;
                WallHp[c] = hp;
                if (hp <= 0f)
                {
                    Cells[c] = TerrainCell.Breach;
                    Dirty[0] = 1;
                }
            }
        }
    }
}
