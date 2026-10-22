using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Render
{
    // 들판 땅 재질·격자 (W15): 단색 평면 대신 촘촘한 격자 + FieldGround 셰이더 (큰 무늬·잔 자갈·낮은 출렁임·노멀맵·점광원 목록).
    // 높이는 눈에만 보인다 — 시뮬레이션은 평지 그대로이고(BattleSim에 높이 없음) 땅은 0 아래로만 꺼져서 발이 묻히지 않는다
    public static class FieldGround
    {
        public const string ShaderName = "FieldGround";
        const float InnerStep = 2.5f;   // 가운데(전장) 격자 간격 (m)
        const float OuterStep = 80f;    // 바깥 가장자리 (회색 가장자리 방지용)

        public static bool Dress(GameObject ground, float halfExtent, float inner, float castleHalf)
        {
            Shader shader = Resources.Load<Shader>(ShaderName);
            var grass = Resources.Load<Texture2D>("BattleArt/World/field_grass_albedo");
            if (shader == null || grass == null || ground == null) { Debug.LogWarning($"[FieldGround] 자원 없음: 셰이더 {shader != null}, 풀 그림 {grass != null}"); return false; }
            var dirt = Resources.Load<Texture2D>("BattleArt/World/field_dirt_albedo");
            var grassN = Resources.Load<Texture2D>("BattleArt/World/field_grass_normal");
            var dirtN = Resources.Load<Texture2D>("BattleArt/World/field_dirt_normal");
            Point(grass); Point(dirt); Point(grassN); Point(dirtN);

            var material = new Material(shader) { name = "FieldGround (runtime)" };
            material.SetTexture("_GrassTex", grass);
            material.SetTexture("_DirtTex", dirt != null ? dirt : grass);
            if (grassN != null) material.SetTexture("_GrassNrm", grassN);
            if (dirtN != null) material.SetTexture("_DirtNrm", dirtN);
            material.SetFloat("_TileMeters", grass.width / 32f);   // 1m당 32화소 (BattleWorldMaterials와 같은 비율)
            material.SetFloat("_CastleHalf", castleHalf);

            var filter = ground.GetComponent<MeshFilter>();
            var renderer = ground.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return false;
            filter.sharedMesh = BuildGrid(halfExtent, inner);
            renderer.sharedMaterial = material;
            Debug.Log("[FieldGround] 들판 땅 셰이더 적용");
            ground.transform.localScale = Vector3.one;
            var oldCollider = ground.GetComponent<Collider>();
            if (oldCollider != null) Object.Destroy(oldCollider);
            return true;
        }

        static void Point(Texture2D t)
        {
            if (t == null) return;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Repeat;
        }

        // 가운데 ±inner는 InnerStep 간격, 그 바깥은 OuterStep 몇 칸으로 halfExtent까지
        static Mesh BuildGrid(float halfExtent, float inner)
        {
            var axis = new List<float>();
            for (float c = -inner; c <= inner + 0.01f; c += InnerStep) axis.Add(c);
            for (float c = inner + OuterStep; c < halfExtent; c += OuterStep) { axis.Insert(0, -c); axis.Add(c); }
            axis.Insert(0, -halfExtent);
            axis.Add(halfExtent);
            int n = axis.Count;

            var vertices = new Vector3[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    vertices[z * n + x] = new Vector3(axis[x], 0f, axis[z]);

            var triangles = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    triangles[t++] = i; triangles[t++] = i + n; triangles[t++] = i + 1;   // 위에서 볼 때 시계 방향 (위쪽 면)
                    triangles[t++] = i + 1; triangles[t++] = i + n; triangles[t++] = i + n + 1;
                }
            }

            var mesh = new Mesh { name = "Field Ground (runtime)", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(halfExtent * 2f, 2f, halfExtent * 2f));
            return mesh;
        }
    }
}

