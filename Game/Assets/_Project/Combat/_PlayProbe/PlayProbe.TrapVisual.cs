using System.Collections;
using System.IO;
using System.Reflection;
using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

public sealed partial class PlayProbe
{
    IEnumerator VerifyTrapVisualCleanup(BattleEntry entry, BattleSim sim, int index)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Field(object obj, string name) => obj.GetType().GetField(name, flags).GetValue(obj);
        var view = (BattleView)Field(entry, "view");
        var effect = (BattleEffectsView)Field(view, "effectArt");
        object fieldArt = Field(effect, "fieldArt");
        Assert.IsNotNull(fieldArt, "실제 함정 그림 연결");
        object crater = Field(fieldArt, "trapCrater");
        Assert.IsNotNull(crater);
        var block = (MaterialPropertyBlock)crater.GetType().GetField("Block").GetValue(crater);
        Assert.AreEqual(1f, block.GetFloat("_GroundPlane"), "남는 구덩이는 지면 판");
        Assert.AreEqual(0.5f, block.GetVector("_Pivot").y, 0.0001f, "구덩이 중앙 기준");
        var pause = entry.GetType().GetField("paused", flags);
        bool oldPaused = (bool)pause.GetValue(entry);
        var camera = (Camera)Field(entry, "battleCamera");
        Vector3 oldPosition = camera.transform.position;
        Quaternion oldRotation = camera.transform.rotation;
        try
        {
            pause.SetValue(entry, true);
            // 전투를 멈추고 효과 재생 시간만 흐르게 해 새 폭발과 구분한다.
            for (int i = 0; i < 60; i++) yield return null;
            var plays = (IList)Field(effect, "trapPlays");
            Assert.AreEqual(0, plays.Count, "폭발 효과 수명 뒤 목록에서 완전히 제거");
            object burst = Field(effect, "trap");
            Assert.AreEqual(0, (int)burst.GetType().GetField("Count").GetValue(burst), "남은 폭발 그리기 없음");
            var instances = (HumanLastRampart.Combat.PerfLab.PerfLabInstance[])crater.GetType().GetField("Instances").GetValue(crater);
            int count = (int)crater.GetType().GetField("Count").GetValue(crater);
            Assert.Greater(count, 0, "남는 것은 구덩이");
            for (int i = 0; i < count; i++) Assert.AreEqual(0.025f, instances[i].PosFrame.y, 0.0001f, "지면 높이");
            Vector3 at = sim.GetTrap(index).Position;
            camera.transform.position = at + new Vector3(0f, 7f, -12f);
            camera.transform.LookAt(at);
            yield return Capture("trap_3_ground_crater_after_burst", 1920, 1080, true);
            Note("폭발 잔류 확인: 효과 목록/그리기 0, 구덩이 GroundPlane=1·높이0.025m·중앙 피벗");
        }
        finally
        {
            camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            pause.SetValue(entry, oldPaused);
        }
    }
}
