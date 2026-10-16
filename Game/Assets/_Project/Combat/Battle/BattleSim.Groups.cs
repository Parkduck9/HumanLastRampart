using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 병력 묶음 이동 (D-072): 분대를 10명 묶음으로 나눠 지도에서 다른 성벽으로 보낸다
    // - 묶음은 성벽 통로를 따라 달려간다(전령과 같은 속도). 가는 동안은 쏘지 않고, 어느 성벽 병력에도 세지 않는다
    // - 떠날 때 원래 성벽의 처음·남은 병력에서 빼고, 닿으면 새 성벽에 더한다 — 떠났다고 그 성벽이 70% 손실 패배가 되지 않게
    // - 목적지는 성벽만 고른다. 그 성벽에서 병사가 가장 드문 자리에 한 줄로 선다 (바깥 줄이 차 있으면 조금 안쪽 줄, K-039)
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
            public float TargetCenter;   // 도착해서 설 자리(묶음 가운데, 성벽 따라 m)
            public int TargetRow;        // 도착해서 설 줄 (0 바깥, K-039)
            public float3[] StartSpot;   // 떠난 자리 — 여기서 달리는 길로 비스듬히 나간다 (K-039)
            public float3[] EndSpot;     // 설 자리 — 마지막 몇 m를 비스듬히 들어간다
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
            // 설 자리를 떠나기 전에 정한다 — 병사마다 자기 자리로 곧장 달려간다 (사용자 2차 피드백 10: 한곳에 모였다가 한 번에 뿅 옮겨지던 것)
            var (center, row) = ChooseSpot(index, targetSide);
            LeaveSide(index, from);
            if (instant)
            {
                ArriveSide(index, targetSide, center, row);
                Notice?.Invoke($"{squad.UnitName} {squad.Alive}명: {SideNames[from]} → {SideNames[targetSide]}");
                return true;
            }

            var move = new GroupMove
            {
                Group = index, FromSide = from, TargetSide = targetSide, TargetCenter = center, TargetRow = row,
                StartSpot = new float3[squad.Alive], EndSpot = new float3[squad.Alive],
                StartU = new float[squad.Alive], Direction = new float[squad.Alive], Distance = new float[squad.Alive],
            };
            float perimeter = CastleHalf * 8f;
            float longest = 0f;
            for (int k = 0; k < squad.Alive; k++)
            {
                int soldier = squad.FirstSoldier + k;
                float lateral = GroupLateralAt(squad.Alive, center, k);
                float endU = RingU(targetSide, lateral);   // 자기 자리
                float startU = RingU(from, LateralOf(soldier, from));
                float forward = Mod(endU - startU, perimeter);
                float backward = perimeter - forward;
                move.StartSpot[k] = sPos[soldier];
                move.EndSpot[k] = WallSpot(targetSide, lateral, row);
                move.StartU[k] = startU;
                move.Direction[k] = forward <= backward ? 1f : -1f;
                move.Distance[k] = math.min(forward, backward);
                longest = math.max(longest, move.Distance[k]);
                sAway[soldier] = 1;   // 가는 동안 쏘지 않는다
            }

            groupMoves.Add(move);
            Notice?.Invoke($"{squad.UnitName} {squad.Alive}명이 {SideNames[targetSide]}으로 출발 (약 {longest / MessengerSpeed():0}초)");
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
            else Notice?.Invoke($"{SideNames[fromSide]} → {SideNames[toSide]}: {moved}명 보냄 ({movable}명 남김)");
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
                ArriveSide(move.Group, move.TargetSide, move.TargetCenter, move.TargetRow);
                Notice?.Invoke($"{squad.UnitName} {squad.Alive}명이 {SideNames[move.TargetSide]}에 도착");
            }
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
        }

        // 묶음 병사 k의 성벽 따라 자리 (묶음 가운데 center, 간격 SoldierMaxSpacing)
        static float GroupLateralAt(int alive, float center, int k) => center - (alive - 1) * SoldierMaxSpacing * 0.5f + k * SoldierMaxSpacing;

        // 새 성벽에서 병사가 가장 드문 자리(묶음 가운데)와 줄. 이미 그 성벽으로 가는 중인 묶음의 자리도 피한다.
        // 바깥 줄부터 보고, 그 줄에 그림이 안 겹치는 자리(묶음 양끝에서 다른 병사까지 SoldierMinSpacing 이상)가 있으면 거기,
        // 없으면 한 줄 안쪽 (K-039 의견 1). 모든 줄이 차 있으면 가장 덜 겹치는 곳
        (float center, int row) ChooseSpot(int index, int side)
        {
            SquadInfo squad = squads[index];
            float half = (squad.Alive - 1) * SoldierMaxSpacing * 0.5f;
            float limit = math.max(0f, CastleHalf - 1f - half);
            float bestCenter = 0f, bestGap = -1f;
            int bestRow = 0;
            for (int row = 0; row < SoldierMaxRows; row++)
            {
                float rowCenter = 0f, rowGap = -1f;
                for (float c = -limit; c <= limit; c += 0.5f)
                {
                    float gap = float.MaxValue;
                    for (int q = 0; q < squads.Count; q++)
                    {
                        if (q == index || squads[q].Side != side) continue;
                        for (int i = squads[q].FirstSoldier; i < squads[q].FirstSoldier + squads[q].Alive; i++)
                        {
                            if (sAlive[i] == 0 || sAway[i] != 0 || RowOf(i, side) != row) continue;   // 전령으로 나간 병사는 빈자리로 본다
                            gap = math.min(gap, math.max(0f, math.abs(LateralOf(i, side) - c) - half));
                        }
                    }

                    foreach (GroupMove other in groupMoves)
                    {
                        if (other.TargetSide != side || other.Group == index || other.TargetRow != row) continue;
                        float otherHalf = (squads[other.Group].Alive - 1) * SoldierMaxSpacing * 0.5f;
                        gap = math.min(gap, math.max(0f, math.abs(other.TargetCenter - c) - otherHalf - half));
                    }

                    if (gap > rowGap) { rowGap = gap; rowCenter = c; }
                }

                if (rowGap >= SoldierMinSpacing) return (rowCenter, row);
                if (rowGap > bestGap) { bestGap = rowGap; bestCenter = rowCenter; bestRow = row; }
            }

            return (bestCenter, bestRow);
        }

        // 정해 둔 자리(center, 줄)에 한 줄로 세운다
        void ArriveSide(int index, int side, float center, int row)
        {
            SquadInfo squad = squads[index];
            for (int k = 0; k < squad.Alive; k++)
            {
                int soldier = squad.FirstSoldier + k;
                sPos[soldier] = WallSpot(side, GroupLateralAt(squad.Alive, center, k), row);
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
