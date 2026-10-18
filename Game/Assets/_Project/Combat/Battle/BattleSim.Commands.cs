using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽별 명령 (CY2-C3, Battle.md 3.1-1, C-011, D-028·D-030·D-039)
    // - 명령하기: 지금 서 있는 성벽에 즉시
    // - 전령: 병사 한 명이 성벽 위 길을 따라 달려가 도착하면 전달 (주인공 속도의 70%), 뛰는 동안 쏘지 않음
    // - 소리 지르기: 즉시. 목표 성벽이 못 들을 수 있고, 다른 성벽이 잘못 듣고 따를 수 있다
    // - 겹침: 나중에 "내린" 명령이 이긴다 (D-039 C16-1). 늦게 도착한 옛 명령은 무시하고 알린다
    public sealed partial class BattleSim
    {
        public const float HeroSpeed = 5f;   // 주인공 달리기(m/초) — 성벽 한 면(40m) 약 8초 (D-071, 예전 10m는 120m 성벽 기준). 전령은 이 값 × 비율(0.7)
        public const float HeroWalkSpeed = 2.5f; // 통로 안쪽·바깥쪽 걷기(m/초) — 달리기와 구분 (D-049, D-071)

        public enum CommandVia { Direct, Messenger, Shout, Global }

        static readonly string[] SideNames = { "북쪽", "동쪽", "남쪽", "서쪽" };

        public static readonly string[] PriorityNames =
        {
            "체력 낮은 적부터", "체력 높은 적부터", "가까운 적부터", "오르는 적부터", "특수 적 먼저", "밀집한 곳 쏘기",
        };

        sealed class Messenger
        {
            public int Soldier;
            public int TargetSide;
            public TargetPriority Priority;
            public float IssuedAt;
            public float StartU;        // 성벽 위 둘레 위치 (0 ~ 8H)
            public float EndU;
            public float Direction;     // +1 시계, -1 반시계
            public float Distance;
            public float Travelled;
            public bool Returning;
            public float3 Home;
            public int MoveGroup = -1;     // D-072: 명령 대신 '이 묶음을 MoveTarget 성벽으로' 전하러 간다. -2 = 그 성벽 묶음 한 번에(MoveWallGroups)
            public int MoveTarget;
            public int MoveCannon = -1;    // K-060: '이 대포를 MoveTarget 성벽으로' 전하러 간다
        }

        readonly TargetPriority[] wallPriority = new TargetPriority[4];
        readonly float[] wallIssuedAt = new float[4];
        readonly List<Messenger> messengers = new List<Messenger>();
        readonly List<Action> pendingCommands = new List<Action>();
        Unity.Mathematics.Random commandRandom;

        // 명령이 실제로 적용될 때 (BattleEntry가 Campaign.Targeting에 기록, C-011)
        public event Action<int, TargetPriority> WallCommandApplied;
        public event Action<TargetPriority> GlobalCommandApplied;
        // 플레이어에게 보여 줄 알림
        public event Action<string> Notice;

        public TargetPriority PriorityOf(int side) => wallPriority[side];
        public int MessengersRunning => messengers.Count;

        void InitCommands(Allocator p)
        {
            sidePriority = new NativeArray<byte>(4, p);
            TargetingSettings initial = setup.InitialTargeting ?? TargetingSettings.CreateDefault();
            for (int side = 0; side < 4; side++)
            {
                wallPriority[side] = initial.PriorityFor(WallIds.Of((WallSide)side));
                wallIssuedAt[side] = -1f;   // 전투 전 설정: 전투 중 어떤 명령보다 이르다
                sidePriority[side] = (byte)wallPriority[side];
            }

            commandRandom = new Unity.Mathematics.Random(seed ^ 0x5bd1e995u);
        }

        // 메뉴 버튼은 작업(Job)이 도는 중에 눌릴 수 있다 — 그때는 줄 세웠다가 다음 프레임 처음(BeginFrame)에 처리한다.
        // 내린 시각은 누른 순간으로 정한다.
        void RunOrQueue(Action action)
        {
            if (scheduled) pendingCommands.Add(action);
            else action();
        }

        void RunPendingCommands()
        {
            for (int i = 0; i < pendingCommands.Count; i++) pendingCommands[i]();
            pendingCommands.Clear();
        }

        // ① 명령하기 — 지금 서 있는 성벽
        public void IssueDirect(int side, TargetPriority priority)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() => Apply(side, priority, issuedAt, CommandVia.Direct));
        }

        // 전체 명령 (지도, D-027) — 모든 성벽에 같은 시각으로. 성벽 명령은 지워진다(C-011 SetGlobal)
        public void IssueGlobal(TargetPriority priority)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() => ApplyGlobal(priority, issuedAt));
        }

        void ApplyGlobal(TargetPriority priority, float issuedAt)
        {
            if (Ended) return;
            for (int side = 0; side < 4; side++)
            {
                wallPriority[side] = priority;
                wallIssuedAt[side] = issuedAt;
                sidePriority[side] = (byte)priority;
            }

            GlobalCommandApplied?.Invoke(priority);
            Notice?.Invoke($"전체: {PriorityNames[(int)priority]}");
        }

        // ③ 소리 지르기 — 즉시, 못 들음·잘못 들음 (D-030). 비·밤이면 확률 증가
        public void IssueShout(int fromSide, int targetSide, TargetPriority priority)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() => ApplyShout(fromSide, targetSide, priority, issuedAt));
        }

        void ApplyShout(int fromSide, int targetSide, TargetPriority priority, float issuedAt)
        {
            if (Ended || fromSide == targetSide) return;
            float bonus = BadHearing() && rules != null ? rules.ShoutBadConditionBonus : 0f;
            bool opposite = (fromSide + 2) % 4 == targetSide;
            float notHeard = (rules != null ? (opposite ? rules.ShoutNotHeardOpposite : rules.ShoutNotHeardAdjacent) : (opposite ? 0.3f : 0.1f)) + bonus;

            if (commandRandom.NextFloat() < notHeard)
            {
                Notice?.Invoke($"{SideNames[targetSide]}이 외침을 듣지 못했습니다");
            }
            else
            {
                Apply(targetSide, priority, issuedAt, CommandVia.Shout);
            }

            // 다른 성벽이 잘못 듣고 따름: 목표의 옆 두 벽 각각 15%, 맞은편 30% (주인공이 서 있는 벽은 직접 들으므로 제외)
            for (int other = 0; other < 4; other++)
            {
                if (other == targetSide || other == fromSide) continue;
                bool otherOpposite = (targetSide + 2) % 4 == other;
                float misheard = (rules != null ? (otherOpposite ? rules.ShoutMisheardOpposite : rules.ShoutMisheardAdjacent) : (otherOpposite ? 0.3f : 0.15f)) + bonus;
                if (commandRandom.NextFloat() < misheard)
                {
                    Notice?.Invoke($"{SideNames[other]}이 잘못 듣고 따릅니다");
                    Apply(other, priority, issuedAt, CommandVia.Shout);
                }
            }
        }

        // ② 전령 — 주인공 가까이의 병사 한 명이 달려간다
        public void IssueMessenger(int fromSide, float fromAlong, int targetSide, TargetPriority priority)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() => StartMessenger(fromSide, fromAlong, targetSide, priority, issuedAt));
        }

        bool StartMessenger(int fromSide, float fromAlong, int targetSide, TargetPriority priority, float issuedAt)
        {
            if (Ended || fromSide == targetSide) return false;
            int soldier = NearestFreeSoldier(fromSide, fromAlong);
            if (soldier < 0)
            {
                Notice?.Invoke($"{SideNames[fromSide]}에 보낼 병사가 없습니다");
                return false;
            }

            float startU = RingU(fromSide, LateralOf(soldier, fromSide));
            float endU = RingU(targetSide, 0f);   // 그 성벽 병력 가운데
            float perimeter = CastleHalf * 8f;
            float forward = Mod(endU - startU, perimeter);
            float backward = perimeter - forward;
            var m = new Messenger
            {
                Soldier = soldier,
                TargetSide = targetSide,
                Priority = priority,
                IssuedAt = issuedAt,
                StartU = startU,
                EndU = endU,
                Direction = forward <= backward ? 1f : -1f,
                Distance = math.min(forward, backward),
                Home = HomeSpot(soldier),   // 맡아 둔 총안 자리로 돌아온다 (K-045)
            };
            sAway[soldier] = 1;
            messengers.Add(m);
            Notice?.Invoke($"전령이 {SideNames[targetSide]}으로 출발 (약 {m.Distance / MessengerSpeed():0}초)");
            return true;
        }

        // D-072: 주인공이 없는 성벽의 묶음을 옮길 때 — 전령이 그 묶음의 성벽에 닿아야 출발한다
        public void IssueGroupMoveByMessenger(int fromSide, float fromAlong, int group, int targetSide)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() =>
            {
                if (group < 0 || group >= squads.Count || squads[group].Side < 0) return;
                int groupSide = squads[group].Side;
                if (StartMessenger(fromSide, fromAlong, groupSide, PriorityOf(groupSide), issuedAt))
                {
                    Messenger m = messengers[messengers.Count - 1];
                    m.MoveGroup = group;
                    m.MoveTarget = targetSide;
                }
            });
        }

        // 한 번에 옮기기를 전령으로 (주인공이 없는 성벽) — 전령이 그 성벽에 닿으면 MoveWallGroups
        public void IssueWallMoveByMessenger(int fromSide, float fromAlong, int wallSide, int targetSide)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() =>
            {
                if (StartMessenger(fromSide, fromAlong, wallSide, PriorityOf(wallSide), issuedAt))
                {
                    Messenger m = messengers[messengers.Count - 1];
                    m.MoveGroup = -2;
                    m.MoveTarget = targetSide;
                }
            });
        }

        public void IssueWallMove(int wallSide, int targetSide, bool instant) => RunOrQueue(() => MoveWallGroups(wallSide, targetSide, instant));

        // 바로 옮기기 (전투 준비 중이면 순간, 아니면 달려서) — BattleEntry가 주인공 성벽 묶음에 쓴다
        public void IssueGroupMove(int group, int targetSide, bool instant)
        {
            RunOrQueue(() => MoveGroup(group, targetSide, instant));
        }

        float MessengerSpeed() => HeroSpeed * (rules != null ? rules.MessengerSpeedRatio : 0.7f);

        void UpdateMessengers(float dt)
        {
            for (int i = messengers.Count - 1; i >= 0; i--)
            {
                Messenger m = messengers[i];
                if (sAlive[m.Soldier] == 0)
                {
                    // 원래 성벽에서 병사를 잃을 때 전령이 뽑혔다 (C16-2: 전령도 원래 성벽 병력)
                    if (!m.Returning) Notice?.Invoke($"{SideNames[m.TargetSide]}으로 가던 전령을 잃었습니다");
                    sAway[m.Soldier] = 0;
                    SetRunning(m.Soldier, 0f);
                    messengers.RemoveAt(i);
                    continue;
                }

                m.Travelled = math.min(m.Travelled + MessengerSpeed() * RunSpeedRatio(m.Soldier) * dt, m.Distance);   // 대포병은 느리게 (K-034)
                float u = m.Returning ? m.EndU - m.Direction * m.Travelled : m.StartU + m.Direction * m.Travelled;
                // 떠날 때는 자기 자리에서 달리는 길로, 돌아올 때는 마지막 몇 m를 자기 자리로 비스듬히 (K-039 의견 6: 순간 이동 없앰)
                sPos[m.Soldier] = WalkIn(RingPosition(u), m.Home, m.Returning ? m.Distance - m.Travelled : m.Travelled);
                SetRunning(m.Soldier, m.Returning ? -m.Direction : m.Direction);
                SoldiersDirty = true;

                if (m.Travelled < m.Distance) continue;
                if (!m.Returning)
                {
                    if (m.MoveCannon >= 0) MoveCannon(m.MoveCannon, m.MoveTarget, false);   // 전령이 닿으면 대포가 출발 (K-060)
                    else if (m.MoveGroup >= 0) MoveGroup(m.MoveGroup, m.MoveTarget, false);   // 전령이 닿으면 묶음이 출발
                    else if (m.MoveGroup == -2) MoveWallGroups(m.TargetSide, m.MoveTarget, false);   // 한 번에 옮기기
                    else Apply(m.TargetSide, m.Priority, m.IssuedAt, CommandVia.Messenger);
                    m.Returning = true;
                    m.Travelled = 0f;
                }
                else
                {
                    sPos[m.Soldier] = m.Home;
                    sAway[m.Soldier] = 0;
                    SetRunning(m.Soldier, 0f);
                    messengers.RemoveAt(i);
                }
            }
        }

        // 나중에 내린 명령만 적용 (D-039 C16-1·C16-4)
        void Apply(int side, TargetPriority priority, float issuedAt, CommandVia via)
        {
            if (Ended) return;
            if (issuedAt < wallIssuedAt[side])
            {
                Notice?.Invoke($"{SideNames[side]}: {ViaName(via)} 명령이 더 새 명령에 밀려 무시됨");
                return;
            }

            wallPriority[side] = priority;
            wallIssuedAt[side] = issuedAt;
            sidePriority[side] = (byte)priority;
            WallCommandApplied?.Invoke(side, priority);
            Notice?.Invoke($"{SideNames[side]}: {PriorityNames[(int)priority]} ({ViaName(via)})");
        }

        static string ViaName(CommandVia via)
        {
            switch (via)
            {
                case CommandVia.Messenger: return "전령";
                case CommandVia.Shout: return "외침";
                case CommandVia.Global: return "전체";
                default: return "직접";
            }
        }

        bool BadHearing() => CurrentWeather == Weather.Rain || TimeOfDay == TimeOfDay.Night;   // 지금 날씨 기준

        int NearestFreeSoldier(int side, float along)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            foreach (SquadInfo squad in squads)
            {
                // 대포 분대(옮길 수 없는 분대)는 통째로 뺀다 — 쏘는 대포병이 뽑혀 그동안 대포가 멈추던 버그 (K-039 의견 8).
                // 우회 보고(BattleSim.Flank)도 이 함수로 고른다
                if (squad.Side != side || !squad.Movable) continue;
                for (int i = squad.FirstSoldier; i < squad.FirstSoldier + squad.Alive; i++)
                {
                    if (sAlive[i] == 0 || sAway[i] != 0 || sWeapon[i] < 0) continue;
                    float d = math.abs(LateralOf(i, side) - along);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
            }

            return best;
        }

        float LateralOf(int soldier, int side) => math.dot(sPos[soldier], WallGeometry.Right(side));

        // 성벽 위 둘레 좌표: 북(0) → 동 → 남 → 서, 각 성벽은 바깥을 볼 때 왼쪽 끝에서 오른쪽 끝으로 2H
        float RingU(int side, float along) => side * 2f * CastleHalf + math.clamp(along, -CastleHalf, CastleHalf) + CastleHalf;

        float3 RingPosition(float u)
        {
            float perimeter = CastleHalf * 8f;
            u = Mod(u, perimeter);
            int side = math.min(3, (int)(u / (2f * CastleHalf)));
            float along = u - side * 2f * CastleHalf - CastleHalf;
            return WallGeometry.Outward(side) * (CastleHalf - 1.4f) + WallGeometry.Right(side) * along + new float3(0f, WallHeight, 0f);
        }

        static float Mod(float a, float m) => a - m * math.floor(a / m);
    }
}
