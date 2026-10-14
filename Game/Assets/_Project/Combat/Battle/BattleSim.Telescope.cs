using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 망원경 (MAP-5, Battle.md 5.4, D-044 M5)
    // - 지도에서 오른쪽 버튼을 누르고 있으면 렌즈(반경 40m). 렌즈 안의 적이 속한 '무리'(웨이브의 같은 방향·같은 출발 묶음)를 확인한다
    // - 확인한 무리: 전투 끝까지 지도에 방향·규모·특수 적 표시, 안개·밤 시야 밖이어도 보이고, 먼 거리 명중 감소가 없다
    public sealed partial class BattleSim
    {
        public const float TelescopeRadius = 40f;

        public struct GroupSummary
        {
            public int Side;
            public Vector3 Center;
            public int Alive;
            public int Special;
        }

        readonly List<(int side, bool special)> waveGroups = new List<(int, bool)>();
        NativeArray<byte> groupSpotted;
        bool[] spottedManaged;
        readonly List<GroupSummary> spottedSummaries = new List<GroupSummary>();

        public IReadOnlyList<GroupSummary> SpottedGroups => spottedSummaries;
        public int SpottedGroupCount { get; private set; }

        void InitTelescope(Allocator p)
        {
            groupSpotted = new NativeArray<byte>(math.max(1, waveGroups.Count), p);
            spottedManaged = new bool[math.max(1, waveGroups.Count)];
        }

        // 렌즈로 본 곳 — 작업이 도는 중이면 다음 프레임 처음에 처리
        public void LookThroughTelescope(Vector3 point)
        {
            var at = new float3(point.x, 0f, point.z);
            RunOrQueue(() => Spot(at));
        }

        void Spot(float3 at)
        {
            if (Ended) return;
            int2 minCell = grid.CellOf(at - new float3(TelescopeRadius, 0f, TelescopeRadius));
            int2 maxCell = grid.CellOf(at + new float3(TelescopeRadius, 0f, TelescopeRadius));
            float radiusSq = TelescopeRadius * TelescopeRadius;
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int cell = grid.IndexOf(new int2(x, z));
                    int start = cellStart[cell];
                    int end = start + cellCount[cell];
                    for (int k = start; k < end; k++)
                    {
                        int e = sorted[k];
                        if (!EnemyState.IsAlive(eState[e])) continue;
                        float3 d = ePos[e] - at;
                        if (d.x * d.x + d.z * d.z > radiusSq) continue;
                        int g = eGroup[e];
                        if (spottedManaged[g]) continue;
                        spottedManaged[g] = true;
                        groupSpotted[g] = 1;
                        SpottedGroupCount++;
                        GroupSummary summary = Summarize(g);
                        Notice?.Invoke($"망원경: {SideNames[summary.Side]} 무리 확인 — 약 {summary.Alive}{(summary.Special > 0 ? $" · 특수 적 {summary.Special}" : "")}");
                    }
                }
            }
        }

        // 확인한 무리들의 지금 위치·규모 (그리기 전, 작업이 끝난 뒤 부른다)
        public void RefreshSpottedGroups()
        {
            spottedSummaries.Clear();
            if (SpottedGroupCount == 0) return;
            for (int g = 0; g < spottedManaged.Length; g++)
            {
                if (!spottedManaged[g]) continue;
                GroupSummary s = Summarize(g);
                if (s.Alive > 0) spottedSummaries.Add(s);
            }
        }

        GroupSummary Summarize(int g)
        {
            var sum = float3.zero;
            int alive = 0, special = 0;
            for (int i = 0; i < enemyCount; i++)
            {
                if (eGroup[i] != g || !EnemyState.IsAlive(eState[i])) continue;
                sum += ePos[i];
                alive++;
                if (kinds[eKind[i]].IsSpecial != 0) special++;
            }

            float3 center = alive > 0 ? sum / alive : float3.zero;
            return new GroupSummary { Side = waveGroups[g].side, Center = new Vector3(center.x, 0f, center.z), Alive = alive, Special = special };
        }
    }
}
