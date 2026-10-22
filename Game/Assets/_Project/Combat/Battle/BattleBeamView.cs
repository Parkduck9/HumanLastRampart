using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 등대 빛줄기 임시 그림 (Concept_Beacon_v1 9.4): 탑에서 뻗는 납작한 반투명 부채꼴.
    // 최종 그림은 Codex 카드(K-106)로 바뀐다 — 그 그림은 사진기 방향을 미리 그린 평면 그림이라 네 방향으로 돌리는 데는 못 쓰고, 렌즈 빛(beam_head)만 BattleBeaconView가 쓴다.
    // W9: 마당 위(성벽 윗면 높이)와 들판 위(땅 높이) 두 장으로 그려 탑에서 들판까지 이어져 보이게 하고,
    // 도는 중에는 가려는 방향의 연한 부채꼴을 하나 더 그려 '지금 방향 + 목표 방향'을 함께 보여 준다 (지도에서도 보인다)
    public sealed class BattleBeamView
    {
        const int Segments = 16;
        const float FieldHeight = 0.06f;
        const float YardLift = 0.03f;   // 마당 윗면(성벽 상자 윗면 -0.1) 바로 위

        sealed class Wedge
        {
            public Mesh Mesh;
            public readonly Vector3[] Vertices = new Vector3[Segments + 2];
            public readonly Color[] Colors = new Color[Segments + 2];
            public readonly int[] Triangles = new int[Segments * 3];
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

            Shader shader = Shader.Find("Sprites/Default");   // 꼭짓점 색 × 반투명 (기본 포함 셰이더)
            if (shader != null) material = new Material(shader) { name = "등대 빛줄기 (runtime)", renderQueue = 3000 };
        }

        public void Draw()
        {
            if (sim == null || material == null) return;
            float field = sim.FieldSize;
            float yard = sim.CastleHalf - 2f;
            float yardHeight = BattleSim.WallHeight - 0.1f + YardLift;
            Fill(fieldNow, sim.BeamAngleDegrees, field, FieldHeight, 0.34f, 0.08f);
            Fill(yardNow, sim.BeamAngleDegrees, yard, yardHeight, 0.20f, 0.05f);   // 마당 위는 연하게 (주인공·일꾼이 서 있는 곳)
            if (sim.BeamRotating)
            {
                // 목표 방향: 연한 부채꼴 (도는 동안만)
                Fill(fieldTarget, sim.BeamTargetDegrees, field, FieldHeight + 0.01f, 0.14f, 0.03f);
                Fill(yardTarget, sim.BeamTargetDegrees, yard, yardHeight + 0.01f, 0.10f, 0.03f);
            }
        }

        void Fill(Wedge w, float centerDegrees, float radius, float height, float alphaCenter, float alphaEdge)
        {
            float half = sim.BeamWidth * 0.5f;
            w.Vertices[0] = new Vector3(0f, height, 0f);
            w.Colors[0] = new Color(1f, 0.92f, 0.55f, alphaCenter);
            for (int i = 0; i <= Segments; i++)
            {
                float a = (centerDegrees - half + sim.BeamWidth * i / Segments) * Mathf.Deg2Rad;
                w.Vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
                w.Colors[i + 1] = new Color(1f, 0.92f, 0.55f, alphaEdge);
            }

            w.Mesh.Clear();
            w.Mesh.vertices = w.Vertices;
            w.Mesh.colors = w.Colors;
            w.Mesh.triangles = w.Triangles;
            w.Mesh.bounds = new Bounds(new Vector3(0f, height, 0f), new Vector3(radius * 2f, 2f, radius * 2f));
            Graphics.DrawMesh(w.Mesh, Matrix4x4.identity, material, 0);
        }

        public void Dispose()
        {
            foreach (Wedge w in new[] { fieldNow, yardNow, fieldTarget, yardTarget }) if (w.Mesh != null) Object.Destroy(w.Mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}
