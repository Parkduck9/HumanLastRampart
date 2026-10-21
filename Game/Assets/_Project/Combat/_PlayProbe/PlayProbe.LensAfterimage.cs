using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Upgrades;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// CODE03: 실제 지도 그리기 자원과 관측 해제 잔상을 같은 화면에서 확인한다.
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator LensAfterimageMap()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            var campaign = GameFlow.Instance.Campaign;
            campaign.Gold = 10000;
            Assert.IsTrue(UpgradeRules.TryBuy(campaign, GameFlow.Instance.Definitions,
                "item_telescope_lens", null, out string reason), reason);
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            Assert.IsNotNull(story);
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            var entry = Object.FindAnyObjectByType<BattleEntry>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var sim = (BattleSim)typeof(BattleEntry).GetField("sim", flags).GetValue(entry);
            sim.Complete();
            Assert.IsTrue(sim.HasTelescopeLens);
            var forecast = campaign.ActiveBattleSetup.Forecast;
            forecast.InitialWeather = Weather.Fog;
            forecast.TimeOfDay = TimeOfDay.Night;
            forecast.Changes = false;
            campaign.ActiveBattleSetup.Forecast = forecast;
            sim.WeatherDirty = true;
            entry.ToggleMap();
            entry.MapCamera.SetLevel(BattleMapCamera.Level.Full);
            var states = (NativeArray<byte>)typeof(BattleSim).GetField("eState", flags).GetValue(sim);
            var positions = (NativeArray<float3>)typeof(BattleSim).GetField("ePos", flags).GetValue(sim);
            states[0] = EnemyState.Walking;
            positions[0] = new float3(0, 0, 100);
            sim.UpdateTelescopeAfterimages(new Vector3(0, 0, 100), Time.unscaledTime);
            sim.UpdateTelescopeAfterimages(null, Time.unscaledTime);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(sim.TelescopeAfterimages.TryGet(0, Time.unscaledTime, out var dot, out float alpha));
            Assert.AreEqual(100f, dot.Position.z);
            Assert.Greater(alpha, 0f);
            var view = (BattleView)typeof(BattleEntry).GetField("view", flags).GetValue(entry);
            var material = (Material)typeof(BattleView).GetField("lensAfterimageMaterial", flags).GetValue(view);
            Assert.IsNotNull(material, "실제 지도 잔상 재질 로드");
            Assert.IsTrue(material.shader.isSupported, "현재 그래픽 장치에서 잔상 shader 지원");
            Assert.IsNotNull(typeof(BattleView).GetField("lensAfterimageBuffer", flags).GetValue(view));
            Note("렌즈 장착·마지막 관측100m·잔상 재질과 GPU버퍼 확인");
            yield return Capture("lens_afterimage_visible");
            yield return new WaitForSecondsRealtime(4.2f);
            Assert.IsFalse(sim.TelescopeAfterimages.TryGet(0, Time.unscaledTime, out _, out _), "실제 시간4초 만료");
            yield return Capture("lens_afterimage_expired");
            Note("4초 뒤 잔상 만료 확인");
        }
        finally
        {
            File.WriteAllText(Path.Combine(Out, "lens_afterimage_map.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
