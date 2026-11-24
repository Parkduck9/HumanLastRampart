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
            public bool Assigned;       // 탑 명령: 전령에게 맡겨졌는가 (아니면 줄 서 있다)
            public float3 Home;
            public int MoveGroup = -1;     // D-072: 명령 대신 '이 묶음을 MoveTarget 성벽으로' 전하러 간다. -2 = 그 성벽 묶음 한 번에(MoveWallGroups)
            public int MoveTarget;
            public int MoveCount = int.MaxValue;   // 성벽 수량 명령을 전령이 그대로 전달
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
            Notice?.Invoke($"모든 성벽: {PriorityNames[(int)priority]}");
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
                float misheard = ((rules != null ? (otherOpposite ? rules.ShoutMisheardOpposite : rules.ShoutMisheardAdjacent) : (otherOpposite ? 0.3f : 0.15f)) + bonus) * Boost.ShoutMisheardMul;   // 큰 목청 호루라기 (K-058)
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

        // 주인공이 성벽에 없을 때(등대 탑, W9) 전령이 떠나는 곳. 어느 성벽이든 탑에서 출발하는 전령이 닿아야 명령이 적용된다
        public const int TowerOrigin = -1;

        // 탑 대기 전령 (W9, 사용자 결정): 주인공이 성벽에 없는 동안 모든 성벽 명령(북쪽 포함)은 탑에 대기 중인 전령 3명이 나른다.
        // 한 명이 명령 하나를 들고 탑에서 그 성벽 가운데까지 달려가 전하고(눈에 보인다) 탑으로 돌아와야 다시 쓸 수 있다.
        // 셋 다 나가 있으면 명령은 줄을 서고 알림이 뜬다. 전령은 기본으로 죽지 않는다 (병사가 아니라 탑 소속 — 아래 TowerMessengersMortal)
        public const int TowerCourierCount = 3;
        public const bool TowerMessengersMortal = false;   // 켜면(나중에 결정) 적이 닿는 성벽에서 잃을 수 있게 할 자리. 지금은 쓰이지 않는다

        public struct TowerCourierInfo
        {
            public bool Busy;
            public bool Returning;
            public int TargetSide;
            public Vector3 Position;    // 지금 발 위치 (대기 중이면 탑 곁 자리)
            public Vector3 Direction;   // 달리는 방향 (대기 중이면 0)
        }

        sealed class TowerCourier
        {
            public Messenger Order;     // 들고 가는 명령 (돌아오는 길에는 null)
            public bool Busy;
            public bool Returning;
            public int TargetSide;
            public float Travelled;
        }

        readonly TowerCourier[] towerCouriers = { new TowerCourier(), new TowerCourier(), new TowerCourier() };
        static readonly Vector3[] TowerStandby = { new Vector3(-2.1f, 0f, -0.5f), new Vector3(2.3f, 0f, -0.6f), new Vector3(-1.6f, 0f, -2.0f) };

        // 탑에서 그 성벽 병력 가운데 안쪽까지 직선 (마당 윗면 높이)
        Vector3 TowerRoute(int side)
        {
            float3 n = WallGeometry.Outward(side);
            return new Vector3(n.x, 0f, n.z) * (CastleHalf - 2f);
        }

        public float TowerRouteSeconds(int side) => TowerRoute(side).magnitude / (MessengerSpeed() * Boost.MessengerSpeedMul);
        public int TowerCouriersFree { get { int n = 0; foreach (TowerCourier c in towerCouriers) if (!c.Busy) n++; return n; } }
        public int TowerOrdersQueued { get { int n = 0; foreach (Messenger m in messengers) if (m.Soldier < 0 && !m.Assigned) n++; return n; } }

        public TowerCourierInfo GetTowerCourier(int i)
        {
            TowerCourier c = towerCouriers[i];
            var info = new TowerCourierInfo { Busy = c.Busy, Returning = c.Returning, TargetSide = c.TargetSide };
            Vector3 home = new Vector3(0f, WallHeight, 0f) + TowerStandby[i];
            if (!c.Busy) { info.Position = home; return info; }
            Vector3 route = TowerRoute(c.TargetSide);
            float length = Mathf.Max(0.01f, route.magnitude);
            float t = Mathf.Clamp01(c.Travelled / length);
            Vector3 start = new Vector3(0f, WallHeight, 0f);
            // 높은 탑(W20) 몸체 밖(1.5m)에서 달려 나가고 돌아온다 (탑 속을 지나지 않게)
            float out0 = Mathf.Min(1.5f, length);
            float progress = c.Returning ? 1f - t : t;
            // 출발·도착 가까이에서는 옆으로 비껴 선다 (남쪽 성벽 전령이 문 앞 주인공 자리에서 출발하지 않게)
            Vector3 side = Vector3.Cross(Vector3.up, route.normalized) * (0.9f * (1f - progress));
            info.Position = start + route.normalized * (out0 + (length - out0) * progress) + side;
            info.Direction = c.Returning ? -route.normalized : route.normalized;
            return info;
        }

        // 탑 전령 진행: 달려가 전하고, 돌아오면 쉬는 전령이 된다. 줄 선 명령을 쉬는 전령에게 맡긴다
        void UpdateTowerCouriers(float dt)
        {
            float speed = MessengerSpeed() * Boost.MessengerSpeedMul;
            foreach (TowerCourier c in towerCouriers)
            {
                if (!c.Busy) continue;
                float length = TowerRoute(c.TargetSide).magnitude;
                c.Travelled += speed * dt;
                if (c.Travelled < length) continue;
                if (!c.Returning)
                {
                    Messenger order = c.Order;
                    c.Order = null;
                    if (order != null)
                    {
                        Deliver(order);
                        messengers.Remove(order);
                    }

                    c.Returning = true;
                    c.Travelled = 0f;
                }
                else c.Busy = false;
            }

            for (int i = 0; i < messengers.Count; i++)
            {
                Messenger m = messengers[i];
                if (m.Soldier >= 0 || m.Assigned) continue;
                TowerCourier free = null;
                foreach (TowerCourier c in towerCouriers) if (!c.Busy) { free = c; break; }
                if (free == null) break;
                AssignCourier(free, m);
            }
        }

        void AssignCourier(TowerCourier c, Messenger m)
        {
            m.Assigned = true;
            c.Busy = true;
            c.Returning = false;
            c.Order = m;
            c.TargetSide = m.TargetSide;
            c.Travelled = 0f;
            Notice?.Invoke($"등대 전령이 {SideNames[m.TargetSide]}으로 달려갑니다 · 약 {TowerRouteSeconds(m.TargetSide):0}초");
        }

        // 분대·대포 이동 명령을 전령이 실제로 출발시켰을 때 (BattleEntry가 SquadOrders를 센다)
        public event Action SquadOrderStarted;

        bool StartMessenger(int fromSide, float fromAlong, int targetSide, TargetPriority priority, float issuedAt)
        {
            if (Ended) return false;
            if (fromSide == TowerOrigin)
            {
                // 탑 대기 전령 (병사가 아님, Soldier = -1): 쉬는 전령이 있으면 바로 출발, 없으면 줄을 선다
                var order = new Messenger { Soldier = -1, TargetSide = targetSide, Priority = priority, IssuedAt = issuedAt, Direction = 1f };
                messengers.Add(order);
                TowerCourier free = null;
                foreach (TowerCourier c in towerCouriers) if (!c.Busy) { free = c; break; }
                if (free != null) AssignCourier(free, order);
                else Notice?.Invoke($"등대 전령 {TowerCourierCount}명이 모두 나가 있습니다 — 명령이 줄을 섭니다 (대기 {TowerOrdersQueued}번째)");
                return true;
            }

            if (fromSide == targetSide)
            {
                Notice?.Invoke($"{SideNames[targetSide]}에는 이미 주인공이 있습니다");
                return false;
            }

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
            Notice?.Invoke($"전령이 {SideNames[targetSide]}으로 달려갑니다 · 약 {m.Distance / (MessengerSpeed() * Boost.MessengerSpeedMul):0}초");   // 전령 나팔 (K-058)
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
                    SquadOrderStarted?.Invoke();
                }
            });
        }

        // 한 번에 옮기기를 전령으로 (주인공이 없는 성벽) — 전령이 그 성벽에 닿으면 MoveWallGroups
        public void IssueWallMoveByMessenger(int fromSide, float fromAlong, int wallSide, int targetSide, int maxGroups = int.MaxValue)
        {
            float issuedAt = Elapsed;
            RunOrQueue(() =>
            {
                if (StartMessenger(fromSide, fromAlong, wallSide, PriorityOf(wallSide), issuedAt))
                {
                    Messenger m = messengers[messengers.Count - 1];
                    m.MoveGroup = -2;
                    m.MoveTarget = targetSide;
                    m.MoveCount = maxGroups;
                    SquadOrderStarted?.Invoke();
                }
            });
        }

        public void IssueWallMove(int wallSide, int targetSide, bool instant, int maxGroups = int.MaxValue) => RunOrQueue(() => MoveWallGroups(wallSide, targetSide, instant, maxGroups));

        // 바로 옮기기 (전투 준비 중이면 순간, 아니면 달려서) — BattleEntry가 주인공 성벽 묶음에 쓴다
        public void IssueGroupMove(int group, int targetSide, bool instant)
        {
            RunOrQueue(() => MoveGroup(group, targetSide, instant));
        }

        float MessengerSpeed() => HeroSpeed * (rules != null ? rules.MessengerSpeedRatio : 0.7f);

        void UpdateMessengers(float dt)
        {
            UpdateTowerCouriers(dt);
            for (int i = messengers.Count - 1; i >= 0; i--)
            {
                Messenger m = messengers[i];
                if (m.Soldier < 0) continue;   // 탑 명령은 UpdateTowerCouriers가 맡는다

                if (sAlive[m.Soldier] == 0)
                {
                    // 원래 성벽에서 병사를 잃을 때 전령이 뽑혔다 (C16-2: 전령도 원래 성벽 병력)
                    if (!m.Returning) Notice?.Invoke($"{SideNames[m.TargetSide]}으로 가던 전령을 잃었습니다");
                    sAway[m.Soldier] = 0;
                    SetRunning(m.Soldier, 0f);
                    messengers.RemoveAt(i);
                    continue;
                }

                m.Travelled = math.min(m.Travelled + MessengerSpeed() * Boost.MessengerSpeedMul * RunSpeedRatio(m.Soldier) * dt, m.Distance);   // 전령 나팔 (K-058)   // 대포병은 느리게 (K-034)
                float u = m.Returning ? m.EndU - m.Direction * m.Travelled : m.StartU + m.Direction * m.Travelled;
                // 떠날 때는 자기 자리에서 달리는 길로, 돌아올 때는 마지막 몇 m를 자기 자리로 비스듬히 (K-039 의견 6: 순간 이동 없앰)
                sPos[m.Soldier] = WalkIn(RingPosition(u), m.Home, m.Returning ? m.Distance - m.Travelled : m.Travelled);
                SetRunning(m.Soldier, m.Returning ? -m.Direction : m.Direction);
                SoldiersDirty = true;

                if (m.Travelled < m.Distance) continue;
                if (!m.Returning)
                {
                    Deliver(m);
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

        // 전령이 목표 성벽에 닿았을 때 전하는 일
        void Deliver(Messenger m)
        {
            if (m.MoveCannon >= 0) MoveCannon(m.MoveCannon, m.MoveTarget, false);   // 전령이 닿으면 대포가 출발 (K-060)
            else if (m.MoveGroup >= 0) MoveGroup(m.MoveGroup, m.MoveTarget, false);   // 전령이 닿으면 묶음이 출발
            else if (m.MoveGroup == -2) MoveWallGroups(m.TargetSide, m.MoveTarget, false, m.MoveCount);   // 한 번에 옮기기
            else Apply(m.TargetSide, m.Priority, m.IssuedAt, CommandVia.Messenger);
        }

        // 나중에 내린 명령만 적용 (D-039 C16-1·C16-4)
        void Apply(int side, TargetPriority priority, float issuedAt, CommandVia via)
        {
            if (Ended) return;
            if (issuedAt < wallIssuedAt[side])
            {
                Notice?.Invoke($"{SideNames[side]}: 늦게 닿은 {ViaName(via)} 명령은 버렸습니다");
                return;
            }

            wallPriority[side] = priority;
            wallIssuedAt[side] = issuedAt;
            sidePriority[side] = (byte)priority;
            WallCommandApplied?.Invoke(side, priority);
            Notice?.Invoke($"{SideNames[side]} {ViaName(via)}: {PriorityNames[(int)priority]}");
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

        const float RingInset = 1.4f;   // 둘레 길: 성벽 바깥 모서리에서 안쪽으로

        float3 RingPosition(float u)
        {
            float perimeter = CastleHalf * 8f;
            u = Mod(u, perimeter);
            int side = math.min(3, (int)(u / (2f * CastleHalf)));
            float along = u - side * 2f * CastleHalf - CastleHalf;
            // 모서리: 성벽 따라 끝까지 가면 바깥 모서리(탑 자리)에 닿았다가 옆 성벽 시작점으로 2m 순간이동했다 (사용자 지적 2026-10-20).
            // 양쪽 성벽이 같이 쓰는 안쪽 모서리 점(통로 안쪽 RingInset)에서 꺾어 가게 — 주인공처럼 모서리를 돈다.
            // 성벽 한 구간(2H)을 안쪽 모서리 점 사이(2(H−RingInset))로 줄여 이어 붙인다 → 모서리에서 멈칫하지 않는다
            // (자기 자리와의 차이는 WalkIn이 처음·마지막 몇 m에서 메운다)
            along *= (CastleHalf - RingInset) / CastleHalf;
            return WallGeometry.Outward(side) * (CastleHalf - RingInset) + WallGeometry.Right(side) * along + new float3(0f, WallHeight, 0f);
        }

        static float Mod(float a, float m) => a - m * math.floor(a / m);
    }
}
