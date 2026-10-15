using System.Collections.Generic;
using HumanLastRampart.Core.Definitions;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 정면 공격·우회하는 적·발견 전령 (D-053, BattleFlank.md)
    // - 정면 = 북쪽(지도 위). 지휘관은 북쪽 가운데에서 시작한다
    // - 정면 비율은 맵마다 50~100% 무작위(1번 전투는 100%). 우회 무리는 0~150초 사이에 옆·뒤 면에 나타난다
    //   옆 면은 2번 전투부터, 뒷면은 3번 전투부터, 4번 전투는 완전 무작위 (1~3번은 배우는 단계)
    // - 우회 무리의 첫 적이 성벽에서 120m(안개·밤이면 시야) 안에 들어오면 그 면 병사가 '발견'
    // - 지휘관이 같은 성벽이면 알림만. 다른 성벽이면 병사 2명이 양쪽으로 한 바퀴 달리며 먼저 만난 쪽이 보고
    //   (성벽마다 한 쌍만 — 달리는 동안 새 발견은 같은 보고에 담는다)
    public sealed partial class BattleSim
    {
        public const int FrontSide = 0;           // 북쪽 (W1)
        const float FlankMinFront = 0.5f;         // 정면 비율 아래 끝 (W2)
        const float FlankLatestSeconds = 150f;    // 우회 무리가 나오는 시각 0~150초 (W2)
        const float DetectDistance = 120f;        // 성벽에서 (W4)
        const float RunnerMeetDistance = 3f;      // 지휘관과 이만큼 가까우면 보고

        public struct GroupPlan
        {
            public int Side;
            public float SpawnAt;
            public bool Flank;
            public int Count;
        }

        sealed class RunnerPair
        {
            public int Side;
            public int[] Soldiers = new int[2];
            public float[] StartU = new float[2];
            public float3[] Home = new float3[2];
            public float Travelled;
            public bool Reported;
            public int Groups;      // 이 보고에 담긴 무리 수
            public int Enemies;
        }

        readonly List<GroupPlan> attackPlan = new List<GroupPlan>();
        readonly List<(int start, int count)> groupSlots = new List<(int, int)>();
        bool[] groupDiscovered;
        readonly RunnerPair[] runners = new RunnerPair[4];
        readonly float[] sideAlertUntil = new float[4];
        int commanderSide = FrontSide;
        float commanderAlong;

        public IReadOnlyList<GroupPlan> AttackPlan => attackPlan;
        public float FrontShare { get; private set; } = 1f;
        public int RunnerPairsActive { get; private set; }
        public int FlankReports { get; private set; }

        // 지도에서 그 면을 깜빡일지 (보고를 받은 뒤 몇 초)
        public bool SideAlert(int side) => Elapsed < sideAlertUntil[side];

        // 지휘관(주인공) 위치 — BattleEntry가 매 프레임 알려 준다
        public void SetCommander(int side, float along)
        {
            commanderSide = side;
            commanderAlong = along;
        }

        // 웨이브 무리마다 나오는 면·시각을 정한다 (전투 실행 ID로 정해 같은 전투를 다시 돌리면 같다)
        void PlanAttack(List<(WaveGroup group, int count)> groups)
        {
            var random = new Unity.Mathematics.Random(seed ^ 0x2545F491u);
            int stage = setup.BattleNumber;
            FrontShare = stage <= 1 ? 1f : random.NextFloat(FlankMinFront, 1f);
            var flankSides = new List<int>();
            if (stage >= 2) { flankSides.Add(1); flankSides.Add(3); }   // 옆 (동·서)
            if (stage >= 3) flankSides.Add(2);                           // 뒤 (남)

            int total = 0;
            foreach (var (_, count) in groups) total += count;
            int flankTarget = Mathf.RoundToInt(total * (1f - FrontShare));

            // 우회할 무리를 무작위 순서로 골라 목표 수까지 (첫 무리는 정면에 남겨 전투가 정면에서 시작되게)
            var order = new List<int>();
            for (int g = 1; g < groups.Count; g++) order.Add(g);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.NextInt(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var flank = new bool[groups.Count];
            int flanked = 0;
            foreach (int g in order)
            {
                if (flankSides.Count == 0 || flanked >= flankTarget) break;
                if (flanked + groups[g].count > flankTarget * 1.5f && flanked > 0) continue;   // 너무 넘치면 건너뜀
                flank[g] = true;
                flanked += groups[g].count;
            }

            for (int g = 0; g < groups.Count; g++)
            {
                attackPlan.Add(flank[g]
                    ? new GroupPlan { Side = flankSides[random.NextInt(0, flankSides.Count)], SpawnAt = random.NextFloat(0f, FlankLatestSeconds), Flank = true, Count = groups[g].count }
                    : new GroupPlan { Side = FrontSide, SpawnAt = groups[g].group.SpawnAtSeconds, Flank = false, Count = groups[g].count });
            }

            groupDiscovered = new bool[groups.Count];
        }

        // BeginFrame에서 (작업이 끝난 뒤) — 발견 판정, 전령 이동
        void UpdateFlank(float dt)
        {
            float detect = Mathf.Min(DetectDistance, VisibleDistance);
            for (int g = 0; g < attackPlan.Count; g++)
            {
                if (!attackPlan[g].Flank || groupDiscovered[g] || Elapsed < attackPlan[g].SpawnAt) continue;
                int side = attackPlan[g].Side;
                float3 n = WallGeometry.Outward(side);
                var (start, count) = groupSlots[g];
                int alive = 0;
                bool seen = false;
                for (int e = start; e < start + count; e++)
                {
                    if (!EnemyState.IsAlive(eState[e])) continue;
                    alive++;
                    if (math.dot(ePos[e], n) - CastleHalf <= detect) seen = true;
                }

                if (seen)
                {
                    groupDiscovered[g] = true;
                    Discover(side, alive);
                }
            }

            UpdateRunners(dt);
        }

        void Discover(int side, int enemies)
        {
            if (commanderSide == side)
            {
                // 같은 성벽: 지휘관도 보고 있다 — 알림만 (W7 가)
                Notice?.Invoke($"{SideNames[side]}: 적이 돌아오고 있습니다! (약 {enemies})");
                sideAlertUntil[side] = Elapsed + 6f;
                FlankReports++;
                return;
            }

            RunnerPair pair = runners[side];
            if (pair != null)
            {
                pair.Groups++;   // 이미 달리는 중 — 같은 보고에 담는다 (W6)
                pair.Enemies += enemies;
                return;
            }

            pair = new RunnerPair { Side = side, Groups = 1, Enemies = enemies };
            float centerAlong = 0f;
            for (int k = 0; k < 2; k++)
            {
                int soldier = NearestFreeSoldier(side, centerAlong);
                if (soldier < 0)
                {
                    if (k == 0)
                    {
                        Notice?.Invoke($"{SideNames[side]}에서 적을 발견했지만 알리러 갈 병사가 없습니다");
                        return;
                    }

                    pair.Soldiers[k] = -1;
                    continue;
                }

                pair.Soldiers[k] = soldier;
                pair.Home[k] = sPos[soldier];
                pair.StartU[k] = RingU(side, LateralOf(soldier, side));
                sAway[soldier] = 1;   // 달리는 동안 쏘지 않는다
            }

            runners[side] = pair;
            RunnerPairsActive++;
            SoldiersDirty = true;
        }

        void UpdateRunners(float dt)
        {
            float perimeter = CastleHalf * 8f;
            float commanderU = RingU(commanderSide, commanderAlong);
            for (int side = 0; side < 4; side++)
            {
                RunnerPair pair = runners[side];
                if (pair == null) continue;
                pair.Travelled = math.min(pair.Travelled + MessengerSpeed() * dt, perimeter);   // W5: 전령과 같은 속도
                for (int k = 0; k < 2; k++)
                {
                    int soldier = pair.Soldiers[k];
                    if (soldier < 0) continue;
                    if (sAlive[soldier] == 0)
                    {
                        sAway[soldier] = 0;
                        SetRunning(soldier, 0f);
                        pair.Soldiers[k] = -1;
                        continue;
                    }

                    float direction = k == 0 ? 1f : -1f;   // 한 명은 시계, 한 명은 반시계
                    float u = pair.StartU[k] + direction * pair.Travelled;
                    sPos[soldier] = RingPosition(u);
                    SetRunning(soldier, direction);
                    if (!pair.Reported && math.abs(Wrap(u - commanderU, perimeter)) <= RunnerMeetDistance)
                    {
                        pair.Reported = true;
                        FlankReports++;
                        sideAlertUntil[side] = Elapsed + 6f;
                        Notice?.Invoke($"전령: {SideNames[side]}에 적이 돌아오고 있습니다! (무리 {pair.Groups} · 약 {pair.Enemies})");
                    }
                }

                SoldiersDirty = true;
                if (pair.Travelled < perimeter) continue;
                // 둘 다 한 바퀴를 마치고 자기 자리로 (보고 여부와 상관없이, W5·D-052)
                for (int k = 0; k < 2; k++)
                {
                    if (pair.Soldiers[k] < 0) continue;
                    sPos[pair.Soldiers[k]] = pair.Home[k];
                    sAway[pair.Soldiers[k]] = 0;
                    SetRunning(pair.Soldiers[k], 0f);
                }

                runners[side] = null;
                RunnerPairsActive--;
            }
        }

        // -perimeter/2 ~ perimeter/2
        static float Wrap(float d, float perimeter)
        {
            d = Mod(d + perimeter * 0.5f, perimeter) - perimeter * 0.5f;
            return d;
        }
    }
}
