using System.Collections.Generic;
using System.IO;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Combat.Render;
using HumanLastRampart.Core.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 반사 마스크·접촉 그림자 연결 (W3, K-092·K-098)
public sealed class SpecMaskTests
{
    const string SpecDir = "Assets/_Project/Art/Battle/Resources/BattleArt/Spec/";

    static IEnumerable<Texture2D> CharacterSheets()
    {
        var seen = new HashSet<Texture2D>();
        foreach (string guid in AssetDatabase.FindAssets("t:UnitDef"))
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDef>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var s in new[] { unit.Sheet, unit.IdleSheet, unit.RunSheet, unit.FireSheet, unit.DeathSheet, unit.CastSheet,
                unit.CommandSheet, unit.IgniteSheet, unit.FrontSheet })
                if (s != null && seen.Add(s)) yield return s;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:EnemyDef"))
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var s in new[] { enemy.Sheet, enemy.MovingSheet, enemy.AttackSheet, enemy.DeathSheet, enemy.DeploySheet,
                enemy.ClimbSheet, enemy.LadderSheet, enemy.LadderSideSheet })
                if (s != null && seen.Add(s)) yield return s;
        }
    }

    [Test]
    public void EveryCharacterSheetWithAMaskResolvesIt()
    {
        int found = 0;
        foreach (Texture2D sheet in CharacterSheets())
        {
            bool fileExists = File.Exists(SpecDir + sheet.name + PerfLabArt.SpecSuffix + ".png");
            Texture2D mask = PerfLabArt.SpecFor(sheet);
            if (!fileExists) { Assert.IsNull(mask, sheet.name + ": 마스크 파일이 없으면 null"); continue; }
            Assert.IsNotNull(mask, sheet.name + ": 마스크 파일이 있는데 못 찾았거나 크기가 다름");
            Assert.AreEqual(sheet.width, mask.width, sheet.name); Assert.AreEqual(sheet.height, mask.height, sheet.name);
            found++;
        }

        Assert.GreaterOrEqual(found, 12, "인물·사다리 시트 대부분에 마스크가 있어야 한다 (K-092)");
    }

    [TestCase("BattleArt/Cannon/cannon_recoil")]
    [TestCase("BattleArt/Cannon/crew_pull6")]
    [TestCase("BattleArt/Cannon/crew_push6")]
    [TestCase("BattleArt/Cannon/haul_cannon_body")]
    [TestCase("BattleArt/Cannon/haul_cannon_chassis")]
    [TestCase("BattleArt/Cannon/haul_cannon_wheel")]
    [TestCase("BattleArt/cannon_aim3")]
    public void CannonSheetsResolveTheirMask(string path)
    {
        var sheet = Resources.Load<Texture2D>(path);
        Assert.IsNotNull(sheet, path);
        Texture2D mask = PerfLabArt.SpecFor(sheet);
        Assert.IsNotNull(mask, path + " 반사 마스크");
        Assert.AreEqual(sheet.width, mask.width); Assert.AreEqual(sheet.height, mask.height);
    }

    [Test]
    public void MasksAreLinearUncompressedPoint()
    {
        foreach (string file in Directory.GetFiles(SpecDir, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            Assert.IsNotNull(importer, file);
            Assert.IsFalse(importer.sRGBTexture, file + ": 마스크는 sRGB 끔");
            Assert.AreEqual(TextureImporterType.Default, importer.textureType, file);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, file);
            Assert.IsFalse(importer.mipmapEnabled, file);
        }
    }

    [Test]
    public void SizeMismatchedMaskIsRejected()
    {
        var sheet = Resources.Load<Texture2D>("BattleArt/Cannon/cannon_recoil");
        var fake = new Texture2D(sheet.width + 1, sheet.height) { name = sheet.name };
        try { Assert.IsNull(PerfLabArt.SpecFor(fake)); }
        finally { Object.DestroyImmediate(fake); }
    }

    [Test]
    public void ShaderDeclaresSpecAndShadowAssetsLoad()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Combat/PerfLab/PerfLabSprite.shader");
        Assert.IsNotNull(sprite);
        Assert.GreaterOrEqual(sprite.FindPropertyIndex("_SpecTex"), 0);
        Assert.GreaterOrEqual(sprite.FindPropertyIndex("_HasSpec"), 0);
        Assert.IsNotNull(Resources.Load<Shader>(ContactShadows.ShaderName), "접촉 그림자 셰이더");
        Assert.IsNotNull(Resources.Load<Texture2D>(ContactShadows.TexturePath), "접촉 그림자 타원");
    }
}
