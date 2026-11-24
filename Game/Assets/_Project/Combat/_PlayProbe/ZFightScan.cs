using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 깊이 겹침(z-fighting) 점검 (W41): 장면의 모든 보이는 메시에서 '위를 보는 평평한 삼각형'을 모아,
// 가로(XZ)로 겹치면서 높이(Y) 차가 MinSeparation 미만인 쌍을 찾는다. 겹친 곳이 더 높은 다른 윗면에 완전히 덮여 있으면 '숨은 겹침'(안 보임),
// 아니면 '보이는 겹침'(깜박임). 시험은 보이는 겹침이 0개임을 약속한다
// 검사 범위: MeshRenderer의 위를 보는 평평한 삼각형만. 묶음 그리기(땅 번짐 GroundGlow 판)·직접 그리기 호출·세로 면(성벽 바깥면 덮개 등)은 안 본다.
//   땅 번짐 판과 데칼 높이는 BattleRoadTests·BattleCourtyardTests 단위 시험이 지킨다
public static class ZFightScan
{
    // 이보다 가까운 높이 차는 깊이 정밀도 문제로 본다. 층 간격은 1cm 이상 (근접면 0.1m 기준 해상도는 40m에서 약 1mm). 1cm 층이 통과하도록 0.9cm
    public const float MinSeparation = 0.009f;
    const float FlatNormalY = 0.999f;
    const float MinArea = 1e-3f;       // 이보다 작은 겹침(모서리 맞닿음)은 무시 (m²)
    const float Cell = 2f;
    const float MaxExtent = 100f;      // 이보다 큰 삼각형(들판 바닥)은 검사 영역 안쪽만 칸에 넣는다

    public struct Finding
    {
        public string A, B;
        public float Dy;               // B - A (m)
        public float VisibleArea, HiddenArea;
        public Vector3 Where;          // 겹친 곳 한 점
    }

    struct Tri
    {
        public Vector2 P0, P1, P2;
        public float Y;
        public int Owner;
        public bool NoZWrite;          // 깊이를 안 쓰는 반투명: 둘이 겹쳐도 그리는 순서로 섞일 뿐 깜박이지 않는다
        public bool NoOccluder;        // 잘라내기·반투명·깊이 안 씀: 아래 면을 완전히 가리지 못한다
        public Vector2 Min, Max;
    }

    public static List<Finding> Scan(float areaHalf, StringBuilder stats = null)
    {
        var tris = new List<Tri>();
        var names = new List<string>();
        var grid = new Dictionary<long, List<int>>();

        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            int owner = names.Count;
            names.Add(PathOf(mr.transform));
            Mesh mesh = mf.sharedMesh;
            Matrix4x4 m = mr.transform.localToWorldMatrix;
            var verts = mesh.vertices;
            var mats = mr.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                Material mat = mats.Length == 0 ? null : mats[Mathf.Min(sub, mats.Length - 1)];   // 부분 메시마다 자기 재질
                bool noZ = mat != null && (mat.HasProperty("_ZWrite") ? mat.GetFloat("_ZWrite") < 0.5f : mat.renderQueue >= 3000);
                // 가림 판정에서 뺀다: 깊이를 안 쓰는 면, 잘라내기·반투명 면 (구멍 사이로 아래가 보인다)
                bool noOccluder = noZ || mat != null && (mat.renderQueue >= 2450 || mat.IsKeywordEnabled("_ALPHATEST_ON") || mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f);
                var idx = mesh.GetTriangles(sub);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(verts[idx[i]]), b = m.MultiplyPoint3x4(verts[idx[i + 1]]), c = m.MultiplyPoint3x4(verts[idx[i + 2]]);
                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    float len = nrm.magnitude;
                    if (len < 1e-8f) continue;
                    // 위를 보는 평평한 삼각형만 (세 점 높이가 같은 것). 유니티는 시계 방향이 앞면이라 앞면의 외적이 바깥(위)을 향한다
                    if (nrm.y / len < FlatNormalY) continue;
                    if (Mathf.Abs(a.y - b.y) > 1e-3f || Mathf.Abs(a.y - c.y) > 1e-3f) continue;
                    var t = new Tri { P0 = new Vector2(a.x, a.z), P1 = new Vector2(b.x, b.z), P2 = new Vector2(c.x, c.z), Y = (a.y + b.y + c.y) / 3f, Owner = owner, NoZWrite = noZ, NoOccluder = noOccluder };
                    t.Min = Vector2.Min(t.P0, Vector2.Min(t.P1, t.P2));
                    t.Max = Vector2.Max(t.P0, Vector2.Max(t.P1, t.P2));
                    if (t.Max.x < -areaHalf || t.Min.x > areaHalf || t.Max.y < -areaHalf || t.Min.y > areaHalf) continue;
                    int id = tris.Count;
                    tris.Add(t);
                    int x0 = Mathf.FloorToInt(Mathf.Max(t.Min.x, -areaHalf) / Cell), x1 = Mathf.FloorToInt(Mathf.Min(t.Max.x, areaHalf) / Cell);
                    int z0 = Mathf.FloorToInt(Mathf.Max(t.Min.y, -areaHalf) / Cell), z1 = Mathf.FloorToInt(Mathf.Min(t.Max.y, areaHalf) / Cell);
                    for (int cx = x0; cx <= x1; cx++)
                        for (int cz = z0; cz <= z1; cz++)
                        {
                            long key = ((long)cx << 32) ^ (uint)cz;
                            if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                            list.Add(id);
                        }
                }
            }
        }

        stats?.AppendLine($"위 평면 삼각형 {tris.Count}개, 메시 {names.Count}개, 칸 {grid.Count}개");
        var seen = new HashSet<long>();
        var byPair = new Dictionary<long, Finding>();
        foreach (var kv in grid)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    int ia = list[i], ib = list[j];
                    if (ia > ib) { int s = ia; ia = ib; ib = s; }
                    Tri ta = tris[ia], tb = tris[ib];
                    if (Mathf.Abs(ta.Y - tb.Y) >= MinSeparation) continue;
                    if (ta.NoZWrite && tb.NoZWrite) continue;
                    if (ta.Max.x <= tb.Min.x || tb.Max.x <= ta.Min.x || ta.Max.y <= tb.Min.y || tb.Max.y <= ta.Min.y) continue;
                    long pairKey = ((long)ia << 32) | (uint)ib;
                    if (!seen.Add(pairKey)) continue;
                    var poly = Clip(ta, tb);
                    float area = Area(poly);
                    if (area < MinArea) continue;
                    bool hidden = IsCovered(poly, Mathf.Max(ta.Y, tb.Y) + MinSeparation, tris, grid);
                    long ownerKey = ((long)Mathf.Min(ta.Owner, tb.Owner) << 32) | (uint)Mathf.Max(ta.Owner, tb.Owner);
                    byPair.TryGetValue(ownerKey, out Finding f);
                    if (f.A == null)
                    {
                        Tri lo = ta.Owner <= tb.Owner ? ta : tb, hi = ta.Owner <= tb.Owner ? tb : ta;
                        f = new Finding { A = names[lo.Owner], B = names[hi.Owner], Dy = hi.Y - lo.Y, Where = new Vector3(Centroid(poly).x, ta.Y, Centroid(poly).y) };
                    }

                    if (hidden) f.HiddenArea += area; else f.VisibleArea += area;
                    byPair[ownerKey] = f;
                }
        }

        return new List<Finding>(byPair.Values);
    }

    // ── W47: 위를 보지 않는 면(세로 면·기울어진 면)의 겹침 ──
    // 같은 방향을 보고 같은 평면(거리 차 MinSeparation 미만)에 놓이며 평면 위에서 겹치는 삼각형 쌍. 가려졌는지는 가르지 않는다 (이름으로 판단)
    struct PlaneTri
    {
        public Vector3 A, B, C, N;
        public float D;
        public int Owner;
        public bool NoZWrite;
        public Vector3 Min, Max;
    }

    public static List<Finding> ScanAnyOrientation(float areaHalf, StringBuilder stats = null)
    {
        var tris = new List<PlaneTri>();
        var names = new List<string>();
        var grid = new Dictionary<long, List<int>>();
        const float yMin = -5f, yMax = 40f;
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            int owner = names.Count;
            names.Add(PathOf(mr.transform));
            Mesh mesh = mf.sharedMesh;
            Matrix4x4 m = mr.transform.localToWorldMatrix;
            var verts = mesh.vertices;
            var mats = mr.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                Material mat = mats.Length == 0 ? null : mats[Mathf.Min(sub, mats.Length - 1)];
                bool noZ = mat != null && (mat.HasProperty("_ZWrite") ? mat.GetFloat("_ZWrite") < 0.5f : mat.renderQueue >= 3000);
                var idx = mesh.GetTriangles(sub);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(verts[idx[i]]), b = m.MultiplyPoint3x4(verts[idx[i + 1]]), c = m.MultiplyPoint3x4(verts[idx[i + 2]]);
                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    float len = nrm.magnitude;
                    if (len < 1e-6f) continue;
                    nrm /= len;
                    if (Mathf.Abs(nrm.y) >= FlatNormalY) continue;   // 위·아래를 보는 평평한 면은 Scan이 본다
                    var t = new PlaneTri { A = a, B = b, C = c, N = nrm, D = Vector3.Dot(nrm, a), Owner = owner, NoZWrite = noZ };
                    t.Min = Vector3.Min(a, Vector3.Min(b, c));
                    t.Max = Vector3.Max(a, Vector3.Max(b, c));
                    if (t.Max.x < -areaHalf || t.Min.x > areaHalf || t.Max.z < -areaHalf || t.Min.z > areaHalf || t.Max.y < yMin || t.Min.y > yMax) continue;
                    int id = tris.Count;
                    tris.Add(t);
                    int x0 = Mathf.FloorToInt(Mathf.Max(t.Min.x, -areaHalf) / Cell), x1 = Mathf.FloorToInt(Mathf.Min(t.Max.x, areaHalf) / Cell);
                    int y0 = Mathf.FloorToInt(Mathf.Max(t.Min.y, yMin) / Cell), y1 = Mathf.FloorToInt(Mathf.Min(t.Max.y, yMax) / Cell);
                    int z0 = Mathf.FloorToInt(Mathf.Max(t.Min.z, -areaHalf) / Cell), z1 = Mathf.FloorToInt(Mathf.Min(t.Max.z, areaHalf) / Cell);
                    for (int cx = x0; cx <= x1; cx++)
                        for (int cy = y0; cy <= y1; cy++)
                            for (int cz = z0; cz <= z1; cz++)
                            {
                                long key = ((long)(cx + 512) << 40) ^ ((long)(cy + 512) << 20) ^ (long)(cz + 512);
                                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                                list.Add(id);
                            }
                }
            }
        }

        stats?.AppendLine($"기울어진·세로 삼각형 {tris.Count}개, 메시 {names.Count}개, 칸 {grid.Count}개");
        var seen = new HashSet<long>();
        var byPair = new Dictionary<long, Finding>();
        foreach (var kv in grid)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    int ia = list[i], ib = list[j];
                    if (ia > ib) { int s = ia; ia = ib; ib = s; }
                    PlaneTri ta = tris[ia], tb = tris[ib];
                    if (Vector3.Dot(ta.N, tb.N) < 0.9999f) continue;
                    if (Mathf.Abs(ta.D - Vector3.Dot(ta.N, tb.A)) >= MinSeparation) continue;
                    if (Mathf.Abs(ta.D - Vector3.Dot(ta.N, tb.B)) >= MinSeparation || Mathf.Abs(ta.D - Vector3.Dot(ta.N, tb.C)) >= MinSeparation) continue;
                    if (ta.NoZWrite && tb.NoZWrite) continue;
                    if (ta.Max.x <= tb.Min.x || tb.Max.x <= ta.Min.x || ta.Max.y <= tb.Min.y || tb.Max.y <= ta.Min.y || ta.Max.z <= tb.Min.z || tb.Max.z <= ta.Min.z) continue;
                    long pairKey = ((long)ia << 32) | (uint)ib;
                    if (!seen.Add(pairKey)) continue;
                    // 평면 위 두 축으로 눌러 겹침 넓이를 잰다
                    Vector3 u = Vector3.Cross(ta.N, Mathf.Abs(ta.N.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(ta.N, u);
                    Tri pa = Flatten(ta, u, v), pb = Flatten(tb, u, v);
                    var poly = Clip(pa, pb);
                    float area = Area(poly);
                    if (area < MinArea) continue;
                    long ownerKey = ((long)Mathf.Min(ta.Owner, tb.Owner) << 32) | (uint)Mathf.Max(ta.Owner, tb.Owner);
                    byPair.TryGetValue(ownerKey, out Finding f);
                    if (f.A == null)
                    {
                        Vector2 cp = Centroid(poly);
                        f = new Finding { A = names[Mathf.Min(ta.Owner, tb.Owner)], B = names[Mathf.Max(ta.Owner, tb.Owner)], Dy = Vector3.Dot(ta.N, tb.A) - ta.D, Where = ta.A + u * (cp.x - pa.P0.x) + v * (cp.y - pa.P0.y) };
                    }

                    f.VisibleArea += area;
                    byPair[ownerKey] = f;
                }
        }

        return new List<Finding>(byPair.Values);
    }

    static Tri Flatten(PlaneTri t, Vector3 u, Vector3 v) => new Tri
    {
        P0 = new Vector2(Vector3.Dot(t.A, u), Vector3.Dot(t.A, v)),
        P1 = new Vector2(Vector3.Dot(t.B, u), Vector3.Dot(t.B, v)),
        P2 = new Vector2(Vector3.Dot(t.C, u), Vector3.Dot(t.C, v)),
    };

    // ── W47: 세운 그림(병사·장교·대포 운용병·주인공 등)끼리 같은 깊이에 겹치는가 ──
    // 세운 그림은 사진기를 향한 세로 판이라(PerfLabSprite) 평면은 '사진기 앞쪽 수평 방향 깊이'로 정해진다. 깊이 차가 MinSeparation 미만이고
    // 좌우로 겹치면(폭 합의 절반 안) 같은 평면 겹침 — 사진기가 움직이면 어느 쪽이 이기는지 뒤바뀐다.
    // items: xyz = 발 위치, w = 높이(m). widthPerHeight = 판 폭/높이 (대략)
    public static List<(int A, int B, float Depth, float Lateral)> ScanBillboards(Camera cam, IList<Vector4> items, float widthPerHeight = 0.9f)
    {
        var found = new List<(int, int, float, float)>();
        Vector3 f = cam.transform.forward; f.y = 0f; f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, f);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].w <= 0f) continue;
            for (int j = i + 1; j < items.Count; j++)
            {
                if (items[j].w <= 0f) continue;
                Vector3 d = new Vector3(items[j].x - items[i].x, 0f, items[j].z - items[i].z);
                float depth = Mathf.Abs(Vector3.Dot(d, f)), lateral = Mathf.Abs(Vector3.Dot(d, right));
                if (depth >= MinSeparation) continue;
                if (lateral >= (items[i].w + items[j].w) * 0.5f * widthPerHeight * 0.9f) continue;
                found.Add((i, j, depth, lateral));
            }
        }

        return found;
    }

    static string PathOf(Transform t)
    {
        string n = t.name;
        if (t.parent != null) n = t.parent.name + "/" + n;
        return n;
    }

    // 두 삼각형(위에서 본 모양)의 겹친 볼록 다각형
    static List<Vector2> Clip(Tri subject, Tri clip)
    {
        var poly = new List<Vector2> { subject.P0, subject.P1, subject.P2 };
        var c = new[] { clip.P0, clip.P1, clip.P2 };
        float orient = Cross(c[1] - c[0], c[2] - c[0]) >= 0f ? 1f : -1f;
        for (int e = 0; e < 3 && poly.Count > 0; e++)
        {
            Vector2 p = c[e], q = c[(e + 1) % 3];
            var input = poly;
            poly = new List<Vector2>();
            for (int k = 0; k < input.Count; k++)
            {
                Vector2 cur = input[k], prev = input[(k + input.Count - 1) % input.Count];
                float sc = orient * Cross(q - p, cur - p), sp = orient * Cross(q - p, prev - p);
                if (sc >= 0f)
                {
                    if (sp < 0f) poly.Add(Intersect(prev, cur, sp, sc));
                    poly.Add(cur);
                }
                else if (sp >= 0f) poly.Add(Intersect(prev, cur, sp, sc));
            }
        }

        return poly;
    }

    static Vector2 Intersect(Vector2 a, Vector2 b, float sa, float sb) => a + (b - a) * (sa / (sa - sb));
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static float Area(List<Vector2> poly)
    {
        float s = 0f;
        for (int i = 0; i < poly.Count; i++) s += Cross(poly[i], poly[(i + 1) % poly.Count]);
        return Mathf.Abs(s) * 0.5f;
    }

    static Vector2 Centroid(List<Vector2> poly)
    {
        Vector2 s = Vector2.zero;
        foreach (Vector2 p in poly) s += p;
        return poly.Count > 0 ? s / poly.Count : Vector2.zero;
    }

    // 겹친 다각형의 몇 점(가운데 + 꼭짓점을 가운데로 당긴 점)이 모두 minY보다 높은 다른 윗면에 덮였는가
    static bool IsCovered(List<Vector2> poly, float minY, List<Tri> tris, Dictionary<long, List<int>> grid)
    {
        Vector2 c = Centroid(poly);
        if (!CoveredAt(c, minY, tris, grid)) return false;
        foreach (Vector2 v in poly)
            if (!CoveredAt(Vector2.Lerp(v, c, 0.15f), minY, tris, grid)) return false;
        return true;
    }

    static bool CoveredAt(Vector2 p, float minY, List<Tri> tris, Dictionary<long, List<int>> grid)
    {
        long key = ((long)Mathf.FloorToInt(p.x / Cell) << 32) ^ (uint)Mathf.FloorToInt(p.y / Cell);
        if (!grid.TryGetValue(key, out var list)) return false;
        foreach (int id in list)
        {
            Tri t = tris[id];
            if (t.Y < minY || t.NoOccluder) continue;
            if (InTri(p, t)) return true;
        }

        return false;
    }

    static bool InTri(Vector2 p, Tri t)
    {
        float d1 = Cross(t.P1 - t.P0, p - t.P0), d2 = Cross(t.P2 - t.P1, p - t.P1), d3 = Cross(t.P0 - t.P2, p - t.P2);
        bool neg = d1 < -1e-5f || d2 < -1e-5f || d3 < -1e-5f, pos = d1 > 1e-5f || d2 > 1e-5f || d3 > 1e-5f;
        return !(neg && pos);
    }
}
