using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using Unity.Collections;
using Unity.Mathematics;

namespace HumanLastRampart.Combat.Battle
{
    // 강화·용병 (K-058, D-079, Docs/GDD/Upgrade_Shop.md). 수치는 App이 BattleSetup.Boosts로 넘긴다 — 여기서는 곱하고 더하기만.
    // - 병종 강화(훈련·단체 아이템)는 무기 값에 구워 넣는다 (BuildSoldiers의 MakeWeapon). 용병은 정예 단계라 무기를 따로 만든다
    // - 용병 무리: 분대처럼 서고 싸우지만 성벽 병력(70% 손실 패배)에 들지 않고 옮기지 않는다. 손실은 결과의 MercenaryLosses로
    // - 불화살: 화살이 맞힌 적이 무기 BurnChance 확률로 화상 — 불바다 화상 규칙(초·초당 피해)을 그대로 쓴다
    public sealed partial class BattleSim
    {
        static readonly BattleBoosts NoBoosts = new BattleBoosts();
        const float ArrowBurnSecondsFallback = 10f, ArrowBurnDpsFallback = 2.5f;

        readonly HashSet<string> mercenaryIds = new HashSet<string>();
        NativeQueue<int> igniteQueue;

        BattleBoosts Boost => setup.Boosts ?? NoBoosts;

        // 망원경 반경 (망원경 렌즈)
        public float TelescopeRange => TelescopeRadius * Boost.TelescopeRadiusMul;

        float WallGuardMul(int side) => Boost.WallGuardMul(WallIds.Of((WallSide)side));

        // ── 마법 (보석 지팡이·불씨 부적·모래시계 부적) ──
        float SpellRadiusOf(int index) => spells[index].Radius + (spells[index].Duration > 0f ? 0f : Boost.ExplosionRadiusAdd);
        float SpellCooldownOf(int index) => spells[index].Cooldown * Boost.SpellCooldownMul;
        float SpellDurationOf(int index) => spells[index].Duration > 0f ? spells[index].Duration + Boost.FireDurationAdd : 0f;
        int SpellDamageOf(int index) => spells[index].Duration > 0f ? spells[index].Damage : math.max(1, (int)math.round(spells[index].Damage * Boost.ExplosionDamageMul));
        float BurnChanceOf(SpellDef spell) => spell.BurnChance + Boost.FireBurnChanceAdd;

        // 불화살: 지난 프레임 투사체가 고른 적에게 화상 (작업이 끝난 뒤, BeginFrame)
        void ApplyArrowBurns()
        {
            if (!igniteQueue.IsCreated || igniteQueue.Count == 0) return;
            SpellDef fire = spells.Find(s => s.Duration > 0f);
            float seconds = fire != null && fire.BurnSeconds > 0f ? fire.BurnSeconds : ArrowBurnSecondsFallback;
            float dps = (fire != null && fire.BurnDamage > 0f ? fire.BurnDamage : ArrowBurnDpsFallback) * FireDamageMultiplier;
            while (igniteQueue.TryDequeue(out int e))
            {
                if (e >= 0 && e < enemyCount && EnemyState.IsAlive(eState[e])) Ignite(e, seconds, dps);
            }
        }

        // ── 시험·화면용 ──
        public WeaponKindData WeaponOfSoldier(int soldier) => sWeapon[soldier] >= 0 ? weapons[sWeapon[soldier]] : default;

        public int MercenaryAlive(int side)
        {
            int alive = 0;
            foreach (SquadInfo squad in squads) if (squad.Mercenary && squad.Side == side) alive += squad.Alive;
            return alive;
        }

        // 병종(일반/용병)의 첫 병사 번호, 없으면 -1
        public int FirstSoldierOf(string unitDefId, bool mercenary)
        {
            foreach (SquadInfo squad in squads)
                if (squad.State.UnitDefId == unitDefId && squad.Mercenary == mercenary && squad.Start > 0) return squad.FirstSoldier;
            return -1;
        }
    }
}
