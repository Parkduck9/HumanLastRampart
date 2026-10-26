using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Editor
{
    // 성벽 부서짐 그림 가져오기 설정 (W39 — Codex K-126)
    // - Resources/BattleArt/WallDamage 안의 원본(32 PPU, 칸 96×128·데칼 128×128) 그림: 점 필터, 가장자리 고정, 밉맵 끔, 알파 보존
    // - 실행 중 한 장으로 합치므로 읽기 가능(BattleWallDamageView). 에디터는 압축 없음, 휴대기기(Android·iPhone)는 ASTC 4x4 덮어쓰기
    // 그림을 바꿔 넣어도 매번 같은 설정이 걸리게 한다 (메타 파일은 유니티가 만든다)
    public sealed class BattleWallDamageArtImport : AssetPostprocessor
    {
        const string Folder = "/Resources/BattleArt/WallDamage/";

        // 점 그림 + 알파 가장자리가 뭉개지지 않도록 4x4 블록(가장 높은 화질). 압축 없는 원본 대비 메모리 1/4
        public static void ApplyMobile(TextureImporter importer)
        {
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.maxTextureSize = 2048;
                settings.format = TextureImporterFormat.ASTC_4x4;
                settings.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains(Folder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            ApplyMobile(importer);
        }
    }
}
