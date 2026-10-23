using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Editor
{
    // 등대 탑·마당 그림 가져오기 설정 (W27 — Codex K-115 등대탑, K-116 안마당)
    // - Resources/BattleArt/Tower, Courtyard 안의 K-115/116 새 그림만 (옛 마당 그림 courtyard_cobble·crate_wood·barrel_wood는 건드리지 않는다)
    // - 기본: 점 필터, 가장자리 고정, 압축 없음, 밉맵 끔, NPOT 그대로 (32화소 = 1m)
    // - 반복 재질(석벽·지붕·바닥)은 양방향 Repeat + 밉맵 켬(멀리서 지글거리지 않게), 띠는 가로만 반복, 계단 옆돌·통 옆면은 가로만 반복
    // - 읽기 가능: 문(열린 문 구멍을 실행 중 뚫는다)·데칼·소품 그림(실행 중 한 장으로 합친다)
    // - 번짐 그림(lamp_interior_glow)만 부드러운 필터
    // 그림을 바꿔 넣어도 매번 같은 설정이 걸리게 한다 (메타 파일은 유니티가 만든다)
    public sealed class BattleTowerArtImport : AssetPostprocessor
    {
        const string TowerFolder = "/Resources/BattleArt/Tower/";
        const string CourtyardFolder = "/Resources/BattleArt/Courtyard/";

        static readonly string[] BothRepeat = { "tower_wall_albedo", "tower_roof_albedo", "courtyard_cobble_512", "road_paving" };
        static readonly string[] RepeatX = { "tower_trim_albedo", "stair_flank_128x64" };
        static readonly string[] Readable = { "door_dead", "door_lit", "decal_dirt", "decal_puddle", "decal_straw", "decal_cracked", "weapon_rack", "cart", "arrow_bundles" };
        static readonly string[] K116Names =
        {
            "courtyard_cobble_512", "road_paving", "decal_dirt", "decal_puddle", "decal_straw", "decal_cracked", "props_box_atlas",
            "weapon_rack", "cart", "arrow_bundles", "stair_step_atlas_64x16", "stair_flank_128x64",
        };

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool tower = path.Contains(TowerFolder);
            bool courtyard = path.Contains(CourtyardFolder);
            if (!tower && !courtyard) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (courtyard && System.Array.IndexOf(K116Names, name) < 0) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.filterMode = name == "lamp_interior_glow" ? FilterMode.Bilinear : FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = System.Array.IndexOf(Readable, name) >= 0;

            if (System.Array.IndexOf(BothRepeat, name) >= 0)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;   // 먼 곳에서 반복 무늬가 지글거리지 않게
                importer.alphaSource = TextureImporterAlphaSource.None;
            }
            else if (System.Array.IndexOf(RepeatX, name) >= 0)
            {
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.mipmapEnabled = name == "tower_trim_albedo";   // 띠는 멀리서도 보이므로 밉맵, 나머지는 가까이서만
                if (name == "tower_trim_albedo") importer.alphaSource = TextureImporterAlphaSource.None;
            }
        }
    }
}
