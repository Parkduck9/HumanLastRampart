using System;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 가운데 등대 탑 그림 (W9, Concept_Beacon_v1 9.7 4차 결정). 그림: Codex K-110 탑 몸체(계단·밧줄 감는 틀)·앞 기둥·일꾼 둘(a·b)·빛 번쩍 셋,
    // K-102 불꽃·빛기둥. 그리는 차례: 탑 몸체 → 일꾼 → 앞 기둥 → 불꽃 → 더하기 빛 (더하기 빛은 탑 뒤로 밀어 둔다)
    // - 불꽃: 전투에서는 탑이 '꺼짐' 상태일 수 없다(빛줄기가 돈다). 등대 복구가 세기를 정한다(BeaconRules.TowerFlameStrength), 점화의 밤이면 완전 점화 불꽃 (정비 마당도 같은 규칙, S4)
    // - 일꾼: 빛줄기가 도는 동안 미는 그림(8칸 8fps)에 앞뒤로 기우는 흔들림을 더하고, 끝나면 쉬는 그림을 한 번 재생·마지막 칸 유지, 한동안 뒤 서 있는 그림.
    //   모든 움직임은 전투 시간(sim.Elapsed)으로 — 멈춤·지도 시간 멈춤에서 같이 멈춘다 (불꽃 일렁임만 실제 시간)
    // 크기: 인물 73점 = 주인공 키(HeroDrawSize), K-110 칸은 288×384(일꾼)·1152×960(탑)을 3배 확대본 그대로
    public sealed class BattleBeaconView : IDisposable
    {
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int SpecTexId = Shader.PropertyToID("_SpecTex");
        static readonly int HasSpecId = Shader.PropertyToID("_HasSpec");
        static readonly int ViewStretchId = Shader.PropertyToID("_ViewStretch");
        static readonly int EmissiveId = Shader.PropertyToID("_Emissive");
        static readonly int PushId = Shader.PropertyToID("_Push");
        static readonly int SheetScaleId = Shader.PropertyToID("_SheetScale");

        // 게임 그림 1점 = 몇 m (인물 키 219점 = 주인공 키)
        public static readonly float Mpp = BattleView.HeroDrawSize / 219f;
        // K-110 탑 칸 1152×960, 발 (720, 864), 화로 가운데 약 (762, 215)
        const float TowerW = 1152f, TowerH = 960f, TowerFootX = 720f, TowerFootY = 864f, BowlPxX = 762f, BowlPxY = 215f;
        public static readonly float TowerCellMeters = TowerH * Mpp;
        public static readonly float BowlHeight = (TowerFootY - BowlPxY) * Mpp;   // 발에서 화로까지 (m) ≈ 1.11
        public static readonly float BowlOffsetX = (BowlPxX - TowerFootX) * Mpp;
        // K-110 일꾼 칸 288×384, 발 (144, 360). 탑 안 밧줄 틀 바닥 줄 (탑 그림 y 745)
        const float WorkerW = 288f, WorkerH = 384f, WorkerFootY = 360f;
        static readonly float WorkerFloorLift = (TowerFootY - 745f) * Mpp;
        // 일꾼 서는 자리 (탑 그림 가로 점)와 변형 (0 = a, 1 = b)
        static readonly float[] WorkerPx = { 655f, 790f, 930f };
        static readonly int[] WorkerVariant = { 0, 1, 1 };
        const int WorkerCount = 3;
        // K-102 불꽃(6칸 8fps)·빛기둥. 칸 288×384, 불꽃 밑·화로 기준점 (144, 126). 새 화로가 K-102 화로보다 커서 키운다
        const float FlameCellW = 288f, FlameCellH = 384f, FlameAnchorY = 126f;
        const int FlameFrames = 6;
        const float FlameFps = 8f;
        const float FlameScale = 2.2f;
        const float PillarScale = 1.5f;
        const float BurstScale = 2.2f;
        const float GlowPush = 0.6f;             // 더하기 빛을 카메라에서 멀리 (탑 몸체 판 뒤) — 위로 세운 판은 높이 둔 것일수록 가까워서 이만큼은 밀어야 탑에 가려진다
        const float RestSeconds = 4f / 3f;       // 쉬는 그림 4칸 3fps 한 번
        const float RestHoldSeconds = 6f;        // 마지막 칸을 이만큼 유지한 뒤 서 있는 그림으로
        const float LeanMeters = 0.02f;          // 밀 때 앞뒤로 기우는 폭 (미는 8칸이 거의 같아서 코드로 더한다)
        const float IdleFps = 4f;

        readonly BattleSim sim;
        readonly Mesh quad;
        readonly Bounds bounds;
        readonly Material material;      // 도트 그림 셰이더 (BattleView와 같은 것)
        readonly Material glowMaterial;  // 더하기 번쩍
        readonly Vector3 baseFeet;
        readonly bool lit;
        readonly float strength;

        readonly Piece tower, foreground, flame, pillar;
        readonly Piece[] bursts = new Piece[3];
        readonly Piece courierStand, courierRun;   // 탑 대기 전령 3명 (병사 그림: 서 있기·달리기, W9)
        readonly Texture2D courierRunSheet;
        readonly Piece[,] workers = new Piece[2, 3];   // [변형 a·b, 상태 idle·push·rest]
        const int Idle = 0, Push = 1, Rest = 2;
        static readonly int[] StateFrames = { 4, 8, 4 };
        bool wasPushing;
        float restStart = -999f;
        bool faceLeft;      // 시계 방향(각도가 줄어드는 쪽)이면 일꾼이 왼쪽으로 민다 (사진기에서 보는 앞쪽은 서쪽으로 움직인다)

        sealed class Piece
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public PerfLabInstance[] Instances;
            public int Count;
        }

        public bool HasArt { get; }
        public Vector3 Feet => baseFeet;
        public Vector3 Bowl => baseFeet + new Vector3(BowlOffsetX, BowlHeight, 0f);
        // 확인용
        public bool WorkersPushing { get; private set; }
        public int WorkerState { get; private set; }   // 0 서기 · 1 밀기 · 2 쉬기
        public bool FacingLeft => faceLeft;
        public float FlameStrength => strength;
        public bool Lit => lit;
        public static int WorkerTotal => WorkerCount;

        public BattleBeaconView(BattleSim sim, Material material, Mesh quad, Bounds bounds)
        {
            this.sim = sim;
            this.material = material;
            this.quad = quad;
            this.bounds = bounds;
            baseFeet = new Vector3(0f, BattleSim.WallHeight, 0f);   // 마당 윗면은 성벽 윗면과 같은 높이
            lit = sim.IsIgnitionNight;
            strength = BeaconRules.TowerFlameStrength(sim.BeaconRepair, lit);

            var towerTexture = Resources.Load<Texture2D>("BattleArt/Beacon/tower_base");
            if (towerTexture == null) return;   // 그림이 없으면 아무것도 안 그린다 (빛줄기 부채꼴은 그대로)
            HasArt = true;

            var towerLayout = PerfLabArt.SheetLayout.Make(1, 1, TowerFootX / TowerW, (TowerH - TowerFootY) / TowerH, TowerW / TowerH);
            tower = Make(towerTexture, towerLayout, 1);
            tower.Block.SetFloat(ViewStretchId, 0f);
            var fore = Resources.Load<Texture2D>("BattleArt/Beacon/tower_foreground");
            if (fore != null)
            {
                foreground = Make(fore, towerLayout, 1);
                foreground.Block.SetFloat(ViewStretchId, 0f);
            }

            var flameTexture = Resources.Load<Texture2D>(lit ? "BattleArt/Beacon/flame_lit" : "BattleArt/Beacon/flame_warming");
            if (flameTexture != null)
            {
                flame = Make(flameTexture, PerfLabArt.SheetLayout.Make(FlameFrames, 1, 0.5f, (FlameCellH - FlameAnchorY) / FlameCellH, FlameCellW / FlameCellH), 1);
                flame.Block.SetFloat(EmissiveId, 1f);   // 스스로 빛나는 불 (빛번짐)
            }

            string[] variants = { "a", "b" };
            string[] states = { "idle", "push", "rest" };
            for (int v = 0; v < 2; v++)
                for (int s = 0; s < 3; s++)
                {
                    var tex = Resources.Load<Texture2D>($"BattleArt/Beacon/worker_{variants[v]}_{states[s]}");
                    if (tex == null) continue;
                    var layout = PerfLabArt.SheetLayout.Make(StateFrames[s], 1, 0.5f, (WorkerH - WorkerFootY) / WorkerH, WorkerW / WorkerH);
                    workers[v, s] = Make(tex, layout, WorkerCount);
                    workers[v, s].Block.SetFloat(ViewStretchId, 1f);
                }

            Texture2D standSheet = null;
            foreach (var (unit, _, count) in sim.SoldierGroups)
                if (count > 0 && unit != null && unit.Sheet != null) { standSheet = unit.Sheet; courierRunSheet = unit.RunSheet; break; }
            if (standSheet != null)
            {
                courierStand = Make(standSheet, PerfLabArt.FromSheet(standSheet), BattleSim.TowerCourierCount);
                courierStand.Block.SetFloat(SheetScaleId, PerfLabArt.SheetScale(standSheet));
                courierStand.Block.SetFloat(ViewStretchId, 1f);
                if (courierRunSheet != null)
                {
                    courierRun = Make(courierRunSheet, PerfLabArt.FromSheet(courierRunSheet), BattleSim.TowerCourierCount);
                    courierRun.Block.SetFloat(SheetScaleId, PerfLabArt.SheetScale(courierRunSheet));
                    courierRun.Block.SetFloat(ViewStretchId, 1f);
                }
            }

            Shader glow = Resources.Load<Shader>("BeaconGlow");
            var pillarTexture = Resources.Load<Texture2D>("BattleArt/Beacon/light_pillar");
            if (glow != null && pillarTexture != null)
            {
                glowMaterial = new Material(glow) { name = "등대 빛 번쩍 (runtime)" };
                glowMaterial.SetFloat(PushId, GlowPush);
                pillar = MakeGlow(pillarTexture, 288f, 768f, 744f);
                string[] levels = { "normal", "low", "high" };   // 인덱스 = (int)LampBrightness (보통·약·강)
                for (int i = 0; i < 3; i++)
                {
                    var burst = Resources.Load<Texture2D>("BattleArt/Beacon/light_burst_" + levels[i]);
                    if (burst != null) bursts[i] = MakeGlow(burst, 384f, 384f, 192f);
                }
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

        // 더하기 그림: 기준점 = 칸 안 anchorY 줄 (위에서 px)
        Piece MakeGlow(Texture2D texture, float cellW, float cellH, float anchorY)
        {
            var piece = new Piece { Block = new MaterialPropertyBlock(), Instances = new PerfLabInstance[1] };
            piece.Block.SetTexture(MainTexId, texture);
            piece.Block.SetVector(PivotId, new Vector4(0.5f, (cellH - anchorY) / cellH, 0f, 0f));
            piece.Block.SetFloat(AspectId, cellW / cellH);
            piece.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, PerfLabInstance.Stride);
            piece.Block.SetBuffer(InstancesId, piece.Buffer);
            return piece;
        }

        // 일꾼 i가 서는 자리 (발 위치, 밀 때 흔들림 제외)
        public Vector3 WorkerFeet(int i) => baseFeet + new Vector3((WorkerPx[i] - TowerFootX) * Mpp, WorkerFloorLift, 0f);

        // 작업이 끝난 뒤(sim.Complete 후), 지도 모드가 아닐 때
        public void Draw()
        {
            if (!HasArt) return;
            Camera cam = Camera.main;
            Vector3 toCamera = cam != null ? -cam.transform.forward : Vector3.back;   // 화면 위치는 그대로 두고 깊이만 가까이 (같은 평면 겹침 순서)
            float shade = sim.Brightness;
            float time = sim.Elapsed;

            // 탑 몸체
            tower.Instances[0] = new PerfLabInstance { PosFrame = new float4(baseFeet.x, baseFeet.y, baseFeet.z, 0f), TintSize = new float4(shade, shade, shade, TowerCellMeters) };
            Submit(tower, 1, material);

            // 일꾼: 상태는 전투 시간으로 (미는 중 → 쉬기 → 서기)
            bool pushing = sim.BeamRotating;
            WorkersPushing = pushing;
            if (pushing)
            {
                faceLeft = Mathf.DeltaAngle(sim.BeamAngleDegrees, sim.BeamTargetDegrees) < 0f;
                wasPushing = true;
            }
            else if (wasPushing)
            {
                wasPushing = false;
                restStart = time;
            }

            float sinceRest = time - restStart;
            int state = pushing ? Push : sinceRest < RestSeconds + RestHoldSeconds ? Rest : Idle;
            WorkerState = state;
            foreach (Piece p in workers) if (p != null) p.Count = 0;
            for (int i = 0; i < WorkerCount; i++)
            {
                Piece piece = workers[WorkerVariant[i], state];
                if (piece == null) continue;
                int frames = StateFrames[state];
                int frame = state == Push ? (int)(time * 8f) % frames
                    : state == Rest ? Mathf.Min((int)(Mathf.Max(0f, sinceRest) * 3f), frames - 1)
                    : (int)(time * IdleFps + i) % frames;
                bool mirror = state == Idle ? (i == 2) : (i == 2 ? !faceLeft : faceLeft);   // 서 있을 때는 오른쪽 끝이 가운데를 보게, 밀 때는 미는 방향을 본다
                Vector3 p3 = WorkerFeet(i);
                float lean = 0f, bob = 0f;
                if (state == Push)
                {
                    // 미는 8칸은 거의 같다 → 앞뒤로 기우는 흔들림·위아래 들썩임을 코드로 더한다 (한 걸음 1초)
                    float phase = time * Mathf.PI * 2f + i * 1.3f;
                    lean = Mathf.Sin(phase) * LeanMeters * (mirror ? -1f : 1f);
                    bob = Mathf.Abs(Mathf.Sin(phase * 0.5f)) * 0.008f;
                }

                p3 += new Vector3(lean, bob, 0f) + toCamera * 0.005f;
                piece.Instances[piece.Count++] = new PerfLabInstance
                {
                    PosFrame = new float4(p3.x, p3.y, p3.z, mirror ? -frame - 1 : frame),
                    TintSize = new float4(shade, shade, shade, WorkerH * Mpp),
                };
            }

            foreach (Piece p in workers) if (p != null && p.Count > 0) Submit(p, p.Count, material);

            // 탑 대기 전령: 쉬는 동안 탑 곁에 서 있고, 명령을 들면 성벽까지 달려간다 (눈에 보인다)
            if (courierStand != null)
            {
                courierStand.Count = 0;
                if (courierRun != null) courierRun.Count = 0;
                Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
                float soldierSize = 1.4f * BattleSim.WallFigureScale;
                for (int i = 0; i < BattleSim.TowerCourierCount; i++)
                {
                    BattleSim.TowerCourierInfo c = sim.GetTowerCourier(i);
                    Vector3 cp = c.Position;
                    if (c.Busy && courierRun != null)
                    {
                        int frame = (int)(time * 8f) % PerfLabArt.FrameCount(courierRunSheet);
                        bool movingRight = Vector3.Dot(c.Direction, camRight) > 0f;
                        bool flip = PerfLabArt.RunFacesRight(courierRunSheet) ? !movingRight : movingRight;   // 달리기 그림이 보는 쪽 (PerfLabArt.RunFacesRight)
                        courierRun.Instances[courierRun.Count++] = new PerfLabInstance { PosFrame = new float4(cp.x, cp.y, cp.z, flip ? -frame - 1 : frame), TintSize = new float4(shade, shade, shade, soldierSize) };
                    }
                    else
                    {
                        // 달리기 그림이 없으면 서 있는 그림이 통통 튀며 움직인다
                        float bob = c.Busy ? Mathf.Abs(Mathf.Sin(time * 14f)) * 0.12f : 0f;
                        bool movingRight = c.Busy && Vector3.Dot(c.Direction, camRight) > 0f;
                        courierStand.Instances[courierStand.Count++] = new PerfLabInstance { PosFrame = new float4(cp.x, cp.y + bob, cp.z, movingRight ? -1f : 0f), TintSize = new float4(shade, shade, shade, soldierSize) };
                    }
                }

                if (courierStand.Count > 0) Submit(courierStand, courierStand.Count, material);
                if (courierRun != null && courierRun.Count > 0) Submit(courierRun, courierRun.Count, material);
            }

            // 앞 기둥 (일꾼 앞을 지난다)
            if (foreground != null)
            {
                Vector3 f = baseFeet + toCamera * 0.01f;
                foreground.Instances[0] = new PerfLabInstance { PosFrame = new float4(f.x, f.y, f.z, 0f), TintSize = new float4(shade, shade, shade, TowerCellMeters) };
                Submit(foreground, 1, material);
            }

            // 불꽃 (실제 시간으로 일렁임)
            Vector3 bowl = Bowl;
            if (flame != null)
            {
                int frame = (int)(Time.unscaledTime * FlameFps) % FlameFrames;
                float glow = 1.4f;
                float size = FlameCellH * Mpp * FlameScale * Mathf.Lerp(0.55f, 1f, strength);
                flame.Instances[0] = new PerfLabInstance { PosFrame = new float4(bowl.x, bowl.y, bowl.z, frame), TintSize = new float4(glow * 1.2f, glow, glow * 0.8f, size) };
                Submit(flame, 1, material);
            }

            // 탑 뒤 빛 번쩍 (더하기). 세기는 등불 밝기 약·보통·강 (공용 표 BattleSim.GlowLevel)
            if (glowMaterial != null)
            {
                float level = BattleSim.GlowLevel(sim.Lamp);
                float pulse = 0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 7f) + 0.04f * Mathf.Sin(Time.unscaledTime * 13.1f);
                float day = sim.TimeOfDay == TimeOfDay.Night ? 1f : 0.45f;   // 낮에는 햇빛에 묻힌다
                float k = level * pulse * day * Mathf.Lerp(0.6f, 1.15f, strength);
                pillar.Instances[0] = new PerfLabInstance { PosFrame = new float4(bowl.x, bowl.y, bowl.z, 0f), TintSize = new float4(1.0f * k, 0.85f * k, 0.5f * k, 768f * Mpp * PillarScale * (0.9f + 0.1f * level)) };
                Submit(pillar, 1, glowMaterial);
                Piece burst = bursts[Mathf.Clamp((int)sim.Lamp, 0, 2)];
                if (burst != null)
                {
                    burst.Instances[0] = new PerfLabInstance { PosFrame = new float4(bowl.x, bowl.y, bowl.z, 0f), TintSize = new float4(1.5f * k, 1.2f * k, 0.7f * k, 384f * Mpp * BurstScale) };
                    Submit(burst, 1, glowMaterial);
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
            foreach (Piece piece in new[] { tower, foreground, flame, pillar, bursts[0], bursts[1], bursts[2], courierStand, courierRun }) piece?.Buffer?.Release();
            foreach (Piece piece in workers) piece?.Buffer?.Release();
            if (glowMaterial != null) UnityEngine.Object.Destroy(glowMaterial);
        }
    }
}
