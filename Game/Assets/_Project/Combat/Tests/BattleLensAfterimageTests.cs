using System.Collections.Generic;
using System.Reflection;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Upgrades;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    public sealed class BattleLensAfterimageTests
    {
        static int Count(LensAfterimage memory, float now)
        {
            int count = 0;
            for (int i = 0; i < LensAfterimage.Capacity; i++)
                if (memory.TryGet(i, now, out _, out _)) count++;
            return count;
        }

        [Test]
        public void Release_RemembersLastPosition_FadesFourSeconds_ThenExpires()
        {
            var memory = new LensAfterimage(2);
            memory.Begin(); memory.Observe(0, Vector3.left, false); memory.End(0f);
            memory.Begin(); memory.Observe(0, Vector3.right, false); memory.End(1f);
            memory.Begin(); memory.End(2f);
            Assert.IsTrue(memory.TryGet(0, 2f, out LensAfterimage.Dot dot, out float alpha));
            Assert.AreEqual(Vector3.right, dot.Position);
            Assert.AreEqual(1f, alpha, 0.001f);
            Assert.IsTrue(memory.TryGet(0, 4f, out _, out alpha));
            Assert.AreEqual(0.5f, alpha, 0.001f);
            Assert.AreEqual(1, Count(memory, 5.99f));
            Assert.AreEqual(0, Count(memory, 7f));
        }

        [Test]
        public void Reobserving_RemovesOldDot_AndNextExitStartsNewLifetime()
        {
            var memory = new LensAfterimage(1);
            memory.Begin(); memory.Observe(0, Vector3.zero, true); memory.End(0f);
            memory.Begin(); memory.End(1f);
            Assert.AreEqual(1, Count(memory, 1f));
            memory.Begin(); memory.Observe(0, Vector3.forward, true); memory.End(2f);
            Assert.AreEqual(0, Count(memory, 2f));
            memory.Begin(); memory.End(3f);
            Assert.IsTrue(memory.TryGet(1, 6f, out LensAfterimage.Dot dot, out float alpha));
            Assert.AreEqual(Vector3.forward, dot.Position);
            Assert.IsTrue(dot.Special);
            Assert.AreEqual(0.25f, alpha, 0.001f);
        }

        [Test]
        public void Capacity512_ReplacesOldestWithoutGrowing()
        {
            var memory = new LensAfterimage(513);
            for (int i = 0; i < 513; i++)
            {
                memory.Begin(); memory.Observe(i, new Vector3(i, 0, 0), false); memory.End(0f);
                memory.Begin(); memory.End(0f);
            }
            Assert.AreEqual(512, Count(memory, 0f));
            Assert.IsTrue(memory.TryGet(0, 0f, out LensAfterimage.Dot dot, out _));
            Assert.AreEqual(512, dot.Enemy);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Sim_OnlyLensItemCreatesMemory_EnemyExitAndLensMoveKeepLastPlace(bool equipped)
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            campaign.Gold = 10000;
            if (equipped) Assert.IsTrue(UpgradeRules.TryBuy(campaign, lookup, "item_telescope_lens", null, out string reason), reason);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "lens_memory_test", out string error);
            Assert.IsNull(error);
            using (var sim = new BattleSim(setup, lookup))
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var positions = (NativeArray<float3>)typeof(BattleSim).GetField("ePos", flags).GetValue(sim);
                var states = (NativeArray<byte>)typeof(BattleSim).GetField("eState", flags).GetValue(sim);
                states[0] = EnemyState.Walking;
                positions[0] = new float3(0, 0, 150);
                sim.UpdateTelescopeAfterimages(new Vector3(0, 0, 150), 0f);
                positions[0] = new float3(0, 0, 300);
                sim.UpdateTelescopeAfterimages(new Vector3(0, 0, 150), 1f);
                Assert.AreEqual(equipped, sim.HasTelescopeLens);
                Assert.AreEqual(40f * (equipped ? 1.4f : 1f), sim.TelescopeRange, 0.001f);
                if (!equipped) { Assert.IsNull(sim.TelescopeAfterimages); return; }
                Assert.AreEqual(1, Count(sim.TelescopeAfterimages, 1f));
                Assert.IsTrue(sim.TelescopeAfterimages.TryGet(0, 1f, out LensAfterimage.Dot dot, out _));
                Assert.AreEqual(150f, dot.Position.z);
                sim.UpdateTelescopeAfterimages(new Vector3(0, 0, 300), 2f);
                Assert.AreEqual(0, Count(sim.TelescopeAfterimages, 2f));
                sim.UpdateTelescopeAfterimages(new Vector3(500, 0, 500), 3f);
                Assert.AreEqual(1, Count(sim.TelescopeAfterimages, 3f));
                Assert.IsTrue(sim.TelescopeAfterimages.TryGet(1, 3f, out dot, out _));
                Assert.AreEqual(300f, dot.Position.z);
                sim.UpdateTelescopeAfterimages(null, 3.1f);
                Assert.AreEqual(0, Count(sim.TelescopeAfterimages, 8f));
            }
        }
    }
}
