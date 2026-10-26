#if UNITY_EDITOR
using UnityEditor;

namespace HumanLastRampart.UI.Editor
{
    // K-125 터치 조작 그림: 컴퓨터에서는 무압축(UiKitTextureImport 규칙), 휴대 기기(안드로이드·iOS)에서는 ASTC 4×4로 줄인다.
    // 점 필터·밉 없음·Clamp는 그대로 (둥근 단추의 계단 모서리를 지키려고 가장 높은 화질인 4×4)
    public sealed class TouchKitTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/UI/Resources/UiKit/Touch/")) return;
            var importer = (TextureImporter)assetImporter;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.format = TextureImporterFormat.ASTC_4x4;
                settings.maxTextureSize = 1024;
                settings.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
#endif
