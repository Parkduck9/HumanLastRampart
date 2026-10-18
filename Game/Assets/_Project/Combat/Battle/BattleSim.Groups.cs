using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 병력 묶음 이동 (D-072): 분대를 10명 묶음으로 나눠 지도에서 다른 성벽으로 보낸다
    // - 묶음은 성벽 통로를 따라 달려간다(전령과 같은 속도). 가는 동안은 쏘지 않고, 어느 성벽 병력에도 세지 않는다
    // - 떠날 때 원래 성벽의 처음·남은 병력에서 빼고, 닿으면 새 성벽에 더한다 — 떠났다고 그 성벽이 70% 손실 패배가 되지 않게
    // - 목적지는 성벽만 고른다. 그 성벽에서 빈 총안이 가장 많은 구간을 골라 가까운 빈 총안부터 선다 (모자라면 돌 뒤 엇갈린 줄, K-045 BattleSim.Slots)
    // - 통로 안쪽 길로 달리고, 떠날 때·닿을 때 몇 m는 자기 자리와 그 길 사이를 비스듬히 걷는다 (K-039)
    // - 전투 준비 중에는 바로 옮긴다(시간이 멈춰 있으므로). 전투 중 주인공이 없는 성벽의 묶음은 전령이 닿아야 출발 (BattleEntry)
    // - 대포 분대는 대포에 붙어 있으므로 옮기지 않는다
    public sealed partial class BattleSim
    {
        public const int GroupSize = 10;

        public struct GroupInfo
        {
            public int Side;          // 지금 성벽 (-1 = 옮기는 중)
            public int TargetSide;    // 옮기는 중이면 갈 성벽
            public int Alive;
            public bool Movable;
            public bool Moving;
            public float3 Center;     // 살아 있는 병사 가운데 (지도 표시용)
            public string UnitName;   // 병종 이름 (궁수 등)
        }

        sealed class GroupMove
        {
            public int Group;
            public int FromSide;
            public int TargetSide;
            public float3[] StartSpot;   // 떠난 자리 — 여기서 달리는 길로 비스듬히 나간다 (K-039)
            public float3[] EndSpot;     // 맡아 둔 총안 자리 (K-045) — 마지막 몇 m를 비스듬히 들어간다
            public float[] StartU;
            public float[] Direction;
            public float[] Distance;
            public float Travelled;
        }

        readonly List<GroupMove> groupMoves = new List<GroupMove>();

        public int GroupCount => squads.Count;

        public GroupInfo GetGroup(int index)
        {
            SquadInfo squad = squads[index];
            var center = float3.zero;
            int alive = 0;
            for (int i = squad.FirstSoldier; i < squad.FirstSoldier + squad.Alive; i++)
            {
                if (sAlive[i] == 0) continue;
                center += sPos[i];
                alive++;
            }

            GroupMove move = groupMoves.Find(m => m.Group == index);
            return new GroupInfo
            {
                Side = squad.Side,
                TargetSide = move != null ? move.TargetSide : squad.Side,
                Alive = squad.Alive,
                Movable = squad.Movable,
                Moving = move != null || squad.Side < 0,
                Center = alive > 0 ? center / alive : float3.zero,
                UnitName = squad.UnitName,
            };
        }

        // 묶음을 다른 성벽으로. instant = 전투 준비 중(바로 그 자리에 선다). 못 옮기면 false와 알림
        public bool MoveGroup(int index, int targetSide, bool instant)
        {
            if (Ended || index < 0 || index >= squads.Count || targetSide < 0 || targetSide > 3) return false;
            SquadInfo squad = squads[index];
            if (!squad.Movable) { Notice?.Invoke("대포 분대는 옮길 수 없습니다"); return false; }
            if (squad.Side < 0) { Notice?.Invoke("이미 옮기는 중인 묶음입니다"); return false; }
            if (squad.Side == targetSide) return false;
            if (squad.Alive <= 0) return false;
            for (int i = squad.FirstSoldier; i < squad.FirstSoldier + squad.Alive; i++)
            {
                if (sAway[i] != 0) { Notice?.Invoke("묶음 병사 중 전령으로 나간 병사가 있어 지금은 못 옮깁니다"); return false; }
            }

            int from = squad.Side;
            LeaveSide(index, from);   // 원래 성벽 자리를 비운다
            // 설 자리를 떠나기 전에 정해 맡아 둔다 — 병사마다 자기 총안 자리로 곧장 달려간다 (사용자 2차 피드백 10, K-045).
            // 묶음 구간 = 빈 앞줄 자리가 가장 많은 곳, 그 안에서 가까운 빈 총안부터
            float center = ChooseGroupWindow(targetSide, squad.Alive, out float lo, out float hi);
            var spots = new float3[squad.Alive];
            for (int k = 0; k < squad.Alive; k++) spots[k] = ClaimSlot(squad.FirstSoldier + k, targetSide, lo, hi, center);
            if (instant)
            {
                ArriveSide(index, targetSide, spots);
                Notice?.Invoke($"{squad.UnitName} {squad.Alive}명이 {SideNames[from]}에서 {SideNames[targetSide]}으로");
                return true;
            }

            var move = new GroupMove
            {
                Group = index, FromSide = from, TargetSide = targetSide,
                StartSpot = new float3[squad.Alive], EndSpot = spots,
                StartU = new float[squad.Alive], Direction = new float[squad.Alive], Distance = new float[squad.Alive],
            };
            float perimeter = CastleHalf * 8f;
            float longest = 0f;
            float3 targetRight = WallGeometry.Right(targetSide);
            for (int k = 0; k < squad.Alive; k++)
            {
                int soldier = squad.FirstSoldier + k;
                StopWalk(soldier);
                float endU = RingU(targetSide, math.dot(spots[k], targetRight));   // 자기 자리
                float startU = RingU(from, LateralOf(soldier, from));
                float forward = Mod(endU - startU, perimeter);
                float backward = perimeter - forward;
                move.StartSpot[k] = sPos[soldier];
                move.StartU[k] = startU;
                move.Direction[k] = forward <= backward ? 1f : -1f;
                move.Distance[k] = math.min(forward, backward);
                longest = math.max(longest, move.Distance[k]);
                sAway[soldier] = 1;   // 가는 동안 쏘지 않는다
            }

            groupMoves.Add(move);
            Notice?.Invoke($"{squad.UnitName} {squad.Alive}명이 {SideNames[targetSide]}으로 달려갑니다 · 약 {longest / MessengerSpeed():0}초");
            return true;
        }

        // 한 번에 옮기기 (사용자 2차 피드백 11): 한 성벽의 옮길 수 있는 묶음을 다른 성벽으로 보내되,
        // 그 성벽에 옮길 수 있는 병사를 최소 KeepPerWall명(방어 10 + 전령용 10) 남긴다. 대포 분대는 원래 안 옮긴다
        public const int KeepPerWall = 20;

        public int MoveWallGroups(int fromSide, int toSide, bool instant)
        {
            if (Ended || fromSide == toSide || fromSide < 0 || fromSide > 3 || toSide < 0 || toSide > 3) return 0;
            var candidates = new List<int>();
            int movable = 0;
            for (int g = 0; g < squads.Count; g++)
            {
                SquadInfo s = squads[g];
                if (!s.Movable || s.Side != fromSide || s.Alive <= 0) continue;
                movable += s.Alive;
                candidates.Add(g);
            }

            // 가는 쪽 끝에 가까운 묶음부터 (달리는 거리가 짧게)
            float3 right = WallGeometry.Right(fromSide), toward = WallGeometry.Outward(toSide);
            float sign = math.sign(math.dot(right, toward));
            candidates.Sort((a, b) => (sign * GroupLateral(b, fromSide)).CompareTo(sign * GroupLateral(a, fromSide)));
            int moved = 0;
            foreach (int g in candidates)
            {
                int alive = squads[g].Alive;
                if (movable - alive < KeepPerWall) continue;   // 이 묶음까지 보내면 최소 병력보다 적어짐
                if (!MoveGroup(g, toSide, instant)) continue;
                movable -= alive;
                moved += alive;
            }

            if (moved == 0) Notice?.Invoke($"{SideNames[fromSide]}: 최소 {KeepPerWall}명을 남기면 더 보낼 병력이 없습니다");
            else Notice?.Invoke($"{SideNames[fromSide]}에서 {SideNames[toSide]}으로 {moved}명 보냄 · {movable}명 지킴");
            return moved;
        }

        float GroupLateral(int g, int side)
        {
            SquadInfo s = squads[g];
            float sum = 0f;
            for (int i = s.FirstSoldier; i < s.FirstSoldier + s.Alive; i++) sum += LateralOf(i, side);
            return s.Alive > 0 ? sum / s.Alive : 0f;
        }

        void UpdateGroupMoves(float dt)
        {
            for (int g = groupMoves.Count - 1; g >= 0; g--)
            {
                GroupMove move = groupMoves[g];
                SquadInfo squad = squads[move.Group];
                move.Travelled += MessengerSpeed() * RunSpeedRatio(squad.FirstSoldier) * dt;   // 대포병 묶음은 느리게 (K-034)
                bool done = true;
                for (int k = 0; k < move.StartU.Length && k < squad.Alive; k++)
                {
                    int soldier = squad.FirstSoldier + k;
                    float t = math.min(move.Travelled, move.Distance[k]);
                    if (t < move.Distance[k])
                    {
                        done = false;
                        // 처음 몇 m는 떠난 자리에서, 마지막 몇 m는 설 자리로 비스듬히 (K-039 의견 6: 도착해서 뚝 끊기던 것)
                        float3 ring = RingPosition(move.StartU[k] + move.Direction[k] * t);
                        float left = move.Distance[k] - t;
                        sPos[soldier] = t < left ? WalkIn(ring, move.StartSpot[k], t) : WalkIn(ring, move.EndSpot[k], left);
                        SetRunning(soldier, move.Direction[k]);
                    }
                    else
                    {
                        // 먼저 닿은 병사는 자기 자리에 서서 기다린다 (모두 닿을 때까지 한곳에 겹쳐 있지 않게)
                        sPos[soldier] = move.EndSpot[k];
                        SetRunning(soldier, 0f);
                    }
                }

                SoldiersDirty = true;
                if (!done) continue;
                groupMoves.RemoveAt(g);
                ArriveSide(move.Group, move.TargetSide, move.EndSpot);
                Notice?.Invoke($"{squad.UnitName} {squad.Alive}명이 {SideNames[move.TargetSide]}에 도착");
            }

            UpdateCannonMoves(dt);   // 대포 옮기기 (K-060, BattleSim.Haul.cs)
        }

        void LeaveSide(int index, int side)
        {
            SquadInfo squad = squads[index];
            SideStatus status = Sides[side];
            status.GarrisonStart -= squad.Alive;   // 떠난 병력은 그 성벽 손실로 세지 않는다
            status.GarrisonAlive -= squad.Alive;
            Sides[side] = status;
            squad.Side = -1;
            squads[index] = squad;
            for (int i = squad.FirstSoldier; i < squad.FirstSoldier + squad.Alive; i++) ReleaseSlot(i);   // 자리를 비운다 (K-045)
        }

        // 맡아 둔 자리(spots, MoveGroup의 ClaimSlot)에 세운다
        void ArriveSide(int index, int side, float3[] spots)
        {
            SquadInfo squad = squads[index];
            for (int k = 0; k < squad.Alive && k < spots.Length; k++)
            {
                int soldier = squad.FirstSoldier + k;
                StopWalk(soldier);
                sPos[soldier] = spots[k];
                sSide[soldier] = (byte)side;
                sAway[soldier] = 0;
                SetRunning(soldier, 0f);
            }

            squad.Side = side;
            squads[index] = squad;
            SideStatus status = Sides[side];
            status.GarrisonStart += squad.Alive;
            status.GarrisonAlive += squad.Alive;
            Sides[side] = status;
            SoldiersDirty = true;
        }
    }
}
