using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 가운데 높은 등대 탑 (W20, 사용자 결정 2026-10-22: 탑이 너무 작다 -> 성벽 높이의 3배).
    // Codex 그림이 올 때(10-27 이후)까지의 임시 처리라 모양을 일부러 단순하게 한 클래스에 가둔다 (그림이 오면 이 클래스만 바꾼다/끈다).
    // - 몸체: 성벽 돌 재질(wall_stone)로 쌓은 위로 좁아지는 사각 탑 + 띠 둘 + 둘레길 + 귀퉁이 돌출부 + 지붕. 한 메시 = 그리기 1번
    // - 꼭대기 불방(등불 방): 모서리 기둥(돌, 몸체 메시에 포함) + 가운데 빛나는 상자(그리기 1번, 밝기는 등불 밝기를 따른다)
    // - 문: 남쪽(사진기·주인공이 서는 쪽) 아래 어두운 문 하나 (그리기 1번). 주인공은 문 앞(BattleCameraRig.TowerHeroBack)에서 점화·명령한다 — 오르지 않는다
    // - 합쳐 그리기 3번, 프레임마다 새로 만드는 것 없음 (밝기는 재질 색만 바꾼다)
    // 빛줄기·탑 불빛·불꽃은 모두 LampHeight(불방 가운데 높이)에서 나간다 (BattleBeamView.OriginHeight, BattleLights, BattleBeaconView.Bowl)
    public sealed class BattleTowerView : IDisposable
    {
        public const float Height = 3f * BattleSim.WallHeight;   // 마당 윗면 위로 탑 전체 키 (성벽 높이의 3배)
        public const float BaseHalf = 1.1f;                      // 아래 반폭 (병사 전령이 곁에 서는 자리가 이 밖이어야 한다)
        public const float DoorZ = -BaseHalf;                    // 문이 있는 면 (남쪽)
        const float TopHalf = 0.78f;
        const float GalleryY = 0.72f * Height;                   // 둘레길 바닥
        const float LampRoomHeight = 0.11f * Height;             // 불방 키
        public const float LampFloor = GalleryY + 0.34f;           // 불방 바닥 높이 (불꽃이 서는 자리)
        public const float LampHeight = GalleryY + 0.3f + LampRoomHeight * 0.5f;   // 마당 윗면 위 불방 가운데 높이 (빛이 나가는 자리)

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly Color LampColor = Color.white;   // 색은 번짐 그림이 가진다. 밝기 배수만 바꾼다
        readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        readonly GameObject root;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Material lampMaterial;
        BattleWorldMaterials stone;
        bool ownsStone;

        // 시험·확인용
        public Vector3 LampWorld => new Vector3(0f, BattleSim.WallHeight + LampHeight, 0f);
        public int RendererCount { get; private set; }
        public bool HasStoneArt { get; private set; }

        public BattleTowerView(Transform parent, BattleWorldMaterials sharedStone = null)
        {
            root = new GameObject("Beacon Tower (W20 임시)");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, BattleSim.WallHeight, 0f);

            ownsStone = sharedStone == null;
            stone = sharedStone ?? new BattleWorldMaterials();
            var surface = BattleWorldMaterials.Surface.WallStone;
            HasStoneArt = stone.HasArt(surface);
            Vector2 tile = Vector2.one;
            if (HasStoneArt)
            {
                var t = stone.Get(surface).mainTexture;
                tile = new Vector2(t.width / 32f, t.height / 32f);   // 성벽·계단과 같은 돌 크기
            }

            // 몸체 (돌)
            var body = new Builder(tile);
            body.Box(new Vector3(-BaseHalf - 0.15f, 0f, -BaseHalf - 0.15f), new Vector3(BaseHalf + 0.15f, 0.14f, BaseHalf + 0.15f));   // 받침 한 단
            float shaftTop = GalleryY;
            body.Frustum(0.14f, BaseHalf, shaftTop, TopHalf);
            foreach (float bandY in new[] { 0.28f * Height, 0.52f * Height })   // 두 띠: 위로 좁아지는 폭에 맞춘다
            {
                float h = HalfAt(bandY) + 0.1f;
                body.Box(new Vector3(-h, bandY, -h), new Vector3(h, bandY + 0.22f, h));
            }

            float g = TopHalf + 0.4f;
            body.Box(new Vector3(-g, GalleryY, -g), new Vector3(g, GalleryY + 0.3f, g));                                    // 둘레길 바닥
            foreach (int sx in new[] { -1, 1 })                                                                              // 귀퉁이 돌출부
                foreach (int sz in new[] { -1, 1 })
                    body.Box(new Vector3(sx * g - 0.14f, GalleryY + 0.3f, sz * g - 0.14f), new Vector3(sx * g + 0.14f, GalleryY + 0.62f, sz * g + 0.14f));
            float lampBase = GalleryY + 0.3f, lampTop = lampBase + LampRoomHeight;
            float p = 0.62f;
            foreach (int sx in new[] { -1, 1 })                                                                              // 불방 기둥
                foreach (int sz in new[] { -1, 1 })
                    body.Box(new Vector3(sx * p - 0.1f, lampBase, sz * p - 0.1f), new Vector3(sx * p + 0.1f, lampTop, sz * p + 0.1f));
            body.Box(new Vector3(-p - 0.2f, lampTop, -p - 0.2f), new Vector3(p + 0.2f, lampTop + 0.16f, p + 0.2f));        // 불방 지붕 테
            body.Frustum(lampTop + 0.16f, p + 0.12f, Height - 0.28f, 0.14f);                                                 // 뾰족 지붕
            body.Box(new Vector3(-0.05f, Height - 0.28f, -0.05f), new Vector3(0.05f, Height, 0.05f));                       // 꼭대기 꽂이
            Spawn("Tower Stone", body, stone.Get(surface));

            // 불방 속: 안쪽을 보는 면 (바깥에서 보면 뒷면이 보인다) + 따뜻한 번짐 그림. 열린 기둥 사이로 어두운 속과 가운데 불꽃(BattleBeaconView)이 보인다
            var glow = new Builder(Vector2.one);
            glow.BoxInside(new Vector3(-p + 0.1f, lampBase + 0.02f, -p + 0.1f), new Vector3(p - 0.1f, lampTop - 0.02f, p - 0.1f));
            lampMaterial = MakeUnlit("Tower Lamp (runtime)", Color.white);
            var gradient = MakeGlowTexture();
            lampMaterial.SetTexture(BaseMapId, gradient);
            lampMaterial.mainTexture = gradient;
            Spawn("Tower Lamp", glow, lampMaterial);

            // 문 (남쪽 면)
            var door = new Builder(Vector2.one);
            door.Box(new Vector3(-0.3f, 0.14f, DoorZ - 0.03f), new Vector3(0.3f, 1.05f, DoorZ + 0.04f));
            Spawn("Tower Door", door, MakeUnlit("Tower Door (runtime)", new Color(0.05f, 0.04f, 0.035f)));
        }

        // 가운데가 밝고 가장자리로 어두워지는 따뜻한 번짐 (불꽃이 속 벽을 비추는 모습). 한 번만 만든다
        Texture2D MakeGlowTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Tower Lamp Glow (runtime)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.3f;   // 불꽃은 방 아래쪽 가운데
                    float v = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 1.35f);
                    v = v * v * (3f - 2f * v);
                    px[y * n + x] = new Color(0.06f + 1.7f * v, 0.035f + 1.0f * v * v, 0.02f + 0.35f * v * v * v, 1f);
                }

            tex.SetPixels(px);
            tex.Apply(false, true);
            ownedObjects.Add(tex);
            return tex;
        }

        // 높이 y에서 탑 반폭
        static float HalfAt(float y) => Mathf.Lerp(BaseHalf, TopHalf, Mathf.InverseLerp(0.14f, GalleryY, y));

        // 불방 밝기: 밤에는 등불 밝기(level)에 맞춰 환하고 낮에는 어둡게. 재질 색만 바꾼다 (새로 만드는 것 없음)
        public void Tick(bool night, float level, float flameStrength)
        {
            if (lampMaterial == null) return;
            float k = night ? Mathf.Lerp(0.8f, 1.4f, Mathf.Clamp01(flameStrength)) * Mathf.Max(0.3f, level) : 0.55f;
            Color c = LampColor * k;
            c.a = 1f;
            lampMaterial.SetColor(BaseColorId, c);
            lampMaterial.SetColor(ColorId, c);
        }

        void Spawn(string name, Builder builder, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = builder.ToMesh(name);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            RendererCount++;
        }

        Material MakeUnlit(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = name };
            m.SetColor(BaseColorId, color);
            m.SetColor(ColorId, color);
            ownedMaterials.Add(m);
            return m;
        }

        static void Kill(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);   // 편집 모드 시험
        }

        public void Dispose()
        {
            if (root != null) Kill(root);
            foreach (Mesh m in meshes) if (m != null) Kill(m);
            foreach (Material m in ownedMaterials) if (m != null) Kill(m);
            foreach (UnityEngine.Object o in ownedObjects) if (o != null) Kill(o);
            ownedObjects.Clear();
            meshes.Clear();
            ownedMaterials.Clear();
            if (ownsStone) stone?.Dispose();
        }

        // 상자·위가 좁은 뿔대를 한 메시로 합친다. 돌 질감은 미터 단위 (tile = 그림 한 장이 덮는 미터)
        sealed class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();
            readonly Vector2 tile;
            readonly bool meters;

            public Builder(Vector2 tile)
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

            // 안쪽을 보는 상자 (불방 속): 바깥에서는 뒷면이라 가까운 쪽 면은 안 보이고 먼 쪽 면만 보인다
            public void BoxInside(Vector3 min, Vector3 max)
            {
                int s = verts.Count, t = tris.Count;
                Box(min, max);
                for (int i = s; i < verts.Count; i++) normals[i] = -normals[i];
                for (int i = t; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            }

            // 네 옆면만 (아래·위는 다른 상자가 덮는다). 아래 반폭 h0 -> 위 반폭 h1
            public void Frustum(float y0, float h0, float y1, float h1)
            {
                float dy = y1 - y0, dh = h0 - h1;
                float len = Mathf.Sqrt(dy * dy + dh * dh);
                float ny = dh / len, nh = dy / len;   // 바깥·위쪽으로 기운 법선
                float w0 = h0 * 2f, w1 = h1 * 2f, w = (w0 + w1) * 0.5f;
                // 남(-z)
                Quad(new Vector3(h0, y0, -h0), new Vector3(-h0, y0, -h0), new Vector3(-h1, y1, -h1), new Vector3(h1, y1, -h1), new Vector3(0f, ny, -nh), w, len);
                // 북(+z)
                Quad(new Vector3(-h0, y0, h0), new Vector3(h0, y0, h0), new Vector3(h1, y1, h1), new Vector3(-h1, y1, h1), new Vector3(0f, ny, nh), w, len);
                // 동(+x)
                Quad(new Vector3(h0, y0, h0), new Vector3(h0, y0, -h0), new Vector3(h1, y1, -h1), new Vector3(h1, y1, h1), new Vector3(nh, ny, 0f), w, len);
                // 서(-x)
                Quad(new Vector3(-h0, y0, -h0), new Vector3(-h0, y0, h0), new Vector3(-h1, y1, h1), new Vector3(-h1, y1, -h1), new Vector3(-nh, ny, 0f), w, len);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float width, float height)
            {
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                float u = meters ? width / tile.x : 1f, v = meters ? height / tile.y : 1f;
                float ou = meters ? (a.x + a.z) / tile.x : 0f, ov = meters ? a.y / tile.y : 0f;
                uvs.Add(new Vector2(ou, ov)); uvs.Add(new Vector2(ou + u, ov)); uvs.Add(new Vector2(ou + u, ov + v)); uvs.Add(new Vector2(ou, ov + v));
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
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
