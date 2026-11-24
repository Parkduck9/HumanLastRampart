using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Traps;
using NUnit.Framework;

namespace HumanLastRampart.Core.Tests
{
    public sealed class TrapRulesTests
    {
        static FakeDefinitions Defs(float spotChance) => new FakeDefinitions()
            .Add(TestDefs.Make<TrapDef>("trap_bomb"))
            .Add(TestDefs.Make<BattleRulesDef>("rules", ("recoverySpotChance", spotChance)));

        static TrapPlacement Trap(string id, float x, float z, bool marked) => new TrapPlacement
        {
            TrapId = id,
            TrapDefId = "trap_bomb",
            TerrainId = "terrain_1",
            ActualX = x,
            ActualZ = z,
            Marked = marked,
            MarkX = x,
            MarkZ = z,
        };

        static RecoveryOrder Order(params string[] targets)
        {
            var order = new RecoveryOrder { RecoveryId = "recovery_1", TerrainId = "terrain_1", SquadId = "squad_1" };
            order.TargetTrapIds.AddRange(targets);
            return order;
        }

        [Test]
        public void KnownTarget_IsRecoveredSafely()
        {
            var traps = new List<TrapPlacement> { Trap("trap_a", 20f, 0f, true) };
            RecoveryResult result = TrapRules.ResolveRecovery(Order("trap_a"), traps, null, Defs(0f), 7);
            CollectionAssert.AreEqual(new[] { "trap_a" }, result.RecoveredTrapIds);
            Assert.AreEqual(0, result.ExplodedTrapIds.Count);
            Assert.AreEqual(0, result.SquadLoss);
        }

        [Test]
        public void UnknownTrapOnPath_ExplodesWhenNotSpotted()   // D-018
        {
            var traps = new List<TrapPlacement>
            {
                Trap("trap_a", 20f, 0f, true),
                Trap("trap_b", 10f, 0.5f, false),
            };
            RecoveryResult result = TrapRules.ResolveRecovery(Order("trap_a"), traps, null, Defs(0f), 7);
            CollectionAssert.Contains(result.ExplodedTrapIds, "trap_b");
            Assert.Greater(result.SquadLoss, 0);
        }

        [Test]
        public void UnknownTrapOnPath_IsSpottedWhenNoticed()
        {
            var traps = new List<TrapPlacement>
            {
                Trap("trap_a", 20f, 0f, true),
                Trap("trap_b", 10f, 0.5f, false),
            };
            RecoveryResult result = TrapRules.ResolveRecovery(Order("trap_a"), traps, null, Defs(1f), 7);
            CollectionAssert.Contains(result.NewlySpottedTrapIds, "trap_b");
            Assert.AreEqual(0, result.ExplodedTrapIds.Count);
            CollectionAssert.DoesNotContain(result.RecoveredTrapIds, "trap_b", "고르지 않은 함정은 회수하지 않는다");
        }

        [Test]
        public void SameInput_SameResult()
        {
            var traps = new List<TrapPlacement>
            {
                Trap("trap_a", 20f, 0f, true),
                Trap("trap_b", 10f, 0.5f, false),
            };
            RecoveryResult first = TrapRules.ResolveRecovery(Order("trap_a"), traps, null, Defs(0.5f), 99);
            RecoveryResult second = TrapRules.ResolveRecovery(Order("trap_a"), traps, null, Defs(0.5f), 99);
            CollectionAssert.AreEqual(first.ExplodedTrapIds, second.ExplodedTrapIds);
            CollectionAssert.AreEqual(first.RecoveredTrapIds, second.RecoveredTrapIds);
        }
    }
}