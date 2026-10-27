using HumanLastRampart.Tutorial;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 우두머리 첫 예고 안내 (W48)가 전투 상태를 읽는 곳. 읽기만 하고 바꾸는 것은 시간 배율뿐 (BattleTutorialSource와 같은 길)
    public sealed class BattleBossTip : IBossTipSource
    {
        readonly BattleEntry entry;

        BattleBossTip(BattleEntry entry) { this.entry = entry; }

        // 이 전투에 우두머리가 있고, 튜토리얼이 꺼져 있지 않고, 아직 안 봤을 때만 달아 둔다
        public static bool ShouldBegin(BattleSim sim, bool practice) => sim != null && !practice && sim.BossPresent && TutorialState.ShouldShowBossTip();

        public static BossTipDirector TryBegin(BattleEntry entry, Transform parent, bool practice)
        {
            if (!ShouldBegin(entry.Sim, practice)) return null;
            return BossTipDirector.Begin(parent, new BattleBossTip(entry));
        }

        public bool Alive => entry != null && entry.Sim != null;
        // TryGetBossWarning과 같은 조건이지만 적 배열(작업이 쓰는 중일 수 있다)을 읽지 않는다 — 안내는 LateUpdate의 Complete 앞에서 불린다
        public bool BossWarning => IsWarning(entry.Sim);
        public static bool IsWarning(BattleSim sim) => sim != null && sim.BossPresent && !sim.BossSpawned && BattleSim.BossSpawnSeconds - sim.Elapsed <= BattleSim.BossWarnSeconds;
        public void SetTimeScale(float scale) { if (entry != null) entry.TutorialTimeScale = scale; }
    }
}
