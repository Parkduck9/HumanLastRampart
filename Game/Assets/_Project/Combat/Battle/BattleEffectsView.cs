using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Audio;
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
    // K-041: 불·폭발·불티·횃불은 스스로 빛나는 그림(_Emissive, 1 넘는 밝은 색 → 빛번짐). 마법 폭발은 큰 폭발 + 곁불덩이 + 충격파 고리 + 잔불 + 연기 + 화면 흔들림.
    //        연기는 폭발·함정·대포알 떨어진 자리·불바다·대포 쏠 때 위로 피어오른다 (포구 연기 그림 재활용 — 전용 연기 그림 필요)
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

        const int BurstCapacity = 48;                // 함정 폭발 + 대포알 떨어진 자리 + 마법 폭발 곁불덩이 (K-041: 32에서 늘림)
        const float CannonBurstPerRadius = 1.2f;     // 대포알 폭발 판 크기 = 범위 반경 × 1.2 (함정·마법 2배보다 작게)
        const float CannonBurstMinSize = 1.5f;
        const int FireCapacity = 160;                // 불바다 조각 + 마법 폭발 잔불 (K-041: 96에서 늘림)
        const int HitCapacity = 160;                 // 맞음 불꽃 + 마법 폭발 충격파 고리 불티 (K-041: 48에서 늘림)

        // 마법 폭발 (K-041 사용자 피드백 '폭탄도 일반 대포랑 똑같은 이펙트'): 대포 착탄과 확실히 다르게
        const float MagicBurstPerRadius = 2.6f;      // 가운데 큰 폭발 (예전 2배)
        const float MagicBurstSink = 0.3f;           // 판 높이의 이만큼 땅 밑으로 (2026-10-20)
        const int MagicFireballs = 4;                // 둘레에 조금 늦게 터지는 불덩이
        const float FireballDelayMax = 0.3f;
        const int RingSparks = 28;                   // 충격파 고리: 불티를 원으로 늘어놓고 반경을 키운다
        const float RingSeconds = 0.55f;
        const float RingReach = 1.7f;                // 반경 × 이만큼까지 퍼진다
        const int MagicEmbers = 8;                   // 잔불: 범위 안에 작은 불이 잠깐 남는다
        const float EmberSeconds = 1.8f;
        const float MagicShake = 0.35f, TrapShake = 0.15f;   // 화면 흔들림 세기 (BattleCameraRig.Shake)

        // 연기 (K-041): 도트 연기 그림이 따로 없어 대포 포구 연기(Cannon/muzzle_smoke 6칸)를 어둡게 크게 재활용 — 위로 피어오른다
        const int SmokeCapacity = 160;
        const float FireSmokeEvery = 0.35f;          // 불바다 하나에서 연기 한 덩이씩
        // 스스로 빛나는 그림 밝기 (1 넘으면 빛번짐) — 해·달 빛을 받지 않는다(_Emissive)
        const float FireGlow = 2.2f, BurstGlow = 2.6f, HitGlow = 1.6f, TorchGlow = 2.0f;
        const float BurstSizePerRadius = 2f;         // 판 높이 = 반경 × 2 (그림 폭이 칸의 약 90%)
        const float FirePieceArea = 30f;             // 조각 하나가 맡는 넓이(m²) — 반경 5m면 약 3조각
        const int FireMinPieces = 3, FireMaxPieces = 12;
        const float HitSize = 1.2f;                  // 불꽃 그림은 칸의 약 1/3
        const float CannonShotSize = 1f;             // 투사체 그림 크기가 이보다 크면 대포알 (WriteShotInstancesJob 1.6)

        // 횃불 자리 — BattleLights와 같은 규칙 (그쪽 값이 바뀌면 함께 맞춘다)
        const float TorchInset = BattleLights.TorchInset;      // 빛과 같은 자리 (D-071 피드백: 그림과 빛이 따로 놀던 것). 성벽 따라 자리는 sim.TorchSpots
        const float TorchFootHeight = BattleLights.TorchHeight - 0.1f;   // 빛보다 조금 아래에서 불꽃이 솟는다
        const float TorchSize = 0.35f;   // 병사 키(0.35m)와 같은 칸 크기 — 0.9는 병사보다 훨씬 컸다

        // 횃불대 (K-031, Codex K-018 시안 torch_mount_off): 칸 128×256, 부착 기준점 (64,224), 보이는 높이 약 0.3m(192화소).
        // 낮·밤 모두 같은 받침을 통로 위에 세우고, 밤 불꽃은 시안 겹침(manifest flameBaseTopLeft (52,36), 불꽃 칸 36화소)대로 받침 머리에 얹는다
        const float MountSize = 0.3f * 256f / 192f;                    // 칸 높이(m) 0.4 — 보이는 그림 0.3m
        const float MountFlameFoot = (224f - 72f) / 256f * MountSize;   // 받침 발에서 불꽃 칸 바닥까지 0.2375m (= 빛 높이 0.24m와 거의 같다)
        const float MountFlameSize = 36f / 256f * MountSize;            // 불꽃 칸 0.056m (시안 비율 — 화면에서 너무 작으면 이 값을 키운다)


        readonly BattleSim sim;
        readonly Material material;
        readonly Mesh quad;
        readonly Bounds bounds;

        readonly Kind magic, fire, trap, hit, torch, mount, smoke;
        readonly List<(Vector3 center, float radius, float age)> ringPlays = new List<(Vector3, float, float)>();
        readonly List<(Vector3 position, float size, float age)> emberPlays = new List<(Vector3, float, float)>();
        // 연기: 자리, 크기, 지난 시간(음수 = 늦게 시작), 길이, 오르는 높이, 밝기
        readonly List<(Vector3 position, float size, float age, float life, float rise, float shade)> smokePlays = new List<(Vector3, float, float, float, float, float)>();
        readonly List<float> fireSmokeClock = new List<float>();   // 불바다마다 다음 연기까지
        float[] cannonAges;                                         // 대포마다 지난 프레임 '쏜 뒤 지난 시간' (줄면 새로 쏨)
        readonly System.Random random = new System.Random(41);
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
        readonly List<Vector3> roadFeet = new List<Vector3>();    // 돌길 등불 기둥 화로 위 (W26): 같은 횃불 불꽃 그림을 밤에만, 더 크게
        const float RoadFlameSize = 0.24f;                        // 화로 위 불꽃 칸 (m) — 기둥 0.7m·병사 0.35m에 맞춰 눈에 띄게
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
            torch = MakeKind("torch_flame", Mathf.Max(1, torchFeet.Count + roadFeet.Count));
            if (hasMount) mount = MakeKind("Props/torch_mount_off", Mathf.Max(1, mountFeet.Count), PerfLabArt.SheetLayout.Make(1, 1, 0.5f, (256f - 224f) / 256f, 128f / 256f), "BattleArt/", false);
            smoke = MakeKind("muzzle_smoke", SmokeCapacity, PerfLabArt.SheetLayout.Make(Frames, 1, 0.5f, (256f - 240f) / 256f, 1f), "BattleArt/Cannon/", false);
            cannonAges = new float[sim.CannonCount];
            for (int i = 0; i < cannonAges.Length; i++) cannonAges[i] = 1000f;
        }

        // 들판 그림 묶음을 붙인다 (BattleView 생성자에서 한 줄). 그리기·정리는 이 효과 그림과 같이 한다
        public void Attach(BattleFieldArt art) => fieldArt = art;

        // 시체 더미 그림(K-024)이 있으면 BattleView의 상자 더미 대신 그림으로 그린다
        public bool DrawsPiles => fieldArt != null && fieldArt.HasPileArt;

        static readonly int EmissiveId = Shader.PropertyToID("_Emissive");

        // 효과 그림(불·폭발·불티·횃불)은 스스로 빛난다 (K-041)
        Kind MakeKind(string name, int capacity) => MakeKind(name, capacity, PerfLabArt.SheetLayout.Make(Frames, 1, 0.5f, 0f, 1f), "BattleArt/Effects/", true);

        Kind MakeKind(string name, int capacity, PerfLabArt.SheetLayout layout, string folder, bool emissive)
        {
            var texture = Resources.Load<Texture2D>(folder + name);
            if (texture == null) return null;
            var kind = new Kind { Block = new MaterialPropertyBlock(), Instances = new PerfLabInstance[capacity] };
            kind.Block.SetTexture(MainTexId, texture);
            kind.Block.SetVector(GridId, layout.Grid);
            kind.Block.SetVector(PivotId, layout.Pivot);
            kind.Block.SetFloat(AspectId, layout.Aspect);
            kind.Block.SetFloat(HasNormalId, 0f);
            kind.Block.SetFloat(EmissiveId, emissive ? 1f : 0f);
            kind.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, PerfLabInstance.Stride);
            kind.Block.SetBuffer(InstancesId, kind.Buffer);
            return kind;
        }

        // BattleLights.BuildTorches와 같은 자리: 네 성벽 난간 바로 뒤, 간격마다 가장 가까운 돌출부 뒤 (sim.TorchSpots, K-045)
        // 받침 그림이 있으면 받침은 통로 위에 서고, 불꽃은 받침 머리(시안 접점)에서 시안 비율 크기로 솟는다
        void BuildTorchFeet(bool hasMount)
        {
            float flameFoot = hasMount ? MountFlameFoot : TorchFootHeight;
            torchSize = hasMount ? MountFlameSize : TorchSize;
            for (int i = 0; i < BattleRoad.LampCount(sim.CastleHalf); i++)
            {
                Vector3 p = BattleRoad.LampBase(sim.CastleHalf, i);
                roadFeet.Add(new Vector3(p.x, p.y + BattleRoad.LampBowlTop, p.z));
            }

            float inner = sim.CastleHalf - TorchInset;
            for (int side = 0; side < 4; side++)
            {
                float3 n = WallGeometry.Outward(side);
                float3 r = WallGeometry.Right(side);
                foreach (float along in sim.TorchSpots(side))   // 빛과 같은 목록 — 돌출부 뒤 (K-045)
                {
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
            if (mapMode) { HumanLastRampart.Combat.Render.GroundGlow.Flush(true, false); return; }

            bool night = sim.TimeOfDay == TimeOfDay.Night;

            if (magic != null) DrawOneShots(magic, magicPlays, BurstGlow);
            if (trap != null) DrawOneShots(trap, trapPlays, BurstGlow);

            if (hit != null)
            {
                hit.Count = 0;
                foreach (var (position, age) in hitPlays)
                    hit.Instances[hit.Count++] = Instance(position, OneShotFrame(age), HitSize, HitGlow);
                AddRingSparks();
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
                    AddFirePieces(e, FireGlow);
                }

                AddEmbers();
                Submit(fire);
            }

            if (smoke != null)
            {
                smoke.Count = 0;
                AddSmoke();
                Submit(smoke);
            }

            if (torch != null && night)
            {
                torch.Count = 0;
                for (int i = 0; i < torchFeet.Count; i++)
                    torch.Instances[torch.Count++] = Instance(torchFeet[i], LoopFrame(i * 2.3f), torchSize, TorchGlow);
                for (int i = 0; i < roadFeet.Count; i++)
                    torch.Instances[torch.Count++] = Instance(roadFeet[i], LoopFrame(i * 1.7f + 0.9f), RoadFlameSize, TorchGlow);
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

            HumanLastRampart.Combat.Render.GroundGlow.Flush(false, night);   // 불 밑 땅 웅덩이 (W15) — 이번 프레임 Add한 것을 한 번에
        }

        // 터지는 순간 찾기·재생 시간 진행 (지도 모드에서도)
        void Track(float dt)
        {
            Age(magicPlays, dt);
            Age(trapPlays, dt);
            Age(ringPlays, dt, RingSeconds);
            Age(emberPlays, dt, EmberSeconds);
            AgeSmoke(smokePlays, dt);
            TrackFireSmoke(dt);
            TrackCannonSmoke();
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
                    bool known = false;
                    for (int k = 0; k < pendingBefore.Count && !known; k++)
                        if ((pendingBefore[k] - e.Position).sqrMagnitude < 0.0001f) known = true;
                    if (!known) Sound.PlayAt(AudioCues.MagicCast, e.Position);   // 마법 시전 순간 (W24)
                    pendingNow.Add(e.Position);
                    continue;
                }

                if (e.Fire || magic == null) continue;
                // 반복문으로 찾는다 (K-061: 람다가 e를 잡아 효과마다 매 프레임 할당되던 것)
                int was = -1;
                for (int k = 0; k < pendingBefore.Count && was < 0; k++)
                    if ((pendingBefore[k] - e.Position).sqrMagnitude < 0.0001f) was = k;
                if (was < 0) continue;
                pendingBefore.RemoveAt(was);
                StartMagicBurst(e.Position, e.Radius);
                Sound.PlayAt(AudioCues.MagicExplosion, e.Position);   // 소리 (W24, 표현 전용)
            }

            pendingBefore.Clear();
            pendingBefore.AddRange(pendingNow);

            // 함정 폭발: Exploded로 바뀌는 순간
            int traps = Mathf.Min(trapStates.Length, sim.TrapCount);
            for (int i = 0; i < traps; i++)
            {
                BattleSim.TrapInfo info = sim.GetTrap(i);
                if (info.State == BattleSim.TrapState.Exploded && trapStates[i] != BattleSim.TrapState.Exploded)
                {
                    // 땅에 묻힌 함정이 솟구치게: 판을 땅 밑으로 내린다 (사용자 2026-10-20 '묻힌 것 같지 않고 위로 뜬다')
                    float trapSize = info.DamageRadius * BurstSizePerRadius;
                    if (trap != null && trapPlays.Count < BurstCapacity) trapPlays.Add((info.Position + Vector3.down * trapSize * MagicBurstSink, trapSize, 0f));
                    AddSmokePlay(info.Position, info.DamageRadius * 1.2f, -0.3f, 2f, info.DamageRadius * 0.6f, 0.4f);   // 흙먼지 섞인 검은 연기 (K-041)
                    BattleCameraRig.Shake(TrapShake, info.Position);
                    Sound.PlayAt(AudioCues.TrapBurst, info.Position);
                }

                trapStates[i] = info.State;
            }

            // 맞음 불꽃: 화살·총알 그림 크기가 0으로 바뀐 칸 = 이번 프레임 착지 (빗나간 화살도 땅에 닿은 자리에 난다)
            if (shotSizes == null) return;
            int firedBow = 0, firedCrossbow = 0, firedGun = 0, firedMagic = 0;   // 이번 프레임 새로 나간 투사체 수 종류별 (대포알 제외)
            int landedHits = 0;                   // 이번 프레임 맞음 수 — 한 소리로 묶어 알린다 (W24)
            Vector3 landedSum = Vector3.zero;
            NativeArray<PerfLabInstance> shots = sim.ShotInstances;
            int slots = Mathf.Min(shotSizes.Length, shots.Length);
            for (int i = 0; i < slots; i++)
            {
                PerfLabInstance shot = shots[i];
                float size = shot.TintSize.w;
                float before = shotSizes[i];
                shotSizes[i] = size;
                if (size > 0f) shotWeapons[i] = (int)shot.PosFrame.w;   // 날아가는 동안 칸 번호 = 무기 번호 (K-032)
                if (before <= 0f && size > 0f && size <= CannonShotSize)
                {
                    switch (sim.WeaponKindOf(shotWeapons[i]))
                    {
                        case HumanLastRampart.Core.Definitions.WeaponKind.Crossbow: firedCrossbow++; break;
                        case HumanLastRampart.Core.Definitions.WeaponKind.Gun: firedGun++; break;
                        case HumanLastRampart.Core.Definitions.WeaponKind.Magic: firedMagic++; break;
                        default: firedBow++; break;
                    }
                }
                if (before <= 0f || size > 0f) continue;

                // 대포알이 떨어진 자리: 함정 폭발 그림을 범위보다 작게 한 번 (K-036). 땅 높이에서
                if (before > CannonShotSize)
                {
                    Sound.PlayAt(AudioCues.CannonImpact, new Vector3(shot.PosFrame.x, 0f, shot.PosFrame.z));
                    if (trap == null || trapPlays.Count >= BurstCapacity) continue;
                    float radius = sim.WeaponAreaRadius(shotWeapons[i]);
                    var landed = new Vector3(shot.PosFrame.x, 0f, shot.PosFrame.z);
                    trapPlays.Add((landed, Mathf.Max(CannonBurstMinSize, radius * CannonBurstPerRadius), 0f));
                    AddSmokePlay(landed, Mathf.Max(1.2f, radius * 0.8f), -0.2f, 1.4f, 1.5f, 0.55f);   // 작은 흙연기 (K-041)
                    BattleLights.Flash(landed, radius, BattleLights.ImpactColor, BattleLights.CannonImpactIntensity, 0.35f);   // 터지는 빛 (2026-10-20)
                    continue;
                }

                landedHits++;
                landedSum += new Vector3(shot.PosFrame.x, shot.PosFrame.y, shot.PosFrame.z);
                if (hit == null || hitPlays.Count >= HitCapacity) continue;
                hitPlays.Add((new Vector3(shot.PosFrame.x, shot.PosFrame.y, shot.PosFrame.z), 0f));
            }

            if (landedHits > 0)
            {
                Sound.Report(AudioCues.ArrowHit, landedHits, landedSum / landedHits);
                Sound.Report(AudioCues.EnemyHit, landedHits, landedSum / landedHits);   // 몸에 맞는 둔탁한 소리를 겹친다 (묶음)
            }
            Vector3 ear = BattleLights.ViewPosition;
            if (firedBow > 0) Sound.Report(AudioCues.BowVolley, firedBow, ear);
            if (firedCrossbow > 0) Sound.Report(AudioCues.CrossbowVolley, firedCrossbow, ear);
            if (firedGun > 0) Sound.Report(AudioCues.MusketVolley, firedGun, ear);
            if (firedMagic > 0) Sound.Report(AudioCues.MagicCast, firedMagic, ear);
        }

        static void Age(List<(Vector3 position, float size, float age)> plays, float dt, float life = OneShotSeconds)
        {
            for (int i = plays.Count - 1; i >= 0; i--)
            {
                var play = plays[i];
                play.age += dt;
                if (play.age >= life) plays.RemoveAt(i);
                else plays[i] = play;
            }
        }

        // 불바다: 범위 안 아무 데서나 연기가 피어오른다 (불바다 순서가 바뀌어도 간격만 조금 어긋날 뿐)
        void TrackFireSmoke(float dt)
        {
            IReadOnlyList<BattleSim.SpellEffect> effects = sim.Effects;
            while (fireSmokeClock.Count < effects.Count)
            {
                if (effects[fireSmokeClock.Count].Fire) Sound.PlayAt(AudioCues.FirePool, effects[fireSmokeClock.Count].Position);   // 새 불바다 (W24)
                fireSmokeClock.Add(Range(0f, FireSmokeEvery));
            }
            for (int i = 0; i < effects.Count; i++)
            {
                BattleSim.SpellEffect e = effects[i];
                if (!e.Fire || e.Pending) continue;
                fireSmokeClock[i] -= dt;
                if (fireSmokeClock[i] > 0f) continue;
                fireSmokeClock[i] += FireSmokeEvery;
                AddSmokePlay(e.Position + RandomFlat(e.Radius * 0.7f), Mathf.Max(1.5f, e.Radius * 0.6f), 0f, 2.4f, 4f, 0.35f);
            }
        }

        // 대포를 쏘면 포구 연기(BattlePropView) 뒤로 옅은 연기가 위로 피어오른다
        void TrackCannonSmoke()
        {
            int count = Mathf.Min(cannonAges.Length, sim.CannonCount);
            for (int i = 0; i < count; i++)
            {
                float age = sim.CannonFireAge(i);
                bool fired = age < cannonAges[i] - 0.01f;
                cannonAges[i] = age;
                if (!fired) continue;
                BattleSim.CannonInfo c = sim.GetCannon(i);
                Sound.PlayAt(AudioCues.CannonFire, c.Position);   // 소리 (W24)
                if (smoke == null) continue;
                float3 n = WallGeometry.Outward(c.Side);
                AddSmokePlay(c.Position + new Vector3(n.x, 0f, n.z) * 0.6f + Vector3.up * 0.15f, 0.9f, 0.25f, 1.8f, 1.2f, 0.8f);
            }
        }

        // 지난 시간이 음수인 것(늦게 터질 불덩이)은 아직 그리지 않는다
        void DrawOneShots(Kind kind, List<(Vector3 position, float size, float age)> plays, float glow)
        {
            kind.Count = 0;
            foreach (var (position, size, age) in plays)
                if (age >= 0f && kind.Count < kind.Instances.Length) kind.Instances[kind.Count++] = Instance(position, OneShotFrame(age), size, glow);
            Submit(kind);
        }

        // ── 마법 폭발 꾸밈 (K-041) ──

        // 터지는 순간 한 번: 큰 폭발 + 늦게 터지는 불덩이 + 충격파 고리 + 잔불 + 연기 + 화면 흔들림
        void StartMagicBurst(Vector3 at, float radius)
        {
            // 판을 땅 밑으로 조금 내린다 — 폭발 그림 가운데가 판 높이의 절반쯤이라 너무 위에서 터져 보였다 (사용자 2026-10-20).
            // 내려간 아랫부분은 땅에 가려져 땅에서 솟구치는 것처럼 보인다
            float main = radius * MagicBurstPerRadius;
            if (magicPlays.Count < BurstCapacity) magicPlays.Add((at + Vector3.down * main * MagicBurstSink, main, 0f));
            for (int k = 0; k < MagicFireballs && magicPlays.Count < BurstCapacity; k++)
            {
                float size = radius * Range(0.8f, 1.2f);
                Vector3 p = at + RandomFlat(radius * 0.75f) + Vector3.down * size * MagicBurstSink;
                magicPlays.Add((p, size, -Range(0.08f, FireballDelayMax)));
            }

            ringPlays.Add((at, radius, 0f));
            for (int k = 0; k < MagicEmbers; k++) emberPlays.Add((at + RandomFlat(radius * 0.85f), Range(0.8f, 1.4f), -Range(0.1f, 0.4f)));
            for (int k = 0; k < 3; k++) AddSmokePlay(at + RandomFlat(radius * 0.5f), radius * Range(0.9f, 1.3f), -Range(0.25f, 0.6f), 2.2f, radius * 0.8f, 0.45f);
            BattleCameraRig.Shake(MagicShake, at);
        }

        // 충격파 고리: 불티를 원으로 놓고 반경을 키운다. 처음엔 크고 퍼지며 작아진다
        void AddRingSparks()
        {
            foreach (var (center, radius, age) in ringPlays)
            {
                float t = Mathf.Clamp01(age / RingSeconds);
                float reach = radius * Mathf.Lerp(0.3f, RingReach, 1f - (1f - t) * (1f - t));   // 처음에 빠르게
                float size = Mathf.Lerp(2.2f, 1.0f, t);
                int frame = Mathf.Clamp((int)(t * Frames), 0, Frames - 1);
                for (int k = 0; k < RingSparks && hit.Count < hit.Instances.Length; k++)
                {
                    float a = k * Mathf.PI * 2f / RingSparks;
                    var p = new Vector3(center.x + Mathf.Cos(a) * reach, center.y, center.z + Mathf.Sin(a) * reach);
                    hit.Instances[hit.Count++] = Instance(p, frame, size, HitGlow * 1.3f);
                }
            }
        }

        // 잔불: 불바다 그림을 작게, 시간이 지나며 더 작아진다
        void AddEmbers()
        {
            foreach (var (position, size, age) in emberPlays)
            {
                if (age < 0f || fire.Count >= fire.Instances.Length) continue;
                float fade = 1f - Mathf.Clamp01(age / EmberSeconds);
                fire.Instances[fire.Count++] = Instance(position, LoopFrame(position.x * 1.3f), size * Mathf.Lerp(0.4f, 1f, fade), FireGlow);
                HumanLastRampart.Combat.Render.GroundGlow.Add(position, size * Mathf.Lerp(0.4f, 1f, fade) * 1.4f + 0.3f, HumanLastRampart.Combat.Render.GroundGlow.Fire, 0.3f * fade);   // 잔불 작은 웅덩이
            }
        }

        // ── 연기 (K-041) ──

        // life초 동안 rise만큼 오르고 커진다. shade = 밝기(검을수록 진한 연기)
        void AddSmokePlay(Vector3 at, float size, float age, float life, float rise, float shade)
        {
            if (smoke == null || smokePlays.Count >= SmokeCapacity) return;
            smokePlays.Add((at, size, age, life, rise, shade));
        }

        void AddSmoke()
        {
            float light = sim.Brightness;
            foreach (var (position, size, age, life, rise, shade) in smokePlays)
            {
                if (age < 0f || smoke.Count >= smoke.Instances.Length) continue;
                float t = Mathf.Clamp01(age / life);
                Vector3 p = position + Vector3.up * (rise * t);
                // 연기 시트 6칸: 점화 → 덩이 → 흩어짐. 처음 칸(불티)은 건너뛰고 1~5칸만
                int frame = 1 + Mathf.Min(Frames - 2, (int)(t * (Frames - 1)));
                float s = shade * light;
                smoke.Instances[smoke.Count++] = new PerfLabInstance
                {
                    PosFrame = new float4(p.x, p.y, p.z, frame),
                    TintSize = new float4(s, s, s * 1.02f, size * Mathf.Lerp(0.7f, 1.4f, t)),
                };
            }
        }

        static void AgeSmoke(List<(Vector3 position, float size, float age, float life, float rise, float shade)> plays, float dt)
        {
            for (int i = plays.Count - 1; i >= 0; i--)
            {
                var play = plays[i];
                play.age += dt;
                if (play.age >= play.life) plays.RemoveAt(i);
                else plays[i] = play;
            }
        }

        float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        Vector3 RandomFlat(float radius)
        {
            float a = Range(0f, Mathf.PI * 2f);
            float r = radius * Mathf.Sqrt((float)random.NextDouble());
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
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
                HumanLastRampart.Combat.Render.GroundGlow.Add(p, pieceSize * fade * 1.5f, HumanLastRampart.Combat.Render.GroundGlow.Fire, 0.14f * fade);   // 불 조각마다 땅 웅덩이 (불 크기의 2~3배 지름)
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
            HumanLastRampart.Combat.Render.GroundGlow.Release();
            magic?.Buffer.Release();
            fire?.Buffer.Release();
            trap?.Buffer.Release();
            hit?.Buffer.Release();
            torch?.Buffer.Release();
            mount?.Buffer.Release();
            smoke?.Buffer.Release();
            ringPlays.Clear();
            emberPlays.Clear();
            smokePlays.Clear();
            fieldArt?.Dispose();
            fieldArt = null;
            magicPlays.Clear();
            trapPlays.Clear();
            hitPlays.Clear();
        }
    }
}
