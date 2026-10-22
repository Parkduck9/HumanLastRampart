using HumanLastRampart.Combat.Render;
using HumanLastRampart.Core.CombatContracts;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 등대 빛줄기 그림 (Concept_Beacon_v1 9.4): 탑 꼭대기(사진기 뒤·위)에서 뻗는 부채꼴 빛.
    // W15 (사용자 2026-10-22): 땅 위 빛은 성벽 바로 너머의 짧은 어두운 띠 뒤에서 시작해 바깥으로 넓어지고 가장자리는 부드럽다 (BeamCone 셰이더, 덧셈).
    // 같은 모양을 HlrBeam 전역으로 그림 셰이더에도 넘겨, 빛 안의 적·소품이 따뜻하게 밝아진다. 폭·느려짐·피해 숫자는 BattleSim.Lamp 그대로 (보이는 빛만)
    // W9: 마당 위(성벽 윗면 높이)와 들판 위(땅 높이) 두 장 + 도는 중에는 가려는 방향의 연한 부채꼴 (지도에서도 보인다)
    // 최종 그림은 Codex 카드(K-106)로 바뀔 수 있다 — 렌즈 빛(beam_head)은 BattleBeaconView가 쓴다
    public sealed class BattleBeamView
    {
        const int Segments = 24;
        const float FieldHeight = 0.07f;
        const float YardLift = 0.03f;       // 마당 윗면(성벽 상자 윗면 -0.1) 바로 위
        const float FieldDrawReach = 110f;  // 눈에 보이는 빛은 성벽에서 이만큼까지 (그 너머는 거의 꺼진다)
        const float NightStrength = 0.36f;   // 등불 밝기 보통 = 1. 약·강은 BattleSim.GlowLevel
        const float DayStrength = 0.06f;    // 낮에는 아주 연하게 (햇빛에 묻힌다)
        const float MapStrength = 0.12f;     // 지도(밝은 화면)에서는 연하게

        static readonly int ModeId = Shader.PropertyToID("_Mode");
        static readonly int DirId = Shader.PropertyToID("_Dir");
        static readonly int HalfId = Shader.PropertyToID("_Half");
        static readonly int ProfileId = Shader.PropertyToID("_Profile");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        sealed class Wedge
        {
            public Mesh Mesh;
            public readonly Vector3[] Vertices = new Vector3[Segments + 2];
            public readonly int[] Triangles = new int[Segments * 3];
            public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        }

        readonly BattleSim sim;
        readonly Material material;
        readonly Wedge fieldNow = new Wedge(), yardNow = new Wedge(), fieldTarget = new Wedge(), yardTarget = new Wedge();

        public BattleBeamView(BattleSim sim)
        {
            this.sim = sim;
            foreach (Wedge w in new[] { fieldNow, yardNow, fieldTarget, yardTarget })
            {
                w.Mesh = new Mesh { name = "등대 빛줄기 (runtime)" };
                w.Mesh.MarkDynamic();
                for (int i = 0; i < Segments; i++)
                {
                    w.Triangles[i * 3] = 0;
                    w.Triangles[i * 3 + 1] = i + 1;
                    w.Triangles[i * 3 + 2] = i + 2;
                }
            }

            Shader shader = Resources.Load<Shader>("BeamCone");
            if (shader != null) material = new Material(shader) { name = "등대 빛줄기 (runtime)" };
        }

        public void Draw(bool mapMode = false)
        {
            if (sim == null || material == null) return;
            bool night = sim.TimeOfDay == TimeOfDay.Night;
            float level = BattleSim.GlowLevel(sim.Lamp);   // 등불 밝기 약·보통·강
            float strength = level * (mapMode ? MapStrength : night ? NightStrength : DayStrength);
            float castle = sim.CastleHalf;
            float fieldReach = Mathf.Min(sim.FieldSize, castle + FieldDrawReach);
            float yardReach = castle - 2f;
            float yardHeight = BattleSim.WallHeight - 0.1f + YardLift;

            // 등불이 꺼졌으면(세기 0) 부채꼴을 그리지 않는다
            if (strength <= 0.001f)
            {
                HlrBeam.Clear();
                return;
            }

            Draw(fieldNow, 0f, sim.BeamAngleDegrees, fieldReach, FieldHeight, strength, castle);
            Draw(yardNow, 1f, sim.BeamAngleDegrees, yardReach, yardHeight, strength, castle);
            if (sim.BeamRotating)
            {
                // 목표 방향: 연한 부채꼴 (도는 동안만)
                Draw(fieldTarget, 0f, sim.BeamTargetDegrees, fieldReach, FieldHeight + 0.01f, strength * 0.4f, castle);
                Draw(yardTarget, 1f, sim.BeamTargetDegrees, yardReach, yardHeight + 0.01f, strength * 0.4f, castle);
            }

            // 그림(적·소품)이 받는 빛: 밤에만, 지도에서는 끔
            if (night && !mapMode) HlrBeam.Set(sim.BeamAngleDegrees, sim.BeamWidth, level * 0.8f, castle);
            else HlrBeam.Clear();
        }

        void Draw(Wedge w, float mode, float centerDegrees, float radius, float height, float strength, float castle)
        {
            float halfRad = sim.BeamWidth * 0.5f * Mathf.Deg2Rad;
            float soft = HlrBeam.SoftRadians(halfRad);
            float pad = (halfRad + soft * 1.5f) * Mathf.Rad2Deg * 2f;   // 부드러운 가장자리까지 덮는 각도 폭
            float center = centerDegrees;
            w.Vertices[0] = new Vector3(0f, height, 0f);
            for (int i = 0; i <= Segments; i++)
            {
                float a = (center - pad * 0.5f + pad * i / Segments) * Mathf.Deg2Rad;
                w.Vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
            }

            w.Mesh.Clear();
            w.Mesh.vertices = w.Vertices;
            w.Mesh.triangles = w.Triangles;
            w.Mesh.bounds = new Bounds(new Vector3(0f, height, 0f), new Vector3(radius * 2f, 2f, radius * 2f));

            float rad = centerDegrees * Mathf.Deg2Rad;
            w.Block.SetFloat(ModeId, mode);
            w.Block.SetVector(DirId, new Vector4(Mathf.Cos(rad), Mathf.Sin(rad), 0f, 0f));
            w.Block.SetVector(HalfId, new Vector4(halfRad, soft, strength, castle));
            w.Block.SetVector(ProfileId, new Vector4(HlrBeam.Strip, HlrBeam.Ramp, HlrBeam.FallLength, 0f));
            w.Block.SetColor(ColorId, HlrBeam.BeamColor);
            Graphics.DrawMesh(w.Mesh, Matrix4x4.identity, material, 0, null, 0, w.Block);
        }

        public void Dispose()
        {
            HlrBeam.Clear();
            foreach (Wedge w in new[] { fieldNow, yardNow, fieldTarget, yardTarget }) if (w.Mesh != null) Object.Destroy(w.Mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}
