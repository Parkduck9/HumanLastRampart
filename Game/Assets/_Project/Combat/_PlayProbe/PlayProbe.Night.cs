using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 밤 어둡게·등대 빛줄기 확인 사진 (W15, 사용자 2026-10-22): 4번 전투 밤, 북쪽 성벽에서 빛줄기 보통·강 두 장 + 북쪽 빛 끈 장면.
// 결과: HLR_PLAY_OUT/NN_night_beam_*.png (1920×1080, 표시판 없음)
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator NightBeamShots()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("현실", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            CampaignState c = GameFlow.Instance.Campaign;
            c.BattleNumber = 4;
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            BattleSetup setup = c.ActiveBattleSetup;
            var f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Night; f.Changes = false; setup.Forecast = f;
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry)
                .GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            sim.WeatherDirty = true;
            entry.StartBattle();
            yield return Press("×4", 120);
            for (int i = 0; i < 6000 && sim.Elapsed < 25f && !sim.Ended; i++) yield return null;
            yield return Press("×1", 120);
            sim.Complete();
            for (int i = 0; i < 20; i++) yield return null;
            sim.Complete();

            Note($"밤 밝기 {HumanLastRampart.Combat.Render.NightTuning.Level:0.00} · 빛줄기 {sim.BeamAngleDegrees:0}도 폭 {sim.BeamWidth:0}");
            yield return Capture("night_beam_normal", 1920, 1080, true);
            sim.SetLamp(LampBrightness.High);
            for (int i = 0; i < 4; i++) yield return null;
            sim.Complete();
            yield return Capture("night_beam_high", 1920, 1080, true);
            sim.SetLamp(LampBrightness.Low);
            for (int i = 0; i < 4; i++) yield return null;
            sim.Complete();
            yield return Capture("night_beam_low", 1920, 1080, true);
        }
        finally
        {
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "night_beam.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
