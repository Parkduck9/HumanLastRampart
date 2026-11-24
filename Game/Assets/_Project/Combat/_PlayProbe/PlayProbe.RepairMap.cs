using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Meta;
using HumanLastRampart.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class PlayProbe
{
    // 수리 지도 (W1, 사용자 2026-10-20 #16): 지도에서 성벽 조각을 눌러 고르고 +100 수리.
    // 옛 탭 때와 같은 결과(체력·골드)인지, 체력 색 단계가 기준(70/40/15%)과 맞는지, 사진 한 장(prep_repair_map)
    [UnityTest]
    public IEnumerator PrepRepairMap()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        try
        {
            // 단계 경계 (순수 규칙)
            Assert.AreEqual(RepairWallState.Normal, RepairWallLook.StateOf(70, 100));
            Assert.AreEqual(RepairWallState.Cracked, RepairWallLook.StateOf(69, 100));
            Assert.AreEqual(RepairWallState.Cracked, RepairWallLook.StateOf(40, 100));
            Assert.AreEqual(RepairWallState.Heavy, RepairWallLook.StateOf(39, 100));
            Assert.AreEqual(RepairWallState.Heavy, RepairWallLook.StateOf(15, 100));
            Assert.AreEqual(RepairWallState.Breach, RepairWallLook.StateOf(14, 100));
            Assert.AreEqual(RepairWallState.Breach, RepairWallLook.StateOf(0, 100));
            Assert.AreEqual(RepairWallState.Normal, RepairWallLook.StateOf(100, 100));

            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Click("게임 시작", 600);
            yield return Click("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            yield return Click("넘기기", 600);
            yield return WaitScreen(ScreenId.Battle, 900);
            yield return Click("[시험] 승리", 300);
            yield return WaitScreen(ScreenId.Result, 900);
            yield return Click("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);

            var c = GameFlow.Instance.Campaign;
            c.Gold = System.Math.Max(c.Gold, 1000);
            // 북 90%(튼튼) / 동 60%(금) / 남 30%(크게) / 서 10%(무너짐)
            float[] ratio = { 0.9f, 0.6f, 0.3f, 0.1f };
            RepairWallState[] expect = { RepairWallState.Normal, RepairWallState.Cracked, RepairWallState.Heavy, RepairWallState.Breach };
            for (int i = 0; i < 4; i++)
            {
                var w = c.Wall.First(x => x.SegmentId == WallIds.All[i]);
                w.Hp = (int)(w.MaxHp * ratio[i]);
                Assert.Greater(w.MaxHp - w.Hp, 100, "시험은 100HP 이상 손상이 필요하다");
                Assert.AreEqual(expect[i], RepairWallLook.StateOf(w.Hp, w.MaxHp), WallIds.All[i]);
            }

            yield return Click("성벽 수리", 300);
            for (int i = 0; i < 4; i++)
            {
                var w = c.Wall.First(x => x.SegmentId == WallIds.All[i]);
                var piece = FindReady("성벽맵:" + WallIds.All[i]);
                Assert.IsNotNull(piece, "지도 조각 " + WallIds.All[i]);
                var image = piece.GetComponent<Image>();
                Color tint = RepairWallLook.Tint(expect[i]);
                Assert.IsTrue(image.sprite != null || Approx(image.color, tint), $"{WallIds.All[i]} 체력 색 단계 {expect[i]}");
                // K-096 + K-091 v2 그림이 실제로 읽혔는지 (조각 그림·외곽선·성 그림)
                Assert.IsNotNull(image.sprite, $"{WallIds.All[i]} {expect[i]} 성벽 그림 로드");
                Assert.IsNotNull(Resources.Load<Texture2D>("HudArt/" + RepairWallLook.WallSlot(WallIds.All[i], expect[i])), "성벽 패치 파일");
            }
            foreach (string id in WallIds.All)
                foreach (RepairWallState s in new[] { RepairWallState.Normal, RepairWallState.Cracked, RepairWallState.Heavy, RepairWallState.Breach })
                    Assert.IsNotNull(Resources.Load<Texture2D>("HudArt/" + RepairWallLook.WallSlot(id, s)), $"패치 {id} {s}");
            Assert.IsNotNull(UiArt.Hud(RepairWallLook.KeepSlot), "성 전체 그림");
            Assert.IsNotNull(UiArt.Hud(RepairWallLook.OutlineSlot(WallIds.North, true)), "선택 외곽선");
            Assert.IsNotNull(UiArt.Hud(RepairWallLook.BarSlot), "체력 막대 프레임");
            Assert.IsNotNull(GameObject.Find("Outline:selected:" + WallIds.North), "외곽선 층");

            // 사진: 동쪽(금 간 성벽)을 골라 지도·막대·수리 뒤 그림자가 한 장에
            yield return Click("성벽맵:" + WallIds.East, 60);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("prep_repair_map");

            // 네 성벽 모두 +100: 옛 탭 때와 같은 체력·골드 (100HP = 10골드)
            for (int i = 0; i < 4; i++)
            {
                string id = WallIds.All[i];
                int hp0 = c.Wall.First(x => x.SegmentId == id).Hp, gold0 = c.Gold;
                yield return Click("성벽맵:" + id, 60);
                yield return Click("수리:100", 60);
                yield return Click("확정 수리", 60);
                var after = c.Wall.First(x => x.SegmentId == id);
                Assert.AreEqual(hp0 + 100, after.Hp, id + " +100HP");
                Assert.AreEqual(gold0 - 10, c.Gold, id + " 100HP = 10골드");
                for (int k = 0; k < 3; k++) yield return null;
            }

            Note("수리 지도: 네 성벽 단계 색, 조각 선택 + 100 수리 체력·골드 통과");
            yield return Click("닫기", 60);
        }
        finally
        {
            Time.captureDeltaTime = 0f;
            File.WriteAllText(Path.Combine(Out, "prep_repair_map.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }

    static bool Approx(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
}
