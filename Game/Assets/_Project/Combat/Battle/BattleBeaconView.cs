using System;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 가운데 등대 불꽃·화로·일꾼·탑 전령 그림 (W9, Concept_Beacon_v1 9.7 4차 결정). W20: 탑 몸체는 BattleTowerView(돌 메시)가 그리고, 여기서는 불꽃·화로·일꾼 둘(a·b)과 탑 대기 전령만 그린다
    // W27: Codex K-115 화로·반사경(꺼짐·점등)과 불꽃 세 단계(약함·보통·강함)를 불방에 세운다. 꺼진 탑(등대 복구 0)은 꺼진 화로·불꽃 없음
    //      일꾼 셋은 이제 탑 안에서 큰 돌림대를 민다 (BattleTowerInterior) — 문 앞에 서지 않는다. 전령은 그대로 문 앞(sim 소유)
    // - 불꽃: 전투에서는 탑이 '꺼짐' 상태일 수 없다(빛줄기가 돈다)지만 그림은 복구 0이면 꺼진 모습. 등대 복구가 세기를 정한다(BeaconRules.TowerFlameStrength), 점화의 밤이면 완전 점화 불꽃 (정비 마당도 같은 규칙, S4)
    // - 일꾼: 빛줄기가 도는 동안 돌림대 둘레를 돌며 미는 그림(8칸 8fps)으로 돌림대를 같이 돌린다. 끝나면 쉬는 그림을 한 번 재생·마지막 칸 유지, 한동안 뒤 서 있는 그림.
    //   쉬는 동안 한 명(ClimberIndex)이 안쪽 나선 계단을 올라 불방에 갔다 오는 길을 되풀이한다
    // W31 (Codex K-119): 걷기(앞·뒤·왼·오른)·계단 오르기/내려오기(뒤·왼·오른)·화로 관리(앞·왼·오른)·기름통 운반 걷기(네 방향) 시트를 쓴다. 돌림대 밀기는 옛 미는 그림 그대로.
    //   올라갈 때(바닥 걸음·계단 발치·불방 입구)는 기름통을 든 운반 걷기, 불방에서는 화로 관리, 내려올 때는 빈손 걷기·계단 내려오기. 방향은 가는 쪽을 사진기 기준으로 네 방향 중 고른다 (깜빡이지 않게 이전 방향을 조금 더 유지)
    //   새 시트는 칸 96×128 원본(3배 확대본과 같은 세계 크기, 발 (48,120)) — 메모리 1/9, 점 필터라 모양은 같다. 시트가 없으면 옛 미는·서기 그림으로 되돌아간다
    //   모든 움직임은 전투 시간(sim.Elapsed)으로 — 멈춤·지도 시간 멈춤에서 같이 멈춘다 (불꽃 일렁임만 실제 시간)
    // W39 (Codex K-127): 돌림대를 미는 일꾼은 이제 방향별 밀기 시트(밀기 8칸·시작 3칸·멈춤 3칸·힘줌 8칸)를 쓴다. 방향 고르기는 걷기와 같은 규칙, 시작·멈춤은 미는 일이 시작·끝날 때 한 번, 힘줌은 180도 이상 돌리는 명령이 도는 동안(옆모습만).
    //   옛 K-110 미는·쉬는 그림은 시트가 없을 때의 대비책으로만 남는다. 돌림대 막대 높이는 시트의 손 높이에 맞춘다 (BattleTowerInterior.CapstanBarY)
    // 크기: 인물 73점 = 주인공 키(HeroDrawSize), K-110 일꾼 칸은 288×384를 3배 확대본 그대로
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
        static readonly int SheetScaleId = Shader.PropertyToID("_SheetScale");

        // 게임 그림 1점 = 몇 m (인물 키 219점 = 주인공 키)
        public static readonly float Mpp = BattleView.HeroDrawSize / 219f;
        // K-110 탑 칸 1152×960, 발 (720, 864), 화로 가운데 약 (762, 215)
        const float TowerW = 1152f, TowerH = 960f, TowerFootX = 720f, TowerFootY = 864f, BowlPxX = 762f, BowlPxY = 215f;
        public static readonly float TowerCellMeters = TowerH * Mpp;
        public static readonly float BowlHeight = (TowerFootY - BowlPxY) * Mpp;   // 발에서 화로까지 (m) ≈ 1.11
        public static readonly float BowlOffsetX = (BowlPxX - TowerFootX) * Mpp;
        // K-110 일꾼 칸 288×384, 발 (144, 360)
        const float WorkerW = 288f, WorkerH = 384f, WorkerFootY = 360f;
        const int WorkerCount = BattleTowerInterior.WorkerCount;
        static readonly int[] WorkerVariant = { 0, 1, 1 };
        // 옛 K-102 불꽃(6칸 8fps). 칸 288×384, 불꽃 밑·화로 기준점 (144, 126) — K-115 그림이 없을 때의 대비책
        const float FlameCellW = 288f, FlameCellH = 384f, FlameAnchorY = 126f;
        const int FlameFrames = 6;
        const float FlameFps = 8f;
        const float FlameScale = 2.2f;
        const float FlameRoomScale = 1.3f;
        const float RestSeconds = 4f / 3f;       // 쉬는 그림 4칸 3fps 한 번
        const float RestHoldSeconds = 6f;        // 마지막 칸을 이만큼 유지한 뒤 서 있는 그림으로
        const float IdleFps = 4f;
        static readonly string[] FlameSheets = { "flame_warming", "flame_lit", "flame_bright" };   // K-115 불꽃 세 단계 (BattleTowerView.FlameKind)

        readonly BattleSim sim;
        readonly Mesh quad;
        readonly Bounds bounds;
        readonly Material material;      // 도트 그림 셰이더 (BattleView와 같은 것)
        readonly Vector3 baseFeet;
        bool lit;
        float strength;

        readonly Piece[] flames = new Piece[3];   // [단계] K-115 불꽃 (약함·보통·강함). 없으면 flames[0]만 옛 그림
        readonly bool newFlames;
        readonly Piece brazierDead, brazierLit;   // K-115 화로·반사경
        readonly Piece courierStand, courierRun;   // 탑 대기 전령 3명 (병사 그림: 서 있기·달리기, W9)
        readonly Texture2D courierRunSheet;
        readonly Piece[,] workers = new Piece[2, 3];   // [변형 a·b, 상태 idle·push·rest]
        readonly Piece[,,] motionSheets = new Piece[2, 5, 4];   // [변형, WorkMotion, WorkDir] K-119 (없으면 null)
        readonly bool[] workerNewSheet = new bool[WorkerCount];
        readonly Piece[,,] pushSheets = new Piece[2, 4, 4];   // [변형, PushKind, WorkDir] K-127 (처음 쓸 때 읽는다)
        readonly bool[,,] pushTried = new bool[2, 4, 4];
        readonly bool[] workerPushSheet = new bool[WorkerCount];
        readonly PushKind[] workerPushKind = new PushKind[WorkerCount];
        readonly float[] workerPushSince = new float[WorkerCount];
        readonly bool[] workerWasPush = new bool[WorkerCount];
        bool pushDisabled;
        readonly WorkMotion[] workerMotion = new WorkMotion[WorkerCount];
        readonly WorkDir[] workerDir = new WorkDir[WorkerCount];
        const int Idle = 0, Push = 1, Rest = 2;
        static readonly int[] StateFrames = { 4, 8, 4 };

        // K-119 새 동작 시트. 칸 96×128, 발 (48,120) = 정규화 (0.5, 0.0625)
        public enum WorkMotion { Walk, Carry, ClimbUp, ClimbDown, Tend }
        public enum WorkDir { Front, Back, Left, Right }
        static readonly string[] MotionNames = { "walk", "carry", "climb_up", "climb_down", "tend" };
        static readonly string[] DirNames = { "front", "back", "left", "right" };
        static readonly int[] MotionFrames = { 8, 8, 6, 6, 6 };
        static readonly float[] MotionFps = { 8f, 8f, 8f, 8f, 6f };
        const float DirectionHysteresis = 1.25f;   // 축이 바뀌려면 새 축 성분이 이만큼 더 커야 한다 (경계에서 깜빡임 방지)
        static readonly Vector3 LampCenterLocal = new Vector3(0f, BattleTowerView.LampBase, 0f);   // 화로 관리 때 바라보는 곳 (탑 로컬)

        public static string SheetName(int variant, WorkMotion motion, WorkDir dir)
            => $"worker_{(variant == 0 ? "a" : "b")}_{MotionNames[(int)motion]}_{DirNames[(int)dir]}";

        // K-119가 준 방향: 걷기·운반 = 네 방향, 계단 = 뒤·왼·오른, 화로 관리 = 앞·왼·오른
        public static bool SheetProvided(WorkMotion motion, WorkDir dir)
            => motion == WorkMotion.ClimbUp || motion == WorkMotion.ClimbDown ? dir != WorkDir.Front : motion != WorkMotion.Tend || dir != WorkDir.Back;

        // 가는 쪽(moveDir, 수평)을 사진기 기준 방향으로: camRight = 화면 오른쪽, camToward = 사진기 쪽(앞 = 사진기를 봄). previous를 조금 더 유지한다
        public static WorkDir PickDirection(Vector3 moveDir, Vector3 camRight, Vector3 camToward, WorkDir previous, WorkMotion motion)
        {
            moveDir.y = 0f;
            bool prevHorizontal = previous == WorkDir.Left || previous == WorkDir.Right;
            if (moveDir.sqrMagnitude < 1e-8f) return SheetProvided(motion, previous) ? previous : WorkDir.Right;
            float r = Vector3.Dot(moveDir, camRight), f = Vector3.Dot(moveDir, camToward);
            float a = Mathf.Abs(r), b = Mathf.Abs(f);
            bool vertical = prevHorizontal ? b > a * DirectionHysteresis : !(a > b * DirectionHysteresis);
            WorkDir horizontalPick = r > 0f ? WorkDir.Right : r < 0f ? WorkDir.Left : (prevHorizontal ? previous : WorkDir.Right);
            if (!vertical) return horizontalPick;
            WorkDir verticalPick = f >= 0f ? WorkDir.Front : WorkDir.Back;
            return SheetProvided(motion, verticalPick) ? verticalPick : horizontalPick;   // 없는 방향(계단 앞·화로 뒤)은 옆모습
        }

        // W39 (Codex K-127): 일꾼 바퀴 밀기 시트 — 밀기 8칸·시작 3칸(한 번)·멈춤 3칸(한 번 뒤 마지막 칸 유지)·힘줌 8칸(좌우만). 칸 96×128, 발 (48,120)
        // 방향은 K-119 걷기와 같은 규칙(PickDirection)으로 고른다. 시트는 처음 필요할 때 읽는다 (쓰는 방향만 메모리에 — W31과 같은 방식). 없으면 옛 K-110 미는·쉬는 그림으로 되돌아간다
        public enum PushKind { Push, Start, Stop, Strain }
        static readonly string[] PushKindNames = { "push", "push_start", "push_stop", "push_strain" };
        static readonly int[] PushKindFrames = { 8, 3, 3, 8 };
        const float PushFps = 8f, StartFps = 6f, StopFps = 3f, StrainFps = 10f;

        public static string PushSheetName(int variant, PushKind kind, WorkDir dir)
            => $"worker_{(variant == 0 ? "a" : "b")}_{PushKindNames[(int)kind]}_{DirNames[(int)dir]}";

        // K-127가 준 방향: 밀기·시작·멈춤 = 네 방향, 힘줌 = 왼·오른쪽만
        public static bool PushSheetProvided(PushKind kind, WorkDir dir) => kind != PushKind.Strain || dir == WorkDir.Left || dir == WorkDir.Right;

        // 밀기 상태에서 쓸 시트 종류: 막 시작했으면 시작, 반대쪽으로 돌리는 중(BeamReverseTurn)이고 옆모습이면 힘줌, 아니면 밀기. 멈춘 뒤는 멈춤
        public static PushKind PushKindFor(bool pushing, float secondsSincePushStart, bool strain, WorkDir dir)
        {
            if (!pushing) return PushKind.Stop;
            if (secondsSincePushStart < PushKindFrames[(int)PushKind.Start] / StartFps) return PushKind.Start;
            return strain && PushSheetProvided(PushKind.Strain, dir) ? PushKind.Strain : PushKind.Push;
        }

        bool wasPushing;
        float restStart = -999f;
        bool faceLeft;      // 시계 방향(각도가 줄어드는 쪽)이면 일꾼이 왼쪽으로 민다 (사진기에서 보는 앞쪽은 서쪽으로 움직인다)

        // 탑 안 돌림대 (W27): 돌아간 각도(미는 동안만 늘어난다)·마지막 시간·쉬기 시작한 시간 (계단 오르내림 시계)
        readonly Mesh capstanMesh;
        readonly Material capstanMaterial;
        float capstanDegrees;
        float lastTime = -1f;
        float idleSince;
        readonly Vector3[] workerWorld = new Vector3[WorkerCount];
        readonly bool[] workerVisible = new bool[WorkerCount];

        sealed class Piece
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public PerfLabInstance[] Instances;
            public int Count;
        }

        public bool HasArt { get; private set; }
        public Vector3 Feet => baseFeet;
        // 불꽃·빛기둥·번쩍은 높은 탑(BattleTowerView) 꼭대기 불방에서 나온다. 옛 탑 그림(몸체·앞 기둥)은 그리지 않는다
        public Vector3 Bowl => baseFeet + new Vector3(0f, BattleTowerView.LampFloor, 0f);
        // 확인용
        public bool WorkersPushing { get; private set; }
        public int WorkerState { get; private set; }   // 0 서기 · 1 밀기 · 2 쉬기
        public bool FacingLeft => faceLeft;
        public float FlameStrength => strength;
        public bool Lit => lit;
        public bool Dead => BattleTowerView.IsDead(strength);
        public static int WorkerTotal => WorkerCount;
        public bool HasCapstan => capstanMesh != null && capstanMaterial != null;
        public float CapstanDegrees => capstanDegrees;
        public bool HasBrazierArt => brazierLit != null && brazierDead != null;
        public bool HasFlameArt => newFlames;
        public bool WorkerVisible(int i) => workerVisible[i];
        // W31 확인용: i번 일꾼이 지금 K-119 새 시트로 그려지는가 / 그 동작·방향·시트 이름
        public bool WorkerUsesMotionSheet(int i) => workerNewSheet[i] && workerVisible[i];
        public WorkMotion WorkerMotionKind(int i) => workerMotion[i];
        // W39 확인용: i번 일꾼이 지금 K-127 밀기 시트로 그려지는가 / 종류 / 시트 이름 / 지금까지 읽은 시트 수 / 빛줄기가 힘줌 속도인가
        public bool WorkerUsesPushSheet(int i) => workerPushSheet[i] && workerVisible[i];
        public PushKind WorkerPushKind(int i) => workerPushKind[i];
        public string WorkerPushSheetName(int i) => PushSheetName(WorkerVariant[i], workerPushKind[i], workerDir[i]);
        public bool BeamStraining => sim.BeamReverseTurn;   // 180도 이상 돌리는 명령이 도는 동안 (시뮬레이션이 정한다)
        public int PushSheetsLoaded { get { int n = 0; foreach (Piece w in pushSheets) if (w != null) n++; return n; } }
        public WorkDir WorkerFacing(int i) => workerDir[i];
        public string WorkerSheetName(int i) => SheetName(WorkerVariant[i], workerMotion[i], workerDir[i]);
        public bool HasMotionArt => motionSheets[WorkerVariant[BattleTowerInterior.ClimberIndex], (int)WorkMotion.Walk, (int)WorkDir.Left] != null;
        // 시험용: 새 시트를 버려 옛 미는·서기 그림으로 되돌아가는 길을 확인한다
        public void DropPushSheets()
        {
            foreach (Piece w in pushSheets) w?.Buffer?.Release();
            System.Array.Clear(pushSheets, 0, pushSheets.Length);
            pushDisabled = true;
        }

        Piece GetPushSheet(int variant, PushKind kind, WorkDir dir)
        {
            if (pushDisabled || !PushSheetProvided(kind, dir)) return null;
            Piece have = pushSheets[variant, (int)kind, (int)dir];
            if (have != null || pushTried[variant, (int)kind, (int)dir]) return have;
            pushTried[variant, (int)kind, (int)dir] = true;
            var tex = Resources.Load<Texture2D>("BattleArt/Beacon/" + PushSheetName(variant, kind, dir));
            if (tex == null) return null;
            Piece piece = Make(tex, PerfLabArt.SheetLayout.Make(PushKindFrames[(int)kind], 1, 0.5f, 8f / 128f, 96f / 128f), WorkerCount);
            piece.Block.SetFloat(ViewStretchId, 1f);
            pushSheets[variant, (int)kind, (int)dir] = piece;
            return piece;
        }

        public void DropMotionSheets()
        {
            foreach (Piece w in motionSheets) w?.Buffer?.Release();
            System.Array.Clear(motionSheets, 0, motionSheets.Length);
        }

        public BattleBeaconView(BattleSim sim, Material material, Mesh quad, Bounds bounds)
        {
            this.sim = sim;
            this.material = material;
            this.quad = quad;
            this.bounds = bounds;
            baseFeet = new Vector3(0f, BattleSim.WallHeight, 0f);   // 마당 윗면은 성벽 윗면과 같은 높이
            RefreshState();

            // K-115 불꽃 세 단계: 칸 96×128 원본, 발 (48, 120) = 정규화 (0.5, 0.0625)
            var flameLayout = PerfLabArt.SheetLayout.Make(FlameFrames, 1, 0.5f, 8f / 128f, 96f / 128f);
            for (int k = 0; k < flames.Length; k++)
            {
                var tex = Resources.Load<Texture2D>("BattleArt/Tower/" + FlameSheets[k]);
                if (tex == null) { newFlames = false; break; }
                flames[k] = Make(tex, flameLayout, 1);
                flames[k].Block.SetFloat(EmissiveId, 1f);   // 스스로 빛나는 불 (빛번짐)
                newFlames = true;
            }

            if (!newFlames)
            {
                for (int k = 0; k < flames.Length; k++) { flames[k]?.Buffer?.Release(); flames[k] = null; }
                var flameTexture = Resources.Load<Texture2D>(lit ? "BattleArt/Beacon/flame_lit" : "BattleArt/Beacon/flame_warming");
                if (flameTexture != null)
                {
                    flames[0] = Make(flameTexture, PerfLabArt.SheetLayout.Make(FlameFrames, 1, 0.5f, (FlameCellH - FlameAnchorY) / FlameCellH, FlameCellW / FlameCellH), 1);
                    flames[0].Block.SetFloat(EmissiveId, 1f);
                }
            }

            var brazierLayout = PerfLabArt.SheetLayout.Make(1, 1, 0.5f, 8f / 128f, 96f / 128f);
            var bd = Resources.Load<Texture2D>("BattleArt/Tower/brazier_lens_dead");
            var bl = Resources.Load<Texture2D>("BattleArt/Tower/brazier_lens_lit");
            if (bd != null) { brazierDead = Make(bd, brazierLayout, 1); brazierDead.Block.SetFloat(EmissiveId, 0f); }
            if (bl != null) { brazierLit = Make(bl, brazierLayout, 1); brazierLit.Block.SetFloat(EmissiveId, 0.3f); }

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

            int climberVariant = WorkerVariant[BattleTowerInterior.ClimberIndex];   // K-119 시트는 계단 일꾼 외형 것만 올린다 (나머지 둘은 돌림대만 민다)
            for (int v = climberVariant; v <= climberVariant; v++)
                for (int m = 0; m < MotionNames.Length; m++)
                {
                    var motionLayout = PerfLabArt.SheetLayout.Make(MotionFrames[m], 1, 0.5f, 8f / 128f, 96f / 128f);
                    for (int d = 0; d < DirNames.Length; d++)
                    {
                        if (!SheetProvided((WorkMotion)m, (WorkDir)d)) continue;
                        var tex = Resources.Load<Texture2D>("BattleArt/Beacon/" + SheetName(v, (WorkMotion)m, (WorkDir)d));
                        if (tex == null) continue;
                        motionSheets[v, m, d] = Make(tex, motionLayout, WorkerCount);
                        motionSheets[v, m, d].Block.SetFloat(ViewStretchId, 1f);
                    }
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

            HasArt = newFlames || flames[0] != null || brazierLit != null;
            foreach (Piece w in workers) if (w != null) HasArt = true;   // 옛 탑 몸체 그림 유무와 상관없이 불꽃·화로·일꾼이 있으면 그린다
            foreach (Piece w in motionSheets) if (w != null) HasArt = true;

            // 돌림대 (나무 칸 그림). 그림이 없으면 일꾼만 돌림대 둘레를 돈다
            var atlas = Resources.Load<Texture2D>("BattleArt/Courtyard/props_box_atlas");
            if (atlas != null)
            {
                capstanMesh = BattleTowerInterior.BuildCapstanMesh();
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                capstanMaterial = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Tower Capstan (runtime)" };
                if (capstanMaterial.HasProperty("_BaseMap")) capstanMaterial.SetTexture("_BaseMap", atlas);
                capstanMaterial.mainTexture = atlas;  BattleSeeThrough.Adopt(capstanMaterial);   // 돌림대도 주인공을 가릴 수 있다 — 투시 구멍 대상 (W42)
                if (capstanMaterial.HasProperty("_Smoothness")) capstanMaterial.SetFloat("_Smoothness", 0.1f);
                if (capstanMaterial.HasProperty("_Metallic")) capstanMaterial.SetFloat("_Metallic", 0f);
            }

            for (int i = 0; i < WorkerCount; i++)
            {
                workerWorld[i] = baseFeet + BattleTowerInterior.WorkerStation(i, 0f);
                workerVisible[i] = true;
            }
        }

        // 계단 오르던 일꾼이 돌림대로 돌아오는 길 (빛줄기가 돌기 시작할 때). 0 = 계단을 걸어 내려옴, 1 = 바닥을 걸어 돌림대 자리로
        bool returning;
        int returnStage;
        float returnS;
        Vector3 returnPos;
        const float ReturnStairSeconds = 3f, ReturnWalkSpeed = 0.9f;

        void BeginReturn()
        {
            int c = BattleTowerInterior.ClimberIndex;
            Vector3 last = workerWorld[c] - baseFeet;
            bool inLamp = last.y > BattleTowerView.LampBase - 0.2f;
            bool onStair = last.y > BattleTowerInterior.FloorY + 0.2f;
            if (!workerVisible[c] || inLamp) { returning = true; returnStage = 0; returnS = 1f; }   // 벽 속·불방 → 계단 맨 위에서 내려온다
            else if (onStair) { returning = true; returnStage = 0; returnS = Mathf.Clamp01((last.y - BattleTowerInterior.FloorY) / (BattleTowerInterior.StairTopY - BattleTowerInterior.FloorY)); }
            else if ((last - BattleTowerInterior.WorkerStation(c, capstanDegrees)).sqrMagnitude > 0.0025f) { returning = true; returnStage = 1; returnPos = last; }
            else return;
            returnPos = returnStage == 0 ? BattleTowerInterior.StairPoint(returnS) : returnPos;
        }

        Vector3 AdvanceReturn(float dt, out Vector3 moveDir)
        {
            moveDir = Vector3.zero;
            if (returnStage == 0)
            {
                returnS -= dt / ReturnStairSeconds;
                Vector3 now = BattleTowerInterior.StairPoint(Mathf.Max(0f, returnS));
                moveDir = BattleTowerInterior.StairPoint(Mathf.Max(0f, returnS - 0.03f)) - now;
                moveDir.y = 0f;
                returnPos = now;
                if (returnS <= 0f) returnStage = 1;
                return returnPos;
            }

            Vector3 target = BattleTowerInterior.WorkerStation(BattleTowerInterior.ClimberIndex, capstanDegrees);
            Vector3 to = target - returnPos;
            to.y = 0f;
            float step = ReturnWalkSpeed * dt;
            returnPos.y = Mathf.MoveTowards(returnPos.y, BattleTowerInterior.FloorY, 0.6f * dt);
            if (to.magnitude <= step) { returning = false; returnPos = target; return target; }
            moveDir = to;
            returnPos += to.normalized * step;
            return returnPos;
        }

        // 등대 복구·점화의 밤은 전투 중에도 바뀔 수 있다 (시험·사진) — 그릴 때마다 읽는다
        void RefreshState()
        {
            lit = sim.IsIgnitionNight;
            strength = BeaconRules.TowerFlameStrength(sim.BeaconRepair, lit);
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

        // 일꾼 i가 지금 서 있는 자리 (월드 발 위치). 탑 안이다 — 마지막으로 그린 자리 (그리기 전에는 돌림대 둘레 처음 자리)
        public Vector3 WorkerFeet(int i) => workerWorld[i];

        // 작업이 끝난 뒤(sim.Complete 후), 지도 모드가 아닐 때
        public void Draw(Camera camera = null)
        {
            if (!HasArt) return;
            RefreshState();
            Camera cam = camera != null ? camera : Camera.main;   // 전투 사진기는 주 사진기 표시가 없어 BattleView가 넘겨 준다
            Vector3 toCamera = cam != null ? -cam.transform.forward : Vector3.back;   // 화면 위치는 그대로 두고 깊이만 가까이 (같은 평면 겹침 순서)
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            float shade = sim.Brightness;
            float time = sim.Elapsed;
            float dt = lastTime < 0f ? 0f : Mathf.Clamp(time - lastTime, 0f, 0.25f);
            lastTime = time;
            // 탑 몸체: BattleTowerView(돌 메시)가 그린다 — 옛 그림은 그리지 않는다

            // 일꾼: 상태는 전투 시간으로 (미는 중 → 쉬기 → 서기)
            bool pushing = sim.BeamRotating;
            WorkersPushing = pushing;
            if (pushing && !wasPushing && !returning) BeginReturn();
            if (pushing)
            {
                faceLeft = Mathf.DeltaAngle(sim.BeamAngleDegrees, sim.BeamTargetDegrees) < 0f;
                wasPushing = true;
                capstanDegrees = Mathf.Repeat(capstanDegrees + (faceLeft ? 1f : -1f) * BattleTowerInterior.PushDegreesPerSecond * dt, 360f);
                idleSince = time;   // 계단 오르내림은 쉬기 시작할 때부터 센다
            }
            else if (wasPushing)
            {
                wasPushing = false;
                restStart = time;
                idleSince = time;
            }

            float sinceRest = time - restStart;
            int state = pushing ? Push : sinceRest < RestSeconds + RestHoldSeconds ? Rest : Idle;
            WorkerState = state;
            foreach (Piece p in workers) if (p != null) p.Count = 0;
            foreach (Piece p in motionSheets) if (p != null) p.Count = 0;
            foreach (Piece p in pushSheets) if (p != null) p.Count = 0;
            float climbClock = time - idleSince;
            Vector3 camToward = new Vector3(toCamera.x, 0f, toCamera.z);   // 사진기 쪽 (수평)
            camToward = camToward.sqrMagnitude > 1e-6f ? camToward.normalized : Vector3.back;
            Vector3 camRightFlat = new Vector3(camRight.x, 0f, camRight.z);
            camRightFlat = camRightFlat.sqrMagnitude > 1e-6f ? camRightFlat.normalized : Vector3.right;
            for (int i = 0; i < WorkerCount; i++)
            {
                Vector3 local = BattleTowerInterior.WorkerStation(i, capstanDegrees);
                int wstate = state;
                Vector3 moveDir = Vector3.zero;   // 걷는 방향 (탑 로컬, 그림이 보는 쪽을 정한다)
                bool visible = true;
                bool hasMotion = false;           // K-119 동작 그림을 쓸 차례인가
                var motion = WorkMotion.Walk;
                if (returning && i == BattleTowerInterior.ClimberIndex)
                {
                    // 계단·불방에 있던 일꾼은 빛줄기가 돌기 시작해도 튀지 않고 걸어 내려와 돌림대로 돌아온다 (계단은 내려오기, 바닥은 빈손 걷기)
                    wstate = Push;
                    local = AdvanceReturn(dt, out moveDir);
                    idleSince = time;
                    if (returning) { hasMotion = true;   // 도착해 returning이 꺼진 프레임은 미는 그림 그대로
                        motion = returnStage == 0 ? WorkMotion.ClimbDown : WorkMotion.Walk; }
                }
                else if (state == Push)
                {
                    // 돌림대 둘레 원의 접선 방향으로 간다 (각도가 늘면 (-sin, cos))
                    float a = (capstanDegrees + 60f + i * 120f) * Mathf.Deg2Rad;
                    float sign = faceLeft ? 1f : -1f;
                    moveDir = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * sign;
                }
                else if (i == BattleTowerInterior.ClimberIndex)
                {
                    Vector3 station = local;
                    var phase = BattleTowerInterior.Climber(climbClock, station, out Vector3 feet, out BattleTowerInterior.ClimbMotion cm);
                    if (phase == BattleTowerInterior.ClimbPhase.Hidden) visible = false;
                    else
                    {
                        local = feet;
                        if (phase == BattleTowerInterior.ClimbPhase.Walk)
                        {
                            wstate = Push;   // 새 시트가 없을 때의 대비 (옛 미는 그림)
                            BattleTowerInterior.Climber(climbClock + 0.1f, station, out Vector3 next);
                            moveDir = next - feet;
                            moveDir.y = 0f;
                            hasMotion = true;
                            motion = cm == BattleTowerInterior.ClimbMotion.ClimbUp ? WorkMotion.ClimbUp
                                : cm == BattleTowerInterior.ClimbMotion.ClimbDown ? WorkMotion.ClimbDown
                                : cm == BattleTowerInterior.ClimbMotion.WalkCarry ? WorkMotion.Carry : WorkMotion.Walk;
                        }
                        else if (phase == BattleTowerInterior.ClimbPhase.Tend)
                        {
                            wstate = Idle;
                            moveDir = LampCenterLocal - local;   // 화로를 바라보며 기름을 붓는다
                            hasMotion = true;
                            motion = WorkMotion.Tend;
                        }
                    }
                }

                workerVisible[i] = visible;
                bool pushNow = !hasMotion && state == Push;   // 돌림대를 실제로 미는 중 (걸어 돌아오는 일꾼은 제외)
                if (pushNow && !workerWasPush[i]) workerPushSince[i] = time;
                workerWasPush[i] = pushNow;
                workerWorld[i] = baseFeet + local;
                if (!visible) continue;

                workerNewSheet[i] = false;
                workerPushSheet[i] = false;
                if (!hasMotion && (pushNow || wstate == Rest))
                {
                    // K-127: 밀기·시작·멈춤·힘줌. 멈춤은 마지막으로 향하던 쪽 그대로 (가는 쪽이 없으면 이전 방향)
                    WorkDir pdir = PickDirection(moveDir, camRightFlat, camToward, workerDir[i], WorkMotion.Walk);
                    float since = time - workerPushSince[i];
                    PushKind want = PushKindFor(pushNow, since, sim.BeamReverseTurn, pdir);
                    PushKind got = want;
                    Piece ps = GetPushSheet(WorkerVariant[i], got, pdir);
                    if (ps == null && (want == PushKind.Start || want == PushKind.Strain)) { got = PushKind.Push; ps = GetPushSheet(WorkerVariant[i], got, pdir); }   // 시작·힘줌이 없으면 밀기로
                    if (ps != null)
                    {
                        int pframe = got == PushKind.Start ? Mathf.Min((int)(Mathf.Max(0f, since) * StartFps), 2)
                            : got == PushKind.Stop ? Mathf.Min((int)(Mathf.Max(0f, sinceRest) * StopFps), 2)
                            : (int)(time * (got == PushKind.Strain ? StrainFps : PushFps) + i * 3) % PushKindFrames[(int)got];
                        Vector3 pp = baseFeet + local + toCamera * 0.005f;
                        ps.Instances[ps.Count++] = new PerfLabInstance
                        {
                            PosFrame = new float4(pp.x, pp.y, pp.z, pframe),
                            TintSize = new float4(shade, shade, shade, WorkerH * Mpp),
                        };
                        workerDir[i] = pdir;
                        workerPushSheet[i] = true;
                        workerPushKind[i] = got;
                        continue;
                    }
                }
                if (hasMotion)
                {
                    WorkDir dir = PickDirection(moveDir, camRightFlat, camToward, workerDir[i], motion);
                    Piece sheet = motionSheets[WorkerVariant[i], (int)motion, (int)dir];
                    workerDir[i] = dir;
                    workerMotion[i] = motion;
                    if (sheet != null)
                    {
                        // 새 시트: 한 줄 8칸(화로 6칸) 왼쪽부터. 왼쪽 시트는 따로 있어 뒤집지 않는다. 위아래 들썩임은 시트 안에 있다
                        int mframes = MotionFrames[(int)motion];
                        int mframe = (int)(time * MotionFps[(int)motion] + i * 3) % mframes;
                        Vector3 mp = baseFeet + local + toCamera * 0.005f;
                        sheet.Instances[sheet.Count++] = new PerfLabInstance
                        {
                            PosFrame = new float4(mp.x, mp.y, mp.z, mframe),
                            TintSize = new float4(shade, shade, shade, WorkerH * Mpp),
                        };
                        workerNewSheet[i] = true;
                        continue;
                    }

                    wstate = motion == WorkMotion.Tend ? Idle : Push;   // 시트가 없으면 옛 그림
                }

                Piece piece = workers[WorkerVariant[i], wstate];
                if (piece == null) continue;
                int frames = StateFrames[wstate];
                int frame = wstate == Push ? (int)(time * 8f) % frames
                    : wstate == Rest ? Mathf.Min((int)(Mathf.Max(0f, sinceRest) * 3f), frames - 1)
                    : (int)(time * IdleFps + i) % frames;
                bool mirror = moveDir.sqrMagnitude > 1e-6f ? Vector3.Dot(moveDir, camRight) < 0f : (i == 2);   // 가는 쪽을 본다. 서 있을 때는 오른쪽 끝이 가운데를 보게
                Vector3 p3 = baseFeet + local;
                float bob = 0f;
                if (wstate == Push) bob = Mathf.Abs(Mathf.Sin((time * Mathf.PI * 2f + i * 1.3f) * 0.5f)) * 0.008f;   // 한 걸음 1초, 위아래 들썩임
                p3 += new Vector3(0f, bob, 0f) + toCamera * 0.005f;
                piece.Instances[piece.Count++] = new PerfLabInstance
                {
                    PosFrame = new float4(p3.x, p3.y, p3.z, mirror ? -frame - 1 : frame),
                    TintSize = new float4(shade, shade, shade, WorkerH * Mpp),
                };
            }

            // 돌림대: 미는 동안 같이 돈다 (그리기 1번, 새로 만드는 것 없음)
            if (HasCapstan)
                Graphics.DrawMesh(capstanMesh, Matrix4x4.TRS(baseFeet + new Vector3(0f, BattleTowerInterior.FloorY, 0f), Quaternion.Euler(0f, -capstanDegrees, 0f), Vector3.one),
                    capstanMaterial, 0, null, 0, null, false, false);

            foreach (Piece p in workers) if (p != null && p.Count > 0) Submit(p, p.Count, material);
            foreach (Piece p in motionSheets) if (p != null && p.Count > 0) Submit(p, p.Count, material);
            foreach (Piece p in pushSheets) if (p != null && p.Count > 0) Submit(p, p.Count, material);

            // 탑 대기 전령: 쉬는 동안 탑 곁에 서 있고, 명령을 들면 성벽까지 달려간다 (눈에 보인다)
            if (courierStand != null)
            {
                courierStand.Count = 0;
                if (courierRun != null) courierRun.Count = 0;
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

            // 화로·반사경 (꺼진 탑은 꺼진 그림) — 불방 가운데, 창틀 받침보다 조금 위
            Vector3 lamp = baseFeet + new Vector3(0f, BattleTowerView.LampBase, 0f);
            bool dead = BattleTowerView.IsDead(strength);
            Piece brazier = dead ? brazierDead : brazierLit;
            if (brazier != null)
            {
                Vector3 bp = lamp + new Vector3(0f, BattleTowerView.BrazierLift, 0f);
                brazier.Instances[0] = new PerfLabInstance { PosFrame = new float4(bp.x, bp.y, bp.z, 0f), TintSize = new float4(shade, shade, shade, BattleTowerView.SpriteCellMeters) };
                Submit(brazier, 1, material);
            }

            // 불꽃 (실제 시간으로 일렁임). 꺼진 탑은 없다. 세기에 따라 약함·보통·강함 그림
            int kind = newFlames ? BattleTowerView.FlameKind(strength) : (flames[0] != null ? 0 : -1);
            Piece flame = kind >= 0 ? flames[kind] : null;
            if (flame != null)
            {
                int frame = (int)(Time.unscaledTime * FlameFps) % FlameFrames;
                float glow = 1.4f;
                float size, y;
                if (newFlames)
                {
                    size = BattleTowerView.SpriteCellMeters;
                    y = lamp.y + BattleTowerView.FlameLift;
                }
                else
                {
                    size = FlameCellH * Mpp * FlameScale * FlameRoomScale * Mathf.Lerp(0.55f, 1f, strength);
                    y = Bowl.y;
                }

                flame.Instances[0] = new PerfLabInstance { PosFrame = new float4(lamp.x, y, lamp.z, frame), TintSize = new float4(glow * 1.2f, glow, glow * 0.8f, size) };
                Submit(flame, 1, material);
            }
        }

        void Submit(Piece piece, int count, Material mat)
        {
            piece.Buffer.SetData(piece.Instances, 0, 0, count);
            Graphics.DrawMeshInstancedProcedural(quad, 0, mat, bounds, count, piece.Block);
        }

        public void Dispose()
        {
            foreach (Piece piece in flames) piece?.Buffer?.Release();
            foreach (Piece piece in new[] { brazierDead, brazierLit, courierStand, courierRun }) piece?.Buffer?.Release();
            foreach (Piece piece in workers) piece?.Buffer?.Release();
            foreach (Piece piece in motionSheets) piece?.Buffer?.Release();
            foreach (Piece piece in pushSheets) piece?.Buffer?.Release();
            Kill(capstanMesh);
            Kill(capstanMaterial);
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);   // 편집 모드 시험
        }
    }
}
