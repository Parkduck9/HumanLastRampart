using System.Collections;
using System.Collections.Generic;
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

    // 통로 윗면 높이 (장면에서 직접): 통로 덮는 판("Walkway"·"Corner Walkway")과 난간 조각("Parapet": 사격구에서는 낮은 난간)의 실제 그리기 상자 중
    // 이 점(가로·세로)을 품은 것의 윗면. 코드의 계산식(BattleSim.Standing)을 다시 쓰지 않는다. 마당 바닥은 상자가 없어 통로 판 높이(+1cm)와 같다
    static float W49FloorUnder(List<Bounds> tops, Vector3 p)
    {
        float floor = BattleSim.WallHeight + 0.01f;
        foreach (Bounds b in tops)
            if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) floor = Mathf.Max(floor, b.max.y);
        return floor;
    }

    static List<Bounds> W49CollectFloorTops()
    {
        var tops = new List<Bounds>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (r.enabled && (r.name == "Walkway" || r.name == "Corner Walkway" || r.name == "Parapet")) tops.Add(r.bounds);
        return tops;
    }

    // 그린 발 높이 약속 (낮·밤 같음): 모든 병사·달리는 병사·설치된 대포·주인공·전령이 서 있는 바닥 윗면 위 0.3cm~6cm — 묻히지도 뜨지도 않는다.
    // 끄는 중에는 매 프레임 끄는 병사가 바닥 위 같은 범위이고, 포가 높이는 프레임 사이에 뛰지 않으며 출발 전·도착 뒤 설치 대포 높이와 같다
    [UnityTest, Timeout(1200000)]
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
            var tops = W49CollectFloorTops();
            Assert.Greater(tops.Count, 8, "통로 판·난간 상자가 있다");
            int bad = 0, soldiers = 0, runners = 0, cannons = 0, onSlab = 0, others = 0;
            float minGap = 99f, maxGap = -99f;
            void Check(string what, Vector3 feet)
            {
                float gap = feet.y - W49FloorUnder(tops, feet);
                minGap = Mathf.Min(minGap, gap); maxGap = Mathf.Max(maxGap, gap);
                if (gap < 0.003f || gap > 0.06f) { bad++; if (bad <= 12) Note($"{what} 발 {feet} 바닥 위 {gap * 100f:0.0}cm"); }
            }

            void CheckPiece(string what, object piece, float maxY)
            {
                if (piece == null) return;
                var inst = ReadField<HumanLastRampart.Combat.PerfLab.PerfLabInstance[]>(piece, "Instances");
                int n = ReadField<int>(piece, "Count");
                for (int i = 0; i < n && i < inst.Length; i++)
                {
                    var p = inst[i].PosFrame;
                    if (inst[i].TintSize.w <= 0f || p.y > maxY) continue;   // 탑 위로 오르는 작업병은 이 시험 범위 밖
                    others++;
                    Check(what + i, new Vector3(p.x, p.y, p.z));
                }
            }

            void CheckAllFigures()
            {
                for (int i = 0; i < sim.SoldierCount; i++)
                {
                    var inst = sim.SoldierInstances[i];
                    if (inst.TintSize.w > 0f) { soldiers++; Check("병사" + i, new Vector3(inst.PosFrame.x, inst.PosFrame.y, inst.PosFrame.z)); }
                    var run = sim.RunnerInstances[i];
                    if (run.TintSize.w > 0f) { runners++; Check("달리는 병사" + i, new Vector3(run.PosFrame.x, run.PosFrame.y, run.PosFrame.z)); }
                }

                var hero = ReadField<HumanLastRampart.Combat.PerfLab.PerfLabInstance[]>(view, "heroInstance")[0].PosFrame;
                Check("주인공", new Vector3(hero.x, hero.y, hero.z));
                var beacon = ReadField<object>(view, "beacon");
                CheckPiece("전령(서기)", ReadField<object>(beacon, "courierStand"), WallHeight4 + 0.2f);
                CheckPiece("전령(달리기)", ReadField<object>(beacon, "courierRun"), WallHeight4 + 0.2f);
                // 작업병은 탑 안쪽 바닥(통로 판보다 약 14cm 높다)에 서서 이 시험의 바닥 상자로는 잴 수 없다 — 발 높이는 FootLift만 더한다 (BattleBeaconView)
            }

            CheckAllFigures();
            var props = ReadField<BattlePropView>(view, "props");
            float installedY = 0f; int mover = -1;
            for (int c = 0; c < sim.CannonCount; c++)
            {
                var info = sim.GetCannon(c);
                if (!info.Alive || info.Moving) continue;
                cannons++;
                Vector3 f = props.CannonFeet(c);
                Check("대포" + c, f);
                if (W49FloorUnder(tops, f) > BattleSim.WallHeight + 0.02f) onSlab++;
                if (mover < 0 && info.Side == 0) { mover = c; installedY = f.y; }
            }

            Note($"병사 {soldiers}명, 달리는 병사 {runners}, 전령 {others}, 대포 {cannons}문(낮은 난간 위 {onSlab}), 바닥 위 높이 {minGap * 100f:0.0}~{maxGap * 100f:0.0}cm, 벗어난 것 {bad}");
            Assert.Greater(cannons, 0, "설치된 대포가 있다");
            Assert.Greater(onSlab, 0, "대포는 사격구 낮은 난간 윗면 위에 선다");

            // 끌기: 북쪽 대포를 동쪽 빈 사격구로. 출발 전 높이 = 끄는 첫 프레임 포가 높이, 끄는 동안 프레임 사이 변화가 작고, 도착 뒤 설치 대포 높이와 같다
            Assert.Greater(sim.FreeCannonBays(1), 0, "동쪽 빈 사격구");
            sim.Complete();
            Assert.IsTrue(sim.MoveCannon(mover, 1, false), "대포 옮기기 출발");
            var chassis = ReadField<object>(props, "haulChassis");
            float prevY = float.NaN, firstY = float.NaN, lastY = float.NaN, maxJump = 0f;
            int haulFrames = 0, crewChecked = 0;
            for (int i = 0; i < 4000; i++)
            {
                Time.captureDeltaTime = 0.1f;
                yield return null;
                sim.Complete();
                if (sim.CannonHauls.Count == 0) { if (haulFrames > 0) break; else continue; }
                haulFrames++;
                var ci = ReadField<HumanLastRampart.Combat.PerfLab.PerfLabInstance[]>(chassis, "Instances");
                if (ReadField<int>(chassis, "Count") < 1) continue;
                float y = ci[0].PosFrame.y;
                if (float.IsNaN(firstY)) firstY = y;
                if (!float.IsNaN(prevY)) maxJump = Mathf.Max(maxJump, Mathf.Abs(y - prevY));
                prevY = lastY = y;
                foreach (string kind in new[] { "haulPull", "haulPush" })
                {
                    object piece = ReadField<object>(props, kind);
                    var pi = ReadField<HumanLastRampart.Combat.PerfLab.PerfLabInstance[]>(piece, "Instances");
                    for (int k = 0; k < ReadField<int>(piece, "Count"); k++)
                    {
                        crewChecked++;
                        Check("끄는 병사", new Vector3(pi[k].PosFrame.x, pi[k].PosFrame.y, pi[k].PosFrame.z));
                    }
                }
            }

            Time.captureDeltaTime = 0f;
            for (int i = 0; i < 3; i++) yield return null;
            float arrivedY = props.CannonFeet(mover).y;
            Note($"끌기 {haulFrames}프레임: 설치 높이 {installedY:0.0000} → 첫 포가 {firstY:0.0000}, 마지막 포가 {lastY:0.0000} → 도착 설치 {arrivedY:0.0000}, 프레임 사이 최대 변화 {maxJump * 100f:0.00}cm, 끄는 병사 검사 {crewChecked}, 벗어난 것 {bad}");
            Assert.Greater(haulFrames, 5, "끄는 그림이 여러 프레임 보인다");
            Assert.AreEqual(installedY, firstY, 0.01f, "끌기를 시작할 때 포가가 설치 높이에서 튀지 않는다");
            Assert.AreEqual(arrivedY, lastY, 0.01f, "도착할 때 포가가 설치 높이로 튀지 않는다");
            Assert.Less(maxJump, 0.01f, "끄는 동안 포가 높이가 프레임 사이에 1cm 넘게 뛰지 않는다");
            Assert.AreEqual(0, bad, "그림 발이 바닥 윗면 위 0.3~6cm (묻히거나 뜨지 않음):\n" + log);
        }
        finally
        {
            Time.captureDeltaTime = 0f;
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "w49_feet_on_floor.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }

    const float WallHeight4 = BattleSim.WallHeight;
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
