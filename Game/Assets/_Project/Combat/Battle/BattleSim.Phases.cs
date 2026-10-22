using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 단계가 있는 전투 흐름 (나안, Docs/GDD/Parallel_Wave_20261022.md 6장)
    // ① 선봉: 첫 무리에서 떼어 0~2초, 정면 가까이 · ② 본대: 무리마다 정한 속도로 멀리서 흘러옴
    // ③ 공성: 사다리 고블린·오우거 무리, 나오기 5초 전에 예고 · ④ 최후 돌격: 남은 적 20% 이하면 안 나온 적을 4면에 10초 안에
    // 출현 시각 계산은 BuildEnemies(BattleSim.cs)가, 최후 돌격은 매 프레임 UpdatePhases가 한다
    public sealed partial class BattleSim
    {
        const float VanguardSpawnSeconds = 2f;       // 선봉이 나오는 데 걸리는 시간
        // 선봉 출현 거리 (성벽 앞, 사용자 2026-10-22: 3초 안에 보이게). 성벽 위 화면은 약 45m까지 보이고 고블린은 초당 4m
        public const float VanguardNear = 45f, VanguardFar = 55f;
        const int VanguardMinCount = 10;             // 난이도 배율을 곱한 뒤에도 이만큼은 나온다
        static readonly int[] VanguardByBattle = { 20, 30, 45, 60 };   // 자료에 값이 없을 때 전투 1~4번
        const float DefaultStreamPerSecond = 15f;    // 규칙·무리에 속도가 없을 때
        const float PhaseWarnSeconds = 5f;           // 단계가 바뀌기 이만큼 전에 예고
        const float MainPhaseStart = 20f;            // 본대 단계가 시작되는 시각 (선봉 뒤)
        const float FinalRemainFraction = 0.2f;      // (안 나온 적 + 살아 있는 적)이 전체의 이만큼 이하면 최후 돌격
        const float FinalCompressSeconds = 10f;      // 최후 돌격에서 남은 무리가 모두 나오는 시간
        public const float FinalBannerSeconds = 5f;  // 화면에 '최후 돌격!'을 띄워 두는 시간

        public struct PhaseWarning
        {
            public WavePhase Phase;
            public int Side;
            public float SecondsLeft;
        }

        sealed class SpawnTimeComparer : IComparer<SpawnEntry>
        {
            public static readonly SpawnTimeComparer Instance = new SpawnTimeComparer();
            public int Compare(SpawnEntry a, SpawnEntry b) => a.Time.CompareTo(b.Time);
        }

        readonly List<WavePhase> groupPhase = new List<WavePhase>();    // 무리마다 (선봉을 뗀 뒤 기준: 본대 또는 공성)
        readonly List<float> groupFirstTime = new List<float>();        // 무리의 첫 적이 나오는 시각
        // 무리의 첫 적이 실제로 나온 시각 (동시 출현 상한 때문에 계획보다 늦을 수 있다 — 공성 단계·예고는 이 값을 따른다). 아직이면 MaxValue
        readonly List<float> groupFirstSpawned = new List<float>();
        int vanguardStart = -1;     // 선봉 슬롯 (첫 무리 맨 앞 vanguardSpawned개)
        int vanguardSpawned;
        bool finalStarted;
        float finalStartedAt;

        public int VanguardCount => vanguardSpawned;
        public bool FinalAssaultStarted => finalStarted;
        public float FinalAssaultAt => finalStartedAt;
        public bool IsVanguard(int slot) => vanguardStart >= 0 && slot >= vanguardStart && slot < vanguardStart + vanguardSpawned;
        public WavePhase GroupPhaseOf(int group) => groupPhase[group];
        public float GroupFirstSpawn(int group) => groupFirstTime[group];

        // 지금 단계: 최후 돌격 → 첫 공성 무리가 나온 뒤 공성 → 20초 뒤 본대 → 그 전은 선봉
        public WavePhase CurrentPhase
        {
            get
            {
                if (finalStarted) return WavePhase.Final;
                for (int g = 0; g < groupPhase.Count; g++)
                    if (groupPhase[g] == WavePhase.Siege && groupFirstSpawned[g] <= Elapsed) return WavePhase.Siege;
                return Elapsed >= MainPhaseStart ? WavePhase.Main : WavePhase.Vanguard;
            }
        }

        // 단계가 바뀌기 5초 전 예고 (본대 시작, 공성 무리마다 첫 적). 여럿이면 가장 임박한 것. 없으면 false
        public bool TryGetPhaseWarning(out PhaseWarning warning)
        {
            warning = default;
            if (finalStarted) return false;
            bool found = false;
            float left = MainPhaseStart - Elapsed;
            if (left > 0f && left <= PhaseWarnSeconds)
            {
                warning = new PhaseWarning { Phase = WavePhase.Main, Side = attackPlan.Count > 0 ? attackPlan[0].Side : FrontSide, SecondsLeft = left };
                found = true;
            }

            for (int g = 0; g < groupPhase.Count; g++)
            {
                if (groupPhase[g] != WavePhase.Siege || groupFirstSpawned[g] != float.MaxValue) continue;   // 이미 나온 무리는 예고하지 않는다
                left = groupFirstTime[g] - Elapsed;
                if (left <= 0f || left > PhaseWarnSeconds) continue;
                if (found && warning.SecondsLeft <= left) continue;
                warning = new PhaseWarning { Phase = WavePhase.Siege, Side = attackPlan[g].Side, SecondsLeft = left };
                found = true;
            }

            return found;
        }

        // 선봉 마릿수: 자료 값(없으면 전투 번호로 20/30/45/60) × 난이도 배율, 최소 10
        int ResolveVanguardCount(WaveDef wave)
        {
            if (wave == null) return 0;
            int baseCount = wave.VanguardCount > 0
                ? wave.VanguardCount
                : VanguardByBattle[Mathf.Clamp(setup.BattleNumber - 1, 0, VanguardByBattle.Length - 1)];
            return Mathf.Max(VanguardMinCount, Mathf.RoundToInt(baseCount * setup.EnemyCountMultiplier * LampCountMul(setup.LampBrightness)));
        }

        // 단계가 Auto면 적 종류로: 사다리 고블린·오우거 → 공성, 나머지 → 본대. 선봉·최후는 무리 전체의 단계가 아니라(선봉은 첫 무리에서 떼어 낸다) 본대로 본다
        static WavePhase ResolvePhase(WaveGroup group, EnemyDef def)
        {
            switch (group.Phase)
            {
                case WavePhase.Main: return WavePhase.Main;
                case WavePhase.Siege: return WavePhase.Siege;
                case WavePhase.Vanguard:
                case WavePhase.Final:
                    return WavePhase.Main;
                default:
                    bool siege = def.CarriesLadder || def.Id.IndexOf("ogre", StringComparison.OrdinalIgnoreCase) >= 0;
                    return siege ? WavePhase.Siege : WavePhase.Main;
            }
        }

        // 무리가 흘러나오는 속도(초당): 무리 값 → 전투 규칙 값 → 기본 15
        float ResolveStreamRate(WaveGroup group)
        {
            if (group.StreamPerSecond > 0f) return group.StreamPerSecond;
            if (rules != null && rules.SpawnPerSecond > 0f) return rules.SpawnPerSecond;
            return DefaultStreamPerSecond;
        }

        // BeginFrame에서 Spawn 앞 — 남은 적이 20% 이하가 되면 아직 안 나온 적을 4면에 10초 안에 몰아 낸다 (종류는 그대로)
        void UpdatePhases()
        {
            if (finalStarted || enemyCount <= 0) return;
            int unspawned = spawnOrder.Length - spawnCursor;
            if (unspawned <= 0) return;
            if (enemyCount - Kills > enemyCount * FinalRemainFraction) return;

            finalStarted = true;
            finalStartedAt = Elapsed;
            var random = new Unity.Mathematics.Random(seed ^ 0x9E3779B1u);
            // 이 전투가 이미 공격해 오는 면만 쓴다 (1번 전투는 정면뿐이라 원래 면 그대로)
            var sides = new List<int>();
            foreach (GroupPlan plan in attackPlan) if (!sides.Contains(plan.Side)) sides.Add(plan.Side);
            sides.Sort();
            // 이미 나와 있는 우회 무리는 계속 발견·보고되어야 하므로 표시하지 않는다.
            // 살아서 나와 있는 적이 하나도 없는 무리(남은 적이 전부 아래에서 면이 바뀜)만 발견 끝낸 것으로 본다
            if (groupDiscovered != null)
            {
                for (int g = 0; g < groupDiscovered.Length; g++)
                {
                    if (groupDiscovered[g]) continue;
                    var (start, count) = groupSlots[g];
                    bool aliveOnField = false;
                    for (int e = start; e < start + count && !aliveOnField; e++) aliveOnField = EnemyState.IsAlive(eState[e]);
                    if (!aliveOnField) groupDiscovered[g] = true;
                }
            }

            for (int i = spawnCursor; i < spawnOrder.Length; i++)
            {
                SpawnEntry entry = spawnOrder[i];
                entry.Time = Elapsed + random.NextFloat(0f, FinalCompressSeconds);
                if (sides.Count > 1) entry.Side = sides[random.NextInt(0, sides.Count)];
                spawnOrder[i] = entry;
                eSide[entry.Slot] = (byte)entry.Side;
            }

            Array.Sort(spawnOrder, spawnCursor, unspawned, SpawnTimeComparer.Instance);
            Notice?.Invoke("최후 돌격! 남은 적이 사방에서 몰려옵니다");
        }
    }
}
