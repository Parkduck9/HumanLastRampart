using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 주인공 둘레 투시 구멍 (W42, 사용자 2026-10-26 "건물이 가리면 주인공만 동그랗게 뚫어서 보이게")
    // - 구멍을 내는 것은 주인공을 가릴 수 있는 것만: 마당 건물·등대 탑·마당 소품(무기 걸이·수레)·길 등불 기둥·깃발·등대 돌림대. 병사·적·바닥·성벽·들판은 그대로 (사용자 결정)
    // - 그 그림들의 불투명 URP Lit/Unlit 재질을 전투 화면을 만들 때 한 번만 투시 셰이더(HlrSeeThroughLit/Unlit)로 바꿔 끼운다. 투명 자르기 견본(ClipLit/ClipUnlit)은 처음부터 투시 셰이더
    // - 구멍은 가리는 것이 있을 때만(0.15초에 걸쳐) 켜지고, 켜진 동안만 전역 키워드 HLR_SEE_THROUGH가 서서 셰이더가 조각을 버린다 → 꺼져 있으면 비용 0
    // - 가림 검사는 1초에 10번, 가슴 선부터 (큰 합친 메시는 삼각형까지). 추가 그리기·프레임마다 새로 만드는 것 없음
    // - 코드로 끄기: BattleSeeThrough.Enabled = false
    public sealed class BattleSeeThrough
    {
        public static bool Enabled = true;

        public const float FadeSeconds = 0.15f;      // 켜고 끄는 시간
        public const float RadiusPerHeight = 1.2f;   // 구멍 반지름 = 주인공 키 × 이 값 (화면에서, 발 조금 아래까지 덮는다)
        public const float InnerRatio = 0.55f;       // 반지름 안쪽 이만큼은 완전히 뚫리고 바깥은 디더로 옅어진다
        public const float DepthMargin = 0.08f;      // 주인공 깊이보다 이만큼(m) 더 가까운 것만 뚫는다 (옆에 붙은 것·같은 깊이는 그대로)
        public const float FloorMargin = 0.06f;      // 주인공 발 높이 + 이만큼 아래는 안 뚫는다 (통로·마당 돌바닥)
        public const float CheckSeconds = 0.1f;      // 가림 검사 간격 (1초에 10번)
        public const float PreciseSize = 6f;         // 경계 상자 한 변이 이보다 길면 삼각형까지 검사 (건물이 한 메시로 합쳐져 있다)
        const float BodyCenter = 0.45f;              // 구멍 중심 = 발 위 키의 이 비율
        public const string KeywordName = "HLR_SEE_THROUGH";

        static readonly int AId = Shader.PropertyToID("_HlrSeeThroughA");
        static readonly int BId = Shader.PropertyToID("_HlrSeeThroughB");
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static GlobalKeyword keyword;
        static bool keywordReady;
        static Shader stockLit, stockUnlit, seeLit, seeUnlit;

        readonly Camera camera;
        readonly List<Renderer> occluders = new List<Renderer>();
        readonly List<MeshFilter> occluderMeshes = new List<MeshFilter>();   // 큰 합친 메시만 (읽을 수 있고 비어 있지 않은 것). 작은 것은 null
        readonly Dictionary<Mesh, (Vector3[] vertices, int[] triangles)> meshCache = new Dictionary<Mesh, (Vector3[], int[])>();
        readonly List<Material> materialBuffer = new List<Material>(4);
        float strength;
        float sinceCheck = 999f;
        bool occluded;

        public float Strength => strength;          // 지금 켜진 정도 0~1
        public bool Occluded => occluded;           // 가리는 것이 있다 (1초에 10번 갱신)
        public int OccluderCount => occluders.Count;
        public Renderer LastOccluder { get; private set; }   // 확인용: 마지막으로 가린다고 본 것

        public BattleSeeThrough(Camera camera)
        {
            this.camera = camera;
            LoadShaders();
            Release();
        }

        static void LoadShaders()
        {
            if (!keywordReady) { keyword = GlobalKeyword.Create(KeywordName); keywordReady = true; }
            if (stockLit == null) stockLit = Shader.Find("Universal Render Pipeline/Lit");
            if (stockUnlit == null) stockUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (seeLit == null) seeLit = Resources.Load<Shader>("HlrSeeThroughLit");
            if (seeUnlit == null) seeUnlit = Resources.Load<Shader>("HlrSeeThroughUnlit");
            if (seeLit != null && !seeLit.isSupported) seeLit = null;
            if (seeUnlit != null && !seeUnlit.isSupported) seeUnlit = null;
        }

        // 전투 화면을 떠날 때: 다른 화면에 구멍이 남지 않게 끈다
        public void Release()
        {
            strength = 0f;
            occluded = false;
            meshCache.Clear();
            Off();
        }

        // 구멍을 낼 그림인가: 마당 건물·탑은 전부, 길은 등불 기둥·깃발만, 마당은 세운 소품(무기 걸이·화살 묶음·수레)만. 바닥·계단·성벽·들판은 아님
        public static bool IsOccluderName(string rootName, string rendererName)
        {
            if (rootName.StartsWith("Courtyard Buildings") || rootName.StartsWith("Beacon Tower")) return true;
            if (rootName.StartsWith("Beacon Road")) return rendererName.StartsWith("Road Lamp") || rendererName.StartsWith("Road Banner");
            if (rootName.StartsWith("Courtyard (")) return rendererName.StartsWith("Courtyard Weapon") || rendererName.StartsWith("Courtyard Arrow") || rendererName.StartsWith("Courtyard Cart");
            return false;
        }

        // 전투 화면을 다 만든 뒤 한 번: root 아래에서 구멍을 낼 그림을 골라 재질을 바꾸고 가림 검사 목록에 올린다
        public void AdoptScene(Transform root)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Transform parent = renderer.transform.parent;
                if (parent != null && IsOccluderName(parent.name, renderer.name)) Adopt(renderer);
            }
        }

        // 그림 하나를 구멍 낼 대상으로: 불투명 URP Lit/Unlit 재질을 투시 셰이더로 바꾸고 가림 검사 목록에 올린다
        public void Adopt(MeshRenderer renderer)
        {
            renderer.GetSharedMaterials(materialBuffer);
            bool any = false;
            for (int i = 0; i < materialBuffer.Count; i++) any |= Adopt(materialBuffer[i]);
            if (!any) return;
            occluders.Add(renderer);
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Vector3 size = renderer.bounds.size;
            if (mesh != null && mesh.isReadable && mesh.vertexCount > 0 && Mathf.Max(size.x, size.z) > PreciseSize)
            {
                if (!meshCache.ContainsKey(mesh)) meshCache[mesh] = (mesh.vertices, mesh.triangles);   // 한 번만 (프레임 중에 만들지 않는다)
                occluderMeshes.Add(filter);
            }
            else occluderMeshes.Add(null);
        }

        // Graphics.DrawMesh로 직접 그리는 것(등대 돌림대)은 그리는 쪽이 재질을 만든 직후 이것을 부른다. 목록에는 안 올라가고 구멍만 난다
        public static bool Adopt(Material m)
        {
            if (m == null) return false;
            LoadShaders();
            Shader s = m.shader;
            if (m.HasProperty(SurfaceId) && m.GetFloat(SurfaceId) > 0.5f) return false;   // 투명(빛무리 등)은 안 건드린다
            if (s == null) return false;
            if (s == seeLit || s == seeUnlit) return true;
            Shader to = s == stockLit ? seeLit : s == stockUnlit ? seeUnlit : null;
            if (to == null) return false;
            int queue = m.renderQueue;   // 셰이더를 바꾸면 그리기 순서가 기본값으로 돌아가니 되돌린다 (투명 자르기 2450 등)
            m.shader = to;
            m.renderQueue = queue;
            return true;
        }

        // 한 프레임: feet = 주인공 그린 발 위치, height = 키(m), active = 구멍을 낼 화면인가(지도 등이면 false)
        public void Update(Vector3 feet, float height, float dt, bool active)
        {
            if (camera == null) return;
            Vector3 center = feet + Vector3.up * (height * BodyCenter);
            Vector3 view = camera.worldToCameraMatrix.MultiplyPoint3x4(center);
            float depth = -view.z;
            bool on = Enabled && active && depth > 0.2f;
            if (!on)
            {
                occluded = false;
                sinceCheck = 999f;   // 다시 켜지면 바로 검사
                if (strength > 0f || Shader.IsKeywordEnabled(keyword)) Off();
                return;
            }

            sinceCheck += dt;
            if (sinceCheck >= CheckSeconds)
            {
                sinceCheck = 0f;
                occluded = IsOccluded(feet, center, height);
            }

            strength = Mathf.MoveTowards(strength, occluded ? 1f : 0f, FadeSeconds > 0f ? dt / FadeSeconds : 1f);
            if (strength <= 0f)
            {
                if (Shader.IsKeywordEnabled(keyword)) Off();
                return;
            }

            Shader.SetKeyword(keyword, true);
            Shader.SetGlobalVector(AId, new Vector4(view.x / depth, view.y / depth, RadiusPerHeight * height / depth, depth));
            Shader.SetGlobalVector(BId, new Vector4(strength, DepthMargin, feet.y + FloorMargin, InnerRatio));
        }

        void Off()
        {
            if (!keywordReady) LoadShaders();
            strength = 0f;
            Shader.SetKeyword(keyword, false);
            Shader.SetGlobalVector(BId, Vector4.zero);
        }

        // 사진기 → 가슴·발·머리 선이 가리는 것의 경계 상자를 지나는가 (큰 합친 메시는 삼각형까지). 주인공이 상자 안에 서 있는 작은 것은 뺀다
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
                if (!(Hits(b, origin, center) || Hits(b, origin, foot) || Hits(b, origin, head))) continue;
                if (mf != null && !(HitsMesh(mf, origin, center) || HitsMesh(mf, origin, foot) || HitsMesh(mf, origin, head))) continue;
                LastOccluder = r;
                return true;
            }

            LastOccluder = null;
            return false;
        }

        // 선분(사진기 → 목표)이 메시 삼각형을 지나는가 (메시 안쪽 좌표로 계산 — 비균등 크기여도 선분 위 위치 t는 그대로)
        bool HitsMesh(MeshFilter mf, Vector3 origin, Vector3 target)
        {
            if (mf == null) return false;
            Mesh mesh = mf.sharedMesh;
            if (mesh == null || !meshCache.TryGetValue(mesh, out var data)) return false;   // 지워졌거나 모르는 메시는 건너뜀

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
