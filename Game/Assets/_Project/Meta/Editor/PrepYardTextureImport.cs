#if UNITY_EDITOR
using UnityEditor;

namespace HumanLastRampart.Meta.Editor
{
    public sealed class PrepYardTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Meta/Resources/PrepYard/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
        }
    }
}
#endif
