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

    // 모서리 돌기 확인 (사용자 '코너 돌 때 땅이 위아래로' 2026-10-20): 북쪽 성벽 오른쪽 끝에서 동쪽으로 도는 동안
    // 사진 14장과 매 장면 사진기 높이·숙임·방향, 주인공 발 높이를 적는다. 결과: HLR_PLAY_OUT/corner_sequence.txt
    [UnityTest]
    public IEnumerator CornerSequence()
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
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var rig = (HumanLastRampart.Combat.Battle.BattleCameraRig)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("rig", flags).GetValue(entry);
            var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", flags).GetValue(entry);
            entry.StartBattle();
            Time.captureDeltaTime = 0.0001f;   // 전투는 거의 멈춤 (사진기·주인공은 실제 시간)
            rig.ShowSide(0);
            rig.WalkInput = 1f;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (rig.HeroFeet.x < sim.CastleHalf - 5f && watch.Elapsed.TotalSeconds < 15) yield return null;
            for (int k = 0; k < 14; k++)
            {
                Camera cam = Camera.allCameras.Where(c => !c.orthographic).OrderBy(c => c.depth).First();
                Note($"{k:00}: 면 {rig.Side} 방향 {rig.Yaw:0.0}도 · 사진기 높이 {cam.transform.position.y:0.000} 숙임 {cam.transform.eulerAngles.x:0.0}도 · 발 높이 {rig.HeroFeet.y:0.000} 발 ({rig.HeroFeet.x:0.0}, {rig.HeroFeet.z:0.0})");
                yield return Capture($"corner_{k:00}", 960, 540, true);
                var gap = System.Diagnostics.Stopwatch.StartNew();
                while (gap.Elapsed.TotalSeconds < 0.12) yield return null;
            }

            rig.WalkInput = 0f;
        }
        finally
        {
            File.WriteAllText(Path.Combine(Out, "corner_sequence.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    // 밤 밝기 비교 (사용자 2026-10-20 "밝기 조절해서 3개 사진") — 같은 밤 장면을 단계별로 찍는다. 결과: NN_night_level_*.png
    [UnityTest]
    public IEnumerator NightLevels()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        float before = HumanLastRampart.Combat.Render.NightTuning.Level;
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
            for (int i = 0; i < 6000 && sim.Elapsed < 60f && !sim.Ended; i++) yield return null;
            yield return Press("×1", 120);
            sim.Complete();

            float[] levels = { 0f, 0.35f, 0.7f, 1f };
            foreach (float level in levels)
            {
                HumanLastRampart.Combat.Render.NightTuning.Level = level;
                sim.WeatherDirty = true;   // 하늘·환경광·화면 효과 다시
                for (int i = 0; i < 6; i++) yield return null;
                sim.Complete();
                Note($"밤 밝기 {level:0.00}: 그림 밝기 {HumanLastRampart.Combat.Render.NightTuning.SpriteBrightness:0.00}, 노출 {HumanLastRampart.Combat.Render.NightTuning.Exposure:0.00}");
                yield return Capture($"night_level_{Mathf.RoundToInt(level * 100):000}", 1280, 720, true);
            }
        }
        finally
        {
            HumanLastRampart.Combat.Render.NightTuning.Level = before;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "night_levels.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    [UnityTest]
    public IEnumerator PortfolioShots()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
            "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        CrowdedProbeTerrain crowdedTerrain = null;
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
            crowdedTerrain = new CrowdedProbeTerrain();
            Note("4,200명 · 시험 성벽 80m: 실제 게임 성벽 40m와 구분");
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
            AssertCrowdedProbe(sim);
            entry.StartBattle();

            // 초반 장면의 시각 검토용 촬영이다. 70초 생존·후반 밀집 성능 시험과 구분한다.
            const float captureAt = 6f;
            Note($"시각 촬영 목표 {captureAt:0}초 · 후반 생존·성능 검증 아님");
            yield return Press("×4", 120);
            for (int i = 0; i < 6000 && sim.Elapsed < captureAt && !sim.Ended; i++) yield return null;
            sim.Complete();
            Assert.IsFalse(sim.Ended, "촬영 목표 시간 전에 전투 종료");
            Assert.GreaterOrEqual(sim.Elapsed, captureAt, "촬영 목표 시간 도달");
            yield return Press("×1", 120);
            for (int i = 0; i < 10; i++) yield return null;
            sim.Complete();

            void AssertLive()
            {
                sim.Complete();
                Assert.IsFalse(sim.Ended, "촬영 도중 전투 종료");
                Assert.AreEqual(ScreenId.Battle, GameFlow.Instance.CurrentScreen, "정상 전투 화면");
            }

            IEnumerator CheckedCapture(string name, bool sceneOnly = false)
            {
                AssertLive();
                string path = Path.Combine(Out, $"{shot + 1:00}_{name}.png");
                yield return Capture(name, PfWidth, PfHeight, sceneOnly);
                AssertLive();
                Assert.IsTrue(File.Exists(path), $"촬영 파일 생성: {name}");
                Note($"촬영 {name}: 게임 {sim.Elapsed:0.00}초 · 전투 진행 · 지도 {entry.MapOpen}");
            }

            Note($"낮: 게임 {sim.Elapsed:0}초, 병사 {sim.SoldierCount}명, 처치 {sim.Kills}");
            yield return CheckedCapture("pf_day_clean", true);
            yield return CheckedCapture("pf_day_hud");

            // 정면 성벽 앞 25m에 폭발·불바다 → 터지는 순간
            sim.Complete();
            var at = new Vector3(0f, 0f, sim.CastleHalf + 25f);
            int fire = -1, blast = -1;
            for (int s = 0; s < sim.SpellCount; s++)
            {
                if (sim.GetSpell(s).IsFire) { if (fire < 0) fire = s; }
                else if (blast < 0) blast = s;
            }

            AssertLive();
            Assert.IsTrue(sim.WizardPresent, "촬영 전투에 마법사 참가");
            Assert.IsFalse(sim.WizardMoving, "시전 전에 마법사 정지");
            Assert.AreEqual(0, sim.WizardSide, "즉시 시전할 북쪽 성벽");
            Assert.GreaterOrEqual(fire, 0, "불바다 정의");
            Assert.GreaterOrEqual(blast, 0, "폭발 정의");
            int effectsBefore = sim.EffectsStarted;
            bool fireOk = sim.CastSpell(fire, at + new Vector3(-8f, 0f, 4f));
            bool blastOk = sim.CastSpell(blast, at + new Vector3(6f, 0f, 0f));
            Note($"마법: 불바다 {(fireOk ? "시전" : "실패")}, 폭발 {(blastOk ? "시전" : "실패")}");
            Assert.IsTrue(fireOk, "불바다 시전 성공");
            Assert.IsTrue(blastOk, "폭발 시전 성공");
            sim.Complete();
            Assert.AreEqual(effectsBefore + 2, sim.EffectsStarted, "이동 예약이 아닌 두 마법 효과 시작");
            for (int i = 0; i < 22; i++) yield return null;   // 폭발은 1초 뒤 (프레임당 0.05초)
            AssertLive();
            Assert.IsTrue(sim.Effects.Any(e => e.Fire && e.TimeLeft > 0f), "촬영 시 불바다 효과 유지");
            Assert.IsTrue(sim.Effects.Any(e => !e.Fire && !e.Pending && e.TimeLeft > 0f), "촬영 시 실제 폭발 효과");
            yield return CheckedCapture("pf_day_blast_clean", true);
            for (int i = 0; i < 6; i++) yield return null;
            yield return CheckedCapture("pf_day_blast_later_clean", true);

            // 밤 (횃불·불빛)
            f = setup.Forecast; f.TimeOfDay = TimeOfDay.Night; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 40; i++) yield return null;
            yield return CheckedCapture("pf_night_clean", true);
            yield return CheckedCapture("pf_night_hud");
            f = setup.Forecast; f.TimeOfDay = TimeOfDay.Day; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 10; i++) yield return null;

            // 지도 (표시판이 내용이므로 같이 찍는다)
            entry.ToggleMap();
            for (int i = 0; i < 5; i++) yield return null;
            AssertLive();
            Assert.IsTrue(entry.MapOpen, "지도 열림");
            Assert.IsNotNull(entry.MapCamera, "지도 카메라");
            entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Full);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Full, entry.MapCamera.Current, "전체 지도 단계");
            yield return CheckedCapture("pf_map_full");
            entry.MapCamera.SetLevel(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Side);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(entry.MapOpen, "한 면 촬영 전 지도 유지");
            Assert.AreEqual(HumanLastRampart.Combat.Battle.BattleMapCamera.Level.Side, entry.MapCamera.Current, "한 면 지도 단계");
            yield return CheckedCapture("pf_map_side");
            entry.ToggleMap();
            Assert.IsFalse(entry.MapOpen, "지도 닫힘");
        }
        finally
        {
            crowdedTerrain?.Dispose();
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "portfolio.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
