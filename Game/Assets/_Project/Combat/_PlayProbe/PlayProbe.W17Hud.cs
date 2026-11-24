using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// W17 HUD 위치 확인용 사진 (알림 목록, 아래 계기 띠, 지도, 계단, 탑). 결과: HLR_PLAY_OUT/NN_w17_*.png
public sealed partial class PlayProbe
{
    // 화면 비율(0~1) 사각형. 숨겨졌거나 없으면 빈 사각형
    static Rect Rect01(string objectName)
    {
        foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
        {
            if (rt.name != objectName || !rt.gameObject.activeInHierarchy) continue;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
            float w = Mathf.Max(1f, ((RectTransform)canvas.transform).rect.width * canvas.transform.lossyScale.x);
            float h = Mathf.Max(1f, ((RectTransform)canvas.transform).rect.height * canvas.transform.lossyScale.y);
            return Rect.MinMaxRect(c[0].x / w, c[0].y / h, c[2].x / w, c[2].y / h);
        }

        Assert.Fail("화면 요소 없음: " + objectName);
        return default;
    }

    [UnityTest]
    public IEnumerator W17HudShots()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        if (!Application.isBatchMode) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
        try
        {
            for (int i = 0; i < 10; i++) yield return null;
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleSim sim = entry.Sim;
            BattleCameraRig rig = entry.Rig;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var hud = (BattleHud)typeof(BattleEntry).GetField("hud", flags).GetValue(entry);
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = TimeOfDay.Day; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            Debug.Log($"[W17] screen {Screen.width}x{Screen.height}");
            for (int i = 0; i < 20; i++) yield return null;
            yield return Capture("w17_0_prep", 1920, 1080);
            entry.StartBattle();
            for (int i = 0; i < 100; i++) yield return null;

            // 1) 알림 여러 개
            hud.PushNotice("북쪽 성벽에 전령을 보냈습니다");
            hud.PushNotice("동쪽 성벽으로 명령이 전달됩니다");
            hud.PushNotice("비가 내리기 시작합니다");
            hud.PushNotice("대포 재장전");
            hud.PushNotice("함정이 불발했습니다");
            hud.PushNotice("남쪽 성벽 병사가 없습니다");
            for (int i = 0; i < 8; i++) yield return null;
            yield return FreshHud();
            Canvas.ForceUpdateCanvases();
            var lines = (System.Collections.IList)typeof(BattleHud).GetField("noticeLines", flags).GetValue(hud);
            Assert.LessOrEqual(lines.Count, 5, "알림 목록은 최대 5줄");
            Assert.AreEqual("남쪽 성벽 병사가 없습니다", (string)lines[lines.Count - 1].GetType().GetField("Text").GetValue(lines[lines.Count - 1]), "마지막 줄 = 마지막으로 넣은 알림 (새 줄이 맨 아래)");
            Assert.IsNull(GameObject.Find("Danger Banner"), "가운데 위급 띠는 없다");
            Assert.IsTrue(Mathf.Abs(Rect01("Notice Slips").center.x - 1f) < 0.3f && Rect01("Notice Slips").center.y > 0.7f, "알림 목록은 오른쪽 위");
            Assert.IsTrue(Rect01("Status Band").center.y < 0.2f, "계기 띠는 아래");
            Assert.IsFalse(Rect01("Status Band").Overlaps(Rect01("Key Hints")), "띠와 키 안내가 겹치지 않는다");
            yield return Capture("w17_1_battle_notices", 1920, 1080);

            // 2) 지도
            entry.ToggleMap();
            for (int i = 0; i < 20; i++) yield return null;
            hud.PushNotice("북쪽 성벽에 전령을 보냈습니다");
            hud.PushNotice("동쪽 성벽으로 명령이 전달됩니다");
            hud.PushNotice("비가 내리기 시작합니다");
            yield return FreshHud();
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(Rect01("Fire Board").Overlaps(Rect01("Notice Slips")), $"대포 판과 알림 목록이 겹치지 않는다 {Rect01("Fire Board")} {Rect01("Notice Slips")} 줄 {lines.Count} 자식 {GameObject.Find("Notice Slips").transform.childCount}");
            Assert.IsFalse(Rect01("Fire Board").Overlaps(Rect01("Status Band")), "대포 판과 계기 띠가 겹치지 않는다");
            Assert.IsFalse(Rect01("Fire Board").Overlaps(Rect01("Key Hints")), "대포 판과 키 안내가 겹치지 않는다");
            Assert.IsFalse(Rect01("Command Board").Overlaps(Rect01("Key Hints")), "명령 판과 키 안내가 겹치지 않는다");
            yield return Capture("w17_2_map", 1920, 1080);
            entry.ToggleMap();
            for (int i = 0; i < 20; i++) yield return null;

            // 3) 계단 앞
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            for (int i = 0; i < 20; i++) yield return null;
            yield return FreshHud();
            yield return Capture("w17_3_stairs", 1920, 1080);
            Assert.IsTrue(entry.Interact(), "E: 탑으로 내려가기");
            // 내려가는 중 (3장)
            var cam = (Camera)typeof(BattleCameraRig).GetField("camera", flags).GetValue(rig);
            Time.captureDeltaTime = 0f;   // 사진기 따라가기는 실제 프레임 시간을 쓰므로 하강만 실제 시간으로
            float[] marks = { 0.2f, 0.45f, 0.7f };
            int mark = 0;
            for (int i = 0; i < 4000 && !rig.AtTower; i++)
            {
                yield return null;
                if (i % 10 == 0)
                {
                    Vector3 vp = cam.WorldToViewportPoint(rig.HeroFeet);
                    Assert.IsTrue(vp.y > 0f && vp.y < 1f && vp.x > 0f && vp.x < 1f, $"하강 중 발이 화면 안: ({vp.x:0.00},{vp.y:0.00})");
                    Note($"하강 {i}: blend {rig.TowerBlend:0.00} 발 z {rig.HeroFeet.z:0.0} 화면 ({vp.x * 1920:0},{(1f - vp.y) * 1080:0}) 사진기 z {cam.transform.position.z:0.0}");
                }

                if (mark < marks.Length && rig.TowerBlend >= marks[mark])
                {
                    yield return Capture("w17_4_down_" + mark, 1920, 1080);
                    mark++;
                }
            }

            Time.captureDeltaTime = 1f / 20f;
            yield return WaitFor(() => rig.AtTower, 40f);
            for (int i = 0; i < 20; i++) yield return null;
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(1.5f);
            entry.TutorialTimeScale = 1f;
            for (int i = 0; i < 2; i++) yield return null;
            yield return FreshHud();
            yield return Capture("w17_5_tower", 1920, 1080);
            entry.BeamMapStaySeconds = 99f;
            entry.Interact();
            for (int i = 0; i < 6; i++) yield return null;
            yield return FreshHud();
            yield return Capture("w17_6_tower_map", 1920, 1080);
            entry.CloseBeamOrder();
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "w17_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
