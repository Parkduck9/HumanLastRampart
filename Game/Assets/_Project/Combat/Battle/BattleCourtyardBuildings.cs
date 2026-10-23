using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 정비 화면(낮) 건물을 마당에 대충 세운 3D 블록아웃 (W28, 사용자 확인용 — 품질은 낮아도 된다)
    // - 건물: 성벽 수리 · 병영 · 함정 공방 · 상점 (PrepEntry의 정비 장소). 등대는 가운데 탑, 성벽 계단은 북쪽 층계(BattleCourtyard)가 이미 맡는다
    // - 상자 벽 + 박공(또는 평평한) 지붕 + 문. 성벽 안쪽 면에 붙여 짓고, 탑 둘레·북쪽 길·계단은 비워 둔다
    // - 높이는 성벽 높이보다 낮게(시야를 막지 않게). 그리는 수: 벽 2 + 지붕 2 + 문 1 = 5번 (재질마다 합친 메시), 매 프레임 일은 없음
    public sealed class BattleCourtyardBuildings : IDisposable
    {
        public const float TowerRing = 5f;        // 탑 둘레 비움 반지름 (병사 전령이 곁에 서는 자리 포함)
        public const float RoadHalfWidth = 3f;    // 계단에서 탑으로 가는 길 (너비 6m)
        const float WallInner = 2f;               // 성벽 두께 (마당은 half - 2까지)
        const float Gap = 0.3f;                   // 성벽 안쪽 면과 건물 사이 틈
        const float Overhang = 0.25f;             // 지붕 처마
        const float DoorWidth = 0.9f, DoorHeight = 1.5f, DoorDepth = 0.06f;
        const float RoofRise = 0.9f;

        public enum WallKind { Stone, Timber }
        public enum RoofKind { Slate, Tile }

        public readonly struct Plot
        {
            public readonly string Name;
            public readonly int Side;        // 붙는 성벽 (0 북 · 1 동 · 2 남 · 3 서)
            public readonly float Along;     // 그 성벽 가운데에서 오른쪽(성 안에서 바깥을 볼 때)으로 옮긴 거리
            public readonly float Width, Depth, WallHeight;
            public readonly bool Pitched;
            public readonly WallKind Wall;
            public readonly RoofKind Roof;

            public Plot(string name, int side, float along, float width, float depth, float wallHeight, bool pitched, WallKind wall, RoofKind roof)
            {
                Name = name; Side = side; Along = along; Width = width; Depth = depth; WallHeight = wallHeight;
                Pitched = pitched; Wall = wall; Roof = roof;
            }

            // 마당 윗면 위 가장 높은 곳
            public float TotalHeight => WallHeight + (Pitched ? RoofRise : 0.15f);
        }

        // 정비 마당 그림의 왼쪽→오른쪽 순서(수리 · 병영 · 등대 · 함정 · 상점 · 계단)를 네 성벽에 돌려 놓았다
        public static readonly Plot[] Plots =
        {
            new Plot("성벽 수리", 3, 6f, 7f, 4f, 2.2f, true, WallKind.Stone, RoofKind.Slate),
            new Plot("병영", 1, 8f, 9f, 5f, 2.4f, true, WallKind.Timber, RoofKind.Tile),
            new Plot("함정 공방", 2, 8f, 6f, 4f, 2.1f, false, WallKind.Stone, RoofKind.Slate),
            new Plot("상점", 2, -8f, 6f, 4f, 2.2f, true, WallKind.Timber, RoofKind.Tile),
        };

        static Vector3 Out(int side) { var o = WallGeometry.Outward(side); return new Vector3(o.x, 0f, o.z); }
        static Vector3 Right(int side) { var r = WallGeometry.Right(side); return new Vector3(r.x, 0f, r.z); }

        // 바닥 위에서 본 건물이 차지하는 땅 (처마·문 포함, 세계 좌표 x·z 사각형) — 테스트가 쓴다
        public static Rect Footprint(in Plot p, float half)
        {
            Vector3 n = Out(p.Side), r = Right(p.Side);
            float back = half - WallInner - Gap;
            float oh = p.Pitched ? Overhang : 0.1f;
            Vector3 a = n * back + r * (p.Along - p.Width * 0.5f - oh);
            Vector3 b = n * (back - p.Depth - Mathf.Max(oh, DoorDepth)) + r * (p.Along + p.Width * 0.5f + oh);
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        readonly GameObject root;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();

        public BattleCourtyardBuildings(float half, Transform parent)
        {
            root = new GameObject("Courtyard Buildings (W28 블록아웃)");
            root.transform.SetParent(parent, false);

            var wallStone = new Builder(); var wallTimber = new Builder();
            var roofSlate = new Builder(); var roofTile = new Builder();
            var doors = new Builder();
            foreach (Plot p in Plots)
            {
                Builder wall = p.Wall == WallKind.Stone ? wallStone : wallTimber;
                Builder roof = p.Roof == RoofKind.Slate ? roofSlate : roofTile;
                Frame f = new Frame(Out(p.Side), Right(p.Side), half - WallInner - Gap, BattleSim.WallHeight + BattleCourtyardSkin);
                float x0 = p.Along - p.Width * 0.5f, x1 = p.Along + p.Width * 0.5f;
                wall.Box(f, x0, 0f, 0f, x1, p.Depth, p.WallHeight);
                if (p.Pitched) roof.Gable(f, x0 - Overhang, -Overhang, x1 + Overhang, p.Depth + Overhang, p.WallHeight, RoofRise);
                else roof.Box(f, x0 - 0.1f, -0.1f, p.WallHeight, x1 + 0.1f, p.Depth + 0.1f, p.WallHeight + 0.15f);   // 평지붕: 얇은 판
                // 문: 마당 쪽 면 가운데에 살짝 튀어나온 판
                doors.Box(f, p.Along - DoorWidth * 0.5f, p.Depth, 0f, p.Along + DoorWidth * 0.5f, p.Depth + DoorDepth, DoorHeight);
            }

            Spawn("Walls Stone", wallStone, Make(new Color(0.52f, 0.50f, 0.46f)));
            Spawn("Walls Timber", wallTimber, Make(new Color(0.46f, 0.34f, 0.24f)));
            Spawn("Roofs Slate", roofSlate, Make(new Color(0.36f, 0.36f, 0.38f)));
            Spawn("Roofs Tile", roofTile, Make(new Color(0.55f, 0.27f, 0.20f)));
            Spawn("Doors", doors, Make(new Color(0.20f, 0.13f, 0.09f)));
        }

        const float BattleCourtyardSkin = 0.01f;   // 마당 바닥판과 같은 높이 (BattleCourtyard.SkinLift)

        void Spawn(string name, Builder b, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            Mesh mesh = b.ToMesh(name);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        Material Make(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            m.name = "CourtyardBuildings (runtime)";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            materials.Add(m);
            return m;
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (Material m in materials) if (m != null) UnityEngine.Object.Destroy(m);
            foreach (Mesh m in meshes) if (m != null) UnityEngine.Object.Destroy(m);
            materials.Clear();
            meshes.Clear();
        }

        // 성벽 한 면에 붙은 자기 좌표: x = 성벽 따라 오른쪽, b = 성벽 안쪽 면에서 마당 쪽 깊이, y = 위
        readonly struct Frame
        {
            readonly Vector3 n, r;
            readonly float back, floor;
            public Frame(Vector3 outward, Vector3 right, float back, float floor) { n = outward; r = right; this.back = back; this.floor = floor; }
            public Vector3 P(float x, float b, float y) => n * (back - b) + r * x + Vector3.up * (floor + y);
            public Vector3 Inward => -n;
            public Vector3 Right => r;
        }

        // 상자·박공 지붕을 한 메시로 합친다 (면마다 법선 하나, 색만 쓰니 UV는 0)
        sealed class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            // x0..x1 성벽 따라, b0..b1 깊이, y0..y1 높이. 아랫면은 안 보이니 만들지 않는다
            public void Box(in Frame f, float x0, float b0, float y0, float x1, float b1, float y1)
            {
                Vector3 up = Vector3.up, inw = f.Inward, rt = f.Right;
                Quad(f.P(x0, b1, y0), f.P(x1, b1, y0), f.P(x1, b1, y1), f.P(x0, b1, y1), inw);     // 마당 쪽 면
                Quad(f.P(x1, b0, y0), f.P(x0, b0, y0), f.P(x0, b0, y1), f.P(x1, b0, y1), -inw);    // 성벽 쪽 면
                Quad(f.P(x1, b1, y0), f.P(x1, b0, y0), f.P(x1, b0, y1), f.P(x1, b1, y1), rt);
                Quad(f.P(x0, b0, y0), f.P(x0, b1, y0), f.P(x0, b1, y1), f.P(x0, b0, y1), -rt);
                Quad(f.P(x0, b1, y1), f.P(x1, b1, y1), f.P(x1, b0, y1), f.P(x0, b0, y1), up);
            }

            // 박공 지붕: 용마루는 성벽과 나란히, 처마 높이 eaveY에서 rise만큼 솟는다
            public void Gable(in Frame f, float x0, float b0, float x1, float b1, float eaveY, float rise)
            {
                float mid = (b0 + b1) * 0.5f;
                Vector3 e0a = f.P(x0, b1, eaveY), e0b = f.P(x1, b1, eaveY);   // 마당 쪽 처마
                Vector3 e1a = f.P(x0, b0, eaveY), e1b = f.P(x1, b0, eaveY);   // 성벽 쪽 처마
                Vector3 ra = f.P(x0, mid, eaveY + rise), rb = f.P(x1, mid, eaveY + rise);
                Quad(e0a, e0b, rb, ra, Vector3.Normalize(f.Inward * rise + Vector3.up * (b1 - mid)));
                Quad(e1b, e1a, ra, rb, Vector3.Normalize(-f.Inward * rise + Vector3.up * (mid - b0)));
                Tri(e0b, e1b, rb, f.Right);
                Tri(e1a, e0a, ra, -f.Right);
            }

            // 앞면 = 법선 쪽 (감는 방향이 어긋나면 뒤집는다)
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 t = a; a = d; d = t; t = b; b = c; c = t; }   // a↔d, b↔c: 순서 뒤집기
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 t = b; b = c; c = t; }
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                for (int i = 0; i < 3; i++) normals.Add(normal);
                tris.AddRange(new[] { s, s + 1, s + 2 });
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
