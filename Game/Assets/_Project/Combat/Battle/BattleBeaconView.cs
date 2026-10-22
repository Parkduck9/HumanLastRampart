using System;
using HumanLastRampart.Combat.PerfLab;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 가운데 등대 탑 그림 (W9, Concept_Beacon_v1 9.7 4차 결정, Codex K-102 탑·불꽃·빛기둥 + K-106 렌즈 빛)
    // - 탑·불꽃: 같은 칸(288×384, 발 (144,360)) 겹쳐 그린다. 점화의 밤이면 완전 점화(밝은 화로), 보통 밤은 예열(작은 불씨)
    // - 탑 뒤 빛 번쩍: 빛기둥(K-102)과 렌즈 빛(K-106)을 더하기로 화로 위치에 — 탑 뒤에서 빛이 강하게 뿜어져 나오는 연출. 밝기(약·보통·강)에 따라 세기가 다르다
    // - 일꾼 3명: 병사 그림(정적 뒷모습)을 갈색으로 칠한 임시 그림 — 최종 그림은 Codex K-110. 서 있다가, 빛줄기가 도는 동안 탑을 따라 돌며 위아래로 들썩인다
    // 그림 크기: 인물 73점이 주인공 키(HeroDrawSize)이므로 칸 높이 = 384/219 × 주인공 키 (K-102 매니페스트 figure_reference)
    public sealed class BattleBeaconView : IDisposable
    {
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int SpecTexId = Shader.PropertyToID("_SpecTex");
        static readonly int HasSpecId = Shader.PropertyToID("_HasSpec");
        static readonly int SheetScaleId = Shader.PropertyToID("_SheetScale");
        static readonly int ViewStretchId = Shader.PropertyToID("_ViewStretch");
        static readonly int EmissiveId = Shader.PropertyToID("_Emissive");

        // K-102 칸: 288×384, 발·화로 위치(칸 위에서 126px, 발 360px)
        const float CellW = 288f, CellH = 384f, FootY = 360f, BowlY = 126f;
        public static readonly float CellMeters = CellH * BattleView.HeroDrawSize / 219f;          // 칸 높이(m) ≈ 0.66
        public static readonly float BowlHeight = (FootY - BowlY) / CellH * CellMeters;              // 발에서 화로까지 (m) ≈ 0.40
        public const float TowerRadius = 0.22f;      // 탑 몸통 반폭 (일꾼은 이보다 바깥을 돈다)
        const int FlameFrames = 6;
        const float FlameFps = 8f;                   // K-102: 초당 8프레임 반복
        const int WorkerCount = 3;
        const float WorkerRadius = 0.5f;
        const float WorkerLapSeconds = 4f;           // 밀 때 탑을 한 바퀴 도는 시간
        static readonly float[] IdleAngles = { 200f, 340f, 90f };   // 서 있을 때 각도 (수평, +x = 0°, +z = 90°) — 사진기(남쪽)에서 보면 양옆 + 뒤
        static readonly Color WorkerTint = new Color(0.95f, 0.72f, 0.5f);

        readonly BattleSim sim;
        readonly Mesh quad;
        readonly Bounds bounds;
        readonly Material material;      // 도트 그림 셰이더 (BattleView와 같은 것)
        readonly Material glowMaterial;  // 더하기 번쩍
        readonly Vector3 baseFeet;
        readonly bool lit;

        readonly Piece tower, flame, workers, pillar, head;
        readonly float[] workerAngle = new float[WorkerCount];
        float lastTime = -1f;

        sealed class Piece
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public PerfLabInstance[] Instances;
        }

        public bool HasArt { get; }
        public Vector3 Feet => baseFeet;
        public Vector3 Bowl => baseFeet + Vector3.up * BowlHeight;
        // 확인용
        public bool WorkersPushing { get; private set; }
        public int WorkerDrawCount => workers != null ? WorkerCount : 0;
        public Vector3 WorkerPosition(int i) => WorkerAt(i);

        public BattleBeaconView(BattleSim sim, Material material, Mesh quad, Bounds bounds)
        {
            this.sim = sim;
            this.material = material;
            this.quad = quad;
            this.bounds = bounds;
            baseFeet = new Vector3(0f, BattleSim.WallHeight, 0f);   // 마당 윗면은 성벽 윗면과 같은 높이
            lit = sim.IsIgnitionNight;
            for (int i = 0; i < WorkerCount; i++) workerAngle[i] = IdleAngles[i];

            var towerTexture = Resources.Load<Texture2D>(lit ? "BattleArt/Beacon/tower_lit" : "BattleArt/Beacon/tower_warming");
            var flameTexture = Resources.Load<Texture2D>(lit ? "BattleArt/Beacon/flame_lit" : "BattleArt/Beacon/flame_warming");
            var pillarTexture = Resources.Load<Texture2D>("BattleArt/Beacon/light_pillar");
            var headTexture = Resources.Load<Texture2D>("BattleArt/Beacon/beam_head");
            if (towerTexture == null) return;   // 그림이 없으면 아무것도 안 그린다 (빛줄기 부채꼴은 그대로)
            HasArt = true;

            float pivotY = (CellH - FootY) / CellH;
            tower = Make(towerTexture, PerfLabArt.SheetLayout.Make(1, 1, 0.5f, pivotY, CellW / CellH), 1);
            tower.Block.SetFloat(ViewStretchId, 0f);
            if (flameTexture != null)
            {
                flame = Make(flameTexture, PerfLabArt.SheetLayout.Make(FlameFrames, 1, 0.5f, pivotY, CellW / CellH), 1);
                flame.Block.SetFloat(EmissiveId, 1f);   // 스스로 빛나는 불 (빛번짐)
            }

            // 일꾼: 병사 그림 첫 병종의 정적 뒷모습 1칸을 갈색으로
            Texture2D workerSheet = null;
            foreach (var (unit, _, count) in sim.SoldierGroups)
                if (count > 0 && unit != null && unit.Sheet != null) { workerSheet = unit.Sheet; break; }
            if (workerSheet != null)
            {
                workers = Make(workerSheet, PerfLabArt.FromSheet(workerSheet), WorkerCount);
                workers.Block.SetFloat(SheetScaleId, PerfLabArt.SheetScale(workerSheet));
                workers.Block.SetFloat(ViewStretchId, 1f);
            }

            Shader glow = Resources.Load<Shader>("BeaconGlow");
            if (glow != null && pillarTexture != null)
            {
                glowMaterial = new Material(glow) { name = "등대 빛 번쩍 (runtime)" };
                pillar = MakeGlow(pillarTexture, 288f, 768f, 744f, 1);
                if (headTexture != null) head = MakeGlow(headTexture, CellW, CellH, BowlY, 1);
            }
        }

        Piece Make(Texture2D texture, PerfLabArt.SheetLayout layout, int capacity)
        {
            var piece = new Piece { Block = new MaterialPropertyBlock(), Instances = new PerfLabInstance[capacity] };
            piece.Block.SetTexture(MainTexId, texture);
            piece.Block.SetVector(GridId, layout.Grid);
            piece.Block.SetVector(PivotId, layout.Pivot);
            piece.Block.SetFloat(AspectId, layout.Aspect);
            Texture2D spec = PerfLabArt.SpecFor(texture);
            if (spec != null) piece.Block.SetTexture(SpecTexId, spec);
            piece.Block.SetFloat(HasSpecId, spec != null ? 1f : 0f);
            piece.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, PerfLabInstance.Stride);
            piece.Block.SetBuffer(InstancesId, piece.Buffer);
            return piece;
        }

        // 더하기 그림: 기준점 = 칸 안 anchorY 줄 (위에서 px), 칸 크기는 K-102 규격 (3배 확대본)
        Piece MakeGlow(Texture2D texture, float cellW, float cellH, float anchorY, int capacity)
        {
            var piece = new Piece { Block = new MaterialPropertyBlock(), Instances = new PerfLabInstance[capacity] };
            piece.Block.SetTexture(MainTexId, texture);
            piece.Block.SetVector(PivotId, new Vector4(0.5f, (cellH - anchorY) / cellH, 0f, 0f));
            piece.Block.SetFloat(AspectId, cellW / cellH);
            piece.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, PerfLabInstance.Stride);
            piece.Block.SetBuffer(InstancesId, piece.Buffer);
            return piece;
        }

        Vector3 WorkerAt(int i)
        {
            float a = workerAngle[i] * Mathf.Deg2Rad;
            return baseFeet + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * WorkerRadius;
        }

        // 작업이 끝난 뒤(sim.Complete 후), 지도 모드가 아닐 때
        public void Draw()
        {
            if (!HasArt) return;
            float now = Time.unscaledTime;
            float dt = lastTime < 0f ? 0f : Mathf.Min(now - lastTime, 0.1f);
            lastTime = now;
            float shade = sim.Brightness;

            // 탑·불꽃
            tower.Instances[0] = new PerfLabInstance { PosFrame = new float4(baseFeet.x, baseFeet.y, baseFeet.z, 0f), TintSize = new float4(shade, shade, shade, CellMeters) };
            Submit(tower, 1, material);
            if (flame != null)
            {
                int frame = (int)(now * FlameFps) % FlameFrames;
                float glow = 1.4f;   // 1 넘는 색 = 빛번짐
                flame.Instances[0] = new PerfLabInstance { PosFrame = new float4(baseFeet.x, baseFeet.y, baseFeet.z, frame), TintSize = new float4(glow * 1.2f, glow, glow * 0.8f, CellMeters) };
                Submit(flame, 1, material);
            }

            // 일꾼: 도는 동안 탑 둘레를 따라 돌며 들썩인다 (미는 방향 = 빛줄기가 도는 방향)
            WorkersPushing = sim.BeamRotating;
            if (workers != null)
            {
                float turn = WorkersPushing ? Mathf.Sign(Mathf.DeltaAngle(sim.BeamAngleDegrees, sim.BeamTargetDegrees)) : 0f;
                for (int i = 0; i < WorkerCount; i++)
                {
                    workerAngle[i] += turn * 360f / WorkerLapSeconds * dt;
                    Vector3 p = WorkerAt(i);
                    float bob = WorkersPushing ? Mathf.Abs(Mathf.Sin(now * 9f + i * 1.9f)) * 0.025f : 0f;   // 걸음마다 위아래 (기울이기 대신 임시)
                    float sway = WorkersPushing ? Mathf.Sin(now * 4.5f + i) * 0.012f : 0f;
                    workers.Instances[i] = new PerfLabInstance
                    {
                        PosFrame = new float4(p.x + sway, p.y + bob, p.z, 0f),
                        TintSize = new float4(shade * WorkerTint.r, shade * WorkerTint.g, shade * WorkerTint.b, BattleView.HeroDrawSize * 0.93f),
                    };
                }

                Submit(workers, WorkerCount, material);
            }

            // 탑 뒤 빛 번쩍 (더하기). 세기는 등불 밝기 약·보통·강
            if (glowMaterial != null)
            {
                float level = sim.Lamp == HumanLastRampart.Core.CombatContracts.LampBrightness.High ? 1.45f
                    : sim.Lamp == HumanLastRampart.Core.CombatContracts.LampBrightness.Low ? 0.7f : 1f;
                float pulse = 0.92f + 0.08f * Mathf.Sin(now * 7f) + 0.04f * Mathf.Sin(now * 13.1f);
                float day = sim.TimeOfDay == HumanLastRampart.Core.CombatContracts.TimeOfDay.Night ? 1f : 0.45f;   // 낮에는 햇빛에 묻힌다
                float k = level * pulse * day * (lit ? 1.15f : 0.9f);
                Vector3 bowl = Bowl;
                pillar.Instances[0] = new PerfLabInstance { PosFrame = new float4(bowl.x, bowl.y, bowl.z, 0f), TintSize = new float4(1.0f * k, 0.85f * k, 0.5f * k, 768f / CellH * CellMeters * (0.9f + 0.1f * level)) };
                Submit(pillar, 1, glowMaterial);
                if (head != null)
                {
                    float burst = 1.9f + 0.5f * level;   // 탑보다 훨씬 크게 번진다
                    head.Instances[0] = new PerfLabInstance { PosFrame = new float4(bowl.x, bowl.y, bowl.z, 0f), TintSize = new float4(1.6f * k, 1.25f * k, 0.7f * k, CellMeters * burst) };
                    Submit(head, 1, glowMaterial);
                }
            }
        }

        void Submit(Piece piece, int count, Material mat)
        {
            piece.Buffer.SetData(piece.Instances, 0, 0, count);
            if (mat == glowMaterial) Graphics.DrawMeshInstancedProcedural(quad, 0, mat, bounds, count, piece.Block, ShadowCastingMode.Off, false);
            else Graphics.DrawMeshInstancedProcedural(quad, 0, mat, bounds, count, piece.Block);
        }

        public void Dispose()
        {
            foreach (Piece piece in new[] { tower, flame, workers, pillar, head }) piece?.Buffer?.Release();
            if (glowMaterial != null) UnityEngine.Object.Destroy(glowMaterial);
        }
    }
}
