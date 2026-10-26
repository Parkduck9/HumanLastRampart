using HumanLastRampart.Meta;
using HumanLastRampart.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace HumanLastRampart.Combat.Tests
{
    // W40 터치 조작: 켜짐 판단(PC에서는 꺼짐)과 한 손가락 포인터(눌렀다 떼기·끌기·길게 누르기·두 손가락), 터치 직후 마우스 무시, 함정 배치 길게 눌러 돌리기.
    // 편집 모드는 프레임·시각이 멈춰 있어 시험이 직접 넘긴다. 저장된 설정(PlayerPrefs)은 건드리지 않는다
    public sealed class TouchInputTests : InputTestFixture
    {
        int frame;
        float clock;
        bool force;

        void Next(float seconds = 0.05f)
        {
            frame++;
            clock += seconds;
            TouchInput.SetTestFrame(frame);
            PointerInput.SetTestFrame(frame);
            PointerInput.SetTestTime(clock);
            _ = TouchInput.Active;      // 매 프레임 켜짐 판단이 돌아야 마지막 입력이 쌓인다
            _ = PointerInput.IsTouch;   // 실제 게임은 매 프레임 읽는다 — 프레임마다 한 번 상태를 진행시킨다
        }

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            force = TouchInput.ForceOn;
            TouchInput.ForceOn = false;
            TouchInput.SetModeInMemory(TouchMode.Auto);   // 개발자가 저장해 둔 켜기·끄기를 덮어쓰지 않는다
            TouchInput.ResetHistory();
            PointerInput.Reset();
            frame = 0;
            clock = 10f;
        }

        [TearDown]
        public override void TearDown()
        {
            TouchInput.ForceOn = force;
            TouchInput.SetTestFrame(-1);
            PointerInput.SetTestFrame(-1);
            PointerInput.SetTestTime(-1f);
            PointerInput.Reset();
            TouchInput.ResetHistory();
            TouchInput.ReloadMode();
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
            Next(1f);
            Move(mouse.position, new Vector2(300f, 300f));
            Next();
            Assert.IsFalse(TouchInput.Active, "한참 뒤 마우스를 움직이면 다시 꺼짐");
        }

        [Test]
        public void MouseRightAfterTouchIsIgnored()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100f, 100f), screen: screen);
            Next();
            EndTouch(1, new Vector2(100f, 100f), screen: screen);
            Next();
            Move(mouse.position, new Vector2(100f, 100f));   // 터치 노트북: 운영체제가 터치를 마우스로도 알려 준다
            Press(mouse.leftButton);
            Next();
            Assert.IsTrue(TouchInput.Active, "터치 직후 마우스는 입력으로 치지 않는다");
            Assert.IsTrue(PointerInput.MouseSuppressed);
            Assert.IsFalse(PointerInput.PrimaryHeld, "마우스 눌림이 따로 한 번 더 눌림이 되지 않는다");
            Next(0.6f);
            Assert.IsFalse(PointerInput.MouseSuppressed);
            Assert.IsTrue(PointerInput.PrimaryHeld, "시간이 지나면 마우스는 다시 마우스");
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
            TouchInput.SetModeInMemory(TouchMode.On);
            Next();
            Assert.IsTrue(TouchInput.Active, "설정 켜기");
            TouchInput.SetModeInMemory(TouchMode.Off);
            Next();
            Assert.IsFalse(TouchInput.Active, "설정 끄기");
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
            Assert.IsFalse(PointerInput.PressIsDrag, "짧은 누름은 끌기가 아니다 (마법·대포·점화는 이 눌림에만)");
            Assert.IsTrue(PointerInput.PrimaryHeld);
            Assert.AreEqual(at, PointerInput.Position);
            Assert.IsTrue(PointerInput.IsTouch);
            Next();
            Assert.IsFalse(PointerInput.PrimaryHeld, "다음 프레임: 뗌");
            Assert.AreEqual(at, PointerInput.Position, "뗀 프레임에도 위치 유지");
            Next();
            Assert.IsFalse(PointerInput.IsTouch);
            Assert.AreEqual(at, PointerInput.Position, "손가락이 없으면 마지막 터치 자리 ((0,0)으로 튀지 않는다)");
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
            Assert.IsTrue(PointerInput.PressIsDrag, "끌기의 시작 눌림");
            Assert.AreEqual(from, PointerInput.Position, "누른 자리부터");
            Next();
            Assert.IsTrue(PointerInput.PrimaryHeld);
            Assert.IsFalse(PointerInput.PressIsDrag);
            Assert.AreEqual(to, PointerInput.Position, "손가락을 따라간다");
            EndTouch(1, to, screen: screen);
            Next();
            Assert.IsFalse(PointerInput.PrimaryHeld, "손가락을 떼면 끝");
            Assert.IsFalse(PointerInput.PrimaryCanceled);
            Assert.AreEqual(to, PointerInput.Position);
        }

        [Test]
        public void SecondFingerCancelsDragInsteadOfReleasing()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100f, 100f), screen: screen);
            Next();
            MoveTouch(1, new Vector2(400f, 100f), screen: screen);
            Next();
            Next();
            Assert.IsTrue(PointerInput.PrimaryHeld, "끌기 중");
            BeginTouch(2, new Vector2(600f, 300f), screen: screen);
            Next();
            Assert.IsTrue(PointerInput.PrimaryCanceled, "두 번째 손가락 = 취소");
            Assert.IsFalse(PointerInput.PrimaryHeld);
            Next();
            Assert.IsFalse(PointerInput.PrimaryCanceled, "취소는 한 프레임");
        }

        [Test]
        public void LongPressGrowsRingThenActsAsSecondaryButton()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 at = new Vector2(500f, 400f);
            BeginTouch(1, at, screen: screen);
            Next();
            Assert.AreEqual(0f, PointerInput.RingProgress, "처음에는 고리 없음");
            Next(0.25f);
            Assert.Greater(PointerInput.RingProgress, 0f);
            Assert.Less(PointerInput.RingProgress, 1f);
            Assert.IsFalse(PointerInput.SecondaryHeld);
            Next(0.4f);
            Assert.IsTrue(PointerInput.LongPressStarted, "0.5초 넘으면 길게 누르기");
            Assert.IsTrue(PointerInput.SecondaryPressed);
            Assert.IsTrue(PointerInput.SecondaryHeld);
            Assert.IsFalse(PointerInput.PrimaryPressed, "길게 누른 손가락은 클릭이 아니다");
            Next();
            Assert.IsTrue(PointerInput.SecondaryHeld, "놓을 때까지 유지");
            EndTouch(1, at, screen: screen);
            Next();
            Assert.IsFalse(PointerInput.SecondaryHeld);
            Assert.IsFalse(PointerInput.PrimaryPressed, "떼도 클릭이 아니다");
        }

        [Test]
        public void PinchStepsAndKeepsMidpoint()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100f, 100f), screen: screen);
            BeginTouch(2, new Vector2(300f, 100f), screen: screen);
            Next();
            Assert.AreEqual(0, PointerInput.PinchStep(), "처음에는 기준만 잡는다");
            MoveTouch(2, new Vector2(500f, 100f), screen: screen);
            Next();
            Assert.AreEqual(1, PointerInput.PinchStep(), "벌리면 가까이");
            Assert.AreEqual(new Vector2(300f, 100f), PointerInput.PinchCenter, "두 손가락 가운데");
            MoveTouch(2, new Vector2(250f, 100f), screen: screen);
            Next();
            Assert.AreEqual(-1, PointerInput.PinchStep(), "오므리면 멀리");
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

        [Test]
        public void TrapPlacementHoldRotatesAtHalfSecondAndSkipsPlaceOnRelease()
        {
            var go = new GameObject("Free Placement", typeof(RectTransform), typeof(FreePlacementInput));
            try
            {
                var input = go.GetComponent<FreePlacementInput>();
                int rotated = 0, placed = 0;
                input.Rotated = () => rotated++;
                input.Placed = _ => placed++;
                PointerInput.SetTestTime(1f);
                var touch = new ExtendedPointerEventData(null) { position = new Vector2(200f, 200f), button = UnityEngine.EventSystems.PointerEventData.InputButton.Left, pointerType = UIPointerType.Touch };
                input.OnPointerDown(touch);
                PointerInput.SetTestTime(1.3f);
                input.Tick(touch.position);
                Assert.AreEqual(0, rotated, "0.5초 전에는 돌지 않는다");
                PointerInput.SetTestTime(1.6f);
                input.Tick(touch.position);
                Assert.AreEqual(1, rotated, "0.5초 지나는 순간 돌린다");
                input.Tick(touch.position);
                Assert.AreEqual(1, rotated, "한 번만");
                input.OnPointerUp(touch);
                input.OnPointerClick(touch);
                Assert.AreEqual(0, placed, "길게 눌러 돌린 뒤 떼는 클릭은 놓기가 아니다");

                PointerInput.SetTestTime(3f);
                input.OnPointerDown(touch);
                PointerInput.SetTestTime(3.1f);
                input.OnPointerUp(touch);
                input.OnPointerClick(touch);
                Assert.AreEqual(1, placed, "짧게 누르면 놓는다");
                Assert.AreEqual(1, rotated);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
