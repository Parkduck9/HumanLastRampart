using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Editor
{
    // 전투 3D 표면 그림 가져오기 설정 (K-005, D-068 — Codex 내보내기 설명서 기준)
    // - Resources/BattleArt/World 안의 <표면>_albedo: 색(sRGB), <표면>_normal: 노멀맵(Linear)
    // - 둘 다 도트 그대로(Point), 반복(Repeat), 압축 없음, 밉맵 켬
    // 그림을 바꿔 넣어도 매번 같은 설정이 걸리게 한다 (손으로 설정을 고칠 필요 없음)
    public sealed class BattleWorldTextureImport : AssetPostprocessor
    {
        const string Folder = "/Resources/BattleArt/World/";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains(Folder)) return;

            var importer = (TextureImporter)assetImporter;
            bool normal = path.EndsWith("_normal.png");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
