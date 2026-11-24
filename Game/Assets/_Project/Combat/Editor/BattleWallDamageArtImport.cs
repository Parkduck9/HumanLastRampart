using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Editor
{
    // 성벽 부서짐 그림 가져오기 설정 (W39 — Codex K-126)
    // - Resources/BattleArt/WallDamage 안의 원본(32 PPU, 칸 96×128·데칼 128×128) 그림: 점 필터, 가장자리 고정, 밉맵 끔, 알파 보존
    // - 실행 중 한 장으로 합치므로 읽기 가능(BattleWallDamageView). 압축 없음 (휴대기기도 — 합친 그림판만 메모리에 남는다). ApplyMobile은 일꾼 K-127 시트용
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

        public override uint GetVersion() => 2;   // 설정을 바꿨으니 다시 가져오게 한다

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
            // 휴대기기도 압축 없음: 28장은 작은 원본이고, 실행 중 그림판 한 장으로 합친 뒤 원본은 내린다 (BattleWallDamageView)
            // 예전에 붙은 ASTC 덮어쓰기는 지운다
            foreach (string platform in new[] { "Android", "iPhone" }) importer.ClearPlatformTextureSettings(platform);
        }
    }
}
