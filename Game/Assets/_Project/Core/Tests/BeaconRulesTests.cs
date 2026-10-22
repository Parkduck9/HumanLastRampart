using System.Collections.Generic;
using System.IO;
using System.Linq;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Save;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Core.Tests
{
    // 요새 × 밤 진행 규칙 (W8): 저장 호환, 복구 계산, 점화 조건, 수비대, 지원금, 패배, 요새 전환
    public sealed class BeaconRulesTests
    {
        static CampaignState NewCampaign(int fortress = 1, int night = 1)
        {
            var c = new CampaignState { FortressNumber = fortress, NightNumber = night, Gold = 1000, RngSeed = 1234 };
            foreach (string id in WallIds.All) c.Wall.Add(new WallSegmentState { SegmentId = id, MaxHp = 1000, Hp = 1000 });
            c.Squads.Add(new SquadState { SquadId = "squad_1", UnitDefId = "unit_archer", Count = 100, PostId = "p1" });
            c.Squads.Add(new SquadState { SquadId = "squad_2", UnitDefId = "unit_archer", Count = 50, PostId = "p2" });
            return c;
        }

        static BattleResult Result(string runId = "run_a", float[] lamp = null) =>
            new BattleResult { BattleRunId = runId, Outcome = BattleOutcome.Victory, LampSeconds = lamp ?? new float[3] };

        // ── 저장 호환 ──

        [Test]
        public void NewFields_DefaultToFortress1Night1()
        {
            var c = new CampaignState();
            Assert.AreEqual(1, c.FortressNumber);
            Assert.AreEqual(1, c.NightNumber);
            Assert.AreEqual(0f, c.BeaconRepair);
            Assert.AreEqual(0, c.ConsecutiveNightLosses);
            Assert.AreEqual(LampBrightness.Normal, c.PlannedLamp);
            Assert.IsNotNull(c.Garrisons);
            Assert.AreEqual(0, c.Garrisons.Count);
        }

        [Test]
        public void OldSaveWithoutNewFields_LoadsAsFortress1Night1()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hlr_beacon_save_" + System.Guid.NewGuid().ToString("N"));
            string previous = CampaignSave.Folder;
            CampaignSave.Folder = folder;
            try
            {
                var original = NewCampaign(2, 3);
                original.BeaconRepair = 40f;
                original.ConsecutiveNightLosses = 1;
                original.PlannedLamp = LampBrightness.High;
                original.Garrisons.Add(new GarrisonState { FortressNumber = 1, Count = 12 });
                Assert.IsTrue(CampaignSave.Save(original, ScreenId.Prep, out string error), error);

                // 새 필드가 모두 들어 있는 저장은 그대로 돌아온다
                Assert.IsTrue(CampaignSave.TryLoad(out CampaignState loaded, out _, out error), error);
                Assert.AreEqual(2, loaded.FortressNumber);
                Assert.AreEqual(3, loaded.NightNumber);
                Assert.AreEqual(40f, loaded.BeaconRepair);
                Assert.AreEqual(1, loaded.ConsecutiveNightLosses);
                Assert.AreEqual(LampBrightness.High, loaded.PlannedLamp);
                Assert.AreEqual(12, loaded.Garrisons[0].Count);
                Assert.IsTrue(CampaignSave.TryPeek(out CampaignSave.Summary summary));
                Assert.AreEqual(2, summary.FortressNumber);
                Assert.AreEqual(3, summary.NightNumber);

                // 예전 파일 흉내: 새 필드 이름을 모두 지운다
                string text = File.ReadAllText(CampaignSave.SlotPath);
                foreach (string name in new[] { "FortressNumber", "NightNumber", "BeaconRepair", "ConsecutiveNightLosses", "PlannedLamp", "Garrisons", "LastNightRunId", "LastSupportGold" })
                    text = text.Replace($"\\\"{name}\\\"", $"\\\"Removed_{name}\\\"");
                File.WriteAllText(CampaignSave.SlotPath, text);
                Assert.IsTrue(CampaignSave.TryLoad(out CampaignState old, out _, out error), error);
                Assert.AreEqual(1, old.FortressNumber, "예전 저장 = 요새 1");
                Assert.AreEqual(1, old.NightNumber, "예전 저장 = 밤 1");
                Assert.AreEqual(0f, old.BeaconRepair, "예전 저장 = 복구 0");
                Assert.AreEqual(0, old.ConsecutiveNightLosses);
                Assert.AreEqual(LampBrightness.Normal, old.PlannedLamp);
                Assert.AreEqual(0, old.Garrisons.Count);
                Assert.AreEqual(1000, old.Gold, "다른 값은 그대로");
            }
            finally
            {
                CampaignSave.Folder = previous;
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        // ── 구조·밤 번호 ──

        [Test]
        public void Run1_HasFiveNights_And_SlotsMapToExistingBattles()
        {
            Assert.AreEqual(2, BeaconRules.NightsIn(1));
            Assert.AreEqual(3, BeaconRules.NightsIn(2));
            Assert.AreEqual(5, BeaconRules.TotalNights);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 4, 4 }, Enumerable.Range(1, 6).Select(n => BeaconRules.BattleSlot(n)).ToArray());
            Assert.AreEqual(2, BeaconRules.BattleSlot(5, 2), "웨이브가 2개뿐이면 2번 자리에서 멈춘다");
        }

        [Test]
        public void EnemyTier_ByFortressAndIgnition()
        {
            Assert.AreEqual(0, BeaconRules.EnemyTier(1, false));
            Assert.AreEqual(0, BeaconRules.EnemyTier(1, true));
            Assert.AreEqual(1, BeaconRules.EnemyTier(2, false));
            Assert.AreEqual(2, BeaconRules.EnemyTier(2, true));
        }

        // ── 복구 계산 ──

        [Test]
        public void RepairGain_AllZeroOrMissingLampSeconds_FallsBackToNormal()
        {
            Assert.AreEqual(50f, BeaconRules.RepairGain(new float[3], 1), 0.001f);
            Assert.AreEqual(50f, BeaconRules.RepairGain(null, 1), 0.001f);
            Assert.AreEqual(100f / 3f, BeaconRules.RepairGain(new float[3], 2), 0.001f);
        }

        [Test]
        public void RepairGain_NormalAllNight_ReachesExactly100_AtEndOfFortress()
        {
            // 보통(인덱스 0) 밤 전체
            foreach (int fortress in new[] { 1, 2 })
            {
                float sum = 0f;
                for (int i = 0; i < BeaconRules.NightsIn(fortress); i++)
                    sum += BeaconRules.RepairGain(new float[] { 300f, 0f, 0f }, fortress);
                Assert.AreEqual(100f, sum, 0.01f, $"요새 {fortress}");
            }
        }

        [Test]
        public void RepairGain_WeightsByLampSeconds()
        {
            // 인덱스 = (int)LampBrightness: Normal 0, Low 1, High 2
            Assert.AreEqual(20f * 100f / 70f, BeaconRules.RepairGain(new float[] { 0f, 120f, 0f }, 1), 0.001f, "약만");
            Assert.AreEqual(50f * 100f / 70f, BeaconRules.RepairGain(new float[] { 0f, 0f, 120f }, 1), 0.001f, "강만");
            // 절반 약 + 절반 강 = 35 → 보통과 같다
            Assert.AreEqual(50f, BeaconRules.RepairGain(new float[] { 0f, 60f, 60f }, 1), 0.001f);
            // 음수·모자란 배열은 안전하게
            Assert.AreEqual(50f, BeaconRules.RepairGain(new float[] { -5f, 0f, 0f }, 1), 0.001f);
            Assert.DoesNotThrow(() => BeaconRules.RepairGain(new float[] { 10f }, 1));
        }

        [Test]
        public void ApplyWin_NormalNight_AddsGain_AdvancesNight_ResetsLosses()
        {
            var c = NewCampaign();
            c.ConsecutiveNightLosses = 1;
            BeaconRules.ApplyWin(c, new BattleSetup(), Result());
            Assert.AreEqual(50f, c.BeaconRepair, 0.001f);
            Assert.AreEqual(2, c.NightNumber);
            Assert.AreEqual(1, c.FortressNumber);
            Assert.AreEqual(0, c.ConsecutiveNightLosses);
            Assert.AreEqual(50f, c.LastNightGain, 0.001f);
            Assert.AreEqual(1, c.LastNightFortress);
            Assert.AreEqual(1, c.LastNightIndex);

            BeaconRules.ApplyWin(c, new BattleSetup(), Result());   // 같은 실행 ID → 한 번만
            Assert.AreEqual(50f, c.BeaconRepair, 0.001f);
            Assert.AreEqual(2, c.NightNumber);
        }

        // ── 점화 조건 ──

        [Test]
        public void Ignition_NeedsRepairLineAndAllWalls90Percent()
        {
            var c = NewCampaign();   // 요새 1: 보통으로 한 밤 지난 값 50이 선
            c.BeaconRepair = 49f;
            Assert.IsFalse(BeaconRules.IsIgnitionNight(c), "복구 모자람");
            c.BeaconRepair = 50f;
            Assert.IsTrue(BeaconRules.IsIgnitionNight(c));
            Assert.IsNull(BeaconRules.MissingForIgnition(c));

            c.Wall[2].Hp = 899;
            Assert.IsFalse(BeaconRules.WallsReady(c), "89.9%");
            Assert.IsFalse(BeaconRules.IsIgnitionNight(c), "복구가 차도 성벽이 모자라면 점화의 밤이 아니다");
            StringAssert.Contains("성벽", BeaconRules.MissingForIgnition(c));
            c.Wall[2].Hp = 900;
            Assert.IsTrue(BeaconRules.IsIgnitionNight(c), "정확히 90%");

            c.BeaconRepair = 100f;
            c.Wall[0].Hp = 0;
            Assert.IsFalse(BeaconRules.IsIgnitionNight(c));
            Assert.IsFalse(BeaconRules.WallsReady(new CampaignState()), "성벽 목록이 없으면 준비 안 됨");
        }

        [Test]
        public void Ignition_CanComeOneNightEarly_WithHighLampAndDayRepair()
        {
            var c = NewCampaign(2, 1);
            BeaconRules.ApplyWin(c, new BattleSetup(), Result("n1", new float[] { 0f, 0f, 100f }));   // 강 한 밤: 50 × 100/105
            Assert.AreEqual(100f * 50f / 105f, c.BeaconRepair, 0.01f);
            Assert.IsFalse(BeaconRules.IsIgnitionNight(c), "47.6은 선(66.7) 아래");
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _));
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _));
            Assert.AreEqual(20f + 100f * 50f / 105f, c.BeaconRepair, 0.01f);
            Assert.IsTrue(BeaconRules.IsIgnitionNight(c), "낮 수리로 선을 넘으면 밤 2가 점화의 밤");
            Assert.AreEqual(2, c.NightNumber);
        }

        [Test]
        public void DayRepair_SpendsGold_StopsAt100_AndRefusesWithoutGold()
        {
            var c = NewCampaign();
            c.Gold = 450;
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _));
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _));
            Assert.AreEqual(50, c.Gold);
            Assert.AreEqual(20f, c.BeaconRepair);
            Assert.IsFalse(BeaconRules.TryBuyRepair(c, out string reason));
            StringAssert.Contains("골드", reason);
            Assert.AreEqual(50, c.Gold, "실패하면 골드 그대로");

            c.Gold = 1000;
            c.BeaconRepair = 95f;
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _));
            Assert.AreEqual(100f, c.BeaconRepair);
            Assert.IsFalse(BeaconRules.TryBuyRepair(c, out _), "이미 가득");
        }

        // ── 수비대 ──

        [Test]
        public void Garrison_Takes20PercentRoundedDown_Proportionally()
        {
            var squads = new List<SquadState>
            {
                new SquadState { SquadId = "a", Count = 100 },
                new SquadState { SquadId = "b", Count = 50 },
                new SquadState { SquadId = "c", Count = 10 },
                new SquadState { SquadId = "d", Count = 1 },
            };
            int taken = BeaconRules.TakeGarrison(squads, 20);
            Assert.AreEqual(32, taken, "161명의 20% 내림");
            Assert.AreEqual(161 - 32, squads.Sum(s => s.Count));
            Assert.AreEqual(80, squads[0].Count);
            Assert.AreEqual(40, squads[1].Count);
            Assert.AreEqual(8, squads[2].Count);
            Assert.AreEqual(1, squads[3].Count, "1명 분대는 건드리지 않는다");
        }

        [Test]
        public void Garrison_NeverEmptiesASquad_RemovesZeroSquads()
        {
            var squads = new List<SquadState>
            {
                new SquadState { SquadId = "a", Count = 3 },
                new SquadState { SquadId = "b", Count = 3 },
                new SquadState { SquadId = "c", Count = 3 },
                new SquadState { SquadId = "dead", Count = 0 },
            };
            int taken = BeaconRules.TakeGarrison(squads, 20);   // 9명의 20% = 1
            Assert.AreEqual(1, taken);
            Assert.AreEqual(3, squads.Count, "인원 0 분대는 목록에서 뺀다");
            Assert.AreEqual(8, squads.Sum(s => s.Count));
            Assert.IsTrue(squads.All(s => s.Count >= 2));

            var tiny = new List<SquadState> { new SquadState { SquadId = "x", Count = 4 } };
            Assert.AreEqual(0, BeaconRules.TakeGarrison(tiny, 20), "4명의 20% = 0.8 → 0");
            Assert.AreEqual(4, tiny[0].Count);
            Assert.AreEqual(0, BeaconRules.TakeGarrison(null, 20));
        }

        [Test]
        public void SupportMoney_TwoGoldPerGarrisonSoldier_AcrossFortresses()
        {
            var c = NewCampaign();
            c.Gold = 100;
            Assert.AreEqual(0, BeaconRules.BeginDay(c));
            Assert.AreEqual(100, c.Gold);

            c.Garrisons.Add(new GarrisonState { FortressNumber = 1, Count = 10 });
            c.Garrisons.Add(new GarrisonState { FortressNumber = 2, Count = 5 });
            Assert.AreEqual(30, BeaconRules.BeginDay(c));
            Assert.AreEqual(130, c.Gold);
            Assert.AreEqual(30, c.LastSupportGold);
        }

        // ── 패배 ──

        [Test]
        public void Loss_HalvesRepairRoundingDown_TwoInARowEndsRun_WinResets()
        {
            var c = NewCampaign();
            c.BeaconRepair = 75f;
            Assert.IsFalse(BeaconRules.LossEndsRun(c), "첫 패배는 엔딩이 아니다");
            Assert.IsFalse(BeaconRules.ApplyLoss(c));
            Assert.AreEqual(37f, c.BeaconRepair, "75의 절반 내림");
            Assert.AreEqual(1, c.ConsecutiveNightLosses);
            Assert.AreEqual(1, c.NightNumber, "진 밤은 밤 번호가 오르지 않는다");

            Assert.IsTrue(BeaconRules.LossEndsRun(c), "연속 두 번째 패배는 엔딩");
            Assert.IsTrue(BeaconRules.ApplyLoss(c));
            Assert.AreEqual(18f, c.BeaconRepair);

            // 이기면 연속 패배가 0으로
            var d = NewCampaign();
            BeaconRules.ApplyLoss(d);
            BeaconRules.ApplyWin(d, new BattleSetup(), Result("w1"));
            Assert.AreEqual(0, d.ConsecutiveNightLosses);
            BeaconRules.ApplyLoss(d);
            Assert.IsFalse(BeaconRules.LossEndsRun(new CampaignState { ConsecutiveNightLosses = 0 }));
            Assert.AreEqual(1, d.ConsecutiveNightLosses, "이긴 뒤 다시 지면 처음부터 센다");
        }

        // ── 요새 전환 ──

        [Test]
        public void IgnitionWin_Fortress1_LeavesGarrison_MovesToFortress2()
        {
            var c = NewCampaign(1, 2);
            c.BeaconRepair = 60f;
            c.Wall[1].Hp = 950;
            c.Traps.Add(new TrapPlacement { TrapId = "trap_1", TrapDefId = "trap_bomb", TerrainId = "terrain_1" });
            c.TerrainWorks.Add(new TerrainWork { TerrainId = "terrain_1", CellId = "cell_0_0_0", Kind = TerrainWorkKind.EarthWall });
            c.TrapStock.Add(new TrapStockEntry { TrapDefId = "trap_bomb", Count = 2 });
            c.PlannedLamp = LampBrightness.High;
            int seed = c.RngSeed;

            BeaconRules.ApplyWin(c, new BattleSetup { IsIgnitionNight = true }, Result("ign"));

            Assert.IsTrue(c.LastNightRelit);
            Assert.AreEqual(30, c.LastNightGarrison, "150명의 20%");
            Assert.AreEqual(150 - 30, c.Squads.Sum(s => s.Count));
            Assert.AreEqual(1, c.Garrisons.Count);
            Assert.AreEqual(1, c.Garrisons[0].FortressNumber);
            Assert.AreEqual(30, c.Garrisons[0].Count);

            Assert.AreEqual(2, c.FortressNumber);
            Assert.AreEqual(1, c.NightNumber);
            Assert.AreEqual(0f, c.BeaconRepair);
            Assert.AreEqual(LampBrightness.Normal, c.PlannedLamp);
            Assert.IsTrue(c.Wall.All(w => w.Hp == w.MaxHp), "새 요새는 성벽 가득");
            Assert.AreEqual(0, c.Traps.Count, "설치 함정은 옛 요새에 남는다");
            Assert.AreEqual(0, c.TerrainWorks.Count);
            Assert.AreEqual(1, c.TrapStock.Count, "회수해 둔 함정은 들고 간다");
            Assert.AreNotEqual(seed, c.RngSeed, "새 지형 씨앗");
            Assert.AreEqual(1, c.LastNightFortress);
            Assert.AreEqual(2, c.LastNightIndex);
            Assert.AreEqual(StoryKind.Normal, c.NextStory.Kind, "마지막 요새가 아니므로 1회차 완료가 아니라 점화 이야기 (W12)");
            Assert.AreEqual("story_fortress1_relit", c.NextStory.StoryId);

            // 낮이 열리면 수비대 지원금
            int gold = c.Gold;
            Assert.AreEqual(60, BeaconRules.BeginDay(c));
            Assert.AreEqual(gold + 60, c.Gold);
        }

        [Test]
        public void IgnitionWin_Fortress2_CompletesRun1()
        {
            var c = NewCampaign(2, 3);
            c.Garrisons.Add(new GarrisonState { FortressNumber = 1, Count = 30 });
            BeaconRules.ApplyWin(c, new BattleSetup { IsIgnitionNight = true }, Result("final"));
            Assert.IsTrue(c.LastNightRelit);
            Assert.AreEqual(2, c.FortressNumber, "마지막 요새라 더 넘어가지 않는다");
            Assert.AreEqual(StoryKind.RunComplete, c.NextStory.Kind);
            Assert.AreEqual("story_run1_complete", c.NextStory.StoryId);
            Assert.AreEqual(2, c.Garrisons.Count);
            Assert.AreEqual(30, c.Garrisons.Find(g => g.FortressNumber == 2).Count);
        }

        [Test]
        public void IgnitionWin_WithNoSurvivorsToLeave_AddsNoGarrisonEntry()
        {
            var c = NewCampaign(1, 2);
            c.Squads.Clear();
            BeaconRules.ApplyWin(c, new BattleSetup { IsIgnitionNight = true }, Result("empty"));
            Assert.AreEqual(0, c.LastNightGarrison);
            Assert.AreEqual(0, c.Garrisons.Count);
            Assert.AreEqual(2, c.FortressNumber);
        }
    }
}
