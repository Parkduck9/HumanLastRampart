using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Editor
{
    // 전장 소품·대포·하늘 그림 가져오기 설정 (K-031 — Codex K-016·018·024·025 시안 연결)
    // - Resources/BattleArt/Props, Cannon: 도트 판 그림 — 점 필터, 가장자리 고정, 압축 없음, 밉맵 끔, 투명 알파 (Effects 폴더와 같은 설정)
    // - Resources/BattleArt/Sky: horizon_* 먼 산 띠는 가로 거울 반복(이음새 감춤)·밉맵 켬, sky_* 하늘 색 띠는 부드럽게(쌍선형)
    // 그림을 바꿔 넣어도 매번 같은 설정이 걸리게 한다 (메타 파일은 유니티가 만든다)
    public sealed class BattleFieldArtImport : AssetPostprocessor
    {
        const string PropsFolder = "/Resources/BattleArt/Props/";
        const string CannonFolder = "/Resources/BattleArt/Cannon/";
        const string SkyFolder = "/Resources/BattleArt/Sky/";
        const string TerrainFolder = "/Resources/BattleArt/Terrain/";   // 토벽·언덕·나무·바위 더미 전투 판 (K-049, Codex K-030)

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool sprite = path.Contains(PropsFolder) || path.Contains(CannonFolder) || path.Contains(TerrainFolder);
            bool sky = path.Contains(SkyFolder);
            if (!sprite && !sky) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;

            if (sprite)
            {
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                return;
            }

            bool horizon = path.Contains("/horizon_");
            importer.filterMode = horizon ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapModeU = horizon ? TextureWrapMode.Mirror : TextureWrapMode.Clamp;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.mipmapEnabled = horizon;   // 먼 거리에서 점 그림이 지글거리지 않게
        }
    }
}
