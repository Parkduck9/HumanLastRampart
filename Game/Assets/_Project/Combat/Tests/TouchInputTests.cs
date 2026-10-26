using HumanLastRampart.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.Tests
{
    // W40 터치 조작: 켜짐 판단(PC에서는 꺼짐)과 한 손가락 포인터(눌렀다 떼기·끌기). 편집 모드는 프레임이 멈춰 있어 시험이 프레임을 직접 넘긴다
    public sealed class TouchInputTests : InputTestFixture
    {
        int frame;
        bool force;

        void Next()
        {
            frame++;
            TouchInput.SetTestFrame(frame);
            PointerInput.SetTestFrame(frame);
            _ = PointerInput.IsTouch;   // 실제 게임은 매 프레임 읽는다 — 프레임마다 한 번 상태를 진행시킨다
        }

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            force = TouchInput.ForceOn;
            TouchInput.ForceOn = false;
            TouchInput.Mode = TouchMode.Auto;
            TouchInput.ResetHistory();
            PointerInput.Reset();
            frame = 0;
        }

        [TearDown]
        public override void TearDown()
        {
            TouchInput.ForceOn = force;
            TouchInput.SetTestFrame(-1);
            PointerInput.SetTestFrame(-1);
            PointerInput.Reset();
            TouchInput.ResetHistory();
            base.TearDown();
        }

        [Test]
        public void HiddenOnPcByDefault()
        {
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Touchscreen>();   // 터치 화면이 있어도 아직 터치하지 않았으면 꺼짐
            Next();
            Assert.IsFalse(TouchInput.Active, "PC 기본은 꺼짐");
        }

        [Test]
        public void ShownAfterTouchAndHiddenAgainByMouse()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100f, 100f), screen: screen);
            Next();
            Assert.IsTrue(TouchInput.Active, "터치하면 켜짐");
            EndTouch(1, new Vector2(100f, 100f), screen: screen);
            Next();
            Assert.IsTrue(TouchInput.Active, "마지막 입력이 터치인 동안 유지");
            Move(mouse.position, new Vector2(300f, 300f));
            Next();
            Assert.IsFalse(TouchInput.Active, "마우스를 움직이면 다시 꺼짐");
        }

        [Test]
        public void ForceOnAndModeOverride()
        {
            Next();
            Assert.IsFalse(TouchInput.Active);
            TouchInput.ForceOn = true;
            Next();
            Assert.IsTrue(TouchInput.Active, "강제 켜기");
            TouchInput.ForceOn = false;
            TouchInput.Mode = TouchMode.On;
            Next();
            Assert.IsTrue(TouchInput.Active, "설정 켜기");
            TouchInput.Mode = TouchMode.Off;
            Next();
            Assert.IsFalse(TouchInput.Active, "설정 끄기");
            TouchInput.Mode = TouchMode.Auto;
        }

        [Test]
        public void TapIsClickOnNextFrame()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 at = new Vector2(400f, 300f);
            BeginTouch(1, at, screen: screen);
            Next();
            Assert.IsFalse(PointerInput.PrimaryPressed, "누르는 중에는 아직 클릭이 아니다");
            EndTouch(1, at, screen: screen);
            Next();
            Assert.IsTrue(PointerInput.PrimaryPressed, "뗀 프레임: 눌림");
            Assert.IsTrue(PointerInput.PrimaryHeld);
            Assert.AreEqual(at, PointerInput.Position);
            Assert.IsTrue(PointerInput.IsTouch);
            Next();
            Assert.IsFalse(PointerInput.PrimaryHeld, "다음 프레임: 뗌");
            Assert.AreEqual(at, PointerInput.Position, "뗀 프레임에도 위치 유지");
            Next();
            Assert.IsFalse(PointerInput.IsTouch);
        }

        [Test]
        public void DragStartsAtPressPointAndFollows()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 from = new Vector2(100f, 100f), to = new Vector2(400f, 260f);
            BeginTouch(1, from, screen: screen);
            Next();
            MoveTouch(1, to, screen: screen);
            Next();
            Assert.IsTrue(PointerInput.PrimaryPressed, "움직이면 끌기 시작");
            Assert.AreEqual(from, PointerInput.Position, "누른 자리부터");
            Next();
            Assert.IsTrue(PointerInput.PrimaryHeld);
            Assert.AreEqual(to, PointerInput.Position, "손가락을 따라간다");
            EndTouch(1, to, screen: screen);
            Next();
            Assert.IsFalse(PointerInput.PrimaryHeld, "손가락을 떼면 끝");
            Assert.AreEqual(to, PointerInput.Position);
        }

        [Test]
        public void TwoFingersAreNotAPointer()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100f, 100f), screen: screen);
            BeginTouch(2, new Vector2(300f, 100f), screen: screen);
            Next();
            EndTouch(1, new Vector2(100f, 100f), screen: screen);
            Next();
            Assert.IsFalse(PointerInput.PrimaryPressed, "두 손가락 확대·축소는 클릭이 아니다");
        }
    }
}
