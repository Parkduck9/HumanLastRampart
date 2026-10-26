using System;
using HumanLastRampart.Combat.PerfLab;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽손 오우거 그리기 (W43, Codex K-129): 일반 적 그리기 경로(칸 256×384 한 시트)와 칸 크기·기준점·동작 수가 달라 따로 그린다.
    // - 시트 15장(Resources/BattleArt/Boss, 칸 768×864 가로 한 줄 + 짝 노멀). 필요한 순간 처음 불러온다
    // - 상태에서 동작을 고르고(걷기·오르기·떨어짐·함정·통로 위·죽음) 카메라가 보는 쪽에 맞는 면(정면·후면·좌·우)을 쓴다
    // - 개체 하나뿐이라 버퍼 1칸, 지금 동작의 시트로 한 번만 그린다. 크기·성벽 가까이 줄임은 일반 적과 같은 규칙
    public sealed class BattleBossArt : IDisposable
    {
        public const float CellWidthPx = 768f, CellHeightPx = 864f;
        public const float CellMeters = CellHeightPx / 432f * 4.5f;   // 서 있는 키 432점(원본 144점 × 3) = 4.5m → 칸 높이 9m
        const string Folder = "BattleArt/Boss/wallhand_";
        static readonly Vector2 GroundPivot = new Vector2(0.5f, 24f / CellHeightPx);          // 지면 (384, 840)
        static readonly Vector2 GripPivot = new Vector2(0.5f, 1f - 48f / CellHeightPx);       // 등반: 양손 위 매달림 (384, 48)
        static readonly Vector2 PelvisPivot = new Vector2(0.5f, 1f - 456f / CellHeightPx);    // 추락: 몸 중심 (384, 456)

        public enum Pose
        {
            WalkFront, WalkBack, WalkLeft, WalkRight, ClimbBack, ClimbFront, LoseGrip, Land, TrapFall, Lie, GetUp, SmashFront, SmashBack, DeathFront, DeathSide,
        }

        static readonly string[] Names =
        {
            "walk_front", "walk_back", "walk_left", "walk_right", "climb_back", "climb_front", "lose_grip_fall", "land_stagger",
            "trap_fall", "trap_lie", "get_up", "parapet_smash_front", "parapet_smash_back", "death_front", "death_side",
        };

        public static readonly int[] FrameCounts = { 8, 8, 8, 8, 8, 8, 6, 6, 4, 2, 6, 8, 8, 10, 10 };
        static readonly float[] Fps = { 8f, 8f, 8f, 8f, 6f, 6f, 8f, 8f, 8f, 2f, 8f, 8f, 8f, 9f, 9f };

        // 쓰러짐 단계 길이 (초): 떨어짐 6칸·착지 6칸·일어남 6칸(8칸/초) = 0.75, 함정 넘어짐 4칸 = 0.5
        const float FallSeconds = 6f / 8f, LandSeconds = 6f / 8f, GetUpSeconds = 6f / 8f, TrapFallSeconds = 4f / 8f;
        const float BossMinScale = 0.7f;   // 걷기·쓰러짐 때 성벽 가까이 줄임의 바닥
        const float WalkwayScale = 0.5f;   // 통로 위 크기 (기본 시점에서 일반 병사의 두 배 남짓)
        const float DeathHold = 1.5f;  // 죽은 뒤 마지막 자세를 잠깐 남겨 둔다

        readonly BattleSim sim;
        readonly Material material;
        readonly Mesh quad;
        readonly Bounds bounds;
        readonly GraphicsBuffer buffer;
        readonly PerfLabInstance[] instance = new PerfLabInstance[1];
        readonly MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[15];
        readonly bool[] missing = new bool[15];
        bool dying;
        float deathStart;
        Pose lastPose = Pose.WalkFront;
        int lastFrame;

        public Pose CurrentPose => lastPose;
        public int CurrentFrame => lastFrame;
        public bool Visible { get; private set; }

        // 시트가 설치되어 있는가 (걷기 정면 한 장으로 확인 — 없으면 일반 오우거 그림 그대로)
        public static bool Available => Resources.Load<Texture2D>(Folder + Names[(int)Pose.WalkFront]) != null;

        public BattleBossArt(BattleSim sim, Material material, Mesh quad, Bounds bounds)
        {
            this.sim = sim;
            this.material = material;
            this.quad = quad;
            this.bounds = bounds;
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, PerfLabInstance.Stride);
        }

        MaterialPropertyBlock BlockFor(Pose pose)
        {
            int index = (int)pose;
            if (blocks[index] != null) return blocks[index];
            if (missing[index]) return null;
            var texture = Resources.Load<Texture2D>(Folder + Names[index]);
            if (texture == null) { missing[index] = true; return null; }
            var normal = Resources.Load<Texture2D>(Folder + Names[index] + "_normal");
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", texture);
            block.SetVector("_MainTex_TexelSize", new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height));
            block.SetVector("_Grid", new Vector4(FrameCounts[index], 1f, 0f, 0f));
            Vector2 pivot = PivotOf(pose);
            block.SetVector("_Pivot", new Vector4(pivot.x, pivot.y, 0f, 0f));
            block.SetFloat("_Aspect", CellWidthPx / CellHeightPx);
            if (normal != null) block.SetTexture("_NormalTex", normal);
            block.SetFloat("_HasNormal", normal != null ? 1f : 0f);
            block.SetFloat("_HasSpec", 0f);
            block.SetFloat("_SheetScale", 1f);
            block.SetFloat("_ViewStretch", 1f);   // 납작함 보정 (다른 인물과 같게)
            block.SetFloat("_EyeGlow", 0f);
            block.SetVector("_FrameWindow", new Vector4(0f, 1000f, 0f, 0f));
            block.SetBuffer("_Instances", buffer);
            blocks[index] = block;
            return block;
        }

        static Vector2 PivotOf(Pose pose) => pose == Pose.ClimbBack || pose == Pose.ClimbFront ? GripPivot : pose == Pose.LoseGrip ? PelvisPivot : GroundPivot;

        // ── 동작 고르기 (시험·사진용으로 공개) ──

        // 우두머리가 가는 쪽(heading, 수평)이 카메라 앞뒤(forward)·오른쪽(right)에 대해 어느 면으로 보이는가: 0 정면(카메라 쪽으로 옴) 1 후면 2 왼쪽 3 오른쪽
        public static int ViewOf(Vector3 heading, Vector3 cameraForward, Vector3 cameraRight)
        {
            heading.y = 0f; cameraForward.y = 0f; cameraRight.y = 0f;
            heading.Normalize(); cameraForward.Normalize(); cameraRight.Normalize();
            float along = Vector3.Dot(heading, cameraForward), across = Vector3.Dot(heading, cameraRight);
            if (along < -0.5f) return 0;
            if (along > 0.5f) return 1;
            return across > 0f ? 3 : 2;
        }

        // 쓰러진 뒤 elapsed초, 전체 total초일 때의 동작·칸. byFall = 성벽에서 떨어짐(떨어짐 → 착지 → 누움 → 일어남), 아니면 함정(넘어짐 → 누움 → 일어남)
        public static void DownPose(float elapsed, float total, bool byFall, out Pose pose, out int frame)
        {
            elapsed = Mathf.Max(0f, elapsed);
            if (total - elapsed <= GetUpSeconds)
            {
                pose = Pose.GetUp;
                frame = Mathf.Clamp((int)((elapsed - (total - GetUpSeconds)) * Fps[(int)Pose.GetUp]), 0, FrameCounts[(int)Pose.GetUp] - 1);
                return;
            }

            float lieFrom = byFall ? FallSeconds + LandSeconds : TrapFallSeconds;
            if (elapsed < lieFrom)
            {
                if (byFall && elapsed < FallSeconds) { pose = Pose.LoseGrip; frame = Mathf.Clamp((int)(elapsed * Fps[(int)Pose.LoseGrip]), 0, FrameCounts[(int)Pose.LoseGrip] - 1); }
                else if (byFall) { pose = Pose.Land; frame = Mathf.Clamp((int)((elapsed - FallSeconds) * Fps[(int)Pose.Land]), 0, FrameCounts[(int)Pose.Land] - 1); }
                else { pose = Pose.TrapFall; frame = Mathf.Clamp((int)(elapsed * Fps[(int)Pose.TrapFall]), 0, FrameCounts[(int)Pose.TrapFall] - 1); }
                return;
            }

            pose = Pose.Lie;
            frame = (int)((elapsed - lieFrom) * Fps[(int)Pose.Lie]) % FrameCounts[(int)Pose.Lie];
        }

        // 작업이 끝난 뒤(Complete 후) 그리기 직전. camera = 보는 사진기
        public void Draw(Camera camera)
        {
            Visible = false;
            if (camera == null || !sim.BossSpawned) return;
            byte state = sim.BossState;
            bool dead = state == EnemyState.Dead;
            if (!dead && !EnemyState.IsAlive(state)) return;
            if (dead)
            {
                if (!dying) { dying = true; deathStart = Time.time; }
            }
            else dying = false;

            int side = sim.BossSide;
            float3 outward = WallGeometry.Outward(side);
            var heading = new Vector3(-outward.x, 0f, -outward.z);   // 성벽 쪽(걷고 오르는 방향) = 통로 위에서는 안쪽을 보는 방향
            Vector3 cameraForward = camera.transform.forward, cameraRight = camera.transform.right;
            int view = ViewOf(heading, cameraForward, cameraRight);   // 0 정면 1 후면 2 왼쪽 3 오른쪽
            float now = Time.time;
            Vector3 feet = sim.BossPosition;
            Pose pose;
            int frame;
            if (dead)
            {
                pose = view == 0 || view == 1 ? Pose.DeathFront : Pose.DeathSide;
                float t = now - deathStart;
                if (t > FrameCounts[(int)pose] / Fps[(int)pose] + DeathHold) return;   // 마지막 자세를 잠깐 둔 뒤 사라진다
                frame = Mathf.Min((int)(t * Fps[(int)pose]), FrameCounts[(int)pose] - 1);
            }
            else if (sim.BossDownLeft > 0f)
            {
                DownPose(sim.BossDownTotal - sim.BossDownLeft, sim.BossDownTotal, sim.BossDownByFall, out pose, out frame);
                if (pose == Pose.LoseGrip)
                {
                    // 몸 중심 기준 — 매달렸던 높이에서 땅 가까이로 내려온다
                    float fall = Mathf.Clamp01((sim.BossDownTotal - sim.BossDownLeft) / FallSeconds);
                    feet.y = Mathf.Lerp(2.4f, 0.9f, fall * fall);
                }
            }
            else if (state == EnemyState.OnWall)
            {
                pose = view == 1 ? Pose.SmashBack : Pose.SmashFront;
                frame = (int)(now * Fps[(int)pose]) % FrameCounts[(int)pose];
            }
            else if (state == EnemyState.Climbing)
            {
                pose = view == 0 ? Pose.ClimbFront : Pose.ClimbBack;
                frame = (int)(now * Fps[(int)pose]) % FrameCounts[(int)pose];
                // 손 높이: 오를수록 올라가 끝에는 손이 성벽 위(난간)에 닿는다
                feet.y = Mathf.Lerp(2.4f, BattleSim.WallHeight + 0.3f, Mathf.Clamp01(feet.y / BattleSim.WallHeight));
            }
            else
            {
                pose = view == 0 ? Pose.WalkFront : view == 1 ? Pose.WalkBack : view == 2 ? Pose.WalkLeft : Pose.WalkRight;
                frame = (int)(now * Fps[(int)pose]) % FrameCounts[(int)pose];
            }

            MaterialPropertyBlock block = BlockFor(pose);
            if (block == null) return;

            // 일반 적과 같은 크기 규칙: 성벽에서 멀면 원래 크기, 성벽 가까이(카메라에서 가려지는 구역)에서 EnemyWallScale로 줄인다
            float fromWall = math.dot(new float3(feet.x, 0f, feet.z), outward) - sim.CastleHalf;
            float scale = Mathf.Lerp(BattleSim.EnemyWallScale, 1f, Mathf.Clamp01((fromWall - 1f) / Mathf.Max(0.1f, BattleSim.WallScaleBand - 1f)));
            // 우두머리는 4.5m 그대로 보여야 한다: 성벽 위·오르는 중에는 줄이지 않고, 걷거나 쓰러져 있을 때도 일반 적만큼 작아지지 않게 바닥을 둔다
            if (state == EnemyState.Climbing) scale = 1f;
            else if (state == EnemyState.OnWall) scale = WalkwayScale;   // 통로 위는 사진기가 4m 안팎이라 1.0이면 화면을 다 덮는다 (사진으로 맞춤)
            else scale = Mathf.Max(scale, BossMinScale);
            float shade = sim.Brightness;
            // 빛줄기 안이면 화가 난 듯 붉게 (빛에 드러나 빨라지는 상태가 눈에 보이게)
            float3 tint = sim.BossLit ? new float3(1f, 0.78f, 0.72f) * shade : new float3(shade, shade, shade);
            instance[0] = new PerfLabInstance { PosFrame = new float4(feet.x, feet.y, feet.z, frame), TintSize = new float4(tint, CellMeters * scale) };
            buffer.SetData(instance);
            lastPose = pose;
            lastFrame = frame;
            Visible = true;
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, 1, block);
        }

        public void Dispose() => buffer?.Release();
    }
}
