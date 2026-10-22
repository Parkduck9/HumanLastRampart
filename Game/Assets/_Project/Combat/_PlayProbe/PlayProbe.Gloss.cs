using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Combat.Render;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 접촉 그림자·광택 사진 (W3, K-098): 낮 gloss_day, 밤 횃불 가까이 gloss_night, 그림자 켬/끔 비교 사진.
// 같은 장면·같은 카메라에서 전투를 멈추고 찍는다 (Capture는 PlayProbe.cs)
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator GlossAndShadows()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("현실", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        yield return Click("넘기기", 600);
        yield return WaitScreen(ScreenId.Battle, 900);
        yield return Click("×4", 300);
        for (int i = 0; i < 200; i++) yield return null;   // 적이 다가와 화면에 들어올 때까지

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (BattleSim)typeof(BattleEntry).GetField("sim", flags).GetValue(entry);
        var view = (BattleView)typeof(BattleEntry).GetField("view", flags).GetValue(entry);
        var rig = (BattleCameraRig)typeof(BattleEntry).GetField("rig", flags).GetValue(entry);
        var camera = (Camera)typeof(BattleEntry).GetField("battleCamera", flags).GetValue(entry);
        var pause = typeof(BattleEntry).GetField("paused", flags);
        var setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        Time.captureDeltaTime = 0.0005f;

        ContactShadows shadows = view.Shadows;
        Assert.IsNotNull(shadows, "접촉 그림자 객체");
        Assert.IsTrue(shadows.Available, "그림자 셰이더·타원 그림 (Resources) 연결");
        Assert.Greater(shadows.KindCount, 0, "등록된 개체 버퍼");

        rig.ShowSide(0);
        rig.SetPreset(0);
        bool oldPaused = (bool)pause.GetValue(entry);
        Vector3 oldPosition = camera.transform.position;
        Quaternion oldRotation = camera.transform.rotation;
        try
        {
            foreach (var (time, name) in new[] { (TimeOfDay.Day, "day"), (TimeOfDay.Night, "night") })
            {
                var f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = time; f.Changes = false; setup.Forecast = f;
                sim.WeatherDirty = true;
                for (int i = 0; i < 6; i++) yield return null;

                // 가장 가까운 횃불 곁으로 사진기를 옮겨 반사 확인 (낮에도 같은 자리라 낮·밤이 비교된다)
                Light torch = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Where(l => l.name == "Torch").OrderBy(l => (l.transform.position - oldPosition).sqrMagnitude).FirstOrDefault();
                pause.SetValue(entry, true);
                if (torch != null)
                {
                    Vector3 t = torch.transform.position;
                    Vector3 away = new Vector3(oldPosition.x - t.x, 0f, oldPosition.z - t.z).normalized;
                    camera.transform.position = t + away * 3.5f + Vector3.up * 1.2f;
                    camera.transform.LookAt(t - Vector3.up * 0.3f);
                }

                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture($"gloss_{name}", 1600, 900, true);
                Note($"gloss_{name}: 횃불 {(torch != null ? torch.transform.position.ToString("0.0") : "없음")}, 그림자 그리기 {shadows.DrawCount}회");
                pause.SetValue(entry, false);
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }

            // 그림자 켬/끔 (낮, 처음 사진기)
            var day = setup.Forecast; day.InitialWeather = Weather.Clear; day.TimeOfDay = TimeOfDay.Day; day.Changes = false; setup.Forecast = day;
            sim.WeatherDirty = true;
            for (int i = 0; i < 30; i++) yield return null;
            pause.SetValue(entry, true);
            foreach (bool on in new[] { true, false })
            {
                ContactShadows.Enabled = on;
                for (int i = 0; i < 3; i++) yield return null;
                if (on) Assert.Greater(shadows.DrawCount, 0, "켜면 그리기 호출이 난다");
                else Assert.AreEqual(0, shadows.DrawCount, "끄면 그리기 호출 없음");
                yield return Capture($"gloss_shadows_{(on ? "on" : "off")}", 1600, 900, true);
            }
        }
        finally
        {
            ContactShadows.Enabled = true;
            camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            pause.SetValue(entry, oldPaused);
        }

        File.WriteAllText(Path.Combine(Out, "gloss.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }
}
