using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Tutorial.Tests
{
    // 튜토리얼 단계 기계: 순서, 직접 해야 하는 단계의 판정, 설명 단계의 [다음], 건너뛰기, 못 하는 단계 건너뜀, 시간 배율, 저장
    public sealed class TutorialMachineTests
    {
        sealed class Fake : ITutorialSource
        {
            public bool Alive { get; set; } = true;
            public float Walked { get; set; }
            public bool Map { get; set; }
            public int Squads { get; set; }
            public int Spells { get; set; }
            public int Traps { get; set; }
            public float Beam { get; set; }
            public float BeamTurnDegrees => Beam;
            public bool WizardOk = true;
            public bool TrapOk = true;
            public float Scale = -1f;
            public void Tick(float dt) { }
            public float WalkedMeters => Walked;
            public bool MapOpen => Map;
            public int SquadSends => Squads;
            public int SpellCasts => Spells;
            public int TrapCount => Traps;
            public bool IsAvailable(TutorialStep step) => step != TutorialStep.Magic || WizardOk;
            public bool CanPerform(TutorialStep step) => step != TutorialStep.Trap || TrapOk;
            public bool TryGetTargetScreen(TutorialStep step, out Vector2 screen) { screen = default; return false; }
            public void SetTimeScale(float scale) => Scale = scale;
        }

        MemoryTutorialStore store;
        ITutorialStore previous;
        bool previousSuppressed;

        [SetUp]
        public void SetUp()
        {
            previous = TutorialState.Store;
            previousSuppressed = TutorialState.Suppressed;
            store = new MemoryTutorialStore();
            TutorialState.Store = store;
            TutorialState.Suppressed = false;
        }

        [TearDown]
        public void TearDown()
        {
            TutorialState.Store = previous;
            TutorialState.Suppressed = previousSuppressed;
        }

        static TutorialMachine Battle(Fake f) => new TutorialMachine(f, new TutorialState(), TutorialStep.Walk, TutorialStep.Beam);

        [Test]
        public void BattleRange_RunsInUserOrder_AndDetectsEachAction()
        {
            var f = new Fake();
            TutorialMachine m = Battle(f);
            Assert.AreEqual(TutorialStep.Walk, m.Current);

            f.Walked = TutorialSteps.WalkMeters - 0.1f; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Walk, m.Current, "조금 걸은 것으로는 안 넘어간다");
            f.Walked = TutorialSteps.WalkMeters; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Telescope, m.Current);

            m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Telescope, m.Current, "설명 단계는 상태로 안 넘어간다");
            Assert.IsTrue(m.Next());
            Assert.AreEqual(TutorialStep.Map, m.Current);

            m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Map, m.Current, "지도를 안 열면 그대로");
            f.Map = true; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Squad, m.Current);

            Assert.IsFalse(m.Next(), "직접 해야 하는 단계는 [다음]으로 못 넘긴다");
            m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Squad, m.Current, "보내지 않으면 그대로");
            f.Squads++; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Cannon, m.Current);

            Assert.IsTrue(m.Next());
            Assert.AreEqual(TutorialStep.Magic, m.Current);
            Assert.IsFalse(m.Next());
            f.Spells++; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Beam, m.Current, "마법 다음은 빛줄기");
            Assert.IsFalse(m.Finished);
            f.Beam = TutorialSteps.BeamTurnDegrees; m.Update(0.1f);
            Assert.IsTrue(m.Finished);
            Assert.IsFalse(m.Skipped);
            for (int i = 0; i <= (int)TutorialStep.Beam; i++) Assert.IsTrue(m.State.IsDone((TutorialStep)i));
            Assert.IsTrue(store.TryLoad(out int progress));
            Assert.AreEqual(TutorialState.BattleSteps, progress, "전투 구간을 마치면 정비 구간 번호를 저장");
            Assert.AreEqual(7, progress);
        }

        [Test]
        public void ActionsDoneBeforeTheStep_DoNotCount()
        {
            var f = new Fake { Spells = 3, Squads = 2, Walked = 50f };
            TutorialMachine m = Battle(f);
            f.Walked = 50f + TutorialSteps.WalkMeters; m.Update(0.1f);
            m.Next(); f.Map = true; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Squad, m.Current, "이전에 보낸 횟수는 세지 않는다");
            f.Squads = 3; m.Update(0.1f);
            m.Next();
            Assert.AreEqual(TutorialStep.Magic, m.Current);
            m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Magic, m.Current, "이전에 쓴 마법은 세지 않는다");
        }

        [Test]
        public void Skip_FinishesAtOnce_AndSavesDone()
        {
            var f = new Fake();
            TutorialMachine m = Battle(f);
            m.Skip();
            Assert.IsTrue(m.Finished);
            Assert.IsTrue(m.Skipped);
            Assert.IsTrue(TutorialState.Completed);
            Assert.IsFalse(TutorialState.ShouldStartBattle(1));
            Assert.IsFalse(TutorialState.ShouldStartPrep(2));
            Assert.AreEqual(1f, m.TimeScale, "끝나면 시간이 정상");
            f.Walked = 99f; m.Update(0.1f);
            Assert.IsTrue(m.Finished);
        }

        [Test]
        public void SquadStep_IgnoresEverythingExceptSquadOrders()
        {
            var f = new Fake();
            TutorialMachine m = Battle(f);
            f.Walked = 10f; m.Update(0.1f); m.Next();
            f.Map = true; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Squad, m.Current);
            f.Spells++; f.Traps++; f.Walked += 20f; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Squad, m.Current, "분대 명령 횟수만 센다");
            f.Squads++; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Cannon, m.Current);
        }

        [Test]
        public void UnavailableMagic_IsSkipped_NotBlocking()
        {
            var f = new Fake { WizardOk = false };
            TutorialMachine m = Battle(f);
            bool skipped = false;
            m.StepCompleted += (step, unavailable) => skipped |= step == TutorialStep.Magic && unavailable;
            f.Walked = 10f; m.Update(0.1f); m.Next();
            f.Map = true; m.Update(0.1f);
            f.Squads++; m.Update(0.1f); m.Next();
            m.Update(0.1f);
            Assert.IsTrue(skipped);
            Assert.AreEqual(TutorialStep.Beam, m.Current, "마법을 건너뛰면 빛줄기로");
            f.Beam = TutorialSteps.BeamTurnDegrees; m.Update(0.1f);
            Assert.IsTrue(m.Finished);
        }

        [Test]
        public void BeamStep_CompletesOnlyAfterTurning_AndIgnoresEarlierTurns()
        {
            var f = new Fake { Beam = 180f };   // 단계 전에 돌려 둔 것은 세지 않는다
            TutorialMachine m = Battle(f);
            f.Walked = 10f; m.Update(0.1f); m.Next();
            f.Map = true; m.Update(0.1f);
            f.Squads++; m.Update(0.1f); m.Next();
            f.Spells++; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Beam, m.Current);
            Assert.IsFalse(m.ExplainOnly, "직접 해 보는 단계");
            Assert.IsFalse(m.Next(), "[다음]으로 못 넘긴다");
            f.Walked += 30f; f.Spells++; f.Squads++; f.Map = false; m.Update(0.1f); f.Map = true; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Beam, m.Current, "빛줄기를 안 돌리면 다른 행동으로 안 넘어간다");
            f.Beam += TutorialSteps.BeamTurnDegrees - 1f; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Beam, m.Current, "조금 돌린 것으로는 안 넘어간다");
            f.Beam += 1f; m.Update(0.1f);
            Assert.IsTrue(m.Finished);
        }

        [Test]
        public void OldSave_Migration_MapsOldStepNumbers_WithoutSkippingOrStalling()
        {
            Assert.AreEqual(0, TutorialState.MigrateV1(0));
            Assert.AreEqual(3, TutorialState.MigrateV1(3));
            Assert.AreEqual(5, TutorialState.MigrateV1(5), "마법을 하던 중이면 그대로 마법부터 (끝나면 새 빛줄기 단계)");
            Assert.AreEqual((int)TutorialStep.Trap, TutorialState.MigrateV1(6), "옛 6(함정) -> 새 함정");
            Assert.AreEqual((int)TutorialStep.Lens, TutorialState.MigrateV1(7), "옛 7(렌즈) -> 새 렌즈");
            Assert.AreEqual(TutorialState.Total, TutorialState.MigrateV1(8), "옛 8(끝) -> 새 끝 (튜토리얼이 다시 시작되지 않는다)");
            Assert.AreEqual(TutorialState.Total, TutorialState.MigrateV1(99));
            // 옮긴 번호로 시작 판단이 맞는가
            store.Save(TutorialState.MigrateV1(6));
            Assert.IsFalse(TutorialState.ShouldStartBattle(1), "전투 구간을 마친 옛 저장은 첫 전투를 다시 안 한다");
            Assert.IsTrue(TutorialState.ShouldStartPrep(2));
            Assert.AreEqual(TutorialStep.Trap, TutorialState.ResumeStep(TutorialStep.Trap, TutorialStep.Lens));
            store.Save(TutorialState.MigrateV1(8));
            Assert.IsTrue(TutorialState.Completed);
            Assert.IsFalse(TutorialState.ShouldStartPrep(2));
        }

        [Test]
        public void TimeScale_PausesWhileReading_SlowsWhileActing_AndResumes()
        {
            var f = new Fake();
            TutorialMachine m = Battle(f);
            m.Update(0.1f);
            Assert.AreEqual(0f, f.Scale, "새 단계는 읽는 동안 멈춤");
            m.Update(TutorialSteps.ReadSeconds);
            Assert.AreEqual(TutorialSteps.ActionTimeScale, f.Scale, 1e-4f, "직접 해 보는 동안 천천히");
            f.Walked = 10f; m.Update(0.1f);
            Assert.AreEqual(0f, f.Scale, "설명 단계는 멈춤");
            m.Skip();
            Assert.AreEqual(1f, f.Scale, "끝나면 정상");
        }

        [Test]
        public void PrepRange_TrapThenLens_AndStoresDone()
        {
            var f = new Fake();
            var m = new TutorialMachine(f, new TutorialState(), TutorialStep.Trap, TutorialStep.Lens);
            m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Trap, m.Current);
            f.Traps = 1; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Lens, m.Current);
            Assert.IsFalse(m.Finished);
            Assert.IsTrue(m.Next());
            Assert.IsTrue(m.Finished);
            Assert.IsTrue(TutorialState.Completed);
        }

        [Test]
        public void Start_Conditions_FollowSavedProgress()
        {
            Assert.IsTrue(TutorialState.ShouldStartBattle(1), "처음엔 시작");
            Assert.IsFalse(TutorialState.ShouldStartBattle(2), "첫 전투만");
            Assert.IsFalse(TutorialState.ShouldStartPrep(2), "시작한 적 없으면 정비 튜토리얼 없음");
            store.Save(3);
            Assert.IsTrue(TutorialState.ShouldStartBattle(1), "전투 구간을 못 마쳤으면 다시");
            Assert.IsTrue(TutorialState.ShouldStartPrep(2), "전투를 마치고 정비에 오면 정비 구간으로");
            store.TryLoad(out int progress);
            Assert.AreEqual(3, progress, "확인만 하면 저장 값은 바뀌지 않는다");
            Assert.IsTrue(TutorialState.ShouldStartBattle(1), "확인만으로 전투 구간이 끝난 것이 되지 않는다");
            var m = new TutorialMachine(new Fake(), new TutorialState(), TutorialState.ResumeStep(TutorialStep.Trap, TutorialStep.Lens), TutorialStep.Lens);
            Assert.AreEqual(TutorialStep.Trap, m.Current, "정비 구간은 함정부터");
            store.TryLoad(out progress);
            Assert.AreEqual(TutorialState.BattleSteps, progress, "정비 구간을 시작하면 그 번호를 저장");
            Assert.IsFalse(TutorialState.ShouldStartBattle(1), "전투 구간을 마쳤으면 다시 안 한다");
            TutorialState.Suppressed = true;
            Assert.IsFalse(TutorialState.ShouldStartPrep(2));
            TutorialState.Suppressed = false;
            TutorialState.Reset();
            Assert.IsTrue(TutorialState.ShouldStartBattle(1), "다시 보기");
        }

        [Test]
        public void MapStep_NeedsClosedToOpenChangeSinceStepStarted()
        {
            var f = new Fake { Map = true };   // 단계가 시작될 때 이미 열려 있음
            TutorialMachine m = Battle(f);
            f.Walked = 10f; m.Update(0.1f); m.Next();
            Assert.AreEqual(TutorialStep.Map, m.Current);
            m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Map, m.Current, "이미 열려 있던 지도로는 안 넘어간다");
            f.Map = false; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Map, m.Current, "닫기만 해도 안 넘어간다");
            f.Map = true; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Squad, m.Current, "닫혔다가 열리면 넘어간다");
        }

        [Test]
        public void EnteringEachStep_SavesIt_AndResumeContinuesThere()
        {
            var f = new Fake();
            TutorialMachine m = Battle(f);
            Assert.IsTrue(store.TryLoad(out int progress));
            Assert.AreEqual(0, progress);
            f.Walked = 10f; m.Update(0.1f);
            store.TryLoad(out progress);
            Assert.AreEqual((int)TutorialStep.Telescope, progress, "단계에 들어갈 때마다 저장");
            m.Next();
            store.TryLoad(out progress);
            Assert.AreEqual((int)TutorialStep.Map, progress);
            Assert.AreEqual(TutorialStep.Map, TutorialState.ResumeStep(TutorialStep.Walk, TutorialStep.Magic), "다시 시작하면 하던 단계부터");
            Assert.AreEqual(TutorialStep.Trap, TutorialState.ResumeStep(TutorialStep.Trap, TutorialStep.Lens), "구간 밖 번호는 구간 안으로");
            new TutorialState().SaveStep(TutorialStep.Walk);
            store.TryLoad(out progress);
            Assert.AreEqual((int)TutorialStep.Map, progress, "저장 값을 낮추지 않는다");
        }

        [Test]
        public void TrapStep_WhenNotAffordable_BecomesExplainOnlyWithNext()
        {
            var f = new Fake { TrapOk = false };
            var m = new TutorialMachine(f, new TutorialState(), TutorialStep.Trap, TutorialStep.Lens);
            Assert.IsTrue(m.ExplainOnly, "살 수 없으면 설명만");
            f.Traps = 1; m.Update(0.1f);
            Assert.AreEqual(TutorialStep.Trap, m.Current, "설명 단계는 상태로 안 넘어간다");
            Assert.AreEqual(0f, f.Scale, "설명 단계는 시간 멈춤");
            Assert.IsTrue(m.Next(), "[다음]으로 넘어간다 — 플레이어가 갇히지 않는다");
            Assert.AreEqual(TutorialStep.Lens, m.Current);

            var g = new Fake();
            var n = new TutorialMachine(g, new TutorialState(), TutorialStep.Trap, TutorialStep.Lens);
            Assert.IsFalse(n.ExplainOnly);
            Assert.IsFalse(n.Next());
            Assert.IsFalse(string.IsNullOrEmpty(TutorialSteps.Get(TutorialStep.Trap).ExplainBody));
        }

        [Test]
        public void StepTable_HasAllNineSteps_WithKoreanText()
        {
            for (int i = 0; i < TutorialState.Total; i++)
            {
                TutorialStepInfo info = TutorialSteps.Get((TutorialStep)i);
                Assert.IsFalse(string.IsNullOrEmpty(info.Title));
                Assert.IsFalse(string.IsNullOrEmpty(info.Body));
                Assert.That(info.Expression, Is.InRange(0, 3));
            }

            Assert.IsTrue(TutorialSteps.Get(TutorialStep.Telescope).ExplainOnly);
            Assert.IsTrue(TutorialSteps.Get(TutorialStep.Cannon).ExplainOnly);
            Assert.IsTrue(TutorialSteps.Get(TutorialStep.Lens).ExplainOnly);
            Assert.IsFalse(TutorialSteps.Get(TutorialStep.Squad).ExplainOnly);
            Assert.IsFalse(TutorialSteps.Get(TutorialStep.Magic).ExplainOnly);
            Assert.IsFalse(TutorialSteps.Get(TutorialStep.Beam).ExplainOnly);
            Assert.AreEqual(TutorialState.Total, TutorialSteps.ArtColumn.Length);
            foreach (int column in TutorialSteps.ArtColumn) Assert.That(column, Is.InRange(0, 7), "그림은 8칸");
            Assert.IsFalse(TutorialSteps.Get(TutorialStep.Trap).ExplainOnly);
        }
    }
}
