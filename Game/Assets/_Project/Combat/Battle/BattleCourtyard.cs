using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 임시 채우기 (W19, 사용자 승인: Codex 그림이 올 때(10-27 이후)까지의 임시 처리, 보이는 것만 — 전투 계산은 그대로)
    // - 바닥: 정비 마당 돌바닥 그림(PrepYard/prep_yard)에서 따낸 courtyard_cobble을 점 필터로 반복해 네 성벽 안쪽에만 깐다 (조명은 다른 땅과 같은 Lit 셰이더)
    // - 계단: 북쪽 성벽(StairsSide) 통로 안쪽 끝 가운데. 마당과 성벽 윗면이 같은 높이(테스트가 약속)라 진짜로 내려가지는 못하므로
    //   '통로 끝 약간 솟은 층계참 + 낮아지는 계단 5단'을 짓고, 주인공 그림만 그 높이를 따라 오르내리게 한다(HeroLift). 그림 높이 말고는 아무것도 안 바뀐다
    // - 소품: 나무 상자·통·무기 걸이 몇 개 (정비 마당 그림에서 따낸 나무 질감). 길(탑으로 가는 가운데 줄)은 비워 둔다
    // - 그리는 수: 바닥 1 + 계단 1 + 상자 1 + 통 1 = 4번 (자리마다 합친 메시)
    public sealed class BattleCourtyard : IDisposable
    {
        const string Folder = "BattleArt/Courtyard/";
        const float SkinLift = 0.01f;            // 통로 덮는 판과 같은 높이 (BattleWorldView.SkinLift)
        const float WallThickness = 2f;
        const float CobbleTileMeters = 2.8f;     // 돌바닥 그림 한 장이 덮는 미터 (돌 하나가 병사 키(0.35m)와 비슷한 크기)

        // 계단 크기 (병사 키 0.35m, 주인공 0.375m 기준으로 낮게)
        public const float StairHalfWidth = 1.0f;
        public const float LandingDepth = 0.8f;     // 통로 안쪽 끝(성벽 안쪽 가장자리)에서 바깥쪽으로 층계참 깊이
        public const float LandingHeight = 0.24f;
        public const int StepCount = 6;             // 층계참 → 마당 (마지막 한 단은 마당 바닥)
        public const float TreadDepth = 0.2f;

        readonly GameObject root;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        BattleWorldMaterials stone;

        public BattleCourtyard(float half, Transform parent)
        {
            root = new GameObject("Courtyard (W19 임시)");
            root.transform.SetParent(parent, false);
            float top = BattleSim.WallHeight;

            BuildFloor(half, top);
            BuildStairs(half, top);
            BuildProps(half, top);
        }

        // 주인공 그림 높이 보정 (m, 발 위치 기준). 계단·층계참 위에서만 0보다 크다
        public static float HeroLift(Vector3 feet, float half)
        {
            float z0 = half - WallThickness;                 // 성벽 안쪽 가장자리
            float landingEnd = z0 + LandingDepth;
            float ax = Mathf.Abs(feet.x);
            float wx = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StairHalfWidth - 0.2f, StairHalfWidth + 0.1f, ax));
            if (wx <= 0f) return 0f;
            float z = feet.z;
            float lift;
            if (z >= z0)
            {
                // 층계참 (바깥 끝은 부드럽게 내려온다 — 통로를 따라 달릴 때 튀지 않게)
                lift = LandingHeight * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(landingEnd - 0.1f, landingEnd + 0.1f, z)));
            }
            else
            {
                int k = Mathf.FloorToInt((z0 - z) / TreadDepth);
                lift = k >= StepCount - 1 ? 0f : TreadTop(k);
            }

            return lift * wx;
        }

        // k번째 단 윗면 높이 (k = 0이 층계참 바로 아래)
        static float TreadTop(int k) => LandingHeight * (1f - (k + 1f) / StepCount);

        // ── 바닥 ──
        void BuildFloor(float half, float top)
        {
            var cobble = Resources.Load<Texture2D>(Folder + "courtyard_cobble");
            float size = (half - WallThickness) * 2f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Courtyard Floor";
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, top + SkinLift, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.GetComponent<Renderer>();
            Material m = MakeLit(cobble, new Color(0.5f, 0.47f, 0.42f), 0.05f);
            if (cobble != null)
            {
                var scale = new Vector2(size / CobbleTileMeters, size / CobbleTileMeters);
                m.mainTextureScale = scale;
                if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", scale);
            }

            renderer.sharedMaterial = m;
        }

        // ── 계단 ──
        void BuildStairs(float half, float top)
        {
            stone = new BattleWorldMaterials();
            var surface = BattleWorldMaterials.Surface.WallStone;
            Vector2 tile = new Vector2(1f, 1f);
            if (stone.HasArt(surface))
            {
                var t = stone.Get(surface).mainTexture;
                tile = new Vector2(t.width / 32f, t.height / 32f);
            }

            var b = new MeshBuilder(tile);
            float z0 = half - WallThickness;
            // 층계참: 통로 안쪽 끝
            b.Box(new Vector3(-StairHalfWidth, top, z0), new Vector3(StairHalfWidth, top + LandingHeight, z0 + LandingDepth));
            for (int k = 0; k < StepCount - 1; k++)
                b.Box(new Vector3(-StairHalfWidth, top, z0 - (k + 1) * TreadDepth), new Vector3(StairHalfWidth, top + TreadTop(k), z0 - k * TreadDepth));

            var go = new GameObject("Courtyard Stairs");
            go.transform.SetParent(root.transform, false);
            var mesh = b.ToMesh("Courtyard Stairs");
            ownedMeshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = stone.Get(surface);
        }

        // ── 소품 ──
        void BuildProps(float half, float top)
        {
            var crateTex = Resources.Load<Texture2D>(Folder + "crate_wood");
            var barrelTex = Resources.Load<Texture2D>(Folder + "barrel_wood");
            float y = top + SkinLift;
            float zN = half - 2.6f;       // 북쪽 성벽 안쪽 줄
            float edge = half - 4f;

            var crates = new MeshBuilder(Vector2.one);
            // (가운데 x=0 줄은 비움: 계단·탑으로 가는 길)
            AddCrate(crates, new Vector3(-3.4f, y, zN), 0.34f, 0.3f, 12f);
            AddCrate(crates, new Vector3(-3.0f, y, zN - 0.5f), 0.26f, 0.24f, -20f);
            AddCrate(crates, new Vector3(-3.4f, y + 0.3f, zN), 0.24f, 0.22f, 30f);
            AddCrate(crates, new Vector3(-edge, y, zN - 0.2f), 0.34f, 0.3f, -8f);
            AddCrate(crates, new Vector3(6.2f, y, 2.4f), 0.3f, 0.28f, 15f);
            // 무기 걸이: 기둥 둘 + 가로대 + 세운 자루 넷
            AddRack(crates, new Vector3(edge, y, zN - 0.1f));

            var barrels = new MeshBuilder(Vector2.one);
            AddBarrel(barrels, new Vector3(3.5f, y, zN), 0.15f, 0.34f);
            AddBarrel(barrels, new Vector3(3.9f, y, zN - 0.45f), 0.13f, 0.3f);
            AddBarrel(barrels, new Vector3(-6.4f, y, 1.2f), 0.15f, 0.34f);
            AddBarrel(barrels, new Vector3(-edge + 0.8f, y, zN - 0.9f), 0.14f, 0.32f);

            Spawn("Courtyard Crates", crates, MakeLit(crateTex, new Color(0.6f, 0.52f, 0.45f), 0.1f));
            Spawn("Courtyard Barrels", barrels, MakeLit(barrelTex, new Color(0.6f, 0.52f, 0.45f), 0.1f));
        }

        void Spawn(string name, MeshBuilder builder, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = builder.ToMesh(name);
            ownedMeshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void AddCrate(MeshBuilder b, Vector3 baseCenter, float size, float height, float yawDegrees)
            => b.BoxYaw(baseCenter, new Vector3(size, height, size), yawDegrees);

        static void AddRack(MeshBuilder b, Vector3 baseCenter)
        {
            b.BoxYaw(baseCenter + new Vector3(-0.35f, 0f, 0f), new Vector3(0.05f, 0.34f, 0.05f), 0f);
            b.BoxYaw(baseCenter + new Vector3(0.35f, 0f, 0f), new Vector3(0.05f, 0.34f, 0.05f), 0f);
            b.BoxYaw(baseCenter + new Vector3(0f, 0.22f, 0f), new Vector3(0.75f, 0.04f, 0.05f), 0f);
            for (int i = 0; i < 4; i++)
                b.BoxYaw(baseCenter + new Vector3(-0.24f + i * 0.16f, 0f, 0.04f), new Vector3(0.025f, 0.46f, 0.025f), 0f);
        }

        static void AddBarrel(MeshBuilder b, Vector3 baseCenter, float radius, float height) => b.Cylinder(baseCenter, radius, height, 10);

        Material MakeLit(Texture2D texture, Color fallback, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            m.name = "Courtyard (runtime)";
            var tint = texture != null ? Color.white : fallback;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Repeat;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
                m.mainTexture = texture;
            }

            ownedMaterials.Add(m);
            return m;
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (Material m in ownedMaterials) if (m != null) UnityEngine.Object.Destroy(m);
            foreach (Mesh m in ownedMeshes) if (m != null) UnityEngine.Object.Destroy(m);
            ownedMaterials.Clear();
            ownedMeshes.Clear();
            stone?.Dispose();
        }

        // 상자·원통을 한 메시로 합친다. uvTile = 그림 한 장이 덮는 미터 (돌 질감은 면 크기에 맞춰 반복, 나무는 면마다 한 장)
        sealed class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();
            readonly Vector2 tile;
            readonly bool meters;

            public MeshBuilder(Vector2 tile)
            {
                this.tile = tile;
                meters = tile != Vector2.one;
            }

            public void Box(Vector3 min, Vector3 max)
            {
                Vector3 s = max - min;
                Quad(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.forward, s.x, s.y);
                Quad(new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.back, s.x, s.y);
                Quad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Vector3.right, s.z, s.y);
                Quad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Vector3.left, s.z, s.y);
                Quad(new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.up, s.x, s.z);
            }

            // 바닥 중심·크기(가로·높이·세로)·좌우 회전(도)
            public void BoxYaw(Vector3 baseCenter, Vector3 size, float yawDegrees)
            {
                int start = verts.Count;
                Box(new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f), new Vector3(size.x * 0.5f, size.y, size.z * 0.5f));
                Quaternion q = Quaternion.Euler(0f, yawDegrees, 0f);
                for (int i = start; i < verts.Count; i++)
                {
                    verts[i] = q * verts[i] + baseCenter;
                    normals[i] = q * normals[i];
                }
            }

            public void Cylinder(Vector3 baseCenter, float radius, float height, int segments)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    // 통 가운데가 볼록: 위·아래 반지름 0.88
                    int s = verts.Count;
                    verts.Add(baseCenter + d0 * radius * 0.88f); verts.Add(baseCenter + d1 * radius * 0.88f);
                    verts.Add(baseCenter + Vector3.up * height * 0.5f + d1 * radius); verts.Add(baseCenter + Vector3.up * height * 0.5f + d0 * radius);
                    verts.Add(baseCenter + Vector3.up * height + d0 * radius * 0.88f); verts.Add(baseCenter + Vector3.up * height + d1 * radius * 0.88f);
                    float u0 = (float)i / segments, u1 = (float)(i + 1) / segments;
                    // 그림 한 장이 통을 한 바퀴가 아니라 앞면 한 장으로 보이게: 반 바퀴에 한 장
                    float v0 = u0 * 2f % 1f, v1 = u1 * 2f % 1f;
                    if (v1 <= v0) v1 = 1f;
                    for (int n = 0; n < 6; n++) normals.Add((n % 2 == 0 ? d0 : d1));
                    uvs.Add(new Vector2(v0, 0f)); uvs.Add(new Vector2(v1, 0f)); uvs.Add(new Vector2(v1, 0.5f)); uvs.Add(new Vector2(v0, 0.5f));
                    uvs.Add(new Vector2(v0, 1f)); uvs.Add(new Vector2(v1, 1f));
                    tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2, s + 3, s + 4, s + 5, s + 3, s + 5, s + 2 });
                }

                // 윗면 (어두운 나무 느낌: 가운데 한 점 부채꼴)
                int c = verts.Count;
                verts.Add(baseCenter + Vector3.up * height); normals.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 0.9f));
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    verts.Add(baseCenter + Vector3.up * height + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.88f);
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.4f, 0.9f + Mathf.Sin(a) * 0.05f));
                }

                for (int i = 0; i < segments; i++) tris.AddRange(new[] { c, c + 2 + i, c + 1 + i });
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float width, float height)
            {
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                float u = meters ? width / tile.x : 1f, v = meters ? height / tile.y : 1f;
                // 돌 질감은 가장자리에서 시작하지 않게 높이·가로 위치를 조금 밀어 이웃 단과 줄눈이 겹치지 않게
                float ou = meters ? (a.x + a.z) / tile.x : 0f, ov = meters ? a.y / tile.y : 0f;
                uvs.Add(new Vector2(ou, ov)); uvs.Add(new Vector2(ou + u, ov)); uvs.Add(new Vector2(ou + u, ov + v)); uvs.Add(new Vector2(ou, ov + v));
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });   // 유니티는 시계 방향이 앞면: 바깥쪽에서 볼 때 a→b→c의 외적이 바깥을 향한다
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
