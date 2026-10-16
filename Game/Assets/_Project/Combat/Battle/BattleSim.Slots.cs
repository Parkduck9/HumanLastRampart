using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽 위 병사 자리 (K-045, 사용자 확정 2026-10-16)
    // - 앞줄은 난간 돌출부 사이 총안(빈칸)마다 1명. 총안·돌출부 자리는 성벽 3D와 같은 계산 (BattleWallLayout)
    // - 총안이 모자라면 한 줄 뒤 돌출부 뒤에 엇갈려 서고, 그래도 넘치면 그 뒤 줄도 엇갈려. 달리는 길(안쪽 1.4m)은 비운다. 뒷줄도 쏜다
    // - 분대·10명 묶음은 자기 구간 안에서 가까운 빈 자리부터 (구간 안 앞줄 → 구간 안 뒷줄 → 구간 밖)
    // - 병사가 쓰러지면 그 자리를 가까운 뒷줄 병사가 걸어 들어와 메운다. 전령·우회 보고는 자리를 맡겨 두고 그 자리로 돌아온다
    // - 대포 운용병은 자리 표를 쓰지 않는다 (대포 사격구 양옆, K-039 CannonSpot). 횃불을 단 돌출부 뒤에는 자리를 두지 않는다
    public sealed partial class BattleSim
    {
        // 줄별 성벽 바깥 모서리에서 안쪽 거리: 0 총안 → 1 돌 뒤 → 2 총안 줄 → (달리는 길 1.4m) → 3 돌 뒤 → 4 총안 줄
        static readonly float[] SlotRowInset = { 0.5f, 0.8f, 1.1f, 1.7f, 1.95f };
        const float RefillReach = 1.2f;       // 쓰러진 자리에서 이만큼(성벽 따라 m) 안의 뒷줄 병사가 앞으로 나온다
        const float SlotWalkSpeed = 1.2f;     // 자리 메우러 걷는 빠르기 (m/초)

        struct WallSlot
        {
            public float Lateral;
            public int Row;
            public int Occupant;   // 병사 번호, -1 = 빈자리
        }

        readonly List<WallSlot>[] wallSlots = new List<WallSlot>[4];
        readonly List<float>[] cannonBays = { new List<float>(), new List<float>(), new List<float>(), new List<float>() };
        readonly List<float>[] torchSpots = { new List<float>(), new List<float>(), new List<float>(), new List<float>() };
        int[] sSlot;            // 병사 → 자리 (side × SlotStride + 번호), -1 = 없음
        float3[] sWalkFrom, sWalkTo;
        float[] sWalkT;         // 0~1 걷는 중, -1 = 아님
        int overflowCount;
        const int SlotStride = 100000;

        // 성벽 따라 대포 사격구 자리 (성벽 3D가 난간을 낮추는 곳) / 횃불을 단 돌출부 자리 (BattleLights·BattleEffectsView)
        public IReadOnlyList<float> CannonBays(int side) => cannonBays[side];
        public IReadOnlyList<float> TorchSpots(int side) => torchSpots[side];

        // 확인용: 성벽 한 면의 줄별 자리 수
        public int SlotCount(int side, int row)
        {
            int n = 0;
            if (wallSlots[side] != null) foreach (WallSlot s in wallSlots[side]) if (s.Row == row) n++;
            return n;
        }

        // 확인용: 병사가 선 줄 (자리 없으면 -1)과 성벽, 성벽 따라 자리
        public int SoldierRow(int soldier, out int side, out float lateral)
        {
            lateral = 0f;
            side = -1;
            if (sSlot == null || soldier < 0 || soldier >= sSlot.Length || sSlot[soldier] < 0) return -1;
            side = sSlot[soldier] / SlotStride;
            WallSlot s = wallSlots[side][sSlot[soldier] % SlotStride];
            lateral = s.Lateral;
            return s.Row;
        }

        // BuildSoldiers 앞에서: 대포 사격구(cannonBays)를 채운 뒤 부른다
        void BuildWallSlots(int soldiers)
        {
            sSlot = new int[soldiers];
            sWalkFrom = new float3[soldiers];
            sWalkTo = new float3[soldiers];
            sWalkT = new float[soldiers];
            for (int i = 0; i < soldiers; i++) { sSlot[i] = -1; sWalkT[i] = -1f; }

            var embrasures = new List<float>();
            var merlons = new List<float>();
            for (int side = 0; side < 4; side++)
            {
                BattleWallLayout.Torches(CastleHalf, BattleLights.TorchInset, BattleLights.TorchSpacing, cannonBays[side], torchSpots[side]);
                BattleWallLayout.Embrasures(CastleHalf, cannonBays[side], embrasures);
                BattleWallLayout.Merlons(CastleHalf, cannonBays[side], merlons);
                merlons.RemoveAll(m => torchSpots[side].Contains(m));   // 횃불 돌 뒤에는 서지 않는다
                var list = new List<WallSlot>();
                for (int row = 0; row < SlotRowInset.Length; row++)
                    foreach (float lateral in row % 2 == 0 ? embrasures : merlons)
                        list.Add(new WallSlot { Lateral = lateral, Row = row, Occupant = -1 });
                wallSlots[side] = list;
            }
        }

        float3 SlotPosition(int encoded)
        {
            int side = encoded / SlotStride;
            WallSlot s = wallSlots[side][encoded % SlotStride];
            return WallGeometry.Outward(side) * (CastleHalf - SlotRowInset[s.Row]) + WallGeometry.Right(side) * s.Lateral + new float3(0f, WallHeight, 0f);
        }

        // 병사가 돌아갈 자리 (전령 복귀 등). 자리가 없으면 지금 위치
        float3 HomeSpot(int soldier)
        {
            if (sSlot == null || soldier < 0 || soldier >= sSlot.Length || sSlot[soldier] < 0) return sPos[soldier];
            return SlotPosition(sSlot[soldier]);
        }

        // 구간 [lo, hi] 안에서 center에 가까운 빈자리를 맡는다: 구간 안 앞줄 → 구간 안 뒷줄 → 구간 밖 가까운 곳(앞줄 먼저)
        // 성벽에 빈자리가 하나도 없으면 안쪽 끝에 겹쳐 세운다(정원 500명을 다 채운 성벽 — 자리 약 270개)
        float3 ClaimSlot(int soldier, int side, float lo, float hi, float center)
        {
            ReleaseSlot(soldier);
            List<WallSlot> list = wallSlots[side];
            int best = -1;
            float bestKey = float.MaxValue;
            for (int k = 0; k < list.Count; k++)
            {
                WallSlot s = list[k];
                if (s.Occupant >= 0) continue;
                float outside = s.Lateral < lo ? lo - s.Lateral : s.Lateral > hi ? s.Lateral - hi : 0f;
                float key = outside > 0f ? 1e6f + outside * 100f + s.Row : s.Row * 1000f + math.abs(s.Lateral - center);
                if (key < bestKey) { bestKey = key; best = k; }
            }

            if (best < 0)
            {
                float lateral = math.clamp(center + ((overflowCount++ % 9) - 4) * 0.24f, -CastleHalf + 1f, CastleHalf - 1f);
                return WallGeometry.Outward(side) * (CastleHalf - SlotRowInset[SlotRowInset.Length - 1]) + WallGeometry.Right(side) * lateral + new float3(0f, WallHeight, 0f);
            }

            WallSlot taken = list[best];
            taken.Occupant = soldier;
            list[best] = taken;
            sSlot[soldier] = side * SlotStride + best;
            return SlotPosition(sSlot[soldier]);
        }

        void ReleaseSlot(int soldier)
        {
            if (sSlot == null || soldier < 0 || soldier >= sSlot.Length || sSlot[soldier] < 0) return;
            int side = sSlot[soldier] / SlotStride, k = sSlot[soldier] % SlotStride;
            WallSlot s = wallSlots[side][k];
            if (s.Occupant == soldier)
            {
                s.Occupant = -1;
                wallSlots[side][k] = s;
            }

            sSlot[soldier] = -1;
        }

        // 묶음(alive명)을 세울 구간: 폭 = 인원 × 총안 간격. 빈 앞줄 자리가 가장 많은 곳(같으면 뒷줄 빈자리), 그다음 성벽 가운데에 가까운 곳
        float ChooseGroupWindow(int side, int alive, out float lo, out float hi)
        {
            BattleWorldView.Proportion p = BattleWallLayout.Proportion;
            float width = math.max(1, alive) * (p.MerlonWidth + p.MerlonGap);
            float from = BattleWallLayout.From(CastleHalf) + width * 0.5f, to = BattleWallLayout.To(CastleHalf) - width * 0.5f;
            if (to < from) from = to = 0f;
            float bestCenter = 0f, bestScore = -1f;
            for (float c = from; c <= to + 1e-3f; c += 0.25f)
            {
                float score = 0f;
                foreach (WallSlot s in wallSlots[side])
                {
                    if (s.Occupant >= 0 || math.abs(s.Lateral - c) > width * 0.5f) continue;
                    score += math.pow(10f, 3 - s.Row);
                }

                score -= math.abs(c) * 1e-4f;   // 같으면 가운데 쪽
                if (score > bestScore) { bestScore = score; bestCenter = c; }
            }

            lo = bestCenter - width * 0.5f;
            hi = bestCenter + width * 0.5f;
            return bestCenter;
        }

        // 쓰러진 병사 자리를 가까운 뒷줄 병사가 메운다 (그 병사가 비운 자리도 다시 메운다)
        void RefillAfterFall(int soldier)
        {
            if (sSlot == null || soldier < 0 || soldier >= sSlot.Length) return;
            int freed = sSlot[soldier];
            ReleaseSlot(soldier);
            for (int step = 0; step < 6 && freed >= 0; step++)
            {
                int side = freed / SlotStride;
                WallSlot hole = wallSlots[side][freed % SlotStride];
                int mover = -1;
                float moverKey = float.MaxValue;
                foreach (WallSlot s in wallSlots[side])
                {
                    int i = s.Occupant;
                    if (i < 0 || s.Row <= hole.Row || sAlive[i] == 0 || sAway[i] != 0) continue;
                    float d = math.abs(s.Lateral - hole.Lateral);
                    if (d > RefillReach) continue;
                    float key = d + 0.3f * (s.Row - hole.Row);
                    if (key < moverKey) { moverKey = key; mover = i; }
                }

                if (mover < 0) return;
                int next = sSlot[mover];
                ReleaseSlot(mover);
                var filled = hole;
                filled.Occupant = mover;
                wallSlots[side][freed % SlotStride] = filled;
                sSlot[mover] = freed;
                StartWalk(mover, SlotPosition(freed));
                freed = next;
            }
        }

        void StartWalk(int soldier, float3 to)
        {
            sWalkFrom[soldier] = sPos[soldier];
            sWalkTo[soldier] = to;
            sWalkT[soldier] = 0f;
        }

        void StopWalk(int soldier)
        {
            if (sWalkT != null && soldier >= 0 && soldier < sWalkT.Length) sWalkT[soldier] = -1f;
        }

        // BeginFrame에서: 자리 메우러 걷는 병사 (전령·묶음 이동으로 나가면 그만둔다)
        void UpdateSlotWalks(float dt)
        {
            if (sWalkT == null) return;
            for (int i = 0; i < soldierCount; i++)
            {
                if (sWalkT[i] < 0f) continue;
                if (sAlive[i] == 0 || sAway[i] != 0) { sWalkT[i] = -1f; continue; }
                float length = math.max(0.05f, math.distance(sWalkFrom[i], sWalkTo[i]));
                sWalkT[i] = math.min(1f, sWalkT[i] + SlotWalkSpeed * dt / length);
                sPos[i] = math.lerp(sWalkFrom[i], sWalkTo[i], math.smoothstep(0f, 1f, sWalkT[i]));
                if (sWalkT[i] >= 1f) sWalkT[i] = -1f;
                SoldiersDirty = true;
            }
        }
    }
}
