using System;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
using System.Text;
#endif
using UnityEngine;

namespace HumanLastRampart.Core.Settings
{
    // 설정 (K-062, 0번 메인 설정 창 P3-X02) — 진행 데이터와 따로, 이 컴퓨터에 한 벌(PlayerPrefs).
    // 화면들은 값을 읽기만 하고, 바꾸는 곳은 설정 창 하나. 바뀌면 Changed를 알린다.
    // - 소리 크기: App이 AudioListener.volume에 적용 (전체). 음악·효과 크기는 Core/Audio AudioService가 읽는다 (W24)
    // - 화면 효과·미니어처 흐림 세기: Combat BattleLook이 매 프레임 읽는다 (G 키 비교 끄기와 함께)
    // - 카메라 흔들림: Combat BattleCameraRig.Shake가 읽는다
    // - 움직임 줄이기: Frontend MenuParallax가 읽는다 (카메라 흔들림과 별개)
    // - 전체 화면/창: App이 Screen.fullScreenMode에 적용 (한 번도 바꾸지 않았으면 손대지 않음)
    // - 글자 크기: 값만 저장. 화면 글자에 반영하는 일은 UI 부품(K-047 UI/Kit) 쪽 — 결정 대기
    public static class GameSettings
    {
        const string Prefix = "hlr.settings.";
        public const int Version = 1;

        public static event Action Changed;

        public static float MasterVolume { get; private set; } = 1f;     // 0~1
        public static float MusicVolume { get; private set; } = 1f;      // 음악 0~1 (AudioService, W24)
        public static float SfxVolume { get; private set; } = 1f;        // 효과·UI·주변 소리 0~1 (AudioService, W24)
        public static bool ScreenEffects { get; private set; } = true;  // 빛번짐·색 보정·미니어처 흐림 등 (K-044)
        public static float TiltStrength { get; private set; } = 1f;     // 미니어처 흐림 세기 0(끔)~1(기본)
        public static bool CameraShake { get; private set; } = true;
        public static bool ReduceMotion { get; private set; }            // 움직임 줄이기: 메인 화면 배경 어긋남 같은 느린 장식 움직임을 멈춘다 (화면 흔들림과 따로, W36)
        public static bool Fullscreen { get; private set; } = true;
        public static bool FullscreenChosen { get; private set; }        // 사용자가 한 번이라도 고름 (아니면 실행 기본 그대로)
        public static float FontScale { get; private set; } = 1f;        // 1 보통, 1.15 크게, 1.3 아주 크게

        public static readonly float[] VolumeSteps = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        public static readonly float[] TiltSteps = { 0f, 0.5f, 1f };
        public static readonly float[] FontSteps = { 1f, 1.15f, 1.3f };

        static bool loaded;

        // W36: 제품 이름이 HumanLastRampart → Lightbearers로 바뀌어 설정 저장 위치(윈도 레지스트리 키)도 옮겨졌다. 한 번만:
        //  1) 예전 키의 값을 하나씩 복사한다. 새 키에 같은 이름이 이미 있는 값만 건너뛴다 (유니티가 새 키에 unity.player_* 같은 값을 먼저 쓴다) (윈도 실행 파일만)
        //  2) 카메라 흔들림을 이미 꺼 둔 사람은 움직임 줄이기도 켠다 (예전에는 흔들림 설정이 움직임 줄이기 대용이었다)
        // 예외 없이 끝났을 때만 표시 키를 남긴다 (실패하면 다음 실행에 다시). 기본값으로 되돌리기는 이 표시를 지우지 않는다
        public const string MigrationMarker = Prefix + "w36";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void MigrateOnce()
        {
            if (PlayerPrefs.HasKey(MigrationMarker)) return;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                CopyRegistryPrefs(@"Software\" + Application.companyName + @"\HumanLastRampart", @"Software\" + Application.companyName + @"\" + Application.productName);
            }
            catch (Exception exception) { Debug.LogWarning("[설정] 예전 설정을 가져오지 못했습니다: " + exception.Message); return; }
#endif
            if (PlayerPrefs.GetInt(Prefix + "shake", 1) == 0 && !PlayerPrefs.HasKey(Prefix + "reduceMotion")) PlayerPrefs.SetInt(Prefix + "reduceMotion", 1);
            PlayerPrefs.SetInt(MigrationMarker, 1);
            PlayerPrefs.Save();
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // 유니티 쪽 어셈블리에 Microsoft.Win32.Registry가 없어 advapi32를 직접 부른다 (값 이름·종류·바이트를 그대로 옮긴다)
        static readonly IntPtr HkeyCurrentUser = new IntPtr(unchecked((int)0x80000001));
        const int KeyRead = 0x20019, KeyAllAccess = 0xF003F;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegOpenKeyExW(IntPtr key, string sub, int options, int access, out IntPtr result);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegCreateKeyExW(IntPtr key, string sub, int reserved, string cls, int options, int access, IntPtr security, out IntPtr result, out int disposition);
        [DllImport("advapi32.dll")] static extern int RegQueryInfoKeyW(IntPtr key, IntPtr cls, IntPtr clsLen, IntPtr reserved, IntPtr subKeys, IntPtr maxSub, IntPtr maxCls, out int values, out int maxName, out int maxData, IntPtr security, IntPtr lastWrite);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegEnumValueW(IntPtr key, int index, StringBuilder name, ref int nameLen, IntPtr reserved, out int type, byte[] data, ref int dataLen);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegSetValueExW(IntPtr key, string name, int reserved, int type, byte[] data, int dataLen);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegQueryValueExW(IntPtr key, string name, IntPtr reserved, out int type, byte[] data, ref int dataLen);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);

        // 레지스트리 키 값을 그대로 복사한다. 새 키에 같은 이름의 값이 이미 있으면 그 값만 건너뛴다. 복사한 값 수를 돌려준다 (예전 키가 없으면 0 = 할 일 없음, 열기·쓰기 실패는 예외)
        public static int CopyRegistryPrefs(string fromPath, string toPath)
        {
            if (RegOpenKeyExW(HkeyCurrentUser, fromPath, 0, KeyRead, out IntPtr from) != 0) return 0;
            IntPtr to = IntPtr.Zero;
            try
            {
                if (RegQueryInfoKeyW(from, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out int count, out int maxName, out int maxData, IntPtr.Zero, IntPtr.Zero) != 0 || count == 0) return 0;
                if (RegCreateKeyExW(HkeyCurrentUser, toPath, 0, null, 0, KeyAllAccess, IntPtr.Zero, out to, out _) != 0) throw new InvalidOperationException("새 설정 키를 열지 못했습니다");
                int copied = 0;
                for (int i = 0; i < count; i++)
                {
                    var name = new StringBuilder(maxName + 1);
                    int nameLen = maxName + 1, dataLen = maxData;
                    var data = new byte[Math.Max(1, maxData)];
                    if (RegEnumValueW(from, i, name, ref nameLen, IntPtr.Zero, out int type, data, ref dataLen) != 0) continue;
                    string valueName = name.ToString(); int probeLen = 0;
                    if (RegQueryValueExW(to, valueName, IntPtr.Zero, out _, null, ref probeLen) == 0) continue; // 새 키에 이미 있음
                    var exact = new byte[dataLen]; Array.Copy(data, exact, dataLen);
                    if (RegSetValueExW(to, valueName, 0, type, exact, dataLen) == 0) copied++; else throw new InvalidOperationException("설정 값을 쓰지 못했습니다: " + valueName);
                }
                return copied;
            }
            finally
            {
                RegCloseKey(from);
                if (to != IntPtr.Zero) RegCloseKey(to);
            }
        }
#endif

        public static void Load()
        {
            MigrateOnce();
            loaded = true;
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "music", 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "sfx", 1f));
            ScreenEffects = PlayerPrefs.GetInt(Prefix + "effects", 1) != 0;
            TiltStrength = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "tilt", 1f));
            CameraShake = PlayerPrefs.GetInt(Prefix + "shake", 1) != 0;
            ReduceMotion = PlayerPrefs.GetInt(Prefix + "reduceMotion", 0) != 0;
            FullscreenChosen = PlayerPrefs.HasKey(Prefix + "fullscreen");
            Fullscreen = FullscreenChosen ? PlayerPrefs.GetInt(Prefix + "fullscreen", 1) != 0 : Screen.fullScreen;
            FontScale = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "font", 1f), 1f, 1.3f);
        }

        public static void EnsureLoaded() { if (!loaded) Load(); }

        public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "volume", MasterVolume); Save(); }
        public static void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "music", MusicVolume); Save(); }
        public static void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "sfx", SfxVolume); Save(); }
        public static void SetScreenEffects(bool on) { ScreenEffects = on; PlayerPrefs.SetInt(Prefix + "effects", on ? 1 : 0); Save(); }
        public static void SetTiltStrength(float value) { TiltStrength = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "tilt", TiltStrength); Save(); }
        public static void SetCameraShake(bool on) { CameraShake = on; PlayerPrefs.SetInt(Prefix + "shake", on ? 1 : 0); Save(); }
        public static void SetReduceMotion(bool on) { ReduceMotion = on; PlayerPrefs.SetInt(Prefix + "reduceMotion", on ? 1 : 0); Save(); }
        public static void SetFullscreen(bool on) { Fullscreen = on; FullscreenChosen = true; PlayerPrefs.SetInt(Prefix + "fullscreen", on ? 1 : 0); Save(); }
        public static void SetFontScale(float value) { FontScale = Mathf.Clamp(value, 1f, 1.3f); PlayerPrefs.SetFloat(Prefix + "font", FontScale); Save(); }

        // 처음 값으로 (설정 창 '기본값으로')
        public static void ResetToDefaults()
        {
            foreach (string key in new[] { "volume", "music", "sfx", "effects", "tilt", "shake", "reduceMotion", "fullscreen", "font" }) PlayerPrefs.DeleteKey(Prefix + key);
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
