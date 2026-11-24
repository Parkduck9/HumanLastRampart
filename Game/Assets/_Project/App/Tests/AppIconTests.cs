using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HumanLastRampart.App.Tests
{
    // 앱 아이콘 (W38): ProjectSettings에 확정 아이콘이 들어 있는지 확인
    public class AppIconTests
    {
        const string Dir = "Assets/_Project/Art/AppIcon/";

        // 안드로이드 모듈이 없으면 종류 형식이 없어 해당 시험은 건너뛴다
        static PlatformIconKind Kind(string name)
        {
            var k = HumanLastRampart.App.Editor.AppIconSetup.AndroidKind(name);
            if (k == null) Assert.Ignore("안드로이드 모듈 없음");
            return k;
        }

        [Test]
        public void DefaultIcon_IsMainIcon()
        {
            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Application);
            Assert.IsNotEmpty(icons);
            Assert.IsNotNull(icons[0]);
            Assert.AreEqual(Dir + "app_icon_1024.png", AssetDatabase.GetAssetPath(icons[0]));
        }

        [Test]
        public void AndroidAdaptive_HasBackgroundAndForeground()
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, Kind("Adaptive"));
            Assert.IsNotEmpty(icons);
            foreach (var icon in icons)
            {
                Assert.AreEqual(Dir + "adaptive_background_432.png", AssetDatabase.GetAssetPath(icon.GetTexture(0)));
                Assert.AreEqual(Dir + "adaptive_foreground_432.png", AssetDatabase.GetAssetPath(icon.GetTexture(1)));
            }
        }

        [Test]
        public void AndroidLegacyAndRound_AreSet()
        {
            foreach (var kind in new PlatformIconKind[] { Kind("Legacy"), Kind("Round") })
                foreach (var icon in PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind))
                    Assert.IsNotNull(icon.GetTexture(0));
        }
    }
}

