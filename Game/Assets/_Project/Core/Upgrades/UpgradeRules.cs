using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;

namespace HumanLastRampart.Core.Upgrades
{
    // 강화·상점·용병 규칙 (K-058, D-079, Docs/GDD/Upgrade_Shop.md). 상태가 없는 계산 — 정비 화면(Meta)·App·시험이 같은 규칙을 쓴다.
    // 실패하면 진행 데이터를 바꾸지 않고 이유(한글)를 돌려준다
    public static class UpgradeRules
    {
        // ── 상점 ──────────────────────────────────────────

        public static int OwnedCount(CampaignState c, string itemId)
        {
            int count = 0;
            foreach (OwnedItem item in c.Items) if (item.ItemId == itemId) count += item.Count;
            return count;
        }

        public static bool OwnsForWall(CampaignState c, string itemId, string wallId) =>
            c.Items.Exists(i => i.ItemId == itemId && i.WallId == wallId && i.Count > 0);

        public static bool CanBuy(CampaignState c, IDefinitionLookup defs, string itemId, string wallId, out int cost, out string reason)
        {
            cost = 0;
            reason = null;
            ItemDef item = defs.Get<ItemDef>(itemId);
            if (item == null) { reason = "없는 품목입니다"; return false; }
            cost = item.Price;
            if (item.PerWall)
            {
                if (!WallIds.TryParse(wallId, out _)) { reason = "붙일 성벽을 고르세요"; return false; }
                if (OwnsForWall(c, itemId, wallId)) { reason = "그 성벽에는 이미 있습니다"; return false; }
            }
            else if (item.MaxOwned > 0 && OwnedCount(c, itemId) >= item.MaxOwned)
            {
                reason = item.Consumable ? $"최대 {item.MaxOwned}개까지 쌓아 둘 수 있습니다" : "이미 가지고 있습니다";
                return false;
            }

            if (item.HeroKind.HasValue && HeroFor(c, defs, item.HeroKind.Value) == null) { reason = "쓸 영웅이 없습니다"; return false; }
            if (c.Gold < cost) { reason = $"골드가 {cost - c.Gold:N0} 모자랍니다"; return false; }
            return true;
        }

        // 사면 영웅 아이템은 그 칸이 비어 있을 때 바로 끼운다
        public static bool TryBuy(CampaignState c, IDefinitionLookup defs, string itemId, string wallId, out string reason)
        {
            if (!CanBuy(c, defs, itemId, wallId, out int cost, out reason)) return false;
            ItemDef item = defs.Get<ItemDef>(itemId);
            c.Gold -= cost;
            if (item.PerWall)
            {
                c.Items.Add(new OwnedItem { ItemId = itemId, Count = 1, WallId = wallId });
            }
            else if (item.IsHeroItem)
            {
                var owned = new OwnedItem { ItemId = itemId, Count = 1 };
                c.Items.Add(owned);
                HeroState hero = HeroFor(c, defs, item.HeroKind.Value);
                if (Equipped(c, defs, hero.InstanceId, item.Slot) == null) owned.EquippedBy = hero.InstanceId;
            }
            else
            {
                OwnedItem line = c.Items.Find(i => i.ItemId == itemId && string.IsNullOrEmpty(i.WallId));
                if (line == null) c.Items.Add(new OwnedItem { ItemId = itemId, Count = 1 });
                else line.Count++;
            }

            return true;
        }

        // 그 종류의 살아 있는 영웅 (주인공·마법사는 한 명씩)
        public static HeroState HeroFor(CampaignState c, IDefinitionLookup defs, UnitKind kind)
        {
            foreach (HeroState hero in c.Heroes)
            {
                UnitDef unit = defs.Get<UnitDef>(hero.UnitDefId);
                if (hero.IsAlive && unit != null && unit.Kind == kind) return hero;
            }

            return null;
        }

        public static OwnedItem Equipped(CampaignState c, IDefinitionLookup defs, string heroId, ItemSlot slot)
        {
            if (string.IsNullOrEmpty(heroId)) return null;
            foreach (OwnedItem owned in c.Items)
            {
                if (owned.EquippedBy != heroId) continue;
                ItemDef item = defs.Get<ItemDef>(owned.ItemId);
                if (item != null && item.Slot == slot) return owned;
            }

            return null;
        }

        // 가방의 영웅 아이템을 칸에 끼운다 — 같은 칸에 있던 것은 가방으로
        public static bool TryEquip(CampaignState c, IDefinitionLookup defs, string itemId, out string reason)
        {
            reason = null;
            ItemDef item = defs.Get<ItemDef>(itemId);
            OwnedItem owned = c.Items.Find(i => i.ItemId == itemId);
            if (item == null || owned == null || !item.IsHeroItem) { reason = "끼울 수 있는 아이템이 없습니다"; return false; }
            HeroState hero = HeroFor(c, defs, item.HeroKind.Value);
            if (hero == null) { reason = "쓸 영웅이 없습니다"; return false; }
            if (owned.EquippedBy == hero.InstanceId) return true;
            OwnedItem current = Equipped(c, defs, hero.InstanceId, item.Slot);
            if (current != null) current.EquippedBy = null;
            owned.EquippedBy = hero.InstanceId;
            return true;
        }

        public static bool TryUnequip(CampaignState c, string itemId, out string reason)
        {
            reason = null;
            OwnedItem owned = c.Items.Find(i => i.ItemId == itemId && !string.IsNullOrEmpty(i.EquippedBy));
            if (owned == null) { reason = "끼운 아이템이 아닙니다"; return false; }
            owned.EquippedBy = null;
            return true;
        }

        // ── 병종 훈련 ─────────────────────────────────────

        public static int TrainingLevel(CampaignState c, string unitDefId) => c.Training.Find(t => t.UnitDefId == unitDefId)?.Level ?? 0;

        public static TrainingDef TrainingAt(IDefinitionLookup defs, string unitDefId, int level)
        {
            foreach (TrainingDef def in defs.All<TrainingDef>())
                if (def.UnitDefId == unitDefId && def.Level == level) return def;
            return null;
        }

        public static int MaxTrainingLevel(IDefinitionLookup defs, string unitDefId)
        {
            int max = 0;
            foreach (TrainingDef def in defs.All<TrainingDef>()) if (def.UnitDefId == unitDefId) max = Math.Max(max, def.Level);
            return max;
        }

        public static bool CanTrain(CampaignState c, IDefinitionLookup defs, string unitDefId, out TrainingDef next, out string reason)
        {
            reason = null;
            next = TrainingAt(defs, unitDefId, TrainingLevel(c, unitDefId) + 1);
            if (next == null) { reason = "더 올릴 단계가 없습니다"; return false; }
            if (c.Gold < next.Price) { reason = $"골드가 {next.Price - c.Gold:N0} 모자랍니다"; return false; }
            return true;
        }

        public static bool TryTrain(CampaignState c, IDefinitionLookup defs, string unitDefId, out string reason)
        {
            if (!CanTrain(c, defs, unitDefId, out TrainingDef next, out reason)) return false;
            c.Gold -= next.Price;
            TrainingState state = c.Training.Find(t => t.UnitDefId == unitDefId);
            if (state == null) c.Training.Add(new TrainingState { UnitDefId = unitDefId, Level = next.Level });
            else state.Level = next.Level;
            return true;
        }

        // ── 용병 ──────────────────────────────────────────

        public static int MercenaryCount(CampaignState c, string mercenaryDefId, string wallId)
        {
            int count = 0;
            foreach (MercenaryContract m in c.Mercenaries) if (m.MercenaryDefId == mercenaryDefId && m.WallId == wallId) count += m.Count;
            return count;
        }

        public static bool CanHire(CampaignState c, IDefinitionLookup defs, string mercenaryDefId, string wallId, int count, out int cost, out string reason)
        {
            cost = 0;
            reason = null;
            MercenaryDef def = defs.Get<MercenaryDef>(mercenaryDefId);
            if (def == null) { reason = "없는 용병입니다"; return false; }
            if (!WallIds.TryParse(wallId, out _)) { reason = "설 성벽을 고르세요"; return false; }
            if (count <= 0) { reason = "고용할 수를 고르세요"; return false; }
            int there = MercenaryCount(c, mercenaryDefId, wallId);
            if (there + count > def.PerWallCap) { reason = $"그 성벽 용병은 {def.PerWallCap}명까지입니다 (지금 {there}명)"; return false; }
            cost = def.ContractCost * count;
            if (c.Gold < cost) { reason = $"골드가 {cost - c.Gold:N0} 모자랍니다"; return false; }
            return true;
        }

        public static bool TryHire(CampaignState c, IDefinitionLookup defs, string mercenaryDefId, string wallId, int count, out string reason)
        {
            if (!CanHire(c, defs, mercenaryDefId, wallId, count, out int cost, out reason)) return false;
            c.Gold -= cost;
            MercenaryContract line = c.Mercenaries.Find(m => m.MercenaryDefId == mercenaryDefId && m.WallId == wallId);
            if (line == null) c.Mercenaries.Add(new MercenaryContract { ContractId = c.NewInstanceId("merc"), MercenaryDefId = mercenaryDefId, WallId = wallId, Count = count });
            else line.Count += count;
            return true;
        }

        public static void Dismiss(CampaignState c, string contractId) => c.Mercenaries.RemoveAll(m => m.ContractId == contractId);

        public static int WagesDue(CampaignState c, IDefinitionLookup defs)
        {
            int total = 0;
            foreach (MercenaryContract m in c.Mercenaries)
            {
                MercenaryDef def = defs.Get<MercenaryDef>(m.MercenaryDefId);
                if (def != null) total += def.Wage * m.Count;
            }

            return total;
        }

        // 전투 시작 전 급료 (App이 전투 준비 직전에 부름). 목록 순서대로 한 계약씩 낸다 — 골드가 모자란 계약은 통째로 떠난다.
        // 알림 문장 목록을 돌려준다 (없으면 빈 목록)
        public static List<string> PayWages(CampaignState c, IDefinitionLookup defs)
        {
            var notes = new List<string>();
            var keep = new List<MercenaryContract>();
            foreach (MercenaryContract m in c.Mercenaries)
            {
                MercenaryDef def = defs.Get<MercenaryDef>(m.MercenaryDefId);
                if (def == null || m.Count <= 0) continue;
                int wage = def.Wage * m.Count;
                if (c.Gold >= wage)
                {
                    c.Gold -= wage;
                    keep.Add(m);
                }
                else
                {
                    notes.Add($"급료를 주지 못해 {def.DisplayName} {m.Count}명이 떠났습니다");
                }
            }

            c.Mercenaries = keep;
            return notes;
        }

        // ── 전투 준비: 강화 수치 계산 ─────────────────────

        // 진행 데이터 → 전투에 넘길 수치. consumed = 이번 전투에 쓰는 소모품 ID (결과 반영 때 하나씩 줄인다)
        public static BattleBoosts ComputeBoosts(CampaignState c, IDefinitionLookup defs, List<string> consumed)
        {
            var global = new Accumulator();
            var groupUnit = new List<UpgradeEffect>();   // 병종 대상 단체 아이템 효과 (일반·용병 모두)
            var walls = new Dictionary<string, float>();

            foreach (OwnedItem owned in c.Items)
            {
                ItemDef item = defs.Get<ItemDef>(owned.ItemId);
                if (item == null || owned.Count <= 0) continue;
                if (item.IsHeroItem)
                {
                    HeroState hero = string.IsNullOrEmpty(owned.EquippedBy) ? null : c.FindHero(owned.EquippedBy);
                    if (hero == null || !hero.IsAlive) continue;   // 가방 속·쓰러진 영웅의 아이템은 효과 없음
                }
                else if (item.Consumable)
                {
                    if (consumed != null && !consumed.Contains(item.Id)) consumed.Add(item.Id);
                }

                foreach (UpgradeEffect effect in item.Effects)
                {
                    if (effect.Stat == UpgradeStat.WallGuard)
                    {
                        if (string.IsNullOrEmpty(owned.WallId)) continue;
                        walls[owned.WallId] = (walls.TryGetValue(owned.WallId, out float mul) ? mul : 1f) * (1f + effect.Value);
                    }
                    else if (UpgradeEffect.IsUnitStat(effect.Stat)) groupUnit.Add(effect);
                    else global.Add(effect);
                }
            }

            var boosts = new BattleBoosts
            {
                TelescopeRadiusMul = global.Mul(UpgradeStat.TelescopeRadius),
                MessengerSpeedMul = global.Mul(UpgradeStat.MessengerSpeed),
                HeroWallIntervalMul = global.Mul(UpgradeStat.HeroWallInterval),
                ShoutMisheardMul = global.Mul(UpgradeStat.ShoutMisheard),
                ExplosionDamageMul = global.Mul(UpgradeStat.ExplosionDamage),
                ExplosionRadiusAdd = global.Add(UpgradeStat.ExplosionRadius),
                FireDurationAdd = global.Add(UpgradeStat.FireDuration),
                FireBurnChanceAdd = global.Add(UpgradeStat.FireBurnChance),
                SpellCooldownMul = global.Mul(UpgradeStat.SpellCooldown),
                RecoverLostRatio = Math.Min(1f, global.Add(UpgradeStat.RecoverLost)),
            };

            foreach (var pair in walls) boosts.Walls.Add(new WallBoost { WallId = pair.Key, OnWallDamageMul = pair.Value });

            // 병종: 분대가 있는 병종 + 용병 병종
            var units = new List<string>();
            foreach (SquadState squad in c.Squads) if (!units.Contains(squad.UnitDefId)) units.Add(squad.UnitDefId);
            foreach (string unitId in units) boosts.Units.Add(UnitBoostFor(defs, unitId, TrainingLevel(c, unitId), groupUnit, false));
            foreach (MercenaryContract m in c.Mercenaries)
            {
                MercenaryDef def = defs.Get<MercenaryDef>(m.MercenaryDefId);
                if (def == null || boosts.UnitFor(def.UnitDefId, true) != null) continue;
                int level = Math.Max(TrainingLevel(c, def.UnitDefId), def.EliteLevel);
                boosts.Units.Add(UnitBoostFor(defs, def.UnitDefId, level, groupUnit, true));
            }

            return boosts;
        }

        static UnitBoost UnitBoostFor(IDefinitionLookup defs, string unitId, int level, List<UpgradeEffect> groupUnit, bool mercenary)
        {
            var acc = new Accumulator();
            for (int l = 1; l <= level; l++)
            {
                TrainingDef def = TrainingAt(defs, unitId, l);
                if (def != null) foreach (UpgradeEffect effect in def.Effects) acc.Add(effect);
            }

            foreach (UpgradeEffect effect in groupUnit) if (effect.UnitDefId == unitId) acc.Add(effect);
            return new UnitBoost
            {
                UnitDefId = unitId,
                Mercenary = mercenary,
                Level = level,
                DamageMul = acc.Mul(UpgradeStat.UnitDamage),
                IntervalMul = acc.Mul(UpgradeStat.UnitInterval),
                RangeAdd = acc.Add(UpgradeStat.UnitRange),
                AreaRadiusAdd = acc.Add(UpgradeStat.UnitAreaRadius),
                AccuracyNearAdd = acc.Add(UpgradeStat.UnitAccuracyNear),
                AccuracyFarAdd = acc.Add(UpgradeStat.UnitAccuracyFar),
                BurnChance = Math.Min(1f, acc.Add(UpgradeStat.UnitBurnChance)),
            };
        }

        // 효과 모으기: 배율 종류는 (1+값)을 곱하고, 더하기 종류는 더한다
        sealed class Accumulator
        {
            readonly Dictionary<UpgradeStat, float> values = new Dictionary<UpgradeStat, float>();

            public void Add(UpgradeEffect effect)
            {
                bool additive = UpgradeEffect.IsAdditive(effect.Stat);
                float current = values.TryGetValue(effect.Stat, out float v) ? v : additive ? 0f : 1f;
                values[effect.Stat] = additive ? current + effect.Value : current * Math.Max(0.05f, 1f + effect.Value);
            }

            public float Mul(UpgradeStat stat) => values.TryGetValue(stat, out float v) ? v : 1f;
            public float Add(UpgradeStat stat) => values.TryGetValue(stat, out float v) ? v : 0f;
        }
    }
}
