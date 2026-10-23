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
            Assert.IsTrue(yard.SetGround(PrepYardProjection.Ground(new Vector2(1500, 960))), "탑이 보이는 자리");
            Note($"화면 {Screen.width}x{Screen.height}, 마당 띠 배율 {((RectTransform)GameObject.Find("Panorama").transform).localScale.x:0.000}, 판 높이 {((RectTransform)GameObject.Find("Panorama").transform.parent).rect.height:0}");
            CampaignState campaign = GameFlow.Instance.Campaign;
            campaign.Gold = 99999; campaign.BeaconRepairBoughtToday = 0;
            foreach (WallSegmentState wall in campaign.Wall) wall.Hp = Mathf.Max(1, wall.MaxHp / 2);   // 성벽 90% 미만: 점화의 밤이 아닌 상태
            campaign.BeaconRepair = 0f; yard.RefreshTower();
            Assert.AreEqual(0f, yard.LampLightNow, "복구 0 = 꺼짐");
            yield return Capture("w36_prep_repair0", 1920, 1080);
            yield return LampCloseup(yard, "w36_lamp_0_dead");

            // 수리 단추를 누르면 마당 불빛이 바로 바뀐다 (refreshTower)
            yield return Click("등대 세부", 120);   // 얇은 띠의 접이식 칸을 연다 (밝기·수리 단추가 그 안에 있다)
            yield return Click("등대 수리", 120);
            Assert.AreEqual(PrepYard.LampLight(BeaconRules.DayRepairAmount, false), yard.LampLightNow, 0.0001f, "수리를 사면 같은 프레임에 불빛이 켜진다");
            Assert.Greater(yard.LampLightNow, 0f);
            yield return Click("등대 세부", 120);   // 다시 접는다
            Assert.IsNull(GameObject.Find("Beacon Detail"), "접힌 칸은 숨김 상태 (Find는 켜진 것만 찾는다)");

            campaign.BeaconRepair = 40f; yard.RefreshTower();
            Assert.IsFalse(yard.LampIgnitionNow);
            yield return Capture("w36_prep_repair_mid", 1920, 1080);
            yield return LampCloseup(yard, "w36_lamp_1_mid");
            campaign.BeaconRepair = 100f; yard.RefreshTower();
            Assert.IsFalse(yard.LampIgnitionNow, "성벽이 낮으면 복구가 가득이어도 점화의 밤이 아니다");
            yield return Capture("w36_prep_repair_full", 1920, 1080);
            yield return LampCloseup(yard, "w36_lamp_2_full");

            // 성벽도 채우면 점화의 밤: 마당이 스스로 알아챈다 (Update가 상태를 따라간다)
            foreach (WallSegmentState wall in campaign.Wall) wall.Hp = wall.MaxHp;
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(yard.LampIgnitionNow, "점화의 밤");
            Assert.AreEqual(1f, yard.LampLightNow, 0.0001f);
            yield return Capture("w36_prep_ignition_night", 1920, 1080);
            yield return LampCloseup(yard, "w36_lamp_3_ignition");
            Note("정비 마당 등실 불빛: 복구 0 꺼짐 → 수리 구매 즉시 켜짐 → 중간 → 가득 → 점화의 밤");
        }
        finally
        {
            KeepPreparation = keep;
            File.WriteAllText(Path.Combine(Out, "w36_prep_lamp.txt"), log.ToString());
        }
    }

    // 확인용 가까이 보기: 마당 갱신을 잠시 멈추고 등실이 화면 가운데 오게 2배로 당긴다 (실제 화면에서는 위쪽 줄이 등실을 덮는다)
    IEnumerator LampCloseup(PrepYard yard, string name)
    {
        yard.enabled = false;
        var strip = (RectTransform)GameObject.Find("Panorama").transform;
        strip.localScale = Vector3.one * 2f; strip.anchoredPosition = new Vector2(960f - 2f * 1918f, 560f - 2f * 900f);
        for (int i = 0; i < 2; i++) yield return null;
        yield return Capture(name, 1920, 1080);
        yard.enabled = true;
        for (int i = 0; i < 2; i++) yield return null;
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
