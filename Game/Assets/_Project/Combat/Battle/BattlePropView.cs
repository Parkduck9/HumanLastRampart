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
    //   쏜 순간(대포알 투사체가 새로 생긴 칸)을 찾아 반동 2칸을 0.1초씩 보이고, 포구 앞에 Cannon/muzzle_smoke 6칸을 한 번 재생한다
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
        const float RecoilCannonSize = 0.6f;                  // 칸 높이(m) — 보이는 폭 약 0.55m, 사격구 폭 1.12m 안
        const float RecoilFrameSeconds = 0.1f;                // 반동 1칸·복귀 1칸을 이만큼씩
        const float CannonShotSize = 1f;                      // 투사체 그림 크기가 이보다 크면 대포알 (WriteShotInstancesJob 1.6, BattleEffectsView와 같은 규칙)
        const float FireMatchDistance = 3f;                   // 새 대포알이 이 거리 안 대포에서 나왔다고 본다
        // 포구 연기 (K-016 muzzle_smoke: 칸 256×256 6칸, 원점 (128,240), 10fps 한 번)
        const int SmokeFrames = 6;
        const float SmokeFps = 10f;
        const float SmokeSize = 0.7f;
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
        readonly float[] lastFired;                           // 대포마다 마지막으로 쏜 시각 (반동·연기)
        float[] shotSizes;                                    // 지난 프레임 투사체 그림 크기 (0에서 커지면 새로 쏨)
        readonly GraphicsBuffer smokeBuffer;
        readonly MaterialPropertyBlock smokeBlock;
        readonly PerfLabInstance[] smokeInstances;

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

            // 성벽마다 대포 자리(성벽 따라 m)
            var bays = new List<float>[4];
            for (int s = 0; s < 4; s++) bays[s] = new List<float>();
            for (int i = 0; i < sim.CannonCount; i++)
            {
                BattleSim.CannonInfo c = sim.GetCannon(i);
                float3 r = WallGeometry.Right(c.Side);
                bays[c.Side].Add(c.Position.x * r.x + c.Position.z * r.z);
            }

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

                if (recoilArt)
                {
                    lastFired = new float[sim.CannonCount];
                    for (int i = 0; i < lastFired.Length; i++) lastFired[i] = -100f;
                    shotSizes = new float[sim.ShotCapacity];

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

        // 새 대포알 찾기: 투사체 칸의 그림 크기가 0(빈 칸)·작은 것에서 대포알 크기로 바뀌면 그 자리에서 가장 가까운 대포가 쐈다
        void TrackCannonShots()
        {
            if (shotSizes == null) return;
            NativeArray<PerfLabInstance> shots = sim.ShotInstances;
            int slots = Mathf.Min(shotSizes.Length, shots.Length);
            float now = Time.time;
            for (int i = 0; i < slots; i++)
            {
                PerfLabInstance shot = shots[i];
                float size = shot.TintSize.w;
                float before = shotSizes[i];
                shotSizes[i] = size;
                if (size <= CannonShotSize || before > CannonShotSize) continue;

                int best = -1;
                float bestDistance = FireMatchDistance * FireMatchDistance;
                for (int c = 0; c < lastFired.Length; c++)
                {
                    Vector3 p = sim.GetCannon(c).Position;
                    float dx = p.x - shot.PosFrame.x, dz = p.z - shot.PosFrame.z;
                    float d = dx * dx + dz * dz;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = c;
                    }
                }

                if (best >= 0) lastFired[best] = now;
            }
        }

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
            if (recoilArt) TrackCannonShots();
            float now = Time.time;
            int smokes = 0;
            for (int i = 0; i < cannonInstances.Length; i++)
            {
                BattleSim.CannonInfo c = sim.GetCannon(i);
                float3 n = WallGeometry.Outward(c.Side);
                float3 r = WallGeometry.Right(c.Side);
                int frame = 1;
                float size = CannonSize;
                if (recoilArt)
                {
                    // 0 평상, 1 반동(뒤로 밀림), 2 반쯤 복귀 — 쏜 뒤 0.1초씩
                    float since = now - lastFired[i];
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
                    TintSize = c.Alive ? new float4(shade, shade, shade, size) : float4.zero,
                };

                // 포구 연기: 쏜 뒤 0.6초 한 번 (스스로 빛나는 불이 아니라 밝기는 대포와 같게)
                if (smokeInstances == null) continue;
                float age = now - lastFired[i];
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

        // 확인용: 대포 i의 지금 그림 칸 (0 왼쪽, 1 가운데, 2 오른쪽)
        public int CannonFrame(int index) => cannonInstances != null && index < cannonInstances.Length ? (int)cannonInstances[index].PosFrame.w : -1;
        public int WallPieceCount { get { int n = 0; foreach (Piece d in wallDraws) n += d.Feet.Length; return n; } }

        public void Dispose()
        {
            foreach (Piece d in wallDraws) d.Buffer?.Release();
            wallDraws.Clear();
            cannonBuffer?.Release();
            smokeBuffer?.Release();
        }
    }
}
