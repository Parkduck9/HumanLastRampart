using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 걷는 중 깜박임 점검 (W47, 사용자 2026-10-26 "플레이어가 성벽돌때 그림 겹쳐서 계속 z-파이팅"):
// 실제 이동 입력(WalkInput)으로 네 성벽·모서리를 한 바퀴 돌며 1m쯤마다 같은 프레임 안에서 사진기 근접면만 0.3% 바꿔 두 번 그린 뒤(화면 모양은 그대로, 깊이 값만 살짝 달라짐),
// 두 그림에서 다른 화소(= 깊이 겹침으로 이기는 면이 뒤바뀐 화소)를 센다. 겹친 면은 사진기가 조금만 움직여도 모양이 바뀐다.
// - W47FlickerWalk{Day,Night}: 통로 안쪽 간격 세 가지로 한 바퀴씩. 모든 표본의 깜박임 화소가 허용 이하임을 약속한다
// 결과: HLR_PLAY_OUT/w47_<label>_flicker.txt, 최악 표본 사진 NN_w47_*.png
public sealed partial class PlayProbe
{
    public const int FlickerMaxFullFrame = 300;   // 한 표본의 화면 전체 깜박임 화소 허용 (1920×1080 중 0.015%). 통로-마당 바닥 이음(동서 성벽 안쪽 가장자리)이 화면 맨 아래 줄에 걸릴 때 이음 틈이 가끔 100~150화소 반짝인다 — 건물 문제(5000화소 이상)와 구별되는 잔여
    public const int FlickerMaxNearHero = 12;    // 주인공 둘레 영역 허용

    // 위를 안 보는 면(세로·기울어진 면)의 같은 평면 겹침: 전투 장면 전체에서 0개 (W47 ZFightScan.ScanAnyOrientation). 걸을 때 사진기가 가까이 지나는 세로 면도 포함
    [UnityTest]
    public IEnumerator W47CoplanarWalls()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        try
        {
            yield return EnterBattle(TimeOfDay.Day);
            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            var stats = new StringBuilder();
            var findings = ZFightScan.ScanAnyOrientation(entry.Sim.CastleHalf + 8f, stats);
            Note(stats.ToString().TrimEnd());
            foreach (var f in findings.OrderByDescending(x => x.VisibleArea))
                Note($"{f.A}  <->  {f.B}  거리 {f.Dy * 100f:0.00}cm 겹침 {f.VisibleArea:0.00}m² 예: ({f.Where.x:0.0}, {f.Where.y:0.00}, {f.Where.z:0.0})");
            Note($"세로·기울어진 같은 평면 겹침 쌍 {findings.Count}개");
            File.WriteAllText(Path.Combine(Out, "w47_coplanar_scan.txt"), log.ToString(), new UTF8Encoding(false));
            Assert.AreEqual(0, findings.Count, "세로·기울어진 면이 같은 평면에서 겹치지 않는다 (깜박임 금지):\n" + log);
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
        }
    }
    [UnityTest, Timeout(3600000)]
    public IEnumerator W47FlickerWalkDay() { yield return W47FlickerWalk(TimeOfDay.Day, "day"); }

    [UnityTest, Timeout(3600000)]
    public IEnumerator W47FlickerWalkNight() { yield return W47FlickerWalk(TimeOfDay.Night, "night"); }

    // 주인공 둘레 12m 안의 세운 그림(주인공·마법사·병사·달리는 병사)끼리 같은 깊이에 겹치는 쌍 수 (ZFightScan.ScanBillboards)
    static int CountBillboardOverlaps(BattleEntry entry, Camera cam, BattleCameraRig rig, out string names)
    {
        var items = new List<Vector4>();
        var labels = new List<string>();
        Vector3 hero = rig.HeroFeet;
        items.Add(new Vector4(hero.x, hero.y, hero.z, BattleView.HeroDrawSize)); labels.Add("주인공");
        Vector3 wiz = entry.Sim.WizardFeet;
        items.Add(new Vector4(wiz.x, wiz.y, wiz.z, BattleView.HeroDrawSize)); labels.Add("마법사");
        var sim = entry.Sim;
        for (int i = 0; i < sim.SoldierCount; i++)
        {
            var inst = sim.SoldierInstances[i];
            if (inst.TintSize.w <= 0f) continue;
            Vector3 p = new Vector3(inst.PosFrame.x, inst.PosFrame.y, inst.PosFrame.z);
            if ((p - hero).sqrMagnitude > 144f) continue;
            items.Add(new Vector4(p.x, p.y, p.z, inst.TintSize.w)); labels.Add("병사" + i);
        }

        var pairs = ZFightScan.ScanBillboards(cam, items);
        lastBillboardItems = items.Count;
        var sb = new StringBuilder();
        foreach (var pr in pairs.Take(4)) sb.Append($"[{labels[pr.A]}~{labels[pr.B]} 깊이차 {pr.Depth * 100f:0.0}cm 옆 {pr.Lateral:0.00}m] ");
        names = sb.ToString();
        return pairs.Count;
    }

    static int lastBillboardItems;

    // 사진기가 건물(처마 포함 바닥 모양 안, 지붕 꼭대기 아래)에 들어가 있는가 — 근접면이 지붕을 가르는 별개 문제 (보고서에서 따로 다룬다)
    static bool CameraInsideBuilding(Vector3 p, float half)
    {
        float floor = BattleSim.WallHeight;
        foreach (var plot in BattleCourtyardBuildings.Plots)
            if (BattleCourtyardBuildings.Footprint(plot, half).Contains(new Vector2(p.x, p.z)) && p.y < floor + plot.TotalHeight + 0.2f) return true;
        foreach (var piece in BattleCourtyardBuildings.Pieces)
            if (BattleCourtyardBuildings.Footprint(piece, half).Contains(new Vector2(p.x, p.z)) && p.y < floor + piece.Y0 + piece.Height + 0.2f && piece.Height > 1f) return true;
        return false;
    }

    struct FlickerSample
    {
        public string Where;
        public int Full, Near;
        public int BlockX, BlockY;
        public Texture2D Shot, Mask;
    }

    IEnumerator W47FlickerWalk(TimeOfDay time, string label)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        BattleCameraRig rig = null;
        var worst = new List<FlickerSample>();
        FlickerSample buildingShot = default;
        bool shotSmithy = false, shotStable = false;
        int samples = 0, overFull = 0, overNear = 0, maxFull = 0, maxNear = 0, stackedTotal = 0, stackedMax = 0, stackedLogged = 0, inBuildingSamples = 0;
        float maxLift = 0f;
        try
        {
            yield return EnterBattle(time);
            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            rig = entry.Rig;
            Camera cam = entry.BattleCamera;
            // 대조: 사진기를 안 옮기고 두 번 그리면 같아야 한다 (두 번 그리기가 정상인지)
            {
                var control = new GameObject("W47 Flicker Control").AddComponent<FlickerShooter>();
                control.Setup(cam, rig.HeroFeet, 0f);
                for (int i = 0; i < 10 && !control.Done; i++) yield return null;
                Note($"대조(옮기지 않음): 화면 전체 깜박임 {control.Full}, 전체 화소 중 어두운 것 {control.DarkPercent:0.0}%");
                Assert.AreEqual(0, control.Full, "같은 자리에서 두 번 그리면 달라지지 않는다");
                Assert.Less(control.DarkPercent, 60f, "장면이 그려져 있다 (검은 화면이 아님)");
                Object.Destroy(control.gameObject);
            }

            float[] insets = { BattleCameraRig.HeroInset, BattleCameraRig.InsetMax, BattleCameraRig.InsetMin };
            for (int lap = 0; lap < insets.Length; lap++)
            {
                rig.ShowSide(0);
                for (int i = 0; i < 4; i++) yield return null;
                rig.DepthInput = insets[lap] > rig.Inset ? -1f : 1f;
                for (int i = 0; i < 80 && Mathf.Abs(rig.Inset - insets[lap]) > 0.02f; i++) yield return null;
                rig.DepthInput = 0f;
                for (int i = 0; i < 6; i++) yield return null;
                rig.WalkInput = 1f;
                int corners = 0, lastSide = rig.Side, frame = 0;
                for (; frame < 3000 && corners < 4; frame++)
                {
                    yield return null;
                    if (rig.Side != lastSide) { corners++; lastSide = rig.Side; }
                    if (frame % 2 != 0) continue;   // 20fps에서 2프레임 = 약 0.5m
                    var shooter = new GameObject("W47 Flicker").AddComponent<FlickerShooter>();
                    shooter.Setup(cam, rig.HeroFeet, 1f);
                    for (int i = 0; i < 10 && !shooter.Done; i++) yield return null;
                    maxLift = Mathf.Max(maxLift, rig.LastLift);
                    int stacked = CountBillboardOverlaps(entry, cam, rig, out string stackedNames);
                    stackedTotal += stacked; stackedMax = Mathf.Max(stackedMax, stacked);
                    if (stacked > 0 && stackedLogged++ < 12) Note($"  같은 깊이 겹침 {stacked}쌍 (사진기 {cam.transform.position}, 주인공 {rig.HeroFeet}): {stackedNames}");
                    string where = $"lap{lap} inset={rig.Inset:0.00} side={rig.Side} along={rig.Along:0.0} pose={rig.Pose} frame={frame}";
                    samples++;
                    bool inBuilding = CameraInsideBuilding(cam.transform.position, entry.Sim.CastleHalf);
                    if (inBuilding) { inBuildingSamples++; where += " 사진기가 건물 안"; }   // 고친 뒤에는 0이어야 한다 (사진기를 지붕 위로 올린다)
                    if (shooter.Full > FlickerMaxFullFrame) overFull++;
                    if (shooter.Near > FlickerMaxNearHero) overNear++;
                    maxFull = Mathf.Max(maxFull, shooter.Full); maxNear = Mathf.Max(maxNear, shooter.Near);
                    if (shooter.Full > 0 || shooter.Near > 0) Note($"{where} 전체 {shooter.Full} 주인공 둘레 {shooter.Near} 가장 몰린 칸 ({shooter.BlockX},{shooter.BlockY})");
                    if (shooter.Full > FlickerMaxFullFrame)
                    {
                        // 가장 몰린 칸이 가리키는 마당·통로 바닥 위 한 점 (원인 찾기용)
                        var ray = cam.ScreenPointToRay(new Vector3(shooter.BlockX + 32f, shooter.BlockY + 32f, 0f));
                        float planeY = BattleSim.WallHeight + 0.01f;
                        if (Mathf.Abs(ray.direction.y) > 1e-4f)
                        {
                            float tHit = (planeY - ray.origin.y) / ray.direction.y;
                            Note($"  몰린 칸의 바닥 위 점 {ray.origin + ray.direction * tHit} (사진기 {cam.transform.position})");
                        }
                    }

                    if (shooter.Full > 1000)
                    {
                        // 근접면에 걸리는 물체 찾기: 사진기에서 0.4m 안의 그리기 상자
                        Vector3 cp = cam.transform.position;
                        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                            if (r.enabled && r.gameObject.activeInHierarchy && r.bounds.SqrDistance(cp) < 0.16f)
                                Note($"  가까운 그리기: {r.transform.parent?.name}/{r.name} ({r.GetType().Name}) 사진기 {cp} 상자 {r.bounds.center} 크기 {r.bounds.size}");
                    }

                    if (inBuilding && buildingShot.Shot == null && shooter.Full > 1000)
                    {
                        buildingShot = new FlickerSample { Where = where, Full = shooter.Full, Near = shooter.Near, Shot = Object.Instantiate(shooter.ShotA) };   // 복사본: worst에서 밀려나 지워져도 안전
                    }

                    int score = shooter.Full + shooter.Near * 4;
                    if (worst.Count < 6 || score > worst.Min(w => w.Full + w.Near * 4))
                    {
                        worst.Add(new FlickerSample { Where = where, Full = shooter.Full, Near = shooter.Near, BlockX = shooter.BlockX, BlockY = shooter.BlockY, Shot = shooter.ShotA, Mask = shooter.MaskTex });
                        shooter.KeepTextures = true;
                        if (worst.Count > 6)
                        {
                            int drop = worst.IndexOf(worst.OrderBy(w => w.Full + w.Near * 4).First());
                            Object.Destroy(worst[drop].Shot); Object.Destroy(worst[drop].Mask);
                            worst.RemoveAt(drop);
                        }
                    }

                    Object.Destroy(shooter.gameObject);
                    // 건물 곁을 지날 때의 실제 화면 (대장간 서쪽 성벽 along≈5, 마구간 동쪽 성벽 along≈-5): 사진기가 지붕 위로 올라간 모습
                    if (lap == 0 && ((rig.Side == 3 && Mathf.Abs(rig.Along - 5f) < 0.8f && !shotSmithy) || (rig.Side == 1 && Mathf.Abs(rig.Along + 5f) < 0.8f && !shotStable)))
                    {
                        if (rig.Side == 3) shotSmithy = true; else shotStable = true;
                        yield return Capture($"w47_{label}_walk_past_{(rig.Side == 3 ? "smithy_west" : "stable_east")}", PfWidth, PfHeight);
                    }
                }

                rig.WalkInput = 0f;
                Note($"한 바퀴 {lap} (안쪽 간격 {insets[lap]:0.00}): 모서리 {corners}개 지남, 프레임 {frame}");
            }

            Note($"마지막 표본의 세운 그림 수 {lastBillboardItems}, 병사 {entry.Sim.SoldierCount}");
            Note($"세운 그림 같은 깊이 겹침: 표본 합계 {stackedTotal}쌍, 한 표본 최대 {stackedMax}쌍");
            Note($"사진기가 건물 모양 안에 든 표본 {inBuildingSamples}개 (0이어야 한다)");
            Note($"표본 {samples}개, 전체 허용({FlickerMaxFullFrame}) 초과 {overFull}개, 주인공 둘레 허용({FlickerMaxNearHero}) 초과 {overNear}개, 최대 전체 {maxFull} 둘레 {maxNear}");
            if (buildingShot.Shot != null)
            {
                File.WriteAllBytes(Path.Combine(Out, $"{++shot:00}_w47_{label}_camera_in_building_shot.png"), buildingShot.Shot.EncodeToPNG());
                Note($"사진기가 건물 안: {buildingShot.Where} 전체 {buildingShot.Full}");
            }

            int n = 0;
            foreach (var w in worst.OrderByDescending(x => x.Full + x.Near * 4))
            {
                if (w.Full == 0 && w.Near == 0) continue;
                File.WriteAllBytes(Path.Combine(Out, $"{++shot:00}_w47_{label}_worst{n}_shot.png"), w.Shot.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(Out, $"{shot:00}_w47_{label}_worst{n}_mask.png"), w.Mask.EncodeToPNG());
                Note($"최악 {n}: {w.Where} 전체 {w.Full} 둘레 {w.Near}");
                n++;
            }

            Note($"사진기 올림 최대 {maxLift:0.000}m");
            Assert.AreEqual(0f, maxLift, 1e-6f, "건물을 성벽에서 뗐으니 걷는 동안 안전망(사진기 올리기)이 한 번도 안 켜진다:\n" + log);
            Assert.AreEqual(0, inBuildingSamples, "걷는 동안 사진기가 건물(지붕 꼭대기 아래) 안에 들지 않는다:\n" + log);
            Assert.AreEqual(0, stackedTotal, "걷는 모든 표본에서 세운 그림끼리 같은 깊이 겹침(사진기가 움직이면 이기는 쪽이 뒤바뀜)이 없다:\n" + log);
            Assert.AreEqual(0, overFull, "걷는 모든 표본에서 화면 전체 깜박임 화소가 허용 이하:\n" + log);
            Assert.AreEqual(0, overNear, "걷는 모든 표본에서 주인공 둘레 깜박임 화소가 허용 이하:\n" + log);
        }
        finally
        {
            foreach (var w in worst) { if (w.Shot != null) Object.Destroy(w.Shot); if (w.Mask != null) Object.Destroy(w.Mask); }
            if (buildingShot.Shot != null) Object.Destroy(buildingShot.Shot);
            if (rig != null) { rig.WalkInput = 0f; rig.DepthInput = 0f; }
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w47_{label}_flicker.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    // 사진기 앞 몇 장면을 같은 프레임에 두 번 그려 비교하는 도구. 전투 갱신·그리기 예약이 끝난 뒤(LateUpdate 맨 끝)에 돈다
    [DefaultExecutionOrder(int.MaxValue)]
    sealed class FlickerShooter : MonoBehaviour
    {
        const int W = 1920, H = 1080, Tolerance = 24;   // 한 화소 색 차이 합(0~765)이 이보다 크면 다르다고 본다
        const float NearScale = 1.003f;   // 근접면만 0.3% 옮긴다: 화면 위 모양은 그대로(가장자리 안 움직임)이고 깊이 값만 살짝 달라져, 겹친 면이 이기는 쪽만 뒤바뀐다

        public bool Done, KeepTextures;
        public int Full, Near, BlockX, BlockY;
        public float DarkPercent;
        public Texture2D ShotA, MaskTex;
        Camera cam;
        Vector3 heroFeet;
        float nudge;

        public void Setup(Camera camera, Vector3 feet, float nudgeScale)
        {
            cam = camera; heroFeet = feet; nudge = nudgeScale;
        }

        void OnDestroy()
        {
            if (KeepTextures) return;
            if (ShotA != null) Object.Destroy(ShotA);
            if (MaskTex != null) Object.Destroy(MaskTex);
        }

        static Color32[] Render(Camera cam, RenderTexture rt, Texture2D tex)
        {
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = null;
            return tex.GetPixels32();
        }

        void LateUpdate()
        {
            if (Done || cam == null) return;
            Done = true;
            var rt = new RenderTexture(W, H, 24);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            Vector3 viewport = cam.WorldToViewportPoint(heroFeet + Vector3.up * 0.5f);   // 주인공 몸 가운데 (화면 위치)
            Color32[] shot = Render(cam, rt, tex);   // 사진은 그림자 있는 그대로
            ShotA = new Texture2D(W, H, TextureFormat.RGB24, false);
            ShotA.SetPixels32(shot); ShotA.Apply();
            // 그림자 판은 근접면이 바뀌면 칸 나눔이 달라져 그림자 가장자리가 흔들린다 (깊이 겹침과 무관) — 비교하는 두 장은 그림자를 끄고 그린다
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var shadowModes = new LightShadows[lights.Length];
            for (int i = 0; i < lights.Length; i++) { shadowModes[i] = lights[i].shadows; lights[i].shadows = LightShadows.None; }
            Color32[] a = Render(cam, rt, tex);
            float near = cam.nearClipPlane;
            cam.nearClipPlane = near * Mathf.Lerp(1f, NearScale, nudge);
            Color32[] b = Render(cam, rt, tex);
            cam.nearClipPlane = near;
            for (int i = 0; i < lights.Length; i++) lights[i].shadows = shadowModes[i];
            Object.Destroy(rt);
            Object.Destroy(tex);

            int dark = 0;
            for (int i = 0; i < a.Length; i += 16) if (a[i].r + a[i].g + a[i].b < 30) dark++;
            DarkPercent = 100f * dark / (a.Length / 16);

            var flags = new Color32[a.Length];
            var differs = new bool[a.Length];
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    differs[i] = Diff(a[i], b[i]) > Tolerance;
                    flags[i] = new Color32((byte)(a[i].r / 3), (byte)(a[i].g / 3), (byte)(a[i].b / 3), 255);
                }

            // 가늘고 긴 줄(근접면에 잘린 가장자리·삼각형 이음)은 깜박임이 아니다: 덩어리(8방향 이웃)의 상자가 3화소 이상 두껍고 6화소 이상일 때만 센다
            var blockCount = new int[(W / 64 + 1) * (H / 64 + 1)];
            int nearX0 = (int)(viewport.x * W) - 260, nearX1 = nearX0 + 520, nearY0 = (int)(viewport.y * H) - 300, nearY1 = nearY0 + 600;
            var stack = new Stack<int>();
            var members = new List<int>();
            for (int s = 0; s < differs.Length; s++)
            {
                if (!differs[s]) continue;
                members.Clear();
                stack.Push(s);
                differs[s] = false;
                int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    members.Add(i);
                    int x = i % W, y = i / W;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 1 || ny < 1 || nx >= W - 1 || ny >= H - 1) continue;
                            int n = ny * W + nx;
                            if (!differs[n]) continue;
                            differs[n] = false;
                            stack.Push(n);
                        }
                }

                bool thin = Mathf.Min(maxX - minX + 1, maxY - minY + 1) < 3 || members.Count < 6;
                foreach (int i in members)
                {
                    int x = i % W, y = i / W;
                    if (thin) { flags[i] = new Color32(90, 90, 0, 255); continue; }
                    Full++;
                    blockCount[(y / 64) * (W / 64 + 1) + x / 64]++;
                    if (x >= nearX0 && x < nearX1 && y >= nearY0 && y < nearY1) Near++;
                    flags[i] = new Color32(255, 0, 255, 255);
                }
            }
            int best = 0;
            for (int i = 1; i < blockCount.Length; i++) if (blockCount[i] > blockCount[best]) best = i;
            BlockX = (best % (W / 64 + 1)) * 64; BlockY = (best / (W / 64 + 1)) * 64;
            MaskTex = new Texture2D(W, H, TextureFormat.RGB24, false);
            MaskTex.SetPixels32(flags); MaskTex.Apply();
        }

        static int Diff(Color32 p, Color32 q) => Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b);
    }
}
