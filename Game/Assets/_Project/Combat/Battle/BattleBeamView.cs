using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 등대 빛줄기 임시 그림 (Concept_Beacon_v1 9.4): 성 가운데에서 뻗는 납작한 반투명 부채꼴 하나.
    // 최종 그림은 Codex 카드(K-106)로 바뀐다. 메시 하나를 매 프레임 각도·폭에 맞게 다시 채운다
    public sealed class BattleBeamView
    {
        const int Segments = 16;
        const float Height = 0.06f;

        readonly BattleSim sim;
        readonly Mesh mesh;
        readonly Material material;
        readonly Vector3[] vertices = new Vector3[Segments + 2];
        readonly Color[] colors = new Color[Segments + 2];
        readonly int[] triangles = new int[Segments * 3];

        public BattleBeamView(BattleSim sim)
        {
            this.sim = sim;
            mesh = new Mesh { name = "등대 빛줄기 (runtime)" };
            mesh.MarkDynamic();
            for (int i = 0; i < Segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            Shader shader = Shader.Find("Sprites/Default");   // 꼭짓점 색 × 반투명 (기본 포함 셰이더)
            if (shader != null) material = new Material(shader) { name = "등대 빛줄기 (runtime)", renderQueue = 3000 };
        }

        public void Draw()
        {
            if (sim == null || material == null) return;
            float radius = sim.FieldSize;
            float half = sim.BeamWidth * 0.5f;
            float center = sim.BeamAngleDegrees;
            vertices[0] = new Vector3(0f, Height, 0f);
            colors[0] = new Color(1f, 0.92f, 0.55f, 0.34f);
            for (int i = 0; i <= Segments; i++)
            {
                float a = (center - half + sim.BeamWidth * i / Segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, Height, Mathf.Sin(a) * radius);
                colors[i + 1] = new Color(1f, 0.92f, 0.55f, 0.08f);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2f, 2f, radius * 2f));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
        }

        public void Dispose()
        {
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}