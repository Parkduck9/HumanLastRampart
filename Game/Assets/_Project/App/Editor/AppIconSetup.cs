using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HumanLastRampart.App.Editor
{
    // 앱 아이콘 설정 (W38, 코덱스 K-124 확정본). 메뉴 또는 batchmode -executeMethod HumanLastRampart.App.Editor.AppIconSetup.Apply
    // 텍스처 임포트(무압축, 밉맵 없음, 포인트)와 PlayerSettings 아이콘 지정을 한 번에 한다.
    public static class AppIconSetup
    {
        public const string Dir = "Assets/_Project/Art/AppIcon/";
        public const string Main1024 = Dir + "app_icon_1024.png";
        public const string AdaptiveFg = Dir + "adaptive_foreground_432.png";
        public const string AdaptiveBg = Dir + "adaptive_background_432.png";

        static readonly string[] Files =
        {
            "app_icon_1024.png", "app_icon_512.png", "app_icon_256.png", "app_icon_192.png",
            "app_icon_128.png", "app_icon_48.png", "adaptive_foreground_432.png", "adaptive_background_432.png",
        };

        [MenuItem("HLR/App/Apply App Icon")]
        public static void Apply()
        {
            foreach (string f in Files) ConfigureImporter(Dir + f);
            AssetDatabase.Refresh();

            var main = Load(Main1024);
            var fg = Load(AdaptiveFg);
            var bg = Load(AdaptiveBg);

            // 기본 아이콘 (PC 포함 모든 플랫폼의 기본값)
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { main }, IconKind.Application);

            // 안드로이드: 레거시·원형은 단일 레이어, 적응형은 배경+전경 두 레이어.
            // 안드로이드 모듈이 없으면 종류 형식이 없어 건너뛴다 (모듈 설치 후 메뉴를 다시 실행).
            var legacy = AndroidKind("Legacy");
            var round = AndroidKind("Round");
            var adaptiveKind = AndroidKind("Adaptive");
            if (legacy != null && round != null && adaptiveKind != null)
            {
                FillAll(NamedBuildTarget.Android, legacy, main);
                FillAll(NamedBuildTarget.Android, round, main);
                var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, adaptiveKind);
                foreach (var icon in adaptive)
                {
                    icon.SetTexture(bg, 0);
                    icon.SetTexture(fg, 1);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, adaptiveKind, adaptive);
            }
            else Debug.LogWarning("[AppIcon] 안드로이드 모듈 없음: 안드로이드 아이콘은 건너뜀");
            AssetDatabase.SaveAssets();
            Debug.Log("[AppIcon] 앱 아이콘 적용 완료");
        }

        // UnityEditor.Android.AndroidPlatformIconKind.<name> 정적 속성을 반사로 얻는다 (모듈 없으면 null)
        public static PlatformIconKind AndroidKind(string name)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("UnityEditor.Android.AndroidPlatformIconKind");
                var prop = type?.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null) return prop.GetValue(null) as PlatformIconKind;
            }
            return null;
        }

        static void FillAll(NamedBuildTarget target, PlatformIconKind kind, Texture2D tex)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) icon.SetTexture(tex, 0);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static void ConfigureImporter(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("아이콘 파일 없음", path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { AssetDatabase.ImportAsset(path); imp = (TextureImporter)AssetImporter.GetAtPath(path); }
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Point;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 2048;
            imp.isReadable = false;
            imp.SaveAndReimport();
        }
    }
}

