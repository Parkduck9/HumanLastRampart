using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 병력 묶음 이동 (D-072): 분대를 10명 묶음으로 나눠 지도에서 다른 성벽으로 보낸다
    // - 묶음은 성벽 통로를 따라 달려간다(전령과 같은 속도). 가는 동안은 쏘지 않고, 어느 성벽 병력에도 세지 않는다
    // - 떠날 때 원래 성벽의 처음·남은 병력에서 빼고, 닿으면 새 성벽에 더한다 — 떠났다고 그 성벽이 70% 손실 패배가 되지 않게
    // - 목적지는 성벽만 고른다. 그 성벽에서 병사가 가장 드문 자리에 한 줄로 선다
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
            LeaveSide(index, from);
            if (instant)
            {
                ArriveSide(index, targetSide);
                Notice?.Invoke($"{squad.UnitName} {squad.Alive}명: {SideNames[from]} → {SideNames[targetSide]}");
                return true;
            }

            var move = new GroupMove { Group = index, FromSide = from, TargetSide = targetSide, StartU = new float[squad.Alive], Direction = new float[squad.Alive], Distance = new float[squad.Alive] };
            float perimeter = CastleHalf * 8f;
            float endU = RingU(targetSide, 0f);
            float longest = 0f;
            for (int k = 0; k < squad.Alive; k++)
            {
                int soldier = squad.FirstSoldier + k;
                float startU = RingU(from, LateralOf(soldier, from));
                float forward = Mod(endU - startU, perimeter);
                float backward = perimeter - forward;
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
                    if (t < move.Distance[k]) done = false;
                    sPos[soldier] = RingPosition(move.StartU[k] + move.Direction[k] * t);
                    SetRunning(soldier, move.Direction[k]);
                }

                SoldiersDirty = true;
                if (!done) continue;
                groupMoves.RemoveAt(g);
                ArriveSide(move.Group, move.TargetSide);
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

        // 새 성벽에서 병사가 가장 드문 자리를 골라 한 줄로 세운다
        void ArriveSide(int index, int side)
        {
            SquadInfo squad = squads[index];
            float span = (squad.Alive - 1) * SoldierMaxSpacing;
            float limit = CastleHalf - 1f - span * 0.5f;
            float bestCenter = 0f, bestGap = -1f;
            for (float c = -limit; c <= limit; c += 0.5f)
            {
                float gap = float.MaxValue;
                for (int q = 0; q < squads.Count; q++)
                {
                    if (q == index || squads[q].Side != side) continue;
                    for (int i = squads[q].FirstSoldier; i < squads[q].FirstSoldier + squads[q].Alive; i++)
                        gap = math.min(gap, math.abs(LateralOf(i, side) - c));
                }

                if (gap > bestGap) { bestGap = gap; bestCenter = c; }
            }

            float3 n3 = WallGeometry.Outward(side), right = WallGeometry.Right(side);
            for (int k = 0; k < squad.Alive; k++)
            {
                int soldier = squad.FirstSoldier + k;
                float lateral = bestCenter - span * 0.5f + k * SoldierMaxSpacing;
                sPos[soldier] = n3 * (CastleHalf - SoldierInset) + right * lateral + new float3(0f, WallHeight, 0f);
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
