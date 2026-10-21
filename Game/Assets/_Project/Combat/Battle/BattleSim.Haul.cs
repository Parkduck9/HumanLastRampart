using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 대포 옮기기 (K-060, 사용자 확정 2026-10-18): 지도에서 대포를 골라 다른 성벽의 빈 대포 사격구로 보낸다.
    // - 성벽마다 사격구 CannonBaysPerSide곳 (대포가 놓인 지점 + 빈 사격구). 빈 사격구가 없으면 거절
    // - 대포와 대포병(앞 병사 당김·뒤 병사 밈, Codex K-052 그림)이 성벽 통로를 따라 느리게 간다. 가는 동안 쏘지 않는다
    // - 떠난 사격구는 비고, 도착하면 그 사격구의 대포가 된다. 칠 수 있는 들판(K-042)은 도착 뒤 다시 계산
    // - 전투 준비 중에는 바로 옮겨진다. 전투 중 주인공이 없는 성벽이면 전령이 닿아야 출발 (묶음 이동과 같은 규칙, BattleEntry)
    // - 성벽 병력(처음·남은 병력) 처리는 묶음 이동과 같다 (LeaveSide·ArriveSide)
    public sealed partial class BattleSim
    {
        public const int CannonBaysPerSide = 6;   // 제안: 대포 4문 + 빈 사격구 2곳 (사용자 확인 전)
        const float SpareBaySpacing = 1.6f;       // 빈 사격구는 다른 사격구에서 사격구 폭의 이 배 넘게 떨어진 곳에
        // 끄는 걸음 (Codex K-052): 칸마다 발이 32px 나아가고 디딘 발은 제자리 → 이동 속도 = 걸음 길이 × 칸 속도라야 발이 미끄러지지 않는다
        public const float HaulFps = 12f;
        public static float HaulSpeed => CannonArt.CrewStepPx * CannonArt.MetersPerPx(SoldierDrawSize) * HaulFps;   // 약 0.64m/초

        public struct CannonHaul
        {
            public Vector3 Position;    // 대포 놓는 기준점 (앞바퀴 접지)
            public Vector3 Direction;   // 옮겨 가는 쪽 (수평 단위 벡터)
            public float Travelled;     // 지금까지 끈 거리(m) — 걷기 칸·바퀴 회전을 이 값으로 맞춘다 (발 미끄러짐 없게)
            public bool PushCrew;       // 미는 병사도 있는가 (운용병이 둘 이상)
        }

        sealed class CannonMove
        {
            public int Cannon;
            public int Squad;
            public int FromSide, TargetSide;
            public float TargetBay;     // 도착할 사격구 (성벽 따라 m)
            public float3 StartSpot, EndSpot;
            public float StartU, Direction, Distance, Travelled;
        }

        readonly List<CannonHaul> cannonHauls = new List<CannonHaul>();
        readonly List<CannonMove> cannonMoves = new List<CannonMove>();
        public IReadOnlyList<CannonHaul> CannonHauls => cannonHauls;
        public bool CannonMoving(int index) => cannonMoves.Exists(m => m.Cannon == index);

        // BuildSoldiers에서 대포 지점의 사격구를 모은 뒤: 성벽마다 빈 사격구를 더해 CannonBaysPerSide곳으로 (성벽 3D·병사 자리가 이것을 본다)
        void AddSpareCannonBays()
        {
            float bay = BattleWallLayout.Proportion.BayWidth;
            float from = BattleWallLayout.From(CastleHalf) + bay, to = BattleWallLayout.To(CastleHalf) - bay;
            const int candidates = 24;
            for (int side = 0; side < 4; side++)
            {
                List<float> bays = cannonBays[side];
                while (bays.Count < CannonBaysPerSide)
                {
                    // 기존 사격구에서 가장 먼 후보 (같으면 성벽 가운데 쪽)
                    float best = float.NaN, bestGap = -1f;
                    for (int k = 0; k < candidates; k++)
                    {
                        float c = math.lerp(from, to, (k + 0.5f) / candidates);
                        float gap = float.MaxValue;
                        foreach (float b in bays) gap = math.min(gap, math.abs(b - c));
                        gap -= math.abs(c) * 1e-3f;
                        if (gap > bestGap) { bestGap = gap; best = c; }
                    }

                    if (float.IsNaN(best) || bestGap < bay * SpareBaySpacing) break;
                    bays.Add(best);
                }

                bays.Sort();
            }
        }

        // 그 사격구에 대포가 있거나 오는 중인가
        bool BayTaken(int side, float lateral)
        {
            for (int i = 0; i < cannons.Count; i++)
            {
                if (cannons[i].side != side || CannonMoving(i)) continue;
                if (math.abs(math.dot(CannonSpot(cannons[i].soldier), WallGeometry.Right(side)) - lateral) < 0.2f) return true;
            }

            foreach (CannonMove m in cannonMoves) if (m.TargetSide == side && math.abs(m.TargetBay - lateral) < 0.2f) return true;
            return false;
        }

        public int FreeCannonBays(int side)
        {
            int n = 0;
            foreach (float b in cannonBays[side]) if (!BayTaken(side, b)) n++;
            return n;
        }

        // 지도에서 고른 대포를 그 성벽으로. instant = 전투 준비 중 (바로 그 사격구에). 못 옮기면 false와 알림
        public bool MoveCannon(int index, int targetSide, bool instant)
        {
            if (Ended || index < 0 || index >= cannons.Count || targetSide < 0 || targetSide > 3) return false;
            var (shooter, side, number, _) = cannons[index];
            if (side == targetSide) return false;
            if (CannonMoving(index)) { Notice?.Invoke("이미 옮기는 중인 대포입니다"); return false; }
            int squad = squads.FindIndex(s => s.FirstSoldier == shooter);
            if (squad < 0 || sAlive[shooter] == 0 || squads[squad].Alive <= 0) { Notice?.Invoke("운용병이 없는 대포는 옮길 수 없습니다"); return false; }
            for (int i = shooter; i < shooter + squads[squad].Alive; i++)
                if (sAway[i] != 0) { Notice?.Invoke("대포병이 전령으로 나가 있어 지금은 못 옮깁니다"); return false; }

            // 떠나는 자리에서 둘레 길로 가장 가까운 빈 사격구
            float3 spot = CannonSpot(shooter);
            float startU = RingU(side, math.dot(spot, WallGeometry.Right(side)));
            float perimeter = CastleHalf * 8f, bestDistance = float.MaxValue, bestBay = 0f, bestDirection = 1f;
            bool found = false;
            foreach (float b in cannonBays[targetSide])
            {
                if (BayTaken(targetSide, b)) continue;
                float forward = Mod(RingU(targetSide, b) - startU, perimeter), backward = perimeter - forward;
                float distance = math.min(forward, backward);
                if (distance >= bestDistance) continue;
                bestDistance = distance; bestBay = b; bestDirection = forward <= backward ? 1f : -1f; found = true;
            }

            if (!found)
            {
                Notice?.Invoke($"{SideNames[targetSide]}에 빈 대포 사격구가 없습니다");
                return false;
            }

            LeaveSide(squad, side);                       // 성벽 병력에서 뺀다 (묶음 이동과 같게)
            if (sFuse.IsCreated) sFuse[shooter] = 0f;     // 불붙이던 중이면 취소
            if (instant)
            {
                PlaceCannon(index, squad, targetSide, bestBay);
                Notice?.Invoke($"{SideNames[side]} 대포 {number} → {SideNames[targetSide]}");
                return true;
            }

            var move = new CannonMove
            {
                Cannon = index, Squad = squad, FromSide = side, TargetSide = targetSide, TargetBay = bestBay,
                StartSpot = spot, EndSpot = WallSpot(targetSide, bestBay, 0),
                StartU = startU, Direction = bestDirection, Distance = bestDistance,
            };
            for (int i = shooter; i < shooter + squads[squad].Alive; i++)
            {
                StopWalk(i);
                sAway[i] = 1;   // 가는 동안 쏘지 않는다 (서 있는 그림도 숨기고 끌기 그림으로)
                SetRunning(i, 0f);
            }

            cannonMoves.Add(move);
            Notice?.Invoke($"{SideNames[side]} 대포 {number}이 {SideNames[targetSide]}으로 출발 (약 {bestDistance / HaulSpeed:0}초)");
            return true;
        }

        // 묶음 이동 갱신 뒤(BeginFrame): 끄는 대포를 둘레 길로 나아가게 하고 그림 상태를 채운다
        void UpdateCannonMoves(float dt)
        {
            cannonHauls.Clear();
            for (int k = cannonMoves.Count - 1; k >= 0; k--)
            {
                CannonMove m = cannonMoves[k];
                var (shooter, _, _, _) = cannons[m.Cannon];
                SquadInfo squad = squads[m.Squad];
                m.Travelled = math.min(m.Travelled + HaulSpeed * dt, m.Distance);
                float u = m.StartU + m.Direction * m.Travelled;
                float left = m.Distance - m.Travelled;
                // 떠날 때·닿을 때 몇 m는 사격구와 통로 길 사이를 비스듬히 (묶음 이동과 같게)
                float3 at = m.Travelled < left ? WalkIn(RingPosition(u), m.StartSpot, m.Travelled) : WalkIn(RingPosition(u), m.EndSpot, left);
                float3 ahead = RingPosition(u + m.Direction * 0.5f) - RingPosition(u);
                float3 dir = math.lengthsq(ahead) > 1e-6f ? math.normalize(new float3(ahead.x, 0f, ahead.z)) : WallGeometry.Right(m.FromSide) * m.Direction;
                cannonSpots[shooter] = at;
                for (int i = shooter; i < shooter + squad.Alive; i++) sPos[i] = at;   // 지도 점은 대포 자리 (그림은 BattlePropView가 대포 앞뒤에)
                SoldiersDirty = true;

                if (m.Travelled < m.Distance)
                {
                    cannonHauls.Add(new CannonHaul
                    {
                        Position = new Vector3(at.x, at.y, at.z),
                        Direction = new Vector3(dir.x, 0f, dir.z),
                        Travelled = m.Travelled,
                        PushCrew = squad.Alive >= 2,
                    });
                    continue;
                }

                cannonMoves.RemoveAt(k);
                PlaceCannon(m.Cannon, m.Squad, m.TargetSide, m.TargetBay);
                Notice?.Invoke($"대포가 {SideNames[m.TargetSide]} 사격구에 도착 (대포 {cannons[m.Cannon].number})");
            }
        }

        // 그 사격구에 대포를 놓고 운용병을 양옆에 세운다. 성벽 병력에 더하고, 칠 수 있는 들판을 다시 계산
        void PlaceCannon(int index, int squad, int side, float bay)
        {
            var (shooter, _, _, postId) = cannons[index];
            int number = 1;
            for (int i = 0; i < cannons.Count; i++) if (i != index && cannons[i].side == side) number = math.max(number, cannons[i].number + 1);
            cannons[index] = (shooter, side, number, postId);
            cannonSpots[shooter] = WallSpot(side, bay, 0);
            int alive = squads[squad].Alive;
            var spots = new float3[alive];
            for (int k = 0; k < alive; k++)
            {
                spots[k] = CannonCrewSpot(shooter, side, bay, k);   // 생성 때의 점화 깊이도 유지한다
            }

            ArriveSide(squad, side, spots);
            if (sHasAim.IsCreated) { sHasAim[shooter] = 0; sIdle[shooter] = 0; }   // 예전 조준점은 다른 성벽 들판 — 지운다
            InitCannonReach();
        }

        // 바로 옮기기 (전투 준비 중이면 순간, 아니면 끌고) — 작업이 도는 중이면 다음 프레임 처음에
        public void IssueCannonMove(int index, int targetSide, bool instant) => RunOrQueue(() => MoveCannon(index, targetSide, instant));

        // 주인공이 없는 성벽의 대포 — 전령이 그 대포의 성벽에 닿으면 출발 (묶음 이동과 같은 규칙)
        public void IssueCannonMoveByMessenger(int fromSide, float fromAlong, int index, int targetSide)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() =>
            {
                if (index < 0 || index >= cannons.Count || CannonMoving(index)) return;
                int cannonSide = cannons[index].side;
                if (StartMessenger(fromSide, fromAlong, cannonSide, PriorityOf(cannonSide), issuedAt))
                {
                    Messenger m = messengers[messengers.Count - 1];
                    m.MoveCannon = index;
                    m.MoveTarget = targetSide;
                }
            });
        }

        // 지도에서 R 키로 옮기기를 고를 때 안내 (BattleEntry)
        public void NoticeCannonMoveArmed(int index)
        {
            if (index < 0 || index >= cannons.Count) return;
            var (_, side, number, _) = cannons[index];
            Notice?.Invoke($"{SideNames[side]} 대포 {number} 옮기기: 보낼 성벽 쪽 들판을 누르세요 (다시 R = 취소)");
        }

        // 서 있는 그림을 숨길 병사 (대포를 끄는 중 — 끌기 그림이 대신 그린다)
        bool Hauling(int soldier)
        {
            foreach (CannonMove m in cannonMoves)
            {
                SquadInfo squad = squads[m.Squad];
                if (soldier >= squad.FirstSoldier && soldier < squad.FirstSoldier + squad.Alive) return true;
            }

            return false;
        }
    }
}
