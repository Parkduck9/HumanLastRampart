#if UNITY_EDITOR
using UnityEditor;

namespace HumanLastRampart.UI.Editor
{
    // K-123 UI 그림 가져오기 규칙: 점 필터·무압축·밉 없음 (9분할 경계는 UiArt가 코드에서 스프라이트를 만들 때 넣는다)
    public sealed class UiKitTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/UI/Resources/UiKit/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
        }
    }
}
#endif
