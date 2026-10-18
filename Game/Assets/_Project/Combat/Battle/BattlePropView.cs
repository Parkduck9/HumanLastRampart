using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽 난간·대포 그림 (미리 연결, 사용자 검토 전 — Codex 시안 wall_a·대포 사격구·대포 3방향)
    // - 성벽: 성벽 윗면 바깥 모서리에 난간 그림 판을 성벽 방향으로 세운다(카메라를 따라 돌지 않음). 윗면 아래 부분은 성벽 상자에 가려진다
    // - 대포 자리: 가운데가 낮은 사격구 판, 그 앞에 대포. 조준점이 왼쪽/오른쪽이면 그쪽 그림, 없으면 가운데
    // - 그림이 없으면(Resources/BattleArt) 그리지 않는다 — 예전 회색 상자만
    // - 대포 반동·포구 연기 (K-031, Codex K-016 시안): Cannon/cannon_recoil(평상→반동→복귀 3칸)이 있으면 3방향 그림 대신 쓴다.
    //   쏜 순간(대포 병사의 마지막 사격 시각, BattleSim.CannonFireAge — K-036)부터 반동 2칸을 보이고, 포구 앞에 Cannon/muzzle_smoke 6칸을 한 번 재생한다
    //   반동 그림은 바깥을 향한 한 방향뿐이라 좌우 조준 그림은 없다
    public sealed class BattlePropView : IDisposable
    {
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int FixedRightId = Shader.PropertyToID("_FixedRight");

        const float ModuleWidth = 2.7f;                       // 난간 한 판 폭(m) — 병사(0.35m)가 허리 높이 돌출부 뒤에 서는 크기
        const float WallPivotY = (512f - 182f) / 512f;        // 그림 속 통로 윗면 줄 = 성벽 윗면
        const float WallInset = 0.12f;                        // 바깥 모서리에서 안쪽으로
        const float CannonSize = 0.45f;
        const float CannonInset = 0.25f;                      // 운용병(0.5m)보다 바깥
        const float AimSideThreshold = 0.2f;                  // 좌우 성분이 이보다 크면 옆 그림 (약 12도)

        // 반동 대포 (K-016 cannon_recoil: 칸 256×256, 바퀴 접지 (128,240), 10fps)
        // K-060: 보이는 폭 = 병사 키 × 1.15 (Codex K-052 측정 — 끄는 대포와 같은 축척) → 칸 높이 약 0.44m. 예전 0.6(보이는 폭 0.55m).
        // 사격구 폭 1.12m 안, 운용병 2명(대포 자리 ±0.45m)과 겹치지 않는다 (대포 반폭 0.2m + 병사 반폭 약 0.12m)
        const float SoldierHeight = 1.4f * BattleSim.WallFigureScale;   // 병사 키 0.35m (BattleSim.SoldierDrawSize와 같다)
        static readonly float RecoilCannonSize = CannonArt.InstalledWidthPerSoldier * SoldierHeight / CannonArt.RecoilVisibleWidthFraction;
        // 위에서 내려다볼 때 납작해지는 것(캐릭터는 셰이더 _ViewStretch로 보정)을 대포도 같게 — 보정이 없어 작은 검은 점처럼 보였다 (K-060)
        static readonly int ViewStretchId = Shader.PropertyToID("_ViewStretch");
        const float RecoilFrameSeconds = 0.15f;               // 반동 1칸·복귀 1칸을 이만큼씩 (K-036: 0.1초는 눈에 안 띄어 늘림)
        // 포구 연기 (K-016 muzzle_smoke: 칸 256×256 6칸, 원점 (128,240) — 시안 10fps를 조금 느리게 한 번)
        const int SmokeFrames = 6;
        const float SmokeFps = 8f;
        const float SmokeSize = 1.2f;                         // K-036: 0.7m는 사진기 거리에서 안 보였다
        const float SmokeForward = 0.45f;                     // 대포 자리에서 바깥쪽으로 (포구)
        const float SmokeLift = 0.12f;                        // 통로 위 (포구 높이쯤)

        readonly BattleSim sim;
        readonly Material material;
        readonly Mesh quad;
        readonly Bounds bounds;
        readonly List<Piece> wallDraws = new List<Piece>();
        readonly GraphicsBuffer cannonBuffer;
        readonly MaterialPropertyBlock cannonBlock;
        readonly PerfLabInstance[] cannonInstances;
        float lastBrightness = -1f;
        readonly bool recoilArt;                              // cannon_recoil이 있으면 true
        readonly GraphicsBuffer smokeBuffer;
        readonly MaterialPropertyBlock smokeBlock;
        readonly PerfLabInstance[] smokeInstances;

        // 대포 끌기 (K-060, Codex K-052): 당기는 병사·미는 병사·포가·바퀴 (sim.CannonHauls마다 하나씩)
        static readonly int SheetScaleId = Shader.PropertyToID("_SheetScale");
        static readonly int ShotModeId = Shader.PropertyToID("_ShotMode");
        static readonly int ShotColorId = Shader.PropertyToID("_ShotColor");
        const float HaulFps = 8f;   // 끄는 걸음 칸 속도 — 이동 속도는 칸마다 한 걸음(32px)이라 HaulSpeed와 짝 (BattleSim.HaulSpeed)
        sealed class HaulKind
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public PerfLabInstance[] Instances;
            public int Count;
        }

        readonly HaulKind haulPull, haulPush, haulChassis, haulWheel;

        sealed class Piece
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public Vector3[] Feet;
            public PerfLabInstance[] Instances;
        }

        public BattlePropView(BattleSim sim, Material material, Mesh quad, Bounds bounds)
        {
            this.sim = sim;
            this.material = material;
            this.quad = quad;
            this.bounds = bounds;

            // 3D 성벽 목업(BattleWorldView)이 켜져 있으면 난간 그림 판은 그리지 않는다 (대포 그림은 그대로)
            var wall = BattleWorldView.Active ? null : Resources.Load<Texture2D>("BattleArt/wall_a");
            var bay = BattleWorldView.Active ? null : Resources.Load<Texture2D>("BattleArt/wall_cannon_bay");
            var recoil = Resources.Load<Texture2D>("BattleArt/Cannon/cannon_recoil");
            var cannon = recoil != null ? recoil : Resources.Load<Texture2D>("BattleArt/cannon_aim3");
            recoilArt = recoil != null;
            float half = sim.CastleHalf;

            // 성벽마다 대포 사격구 자리(성벽 따라 m) — 빈 사격구 포함 (K-060: 대포를 옮겨 받을 자리)
            var bays = new List<float>[4];
            for (int s = 0; s < 4; s++) bays[s] = new List<float>(sim.CannonBays(s));

            var wallLayout = PerfLabArt.SheetLayout.Make(1, 1, 0.5f, WallPivotY, 2f);
            for (int side = 0; side < 4; side++)
            {
                float3 n = WallGeometry.Outward(side);
                float3 r = WallGeometry.Right(side);
                Vector3 Feet(float along) => new Vector3(n.x * (half - WallInset) + r.x * along, BattleSim.WallHeight, n.z * (half - WallInset) + r.z * along);

                if (wall != null)
                {
                    var feet = new List<Vector3>();
                    int modules = Mathf.CeilToInt(half * 2f / ModuleWidth);
                    float step = half * 2f / modules;
                    for (int m = 0; m < modules; m++)
                    {
                        float along = -half + (m + 0.5f) * step;
                        bool nearBay = bay != null && bays[side].Exists(b => Mathf.Abs(b - along) < ModuleWidth * 0.75f);
                        if (!nearBay) feet.Add(Feet(along));
                    }

                    wallDraws.Add(MakeDraw(wall, wallLayout, r, feet));
                }

                if (bay != null && bays[side].Count > 0)
                {
                    var feet = new List<Vector3>();
                    foreach (float b in bays[side]) feet.Add(Feet(b));
                    wallDraws.Add(MakeDraw(bay, wallLayout, r, feet));
                }
            }

            if (cannon != null && sim.CannonCount > 0)
            {
                cannonBlock = new MaterialPropertyBlock();
                Apply(cannonBlock, cannon, recoilArt
                    ? PerfLabArt.SheetLayout.Make(3, 1, 0.5f, (256f - 240f) / 256f, 1f)
                    : PerfLabArt.SheetLayout.Make(3, 1, 0.5f, 32f / 384f, 512f / 384f));
                cannonInstances = new PerfLabInstance[sim.CannonCount];
                cannonBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sim.CannonCount, PerfLabInstance.Stride);
                cannonBlock.SetBuffer(InstancesId, cannonBuffer);
                cannonBlock.SetFloat(ViewStretchId, 1f);   // 병사와 같은 납작함 보정 (K-060)

                // 대포 끌기 그림 (K-060). 그림이 하나라도 없으면 끌기 그림은 그리지 않는다
                int hauls = Mathf.Max(1, sim.CannonCount);
                float crewScale = PerfLabArt.CellHeight / CannonArt.CrewFigurePx;   // 칸 384 중 인물 210 → 크기 값 = 인물 키
                var pullSheet = Resources.Load<Texture2D>(CannonArt.PullSheet);
                var pushSheet = Resources.Load<Texture2D>(CannonArt.PushSheet);
                var chassis = Resources.Load<Texture2D>(CannonArt.HaulChassis);
                var wheel = Resources.Load<Texture2D>(CannonArt.HaulWheel);
                if (pullSheet != null && pushSheet != null && chassis != null && wheel != null)
                {
                    haulPull = MakeHaul(pullSheet, PerfLabArt.FromSheet(pullSheet), crewScale, hauls);
                    haulPush = MakeHaul(pushSheet, PerfLabArt.FromSheet(pushSheet), crewScale, hauls);
                    haulChassis = MakeHaul(chassis, PerfLabArt.SheetLayout.Make(1, 1, CannonArt.HaulPivotXPx / CannonArt.HaulCellWidthPx,
                        (CannonArt.HaulCellHeightPx - CannonArt.HaulPivotYPx) / CannonArt.HaulCellHeightPx, CannonArt.HaulCellWidthPx / CannonArt.HaulCellHeightPx), 1f, hauls);
                    haulWheel = MakeHaul(wheel, PerfLabArt.SheetLayout.Make(1, 1, 0.5f, 0.5f, 1f), 1f, hauls);
                    haulWheel.Block.SetFloat(ShotModeId, 2f);   // 바퀴는 개체 색 칸(rgb)을 방향으로 보고 돌린다 (투사체와 같은 셰이더 기능)
                    // 포가·바퀴는 납작함 보정을 끈다 — 보정은 판마다 기준점에서 늘려 바퀴 중심과 포가 바퀴 자리가 어긋난다 (병사만 보정)
                    haulWheel.Block.SetFloat(ViewStretchId, 0f);
                    haulChassis.Block.SetFloat(ViewStretchId, 0f);
                }

                if (recoilArt)
                {
                    var smoke = Resources.Load<Texture2D>("BattleArt/Cannon/muzzle_smoke");
                    if (smoke != null)
                    {
                        smokeBlock = new MaterialPropertyBlock();
                        Apply(smokeBlock, smoke, PerfLabArt.SheetLayout.Make(SmokeFrames, 1, 0.5f, (256f - 240f) / 256f, 1f));
                        smokeInstances = new PerfLabInstance[sim.CannonCount];
                        smokeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sim.CannonCount, PerfLabInstance.Stride);
                        smokeBlock.SetBuffer(InstancesId, smokeBuffer);
                    }
                }
            }
        }

        HaulKind MakeHaul(Texture2D texture, PerfLabArt.SheetLayout layout, float sheetScale, int capacity)
        {
            var kind = new HaulKind { Block = new MaterialPropertyBlock(), Instances = new PerfLabInstance[capacity] };
            Apply(kind.Block, texture, layout);
            kind.Block.SetFloat(SheetScaleId, sheetScale);
            kind.Block.SetFloat(ViewStretchId, 1f);
            kind.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, PerfLabInstance.Stride);
            kind.Block.SetBuffer(InstancesId, kind.Buffer);
            return kind;
        }

        // 대포 끌기 (K-060): 걸음 칸과 위치를 함께 32px 단위로 나눠 발이 미끄러지지 않게(Codex K-052 규칙 — 칸마다 왼쪽 32px, 디딘 발은 제자리).
        // 그림은 왼쪽으로 끈다 → 사진기에서 오른쪽으로 가면 좌우 뒤집기. 바퀴는 끈 거리만큼 돈다(각 = 거리 ÷ 반지름)
        void DrawHauls(float shade)
        {
            if (haulPull == null) return;
            haulPull.Count = haulPush.Count = haulChassis.Count = haulWheel.Count = 0;
            IReadOnlyList<BattleSim.CannonHaul> hauls = sim.CannonHauls;
            if (hauls.Count == 0) return;
            Camera view = Camera.main;
            Vector3 camRight = view != null ? Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized : Vector3.right;
            float mpp = CannonArt.MetersPerPx(SoldierHeight);
            float step = CannonArt.CrewStepPx * mpp;
            float cannonSize = CannonArt.HaulCellHeightPx * mpp;
            float wheelSize = CannonArt.WheelDiameterPx * mpp;
            for (int h = 0; h < hauls.Count && haulPull.Count < haulPull.Instances.Length; h++)
            {
                BattleSim.CannonHaul haul = hauls[h];
                Vector3 dir = haul.Direction.sqrMagnitude > 1e-6f ? haul.Direction.normalized : Vector3.right;
                int steps = Mathf.FloorToInt(Mathf.Max(0f, haul.Travelled) / step);
                Vector3 at = haul.Position - dir * (haul.Travelled - steps * step);   // 걸음 단위로 끊은 자리
                int frame = steps % CannonArt.CrewFrames;
                bool flip = Vector3.Dot(dir, camRight) > 0f;   // 그림은 왼쪽(사진기 왼쪽)으로 끈다
                float w = flip ? -frame - 1 : frame;
                var tint = new float4(shade, shade, shade, SoldierHeight);
                Vector3 pull = at - dir * (CannonArt.PullOffsetPx * mpp);   // 그림 왼쪽(−) = 가는 쪽
                Vector3 push = at - dir * (CannonArt.PushOffsetPx * mpp);
                haulPull.Instances[haulPull.Count++] = new PerfLabInstance { PosFrame = new float4(pull.x, pull.y, pull.z, w), TintSize = tint };
                if (haul.PushCrew) haulPush.Instances[haulPush.Count++] = new PerfLabInstance { PosFrame = new float4(push.x, push.y, push.z, w), TintSize = tint };
                haulChassis.Instances[haulChassis.Count++] = new PerfLabInstance { PosFrame = new float4(at.x, at.y, at.z, flip ? -1f : 0f), TintSize = new float4(shade, shade, shade, cannonSize) };

                // 바퀴: 칸 중심 = 기준점에서 위로 (352 − 301.5)px, 옆으로 0.5px. 회전 방향은 가는 쪽
                float sideSign = flip ? -1f : 1f;
                Vector3 hub = at + camRight * (sideSign * (CannonArt.WheelCenterXPx - CannonArt.HaulPivotXPx) * mpp)
                    + Vector3.up * ((CannonArt.HaulPivotYPx - CannonArt.WheelCenterYPx) * mpp);   // 바퀴 판은 가운데 기준
                float angle = steps * step / (wheelSize * 0.5f) * (flip ? -1f : 1f);
                Vector3 axis = camRight * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                haulWheel.Instances[haulWheel.Count++] = new PerfLabInstance { PosFrame = new float4(hub.x, hub.y, hub.z, 0f), TintSize = new float4(axis.x, axis.y, axis.z, wheelSize) };
            }

            haulWheel.Block.SetColor(ShotColorId, new Color(shade, shade, shade, 1f));
            foreach (HaulKind kind in new[] { haulChassis, haulWheel, haulPush, haulPull })
            {
                if (kind.Count <= 0) continue;
                kind.Buffer.SetData(kind.Instances, 0, 0, kind.Count);
                Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, kind.Count, kind.Block);
            }
        }

        // 확인용 (K-060): 이번 프레임 그린 끄는 대포 수
        public int HaulDrawCount => haulChassis != null ? haulChassis.Count : 0;

        Piece MakeDraw(Texture2D texture, PerfLabArt.SheetLayout layout, float3 right, List<Vector3> feet)
        {
            var draw = new Piece { Block = new MaterialPropertyBlock(), Feet = feet.ToArray(), Instances = new PerfLabInstance[Mathf.Max(1, feet.Count)] };
            Apply(draw.Block, texture, layout);
            draw.Block.SetVector(FixedRightId, new Vector4(right.x, 0f, right.z, 1f));
            draw.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, draw.Instances.Length, PerfLabInstance.Stride);
            draw.Block.SetBuffer(InstancesId, draw.Buffer);
            return draw;
        }

        static void Apply(MaterialPropertyBlock block, Texture texture, PerfLabArt.SheetLayout layout)
        {
            block.SetTexture(MainTexId, texture);
            block.SetVector(GridId, layout.Grid);
            block.SetVector(PivotId, layout.Pivot);
            block.SetFloat(AspectId, layout.Aspect);
        }

        // 작업이 끝난 뒤(sim.Complete 후), 지도 모드가 아닐 때
        public void Draw()
        {
            float shade = sim.Brightness;
            if (!Mathf.Approximately(shade, lastBrightness))
            {
                lastBrightness = shade;
                foreach (Piece d in wallDraws)
                {
                    for (int i = 0; i < d.Feet.Length; i++)
                    {
                        Vector3 f = d.Feet[i];
                        d.Instances[i] = new PerfLabInstance { PosFrame = new float4(f.x, f.y, f.z, 0f), TintSize = new float4(shade, shade, shade, ModuleWidth * 0.5f) };
                    }

                    d.Buffer.SetData(d.Instances);
                }
            }

            foreach (Piece d in wallDraws)
            {
                if (d.Feet.Length > 0) Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, d.Feet.Length, d.Block);
            }

            if (cannonBuffer == null) return;
            DrawHauls(shade);
            int smokes = 0;
            for (int i = 0; i < cannonInstances.Length; i++)
            {
                BattleSim.CannonInfo c = sim.GetCannon(i);
                float3 n = WallGeometry.Outward(c.Side);
                float3 r = WallGeometry.Right(c.Side);
                int frame = 1;
                float size = CannonSize;
                float since = sim.CannonFireAge(i);   // 전투 시간 기준 (K-036: 병사 사격 시각으로 직접)
                if (recoilArt)
                {
                    // 0 평상, 1 반동(뒤로 밀림), 2 반쯤 복귀 — 쏜 뒤 RecoilFrameSeconds씩
                    frame = since < RecoilFrameSeconds ? 1 : since < RecoilFrameSeconds * 2f ? 2 : 0;
                    size = RecoilCannonSize;
                }
                else if (c.HasAim)
                {
                    var d = new Vector2(c.Aim.x - c.Position.x, c.Aim.z - c.Position.z);
                    float lateral = d.sqrMagnitude > 0.01f ? (d.x * r.x + d.y * r.z) / d.magnitude : 0f;
                    frame = lateral < -AimSideThreshold ? 0 : lateral > AimSideThreshold ? 2 : 1;
                }

                Vector3 p = c.Position + new Vector3(n.x, 0f, n.z) * (0.5f - CannonInset);
                cannonInstances[i] = new PerfLabInstance
                {
                    PosFrame = new float4(p.x, p.y, p.z, frame),
                    TintSize = c.Alive && !c.Moving ? new float4(shade, shade, shade, size) : float4.zero,   // 끄는 중이면 끌기 그림이 대신 (K-060)
                };

                // 포구 연기: 쏜 뒤 0.75초 한 번 (스스로 빛나는 불이 아니라 밝기는 대포와 같게)
                if (smokeInstances == null || !c.Alive || c.Moving) continue;
                float age = since;
                if (age < 0f || age >= SmokeFrames / SmokeFps) continue;
                Vector3 m = p + new Vector3(n.x, 0f, n.z) * SmokeForward + Vector3.up * SmokeLift;
                smokeInstances[smokes++] = new PerfLabInstance
                {
                    PosFrame = new float4(m.x, m.y, m.z, Mathf.Clamp((int)(age * SmokeFps), 0, SmokeFrames - 1)),
                    TintSize = new float4(shade, shade, shade, SmokeSize),
                };
            }

            cannonBuffer.SetData(cannonInstances);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, cannonInstances.Length, cannonBlock);

            if (smokes > 0)
            {
                smokeBuffer.SetData(smokeInstances, 0, 0, smokes);
                Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, smokes, smokeBlock, ShadowCastingMode.Off, false);
            }
        }

        // 확인용 (K-060): 설치 대포 그림 칸 높이(m)와 보이는 폭(m)
        public static float InstalledCannonSize => RecoilCannonSize;
        public static float InstalledCannonVisibleWidth => RecoilCannonSize * CannonArt.RecoilVisibleWidthFraction;

        // 확인용: 대포 i의 지금 그림 칸 (0 왼쪽, 1 가운데, 2 오른쪽)
        public int CannonFrame(int index) => cannonInstances != null && index < cannonInstances.Length ? (int)cannonInstances[index].PosFrame.w : -1;
        public int WallPieceCount { get { int n = 0; foreach (Piece d in wallDraws) n += d.Feet.Length; return n; } }

        public void Dispose()
        {
            foreach (Piece d in wallDraws) d.Buffer?.Release();
            wallDraws.Clear();
            cannonBuffer?.Release();
            smokeBuffer?.Release();
            haulPull?.Buffer.Release();
            haulPush?.Buffer.Release();
            haulChassis?.Buffer.Release();
            haulWheel?.Buffer.Release();
        }
    }
}
