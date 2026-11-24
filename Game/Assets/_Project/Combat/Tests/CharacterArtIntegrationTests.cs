using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Combat.PerfLab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterArtIntegrationTests
{
    const string Data = "Assets/_Project/Data/Combat/";

    [TestCase("unit_hero", 1152)]
    [TestCase("unit_wizard", 288)]
    [TestCase("unit_archer", 288)]
    [TestCase("unit_gunner", 288)]
    [TestCase("unit_cannon_crew", 288)]
    public void InstalledUnitUsesNewSheetAndMatchingNormals(string name, int width)
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitDef>(Data + "Units/" + name + ".asset");
        Assert.AreEqual(width, unit.Sheet.width);
        foreach (var sheet in new[] { unit.Sheet, unit.IdleSheet, unit.RunSheet, unit.FireSheet, unit.DeathSheet,
            unit.CastSheet, unit.CommandSheet, unit.IgniteSheet, unit.FrontSheet })
        {
            if (sheet == null) continue;
            Texture2D normal = unit.NormalFor(sheet);
            Assert.IsNotNull(normal, name + ": " + sheet.name + " 짝 노멀");
            Assert.AreEqual(sheet.width, normal.width); Assert.AreEqual(sheet.height, normal.height);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
            Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType);
            Assert.IsFalse(importer.sRGBTexture);
        }
        if (unit.Kind == UnitKind.CannonCrew)
        {
            Assert.AreEqual(2592, unit.IgniteSheet.width, "최신 점화 여백 유지");
            Assert.AreEqual(6, PerfLabArt.FrameCount(unit.IgniteSheet));
        }
    }

    // K-109 루멘 A안: 전투 시트는 288×384 칸, 색·노멀·광택 마스크가 같은 크기 (D-081)
    [TestCase("Art/Characters/Hero/hero_walk_run_grid", 1152, 1536, 16)]
    [TestCase("Art/Characters/Hero/hero_back_idle", 288, 384, 1)]
    [TestCase("Art/Characters/Hero/hero_command", 864, 384, 3)]
    public void HeroSheetsAre288CellsWithMatchingNormalAndSpec(string path, int width, int height, int frames)
    {
        const string Root = "Assets/_Project/";
        var color = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + path + ".png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + path + "_normal.png");
        var spec = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Battle/Resources/BattleArt/Spec/" + System.IO.Path.GetFileName(path) + "_spec.png");
        Assert.AreEqual(width, color.width); Assert.AreEqual(height, color.height);
        Assert.AreEqual(frames, PerfLabArt.FrameCount(color));
        foreach (var other in new[] { normal, spec })
        {
            Assert.IsNotNull(other, path);
            Assert.AreEqual(width, other.width); Assert.AreEqual(height, other.height);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(Root + path + ".png");
        Assert.AreEqual(FilterMode.Point, importer.filterMode);
        Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
        Assert.IsFalse(importer.mipmapEnabled);
    }

    [TestCase("neutral")] [TestCase("determined")] [TestCase("surprised")] [TestCase("tired")]
    public void HeroPortraitsAre256x384(string mood)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Story/Resources/StoryArt/portrait_hero_" + mood + ".png");
        Assert.AreEqual(256, tex.width); Assert.AreEqual(384, tex.height);
    }

    // K-111: 화로 빛 3단계(576×576)와 먼 전장 안개 띠(1536×384, 가로 반복) — 점 필터·압축 없음·밉맵 끔
    [TestCase("brazier_glow_low", 576, 576, TextureWrapMode.Clamp)]
    [TestCase("brazier_glow_normal", 576, 576, TextureWrapMode.Clamp)]
    [TestCase("brazier_glow_high", 576, 576, TextureWrapMode.Clamp)]
    [TestCase("fog_band", 1536, 384, TextureWrapMode.Repeat)]
    public void K111GlowAndFogBandSizesAndImport(string name, int width, int height, TextureWrapMode wrapU)
    {
        string path = "Assets/_Project/Art/Battle/Resources/BattleArt/Props/" + name + ".png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.IsNotNull(tex, name);
        Assert.AreEqual(width, tex.width); Assert.AreEqual(height, tex.height);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.AreEqual(FilterMode.Point, importer.filterMode);
        Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
        Assert.AreEqual(name == "fog_band", importer.mipmapEnabled, "안개 띠만 밉맵 켬");
        Assert.AreEqual(wrapU, importer.wrapModeU);
    }

    // K-111 하겐: 입이 있는 4표정 시트 256×384 칸 4개 (이야기 화면 portrait_hagen_neutral 한 파일)
    [Test]
    public void HagenStorySheetHasFourExpressionCells()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Story/Resources/StoryArt/portrait_hagen_neutral.png");
        Assert.AreEqual(1024, tex.width); Assert.AreEqual(384, tex.height);
    }

    [TestCase("enemy_goblin")]
    [TestCase("enemy_goblin_ladder")]
    [TestCase("enemy_ogre")]
    public void EnemyActionsAndLadderCarryHaveMatchingNormals(string name)
    {
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(Data + "Enemies/" + name + ".asset");
        foreach (var color in new[] { enemy.Sheet, enemy.MovingSheet, enemy.AttackSheet, enemy.DeathSheet,
            enemy.DeploySheet, enemy.ClimbSheet, enemy.LadderSheet, enemy.LadderSideSheet })
            if (color != null) Assert.IsNotNull(enemy.NormalFor(color), name + ": " + color.name);
        if (enemy.CarriesLadder) Assert.AreEqual("ladder_goblin_carry_run4", enemy.MovingSheet.name);
        if (name == "enemy_goblin") Assert.AreEqual("goblin_wallhit4", enemy.AttackSheet.name);
    }

    [Test]
    public void MismatchedOldNormalIsRejected()
    {
        var color = new Texture2D(288, 384); var normal = new Texture2D(256, 384);
        try { Assert.IsNull(SheetNormalPair.Match(color, normal)); }
        finally { Object.DestroyImmediate(color); Object.DestroyImmediate(normal); }
    }
}
