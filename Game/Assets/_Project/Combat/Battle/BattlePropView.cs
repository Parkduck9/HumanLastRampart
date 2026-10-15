using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽 난간·대포 그림 (미리 연결, 사용자 검토 전 — Codex 시안 wall_a·대포 사격구·대포 3방향)
    // - 성벽: 성벽 윗면 바깥 모서리에 난간 그림 판을 성벽 방향으로 세운다(카메라를 따라 돌지 않음). 윗면 아래 부분은 성벽 상자에 가려진다
    // - 대포 자리: 가운데가 낮은 사격구 판, 그 앞에 대포. 조준점이 왼쪽/오른쪽이면 그쪽 그림, 없으면 가운데
    // - 그림이 없으면(Resources/BattleArt) 그리지 않는다 — 예전 회색 상자만
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

        readonly BattleSim sim;
        readonly Material material;
        readonly Mesh quad;
        readonly Bounds bounds;
        readonly List<Piece> wallDraws = new List<Piece>();
        readonly GraphicsBuffer cannonBuffer;
        readonly MaterialPropertyBlock cannonBlock;
        readonly PerfLabInstance[] cannonInstances;
        float lastBrightness = -1f;

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

            var wall = Resources.Load<Texture2D>("BattleArt/wall_a");
            var bay = Resources.Load<Texture2D>("BattleArt/wall_cannon_bay");
            var cannon = Resources.Load<Texture2D>("BattleArt/cannon_aim3");
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
                Apply(cannonBlock, cannon, PerfLabArt.SheetLayout.Make(3, 1, 0.5f, 32f / 384f, 512f / 384f));
                cannonInstances = new PerfLabInstance[sim.CannonCount];
                cannonBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sim.CannonCount, PerfLabInstance.Stride);
                cannonBlock.SetBuffer(InstancesId, cannonBuffer);
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
            for (int i = 0; i < cannonInstances.Length; i++)
            {
                BattleSim.CannonInfo c = sim.GetCannon(i);
                float3 n = WallGeometry.Outward(c.Side);
                float3 r = WallGeometry.Right(c.Side);
                int frame = 1;
                if (c.HasAim)
                {
                    var d = new Vector2(c.Aim.x - c.Position.x, c.Aim.z - c.Position.z);
                    float lateral = d.sqrMagnitude > 0.01f ? (d.x * r.x + d.y * r.z) / d.magnitude : 0f;
                    frame = lateral < -AimSideThreshold ? 0 : lateral > AimSideThreshold ? 2 : 1;
                }

                Vector3 p = c.Position + new Vector3(n.x, 0f, n.z) * (0.5f - CannonInset);
                cannonInstances[i] = new PerfLabInstance
                {
                    PosFrame = new float4(p.x, p.y, p.z, frame),
                    TintSize = c.Alive ? new float4(shade, shade, shade, CannonSize) : float4.zero,
                };
            }

            cannonBuffer.SetData(cannonInstances);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, cannonInstances.Length, cannonBlock);
        }

        // 확인용: 대포 i의 지금 그림 칸 (0 왼쪽, 1 가운데, 2 오른쪽)
        public int CannonFrame(int index) => cannonInstances != null && index < cannonInstances.Length ? (int)cannonInstances[index].PosFrame.w : -1;
        public int WallPieceCount { get { int n = 0; foreach (Piece d in wallDraws) n += d.Feet.Length; return n; } }

        public void Dispose()
        {
            foreach (Piece d in wallDraws) d.Buffer?.Release();
            wallDraws.Clear();
            cannonBuffer?.Release();
        }
    }
}
