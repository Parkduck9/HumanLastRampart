using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 주인공 둘레 투시 구멍 (W42): 건물·탑이 주인공을 가리는 사진과 가리지 않는 사진에서, 구멍을 켠 것·끈 것·가림막을 치운 것을 같은 자리에서 찍어 화소로 비교한다.
// 결과: HLR_PLAY_OUT/NN_seethrough_<day|night>_*_<on|off|truth>.png, seethrough_<day|night>_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator SeeThroughDay() { yield return SeeThroughShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator SeeThroughNight() { yield return SeeThroughShots(TimeOfDay.Night, "night"); }

    static Color32[] LoadPng(string path, out int w, out int h)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.LoadImage(File.ReadAllBytes(path));
        w = tex.width; h = tex.height;
        Color32[] pixels = tex.GetPixels32();
        Object.Destroy(tex);
        return pixels;
    }

    static bool Differs(Color32 a, Color32 b, int tolerance)
        => Mathf.Abs(a.r - b.r) > tolerance || Mathf.Abs(a.g - b.g) > tolerance || Mathf.Abs(a.b - b.b) > tolerance;

    // 중심(px)에서 반지름이 [r0, r1) 안인 화소 가운데 두 사진이 다른 비율
    static float DiffFraction(Color32[] a, Color32[] b, int w, int h, Vector2 center, float r0, float r1, int tolerance = 30)
    {
        int total = 0, diff = 0;
        int x0 = Mathf.Max(0, (int)(center.x - r1)), x1 = Mathf.Min(w - 1, (int)(center.x + r1));
        int y0 = Mathf.Max(0, (int)(center.y - r1)), y1 = Mathf.Min(h - 1, (int)(center.y + r1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                if (d < r0 || d >= r1) continue;
                total++;
                if (Differs(a[y * w + x], b[y * w + x], tolerance)) diff++;
            }

        return total > 0 ? diff / (float)total : 0f;
    }

    // 사진기를 camPos에 붙잡고 같은 자리에서 켬·끔(·가림막 치움)을 찍어 화소를 비교한다. occluded = 가림이 있어야 하는 자리인가
    IEnumerator SeeThroughPair(BattleEntry entry, string label, string name, Vector3 camPos, bool? occluded, GameObject hideForTruth)
    {
        Camera cam = entry.BattleCamera;
        var pin = cam.gameObject.AddComponent<CameraPin>();
        pin.Target = cam;
        Vector3 feet = entry.Rig.HeroFeet;
        feet.y += BattleCourtyard.HeroLift(feet, entry.Sim.CastleHalf);
        Vector3 center = feet + Vector3.up * (BattleView.HeroDrawSize * 0.45f);
        pin.Position = camPos;
        pin.Rotation = Quaternion.LookRotation(center - camPos);
        float previousScale = entry.TutorialTimeScale;
        entry.TutorialTimeScale = 0f;   // 사진 사이에 전투가 흘러 장면이 달라지지 않게
        var history = new StringBuilder();
        for (int i = 0; i < 12; i++)
        {
            yield return null;
            if (entry.SeeThrough != null) history.Append(entry.SeeThrough.Occluded ? '1' : '0').Append(entry.SeeThrough.Strength.ToString("0.0")).Append(' ');
        }

        yield return new WaitForSecondsRealtime(0.4f);   // 켜짐은 실제 시간(0.15초)으로 변한다 — 배치 모드는 프레임이 아주 짧을 수 있어 실제 시간도 기다린다
        for (int i = 0; i < 3; i++) yield return null;
        Note($"{name}: 프레임별 가림·켜짐 {history} / 발 {entry.Rig.HeroFeet}");
        BattleSeeThrough.Enabled = true;
        yield return Capture($"seethrough_{label}_{name}_on", PfWidth, PfHeight, true);
        string onPath = Path.Combine(Out, $"{shot:00}_seethrough_{label}_{name}_on.png");
        float strengthOn = entry.SeeThrough != null ? entry.SeeThrough.Strength : -1f;
        bool occludedOn = entry.SeeThrough != null && entry.SeeThrough.Occluded;
        Renderer blocker = entry.SeeThrough?.LastOccluder;
        string blockerName = occludedOn && blocker != null ? $"{blocker.transform.parent?.name}/{blocker.name} {blocker.bounds}" : "-";

        BattleSeeThrough.Enabled = false;
        for (int i = 0; i < 6; i++) yield return null;
        yield return Capture($"seethrough_{label}_{name}_off", PfWidth, PfHeight, true);
        string offPath = Path.Combine(Out, $"{shot:00}_seethrough_{label}_{name}_off.png");
        BattleSeeThrough.Enabled = true;

        string truthPath = null;
        if (hideForTruth != null)
        {
            hideForTruth.SetActive(false);
            for (int i = 0; i < 4; i++) yield return null;
            yield return Capture($"seethrough_{label}_{name}_truth", PfWidth, PfHeight, true);
            truthPath = Path.Combine(Out, $"{shot:00}_seethrough_{label}_{name}_truth.png");
            hideForTruth.SetActive(true);
        }

        // 확인용 목록: 사진기→주인공 가슴 선이 지나는 모든 그림(MeshRenderer 아닌 것 포함)과 셰이더. 투시 셰이더가 아닌 것이 있으면 구멍이 안 뚫린다
        var segment = new Ray(camPos, (center - camPos).normalized);
        float segmentLength = (center - camPos).magnitude;
        foreach (Renderer rend in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!rend.enabled || !rend.gameObject.activeInHierarchy || !rend.bounds.IntersectRay(segment, out float hitAt) || hitAt > segmentLength) continue;
            Material rm = rend.sharedMaterial;
            Note($"  선 위 그림: {rend.transform.parent?.name}/{rend.name} ({rend.GetType().Name}) 셰이더 {(rm != null ? rm.shader.name : "-")} 표면 {(rm != null && rm.HasProperty("_Surface") ? rm.GetFloat("_Surface") : -1f)} 크기 {rend.bounds.size}");
        }

        // 구멍 자리: 주인공 가운데의 화면 좌표와 반지름 (화소)
        // 코루틴은 Update 단계라 사진기가 잠깐 원래 자리로 돌아와 있다 — 붙잡은 자리로 옮겨 계산하고 되돌린다
        Vector3 savedPos = cam.transform.position; Quaternion savedRot = cam.transform.rotation;
        cam.transform.SetPositionAndRotation(pin.Position, pin.Rotation);
        Vector3 vp = cam.WorldToViewportPoint(center);
        cam.transform.SetPositionAndRotation(savedPos, savedRot);
        float depth = vp.z;
        Color32[] on = LoadPng(onPath, out int w, out int h), off = LoadPng(offPath, out int w2, out int h2);
        Assert.AreEqual(w, w2); Assert.AreEqual(h, h2);
        Vector2 c = new Vector2(vp.x * w, vp.y * h);
        float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float radius = BattleSeeThrough.RadiusPerHeight * BattleView.HeroDrawSize / depth / tanHalf * (h * 0.5f);
        float inner = DiffFraction(on, off, w, h, c, 0f, radius * 0.4f);
        float ring = DiffFraction(on, off, w, h, c, radius * 1.35f, radius * 1.9f);
        Note($"{name}: 주인공 화면 ({c.x:0},{c.y:0}) 반지름 {radius:0}화소 / 켜짐 {strengthOn:0.00} 가림 {occludedOn} ({blockerName}) / 안쪽(켠·끈 사진) 다른 비율 {inner:0.00} / 바깥 고리 다른 비율 {ring:0.00}");
        bool expectHole = occluded ?? occludedOn;   // null = 기본 사진기 자리라 가림 여부는 자리에 따름 — 가렸으면 뚫려야 하고 아니면 아무것도 안 뚫려야
        if (expectHole)
        {
            if (occluded == true) Assert.IsTrue(occludedOn, $"{name}: 가리는 것을 알아본다");
            Assert.Greater(strengthOn, 0.95f, $"{name}: 다 켜졌다");
            Assert.Greater(inner, 0.5f, $"{name}: 주인공 둘레 화소가 가림막에서 뚫린 뒤 풍경으로 바뀐다");
            Assert.Less(ring, 0.05f, $"{name}: 구멍 밖은 그대로");
            if (truthPath != null)
            {
                Color32[] truth = LoadPng(truthPath, out _, out _);
                // 구멍 한가운데는 가림막이 없는 사진과 거의 같다 (그림자 차이는 허용)
                float differsFromTruth = DiffFraction(on, truth, w, h, c, 0f, radius * 0.4f);
                Note($"{name}: 안쪽이 '가림막 치운 사진'과 다른 비율 {differsFromTruth:0.00} (끈 사진과는 {inner:0.00})");
                Assert.Less(differsFromTruth, inner, $"{name}: 켜면 끈 사진보다 가림막 치운 사진에 가깝다");
            }
        }
        else
        {
            if (occluded == false) Assert.IsFalse(occludedOn, $"{name}: 가리는 것 없음");
            Assert.AreEqual(0f, strengthOn, 1e-4f, $"{name}: 안 켜진다");
            float all = DiffFraction(on, off, w, h, c, 0f, radius * 1.9f);
            Assert.Less(all, 0.02f, $"{name}: 가리는 것이 없으면 아무것도 안 뚫는다 (켠·끈 사진이 같다)");
        }

        Object.Destroy(pin);
        entry.TutorialTimeScale = previousScale;
        yield return null;
    }

    IEnumerator SeeThroughShots(TimeOfDay time, string label)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        BattleSeeThrough.Enabled = true;
        try
        {
            Screen.SetResolution(1920, 1080, false);
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

            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleSim sim = entry.Sim;
            BattleCameraRig rig = entry.Rig;
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = time; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            entry.StartBattle();
            entry.TutorialTimeScale = 0f;   // 걷고 찍는 동안 전투 시간을 멈춘다 (오래 걸으면 전투가 끝나 E가 안 먹는다). 주인공 이동은 실제 시간이라 그대로 걷는다
            for (int i = 0; i < 20; i++) yield return null;
            Camera cam = entry.BattleCamera;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;

            // 0) 가리는 것 없음: 처음 서는 자리의 기본 사진기. 켜도 끄도 같아야 한다
            Vector3 camDefault = cam.transform.position;
            yield return SeeThroughPair(entry, label, "a_wall_default_no_occluder", camDefault, false, null);

            // 1) 성벽 위를 옆으로 달려 예배당(북쪽 성벽 안쪽, 성벽 가운데에서 오른쪽 8m) 뒤에 선다. 사진기는 마당에서 건물 너머로 주인공을 본다
            rig.WalkInput = 1f;
            yield return WaitFor(() => rig.Along >= 7.9f, 8f);
            rig.WalkInput = 0f;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(0, rig.Side, "북쪽 성벽");
            Vector3 hero = rig.HeroFeet;
            GameObject buildings = GameObject.Find("Courtyard Buildings (W31 K-118)");
            Assert.IsNotNull(buildings, "마당 건물");
            // 건물 몸통과 사진기가 마당 쪽 안에 있도록 주인공에서 마당 쪽(-z)으로 6.5m, 위로 1.5m
            yield return SeeThroughPair(entry, label, "b_behind_courtyard_building", new Vector3(hero.x, top + 1.5f, hero.z - 6.5f), true, buildings);
            // 같은 자리에서 사진기를 더 낮게 — 지붕 선에 걸치는 각도
            yield return SeeThroughPair(entry, label, "c_behind_building_low", new Vector3(hero.x, top + 0.8f, hero.z - 6.0f), true, buildings);

            // 2) 원래 자리로 돌아와(가운데) 탑으로 내려가는 길 한가운데에서 탑 너머로 본다
            rig.ShowSide(0);
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            for (int i = 0; i < 10; i++) yield return null;
            entry.TutorialTimeScale = 1f;   // 내려가기(E)와 길 걷기는 전투 시간이 흘러야 한다
            Assert.IsTrue(entry.Interact(), "E: 탑으로 내려가기");
            yield return WaitFor(() => rig.HeroFeet.z <= 3.4f, 40f);   // 광장 가장자리 — 탑이 사진기(남쪽)와 주인공 사이
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(0.5f);
            hero = rig.HeroFeet;
            Note($"길 한가운데: blend {rig.TowerBlend:0.00} 발 ({hero.x:0.0}, {hero.z:0.0})");
            GameObject towerRoot = GameObject.Find("Beacon Tower (W27)");
            Assert.IsNotNull(towerRoot, "등대 탑");
            yield return SeeThroughPair(entry, label, "d_midtrip_behind_tower", new Vector3(hero.x, top + 1.3f, -3.5f), true, towerRoot);
            entry.TutorialTimeScale = 1f;

            // 3) 탑 앞 (등대): 평소 사진기(탑이 주인공 뒤), 그리고 북쪽에서 탑 너머로
            yield return WaitFor(() => rig.AtTower, 40f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착");
            for (int i = 0; i < 20; i++) yield return null;
            Vector3 camAtTower = cam.transform.position;
            yield return SeeThroughPair(entry, label, "e_at_lighthouse_default", camAtTower, null, null);
            hero = rig.HeroFeet;
            yield return SeeThroughPair(entry, label, "f_at_lighthouse_behind_tower", new Vector3(hero.x, top + 1.4f, hero.z + 5.5f), true, towerRoot);

            entry.TutorialTimeScale = 1f;
            entry.LeaveTower();
            yield return WaitFor(() => rig.OnWall, 40f);
            Note("투시 구멍 사진 촬영 완료");
        }
        finally
        {
            BattleSeeThrough.Enabled = true;
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"seethrough_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
