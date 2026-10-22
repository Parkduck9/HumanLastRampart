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

// 먼 적 저비용 그림 사진 (W4b, K-099): 게임 75초 전장을 같은 사진기·같은 순간에 먼 그림 켬(far_on)/끔(far_off)으로 찍는다.
// 전투를 멈추고 찍는다 (Capture는 PlayProbe.cs). 두 사진 사이 거리 띠 안 적 수를 기록에 남긴다
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator FarSpritesPhotos()
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

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
        var sim = (BattleSim)typeof(BattleEntry).GetField("sim", flags).GetValue(entry);
        var camera = (Camera)typeof(BattleEntry).GetField("battleCamera", flags).GetValue(entry);
        var pause = typeof(BattleEntry).GetField("paused", flags);
        var setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        var f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Day; f.Changes = false; setup.Forecast = f;
        sim.WeatherDirty = true;
        // 75초까지 살아남도록 패배 조건만 푼다 (FrameTiming과 같은 시험 전용 방식, 이 개체에서만)
        typeof(BattleSim).GetField("garrisonLossRatio", flags).SetValue(sim, float.MaxValue);
        var wallHp = (float[])typeof(BattleSim).GetField("wallHp", flags).GetValue(sim);
        for (int w = 0; w < wallHp.Length; w++) wallHp[w] = 1e9f;
        yield return AdvanceTo(sim, 75f);
        if (EndedEarly(sim, "75초")) yield break;

        bool farOld = FarSprites.Enabled;
        bool oldPaused = (bool)pause.GetValue(entry);
        try
        {
            pause.SetValue(entry, true);
            Time.captureDeltaTime = 0.0005f;
            sim.Complete();
            Vector3 eye = camera.transform.position;
            int near = 0, half = 0, quarter = 0;
            for (int i = 0; i < sim.EnemyInstances.Length; i++)
            {
                var item = sim.EnemyInstances[i];
                if (item.TintSize.w <= 0f) continue;
                float d = Vector3.Distance(eye, new Vector3(item.PosFrame.x, item.PosFrame.y, item.PosFrame.z));
                if (d < FarSprites.HalfFrom) near++; else if (d < FarSprites.QuarterFrom) half++; else quarter++;
            }

            Note($"75초 적 거리 띠 (사진기 기준): 가까움 {near} · 절반 {half} · 1/4 {quarter}");
            foreach (bool on in new[] { true, false })
            {
                FarSprites.Enabled = on;
                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture(on ? "far_on" : "far_off", 1600, 900, true);
            }
        }
        finally
        {
            FarSprites.Enabled = farOld;
            pause.SetValue(entry, oldPaused);
            Time.captureDeltaTime = 0f;
            File.WriteAllText(Path.Combine(Out, "far_sprites.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }
}
