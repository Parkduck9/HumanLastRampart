using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 발표 자료용 사진 (2026-10-19 사용자 요청): 16:9 1920×1080. 현실 4번 전투(병사 약 4,200명)에서
// 낮 대규모 전투(표시판 없음·있음), 마법 폭발 순간, 밤 전투, 지도(전체·한 면). 결과: HLR_PLAY_OUT/NN_pf_*.png
public sealed partial class PlayProbe
{
    const int PfWidth = 1920, PfHeight = 1080;

    [UnityTest]
    public IEnumerator PortfolioShots()
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
            var line = c.Squads.Where(s => !s.UnitDefId.Contains("cannon")).ToList();
            int cannonCrew = c.Squads.Where(s => s.UnitDefId.Contains("cannon")).Sum(s => s.Count);
            int each = Mathf.CeilToInt((TimingSoldierTarget - cannonCrew) / (float)Mathf.Max(1, line.Count));
            foreach (SquadState s in line) s.Count = each;
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

            // 적이 성벽 앞에 몰릴 때까지 (게임 시간 70초)
            yield return Press("×4", 120);
            for (int i = 0; i < 6000 && sim.Elapsed < 70f && !sim.Ended; i++) yield return null;
            yield return Press("×1", 120);
            for (int i = 0; i < 10; i++) yield return null;
            sim.Complete();
            Note($"낮: 게임 {sim.Elapsed:0}초, 병사 {sim.SoldierCount}명, 처치 {sim.Kills}");
            yield return Capture("pf_day_clean", PfWidth, PfHeight, true);
            yield return Capture("pf_day_hud", PfWidth, PfHeight);

            // 정면 성벽 앞 25m에 폭발·불바다 → 터지는 순간
            sim.Complete();
            var at = new Vector3(0f, 0f, sim.CastleHalf + 25f);
            int fire = -1, blast = -1;
            for (int s = 0; s < sim.SpellCount; s++)
            {
                if (sim.GetSpell(s).IsFire) { if (fire < 0) fire = s; }
                else if (blast < 0) blast = s;
            }

            bool fireOk = fire >= 0 && sim.CastSpell(fire, at + new Vector3(-8f, 0f, 4f));
            bool blastOk = blast >= 0 && sim.CastSpell(blast, at + new Vector3(6f, 0f, 0f));
            Note($"마법: 불바다 {(fireOk ? "시전" : "실패")}, 폭발 {(blastOk ? "시전" : "실패")}");
            for (int i = 0; i < 22; i++) yield return null;   // 폭발은 1초 뒤 (프레임당 0.05초)
            yield return Capture("pf_day_blast_clean", PfWidth, PfHeight, true);
            for (int i = 0; i < 6; i++) yield return null;
            yield return Capture("pf_day_blast_later_clean", PfWidth, PfHeight, true);

            // 밤 (횃불·불빛)
            f = setup.Forecast; f.TimeOfDay = TimeOfDay.Night; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 40; i++) yield return null;
            yield return Capture("pf_night_clean", PfWidth, PfHeight, true);
            yield return Capture("pf_night_hud", PfWidth, PfHeight);
            f = setup.Forecast; f.TimeOfDay = TimeOfDay.Day; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 10; i++) yield return null;

            // 지도 (표시판이 내용이므로 같이 찍는다)
            entry.ToggleMap();
            for (int i = 0; i < 5; i++) yield return null;
            entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Full);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture("pf_map_full", PfWidth, PfHeight);
            entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Side);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture("pf_map_side", PfWidth, PfHeight);
            entry.ToggleMap();
        }
        finally
        {
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "portfolio.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
