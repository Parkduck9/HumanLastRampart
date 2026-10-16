using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.CombatContracts;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 효과 도트 그림 (카드 K-015 — Codex K-012 효과 시트 6종 중 5종, 사용자 외형 검토 전 미리 연결)
    // - 시트: 256×256 칸 6개 가로 한 줄, 아래 가운데 기준점, 초당 10칸 (Resources/BattleArt/Effects)
    // - 마법 폭발: 폭발 대기(Pending)가 끝나 터지는 순간부터 한 번 / 함정 폭발: 함정이 Exploded로 바뀌는 순간 한 번
    // - 불바다: 지속 중 반복, 범위 안 여러 자리에 나눠 그린다 (끝날 때 1초 동안 작아진다)
    // - 맞음 불꽃: 화살·총알이 땅에 닿은 자리(투사체 그림이 크기 0으로 바뀐 칸)에 짧게. 대포알은 빼고, 동시에 최대 HitCapacity개
    // - 횃불 불꽃: 밤에 BattleLights와 같은 규칙으로 계산한 횃불 자리에 반복
    // - 지도 모드면 그리지 않는다(상태 추적만). 그림이 없는 종류는 그리지 않는다
    // 같은 PerfLabSprite 셰이더(카메라를 보는 판)로 그리고, 그림자는 내지 않는다
    public sealed class BattleEffectsView : IDisposable
    {
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int HasNormalId = Shader.PropertyToID("_HasNormal");

        const int Frames = 6;
        const float Fps = 10f;
        const float OneShotSeconds = Frames / Fps;   // 0.6초

        const int BurstCapacity = 32;                // 함정 폭발 + 대포알 떨어진 자리 (K-036: 16에서 늘림)
        const float CannonBurstPerRadius = 1.2f;     // 대포알 폭발 판 크기 = 범위 반경 × 1.2 (함정·마법 2배보다 작게)
        const float CannonBurstMinSize = 1.5f;
        const int FireCapacity = 96;                 // 불바다 조각 전체
        const int HitCapacity = 48;
        const float BurstSizePerRadius = 2f;         // 판 높이 = 반경 × 2 (그림 폭이 칸의 약 90%)
        const float FirePieceArea = 30f;             // 조각 하나가 맡는 넓이(m²) — 반경 5m면 약 3조각
        const int FireMinPieces = 3, FireMaxPieces = 12;
        const float HitSize = 1.2f;                  // 불꽃 그림은 칸의 약 1/3
        const float CannonShotSize = 1f;             // 투사체 그림 크기가 이보다 크면 대포알 (WriteShotInstancesJob 1.6)

        // 횃불 자리 — BattleLights와 같은 규칙 (그쪽 값이 바뀌면 함께 맞춘다)
        const float TorchSpacing = BattleLights.TorchSpacing;   // 빛과 같은 자리 (D-071 피드백: 그림과 빛이 따로 놀던 것)
        const float TorchInset = BattleLights.TorchInset;
        const float TorchFootHeight = BattleLights.TorchHeight - 0.1f;   // 빛보다 조금 아래에서 불꽃이 솟는다
        const float TorchSize = 0.35f;   // 병사 키(0.35m)와 같은 칸 크기 — 0.9는 병사보다 훨씬 컸다

        // 횃불대 (K-031, Codex K-018 시안 torch_mount_off): 칸 128×256, 부착 기준점 (64,224), 보이는 높이 약 0.3m(192화소).
        // 낮·밤 모두 같은 받침을 통로 위에 세우고, 밤 불꽃은 시안 겹침(manifest flameBaseTopLeft (52,36), 불꽃 칸 36화소)대로 받침 머리에 얹는다
        const float MountSize = 0.3f * 256f / 192f;                    // 칸 높이(m) 0.4 — 보이는 그림 0.3m
        const float MountFlameFoot = (224f - 72f) / 256f * MountSize;   // 받침 발에서 불꽃 칸 바닥까지 0.2375m (= 빛 높이 0.24m와 거의 같다)
        const float MountFlameSize = 36f / 256f * MountSize;            // 불꽃 칸 0.056m (시안 비율 — 화면에서 너무 작으면 이 값을 키운다)

        // 빛을 받는 그림(D-062)이라 밤에는 어두워진다 — 스스로 빛나는 불·마법은 색을 올려 덜 어둡게 (화면 확인 뒤 조정)
        const float DayGlow = 1.15f, NightGlow = 2.6f;

        readonly BattleSim sim;
        readonly Material material;
        readonly Mesh quad;
        readonly Bounds bounds;

        readonly Kind magic, fire, trap, hit, torch, mount;
        BattleFieldArt fieldArt;                      // 들판 소품·시체 더미·함정·먼 산·하늘 (K-031) — BattleView가 Attach로 붙인다
        readonly List<(Vector3 position, float size, float age)> magicPlays = new List<(Vector3, float, float)>();
        readonly List<(Vector3 position, float size, float age)> trapPlays = new List<(Vector3, float, float)>();
        readonly List<(Vector3 position, float age)> hitPlays = new List<(Vector3, float)>();
        readonly List<Vector3> pendingBefore = new List<Vector3>();   // 지난 프레임 폭발 대기 자리 (터지는 순간 찾기)
        readonly List<Vector3> pendingNow = new List<Vector3>();
        readonly BattleSim.TrapState[] trapStates;
        readonly float[] shotSizes;                   // 지난 프레임 투사체 그림 크기 (0으로 바뀌면 착지)
        readonly int[] shotWeapons;                   // 칸마다 날아가던 무기 번호 (대포알 폭발 크기)
        readonly List<Vector3> torchFeet = new List<Vector3>();
        readonly List<Vector3> mountFeet = new List<Vector3>();   // 횃불대 받침 발 (통로 위)
        float torchSize = TorchSize;
        float clock;

        sealed class Kind
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public PerfLabInstance[] Instances;
            public int Count;
        }

        public bool HasBurstArt => magic != null && trap != null;
        public bool HasFireArt => fire != null;

        public BattleEffectsView(BattleSim sim, Material material, Mesh quad, Bounds bounds)
        {
            this.sim = sim;
            this.material = material;
            this.quad = quad;
            this.bounds = bounds;

            magic = MakeKind("magic_burst", BurstCapacity);
            fire = MakeKind("fire_field", FireCapacity);
            trap = MakeKind("trap_burst", BurstCapacity);
            hit = MakeKind("arrow_hit", HitCapacity);

            trapStates = new BattleSim.TrapState[sim.TrapCount];
            for (int i = 0; i < trapStates.Length; i++) trapStates[i] = sim.GetTrap(i).State;
            if (hit != null || trap != null)
            {
                shotSizes = new float[sim.ShotCapacity];
                shotWeapons = new int[sim.ShotCapacity];
            }

            bool hasMount = Resources.Load<Texture2D>("BattleArt/Props/torch_mount_off") != null;
            BuildTorchFeet(hasMount);
            torch = MakeKind("torch_flame", Mathf.Max(1, torchFeet.Count));
            if (hasMount) mount = MakeKind("Props/torch_mount_off", Mathf.Max(1, mountFeet.Count), PerfLabArt.SheetLayout.Make(1, 1, 0.5f, (256f - 224f) / 256f, 128f / 256f), "BattleArt/");
        }

        // 들판 그림 묶음을 붙인다 (BattleView 생성자에서 한 줄). 그리기·정리는 이 효과 그림과 같이 한다
        public void Attach(BattleFieldArt art) => fieldArt = art;

        // 시체 더미 그림(K-024)이 있으면 BattleView의 상자 더미 대신 그림으로 그린다
        public bool DrawsPiles => fieldArt != null && fieldArt.HasPileArt;

        Kind MakeKind(string name, int capacity) => MakeKind(name, capacity, PerfLabArt.SheetLayout.Make(Frames, 1, 0.5f, 0f, 1f), "BattleArt/Effects/");

        Kind MakeKind(string name, int capacity, PerfLabArt.SheetLayout layout, string folder)
        {
            var texture = Resources.Load<Texture2D>(folder + name);
            if (texture == null) return null;
            var kind = new Kind { Block = new MaterialPropertyBlock(), Instances = new PerfLabInstance[capacity] };
            kind.Block.SetTexture(MainTexId, texture);
            kind.Block.SetVector(GridId, layout.Grid);
            kind.Block.SetVector(PivotId, layout.Pivot);
            kind.Block.SetFloat(AspectId, layout.Aspect);
            kind.Block.SetFloat(HasNormalId, 0f);
            kind.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, PerfLabInstance.Stride);
            kind.Block.SetBuffer(InstancesId, kind.Buffer);
            return kind;
        }

        // BattleLights.BuildTorches와 같은 계산: 네 성벽 안쪽 가장자리를 따라 간격마다, 가운데 맞춤
        // 받침 그림이 있으면 받침은 통로 위에 서고, 불꽃은 받침 머리(시안 접점)에서 시안 비율 크기로 솟는다
        void BuildTorchFeet(bool hasMount)
        {
            float flameFoot = hasMount ? MountFlameFoot : TorchFootHeight;
            torchSize = hasMount ? MountFlameSize : TorchSize;
            float inner = sim.CastleHalf - TorchInset;
            int count = Mathf.Max(1, Mathf.FloorToInt((inner * 2f) / TorchSpacing));
            float start = -(count - 1) * TorchSpacing * 0.5f;
            for (int side = 0; side < 4; side++)
            {
                float3 n = WallGeometry.Outward(side);
                float3 r = WallGeometry.Right(side);
                for (int k = 0; k < count; k++)
                {
                    float along = start + k * TorchSpacing;
                    torchFeet.Add(new Vector3(n.x * inner + r.x * along, BattleSim.WallHeight + flameFoot, n.z * inner + r.z * along));
                    if (hasMount) mountFeet.Add(new Vector3(n.x * inner + r.x * along, BattleSim.WallHeight, n.z * inner + r.z * along));
                }
            }
        }

        // 작업이 끝난 뒤(sim.Complete 후) 매 프레임. 지도 모드여도 불러야 터지는 순간을 놓치지 않는다
        public void Draw(bool mapMode)
        {
            float dt = Time.deltaTime;
            clock += dt;
            Track(dt);
            fieldArt?.Draw(mapMode);
            if (mapMode) return;

            bool night = sim.TimeOfDay == TimeOfDay.Night;
            float glow = HumanLastRampart.Combat.Render.BattleLook.LitActive ? (night ? NightGlow : DayGlow) : 1f;

            if (magic != null) DrawOneShots(magic, magicPlays, glow);
            if (trap != null) DrawOneShots(trap, trapPlays, glow);

            if (hit != null)
            {
                hit.Count = 0;
                foreach (var (position, age) in hitPlays)
                    hit.Instances[hit.Count++] = Instance(position, OneShotFrame(age), HitSize, glow);
                Submit(hit);
            }

            if (fire != null)
            {
                fire.Count = 0;
                IReadOnlyList<BattleSim.SpellEffect> effects = sim.Effects;
                for (int i = 0; i < effects.Count; i++)
                {
                    BattleSim.SpellEffect e = effects[i];
                    if (!e.Fire) continue;
                    AddFirePieces(e, glow);
                }

                Submit(fire);
            }

            if (torch != null && night)
            {
                torch.Count = 0;
                for (int i = 0; i < torchFeet.Count; i++)
                    torch.Instances[torch.Count++] = Instance(torchFeet[i], LoopFrame(i * 2.3f), torchSize, glow);
                Submit(torch);
            }

            // 횃불대 받침: 낮에도 보인다. 빛을 받는 그림이라 밝기만 맞추고 그림자는 낸다
            if (mount != null)
            {
                float shade = sim.Brightness;
                if (mount.Count != mountFeet.Count || !Mathf.Approximately(mount.Instances[0].TintSize.x, shade))
                {
                    mount.Count = mountFeet.Count;
                    for (int i = 0; i < mountFeet.Count; i++) mount.Instances[i] = Instance(mountFeet[i], 0, MountSize, shade);
                    mount.Buffer.SetData(mount.Instances, 0, 0, mount.Count);
                }

                if (mount.Count > 0) Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, mount.Count, mount.Block);
            }
        }

        // 터지는 순간 찾기·재생 시간 진행 (지도 모드에서도)
        void Track(float dt)
        {
            Age(magicPlays, dt);
            Age(trapPlays, dt);
            for (int i = hitPlays.Count - 1; i >= 0; i--)
            {
                var play = hitPlays[i];
                play.age += dt;
                if (play.age >= OneShotSeconds) hitPlays.RemoveAt(i);
                else hitPlays[i] = play;
            }

            // 마법 폭발: 지난 프레임 대기 중이던 자리가 터짐(대기 아님·불 아님)으로 바뀌면. 함정 폭발 효과는 대기를 거치지 않아 걸리지 않는다
            pendingNow.Clear();
            IReadOnlyList<BattleSim.SpellEffect> effects = sim.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                BattleSim.SpellEffect e = effects[i];
                if (e.Pending)
                {
                    pendingNow.Add(e.Position);
                    continue;
                }

                if (e.Fire || magic == null) continue;
                int was = pendingBefore.FindIndex(p => (p - e.Position).sqrMagnitude < 0.0001f);
                if (was < 0) continue;
                pendingBefore.RemoveAt(was);
                if (magicPlays.Count < BurstCapacity) magicPlays.Add((e.Position, e.Radius * BurstSizePerRadius, 0f));
            }

            pendingBefore.Clear();
            pendingBefore.AddRange(pendingNow);

            // 함정 폭발: Exploded로 바뀌는 순간
            int traps = Mathf.Min(trapStates.Length, sim.TrapCount);
            for (int i = 0; i < traps; i++)
            {
                BattleSim.TrapInfo info = sim.GetTrap(i);
                if (info.State == BattleSim.TrapState.Exploded && trapStates[i] != BattleSim.TrapState.Exploded && trap != null && trapPlays.Count < BurstCapacity)
                    trapPlays.Add((info.Position, info.DamageRadius * BurstSizePerRadius, 0f));
                trapStates[i] = info.State;
            }

            // 맞음 불꽃: 화살·총알 그림 크기가 0으로 바뀐 칸 = 이번 프레임 착지 (빗나간 화살도 땅에 닿은 자리에 난다)
            if (shotSizes == null) return;
            NativeArray<PerfLabInstance> shots = sim.ShotInstances;
            int slots = Mathf.Min(shotSizes.Length, shots.Length);
            for (int i = 0; i < slots; i++)
            {
                PerfLabInstance shot = shots[i];
                float size = shot.TintSize.w;
                float before = shotSizes[i];
                shotSizes[i] = size;
                if (size > 0f) shotWeapons[i] = (int)shot.PosFrame.w;   // 날아가는 동안 칸 번호 = 무기 번호 (K-032)
                if (before <= 0f || size > 0f) continue;

                // 대포알이 떨어진 자리: 함정 폭발 그림을 범위보다 작게 한 번 (K-036). 땅 높이에서
                if (before > CannonShotSize)
                {
                    if (trap == null || trapPlays.Count >= BurstCapacity) continue;
                    float radius = sim.WeaponAreaRadius(shotWeapons[i]);
                    trapPlays.Add((new Vector3(shot.PosFrame.x, 0f, shot.PosFrame.z), Mathf.Max(CannonBurstMinSize, radius * CannonBurstPerRadius), 0f));
                    continue;
                }

                if (hit == null || hitPlays.Count >= HitCapacity) continue;
                hitPlays.Add((new Vector3(shot.PosFrame.x, shot.PosFrame.y, shot.PosFrame.z), 0f));
            }
        }

        static void Age(List<(Vector3 position, float size, float age)> plays, float dt)
        {
            for (int i = plays.Count - 1; i >= 0; i--)
            {
                var play = plays[i];
                play.age += dt;
                if (play.age >= OneShotSeconds) plays.RemoveAt(i);
                else plays[i] = play;
            }
        }

        void DrawOneShots(Kind kind, List<(Vector3 position, float size, float age)> plays, float glow)
        {
            kind.Count = 0;
            foreach (var (position, size, age) in plays)
                kind.Instances[kind.Count++] = Instance(position, OneShotFrame(age), size, glow);
            Submit(kind);
        }

        // 불바다 한 판: 해바라기 배치(황금각)로 범위 안에 고르게 나눈다. 조각마다 칸을 어긋나게 해 같은 모양이 겹쳐 보이지 않게
        void AddFirePieces(BattleSim.SpellEffect e, float glow)
        {
            float area = Mathf.PI * e.Radius * e.Radius;
            int pieces = Mathf.Clamp(Mathf.RoundToInt(area / FirePieceArea), FireMinPieces, FireMaxPieces);
            float pieceSize = e.Radius * 2f / Mathf.Sqrt(pieces) * 1.3f;
            float fade = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(e.TimeLeft));   // 끝날 때 1초 동안 작아진다 (BattleLights 빛과 같은 때)
            float seed = e.Position.x * 0.37f + e.Position.z * 0.61f;
            for (int k = 0; k < pieces && fire.Count < FireCapacity; k++)
            {
                float r = e.Radius * 0.72f * Mathf.Sqrt((k + 0.5f) / pieces);
                float a = k * 2.39996f + seed;
                var p = new Vector3(e.Position.x + Mathf.Cos(a) * r, e.Position.y, e.Position.z + Mathf.Sin(a) * r);
                fire.Instances[fire.Count++] = Instance(p, LoopFrame(seed + k * 1.7f), pieceSize * fade, glow);
            }
        }

        static int OneShotFrame(float age) => Mathf.Clamp((int)(age * Fps), 0, Frames - 1);
        int LoopFrame(float offset) => ((int)(clock * Fps + offset) % Frames + Frames) % Frames;

        static PerfLabInstance Instance(Vector3 feet, int frame, float size, float glow) => new PerfLabInstance
        {
            PosFrame = new float4(feet.x, feet.y, feet.z, frame),
            TintSize = new float4(glow, glow, glow, size),
        };

        void Submit(Kind kind)
        {
            if (kind.Count <= 0) return;
            kind.Buffer.SetData(kind.Instances, 0, 0, kind.Count);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, kind.Count, kind.Block, ShadowCastingMode.Off, false);
        }

        public void Dispose()
        {
            magic?.Buffer.Release();
            fire?.Buffer.Release();
            trap?.Buffer.Release();
            hit?.Buffer.Release();
            torch?.Buffer.Release();
            mount?.Buffer.Release();
            fieldArt?.Dispose();
            fieldArt = null;
            magicPlays.Clear();
            trapPlays.Clear();
            hitPlays.Clear();
        }
    }
}
