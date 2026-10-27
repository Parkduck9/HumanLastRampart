using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

// 대포·병사 발이 바닥에 묻히는지 점검 (W49, 사용자 "대포 올라간 곳도 병사랑 대포 좀 올려야해 아래 묻혀있더라 성벽에")
// - 네 성벽 대포 한 문씩 기본 거리·가까이 낮·밤 사진 (바닥 틈 없이 서 있는지 눈으로)
// - 발 높이 약속: 통로 윗면(덮는 판 윗면) 위에 서 있어야 한다. 그림 발이 윗면보다 낮으면 아래 몇 줄이 바닥에 묻힌다
// 결과: HLR_PLAY_OUT/w49_feet_*.png, w49_feet_<label>.txt
public sealed partial class PlayProbe
{
    [UnityTest, Timeout(1800000)]
    public IEnumerator W49CannonFeetShotsDay() { yield return W49CannonFeet(TimeOfDay.Day, "day"); }

    [UnityTest, Timeout(1800000)]
    public IEnumerator W49CannonFeetShotsNight() { yield return W49CannonFeet(TimeOfDay.Night, "night"); }

    // 통로 윗면 높이 (독립 계산): 덮는 판 윗면 +1cm, 사격구 낮은 난간(두께 안·폭 안)이면 그 윗면
    static float W49FloorUnder(BattleSim sim, Vector3 p)
    {
        float floor = BattleSim.WallHeight + 0.01f;
        if (!BattleWorldView.Active) return floor;
        var prop = BattleWorldView.Current;
        for (int side = 0; side < 4; side++)
        {
            float3 n = WallGeometry.Outward(side), r = WallGeometry.Right(side);
            float inset = sim.CastleHalf - (p.x * n.x + p.z * n.z), along = p.x * r.x + p.z * r.z;
            if (inset < 0f || inset > prop.ParapetDepth) continue;
            foreach (float bay in sim.CannonBays(side)) if (Mathf.Abs(along - bay) < prop.BayWidth * 0.5f) return BattleSim.WallHeight + prop.BayHeight;
        }

        return floor;
    }

    // 그린 발 높이 약속 (낮·밤 같음): 모든 병사·설치된 대포·주인공이 서 있는 바닥 윗면 위 0.3cm~6cm — 묻히지도 뜨지도 않는다
    [UnityTest, Timeout(600000)]
    public IEnumerator W49FeetOnFloorDay()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        try
        {
            yield return EnterBattle(TimeOfDay.Day);
            for (int i = 0; i < 10; i++) yield return null;
            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = entry.Sim;
            object view = ReadField<object>(entry, "view");
            int bad = 0, soldiers = 0, cannons = 0, onSlab = 0;
            float minGap = 99f, maxGap = -99f;
            void Check(string what, Vector3 feet)
            {
                float gap = feet.y - W49FloorUnder(sim, feet);
                minGap = Mathf.Min(minGap, gap); maxGap = Mathf.Max(maxGap, gap);
                if (gap < 0.003f || gap > 0.06f) { bad++; if (bad <= 12) Note($"{what} 발 {feet} 바닥 위 {gap * 100f:0.0}cm"); }
            }

            for (int i = 0; i < sim.SoldierCount; i++)
            {
                var inst = sim.SoldierInstances[i];
                if (inst.TintSize.w <= 0f) continue;
                soldiers++;
                Check("병사" + i, new Vector3(inst.PosFrame.x, inst.PosFrame.y, inst.PosFrame.z));
            }

            var props = ReadField<BattlePropView>(view, "props");
            for (int c = 0; c < sim.CannonCount; c++)
            {
                var info = sim.GetCannon(c);
                if (!info.Alive || info.Moving) continue;
                cannons++;
                Vector3 f = props.CannonFeet(c);
                Check("대포" + c, f);
                if (W49FloorUnder(sim, f) > BattleSim.WallHeight + 0.02f) onSlab++;
            }

            var hero = ReadField<HumanLastRampart.Combat.PerfLab.PerfLabInstance[]>(view, "heroInstance")[0].PosFrame;
            Check("주인공", new Vector3(hero.x, hero.y, hero.z));
            Note($"병사 {soldiers}명, 대포 {cannons}문(낮은 난간 위 {onSlab}), 바닥 위 높이 {minGap * 100f:0.0}~{maxGap * 100f:0.0}cm, 벗어난 것 {bad}");
            Assert.Greater(cannons, 0, "설치된 대포가 있다");
            Assert.Greater(onSlab, 0, "대포는 사격구 낮은 난간 윗면 위에 선다");
            Assert.AreEqual(0, bad, "그림 발이 바닥 윗면 위 0.3~6cm (묻히거나 뜨지 않음):\n" + log);
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "w49_feet_on_floor.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }

    IEnumerator W49CannonFeet(TimeOfDay time, string label)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        CameraPin pin = null;
        try
        {
            yield return EnterBattle(time);
            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = entry.Sim;
            Camera cam = entry.BattleCamera;
            pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            for (int side = 0; side < 4; side++)
            {
                int cannon = -1;
                for (int i = 0; i < sim.CannonCount; i++) if (sim.GetCannon(i).Side == side && sim.GetCannon(i).Alive) { cannon = i; break; }
                if (cannon < 0) continue;
                var info = sim.GetCannon(cannon);
                float3 n3 = WallGeometry.Outward(side);
                Vector3 n = new Vector3(n3.x, 0f, n3.z);
                Vector3 feet = info.Position;
                Note($"{side}면 대포 {cannon}: 위치 {feet} (통로 윗면 {BattleSim.WallHeight + 0.01f:0.000})");
                foreach (var view in new[] { ("default", 3.2f, 2.25f), ("close", 1.3f, 0.9f) })
                {
                    pin.Position = feet - n * view.Item2 + Vector3.up * view.Item3;
                    pin.Rotation = Quaternion.LookRotation(feet + Vector3.up * 0.2f - pin.Position, Vector3.up);
                    for (int i = 0; i < 4; i++) yield return null;
                    yield return Capture($"w49_feet_{label}_side{side}_{view.Item1}", 1920, 1080, true);
                }
            }

            File.WriteAllText(Path.Combine(Out, $"w49_feet_{label}.txt"), log.ToString(), new UTF8Encoding(false));
        }
        finally
        {
            if (pin != null) Object.Destroy(pin);
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            Time.captureDeltaTime = 0f;
        }
    }
}
