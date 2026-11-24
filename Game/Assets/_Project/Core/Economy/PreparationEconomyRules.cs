using System.Collections.Generic;
using HumanLastRampart.Core.Definitions;

namespace HumanLastRampart.Core.Economy
{
    // 정비 모집·수리 순수 계산 (C-017). 상태가 없고 Campaign을 바꾸지 않으며 ID를 만들지 않는다.
    // 골드 부족·지점/성벽 정원·병종 일치·거래 반영은 Meta가 검사한다. 여기서는 가격과 전체 정원만.
    public static class PreparationEconomyRules
    {
        // 지금 일반 병력 ownedSoldierCount명에서 kind를 requestedCount명 더 살 때의 비용.
        // 1명마다 '그 1명을 사기 직전 전체 인원'의 단계 가격 (예: 궁수 599명에서 2명 = 5 + 8 = 13)
        public static bool TryQuoteRecruit(
            PreparationEconomyDef rules, UnitKind kind,
            int ownedSoldierCount, int requestedCount,
            out int goldCost, out string reason)
        {
            goldCost = 0;
            reason = null;
            if (rules == null) { reason = "정비 가격 정의가 없습니다"; return false; }
            if (ownedSoldierCount < 0 || requestedCount < 0) { reason = "인원이 음수입니다"; return false; }

            IReadOnlyList<int> prices = rules.PricesFor(kind);
            if (prices == null) { reason = $"정비에서 모집할 수 없는 병종입니다: {kind}"; return false; }
            IReadOnlyList<int> tiers = rules.TierStartCounts;
            if (tiers == null || tiers.Count == 0 || prices.Count != tiers.Count) { reason = "정비 가격 정의가 잘못됐습니다"; return false; }
            if (requestedCount == 0) return true;

            long after = (long)ownedSoldierCount + requestedCount;
            if (after > rules.GlobalCapacity) { reason = $"전체 정원({rules.GlobalCapacity}명)을 넘습니다"; return false; }

            long total = 0;
            long from = ownedSoldierCount;   // 이번에 살 첫 사람 직전 인원
            for (int t = 0; t < tiers.Count && from < after; t++)
            {
                long tierEnd = t + 1 < tiers.Count ? tiers[t + 1] : long.MaxValue;
                if (from >= tierEnd) continue;
                long count = System.Math.Min(after, tierEnd) - from;
                total += count * prices[t];
                from += count;
            }

            if (total > int.MaxValue) { reason = "비용이 너무 큽니다"; return false; }
            goldCost = (int)total;
            return true;
        }

        // 벽 하나 수리. 실제 복구 = min(요청, 최대 − 현재), 비용 = 실제 복구 ÷ RepairHpPerGold 올림 (벽마다 따로 올림)
        public static bool TryQuoteRepair(
            PreparationEconomyDef rules, int currentHp, int maxHp,
            int requestedHp, out int actualRepairHp,
            out int goldCost, out string reason)
        {
            actualRepairHp = 0;
            goldCost = 0;
            reason = null;
            if (rules == null) { reason = "정비 가격 정의가 없습니다"; return false; }
            if (rules.RepairHpPerGold <= 0) { reason = "정비 가격 정의가 잘못됐습니다"; return false; }
            if (currentHp < 0 || maxHp <= 0 || requestedHp < 0) { reason = "체력 값이 잘못됐습니다"; return false; }
            if (currentHp > maxHp) { reason = "현재 체력이 최대보다 큽니다"; return false; }

            actualRepairHp = System.Math.Min(requestedHp, maxHp - currentHp);
            goldCost = (int)(((long)actualRepairHp + rules.RepairHpPerGold - 1) / rules.RepairHpPerGold);
            return true;
        }

        // 정의 검사 (App 시작 검사에서 부른다). 문제가 없으면 빈 목록
        public static List<string> Validate(PreparationEconomyDef rules)
        {
            var errors = new List<string>();
            if (rules == null)
            {
                errors.Add("정비 가격 정의가 없습니다");
                return errors;
            }

            string id = rules.Id;
            IReadOnlyList<int> tiers = rules.TierStartCounts;
            if (tiers == null || tiers.Count == 0)
            {
                errors.Add($"{id}: 단계 경계가 없습니다");
            }
            else
            {
                if (tiers[0] != 0) errors.Add($"{id}: 첫 단계 경계는 0이어야 합니다");
                for (int i = 0; i < tiers.Count; i++)
                {
                    if (i > 0 && tiers[i] <= tiers[i - 1]) errors.Add($"{id}: 단계 경계가 증가하지 않습니다 ({tiers[i - 1]} → {tiers[i]})");
                    if (tiers[i] >= rules.GlobalCapacity) errors.Add($"{id}: 단계 경계 {tiers[i]}가 전체 정원 이상입니다");
                }
            }

            CheckPrices(errors, id, "궁수", rules.ArcherPrices, tiers);
            CheckPrices(errors, id, "총병", rules.GunnerPrices, tiers);
            CheckPrices(errors, id, "대포 운용병", rules.CannonCrewPrices, tiers);

            if (rules.GlobalCapacity <= 0) errors.Add($"{id}: 전체 정원이 0 이하입니다");
            if (rules.WallCapacity <= 0 || rules.WallCapacity > rules.GlobalCapacity) errors.Add($"{id}: 성벽 정원은 1 이상, 전체 정원 이하여야 합니다");
            if (rules.SquadPostCapacity <= 0 || rules.SquadPostCapacity > rules.WallCapacity) errors.Add($"{id}: 분대 지점 정원은 1 이상, 성벽 정원 이하여야 합니다");
            if (rules.CannonCrewPostCapacity <= 0 || rules.CannonCrewPostCapacity > rules.WallCapacity) errors.Add($"{id}: 대포 지점 정원은 1 이상, 성벽 정원 이하여야 합니다");
            if (rules.RepairHpPerGold <= 0) errors.Add($"{id}: 수리 HP당 골드 값이 0 이하입니다");
            if (rules.DefaultRepairHp <= 0) errors.Add($"{id}: 기본 수리량이 0 이하입니다");
            return errors;
        }

        static void CheckPrices(List<string> errors, string id, string name, IReadOnlyList<int> prices, IReadOnlyList<int> tiers)
        {
            if (prices == null || tiers == null || prices.Count != tiers.Count)
            {
                errors.Add($"{id}: {name} 가격 수가 단계 수와 다릅니다");
                return;
            }

            foreach (int price in prices)
            {
                if (price <= 0) errors.Add($"{id}: {name} 가격에 0 이하가 있습니다");
            }
        }
    }
}
