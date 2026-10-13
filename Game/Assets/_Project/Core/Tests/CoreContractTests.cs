using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using NUnit.Framework;

namespace HumanLastRampart.Core.Tests
{
    public sealed class CoreContractTests
    {
        [Test]
        public void NewInstanceId_IsUniqueAndPrefixed()
        {
            var campaign = new CampaignState();
            string a = campaign.NewInstanceId("squad");
            string b = campaign.NewInstanceId("squad");
            Assert.AreNotEqual(a, b);
            StringAssert.StartsWith("squad_", a);
        }

        [Test]
        public void Targeting_GlobalCommandClearsWallCommands()   // D-031 K3
        {
            var targeting = TargetingSettings.CreateDefault();
            Assert.AreEqual(TargetPriority.LowestHpRatio, targeting.PriorityFor(WallIds.North));

            targeting.SetWall(WallIds.North, TargetPriority.ClimbingFirst);
            Assert.AreEqual(TargetPriority.ClimbingFirst, targeting.PriorityFor(WallIds.North));
            Assert.AreEqual(TargetPriority.LowestHpRatio, targeting.PriorityFor(WallIds.South));

            targeting.SetGlobal(TargetPriority.Nearest);
            Assert.AreEqual(TargetPriority.Nearest, targeting.PriorityFor(WallIds.North));
            Assert.AreEqual(0, targeting.WallRules.Count);
        }

        [Test]
        public void WallIds_OppositeAndAdjacent()
        {
            Assert.AreEqual(WallSide.South, WallIds.Opposite(WallSide.North));
            Assert.IsTrue(WallIds.AreAdjacent(WallSide.North, WallSide.East));
            Assert.IsFalse(WallIds.AreAdjacent(WallSide.North, WallSide.South));
            Assert.IsTrue(WallIds.TryParse(WallIds.West, out WallSide west));
            Assert.AreEqual(WallSide.West, west);
        }

        [Test]
        public void Forecast_IsDeterministic()   // D-031 K4: 스토리 힌트와 실제 전투가 같은 날씨
        {
            var rules = TestDefs.Make<BattleRulesDef>("rules");
            for (int battle = 1; battle <= 20; battle++)
            {
                WeatherForecast first = BattleForecast.Get(1234, battle, rules);
                WeatherForecast second = BattleForecast.Get(1234, battle, rules);
                Assert.AreEqual(first, second, $"전투 {battle}");
            }
        }

        [Test]
        public void Forecast_ChangesFollowRules()   // D-027 Q4, D-028 C5
        {
            var rules = TestDefs.Make<BattleRulesDef>("rules");
            int changes = 0;
            const int samples = 4000;
            for (int seed = 0; seed < samples; seed++)
            {
                WeatherForecast f = BattleForecast.Get(seed, 1, rules);
                if (!f.Changes)
                {
                    Assert.AreEqual(f.InitialWeather, f.WeatherAt(9999f));
                    continue;
                }

                changes++;
                Assert.AreNotEqual(f.InitialWeather, f.ChangedWeather, "바뀌면 다른 날씨");
                Assert.AreEqual(f.ChangedWeather == Weather.Rain, f.IsShower, "전투 중 비 = 소나기");
                if (f.Reverts)
                {
                    Assert.Greater(f.RevertAtSeconds, f.ChangeAtSeconds);
                    Assert.AreEqual(f.InitialWeather, f.WeatherAt(f.RevertAtSeconds + 1f), "원래 날씨로만 돌아온다");
                }
            }

            float ratio = (float)changes / samples;
            Assert.That(ratio, Is.InRange(0.12f, 0.18f), "변화 확률 약 15%");
        }
    }
}