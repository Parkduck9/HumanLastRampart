using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Settings;
using HumanLastRampart.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// W36: 정비 마당 등실 불빛(복구 0 / 중간 / 가득 / 점화의 밤)과 설정 창 '움직임 줄이기' 사진·검사
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator PrepYardLampStates()
    {
        Directory.CreateDirectory(Out); log = new StringBuilder();
        bool keep = KeepPreparation; KeepPreparation = true;
        try
        {
            SceneManager.LoadScene("Boot"); yield return null; yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600); yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>().Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            yield return Press("[시험] 승리", 300);
            yield return WaitScreen(ScreenId.Result, 900); yield return Press("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            var yard = Object.FindAnyObjectByType<PrepYard>();
            Assert.IsNotNull(yard);
            for (int i = 0; i < 15; i++) yield return null;
            Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1920, 960))), "처음 서는 자리처럼 탑이 가운데 오는 자리");
            CampaignState campaign = GameFlow.Instance.Campaign;
            campaign.Gold = 99999; campaign.BeaconRepairBoughtToday = 0;
            foreach (WallSegmentState wall in campaign.Wall) wall.Hp = Mathf.Max(1, wall.MaxHp / 2);   // 성벽 90% 미만: 점화의 밤이 아닌 상태
            campaign.BeaconRepair = 0f; yard.RefreshTower();
            Assert.AreEqual(0f, yard.LampLightNow, "복구 0 = 꺼짐");
            yield return PrepShots(yard, "w36_prep_0_dead");

            // 수리 단추를 누르면 마당 불빛이 바로 바뀐다 (refreshTower). 세부 칸을 연 채로도 등실이 보여야 한다
            yield return Click("등대 세부", 120);   // 얇은 띠의 접이식 칸을 연다 (밝기·수리 단추가 그 안에 있다)
            yield return Click("등대 수리", 120);
            Assert.AreEqual(PrepYard.LampLight(BeaconRules.DayRepairAmount, false), yard.LampLightNow, 0.0001f, "수리를 사면 같은 프레임에 불빛이 켜진다");
            Assert.Greater(yard.LampLightNow, 0f);
            yield return Framed(yard, "w36_prep_detail_open_after_buy", 1920, 1080, true);
            yield return Framed(yard, "w36_prep_detail_open_after_buy_16x10", 1920, 1200, true);
            yield return Click("등대 세부", 120);   // 다시 접는다
            Assert.IsNull(GameObject.Find("Beacon Detail"), "접힌 칸은 숨김 상태 (Find는 켜진 것만 찾는다)");

            // 칸이 접혀 있어도 위쪽 한 줄 요약은 바뀐 값을 따라간다
            campaign.BeaconRepair = 40f; yard.RefreshTower();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(GameObject.Find("Beacon Brief").GetComponent<UnityEngine.UI.Text>().text.Contains("40%"), "접힌 채로 한 줄 요약 갱신");
            Assert.IsFalse(yard.LampIgnitionNow);
            yield return PrepShots(yard, "w36_prep_1_mid");
            campaign.BeaconRepair = 100f; yard.RefreshTower();
            Assert.IsFalse(yard.LampIgnitionNow, "성벽이 낮으면 복구가 가득이어도 점화의 밤이 아니다");
            yield return PrepShots(yard, "w36_prep_2_full");

            // 성벽도 채우면 점화의 밤: 마당이 스스로 알아챈다 (Update가 상태를 따라간다)
            foreach (WallSegmentState wall in campaign.Wall) wall.Hp = wall.MaxHp;
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(yard.LampIgnitionNow, "점화의 밤");
            Assert.AreEqual(1f, yard.LampLightNow, 0.0001f);
            yield return PrepShots(yard, "w36_prep_3_ignition");
            Note("정비 마당 등실 불빛: 복구 0 꺼짐 → 수리 구매 즉시 켜짐 → 중간 → 가득 → 점화의 밤. 16:9·16:10 모두 촬영");
        }
        finally
        {
            KeepPreparation = keep;
            File.WriteAllText(Path.Combine(Out, "w36_prep_lamp.txt"), log.ToString());
        }
    }

    // 정비 사진 한 벌: 16:9와 16:10. 시험 화면은 640x480이라 마당 띠 배율이 실제와 달라 (사진 틀과 어긋난다), 마당 갱신을 멈추고 실제 화면 크기의 배율·가로 위치를 손으로 놓는다
    IEnumerator PrepShots(PrepYard yard, string name)
    {
        yield return Framed(yard, name + "_16x9", 1920, 1080);
        yield return Framed(yard, name + "_16x10", 1920, 1200);
    }

    IEnumerator Framed(PrepYard yard, string name, int width, int height, bool detailMustClearLamp = false)
    {
        yard.enabled = false;
        var strip = (RectTransform)GameObject.Find("Panorama").transform;
        float factor = Mathf.Sqrt(width / 1920f * (height / 1080f));   // 캔버스 배율 (기준 1920x1080, 폭·높이 절반씩 맞춤)
        float canvasWidth = width / factor, canvasHeight = height / factor, scale = canvasHeight / 1080f, picWidth = canvasWidth / scale;
        strip.localScale = Vector3.one * scale;
        strip.anchoredPosition = new Vector2(Mathf.Clamp(picWidth / 2f - yard.HeroPixel.x, Mathf.Min(0f, picWidth - 3840f), 0f) * scale, 0f);
        for (int i = 0; i < 2; i++) yield return null;
        if (detailMustClearLamp)
        {
            // 열린 [등대 세부] 판이 등실 불빛을 가리지 않는다 (두 사각형의 화면 영역이 겹치지 않음)
            var plate = GameObject.Find("Beacon Detail"); var glow = GameObject.Find("Beacon Lamp Glow");
            Assert.IsNotNull(plate, "세부 판이 열려 있다"); Assert.IsNotNull(glow, "등실 불빛 덧그림");
            Rect plateRect = ScreenRect((RectTransform)plate.transform), glowRect = ScreenRect((RectTransform)glow.transform);
            Assert.IsFalse(plateRect.Overlaps(glowRect), $"{width}x{height}: 세부 판 {plateRect} 이 등실 {glowRect} 를 가린다");
        }
        yield return Capture(name, width, height);
        yard.enabled = true;
        for (int i = 0; i < 2; i++) yield return null;
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
    [UnityTest]
    public IEnumerator SettingsReduceMotionToggle()
    {
        Directory.CreateDirectory(Out); log = new StringBuilder();
        string previousFolder = HumanLastRampart.Core.Save.CampaignSave.Folder;
        HumanLastRampart.Core.Save.CampaignSave.Folder = Path.Combine(Path.GetTempPath(), "hlr_probe_save_" + System.Guid.NewGuid().ToString("N"));
        GameSettings.Load();
        bool reduce = GameSettings.ReduceMotion, shake = GameSettings.CameraShake;
        try
        {
            SceneManager.LoadScene("Boot"); yield return null; yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            var texts = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
            Assert.IsTrue(System.Array.Exists(texts, t => t.text == "LIGHTBEARERS"), "영문 부제");
            Assert.IsFalse(System.Array.Exists(texts, t => t.text == "HUMAN LAST RAMPART"), "옛 영문 이름은 없다");
            yield return Capture("w36_main_menu");
            yield return Click("설정", 300);
            for (int i = 0; i < 3; i++) yield return null;
            GameSettings.SetReduceMotion(false);
            yield return Capture("w36_settings_off");
            yield return ClickPrefix("움직임 줄이기", 60);
            Assert.IsTrue(GameSettings.ReduceMotion, "누르면 바로 켜진다");
            Assert.AreEqual(shake, GameSettings.CameraShake, "화면 흔들림은 그대로");
            yield return Capture("w36_settings_reduce_on");
            GameSettings.SetReduceMotion(reduce);
            yield return Click("닫기", 60);
            Note("설정 창 움직임 줄이기 줄 · 메인 부제 LIGHTBEARERS");
        }
        finally
        {
            GameSettings.SetReduceMotion(reduce); GameSettings.SetCameraShake(shake);
            HumanLastRampart.Core.Save.CampaignSave.Folder = previousFolder;
            File.WriteAllText(Path.Combine(Out, "w36_settings.txt"), log.ToString());
        }
    }
}
