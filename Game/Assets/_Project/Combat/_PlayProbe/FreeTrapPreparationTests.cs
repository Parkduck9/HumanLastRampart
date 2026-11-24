using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Traps;
using HumanLastRampart.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// 공방 실제 입력 시험과 거래 시험이 같은 자유 좌표를 고른다. 칸 중앙에서 일부러 벗어난다.
static class FreeTrapTestSupport
{
    public static Vector2 Point(TrapPreparationTransactions session, string definitionId)
    {
        Vector2 first = new Vector2(.375f, session.Terrain.CastleHalfSize + 72.625f);
        if (session.CanPick(definitionId, TrapGrid.FreeId(first.x, first.y), null)) return first;
        for (float z = session.Terrain.CastleHalfSize + session.Terrain.TrapNear + .625f; z < session.Terrain.CastleHalfSize + session.Terrain.TrapFar; z += 2f)
            for (float x = -.375f; x < session.Terrain.CastleHalfSize; x += 2f)
            {
                var point = new Vector2(x, z);
                if (session.CanPick(definitionId, TrapGrid.FreeId(x, z), null)) return point;
            }
        Assert.Fail("함정 자유 배치에 쓸 빈 땅을 찾지 못했습니다");
        return default;
    }

    public static Vector2 MapPoint(TrapPreparationTransactions session, Vector2 point)
    {
        float extent = Mathf.Ceil((session.Terrain.CastleHalfSize + session.Terrain.TrapFar + 5f) / 5f) * 5f;
        return point * (880f / (extent * 2f));
    }

    public static Button BuyButton() => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
        .First(b => b.isActiveAndEnabled && (b.name == "한 번에 구매·설치" || b.name == "btn:한 번에 구매·설치"));
}

// CODE-04: 자유 좌표 거래, 기존 저장, 지형 겹침, 회수가 같은 실제 위치를 쓴다.
public sealed class FreeTrapPreparationTests
{
    sealed class Lookup : IDefinitionLookup
    {
        public readonly List<DefinitionBase> Values = new List<DefinitionBase>();
        public T Get<T>(string id) where T : DefinitionBase => Values.OfType<T>().FirstOrDefault(d => d.Id == id);
        public IReadOnlyList<T> All<T>() where T : DefinitionBase => Values.OfType<T>().ToList();
    }
    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    Lookup definitions;
    CampaignState campaign;
    TrapPreparationTransactions session;
    TerrainDef terrain;

    T Make<T>(string id, params (string name, object value)[] fields) where T : DefinitionBase
    {
        var def = ScriptableObject.CreateInstance<T>(); owned.Add(def);
        typeof(DefinitionBase).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(def, id);
        foreach (var field in fields) typeof(T).GetField(field.name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(def, field.value);
        definitions.Values.Add(def); return def;
    }

    [SetUp]
    public void Before()
    {
        definitions = new Lookup();
        terrain = Make<TerrainDef>("terrain_1", ("castleHalfSize", 20f));
        Make<TrapDef>("trap_bomb");
        Make<CampaignStartDef>("campaign_start", ("waveByBattle", new List<string> { "wave_1", "wave_2" }),
            ("terrainOrder", new List<string> { "terrain_1" }));
        campaign = new CampaignState { BattleNumber = 2, Gold = 1000, RngSeed = 1234, TrapShopRestockedForBattle = 2 };
        campaign.TrapShop.Add(new TrapStockEntry { TrapDefId = "trap_bomb", Count = 12 });
        campaign.TrapStock.Add(new TrapStockEntry { TrapDefId = "trap_bomb", Count = 2 });
        session = new TrapPreparationTransactions(campaign, definitions);
    }

    [TearDown]
    public void After() { foreach (var def in owned) UnityEngine.Object.DestroyImmediate(def); owned.Clear(); }

    string Id(float x, float z) => TrapGrid.FreeId(x, z);
    bool Buy(params string[] ids) => session.TryBuyMany("trap_bomb", ids, false, true, out _, out _, out _, out _);

    [Test]
    public void FreeCoordinate_IsExactAndMarked_WithoutCellSnap()
    {
        string id = Id(.375f, 40.625f);
        Assert.IsTrue(Buy(id));
        var trap = campaign.Traps.Single();
        Assert.AreEqual(id, trap.SlotId);
        Assert.AreEqual(.375f, trap.ActualX); Assert.AreEqual(40.625f, trap.ActualZ);
        Assert.AreEqual(trap.ActualX, trap.MarkX); Assert.AreEqual(trap.ActualZ, trap.MarkZ);
        Assert.AreEqual(975, campaign.Gold); Assert.AreEqual(11, session.ShopCount("trap_bomb"));
    }

    [Test]
    public void Band_IncludesFourSidesAndCorners_RejectsCastleAndOutside()
    {
        foreach (var p in new[] { new Vector2(0f, 30f), new Vector2(30f, 0f), new Vector2(0f, -30f), new Vector2(-30f, 0f), new Vector2(30f, 30f), new Vector2(100f, 100f) })
            Assert.IsTrue(TrapGrid.InBand(terrain, p), p.ToString());
        foreach (var p in new[] { Vector2.zero, new Vector2(29.99f, 0f), new Vector2(0f, 100.01f), new Vector2(float.NaN, 40f), new Vector2(float.PositiveInfinity, 0f) })
            Assert.IsFalse(TrapGrid.InBand(terrain, p), p.ToString());
    }

    [Test]
    public void OnlyTrapGround_IsPlaceable_RegardlessOfIdFormat()
    {
        Vector2 p = new Vector2(.375f, 40.625f);
        int cell = session.Layout.CellOf(p.x, p.y);
        foreach (byte kind in new[] { TerrainCell.EarthWall, TerrainCell.Hill, TerrainCell.Trees, TerrainCell.Rocks })
        {
            session.Layout.Cells[cell] = kind;
            Assert.IsFalse(session.IsPlaceable(Id(p.x, p.y)), "막힌 지형 " + kind);
        }
        foreach (byte kind in new[] { TerrainCell.Free, TerrainCell.Path })
        {
            session.Layout.Cells[cell] = kind;
            Assert.IsTrue(session.IsPlaceable(Id(p.x, p.y)), "함정 가능한 지형 " + kind);
        }
    }

    [Test]
    public void Spacing_UsesBlastRadiusAndActualPosition_ForExistingAndPicked()
    {
        Assert.IsTrue(Buy(Id(.375f, 40.625f)));
        Assert.IsFalse(session.CanPick("trap_bomb", Id(.375f, 50.625f), null), "반경 경계도 거절");
        Assert.IsTrue(session.CanPick("trap_bomb", Id(.375f, 50.75f), null));
        var picked = new[] { Id(.375f, 60.625f) };
        Assert.IsFalse(session.CanPick("trap_bomb", Id(.375f, 65.625f), picked));
        Assert.IsTrue(session.CanPick("trap_bomb", Id(.375f, 71f), picked));
        campaign.Traps[0].ActualZ = 70.625f;
        Assert.IsTrue(session.CanPick("trap_bomb", Id(.375f, 40.625f), null), "이동 전 위치는 비었다");
        Assert.IsFalse(session.CanPick("trap_bomb", Id(.375f, 70.625f), null), "실제 이동 위치를 점유");
    }

    [Test]
    public void Batch_IsPreflighted_NoPartialPaymentForOverlappingSelections()
    {
        Assert.IsFalse(Buy(Id(.375f, 40.625f), Id(.375f, 45.625f)));
        Assert.AreEqual(0, campaign.Traps.Count); Assert.AreEqual(1000, campaign.Gold); Assert.AreEqual(12, session.ShopCount("trap_bomb"));
        Assert.IsTrue(Buy(Id(.375f, 40.625f), Id(.375f, 60.875f)));
        Assert.AreEqual(2, campaign.Traps.Count); Assert.AreEqual(950, campaign.Gold); Assert.AreEqual(10, session.ShopCount("trap_bomb"));
    }

    [Test]
    public void StoredTrap_ReinstallsFreeWithoutGold_AndRecoveryStillFindsIt()
    {
        Assert.IsTrue(session.TryBuyMany("trap_bomb", new[] { Id(.375f, 40.625f) }, true, true,
            out int placed, out int spent, out string reason, out bool error), reason);
        Assert.IsFalse(error); Assert.AreEqual(1, placed); Assert.AreEqual(0, spent);
        Assert.AreEqual(1000, campaign.Gold); Assert.AreEqual(1, session.StoredCount("trap_bomb"));
        var order = new RecoveryOrder { RecoveryId = "recovery_1", TerrainId = terrain.Id, SquadId = "squad_1" };
        order.TargetTrapIds.Add(campaign.Traps[0].TrapId);
        var recovered = TrapRules.ResolveRecovery(order, campaign.Traps, terrain, definitions, 7);
        CollectionAssert.AreEqual(new[] { campaign.Traps[0].TrapId }, recovered.RecoveredTrapIds);
        Assert.AreEqual(0, recovered.ExplodedTrapIds.Count);
    }

    [Test]
    public void LegacyCellSave_WithNearbyAndMovedTraps_StillValidates()
    {
        campaign.Traps.Add(new TrapPlacement { TrapId = "old_1", TrapDefId = "trap_bomb", TerrainId = terrain.Id,
            SlotId = TrapGrid.CellId(0, 3, 4), ActualX = .5f, ActualZ = 40.5f, Marked = true, MarkX = .5f, MarkZ = 40.5f });
        campaign.Traps.Add(new TrapPlacement { TrapId = "old_2", TrapDefId = "trap_bomb", TerrainId = terrain.Id,
            SlotId = TrapGrid.CellId(0, 4, 4), ActualX = .5f, ActualZ = 45.5f, Marked = true, MarkX = .5f, MarkZ = 45.5f });
        var loaded = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(campaign));
        Assert.DoesNotThrow(() => new TrapPreparationTransactions(loaded, definitions));
        Assert.IsFalse(session.CanPick("trap_bomb", Id(.5f, 40.5f), null));
        Assert.IsTrue(TrapGrid.TryPosition(terrain, TrapGrid.CellId(0, 3, 4), out var center));
        Assert.AreEqual(TrapGrid.Center(terrain, 0, 3, 4), center);
    }

    [Test]
    public void TerrainWork_BlocksAtActualTrapCell_NotTheOriginalSlot()
    {
        Vector2 at = new Vector2(42.5f, 42.5f);
        int a = session.Layout.CellOf(at.x, at.y);
        session.Layout.Cells[a] = session.Layout.Cells[a + 1] = TerrainCell.Free;
        var works = new TerrainWorkTransactions(session);
        Assert.IsTrue(works.CanPlaceFree(TerrainWorkKind.EarthWall, at.x, at.y, 0f, null));
        campaign.Traps.Add(new TrapPlacement { TrapId = "old_1", TrapDefId = "trap_bomb", TerrainId = terrain.Id,
            SlotId = TrapGrid.CellId(0, 3, 4), ActualX = at.x, ActualZ = at.y });
        Assert.IsFalse(works.CanPlaceFree(TerrainWorkKind.EarthWall, at.x, at.y, 0f, null));
        campaign.Traps[0].ActualZ += 20f;
        Assert.IsTrue(works.CanPlaceFree(TerrainWorkKind.EarthWall, at.x, at.y, 0f, null));
    }

    [TestCase("free:NaN:40")]
    [TestCase("free:0:Infinity")]
    [TestCase("free:0:40:45")]
    [TestCase("free:0")]
    public void MalformedCoordinates_AreRejected(string id)
    {
        Assert.IsFalse(session.IsPlaceable(id));
        Assert.IsFalse(Buy(id));
        Assert.AreEqual(1000, campaign.Gold);
    }
}
