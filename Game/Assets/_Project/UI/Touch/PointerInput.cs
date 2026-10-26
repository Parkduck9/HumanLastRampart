using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace HumanLastRampart.UI
{
    // 누르는 판정을 눌러도 화면 판으로 치지 않을 그림 (전체 화면 투명 막·고리처럼 스스로 눌림을 받는 것)
    public sealed class PointerPassThrough : MonoBehaviour { }

    // W40 마우스와 한 손가락 터치를 하나로 본 포인터. 전투 지도가 마우스 대신 이것을 읽는다.
    // 터치 규칙 (K-125): 짧게 눌렀다 떼기 = 클릭 (한 프레임 눌림 → 다음 프레임 뗌) · 눌러서 움직이기 = 끌기(누른 자리부터, PressIsDrag) ·
    // 움직이지 않고 0.5초 = 길게 누르기(= 오른쪽 버튼 누름, 놓을 때까지 유지). UI 판 위에서 시작한 터치는 이쪽에 오지 않는다 (판이 받는다).
    // 끌기 도중 두 번째 손가락이 닿으면 놓기가 아니라 취소(PrimaryCanceled)다. 터치 직후 0.5초의 마우스(터치 노트북의 마우스 흉내)는 무시한다.
    public static class PointerInput
    {
        public const float LongPressSeconds = 0.5f;
        public const float RingShowAfter = 0.15f;   // 이 뒤부터 길게 누르기 고리가 자란다
        public const float MouseIgnoreSeconds = 0.5f;

        enum Phase { None, Blocked, Pending, Drag, Long, TapRelease }

        static int frame = -1, testFrame = -1;
        static float testTime = -1f;
        static Phase phase;
        static Vector2 start, position, lastPosition;
        static float began, lastTouchTime = -100f;
        static bool pressed, pressIsDrag, held, released, canceled, secPressed, secHeld, longStarted;

        // 시험용: 편집 모드는 Time이 멈춰 있으므로 프레임·시각을 직접 넘긴다 (-1이면 실제 값)
        public static void SetTestFrame(int value) { testFrame = value; }
        public static void SetTestTime(float value) { testTime = value; }
        public static float Now => testTime >= 0f ? testTime : Time.unscaledTime;
        static int FrameKey => testFrame >= 0 ? testFrame : Time.frameCount;

        internal static void NoteTouch() { lastTouchTime = Now; }

        // 터치 중이거나 막 뗀 뒤 0.5초 안: 마우스 값은 믿지 않는다
        public static bool MouseSuppressed => Now - lastTouchTime < MouseIgnoreSeconds;
        static Mouse M => MouseSuppressed ? null : Mouse.current;

        // 터치가 이어지는 동안 (마우스 대신 터치를 읽는 중)
        public static bool IsTouch { get { Update(); return TouchOwnsPointer; } }
        public static bool Available { get { return Mouse.current != null || Touchscreen.current != null; } }

        // 이번 프레임 포인터를 터치가 쥐고 있는가 (뗀·취소한 프레임 포함)
        static bool TouchOwnsPointer => released || canceled || (phase != Phase.None && phase != Phase.Blocked);

        // 손가락이 닿아 있지 않으면 마지막 터치 자리 (마우스가 없는 휴대 기기에서 (0,0)으로 튀지 않는다)
        public static Vector2 Position
        {
            get
            {
                Update();
                if (TouchOwnsPointer) { lastPosition = position; return position; }
                Mouse mouse = M;
                if (mouse != null) lastPosition = mouse.position.ReadValue();
                return lastPosition;
            }
        }

        public static bool PrimaryPressed { get { Update(); var m = M; return pressed || (m != null && m.leftButton.wasPressedThisFrame); } }
        public static bool PrimaryHeld { get { Update(); var m = M; return held || (m != null && m.leftButton.isPressed); } }
        public static bool PressIsDrag { get { Update(); return pressed && pressIsDrag; } }   // 이번 눌림이 끌기의 시작인가 (터치만)
        public static bool PrimaryCanceled { get { Update(); return canceled; } }
        public static bool SecondaryPressed { get { Update(); var m = M; return secPressed || (m != null && m.rightButton.wasPressedThisFrame); } }
        public static bool SecondaryHeld { get { Update(); var m = M; return secHeld || (m != null && m.rightButton.isPressed); } }
        public static bool RightButtonPressed { get { Update(); var m = M; return m != null && m.rightButton.wasPressedThisFrame; } }   // 실제 마우스 오른쪽만
        public static bool MiddlePressed { get { Update(); var m = M; return m != null && m.middleButton.wasPressedThisFrame; } }
        public static bool LongPressStarted { get { Update(); return longStarted; } }

        // 길게 누르기 고리: 0~1 (자라는 중이 아니면 0)
        public static float RingProgress
        {
            get
            {
                Update();
                if (phase == Phase.Long) return 1f;
                if (phase != Phase.Pending) return 0f;
                float t = Now - began;
                return t < RingShowAfter ? 0f : Mathf.Clamp01(t / LongPressSeconds);
            }
        }

        public static Vector2 RingPosition { get { Update(); return position; } }

        // 시험용: 상태 지우기
        public static void Reset()
        {
            phase = Phase.None;
            frame = -1;
            lastTouchTime = -100f;
            lastPosition = Vector2.zero;
            pressed = pressIsDrag = held = released = canceled = secPressed = secHeld = longStarted = false;
        }

        static float Slop => Mathf.Max(10f, Screen.height * 0.012f);

        static void Update()
        {
            if (frame == FrameKey) return;
            frame = FrameKey;
            pressed = pressIsDrag = released = canceled = secPressed = longStarted = false;
            if (phase == Phase.TapRelease)
            {
                released = true;
                held = false;
                phase = Phase.None;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen == null) { if (phase != Phase.None) phase = Phase.None; held = secHeld = false; return; }
            TouchControl primary = touchscreen.primaryTouch;
            bool down = primary.press.isPressed;
            Vector2 now = primary.position.ReadValue();
            int fingers = 0;
            foreach (TouchControl touch in touchscreen.touches) if (touch.press.isPressed) fingers++;
            if (fingers > 0) NoteTouch();

            switch (phase)
            {
                case Phase.None:
                    if (down)
                    {
                        start = position = now;
                        began = Now;
                        phase = fingers > 1 || OverUi(now) ? Phase.Blocked : Phase.Pending;
                    }
                    break;
                case Phase.Blocked:
                    if (!down) phase = Phase.None;
                    break;
                case Phase.Pending:
                    position = now;
                    if (fingers > 1) { phase = Phase.Blocked; break; }   // 두 손가락(확대·축소)은 포인터로 안 본다
                    if (!down)
                    {
                        pressed = held = true;   // 짧게 눌렀다 뗌: 이번 프레임 눌림, 다음 프레임 뗌
                        position = start;
                        phase = Phase.TapRelease;
                    }
                    else if ((now - start).magnitude > Slop)
                    {
                        pressed = held = pressIsDrag = true;
                        position = start;
                        phase = Phase.Drag;
                    }
                    else if (Now - began >= LongPressSeconds)
                    {
                        longStarted = secPressed = secHeld = true;
                        phase = Phase.Long;
                    }
                    break;
                case Phase.Drag:
                    position = now;
                    if (fingers > 1) { canceled = true; held = false; phase = Phase.Blocked; }   // 두 번째 손가락 = 취소 (놓기 아님)
                    else if (!down) { released = true; held = false; phase = Phase.None; }
                    break;
                case Phase.Long:
                    position = now;
                    if (!down || fingers > 1) { secHeld = false; phase = down ? Phase.Blocked : Phase.None; }
                    break;
            }
        }

        // 두 손가락 확대·축소: 벌리면 +1, 오므리면 -1 (그 외 0). 한 단계 바뀐 뒤 기준을 다시 잡는다. PinchCenter = 두 손가락 가운데 (화면 화소)
        static float pinchBase;
        public static Vector2 PinchCenter { get; private set; }

        public static int PinchStep()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null) return 0;
            Vector2 a = default, b = default;
            int count = 0;
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (count == 0) a = touch.position.ReadValue(); else if (count == 1) b = touch.position.ReadValue();
                count++;
            }

            if (count != 2) { pinchBase = 0f; return 0; }
            PinchCenter = (a + b) * 0.5f;
            float distance = Vector2.Distance(a, b);
            if (pinchBase <= 0f) { pinchBase = distance; return 0; }
            float ratio = distance / pinchBase;
            if (ratio > 1.3f) { pinchBase = distance; return 1; }
            if (ratio < 0.77f) { pinchBase = distance; return -1; }
            return 0;
        }

        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        // 이 화면 점 아래에 눌림을 받는 판(그림)이 있는가. PointerPassThrough가 붙은 것은 건너뛴다
        public static bool OverUi(Vector2 point)
        {
            EventSystem system = EventSystem.current;
            if (system == null) return false;
            hits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = point }, hits);
            foreach (RaycastResult hit in hits)
            {
                if (hit.gameObject == null || hit.gameObject.GetComponentInParent<PointerPassThrough>() != null) continue;
                return true;
            }

            return false;
        }
    }
}
