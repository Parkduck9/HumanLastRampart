using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Editor
{
    // 성벽손 오우거 동작 시트 가져오기 (W43, Codex K-129): 칸 768×864 가로 한 줄 (가로 최대 7680) — 최대 크기 8192 이상 유지
    // - 점 필터·가장자리 고정·밉맵 끔. 색은 sRGB, 노멀은 선형 NormalMap (초록 뒤집기 끔)
    // - 압축 없는 6144×864 한 장이 21MB라 데스크톱은 압축, 휴대기기는 ASTC 4x4 (점 그림 알파 가장자리가 뭉개지지 않는 가장 높은 화질)
    // - 그림이 필요할 때만 불러온다 (BattleBossArt) — 한 번에 다 올리지 않는다
    public sealed class BossArtImport : AssetPostprocessor
    {
        const string Folder = "/Resources/BattleArt/Boss/";

        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains(Folder)) return;

            bool normal = path.EndsWith("_normal.png");
            var importer = (TextureImporter)assetImporter;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = !normal;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !normal;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 100;
            // 휴대기기: 가로 4096을 넘는 시트(칸 6개 이상 = 4608~7680)는 최대 크기 4096으로 제한 — 오래된 휴대 GPU가 못 다루는 크기이고 메모리도 줄인다.
            // 유니티는 2의 거듭제곱으로 줄이므로 이 시트들은 가로세로 절반이 된다 (칸 768×864 → 384×432). 4칸 이하 시트는 그대로
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out _);
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.maxTextureSize = sourceWidth > 4096 ? 4096 : 8192;
                settings.format = TextureImporterFormat.ASTC_4x4;
                settings.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
