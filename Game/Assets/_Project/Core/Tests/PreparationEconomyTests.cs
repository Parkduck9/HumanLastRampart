using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Core.Tests
{
    // C-017 정비 경제 순수 계산 (기본값 = D-042 후보 A)
    public sealed class PreparationEconomyTests
    {
        static PreparationEconomyDef Rules() => ScriptableObject.CreateInstance<PreparationEconomyDef>();

        static int Recruit(int owned, int count, UnitKind kind = UnitKind.Archer)
        {
            Assert.IsTrue(PreparationEconomyRules.TryQuoteRecruit(Rules(), kind, owned, count, out int cost, out string reason), reason);
            return cost;
        }

        [Test]
        public void Recruit_UsesPriceBeforeEachSoldier()
        {
            Assert.AreEqual(0, Recruit(0, 0));
            Assert.AreEqual(5, Recruit(0, 1));
            Assert.AreEqual(13, Recruit(599, 2), "599명에서 2명 = 5 + 8");
            Assert.AreEqual(20, Recruit(1199, 2), "1199명에서 2명 = 8 + 12");
            Assert.AreEqual(Recruit(590, 10) + Recruit(600, 20), Recruit(590, 30), "한 번에 사도 나눠 사도 같다");
            Assert.AreEqual(600 * 5 + 600 * 8 + 800 * 12, Recruit(0, 2000), "0→2000 전체");
            Assert.AreEqual(12 + 18, Recruit(1199, 2, UnitKind.Gunner), "총병 단계 가격");
            Assert.AreEqual(15, Recruit(600, 1, UnitKind.CannonCrew));
        }

        [Test]
        public void Recruit_RejectsBadInputAndCapacity()
        {
            PreparationEconomyDef rules = Rules();
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRecruit(rules, UnitKind.Archer, 1999, 2, out _, out _), "전체 정원 2,000 초과");
            Assert.IsTrue(PreparationEconomyRules.TryQuoteRecruit(rules, UnitKind.Archer, 1999, 1, out _, out _), "정확히 2,000은 허용");
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRecruit(rules, UnitKind.Archer, -1, 1, out _, out _));
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRecruit(rules, UnitKind.Archer, 0, -1, out _, out _));
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRecruit(rules, UnitKind.Hero, 0, 1, out _, out _), "주인공은 정비에서 사지 않는다");
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRecruit(rules, UnitKind.Wizard, 0, 1, out _, out _));
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRecruit(null, UnitKind.Archer, 0, 1, out _, out _));
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRecruit(rules, UnitKind.Archer, int.MaxValue, 1, out _, out _), "넘침 없이 실패");
        }

        [Test]
        public void Repair_RoundsUpPerWall()
        {
            PreparationEconomyDef rules = Rules();
            Assert.IsTrue(PreparationEconomyRules.TryQuoteRepair(rules, 9995, 10000, 100, out int hp, out int cost, out _));
            Assert.AreEqual(5, hp); Assert.AreEqual(1, cost);
            Assert.IsTrue(PreparationEconomyRules.TryQuoteRepair(rules, 9989, 10000, 100, out hp, out cost, out _));
            Assert.AreEqual(11, hp); Assert.AreEqual(2, cost);
            Assert.IsTrue(PreparationEconomyRules.TryQuoteRepair(rules, 5000, 10000, 100, out hp, out cost, out _));
            Assert.AreEqual(100, hp); Assert.AreEqual(10, cost);
            Assert.IsTrue(PreparationEconomyRules.TryQuoteRepair(rules, 10000, 10000, 100, out hp, out cost, out _), "만피는 0·0");
            Assert.AreEqual(0, hp); Assert.AreEqual(0, cost);
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRepair(rules, 10001, 10000, 1, out _, out _, out _));
            Assert.IsFalse(PreparationEconomyRules.TryQuoteRepair(rules, 10, 100, -1, out _, out _, out _));
        }

        [Test]
        public void Validate_DefaultsPass()
        {
            CollectionAssert.IsEmpty(PreparationEconomyRules.Validate(Rules()));
            CollectionAssert.IsNotEmpty(PreparationEconomyRules.Validate(null));
        }
    }
}
