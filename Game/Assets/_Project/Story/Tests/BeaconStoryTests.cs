using System.Collections.Generic;
using System.Linq;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Story.Tests
{
    // W12: 확정 이야기 대본(Docs/GDD/Story_Beacon_v1.md)이 게임에 들어갔는지, 흐름이 알맞은 때 요청하는지 확인
    public sealed class BeaconStoryTests
    {
        static StoryScript Load(string id)
        {
            var script = Resources.Load<StoryScript>("Stories/" + id);
            Assert.IsNotNull(script, id + " 에셋이 없습니다");
            Assert.AreEqual(id, script.Id);
            Assert.IsEmpty(script.Validate(), id + " 검사 실패");
            return script;
        }

        static string[] Speakers(StoryScript s) => s.Flatten().Select(x => x.line.Speaker).ToArray();

        [Test]
        public void Intro_IsSection1()
        {
            StoryScript s = Load("story_intro");
            Assert.AreEqual(StoryKind.Normal, s.Kind);
            Assert.AreEqual(10, s.LineCount);
            CollectionAssert.AreEqual(new[] { "", "", "", "하겐", "루멘", "하겐", "루멘", "하겐", "이그나", "루멘" }, Speakers(s));
            Assert.AreEqual("수십 년 전, 빛을 삼키는 검은 안개가 대륙을 덮었다.", s.Flatten()[0].line.Text);
        }

        [Test]
        public void ArrivalRelitCompleteDefeat_HaveScriptLines()
        {
            StoryScript a1 = Load(BeaconRules.ArriveStoryId(1));
            Assert.AreEqual(5, a1.LineCount);
            CollectionAssert.AreEqual(new[] { "", "하겐", "루멘", "이그나", "루멘" }, Speakers(a1));

            StoryScript relit = Load(BeaconRules.RelitStoryId(1));
            Assert.AreEqual(4, relit.LineCount);
            CollectionAssert.AreEqual(new[] { "", "이그나", "하겐", "루멘" }, Speakers(relit));

            StoryScript a2 = Load(BeaconRules.ArriveStoryId(2));
            Assert.AreEqual(4, a2.LineCount);
            CollectionAssert.AreEqual(new[] { "", "하겐", "이그나", "루멘" }, Speakers(a2));

            StoryScript run = Load(BeaconRules.RunCompleteStoryId);
            Assert.AreEqual(StoryKind.RunComplete, run.Kind);
            Assert.AreEqual(5, run.LineCount);
            CollectionAssert.AreEqual(new[] { "", "하겐", "루멘", "이그나", "" }, Speakers(run));

            StoryScript defeat = Load("story_defeat");
            Assert.AreEqual(StoryKind.DefeatEnding, defeat.Kind);
            Assert.AreEqual(3, defeat.LineCount);
            CollectionAssert.AreEqual(new[] { "", "하겐", "루멘" }, Speakers(defeat));
        }

        [Test]
        public void SpeakingLines_HavePortraits_HagenUsesOneSwappableFile()
        {
            string[] ids = { "story_intro", BeaconRules.ArriveStoryId(1), BeaconRules.RelitStoryId(1), BeaconRules.ArriveStoryId(2), BeaconRules.RunCompleteStoryId, "story_defeat" };
            Texture2D hagen = Resources.Load<Texture2D>("StoryArt/portrait_hagen_neutral");   // K-111: 입이 있는 하겐 4표정 가로 시트 (평상·결의·경계·피로 = 칸 0~3). 이야기 줄의 cell로 고른다
            Assert.IsNotNull(hagen);
            Assert.AreEqual(1024, hagen.width); Assert.AreEqual(384, hagen.height);
            Assert.AreEqual(4, StoryCells.Count(hagen));
            foreach (string id in ids)
                foreach ((StoryScene _, StoryLine line) in Load(id).Flatten())
                {
                    if (line.Side == StorySide.None) { Assert.AreEqual("", line.Speaker, id); continue; }
                    Assert.IsNotNull(line.Portrait, $"{id}: {line.Speaker} 초상 없음");
                    if (line.Speaker != "하겐") continue;
                    Assert.AreSame(hagen, line.Portrait, id + ": 하겐 초상은 portrait_hagen_neutral 한 파일");
                    Assert.That(line.Cell, Is.InRange(0, 3), id + ": 하겐 표정 칸은 0~3");
                    if (id == "story_defeat" || id == BeaconRules.ArriveStoryId(2)) Assert.AreEqual(2, line.Cell, id + ": 경계 표정");
                    if (id == BeaconRules.ArriveStoryId(1) || id == BeaconRules.RelitStoryId(1)) Assert.AreEqual(1, line.Cell, id + ": 결의 표정");
                }
        }

        [Test]
        public void StoryChain_IntroToFortress1_RelitToFortress2ThenDay()
        {
            Assert.AreEqual("story_fortress1_arrive", BeaconRules.NextStoryAfter("story_intro"));
            Assert.IsNull(BeaconRules.NextStoryAfter("story_fortress1_arrive"), "요새 1 도착 뒤는 첫 전투");
            Assert.IsFalse(BeaconRules.StoryLeadsToDay("story_fortress1_arrive"));
            Assert.AreEqual("story_fortress2_arrive", BeaconRules.NextStoryAfter("story_fortress1_relit"));
            Assert.IsTrue(BeaconRules.StoryLeadsToDay("story_fortress2_arrive"), "요새 2 도착 뒤는 낮(정비)");
            Assert.IsTrue(BeaconRules.IsRelitStory("story_fortress1_relit"));
            Assert.IsNull(BeaconRules.NextStoryAfter(null));
        }

        [Test]
        public void Router_ChainsStories_AndEndsInBattleOrDay()
        {
            Assert.AreEqual(ScreenId.Story, FlowRouter.Next(ScreenId.Story, ScreenOutcome.Done, new FlowState { StoryChainNext = true }).Screen);
            Assert.AreEqual(ScreenId.Battle, FlowRouter.Next(ScreenId.Story, ScreenOutcome.Done, default).Screen);
            FlowStep day = FlowRouter.Next(ScreenId.Story, ScreenOutcome.Done, new FlowState { StoryToDay = true });
            Assert.AreEqual(ScreenId.Prep, day.Screen);
            Assert.IsTrue(day.BeginDay);
            // 점화 뒤 순서: 결과 → 봉화선 지도 → 점화 이야기 (→ 도착 이야기 → 낮)
            Assert.AreEqual(ScreenId.BeaconMap, FlowRouter.Next(ScreenId.Result, ScreenOutcome.Done, new FlowState { RelitFortress = true }).Screen);
            Assert.AreEqual(ScreenId.Story, FlowRouter.Next(ScreenId.BeaconMap, ScreenOutcome.Done, new FlowState { ResultStory = true }).Screen);
            FlowStep mapDay = FlowRouter.Next(ScreenId.BeaconMap, ScreenOutcome.Done, default);
            Assert.AreEqual(ScreenId.Prep, mapDay.Screen);
            Assert.AreEqual(ScreenId.Prep, FlowRouter.Next(ScreenId.BeaconMap, ScreenOutcome.Done, new FlowState { MapIsPreview = true }).Screen, "정비에서 연 지도는 이야기 없이 정비로");

            // 이어하기: 못 본 이야기가 있으면 정비 대신 이야기부터
            Assert.AreEqual(ScreenId.Story, FlowRouter.Next(ScreenId.MainMenu, ScreenOutcome.ContinueGame, new FlowState { ResumeStory = true }).Screen);
            Assert.AreEqual(ScreenId.Prep, FlowRouter.Next(ScreenId.MainMenu, ScreenOutcome.ContinueGame, default).Screen);
            Assert.IsTrue(BeaconRules.IsPendingStory(new StoryRequest { Kind = StoryKind.Normal, StoryId = "story_fortress1_relit" }));
            Assert.IsTrue(BeaconRules.IsPendingStory(new StoryRequest { Kind = StoryKind.RunComplete, StoryId = "story_run1_complete" }));
            Assert.IsFalse(BeaconRules.IsPendingStory(new StoryRequest { Kind = StoryKind.Normal, StoryId = "story_intro" }), "옛 저장의 첫 이야기 찌꺼기는 무시");
            Assert.IsFalse(BeaconRules.IsPendingStory(null));
        }

        [Test]
        public void IgnitionWin_Fortress1_RequestsRelitStory_Fortress2_RequestsRunComplete()
        {
            var c = new CampaignState { FortressNumber = 1, NightNumber = 2, BeaconRepair = 60f };
            foreach (string wall in WallIds.All) c.Wall.Add(new WallSegmentState { SegmentId = wall, MaxHp = 1000, Hp = 1000 });
            BeaconRules.ApplyWin(c, new BattleSetup { IsIgnitionNight = true }, new BattleResult { BattleRunId = "r1", Outcome = BattleOutcome.Victory });
            Assert.AreEqual(StoryKind.Normal, c.NextStory.Kind);
            Assert.AreEqual("story_fortress1_relit", c.NextStory.StoryId);
            Assert.AreEqual(2, c.FortressNumber);

            c.NightNumber = 3;
            BeaconRules.ApplyWin(c, new BattleSetup { IsIgnitionNight = true }, new BattleResult { BattleRunId = "r2", Outcome = BattleOutcome.Victory });
            Assert.AreEqual(StoryKind.RunComplete, c.NextStory.Kind);
            Assert.AreEqual("story_run1_complete", c.NextStory.StoryId);
        }

        static CampaignState Day(int fortress, int night, float repair, bool wallsOk, int losses = 0, int lastIndex = 0)
        {
            var c = new CampaignState { FortressNumber = fortress, NightNumber = night, BeaconRepair = repair, ConsecutiveNightLosses = losses, LastNightFortress = fortress, LastNightIndex = lastIndex };
            foreach (string wall in WallIds.All) c.Wall.Add(new WallSegmentState { SegmentId = wall, MaxHp = 1000, Hp = wallsOk ? 1000 : 500 });
            return c;
        }

        [Test]
        public void DayLines_PickTheRightOneLiners()
        {
            // 첫 밤을 버틴 아침 (복구가 선 아래라 점화의 밤이 아직 아님)
            List<(string speaker, string text)> l = BeaconRules.DayLines(Day(1, 2, 30f, true, 0, 1));
            Assert.AreEqual(1, l.Count);
            Assert.AreEqual(BeaconRules.LineFirstNightWon, l[0].text);
            Assert.AreEqual("하겐", l[0].speaker);

            // 같은 아침인데 복구가 점화 선이면 "버텼다" + "오늘 밤이 마지막"
            l = BeaconRules.DayLines(Day(1, 2, 50f, true, 0, 1));
            Assert.AreEqual(new[] { BeaconRules.LineFirstNightWon, BeaconRules.LineIgnitionTonight }, l.Select(x => x.text).ToArray());

            // 복구는 찼는데 성벽 부족
            l = BeaconRules.DayLines(Day(1, 2, 50f, false, 0, 1));
            Assert.AreEqual(new[] { BeaconRules.LineFirstNightWon, BeaconRules.LineWallsNotReady }, l.Select(x => x.text).ToArray());

            // 진 밤 다음 아침
            l = BeaconRules.DayLines(Day(1, 1, 0f, true, 1, 0));
            Assert.AreEqual(BeaconRules.LineLostNight, l[0].text);

            // 요새 2의 평범한 낮은 한마디 없음
            Assert.IsEmpty(BeaconRules.DayLines(Day(2, 2, 20f, true, 0, 1)));
            Assert.IsEmpty(BeaconRules.DayLines(null));
        }
    }
}