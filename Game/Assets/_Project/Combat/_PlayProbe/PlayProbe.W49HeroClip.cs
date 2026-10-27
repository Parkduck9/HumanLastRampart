using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.CombatContracts;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

// 걷는 중 주인공이 잘려 보임 점검 (W49, 사용자 1.0 시험 "플레이어 움직일때 짤리는거같은데"):
// 실제 이동 입력(WalkInput)으로 네 성벽·모서리·계단 층계참을 양쪽 방향으로 돌며, 매 표본마다 같은 프레임 안에서 네 번 그린다.
//   A 장면 그대로 / B 주인공만 숨김 / C2 주인공 층(30)만 빈 화면 / C1 같은 층에 주인공 그림만 (가려지지 않은 '완전한' 모양)
// 보이는 화소 = (C1과 C2가 다른 화소: 완전한 모양) 중 (A와 B가 다른 화소: 실제 화면에서 주인공이 이긴 화소). 가림 비율 = 1 - 보이는 / 완전한.
// 시트 쪽은 EditMode 시험 HeroSheetEdgeTests가 따로 본다 (칸 안에서 그림이 옆으로 싹둑 잘린 칸).
// 결과: HLR_PLAY_OUT/w49_hero_clip.txt, 최악 표본 확대 사진 NN_w49_*.png
public sealed partial class PlayProbe
{
    public const float HeroClipMaxHidden = 0.10f;     // 한 표본에서 주인공 모양 중 다른 것(바닥·병사 그림)에 가려진 화소 비율 허용. 고치기 전 최대 63%, 고친 뒤 약 6%(옆 병사가 가장자리만 겹침)
    public const int HeroClipMaxSideMissing = 8;      // 보이는 모양의 옆 끝이 완전한 모양보다 안쪽으로 들어간 화소 허용 (고치기 전·후 모두 0~4, 병사 가림 빼고 월드만 본다)

    [UnityTest, Timeout(3600000)]
    public IEnumerator W49HeroClipWalkDay() { yield return W49HeroClipWalk(TimeOfDay.Day, "day"); }

    struct HeroClipStat
    {
        public int Samples;
        public float MaxHidden, SumHidden;
        public int MaxBottomMissing, MaxSideMissing;
        public int MinFull, MaxFull;
    }

    static T ReadField<T>(object owner, string name)
    {
        var f = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, $"{owner.GetType().Name}.{name} 필드를 찾는다");
        return (T)f.GetValue(owner);
    }

    IEnumerator W49HeroClipWalk(TimeOfDay time, string label)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        BattleCameraRig rig = null;
        var stats = new Dictionary<string, HeroClipStat>();
        var worst = new Dictionary<string, (float hidden, Texture2D crop, string where)>();
        int samples = 0, overHidden = 0, overSide = 0, liftSamples = 0, holeSamples = 0;
        float maxHidden = 0f, maxWorldHidden = 0f;
        try
        {
            yield return EnterBattle(time);
            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            rig = entry.Rig;
            Camera cam = entry.BattleCamera;
            object view = ReadField<object>(entry, "view");
            float[] insets = { BattleCameraRig.HeroInset, BattleCameraRig.InsetMax, BattleCameraRig.InsetMin };
            for (int lap = 0; lap < insets.Length * 2; lap++)
            {
                float dir = lap % 2 == 0 ? 1f : -1f;
                float inset = insets[lap / 2];
                rig.ShowSide(0);
                for (int i = 0; i < 4; i++) yield return null;
                rig.DepthInput = inset > rig.Inset ? -1f : 1f;
                for (int i = 0; i < 80 && Mathf.Abs(rig.Inset - inset) > 0.02f; i++) yield return null;
                rig.DepthInput = 0f;
                for (int i = 0; i < 6; i++) yield return null;
                rig.WalkInput = dir;
                int corners = 0, lastSide = rig.Side, frame = 0;
                for (; frame < 3000 && corners < 4; frame++)
                {
                    yield return null;
                    if (rig.Side != lastSide) { corners++; lastSide = rig.Side; }
                    if (frame % 3 != 0) continue;
                    var shooter = new GameObject("W49 HeroClip").AddComponent<HeroClipShooter>();
                    shooter.Setup(cam, view, entry.Sim);
                    for (int i = 0; i < 10 && !shooter.Done; i++) yield return null;
                    if (shooter.Full > 0 && shooter.Key != null)
                    {
                        samples++;
                        string where = $"lap{lap} dir={dir:+0;-0} inset={rig.Inset:0.00} side={rig.Side} along={rig.Along:0.0} pose={rig.Pose} frame={frame} 칸={shooter.Key}";
                        float hidden = 1f - shooter.Visible / (float)shooter.Full;
                        float hiddenAll = 1f - shooter.VisibleAll / (float)shooter.Full;
                        stats.TryGetValue(shooter.Key, out var st);
                        st.Samples++; st.SumHidden += hiddenAll; st.MaxHidden = Mathf.Max(st.MaxHidden, hiddenAll);
                        st.MaxBottomMissing = Mathf.Max(st.MaxBottomMissing, shooter.BottomMissing);
                        st.MaxSideMissing = Mathf.Max(st.MaxSideMissing, shooter.SideMissing);
                        st.MinFull = st.Samples == 1 ? shooter.Full : Mathf.Min(st.MinFull, shooter.Full);
                        st.MaxFull = Mathf.Max(st.MaxFull, shooter.Full);
                        stats[shooter.Key] = st;
                        if (hiddenAll > maxHidden) maxHidden = hiddenAll;
                        if (hidden > maxWorldHidden) maxWorldHidden = hidden;
                        if (hiddenAll > HeroClipMaxHidden || hidden > HeroClipMaxHidden) overHidden++;
                        if (shooter.SideMissing > HeroClipMaxSideMissing) overSide++;
                        if (BattleCourtyard.HeroLift(rig.HeroFeet, entry.Sim.CastleHalf) > 0.001f) liftSamples++;
                        if ((entry.SeeThrough != null ? entry.SeeThrough.Strength : 0f) > 0f) holeSamples++;
                        if (hiddenAll > 0.03f || shooter.SideMissing > 0)
                            Note($"{where} 완전 {shooter.Full} 보임 {shooter.Visible} (병사 포함 {shooter.VisibleAll}) 가림 {hiddenAll * 100f:0.0}% (바닥·난간·건물만 {hidden * 100f:0.0}%) 아래줄 {shooter.BottomMissing} 옆줄 {shooter.SideMissing} 구멍 {(entry.SeeThrough != null ? entry.SeeThrough.Strength : 0f):0.00}");
                        if (!worst.TryGetValue(shooter.Key, out var w) || hiddenAll > w.hidden)
                        {
                            if (w.crop != null) Object.Destroy(w.crop);
                            worst[shooter.Key] = (hiddenAll, shooter.Crop, where);
                            shooter.KeepCrop = true;
                        }
                    }
                    Object.Destroy(shooter.gameObject);
                }

                rig.WalkInput = 0f;
                Note($"한 바퀴 {lap} (방향 {dir:+0;-0}, 안쪽 간격 {inset:0.00}): 모서리 {corners}개 지남, 프레임 {frame}");
            }

            foreach (var kv in stats.OrderBy(k => k.Key))
                Note($"칸 {kv.Key}: 표본 {kv.Value.Samples} 완전한 화소 {kv.Value.MinFull}~{kv.Value.MaxFull} 가림 평균 {kv.Value.SumHidden / kv.Value.Samples * 100f:0.0}% 최대 {kv.Value.MaxHidden * 100f:0.0}% 아래줄 최대 {kv.Value.MaxBottomMissing} 옆줄 최대 {kv.Value.MaxSideMissing}");
            Note($"표본 {samples}개 (계단 층계참 위 {liftSamples}, 투시 구멍 켜짐 {holeSamples}), 가림 허용({HeroClipMaxHidden * 100f:0}%) 초과 {overHidden}개, 옆 잘림 허용({HeroClipMaxSideMissing}) 초과 {overSide}개, 최대 가림 {maxHidden * 100f:0.0}% (바닥·난간·건물만 {maxWorldHidden * 100f:0.0}%)");
            int n = 0;
            foreach (var w in worst.OrderByDescending(x => x.Value.hidden).Take(8))
            {
                File.WriteAllBytes(Path.Combine(Out, $"{++shot:00}_w49_{label}_worst{n}_{w.Key.Replace('/', '_')}.png"), w.Value.crop.EncodeToPNG());
                Note($"최악 {n}: {w.Value.where} 가림 {w.Value.hidden * 100f:0.0}%");
                n++;
            }

            Assert.AreEqual(0, overHidden, "걷는 모든 표본에서 주인공이 바닥·난간·건물·병사 그림에 가려 잘리는 화소 비율이 허용 이하:\n" + log);
            Assert.AreEqual(0, overSide, "걷는 모든 표본에서 주인공 옆모습이 칸 모서리에서 잘리지 않는다:\n" + log);
        }
        finally
        {
            foreach (var w in worst) if (w.Value.crop != null) Object.Destroy(w.Value.crop);
            if (rig != null) { rig.WalkInput = 0f; rig.DepthInput = 0f; }
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w49_{label}_hero_clip.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    // 같은 프레임에 네 번 그려 주인공의 완전한 모양과 실제로 보이는 모양을 비교한다 (전투 갱신·그리기 예약이 끝난 LateUpdate 맨 끝)
    [DefaultExecutionOrder(int.MaxValue)]
    sealed class HeroClipShooter : MonoBehaviour
    {
        const int W = 1920, H = 1080, Tolerance = 12, HeroLayer = 30, Pad = 300;

        public bool Done, KeepCrop;
        public int Full, Visible, VisibleAll, BottomMissing, SideMissing;   // Visible = 다른 그림 없이 / VisibleAll = 병사·마법사 포함 실제 화면
        public RectInt FullRect;
        public string Key;
        public Texture2D Crop;
        Camera cam;
        object view;

        BattleSim sim;

        public void Setup(Camera camera, object battleView, BattleSim battleSim) { cam = camera; view = battleView; sim = battleSim; }

        void OnDestroy()
        {
            if (!KeepCrop && Crop != null) Object.Destroy(Crop);
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

        static int Diff(Color32 p, Color32 q) => Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b);

        void LateUpdate()
        {
            if (Done || cam == null) return;
            Done = true;
            var instances = ReadField<PerfLabInstance[]>(view, "heroInstance");
            var buffer = ReadField<GraphicsBuffer>(view, "heroBuffer");
            var quad = ReadField<Mesh>(view, "quad");
            var material = ReadField<Material>(view, "material");
            var bounds = ReadField<Bounds>(view, "bounds");
            var block = ReadField<MaterialPropertyBlock>(view, "heroBlock");   // 걷기·달리기 시트 (서 있기·손짓 시트는 이 시험이 보지 않는다)
            bool visible = ReadField<bool>(view, "heroVisible"), idle = ReadField<bool>(view, "heroIdle");
            int command = ReadField<int>(view, "heroCommandFrame");
            if (!visible || idle || command >= 0) return;
            float w = instances[0].PosFrame.w;
            Key = (w < 0f ? "L" : "R") + "/" + ((int)(w < 0f ? -w - 1f : w)).ToString("00");

            var rt = new RenderTexture(W, H, 24);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            Vector3 vp = cam.WorldToViewportPoint(new Vector3(instances[0].PosFrame.x, instances[0].PosFrame.y + 0.4f, instances[0].PosFrame.z));
            int cx = Mathf.Clamp((int)(vp.x * W), 0, W - 1), cy = Mathf.Clamp((int)(vp.y * H), 0, H - 1);
            int x0 = Mathf.Max(0, cx - Pad), x1 = Mathf.Min(W, cx + Pad), y0 = Mathf.Max(0, cy - Pad), y1 = Mathf.Min(H, cy + Pad);

            Color32[] a = Render(cam, rt, tex);                       // A: 장면 그대로
            buffer.SetData(new PerfLabInstance[1]);                   // 크기 0 = 안 그림
            Color32[] b = Render(cam, rt, tex);                       // B: 주인공만 숨김
            // 병사·달리는 병사·마법사도 숨기고 한 번 더 (B2 = 아무도 없음, A2 = 주인공만): 다른 그림 말고 바닥·난간·건물에 가린 것만 센다
            var soldierDraws = ReadField<List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)>>(view, "soldierDraws");
            var runnerDraws = ReadField<List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)>>(view, "runnerDraws");
            var wizardBuffer = ReadField<GraphicsBuffer>(view, "wizardBuffer");
            var wizardInstance = ReadField<PerfLabInstance[]>(view, "wizardInstance");
            foreach (var d in soldierDraws) d.buffer.SetData(new PerfLabInstance[d.count], 0, 0, d.count);
            foreach (var d in runnerDraws) d.buffer.SetData(new PerfLabInstance[d.count], 0, 0, d.count);
            if (wizardBuffer != null) wizardBuffer.SetData(new PerfLabInstance[1]);
            Color32[] b2 = Render(cam, rt, tex);
            buffer.SetData(instances);
            Color32[] a2 = Render(cam, rt, tex);
            foreach (var d in soldierDraws) d.buffer.SetData(sim.SoldierInstances, d.start, 0, d.count);
            foreach (var d in runnerDraws) d.buffer.SetData(sim.RunnerInstances, d.start, 0, d.count);
            if (wizardBuffer != null) wizardBuffer.SetData(wizardInstance);
            int mask = cam.cullingMask; var flags = cam.clearFlags; Color bg = cam.backgroundColor;
            cam.cullingMask = 1 << HeroLayer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            Color32[] c2 = Render(cam, rt, tex);                      // C2: 주인공 층만 — 빈 화면
            buffer.SetData(instances);
            // 주의: 이 그리기는 그 프레임 끝까지 남는다. 전투 사진기는 30번 층을 안 그리므로 장면에는 안 보이고, 이 도구는 그 프레임 안에서만 쓴다 (점검 전용)
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, 1, block, ShadowCastingMode.Off, false, HeroLayer);
            Color32[] c1 = Render(cam, rt, tex);                      // C1: 주인공 그림만 — 가려지지 않은 완전한 모양
            cam.cullingMask = mask; cam.clearFlags = flags; cam.backgroundColor = bg;
            Object.Destroy(rt);
            Object.Destroy(tex);

            int fMinX = int.MaxValue, fMaxX = -1, fMinY = int.MaxValue, fMaxY = -1, vMinX = int.MaxValue, vMaxX = -1, vMinY = int.MaxValue, vMaxY = -1;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = y * W + x;
                    if (Diff(c1[i], c2[i]) <= Tolerance) continue;
                    Full++;
                    if (x < fMinX) fMinX = x; if (x > fMaxX) fMaxX = x; if (y < fMinY) fMinY = y; if (y > fMaxY) fMaxY = y;
                    if (Diff(a[i], b[i]) > Tolerance) VisibleAll++;
                    if (Diff(a2[i], b2[i]) <= Tolerance) continue;
                    Visible++;
                    if (x < vMinX) vMinX = x; if (x > vMaxX) vMaxX = x; if (y < vMinY) vMinY = y; if (y > vMaxY) vMaxY = y;
                }

            if (Full == 0) return;
            FullRect = new RectInt(fMinX, fMinY, fMaxX - fMinX + 1, fMaxY - fMinY + 1);
            if (Visible == 0) { BottomMissing = fMaxY - fMinY + 1; SideMissing = fMaxX - fMinX + 1; }
            else
            {
                BottomMissing = vMinY - fMinY;                                        // 이미지 y는 위로 커진다: 아래 가장자리 = 최소 y
                SideMissing = Mathf.Max(vMinX - fMinX, fMaxX - vMaxX, 0);
            }

            // 확대 사진: 완전한 모양 상자 둘레 (왼쪽 = 장면 그대로, 오른쪽 = 완전한 모양만, 바닥·난간·건물에 가린 화소 빨강, 병사·마법사에 가린 화소 파랑)
            int cw = fMaxX - fMinX + 1 + 24, ch = fMaxY - fMinY + 1 + 24, cx0 = Mathf.Max(0, fMinX - 12), cy0 = Mathf.Max(0, fMinY - 12);
            cw = Mathf.Min(cw, W - cx0); ch = Mathf.Min(ch, H - cy0);
            const int Zoom = 3;
            Crop = new Texture2D(cw * Zoom * 2 + 8, ch * Zoom, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
            var px = new Color32[Crop.width * Crop.height];
            for (int y = 0; y < ch * Zoom; y++)
                for (int x = 0; x < cw * Zoom * 2 + 8; x++)
                {
                    bool right = x >= cw * Zoom + 8;
                    if (x >= cw * Zoom && !right) { px[y * Crop.width + x] = new Color32(40, 40, 40, 255); continue; }
                    int sx = cx0 + (right ? x - cw * Zoom - 8 : x) / Zoom, sy = cy0 + y / Zoom, si = sy * W + sx;
                    if (!right) { px[y * Crop.width + x] = a[si]; continue; }
                    bool inFull = Diff(c1[si], c2[si]) > Tolerance;
                    bool shown = inFull && Diff(a2[si], b2[si]) > Tolerance;
                    bool shownAll = inFull && Diff(a[si], b[si]) > Tolerance;
                    px[y * Crop.width + x] = !inFull ? new Color32(90, 90, 90, 255) : shown ? (shownAll ? c1[si] : new Color32(0, 90, 255, 255)) : new Color32(255, 0, 0, 255);
                }

            Crop.SetPixels32(px); Crop.Apply();
        }
    }
}
