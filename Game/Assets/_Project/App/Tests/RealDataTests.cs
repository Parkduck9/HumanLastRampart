using System.Collections.Generic;
using System.Linq;
using HumanLastRampart.App.Data;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;

namespace HumanLastRampart.App.Tests
{
    // 프로젝트의 실제 정의 에셋(P2-C09)을 시작 때와 같은 방식으로 검사한다
    public sealed class RealDataTests
    {
        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            {
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            return DefinitionLookup.Build(catalogs);
        }

        [Test]
        public void RealCatalogs_PassValidation()
        {
            DefinitionLookup lookup = LoadAll();
            Assert.IsTrue(lookup.IsValid, string.Join("\n", lookup.Errors));
            Assert.AreEqual(28, lookup.All<PostDef>().Count, "성벽 4개 × (대포 4 + 분대 3)");
            Assert.AreEqual(4, lookup.All<WaveDef>().Count);
            Assert.AreEqual(2, lookup.All<SpellDef>().Count);
            Assert.AreEqual(1, lookup.All<TrapDef>().Count);
        }

        [Test]
        public void RealWaves_MatchD032()   // 현실 1,000 → 3,000 → 8,000 → 20,000
        {
            DefinitionLookup lookup = LoadAll();
            var expected = new Dictionary<string, int> { { "wave_1", 1000 }, { "wave_2", 3000 }, { "wave_3", 8000 }, { "wave_4", 20000 } };
            foreach (KeyValuePair<string, int> pair in expected)
            {
                WaveDef wave = lookup.Get<WaveDef>(pair.Key);
                Assert.AreEqual(pair.Value, wave.Groups.Sum(g => g.Count), pair.Key);
                Assert.IsTrue(wave.Groups.Select(g => g.Side).Distinct().Count() >= 4 || pair.Key == "wave_1", "4방향에서 온다");
            }
        }

        [Test]
        public void RealStart_CreatesPlayableCampaign()
        {
            DefinitionLookup lookup = LoadAll();
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 7);
            Assert.AreEqual(4, campaign.Wall.Count);
            Assert.AreEqual(2, campaign.Heroes.Count, "주인공 + 마법사");
            Assert.AreEqual(4 * 7, campaign.Squads.Count, "성벽마다 대포 4 + 분대 3");
            Assert.IsTrue(campaign.Squads.All(s => lookup.Get<PostDef>(s.PostId) != null));

            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_real", out string error);
            Assert.IsNull(error);
            Assert.AreEqual("terrain_1", setup.TerrainId);
            Assert.AreEqual(25000, setup.MaxConcurrentEnemies);

            // C-005 v1.3 (D-044 M7): 주인공·마법사는 배치 지점 없이도 참가
            Assert.AreEqual(2, setup.ParticipatingHeroIds.Count, "주인공 + 마법사 참가");
            CollectionAssert.AreEquivalent(campaign.Heroes.Select(h => h.InstanceId), setup.ParticipatingHeroIds);
        }

        [Test]
        public void RealRules_EasyBattle4Multiplier()   // D-047
        {
            DefinitionLookup lookup = LoadAll();
            BattleRulesDef rules = lookup.All<BattleRulesDef>()[0];
            Assert.AreEqual(0.5f, rules.EasyEnemyMultiplierFor(1));
            Assert.AreEqual(0.5f, rules.EasyEnemyMultiplierFor(3));
            Assert.AreEqual(0.4f, rules.EasyEnemyMultiplierFor(4), 0.0001f);
            Assert.AreEqual(0.5f, rules.EasyEnemyMultiplierFor(9), "칸이 없으면 기본값");
        }
    }
}