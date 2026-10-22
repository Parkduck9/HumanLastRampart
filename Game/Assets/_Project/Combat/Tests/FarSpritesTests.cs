using System.IO;
using System.Text.RegularExpressions;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Combat.Render;
using HumanLastRampart.Core.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 먼 적 저비용 그림 연결 (W4b, K-099): 시트가 Resources에서 찾아지고 manifest.json 칸 크기와 같은지, 원래 시트와 짝이 맞는지
public sealed class FarSpritesTests
{
    const string FarDir = "Assets/_Project/Art/Battle/Resources/BattleArt/Far/";
    static readonly string[] Species = { "goblin", "ladder_goblin", "ladder_carry", "ogre" };

    static string Manifest()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/References/codex_k099_v1/manifest.json"));
        Assert.IsTrue(File.Exists(path), "manifest.json: " + path);
        return File.ReadAllText(path);
    }

    // "id": "<id>" 다음 첫 "<key>": [a, b]
    static Vector2Int Pair(string manifest, string id, string key)
    {
        var match = Regex.Match(manifest, "\"id\": \"" + id + "\"[\\s\\S]*?\"" + key + "\": \\[\\s*(\\d+),\\s*(\\d+)\\s*\\]");
        Assert.IsTrue(match.Success, id + "." + key);
        return new Vector2Int(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }

    [Test]
    public void FarSheetsResolveAndMatchManifest()
    {
        string manifest = Manifest();
        foreach (string species in Species)
            foreach (string tier in new[] { "half", "quarter" })
            {
                string id = species + "_" + tier;
                var texture = Resources.Load<Texture2D>(FarSprites.Folder + id);
                Assert.IsNotNull(texture, id + ": Resources/BattleArt/Far 에서 못 찾음");
                Vector2Int size = Pair(manifest, id, "size"), frame = Pair(manifest, id, "frame_size"), grid = Pair(manifest, id, "grid");
                Assert.AreEqual(size.x, texture.width, id); Assert.AreEqual(size.y, texture.height, id);
                Assert.AreEqual(frame.x, texture.width / grid.x, id + " 칸 가로"); Assert.AreEqual(frame.y, texture.height / grid.y, id + " 칸 세로");
                if (tier == "quarter") Assert.AreEqual(new Vector2Int(FarSprites.QuarterColumns, 1), grid, id + " 1/4 시트는 2×1");

                var importer = (TextureImporter)AssetImporter.GetAtPath(FarDir + id + ".png");
                Assert.IsNotNull(importer, id);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, id);
                Assert.IsFalse(importer.mipmapEnabled, id);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, id);
            }
    }

    [Test]
    public void RealEnemyMovingSheetsPairWithFarSheets()
    {
        int paired = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyDef"))
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(AssetDatabase.GUIDToAssetPath(guid));
            Texture2D near = enemy.MovingSheet;
            if (near == null || FarSprites.SpeciesOf(near) == null) continue;
            Assert.IsTrue(PerfLabArt.TryFarSheets(near, out Texture2D half, out Texture2D quarter), near.name + ": 먼 그림 짝");
            PerfLabArt.SheetLayout nearLayout = PerfLabArt.FromSheet(near);
            PerfLabArt.SheetLayout halfLayout = PerfLabArt.FarLayout(near, false), quarterLayout = PerfLabArt.FarLayout(near, true);
            // 절반 = 열·행·발·가로세로비 모두 같음, 1/4 = 2×1 이고 나머지 같음 → 월드 크기·발 위치 불변
            Assert.AreEqual(nearLayout.Grid, halfLayout.Grid);
            Assert.AreEqual(new Vector4(FarSprites.QuarterColumns, 1f, 0f, 0f), quarterLayout.Grid);
            Assert.AreEqual(nearLayout.Pivot, quarterLayout.Pivot); Assert.AreEqual(nearLayout.Aspect, quarterLayout.Aspect);
            Assert.AreEqual((float)(half.width / (int)halfLayout.Grid.x) / (half.height / (int)halfLayout.Grid.y), halfLayout.Aspect, 1e-4f, near.name + " 절반 칸 비율");
            Assert.AreEqual((float)(quarter.width / FarSprites.QuarterColumns) / quarter.height, quarterLayout.Aspect, 1e-4f, near.name + " 1/4 칸 비율");
            paired++;
        }

        Assert.GreaterOrEqual(paired, 3, "고블린·사다리 고블린·오우거");
    }

    [Test]
    public void MissingOrMismatchedFarSheetsFallBackToNull()
    {
        var other = new Texture2D(16, 16) { name = "not_an_enemy_sheet" };
        var rightSize = new Texture2D(1152, 768) { name = "goblin_walk_attack4" };   // 실제 고블린 시트와 같은 크기 (내용은 비어 있어도 짝은 크기·이름으로 정해진다)
        var wrongSize2 = new Texture2D(1000, 700) { name = "goblin_walk_attack4" };
        try
        {
            Assert.IsFalse(PerfLabArt.TryFarSheets(other, out _, out _));
            Assert.IsFalse(PerfLabArt.TryFarSheets(null, out _, out _));
            Assert.IsFalse(PerfLabArt.TryFarSheets(wrongSize2, out _, out _), "원래 크기가 다르면 짝이 안 맞는다");
            Assert.IsTrue(PerfLabArt.TryFarSheets(rightSize, out _, out _));
        }
        finally
        {
            Object.DestroyImmediate(other); Object.DestroyImmediate(rightSize); Object.DestroyImmediate(wrongSize2);
        }
    }

    [Test]
    public void BandsAreContiguousAndQuarterFrameMapIsTwoPoses()
    {
        Assert.AreEqual(0f, FarSprites.NearBand.x); Assert.AreEqual(FarSprites.HalfBand.x, FarSprites.NearBand.y);
        Assert.AreEqual(FarSprites.QuarterBand.x, FarSprites.HalfBand.y);
        Assert.AreEqual(FarSprites.QuarterBand.x, 110f); Assert.AreEqual(FarSprites.HalfBand.x, 60f);
        Assert.AreEqual(FarSprites.Off.w, 0f, "꺼짐 = 거리 띠 없음");
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, new[] { FarSprites.QuarterFrame(0), FarSprites.QuarterFrame(1), FarSprites.QuarterFrame(2), FarSprites.QuarterFrame(3) });
        Assert.AreEqual(1, FarSprites.QuarterFrame(7), "공격 칸(4~7)도 걷기 자세로");
        var sprite = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Combat/PerfLab/PerfLabSprite.shader");
        Assert.GreaterOrEqual(sprite.FindPropertyIndex("_LodBand"), 0);
    }
}
