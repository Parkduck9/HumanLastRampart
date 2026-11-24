using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.UI
{
    public enum TouchMode { Auto, On, Off }

    // W40 휴대전화 터치 조작 켜짐 판단 (K-125). PC(마우스·키보드)에서는 꺼져 있고 그림도 안 나온다.
    // 켜지는 경우: 강제(시험·개발) / 설정 '켜기' / 자동 — 휴대 기기(안드로이드·iOS)이거나, 터치 화면이 있고 마지막 입력이 터치(마우스·키보드가 그 뒤에 없음)
    public static class TouchInput
    {
        const string ModeKey = "hlr.touch.mode";   // 설정 창이 나중에 이 값을 고르게 한다 (자동 · 켜기 · 끄기)

        // 시험·개발용 강제 켜기. 에디터에서는 환경 변수 HLR_FORCE_TOUCH=1 로도 켠다 (사진 촬영·실기기 없는 확인)
        public static bool ForceOn = Application.isEditor && Environment.GetEnvironmentVariable("HLR_FORCE_TOUCH") == "1";

        static TouchMode mode;
        static bool modeLoaded;

        // 매 프레임 읽으므로 한 번만 PlayerPrefs에서 읽고 저장할 때만 갱신한다
        public static TouchMode Mode
        {
            get
            {
                if (!modeLoaded)
                {
                    mode = (TouchMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
                    modeLoaded = true;
                }

                return mode;
            }
            set
            {
                mode = value;
                modeLoaded = true;
                PlayerPrefs.SetInt(ModeKey, (int)value);
            }
        }

        // 시험용: 저장된 값을 건드리지 않고 메모리 값만 바꾼다 / 다시 읽게 한다
        public static void SetModeInMemory(TouchMode value) { mode = value; modeLoaded = true; }
        public static void ReloadMode() { modeLoaded = false; }

        static int evalFrame = -1, testFrame = -1;
        static bool active;
        static int sequence, lastTouch, lastKeyMouse;

        public static bool Active
        {
            get
            {
                Evaluate();
                return active;
            }
        }

        // 시험용: 프레임을 직접 넘긴다 (편집 모드는 Time.frameCount가 멈춰 있다). -1이면 실제 프레임
        public static void SetTestFrame(int value) { testFrame = value; evalFrame = -1; }

        // 시험용: 입력 이력 지우기 (이전 시험의 마지막 입력이 남지 않게)
        public static void ResetHistory()
        {
            sequence = lastTouch = lastKeyMouse = 0;
            evalFrame = -1;
        }

        static void Evaluate()
        {
            int key = testFrame >= 0 ? testFrame : Time.frameCount;
            if (evalFrame == key) return;
            evalFrame = key;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.press.isPressed) { lastTouch = ++sequence; PointerInput.NoteTouch(); }
            // 터치 노트북은 터치를 마우스 움직임·눌림으로도 알려 줄 수 있다 — 터치 직후 0.5초의 마우스는 입력으로 치지 않는다
            var mouse = Mouse.current;
            if (mouse != null && !PointerInput.MouseSuppressed && (mouse.delta.ReadValue().sqrMagnitude > 0f || mouse.leftButton.isPressed || mouse.rightButton.isPressed
                || mouse.middleButton.isPressed || mouse.scroll.ReadValue().sqrMagnitude > 0f)) lastKeyMouse = ++sequence;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed) lastKeyMouse = ++sequence;

            TouchMode current = Mode;
            if (ForceOn || current == TouchMode.On) active = true;
            else if (current == TouchMode.Off) active = false;
            else active = Application.isMobilePlatform || (touchscreen != null && lastTouch > lastKeyMouse);
        }

        // 노치·둥근 모서리를 피한 화면 안쪽 (화면 화소)
        public static Rect SafeArea => Screen.safeArea.width > 1f ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
    }
}
