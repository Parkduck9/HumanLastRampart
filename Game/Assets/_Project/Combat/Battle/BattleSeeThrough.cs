using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 주인공 둘레 투시 구멍 (W42, 사용자 2026-10-26 "건물이 가리면 주인공만 동그랗게 뚫어서 보이게")
    // - 가리는 면(건물·탑·소품·성벽 조각·나무)이 사진기와 주인공 사이에 있을 때만 켜진다 (사진기→머리·가슴·발 선이 가리는 것의 경계 상자를 지나는지, 0.15초에 걸쳐 켜고 끈다)
    // - 구멍은 셰이더(HlrSeeThroughLit/Unlit, Render/HlrSeeThrough.hlsl)가 낸다: 구멍 안이면서 주인공보다 사진기에 가까운 조각만 정렬 디더로 버린다 → 주인공 뒤의 것·바닥은 그대로
    // - 추가 그리기 없음·프레임마다 새로 만드는 것 없음. 가리는 재질은 주기적으로(2초) 훑어 URP Lit/Unlit(불투명)을 같은 속성의 투시 셰이더로 바꿔 끼운다 (재질이 여기저기서 만들어져서)
    // - 코드로 끄기: BattleSeeThrough.Enabled = false
    public sealed class BattleSeeThrough
    {
        public static bool Enabled = true;

        public const float FadeSeconds = 0.15f;      // 켜고 끄는 시간
        public const float RadiusPerHeight = 1.2f;   // 구멍 반지름 = 주인공 키 × 이 값 (화면에서, 발 조금 아래까지 덮는다)
        public const float InnerRatio = 0.55f;       // 반지름 안쪽 이만큼은 완전히 뚫리고 바깥은 디더로 옅어진다
        public const float DepthMargin = 0.08f;      // 주인공 깊이보다 이만큼(m) 더 가까운 것만 뚫는다 (옆에 붙은 것·같은 깊이는 그대로)
        public const float FloorMargin = 0.06f;      // 주인공 발 높이 + 이만큼 아래는 안 뚫는다 (통로·마당 돌바닥)
        public const float SweepSeconds = 2f;        // 재질을 훑는 간격
        const float BodyCenter = 0.45f;              // 구멍 중심 = 발 위 키의 이 비율

        static readonly int AId = Shader.PropertyToID("_HlrSeeThroughA");
        static readonly int BId = Shader.PropertyToID("_HlrSeeThroughB");
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");

        readonly Camera camera;
        readonly List<Renderer> occluders = new List<Renderer>();
        readonly List<MeshFilter> occluderMeshes = new List<MeshFilter>();   // 큰 합친 메시(건물 등)만: 경계 상자는 너무 넉넉해서 삼각형으로 다시 본다. 작은 것은 null
        readonly Dictionary<Mesh, (Vector3[] vertices, int[] triangles)> meshCache = new Dictionary<Mesh, (Vector3[], int[])>();
        public const float PreciseSize = 6f;   // 경계 상자 한 변이 이보다 길면 삼각형까지 검사 (건물이 한 메시로 합쳐져 있다)
        readonly List<Material> materialBuffer = new List<Material>(4);
        Shader stockLit, stockUnlit, seeLit, seeUnlit;
        float strength;
        float nextSweep;

        public float Strength => strength;          // 지금 켜진 정도 0~1
        public bool Occluded { get; private set; }  // 가리는 것이 있다
        public int OccluderCount => occluders.Count;
        public Renderer LastOccluder { get; private set; }   // 확인용: 마지막으로 가린다고 본 것

        public BattleSeeThrough(Camera camera)
        {
            this.camera = camera;
            stockLit = Shader.Find("Universal Render Pipeline/Lit");
            stockUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            seeLit = Resources.Load<Shader>("HlrSeeThroughLit");
            seeUnlit = Resources.Load<Shader>("HlrSeeThroughUnlit");
            if (seeLit != null && !seeLit.isSupported) seeLit = null;
            if (seeUnlit != null && !seeUnlit.isSupported) seeUnlit = null;
            Release();
        }

        // 전투 화면을 떠날 때: 다른 화면 재질에 구멍이 남지 않게 끈다
        public void Release()
        {
            strength = 0f;
            Occluded = false;
            meshCache.Clear();
            Shader.SetGlobalVector(BId, Vector4.zero);
        }

        // 가리는 재질 훑기: 불투명 URP Lit/Unlit 재질을 투시 셰이더로 바꾸고, 그것을 쓰는 MeshRenderer를 모은다
        public void Refresh()
        {
            occluders.Clear();
            occluderMeshes.Clear();
            nextSweep = Time.unscaledTime + SweepSeconds;
            if (seeLit == null && seeUnlit == null) return;
            // Graphics.DrawMesh로 직접 그리는 것(등대 돌림대 등)은 Renderer가 없다 — 살아 있는 재질 전부를 훑어 바꾼다 (2초마다 한 번)
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>()) Convert(m);
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                renderer.GetSharedMaterials(materialBuffer);
                bool any = false;
                for (int i = 0; i < materialBuffer.Count; i++) any |= Convert(materialBuffer[i]);
                if (!any) continue;
                occluders.Add(renderer);
                Vector3 size = renderer.bounds.size;
                MeshFilter filter = Mathf.Max(size.x, size.z) > PreciseSize ? renderer.GetComponent<MeshFilter>() : null;
                occluderMeshes.Add(filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable ? filter : null);
            }
        }

        // 불투명이고 투시 셰이더(이미 또는 이번에 바꿈)면 true. 투명(빛무리 등)은 건드리지 않는다
        bool Convert(Material m)
        {
            if (m == null) return false;
            Shader s = m.shader;
            if (m.HasProperty(SurfaceId) && m.GetFloat(SurfaceId) > 0.5f) return false;
            if (s == null) return false;
            if (s == seeLit || s == seeUnlit) return true;
            Shader to = s == stockLit ? seeLit : s == stockUnlit ? seeUnlit : null;
            if (to == null) return false;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(m)) return false;   // 프로젝트 파일 재질은 바꾸지 않는다 (복사본만) — 편집기에서 파일이 더럽혀지지 않게
#endif
            int queue = m.renderQueue;   // 셰이더를 바꾸면 그리기 순서가 기본값으로 돌아가니 되돌린다 (투명 자르기 2450 등)
            m.shader = to;
            m.renderQueue = queue;
            return true;
        }

        // 한 프레임: feet = 주인공 그린 발 위치, height = 키(m), active = 구멍을 낼 화면인가(지도 등이면 false)
        public void Update(Vector3 feet, float height, float dt, bool active)
        {
            if (camera == null) return;
            if (Time.unscaledTime >= nextSweep) Refresh();

            Vector3 center = feet + Vector3.up * (height * BodyCenter);
            Vector3 view = camera.worldToCameraMatrix.MultiplyPoint3x4(center);
            float depth = -view.z;
            bool on = Enabled && active && depth > 0.2f;
            Occluded = on && IsOccluded(feet, center, height);
            strength = Mathf.MoveTowards(strength, Occluded ? 1f : 0f, FadeSeconds > 0f ? dt / FadeSeconds : 1f);
            if (!on || depth <= 0.2f)
            {
                strength = 0f;
                Shader.SetGlobalVector(BId, Vector4.zero);
                return;
            }

            Shader.SetGlobalVector(AId, new Vector4(view.x / depth, view.y / depth, RadiusPerHeight * height / depth, depth));
            Shader.SetGlobalVector(BId, new Vector4(strength, DepthMargin, feet.y + FloorMargin, InnerRatio));
        }

        // 사진기 → 발·가슴·머리 선이 가리는 것의 경계 상자를 지나는가 (주인공이 상자 안에 서 있는 것은 뺀다 — 큰 몸통 상자 오탐 방지, 셰이더가 깊이로 다시 가르므로 조금 넉넉해도 그림은 안 틀린다)
        bool IsOccluded(Vector3 feet, Vector3 center, float height)
        {
            Vector3 origin = camera.transform.position;
            Vector3 foot = feet + Vector3.up * 0.04f, head = feet + Vector3.up * height;
            for (int i = 0; i < occluders.Count; i++)
            {
                Renderer r = occluders[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                MeshFilter mf = occluderMeshes[i];
                if (mf == null && (b.Contains(center) || b.Contains(origin))) continue;   // 삼각형까지 보는 것은 경계 상자 안에 서 있어도 정확히 가른다
                if (!(Hits(b, origin, foot) || Hits(b, origin, center) || Hits(b, origin, head))) continue;
                if (mf != null && !(HitsMesh(mf, origin, foot) || HitsMesh(mf, origin, center) || HitsMesh(mf, origin, head))) continue;
                LastOccluder = r;
                return true;
            }

            LastOccluder = null;
            return false;
        }

        // 선분(사진기 → 목표)이 메시 삼각형을 지나는가 (메시 안쪽 좌표로 계산 — 비균등 크기여도 선분 위 위치 t는 그대로)
        bool HitsMesh(MeshFilter mf, Vector3 origin, Vector3 target)
        {
            Mesh mesh = mf.sharedMesh;
            if (!meshCache.TryGetValue(mesh, out var data))
            {
                data = (mesh.vertices, mesh.triangles);
                meshCache[mesh] = data;
            }

            Transform tr = mf.transform;
            Vector3 o = tr.InverseTransformPoint(origin), d = tr.InverseTransformPoint(target) - o;
            Vector3[] v = data.vertices;
            int[] tri = data.triangles;
            for (int i = 0; i + 2 < tri.Length; i += 3)
            {
                Vector3 v0 = v[tri[i]], e1 = v[tri[i + 1]] - v0, e2 = v[tri[i + 2]] - v0;
                Vector3 p = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, p);
                if (det > -1e-9f && det < 1e-9f) continue;
                float inv = 1f / det;
                Vector3 s = o - v0;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(d, q) * inv;
                if (w < 0f || u + w > 1f) continue;
                float t = Vector3.Dot(e2, q) * inv;
                if (t > 0f && t < 0.97f) return true;
            }

            return false;
        }

        static bool Hits(Bounds b, Vector3 origin, Vector3 target)
        {
            Vector3 d = target - origin;
            float length = d.magnitude;
            if (length < 1e-3f) return false;
            return b.IntersectRay(new Ray(origin, d / length), out float hit) && hit < length - 0.02f;
        }
    }
}
