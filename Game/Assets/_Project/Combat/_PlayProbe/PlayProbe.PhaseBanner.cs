using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class PlayProbe
{
    // 단계 전투 흐름 (나안, 2026-10-22): 4번째 전투 쉬움에서 선봉만 가까이 나오는지, 본대 예고 띠가 뜨는지 사진으로 확인
    [UnityTest]
    public IEnumerator PhaseBanner()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);

            CampaignState c = GameFlow.Instance.Campaign;
            c.BattleNumber = 4;
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            Assert.IsNotNull(story, "스토리 재생기");
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            BattleSetup setup = c.ActiveBattleSetup;
            var f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Day; f.Changes = false; setup.Forecast = f;
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry)
                .GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            sim.WeatherDirty = true;
            entry.StartBattle();

            // 선봉: 3초에 성벽 앞에 몇십 마리만 보여야 한다 (45~55m에서 나와 다가오는 중, 사용자 2026-10-22)
            for (int i = 0; i < 3000 && sim.Elapsed < 3f && !sim.Ended; i++) yield return null;
            yield return FreshHud();
            sim.Complete();
            Note($"선봉 {sim.VanguardCount}마리 · 전체 {sim.TotalEnemies} · {sim.Elapsed:0.0}초");
            yield return Capture("phase_vanguard");

            // 예고 띠: 경고가 뜰 때까지 빠르게 돌린다
            yield return Press("×4", 120);
            bool warned = false;
            for (int i = 0; i < 20000 && sim.Elapsed < 120f && !sim.Ended; i++)
            {
                if (sim.TryGetPhaseWarning(out _)) { warned = true; break; }
                yield return null;
            }
            yield return Press("×1", 120);
            yield return FreshHud();
            sim.Complete();
            Assert.IsTrue(sim.TryGetPhaseWarning(out _) || sim.FinalAssaultStarted, "촬영 순간에도 예고 중");
            Assert.IsTrue(warned, "120초 안에 단계 예고");
            Note($"예고 {sim.Elapsed:0.0}초 · 단계 {sim.CurrentPhase}");

            // K-100 그림이 실제로 읽혀 띠에 붙었는지 (띠·상징·방향 표식)
            Assert.IsNotNull(HumanLastRampart.UI.UiArt.Hud("Phase/phase_banner"), "띠 그림 파일");
            GameObject bandRoot = GameObject.Find("Phase Banner Band");
            Assert.IsNotNull(bandRoot, "예고 띠 오브젝트");
            var bandImage = bandRoot.transform.Find("Band").GetComponent<UnityEngine.UI.Image>();
            Assert.IsNotNull(bandImage.sprite, "띠 9-slice 그림이 붙음");
            Assert.IsTrue(bandImage.enabled, "띠 그림 표시");
            Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, bandImage.type);
            var emblemImage = bandRoot.transform.Find("Emblem").GetComponent<UnityEngine.UI.Image>();
            Assert.IsNotNull(emblemImage.sprite, "단계 상징 그림이 붙음");
            if (sim.TryGetPhaseWarning(out HumanLastRampart.Combat.Battle.BattleSim.PhaseWarning pw))
            {
                GameObject icon = GameObject.Find("Phase Warning Icon");
                Assert.IsNotNull(icon, "방향 경고 표식");
                Assert.IsNotNull(icon.GetComponent<UnityEngine.UI.Image>().sprite, "방향 경고 그림이 붙음");
                Assert.IsNotNull(HumanLastRampart.Combat.Battle.BattleHud.PhasePulseFrame(
                    $"Phase/warning_{HumanLastRampart.Combat.Battle.BattleHud.PhaseKey(pw.Phase)}_{"NESW"[pw.Side]}_pulse", 1), "맥동 시트 칸");
            }
            yield return Capture("phase_banner");
        }
        finally
        {
            Time.captureDeltaTime = 0f;
            File.WriteAllText(Path.Combine(Out, "phase_banner.txt"), log.ToString());
        }
    }

    // 화면 글자는 실제 시간 0.2초마다 바뀐다. 시험은 너무 빨라 갱신 전에 찍히므로 갱신 시각을 당겨 한 프레임 넘긴다
    static IEnumerator FreshHud()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        foreach (var entry in Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None))
        {
            object hud = typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("hud", flags)?.GetValue(entry);
            if (hud != null) typeof(HumanLastRampart.Combat.Battle.BattleHud).GetField("nextRefresh", flags).SetValue(hud, 0f);
        }
        yield return null;
        yield return null;
    }
}