using System;
using System.IO;
using System.Reflection;
using HumanLastRampart.Core.Save;
using HumanLastRampart.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.App.Tests
{
    // W36: 움직임 줄이기 설정, 정비 마당 등실 불빛 세기, 제품 이름 변경에 따른 저장 옮김.
    // Meta·Frontend는 시험 어셈블리가 직접 참조하지 않아 반사로 부른다 (새 모듈 의존을 만들지 않으려고).
    public sealed class LightbearersTests
    {
        const string ReduceKey = "hlr.settings.reduceMotion", ShakeKey = "hlr.settings.shake";
        bool hadReduce, hadShake, hadMarker;
        int markerValue;
        bool reduce, shake;

        [SetUp]
        public void SetUp()
        {
            GameSettings.Load();
            hadReduce = PlayerPrefs.HasKey(ReduceKey); hadShake = PlayerPrefs.HasKey(ShakeKey);
            hadMarker = PlayerPrefs.HasKey(GameSettings.MigrationMarker); markerValue = PlayerPrefs.GetInt(GameSettings.MigrationMarker, 0);
            reduce = GameSettings.ReduceMotion; shake = GameSettings.CameraShake;
        }

        [TearDown]
        public void TearDown()
        {
            // 사용자 설정을 시험 전 상태로 (키가 없었으면 없는 채로)
            if (hadReduce) GameSettings.SetReduceMotion(reduce); else PlayerPrefs.DeleteKey(ReduceKey);
            if (hadShake) GameSettings.SetCameraShake(shake); else PlayerPrefs.DeleteKey(ShakeKey);
            if (hadMarker) PlayerPrefs.SetInt(GameSettings.MigrationMarker, markerValue); else PlayerPrefs.DeleteKey(GameSettings.MigrationMarker);
            PlayerPrefs.Save();
            GameSettings.Load();
        }

        static void ForceProperty(string name, bool value) =>
            typeof(GameSettings).GetProperty(name).GetSetMethod(true).Invoke(null, new object[] { value });

        [Test]
        public void ReduceMotion_PersistsAndIsSeparateFromCameraShake()
        {
            GameSettings.SetCameraShake(true);
            GameSettings.SetReduceMotion(true);
            Assert.IsTrue(GameSettings.CameraShake, "움직임 줄이기가 화면 흔들림을 바꾸지 않는다");
            // 새로 실행한 것처럼 메모리 값을 지우고 저장된 값에서 다시 읽는다
            ForceProperty("ReduceMotion", false);
            GameSettings.Load();
            Assert.IsTrue(GameSettings.ReduceMotion, "저장된 값이 돌아온다");

            GameSettings.SetCameraShake(false);
            Assert.IsTrue(GameSettings.ReduceMotion, "화면 흔들림이 움직임 줄이기를 바꾸지 않는다");
            GameSettings.SetReduceMotion(false);
            Assert.IsFalse(GameSettings.CameraShake);

            GameSettings.SetReduceMotion(true);
            GameSettings.ResetToDefaults();
            Assert.IsFalse(GameSettings.ReduceMotion, "기본값으로 하면 움직임 줄이기는 꺼진다");
            Assert.IsTrue(GameSettings.CameraShake);
            Assert.IsFalse(PlayerPrefs.HasKey(ReduceKey), "저장 키도 지워진다");
        }

        [Test]
        public void MenuParallax_StopsOnReduceMotion_NotOnCameraShake()
        {
            Type type = Type.GetType("HumanLastRampart.Frontend.MenuParallax, HLR.Frontend", true);
            var parent = new GameObject("Parallax Test", typeof(RectTransform));
            try
            {
                var parallax = (Component)type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { parent.transform });
                Assert.IsNotNull(parallax, "배경 그림이 있어야 이 시험이 의미 있다");
                type.GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(parallax, 3.5f);   // 사인 곡선이 0이 아닌 시각
                MethodInfo offset = type.GetMethod("OffsetOf");
                Vector2 Of(int layer) => (Vector2)offset.Invoke(parallax, new object[] { layer });

                GameSettings.SetReduceMotion(false); GameSettings.SetCameraShake(false);
                Assert.Greater(Of(3).sqrMagnitude, 0.01f, "화면 흔들림이 꺼져도 움직임 줄이기가 꺼져 있으면 배경은 움직인다");
                GameSettings.SetReduceMotion(true); GameSettings.SetCameraShake(true);
                Assert.AreEqual(Vector2.zero, Of(3), "움직임 줄이기가 켜지면 화면 흔들림과 상관없이 멈춘다");
                for (int i = 0; i < 4; i++) Assert.AreEqual(Vector2.zero, Of(i));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        static Type Yard => Type.GetType("HumanLastRampart.Meta.PrepYard, HLR.Meta", true);
        static T Call<T>(string name, params object[] args) => (T)Yard.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        static Color32[] Paint(float light, bool ignition) => Call<Color32[]>("PaintLamp", light, ignition);
        static int FlamePixels(Color32[] p) { int n = 0; foreach (var c in p) if (c.a == 255 && c.r >= 200) n++; return n; }
        static int CoverPixels(Color32[] p) { int n = 0; foreach (var c in p) if (c.a > 0 && c.r < 80 && c.g < 120) n += c.a; return n; }
        static int HaloPixels(Color32[] p) { int n = 0; foreach (var c in p) if (c.a > 0 && c.a < 100 && c.r == 255) n++; return n; }

        [Test]
        public void PrepLamp_IntensityFollowsRepair()
        {
            float dead = Call<float>("LampLight", 0f, false), weak = Call<float>("LampLight", 10f, false);
            float mid = Call<float>("LampLight", 50f, false), strong = Call<float>("LampLight", 100f, false), ignition = Call<float>("LampLight", 100f, true);
            Assert.AreEqual(0f, dead, "복구 0은 꺼짐");
            Assert.Greater(weak, 0f); Assert.Less(weak, mid); Assert.Less(mid, strong); Assert.Less(strong, ignition);
            Assert.AreEqual(1f, ignition, "점화의 밤은 가득");
            Assert.AreEqual(1f, Call<float>("LampLight", 0f, true), "점화의 밤이면 복구 값과 상관없이 가득");

            var pDead = Paint(dead, false); var pWeak = Paint(weak, false); var pMid = Paint(mid, false);
            var pStrong = Paint(strong, false); var pIgn = Paint(ignition, true);
            Assert.AreEqual(0, FlamePixels(pDead), "꺼진 등에는 불꽃이 없다");
            Assert.Greater(CoverPixels(pDead), 0, "꺼진 등은 그림 속 등롱을 어둠 덮개로 가린다");
            Assert.AreEqual(0, HaloPixels(pDead));
            Assert.Greater(FlamePixels(pWeak), 0, "약한 불꽃");
            Assert.Greater(FlamePixels(pMid), FlamePixels(pWeak));
            Assert.Greater(FlamePixels(pStrong), FlamePixels(pMid));
            Assert.Less(CoverPixels(pWeak), CoverPixels(pDead), "복구가 오르면 덮개가 옅어진다");
            Assert.LessOrEqual(CoverPixels(pStrong), CoverPixels(pMid));
            Assert.AreEqual(0, CoverPixels(pIgn), "점화의 밤에는 덮개가 없다");
            Assert.Greater(HaloPixels(pIgn), HaloPixels(pStrong), "점화의 밤은 테두리 빛이 더 넓다");
        }

        [Test]
        public void SettingsMigration_TurnsReduceMotionOnOnceForPlayersWhoHadShakeOff()
        {
            PlayerPrefs.DeleteKey(GameSettings.MigrationMarker); PlayerPrefs.DeleteKey(ReduceKey);
            PlayerPrefs.SetInt(ShakeKey, 0);
            GameSettings.MigrateOnce();
            Assert.IsTrue(PlayerPrefs.HasKey(GameSettings.MigrationMarker), "표시 키가 남는다");
            Assert.AreEqual(1, PlayerPrefs.GetInt(ReduceKey, 0), "흔들림을 꺼 둔 사람은 움직임 줄이기가 켜진다");
            // 사용자가 다시 끄면 두 번째 실행은 건드리지 않는다
            PlayerPrefs.SetInt(ReduceKey, 0);
            GameSettings.MigrateOnce();
            Assert.AreEqual(0, PlayerPrefs.GetInt(ReduceKey, 1), "한 번만");
            // 흔들림이 켜져 있던 사람은 영향이 없다
            PlayerPrefs.DeleteKey(GameSettings.MigrationMarker); PlayerPrefs.DeleteKey(ReduceKey); PlayerPrefs.SetInt(ShakeKey, 1);
            GameSettings.MigrateOnce();
            Assert.IsFalse(PlayerPrefs.HasKey(ReduceKey));
        }

#if UNITY_EDITOR_WIN
        [Test]
        public void RegistryPrefs_CopiedOnlyIntoEmptyKey()
        {
            string root = @"Software\HLRTest_W36_" + Guid.NewGuid().ToString("N");
            try
            {
                // 유니티 어셈블리에 레지스트리 형식이 없어 reg.exe로 만들고 읽는다
                Reg("add", @"HKCU\" + root + @"\old", "/v", "hlr.settings.volume_h1", "/t", "REG_DWORD", "/d", "1056964608", "/f");
                Reg("add", @"HKCU\" + root + @"\old", "/v", "blob_h2", "/t", "REG_BINARY", "/d", "010203", "/f");
                Assert.AreEqual(2, GameSettings.CopyRegistryPrefs(root + @"\old", root + @"\new"), "빈 새 키에는 복사한다");
                string dump = Reg("query", @"HKCU\" + root + @"\new").ToLowerInvariant();
                StringAssert.Contains("reg_dword", dump); StringAssert.Contains("0x3f000000", dump);
                StringAssert.Contains("reg_binary", dump); StringAssert.Contains("010203", dump);
                Assert.AreEqual(0, GameSettings.CopyRegistryPrefs(root + @"\old", root + @"\new"), "새 키에 값이 있으면 건드리지 않는다");
                Assert.AreEqual(0, GameSettings.CopyRegistryPrefs(root + @"\none", root + @"\new2"), "예전 키가 없으면 아무 일도 없다");
            }
            finally { Reg("delete", @"HKCU\" + root, "/f"); }
        }

        static string Reg(params string[] args)
        {
            var info = new System.Diagnostics.ProcessStartInfo("reg.exe", string.Join(" ", args)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var process = System.Diagnostics.Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit();
                return output;
            }
        }
#endif

        [Test]
        public void LegacySave_IsCopiedOnceToNewPath()
        {
            string root = Path.Combine(Path.GetTempPath(), "hlr_migrate_test_" + Guid.NewGuid().ToString("N"));
            string oldFolder = Path.Combine(root, CampaignSave.LegacyProductName, "save"), newFolder = Path.Combine(root, "Lightbearers", "save");
            try
            {
                Assert.IsFalse(CampaignSave.MigrateLegacy(oldFolder, newFolder), "예전 저장이 없으면 아무 일도 없다");
                Directory.CreateDirectory(oldFolder);
                File.WriteAllText(Path.Combine(oldFolder, "slot1.json"), "old");
                Assert.IsTrue(CampaignSave.MigrateLegacy(oldFolder, newFolder), "새 위치에 저장이 없으면 가져온다");
                Assert.AreEqual("old", File.ReadAllText(Path.Combine(newFolder, "slot1.json")));
                Assert.IsTrue(File.Exists(Path.Combine(oldFolder, "slot1.json")), "예전 파일은 남긴다");
                File.WriteAllText(Path.Combine(newFolder, "slot1.json"), "new");
                File.WriteAllText(Path.Combine(oldFolder, "slot1.json"), "older");
                Assert.IsFalse(CampaignSave.MigrateLegacy(oldFolder, newFolder), "새 위치에 이미 저장이 있으면 덮어쓰지 않는다");
                Assert.AreEqual("new", File.ReadAllText(Path.Combine(newFolder, "slot1.json")));
                // 진짜 한 번만: 새 위치 저장을 지워도 예전 것이 되살아나지 않는다
                File.Delete(Path.Combine(newFolder, "slot1.json"));
                Assert.IsFalse(CampaignSave.MigrateLegacy(oldFolder, newFolder), "삭제 뒤 다시 가져오지 않는다");
                Assert.IsFalse(File.Exists(Path.Combine(newFolder, "slot1.json")));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
