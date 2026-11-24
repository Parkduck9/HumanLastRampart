using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.App.Data
{
    // 카탈로그를 합친 정의 찾기 + 시작 전 검사 (C-010 3·4)
    public sealed class DefinitionLookup : IDefinitionLookup
    {
        readonly Dictionary<string, DefinitionBase> byId = new Dictionary<string, DefinitionBase>();
        readonly List<DefinitionBase> all = new List<DefinitionBase>();
        readonly Dictionary<Type, object> byType = new Dictionary<Type, object>();

        public IReadOnlyList<string> Errors => errors;
        public bool IsValid => errors.Count == 0;
        readonly List<string> errors = new List<string>();

        public static DefinitionLookup Build(IEnumerable<DefinitionCatalog> catalogs)
        {
            var lookup = new DefinitionLookup();
            foreach (DefinitionCatalog catalog in catalogs)
            {
                if (catalog == null)
                {
                    lookup.errors.Add("카탈로그 목록에 빈 칸이 있습니다");
                    continue;
                }

                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    lookup.Add(catalog.name, i, catalog.Entries[i]);
                }
            }

            lookup.ValidateReferences();
            return lookup;
        }

        void Add(string catalogName, int index, DefinitionBase entry)
        {
            if (entry == null)
            {
                errors.Add($"{catalogName}[{index}]: 빈 칸(null)");
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                errors.Add($"{catalogName}[{index}] {entry.name}: ID가 비어 있음");
                return;
            }

            if (byId.TryGetValue(entry.Id, out DefinitionBase existing))
            {
                // 덮어쓰지 않는다 (C-010 4)
                errors.Add($"{catalogName}[{index}] {entry.name}: ID '{entry.Id}' 중복 (먼저 등록: {existing.name})");
                return;
            }

            byId.Add(entry.Id, entry);
            all.Add(entry);
        }

        public T Get<T>(string id) where T : DefinitionBase
        {
            if (string.IsNullOrEmpty(id) || !byId.TryGetValue(id, out DefinitionBase found))
            {
                Debug.LogError($"[정의] '{id}'를 찾지 못했습니다 ({typeof(T).Name})");
                return null;
            }

            if (found is T typed)
            {
                return typed;
            }

            Debug.LogError($"[정의] '{id}'는 {typeof(T).Name}이 아니라 {found.GetType().Name}입니다");
            return null;
        }

        public IReadOnlyList<T> All<T>() where T : DefinitionBase
        {
            if (byType.TryGetValue(typeof(T), out object cached))
            {
                return (List<T>)cached;
            }

            var list = new List<T>();
            foreach (DefinitionBase entry in all)
            {
                if (entry is T typed)
                {
                    list.Add(typed);
                }
            }

            byType[typeof(T)] = list;
            return list;
        }

        bool Has<T>(string id) where T : DefinitionBase => !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out DefinitionBase d) && d is T;

        void Require<T>(string owner, string field, string id, bool allowEmpty = false) where T : DefinitionBase
        {
            if (string.IsNullOrEmpty(id))
            {
                if (!allowEmpty) errors.Add($"{owner}: {field} 비어 있음");
                return;
            }

            if (!Has<T>(id))
            {
                errors.Add($"{owner}: {field} '{id}'가 {typeof(T).Name}로 등록되어 있지 않음");
            }
        }

        // 종류가 맞지 않는 참조, 없는 ID, 개수 규칙 (C-010 4, D-027, C-008)
        void ValidateReferences()
        {
            foreach (UnitDef unit in All<UnitDef>())
                Require<WeaponDef>(unit.Id, "기본 무기", unit.DefaultWeaponId, allowEmpty: unit.IsIndividual);

            foreach (WaveDef wave in All<WaveDef>())
                foreach (WaveGroup group in wave.Groups)
                {
                    Require<EnemyDef>(wave.Id, "적", group.EnemyDefId);
                    if (group.Count <= 0) errors.Add($"{wave.Id}: 무리 수가 0 이하");
                }

            foreach (TrapSlotDef slot in All<TrapSlotDef>())
                Require<TerrainDef>(slot.Id, "지형", slot.TerrainId);

            foreach (TrapDef trap in All<TrapDef>())
            {
                if (!trap.ConsumeOnTrigger) errors.Add($"{trap.Id}: 반복 함정은 아직 지원하지 않음 (C-008)");
                // D-050 점화식·구덩이·상점 재고 (C-010 v1.5)
                if (trap.Price < 0 || trap.Damage < 0) errors.Add($"{trap.Id}: 값·피해는 0 이상");
                if (trap.TriggerRadius <= 0f || trap.DamageRadius <= 0f || trap.CraterRadius < 0f) errors.Add($"{trap.Id}: 반경이 잘못됨");
                if (trap.FuseSeconds < 0f) errors.Add($"{trap.Id}: 점화 지연이 음수");
                if (trap.DudChance < 0f || trap.DudChance > 1f || trap.AutoChance < 0f || trap.AutoChance > 1f) errors.Add($"{trap.Id}: 불발·저절로 확률은 0~1");
                if (trap.CraterSpeedMultiplier <= 0f || trap.CraterSpeedMultiplier > 1f) errors.Add($"{trap.Id}: 구덩이 속도 배율은 0 초과 1 이하");
                if (trap.ShopStockStart < 0 || trap.ShopStockPerPrep < 0 || trap.ShopStockMax < trap.ShopStockStart) errors.Add($"{trap.Id}: 상점 재고 값이 잘못됨 (시작·보충 0 이상, 최대 ≥ 시작)");
            }

            // 강화·상점·용병 (K-058, D-079)
            foreach (ItemDef item in All<ItemDef>())
            {
                if (item.Price < 0) errors.Add($"{item.Id}: 값이 음수");
                foreach (UpgradeEffect effect in item.Effects)
                    if (UpgradeEffect.IsUnitStat(effect.Stat)) Require<UnitDef>(item.Id, "효과 병종", effect.UnitDefId);
            }

            foreach (TrainingDef training in All<TrainingDef>())
            {
                Require<UnitDef>(training.Id, "병종", training.UnitDefId);
                if (training.Level < 1 || training.Price < 0) errors.Add($"{training.Id}: 단계는 1 이상, 값은 0 이상");
            }

            foreach (MercenaryDef merc in All<MercenaryDef>())
            {
                Require<UnitDef>(merc.Id, "병종", merc.UnitDefId);
                if (merc.ContractCost < 0 || merc.Wage < 0 || merc.PerWallCap <= 0) errors.Add($"{merc.Id}: 계약금·급료는 0 이상, 성벽 정원은 1 이상");
            }

            foreach (TerrainDef terrain in All<TerrainDef>())
            {
                if (terrain.TrapCellSize <= 0f || terrain.TrapNear < 0f || terrain.TrapFar <= terrain.TrapNear || terrain.TrapFar > terrain.FieldSize - terrain.CastleHalfSize)
                    errors.Add($"{terrain.Id}: 함정 칸 규격이 잘못됨 (칸 > 0, 0 ≤ 가까운 끝 < 먼 끝 ≤ 들판)");
                foreach (Vector3 hill in terrain.Hills)
                    if (hill.y <= 0f) errors.Add($"{terrain.Id}: 언덕 반경이 0 이하");
            }

            // 성벽마다 대포 지점 정확히 4개 (D-027)
            var cannonCount = new int[4];
            foreach (PostDef post in All<PostDef>())
                if (post.Kind == PostKind.Cannon) cannonCount[(int)post.Wall]++;
            if (All<PostDef>().Count > 0)
                for (int i = 0; i < 4; i++)
                    if (cannonCount[i] != 4) errors.Add($"{WallIds.All[i]}: 대포 지점이 {cannonCount[i]}개 (4개여야 함)");

            if (All<BattleRulesDef>().Count != 1) errors.Add($"전투 규칙(BattleRulesDef)이 {All<BattleRulesDef>().Count}개 (정확히 1개)");

            // 정비 가격 (C-017, D-048): 정확히 1개, Id 고정, 값 검사. 없거나 틀리면 임의 기본값으로 숨기지 않고 시작을 막는다
            IReadOnlyList<PreparationEconomyDef> economies = All<PreparationEconomyDef>();
            if (economies.Count != 1)
            {
                errors.Add($"정비 가격(PreparationEconomyDef)이 {economies.Count}개 (정확히 1개)");
            }
            else
            {
                if (economies[0].Id != PreparationEconomyDef.ExpectedId)
                    errors.Add($"정비 가격 Id가 {economies[0].Id} ({PreparationEconomyDef.ExpectedId}이어야 함)");
                errors.AddRange(Core.Economy.PreparationEconomyRules.Validate(economies[0]));
            }

            IReadOnlyList<CampaignStartDef> starts = All<CampaignStartDef>();
            if (starts.Count != 1)
            {
                errors.Add($"시작 설정(CampaignStartDef)이 {starts.Count}개 (정확히 1개)");
                return;
            }

            CampaignStartDef start = starts[0];
            var sides = new HashSet<WallSide>();
            foreach (WallStart wall in start.Walls)
            {
                if (!sides.Add(wall.Side)) errors.Add($"{start.Id}: 성벽 {wall.Side} 중복");
                if (wall.MaxHp <= 0) errors.Add($"{start.Id}: 성벽 {wall.Side} 체력이 0 이하");
            }

            if (sides.Count != 4) errors.Add($"{start.Id}: 성벽이 {sides.Count}개 (정확히 4개)");

            foreach (HeroStart hero in start.Heroes)
            {
                Require<UnitDef>(start.Id, "영웅", hero.UnitDefId);
                Require<PostDef>(start.Id, "영웅 배치", hero.PostId, allowEmpty: true);
            }

            var usedPosts = new HashSet<string>();
            foreach (SquadStart squad in start.Squads)
            {
                Require<UnitDef>(start.Id, "분대", squad.UnitDefId);
                Require<PostDef>(start.Id, "분대 배치", squad.PostId, allowEmpty: true);
                if (squad.Count <= 0) errors.Add($"{start.Id}: 분대 {squad.UnitDefId} 인원이 0 이하");
                // 한 지점에 분대 하나 (C-017 E4)
                if (!string.IsNullOrEmpty(squad.PostId) && !usedPosts.Add(squad.PostId)) errors.Add($"{start.Id}: 지점 {squad.PostId}에 분대가 둘 이상");
            }

            if (start.WaveByBattle.Count == 0) errors.Add($"{start.Id}: 전투 웨이브가 없음");
            foreach (string waveId in start.WaveByBattle) Require<WaveDef>(start.Id, "웨이브", waveId);
            foreach (string terrainId in start.TerrainOrder) Require<TerrainDef>(start.Id, "지형", terrainId);
            // 첫 스토리 ID 형식은 C-009(Codex 제안) 확정 뒤 검사한다
        }
    }
}