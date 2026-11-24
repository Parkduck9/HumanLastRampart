using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Editor
{
    public sealed class CharacterSheetImport : AssetPostprocessor
    {
        // 기존 임포트 캐시도 새 규격으로 맞춘다. 자동 갱신에 의존해 빈 참조를 남기지 않는다.
        public static void RefreshInstalled()
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                bool character = path.StartsWith("Assets/_Project/Art/Characters/");
                bool haul = path.Contains("/Resources/BattleArt/Cannon/crew_");
                if ((!character && !haul) || !path.EndsWith(".png")) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith("Assets/_Project/Data/Combat/") && path.EndsWith(".asset"))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        public override int GetPostprocessOrder() => 100;
        void OnPreprocessTexture()
        {
            bool character = assetPath.StartsWith("Assets/_Project/Art/Characters/");
            bool haulNormal = assetPath.Contains("/Resources/BattleArt/Cannon/crew_") && assetPath.EndsWith("_normal.png");
            if (!character && !haulNormal) return;
            bool normal = assetPath.EndsWith("_normal.png");
            var importer = (TextureImporter)assetImporter;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = !normal;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !normal;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
