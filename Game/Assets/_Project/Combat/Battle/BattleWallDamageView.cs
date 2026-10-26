using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽 부서짐 그림 얹기 (W39, Codex K-126): 단계(WallDamageRules)가 오를 때 면마다
    //  · 돌출부(여장)를 금간·반파·잔단 그림으로 바꾸고 (멀쩡한 3D 돌은 숨김)
    //  · 외벽 바깥면에 균열·깊은 흠 데칼, 3단계는 파공(어두운 구멍 + 테두리)을 얹고
    //  · 성벽 발치(바깥)와 통로 위에 잔해 더미를 놓고
    //  · 단계가 바뀌는 순간 먼지(6칸 10fps 한 번)를 피우고, 밤에는 심하게 부서진 곳에 작은 연기·불꽃(6칸 8fps 반복)을 놓는다
    // - 정적 그림(돌출부·잔해·데칼)은 그림판 한 장(실행 중 합침)에 모아 메시 하나(+외벽 데칼·파공 구멍 메시 각 하나) = 그리기 3번. 단계가 바뀔 때만 다시 쌓는다
    // - 그림은 처음 손상이 생길 때 한 번 읽는다 (멀쩡한 전투는 메모리·시간 0). 하나라도 없으면 그림 없음 — 재질 바꿈(K-024)만 남고 옛 돌출부 그대로
    // - 배치는 전투 씨앗으로만 정한다(WallDamageRules). 새 빛 없음 — 불꽃은 제 색(밝기 없는 재질)만
    // - 크기: 돌출부 그림 폭 35화소 = 돌출부 폭, 같은 배율(m/화소)을 잔해·먼지·불꽃에 쓴다. 외벽 데칼은 4m × 4m = 128화소 (32 PPU)
    public sealed class BattleWallDamageView : IDisposable
    {
        public delegate Texture2D TextureLoader(string name);

        const string Folder = "BattleArt/WallDamage/";
        const int CellW = 96, CellH = 128, FootY = 120;          // 소품 칸 (원본 32 PPU), 발 (48, 120)
        const int MerlonX = 30, MerlonY = 80, MerlonW = 35, MerlonH = 40;   // 여장 몸통이 차지하는 칸 안 상자
        const int Frames = 6;
        const float DustFps = 10f, FlameFps = 8f;
        const float TiltDegrees = 40f;       // 잔해 판을 뒤로 눕힌 각도 — 위에서 내려다보아도 읽힌다
        const float DecalLift = 0.03f, VoidLift = 0.04f, RimLift = 0.05f;   // 외벽 면에서 띄움 (덮은 판 1cm 위)
        const int DustPool = 6;
        const float BreachScale = 1f / 3f;

        // 그림판에 넣는 순서
        const int IMerlonChipped = 0, IMerlonHalf = 1, IMerlonStub = 2, IRubbleFirst = 3;   // 잔해 18장: 3 + (field? 0 : 9) + size*3 + variant
        const int ICracks = 21, IGouges = 22, IRim = 23, ICut = 24, IDust = 25, IFire = 26, ISmoke = 27, SourceCount = 28;

        static readonly string[] SizeNames = { "small", "medium", "large" };

        public static string SourceName(int index)
        {
            switch (index)
            {
                case IMerlonChipped: return "merlon_chipped";
                case IMerlonHalf: return "merlon_half_broken";
                case IMerlonStub: return "merlon_stub";
                case ICracks: return "wall_stage1_cracks";
                case IGouges: return "wall_stage2_gouges";
                case IRim: return "wall_stage3_breach_rim";
                case ICut: return "wall_stage3_cut_mask";
                case IDust: return "dust_6f";
                case IFire: return "fire_6f";
                case ISmoke: return "smoke_6f";
                default:
                    int r = index - IRubbleFirst;
                    return $"rubble_{(r < 9 ? "field" : "walkway")}_{SizeNames[(r % 9) / 3]}_{r % 3 + 1}";
            }
        }

        sealed class Effect
        {
            public GameObject Object;
            public Mesh Mesh;
            public Vector2[] Uv = new Vector2[4];
            public int Frame = -1;
            public int Side;
            public int MinStage;
            public bool Fire;
            public float Start;     // 먼지 시작 시간
            public bool Active;
        }

        readonly Transform root;
        readonly uint seed;
        readonly float half, top;
        readonly BattleWorldView.Proportion p;
        readonly TextureLoader loader;
        readonly float mpp;                                   // 1화소 = 몇 m
        readonly int[] stage = new int[4];
        readonly List<(Renderer renderer, float along)>[] merlons = new List<(Renderer, float)>[4];
        readonly List<WallDamageRules.RubblePiece>[] rubble = new List<WallDamageRules.RubblePiece>[4];
        readonly List<WallDamageRules.Spot>[] spots = new List<WallDamageRules.Spot>[4];
        readonly float from, to;

        bool artTried, hasArt, night;
        Texture2D atlas, breachTexture;
        Texture2D[] sources;
        Rect[] rects;
        Material litMaterial, unlitMaterial, voidMaterial, decalMaterial;
        GameObject staticObject, voidObject, decalObject;
        Mesh staticMesh, voidMesh, decalMesh;
        readonly List<Effect> effects = new List<Effect>();   // 먼지 풀 + 면마다 불꽃·연기 자리
        readonly Effect[] dust = new Effect[DustPool];
        int dustNext;
        int rebuilds;

        public bool HasArt => hasArt;
        public bool Night => night;
        public int Rebuilds => rebuilds;
        public int StageOf(int side) => side >= 0 && side < 4 ? stage[side] : 0;
        public int StaticDrawCalls => (staticObject != null && staticMesh != null && staticMesh.vertexCount > 0 ? 1 : 0) + (voidObject != null && voidMesh != null && voidMesh.vertexCount > 0 ? 1 : 0) + (decalObject != null && decalMesh != null && decalMesh.vertexCount > 0 ? 1 : 0);

        // 확인용: 지금 단계에서 보이는 잔해 수 / 숨겨진 3D 돌출부 수 / 돌출부 상태
        public int VisibleRubble(int side)
        {
            int n = 0;
            foreach (var piece in rubble[side]) if (piece.MinStage <= stage[side]) n++;
            return n;
        }

        public int RubbleTotal(int side) => rubble[side].Count;
        public IReadOnlyList<WallDamageRules.RubblePiece> RubblePieces(int side) => rubble[side];
        public IReadOnlyList<WallDamageRules.Spot> SpotList(int side) => spots[side];
        public int MerlonCount(int side) => merlons[side].Count;
        public WallDamageRules.MerlonState MerlonStateOf(int side, int index) => WallDamageRules.MerlonAt(WallDamageRules.MerlonHash(seed, side, index), stage[side]);
        public bool MerlonBoxVisible(int side, int index) => merlons[side][index].renderer != null && merlons[side][index].renderer.enabled;
        public int ActiveDust { get { int n = 0; foreach (Effect e in dust) if (e != null && e.Active) n++; return n; } }
        public int ActiveSpots { get { int n = 0; foreach (Effect e in effects) if (e.Object != null && e.MinStage > 0 && e.Active) n++; return n; } }

        public BattleWallDamageView(Transform parent, uint seed, float castleHalf, BattleWorldView.Proportion proportion,
            List<(Renderer renderer, float along)>[] merlonPieces, TextureLoader loader = null)
        {
            root = new GameObject("Battle Wall Damage").transform;
            root.SetParent(parent, false);
            this.seed = seed;
            half = castleHalf;
            top = BattleSim.WallHeight;
            p = proportion;
            this.loader = loader ?? (name => Resources.Load<Texture2D>(Folder + name));
            mpp = p.MerlonWidth / MerlonW;
            from = BattleWallLayout.From(half);
            to = BattleWallLayout.To(half);
            var along = new List<float>();
            for (int s = 0; s < 4; s++)
            {
                merlons[s] = merlonPieces != null && merlonPieces[s] != null ? merlonPieces[s] : new List<(Renderer, float)>();
                along.Clear();
                foreach (var (_, m) in merlons[s]) along.Add(m);
                rubble[s] = new List<WallDamageRules.RubblePiece>();
                spots[s] = new List<WallDamageRules.Spot>();
                WallDamageRules.Place(seed, s, from, to, along, rubble[s], spots[s]);
            }
        }

        // 면의 단계가 바뀜 (BattleWorldView.SetWallHealth). silent = 처음 받은 값이라 먼지를 피우지 않는다
        public void SetStage(int side, int newStage, bool silent)
        {
            if (side < 0 || side >= 4) return;
            newStage = Mathf.Clamp(newStage, 0, WallDamageRules.MaxStage);
            int old = stage[side];
            if (newStage == old) return;
            stage[side] = newStage;
            if (newStage > 0) EnsureArt();
            if (!hasArt) return;

            // 돌출부: 멀쩡한 3D 돌은 상태가 Intact일 때만 보인다 (나머지는 그림이 대신)
            for (int k = 0; k < merlons[side].Count; k++)
            {
                Renderer r = merlons[side][k].renderer;
                if (r != null) r.enabled = MerlonStateOf(side, k) == WallDamageRules.MerlonState.Intact;
            }

            Rebuild();
            RefreshSpots();
            if (!silent && newStage > old) SpawnDust(side, newStage);
        }

        public void SetNight(bool value)
        {
            if (night == value) return;
            night = value;
            RefreshSpots();
        }

        // ── 그림 읽기·그림판 ──
        void EnsureArt()
        {
            if (artTried) return;
            artTried = true;
            sources = new Texture2D[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                sources[i] = loader(SourceName(i));
                if (sources[i] == null || !sources[i].isReadable) { sources = null; return; }   // 하나라도 없으면 그림 없음 (옛 모습 그대로)
            }

            Material lit = ClipMaterials.LitCopy("Wall Damage Lit (runtime)");
            Material unlit = ClipMaterials.UnlitCopy("Wall Damage Fire (runtime)");
            if (lit == null || unlit == null) { Kill(lit); Kill(unlit); sources = null; return; }

            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Wall Damage Atlas (runtime)" };
            rects = atlas.PackTextures(sources, 2, 2048, true);   // 합친 뒤 시스템 메모리 사본은 버린다
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;
            litMaterial = lit; unlitMaterial = unlit;
            foreach (Material m in new[] { litMaterial, unlitMaterial })
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", atlas);
                m.mainTexture = atlas;
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            litMaterial.SetFloat("_Smoothness", 0.05f);
            // 외벽 데칼: 그림의 밝은 가장자리(224)가 밤 성벽 위에서 흰 선처럼 튀지 않게 어둡게 곱한다 — 어두운 금 + 은은한 빛 가장자리로 읽힌다 (W39 사진 검토)
            decalMaterial = new Material(litMaterial) { name = "Wall Damage Decals (runtime)" };
            Color decalTint = new Color(0.5f, 0.48f, 0.46f, 1f);
            if (decalMaterial.HasProperty("_BaseColor")) decalMaterial.SetColor("_BaseColor", decalTint);
            if (decalMaterial.HasProperty("_Color")) decalMaterial.SetColor("_Color", decalTint);
            voidMaterial = new Material(litMaterial) { name = "Wall Breach Void (runtime)" };
            // 파공 안쪽: 까만 판 대신 어두운 돌(벽돌 줄눈·가장자리로 갈수록 더 어둡게)을 마스크 모양으로 구워 쓴다 — 움푹 들어간 모습 (W39 사진 검토)
            breachTexture = BuildBreachTexture(sources[ICut]);
            if (voidMaterial.HasProperty("_BaseMap")) voidMaterial.SetTexture("_BaseMap", breachTexture);
            voidMaterial.mainTexture = breachTexture;
            if (voidMaterial.HasProperty("_Smoothness")) voidMaterial.SetFloat("_Smoothness", 0f);

            staticObject = MakeObject("Wall Damage Sprites", litMaterial, out staticMesh);
            voidObject = MakeObject("Wall Breach Void", voidMaterial, out voidMesh);
            decalObject = MakeObject("Wall Damage Decals", decalMaterial, out decalMesh);
            BuildEffects();
            hasArt = true;
        }

        GameObject MakeObject(string name, Material material, out Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            mesh = new Mesh { name = name };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // 칸 안 화소 상자 → 그림판 UV (위쪽 왼쪽 원점, 가장자리에서 아주 조금 안쪽)
        Rect Sub(int index, int x, int y, int w, int h)
        {
            const float inset = 0.02f;
            Texture2D t = sources[index];
            Rect r = rects[index];
            float u0 = (x + inset) / t.width, u1 = (x + w - inset) / t.width;
            float v0 = (t.height - y - h + inset) / t.height, v1 = (t.height - y - inset) / t.height;
            return Rect.MinMaxRect(r.xMin + r.width * u0, r.yMin + r.height * v0, r.xMin + r.width * u1, r.yMin + r.height * v1);
        }

        Rect Frame(int index, int frame) => Sub(index, frame * CellW, 0, CellW, CellH);

        // ── 정적 메시 다시 쌓기 (단계가 바뀔 때만) ──
        static readonly Vector3 Up = Vector3.up;

        void Rebuild()
        {
            rebuilds++;
            var main = new MeshKit();
            var hole = new MeshKit();
            var deco = new MeshKit();
            float tilt = TiltDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(tilt), sin = Mathf.Sin(tilt);
            for (int side = 0; side < 4; side++)
            {
                int st = stage[side];
                if (st <= 0) continue;
                Vector3 n = ToVector(WallGeometry.Outward(side)), r = ToVector(WallGeometry.Right(side));

                // 돌출부 그림 (난간 안쪽 면, 통로를 보는 쪽)
                Vector3 inward = -n;
                Vector3 inwardRight = Vector3.Cross(inward, Up);
                for (int k = 0; k < merlons[side].Count; k++)
                {
                    var state = MerlonStateOf(side, k);
                    if (state == WallDamageRules.MerlonState.Intact) continue;
                    int index = state == WallDamageRules.MerlonState.Chipped ? IMerlonChipped : state == WallDamageRules.MerlonState.Half ? IMerlonHalf : IMerlonStub;
                    Vector3 c = n * (half - p.ParapetDepth - 0.004f) + r * merlons[side][k].along + Up * (top + p.ParapetHeight);
                    AddQuad(main, c, inwardRight, Up, p.MerlonWidth, p.MerlonHeight, inward, Sub(index, MerlonX, MerlonY, MerlonW, MerlonH));
                }

                // 외벽 데칼 (바깥면)
                Vector3 outRight = Vector3.Cross(n, Up);
                int tiles = WallDamageRules.TileCount(from, to);
                int breach = WallDamageRules.BreachTile(seed, side, tiles);
                for (int t = 0; t < tiles; t++)
                {
                    float center = WallDamageRules.TileCenter(from, to, t);
                    Vector3 foot = n * (half + DecalLift) + r * center;
                    if (st >= 3 && t == breach)
                    {
                        // 파공은 벽 높이의 1/3 이하 (BreachScale). 안쪽은 구운 어두운 돌 그림 전체, 테두리는 부서진 가장자리 그림
                        float size = WallDamageRules.TileMeters * BreachScale;
                        AddQuad(hole, n * (half + VoidLift) + r * center, outRight, Up, size, size, n, new Rect(0.002f, 0.002f, 0.996f, 0.996f));
                        AddQuad(deco, n * (half + RimLift) + r * center, outRight, Up, size, size, n, Frame4(IRim));
                        continue;
                    }

                    var kind = WallDamageRules.DecalAt(WallDamageRules.TileHash(seed, side, t), st);
                    if (kind == WallDamageRules.DecalKind.None) continue;
                    AddQuad(deco, foot, outRight, Up, WallDamageRules.TileMeters, WallDamageRules.TileMeters, n, Frame4(ICracks));
                }

                // 잔해: 바깥 발치는 바깥을 보고, 통로 위는 안쪽(사진기)을 본다. 뒤로 눕혀 위에서도 읽힌다
                foreach (var piece in rubble[side])
                {
                    if (piece.MinStage > st) continue;
                    Vector3 nh = piece.Field ? n : inward;
                    Vector3 viewRight = Vector3.Cross(nh, Up);
                    Vector3 upT = Up * cos - nh * sin;
                    Vector3 normal = nh * cos + Up * sin;
                    Vector3 foot = piece.Field
                        ? n * (half + 0.05f + piece.Depth) + r * piece.Along + Up * 0.02f
                        : n * (half - p.ParapetDepth - piece.Depth) + r * piece.Along + Up * (top + 0.012f);
                    foot -= upT * ((CellH - FootY) * mpp);   // 칸 아래 8화소는 비어 있다 — 발을 땅에 맞춘다
                    int index = IRubbleFirst + (piece.Field ? 0 : 9) + piece.Size * 3 + piece.Variant;
                    AddQuad(main, foot, viewRight, upT, CellW * mpp, CellH * mpp, normal, Sub(index, 0, 0, CellW, CellH));
                }
            }

            Apply(staticMesh, main);
            Apply(voidMesh, hole);
            Apply(decalMesh, deco);
        }

        Rect Frame4(int index) => Sub(index, 0, 0, sources[index].width, sources[index].height);

        static void AddQuad(MeshKit kit, Vector3 bottomCenter, Vector3 viewRight, Vector3 up, float width, float height, Vector3 normal, Rect uv)
        {
            Vector3 bl = bottomCenter - viewRight * (width * 0.5f), br = bottomCenter + viewRight * (width * 0.5f);
            Vector3 u = up * height;
            kit.Face(bl, bl + u, br + u, br, normal, uv);
        }

        static void Apply(Mesh target, MeshKit kit)
        {
            if (kit.VertexCount == 0) { target.Clear(); return; }
            Mesh built = kit.ToMesh(target.name);
            target.Clear();
            target.vertices = built.vertices;
            target.normals = built.normals;
            target.uv = built.uv;
            target.triangles = built.triangles;
            target.RecalculateBounds();
            Kill(built);
        }

        // 파공 마스크(흰 불투명 = 제거 영역)를 어두운 돌 무늬로 채운 그림. 결정적 (화소 위치 해시만 쓴다)
        static Texture2D BuildBreachTexture(Texture2D mask)
        {
            int w = mask.width, h = mask.height;
            Color32[] src = mask.GetPixels32();
            var dst = new Color32[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (src[y * w + x].a < 128) continue;
                    int d = 1;
                    while (d <= 6 && Solid(src, w, h, x - d, y) && Solid(src, w, h, x + d, y) && Solid(src, w, h, x, y - d) && Solid(src, w, h, x, y + d)) d++;
                    float inside = Mathf.Clamp01((d - 1) / 5f);   // 0 = 가장자리, 1 = 깊은 안쪽
                    float noise = 0.85f + 0.3f * ((WallDamageRules.Hash01(1u, x, y, 9) * 255f) / 255f);
                    int row = (h - 1 - y) / 11;
                    bool mortar = (h - 1 - y) % 11 == 0 || (x + (row % 2) * 9) % 18 == 0;
                    float shade = Mathf.Lerp(0.2f, 0.34f, inside) * noise * (mortar ? 0.6f : 1f);
                    dst[y * w + x] = new Color32((byte)(shade * 255f), (byte)(shade * 250f), (byte)(shade * 244f), 255);
                }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Wall Breach Interior (runtime)", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(dst);
            tex.Apply(false, true);
            return tex;
        }

        static bool Solid(Color32[] px, int w, int h, int x, int y) => x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a >= 128;

        static Vector3 ToVector(Unity.Mathematics.float3 v) => new Vector3(v.x, 0f, v.z);

        // ── 먼지·연기·불꽃 (사진기를 보는 세운 판) ──
        void BuildEffects()
        {
            for (int i = 0; i < DustPool; i++)
            {
                dust[i] = NewEffect("Wall Dust", litMaterial);
                effects.Add(dust[i]);
            }

            for (int side = 0; side < 4; side++)
                foreach (var spot in spots[side])
                {
                    Effect e = NewEffect(spot.Fire ? "Wall Fire" : "Wall Smoke", unlitMaterial);
                    e.Side = side; e.MinStage = spot.MinStage; e.Fire = spot.Fire;
                    Vector3 n = ToVector(WallGeometry.Outward(side)), r = ToVector(WallGeometry.Right(side));
                    e.Object.transform.localPosition = n * (half - p.ParapetDepth - 0.12f) + r * spot.Along + Up * (top + 0.02f + 0.2f * mpp);   // 잔해 더미 위
                    effects.Add(e);
                }
        }

        Effect NewEffect(string name, Material material)
        {
            var e = new Effect();
            e.Object = new GameObject(name);
            e.Object.transform.SetParent(root, false);
            e.Mesh = new Mesh { name = name };
            float hx = CellW * mpp * 0.5f, h = CellH * mpp, y0 = -(CellH - FootY) * mpp;
            e.Mesh.vertices = new[] { new Vector3(-hx, y0, 0f), new Vector3(-hx, y0 + h, 0f), new Vector3(hx, y0 + h, 0f), new Vector3(hx, y0, 0f) };
            e.Mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            e.Mesh.uv = e.Uv;
            e.Mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            e.Mesh.bounds = new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(hx * 2f, h * 2f, hx * 2f));
            e.Object.AddComponent<MeshFilter>().sharedMesh = e.Mesh;
            var renderer = e.Object.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            e.Object.SetActive(false);
            return e;
        }

        void SetFrame(Effect e, int index, int frame)
        {
            if (e.Frame == frame) return;
            e.Frame = frame;
            Rect uv = Frame(index, frame);
            e.Uv[0] = new Vector2(uv.xMin, uv.yMin); e.Uv[1] = new Vector2(uv.xMin, uv.yMax);
            e.Uv[2] = new Vector2(uv.xMax, uv.yMax); e.Uv[3] = new Vector2(uv.xMax, uv.yMin);
            e.Mesh.uv = e.Uv;
        }

        void RefreshSpots()
        {
            if (!hasArt) return;
            foreach (Effect e in effects)
            {
                if (e.MinStage <= 0) continue;   // 먼지는 따로
                bool on = night && stage[e.Side] >= e.MinStage;
                if (on == e.Active) continue;
                e.Active = on;
                e.Object.SetActive(on);
                e.Frame = -1;
            }
        }

        void SpawnDust(int side, int newStage)
        {
            // 통로 위(사진기 쪽)에 한 번, 바깥 발치에 한 번 — 새로 바뀐 자리 가까이
            Vector3 n = ToVector(WallGeometry.Outward(side)), r = ToVector(WallGeometry.Right(side));
            float inner = AlongFor(side, newStage, 10), outer = AlongFor(side, newStage, 11);
            // 막 망가진 돌출부가 있으면 그 곁에서
            for (int k = 0; k < merlons[side].Count; k++)
            {
                var before = WallDamageRules.MerlonAt(WallDamageRules.MerlonHash(seed, side, k), newStage - 1);
                if (MerlonStateOf(side, k) != before) { inner = merlons[side][k].along; break; }
            }

            Puff(side, n * (half - p.ParapetDepth - 0.1f) + r * inner + Up * (top + 0.02f));
            Puff(side, n * (half + 0.4f) + r * outer + Up * 0.02f);
        }

        float AlongFor(int side, int newStage, int salt) => Mathf.Lerp(from + 3f, to - 3f, WallDamageRules.Hash01(seed, side, newStage, salt));

        void Puff(int side, Vector3 feet)
        {
            Effect e = dust[dustNext];
            dustNext = (dustNext + 1) % DustPool;
            e.Side = side;
            e.Start = Time.unscaledTime;
            e.Active = true;
            e.Frame = -1;
            e.Object.transform.localPosition = feet;
            e.Object.SetActive(true);
            SetFrame(e, IDust, 0);
        }

        // 매 프레임 (BattleWorldView.UpdateView): 먼지·연기·불꽃 칸 바꾸기 + 사진기 쪽 돌리기. 켜진 것만, 새로 만드는 것 없음
        public void Tick(float now, Vector3 cameraPosition)
        {
            if (!hasArt) return;
            foreach (Effect e in effects)
            {
                if (!e.Active) continue;
                if (e.MinStage <= 0)
                {
                    int frame = (int)((now - e.Start) * DustFps);
                    if (frame >= Frames) { e.Active = false; e.Object.SetActive(false); continue; }
                    SetFrame(e, IDust, Mathf.Max(0, frame));
                }
                else SetFrame(e, e.Fire ? IFire : ISmoke, (int)(now * FlameFps) % Frames);

                Vector3 away = e.Object.transform.localPosition - cameraPosition;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-4f) e.Object.transform.rotation = Quaternion.LookRotation(away);
            }
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);   // 편집 모드 시험
        }

        public void Dispose()
        {
            foreach (Effect e in effects) Kill(e.Mesh);
            effects.Clear();
            Kill(staticMesh); Kill(voidMesh); Kill(decalMesh);
            Kill(litMaterial); Kill(unlitMaterial); Kill(voidMaterial); Kill(decalMaterial);
            Kill(atlas); Kill(breachTexture);
            if (root != null) Kill(root.gameObject);
        }
    }
}
