using System;
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
    // 터치 규칙 (K-125): 짧게 눌렀다 떼기 = 클릭 (한 프레임 눌림 → 다음 프레임 뗌) · 눌러서 움직이기 = 끌기(누른 자리부터) ·
    // 움직이지 않고 0.5초 = 길게 누르기(= 오른쪽 버튼 누름, 놓을 때까지 유지). UI 판 위에서 시작한 터치는 이쪽에 오지 않는다 (판이 받는다).
    public static class PointerInput
    {
        public const float LongPressSeconds = 0.5f;
        public const float RingShowAfter = 0.15f;   // 이 뒤부터 길게 누르기 고리가 자란다

        enum Phase { None, Blocked, Pending, Drag, Long, TapRelease }

        static int frame = -1, testFrame = -1;
        static Phase phase;
        static Vector2 start, position, lastMouse;
        static float began;
        static bool pressed, held, released, secPressed, secHeld, longStarted;

        public static event Action<Vector2> TouchBegan;   // 모든 터치 시작 (UI 위 포함) — 파문 그리기

        // 터치가 이어지는 동안 (마우스 대신 터치를 읽는 중)
        public static bool IsTouch { get { Update(); return TouchOwnsPointer; } }
        public static bool Available { get { return Mouse.current != null || Touchscreen.current != null; } }

        // 이번 프레임 포인터를 터치가 쥐고 있는가 (뗀 프레임 포함)
        static bool TouchOwnsPointer => released || (phase != Phase.None && phase != Phase.Blocked);

        public static Vector2 Position
        {
            get
            {
                Update();
                if (TouchOwnsPointer) return position;
                var mouse = Mouse.current;
                if (mouse != null) lastMouse = mouse.position.ReadValue();
                return lastMouse;
            }
        }

        public static bool PrimaryPressed { get { Update(); var m = Mouse.current; return pressed || (m != null && m.leftButton.wasPressedThisFrame); } }
        public static bool PrimaryHeld { get { Update(); var m = Mouse.current; return held || (m != null && m.leftButton.isPressed); } }
        public static bool SecondaryPressed { get { Update(); var m = Mouse.current; return secPressed || (m != null && m.rightButton.wasPressedThisFrame); } }
        public static bool SecondaryHeld { get { Update(); var m = Mouse.current; return secHeld || (m != null && m.rightButton.isPressed); } }
        public static bool RightButtonPressed { get { var m = Mouse.current; return m != null && m.rightButton.wasPressedThisFrame; } }   // 실제 마우스 오른쪽만
        public static bool MiddlePressed { get { var m = Mouse.current; return m != null && m.middleButton.wasPressedThisFrame; } }
        public static bool LongPressStarted { get { Update(); return longStarted; } }

        // 길게 누르기 고리: 0~1 (자라는 중이 아니면 0)
        public static float RingProgress
        {
            get
            {
                Update();
                if (phase == Phase.Long) return 1f;
                if (phase != Phase.Pending) return 0f;
                float t = Time.unscaledTime - began;
                return t < RingShowAfter ? 0f : Mathf.Clamp01(t / LongPressSeconds);
            }
        }

        public static Vector2 RingPosition { get { Update(); return position; } }

        // 시험용: 상태 지우기
        public static void Reset()
        {
            phase = Phase.None;
            frame = -1;
            pressed = held = released = secPressed = secHeld = longStarted = false;
        }

        // 시험용: 편집 모드는 Time.frameCount가 멈춰 있으므로 프레임을 직접 넘긴다 (-1이면 실제 프레임)
        public static void SetTestFrame(int value) { testFrame = value; }
        static int FrameKey => testFrame >= 0 ? testFrame : Time.frameCount;

        static float Slop => Mathf.Max(10f, Screen.height * 0.012f);

        static void Update()
        {
            if (frame == FrameKey) return;
            frame = FrameKey;
            pressed = released = secPressed = longStarted = false;
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

            switch (phase)
            {
                case Phase.None:
                    if (down)
                    {
                        start = position = now;
                        began = Time.unscaledTime;
                        TouchBegan?.Invoke(now);
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
                        pressed = held = true;
                        position = start;
                        phase = Phase.Drag;
                    }
                    else if (Time.unscaledTime - began >= LongPressSeconds)
                    {
                        longStarted = secPressed = secHeld = true;
                        phase = Phase.Long;
                    }
                    break;
                case Phase.Drag:
                    position = now;
                    if (fingers > 1 || !down) { released = true; held = false; phase = down ? Phase.Blocked : Phase.None; }
                    break;
                case Phase.Long:
                    position = now;
                    if (!down) { secHeld = false; phase = Phase.None; }
                    break;
            }
        }

        // 터치 한 번이 새로 시작될 때 필요한 두 손가락 확대·축소: 벌리면 +1, 오므리면 -1 (그 외 0). 한 단계 바뀐 뒤 기준을 다시 잡는다
        static float pinchBase;

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


