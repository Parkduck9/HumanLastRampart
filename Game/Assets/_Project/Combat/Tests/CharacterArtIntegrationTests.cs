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
