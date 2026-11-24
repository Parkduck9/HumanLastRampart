using NUnit.Framework;

namespace HumanLastRampart.Tutorial.Tests
{
    // 우두머리 첫 예고 안내 (W48): 첫 예고에만 뜸, 두 쪽, 시간 멈춤, 저장하면 다시 안 뜸, 옛 저장 호환, 튜토리얼 끔
    public sealed class BossTipTests
    {
        sealed class Fake : IBossTipSource
        {
            public bool Warning;
            public float Scale = 1f;
            public bool Alive => true;
            public bool BossWarning => Warning;
            public void SetTimeScale(float scale) => Scale = scale;
        }

        ITutorialStore previousTip, previousMain;
        bool previousSuppressed;

        [SetUp]
        public void SetUp()
        {
            previousTip = TutorialState.BossTipStore;
            previousMain = TutorialState.Store;
            previousSuppressed = TutorialState.Suppressed;
            TutorialState.BossTipStore = new MemoryTutorialStore();
            TutorialState.Store = new MemoryTutorialStore();
            TutorialState.Suppressed = false;
        }

        [TearDown]
        public void TearDown()
        {
            TutorialState.BossTipStore = previousTip;
            TutorialState.Store = previousMain;
            TutorialState.Suppressed = previousSuppressed;
        }

        [Test]
        public void ShowsTwoPages_OnFirstWarning_PausesAndSaves()
        {
            var f = new Fake();
            var m = new BossTipMachine(f);
            m.Update();
            Assert.IsFalse(m.Visible, "예고 전에는 안 뜬다");
            Assert.IsFalse(m.Next(), "안 떴으면 [다음]도 소용없다");
            f.Warning = true;
            m.Update();
            Assert.IsTrue(m.Visible);
            Assert.AreEqual(0f, f.Scale, "전투 시간 멈춤");
            Assert.AreEqual(2, m.Info.Expression, "1쪽은 긴급 표정");
            Assert.IsTrue(TutorialState.BossTipSeen, "뜨자마자 저장");
            Assert.IsTrue(m.Next());
            Assert.AreEqual(1, m.Page);
            Assert.AreEqual(1, m.Info.Expression, "2쪽은 설명 표정");
            Assert.AreEqual(0f, f.Scale);
            Assert.IsTrue(m.Next());
            Assert.IsTrue(m.Finished);
            Assert.AreEqual(1f, f.Scale);
            Assert.IsFalse(m.Next());
        }

        [Test]
        public void NotShownAgain_AfterSeen_AndNewMachineStaysQuiet()
        {
            var f = new Fake { Warning = true };
            new BossTipMachine(f).Update();
            Assert.IsTrue(TutorialState.BossTipSeen);
            Assert.IsFalse(TutorialState.ShouldShowBossTip());
            f.Scale = 1f;
            var again = new BossTipMachine(f);
            again.Update();
            Assert.IsFalse(again.Visible, "본 저장이 있으면 다시 안 뜬다");
            Assert.IsTrue(again.Finished);
            Assert.AreEqual(1f, f.Scale, "시간을 멈추지 않는다");
        }

        [Test]
        public void OldSave_WithoutBossTipField_LoadsAsNotSeen_AndKeepsMainProgress()
        {
            TutorialState.Store.Save(5);   // 옛 저장: 본 튜토리얼 진행만 있고 우두머리 칸은 없음
            Assert.IsFalse(TutorialState.BossTipSeen);
            Assert.IsTrue(TutorialState.ShouldShowBossTip());
            TutorialState.MarkBossTipSeen();
            Assert.IsTrue(TutorialState.Store.TryLoad(out int progress) && progress == 5, "우두머리 칸은 본 진행 번호를 건드리지 않는다");
        }

        [Test]
        public void TutorialOff_NeverShows_AndResetClearsSeen()
        {
            TutorialState.Suppressed = true;
            Assert.IsFalse(TutorialState.ShouldShowBossTip());
            TutorialState.Suppressed = false;
            TutorialState.MarkBossTipSeen();
            TutorialState.Reset();
            Assert.IsFalse(TutorialState.BossTipSeen, "튜토리얼 다시 보기는 우두머리 안내도 되살린다");
        }

        [Test]
        public void PageText_IsShortAndHasChips()
        {
            Assert.AreEqual(BossTipMachine.PageCount, TutorialSteps.BossTip.Length);
            Assert.AreEqual(BossTipMachine.PageCount, TutorialSteps.BossTipIcons.Length);
            foreach (TutorialStepInfo page in TutorialSteps.BossTip)
            {
                Assert.IsTrue(page.ExplainOnly);
                Assert.LessOrEqual(page.Body.Split('\n').Length, 2, "본문은 두 줄 이하 (칩과 겹치지 않게)");
                Assert.IsNotEmpty(page.Chips);
            }
        }
    }
}
