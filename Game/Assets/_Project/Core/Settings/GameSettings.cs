using System;
using UnityEngine;

namespace HumanLastRampart.Core.Settings
{
    // 설정 (K-062, 0번 메인 설정 창 P3-X02) — 진행 데이터와 따로, 이 컴퓨터에 한 벌(PlayerPrefs).
    // 화면들은 값을 읽기만 하고, 바꾸는 곳은 설정 창 하나. 바뀌면 Changed를 알린다.
    // - 소리 크기: App이 AudioListener.volume에 적용 (소리가 아직 없어 자리만)
    // - 화면 효과·미니어처 흐림 세기: Combat BattleLook이 매 프레임 읽는다 (G 키 비교 끄기와 함께)
    // - 카메라 흔들림: Combat BattleCameraRig.Shake가 읽는다
    // - 전체 화면/창: App이 Screen.fullScreenMode에 적용 (한 번도 바꾸지 않았으면 손대지 않음)
    // - 글자 크기: 값만 저장. 화면 글자에 반영하는 일은 UI 부품(K-047 UI/Kit) 쪽 — 결정 대기
    public static class GameSettings
    {
        const string Prefix = "hlr.settings.";
        public const int Version = 1;

        public static event Action Changed;

        public static float MasterVolume { get; private set; } = 1f;     // 0~1
        public static bool ScreenEffects { get; private set; } = true;   // 빛번짐·색 보정·미니어처 흐림 등 (K-044)
        public static float TiltStrength { get; private set; } = 1f;     // 미니어처 흐림 세기 0(끔)~1(기본)
        public static bool CameraShake { get; private set; } = true;
        public static bool Fullscreen { get; private set; } = true;
        public static bool FullscreenChosen { get; private set; }        // 사용자가 한 번이라도 고름 (아니면 실행 기본 그대로)
        public static float FontScale { get; private set; } = 1f;        // 1 보통, 1.15 크게, 1.3 아주 크게

        public static readonly float[] VolumeSteps = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        public static readonly float[] TiltSteps = { 0f, 0.5f, 1f };
        public static readonly float[] FontSteps = { 1f, 1.15f, 1.3f };

        static bool loaded;

        public static void Load()
        {
            loaded = true;
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", 1f));
            ScreenEffects = PlayerPrefs.GetInt(Prefix + "effects", 1) != 0;
            TiltStrength = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "tilt", 1f));
            CameraShake = PlayerPrefs.GetInt(Prefix + "shake", 1) != 0;
            FullscreenChosen = PlayerPrefs.HasKey(Prefix + "fullscreen");
            Fullscreen = FullscreenChosen ? PlayerPrefs.GetInt(Prefix + "fullscreen", 1) != 0 : Screen.fullScreen;
            FontScale = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "font", 1f), 1f, 1.3f);
        }

        public static void EnsureLoaded() { if (!loaded) Load(); }

        public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "volume", MasterVolume); Save(); }
        public static void SetScreenEffects(bool on) { ScreenEffects = on; PlayerPrefs.SetInt(Prefix + "effects", on ? 1 : 0); Save(); }
        public static void SetTiltStrength(float value) { TiltStrength = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "tilt", TiltStrength); Save(); }
        public static void SetCameraShake(bool on) { CameraShake = on; PlayerPrefs.SetInt(Prefix + "shake", on ? 1 : 0); Save(); }
        public static void SetFullscreen(bool on) { Fullscreen = on; FullscreenChosen = true; PlayerPrefs.SetInt(Prefix + "fullscreen", on ? 1 : 0); Save(); }
        public static void SetFontScale(float value) { FontScale = Mathf.Clamp(value, 1f, 1.3f); PlayerPrefs.SetFloat(Prefix + "font", FontScale); Save(); }

        // 처음 값으로 (설정 창 '기본값으로')
        public static void ResetToDefaults()
        {
            foreach (string key in new[] { "volume", "effects", "tilt", "shake", "fullscreen", "font" }) PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
            Load();
            Changed?.Invoke();
        }

        static void Save()
        {
            loaded = true;
            PlayerPrefs.SetInt(Prefix + "version", Version);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 목록에서 지금 값 다음 값 (설정 창 버튼을 누를 때마다 돌아간다)
        public static float NextStep(float[] steps, float current)
        {
            for (int i = 0; i < steps.Length; i++)
                if (current < steps[i] - 0.001f) return steps[i];
            return steps[0];
        }
    }
}
