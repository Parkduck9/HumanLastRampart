using System;
using System.IO;
using System.Text;
using HumanLastRampart.UI;
using NUnit.Framework;
using UnityEngine;

// W34 K-123 UI 그림 시험: manifest의 모든 그림이 Resources/UiKit에서 불러와지고, 9칸 경계가 manifest 값과 같으며, 단추 상태 그림이 연결되는지 본다
public class UiKitArtTests
{
    [Serializable] class GameInfo { public string path; public int[] size_px; public int[] nine_slice_lbrt_px; }
    [Serializable] class AssetInfo { public string id; public GameInfo game; }
    [Serializable] class Manifest { public AssetInfo[] assets; }

    static Manifest Load()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/References/codex_k123_v1/manifest.json"));
        Assert.IsTrue(File.Exists(path), "manifest.json 없음: " + path);
        return JsonUtility.FromJson<Manifest>(File.ReadAllText(path, Encoding.UTF8));
    }

    [Test]
    public void EveryManifestSpriteLoadsWithNineSliceBorder()
    {
        Manifest manifest = Load();
        Assert.AreEqual(99, manifest.assets.Length, "manifest 그림 수");
        foreach (AssetInfo asset in manifest.assets)
        {
            var texture = Resources.Load<Texture2D>("UiKit/" + asset.id);
            Assert.IsNotNull(texture, asset.id + ": Resources/UiKit 그림 없음");
            Assert.AreEqual(asset.game.size_px[0], texture.width, asset.id + " 너비");
            Assert.AreEqual(asset.game.size_px[1], texture.height, asset.id + " 높이");
            Assert.AreEqual(1, texture.mipmapCount, asset.id + ": 밉맵 없어야 함");
            Sprite sprite = UiArt.Get(asset.id);
            Assert.IsNotNull(sprite, asset.id + ": 스프라이트 만들기 실패");
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, asset.id + ": 점 필터");
            int[] b = asset.game.nine_slice_lbrt_px;
            Assert.AreEqual(new Vector4(b[0], b[1], b[2], b[3]), sprite.border, asset.id + ": 9칸 경계 (왼쪽, 아래, 오른쪽, 위)");
            Assert.AreEqual(texture.width, UiArt.Size(sprite).x, 0.01f, asset.id + ": 3배 그림은 다시 3배 하지 않는다");
        }
    }

    [Test]
    public void ButtonsGetEveryStateArt()
    {
        var root = new GameObject("UiKitArtTest", typeof(RectTransform));
        try
        {
            // 종류별 상태 그림: 보통·주 단추는 전체 묶음, 탭은 꺼짐/켜짐(눌림 = 켜짐), 아이콘 단추는 틀 하나
            var normal = Ui.Button(root.transform, "n", "시험", null, UiButtonKind.Normal).GetComponent<UiButtonFeel>();
            Assert.AreEqual(UiArt.ButtonWood, normal.Normal.name);
            Assert.AreEqual(UiArt.ButtonWoodHover, normal.Hover.name);
            Assert.AreEqual(UiArt.ButtonWoodDown, normal.Down.name);
            Assert.AreEqual(UiArt.ButtonWoodDisabled, normal.Disabled.name);
            Assert.AreEqual(UiArt.ButtonWoodOn, normal.OnSprite.name);
            var primary = Ui.Button(root.transform, "p", "시험", null, UiButtonKind.Primary).GetComponent<UiButtonFeel>();
            Assert.AreEqual(UiArt.ButtonBrass, primary.Normal.name);
            Assert.AreEqual(UiArt.ButtonBrassHover, primary.Hover.name);
            Assert.AreEqual(UiArt.ButtonBrassDown, primary.Down.name);
            Assert.AreEqual(UiArt.ButtonBrassDisabled, primary.Disabled.name);
            Assert.AreEqual(UiArt.ButtonBrassOn, primary.OnSprite.name);

            var tab = Ui.Button(root.transform, "tab", "탭", null, UiButtonKind.Tab).GetComponent<UiButtonFeel>();
            Assert.AreEqual(UiArt.TabOff, tab.Normal.name);
            Assert.AreEqual(UiArt.TabOn, tab.OnSprite.name);
            Assert.AreEqual(UiArt.TabOn, tab.Down.name);
            Assert.IsNull(tab.Hover, "탭 올림 그림은 없다 (빛 테두리)");
            Assert.IsNull(tab.Disabled, "탭 꺼짐 그림은 없다 (어둡게)");

            var icon = Ui.Button(root.transform, "i", null, null, UiButtonKind.Icon, UiArt.IconClose).GetComponent<UiButtonFeel>();
            Assert.AreEqual(UiArt.IconButtonFrame, icon.Normal.name);
            Assert.IsNull(icon.Hover);
            Assert.IsNull(icon.Down);
            Assert.IsNull(icon.Disabled);
            Assert.IsNull(icon.OnSprite);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void FortressWallPicksHpStage()
    {
        Assert.AreEqual("fortress_wall_north_intact", UiArt.FortressWall("north", 1f));
        Assert.AreEqual("fortress_wall_east_damaged", UiArt.FortressWall("east", 0.5f));
        Assert.AreEqual("fortress_wall_south_critical", UiArt.FortressWall("south", 0.1f));
        Assert.AreEqual("fortress_wall_west_broken", UiArt.FortressWall("west", 0f));
        foreach (string side in new[] { "north", "east", "south", "west" })
            foreach (float ratio in new[] { 1f, 0.5f, 0.1f, 0f })
                Assert.IsNotNull(UiArt.Get(UiArt.FortressWall(side, ratio)), side + " " + ratio);
    }
}
