using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 정비 화면(낮) 건물을 마당에 세운 3D 건물 (W28 블록아웃 → W31 Codex K-118 그림 입힘)
    // - 기능 건물: 성벽 수리 · 병영 · 함정 공방 · 상점 (PrepEntry의 정비 장소). 등대는 가운데 탑, 성벽 계단은 북쪽 층계(BattleCourtyard)가 이미 맡는다
    // - 장식 건물(기능 없음): 대장간(굴뚝) · 마구간(울타리) · 예배당(작은 종탑) · 곡물창고 · 작은 집·창고, 우물·건초·장작더미
    // - 탑이 한눈에 보이도록 작게(키 2.6m 이하, 굴뚝·종탑 3.0m 이하) 성벽 안쪽 면에 낮게 모아 짓는다. 탑 둘레·북쪽 길·계단은 비워 둔다
    // - W31 그림: 벽(회벽·석조·판벽)·지붕(슬레이트·기와)은 256px=8m 반복 재질을 미터 기준 UV로 입힌다 (늘리지 않는다).
    //   기능 건물 앞면 데칼(문·간판)과 소품(굴뚝·종탑·울타리·건초·장작·우물·물통)은 한 장으로 합친 판 그림 → 한 메시, 밤에는 창문만 켜진다
    // - 그리는 수: 벽 3 + 지붕 2 + 그림판 1 = 6번 (재질마다 합친 메시), 밤에는 켜진 창 1번 더. 매 프레임 일은 없음 (소품 정면만 사진기 방향이 바뀔 때)
    // - 그림이 하나라도 없으면 예전 색 블록으로 그린다 (대비책)
    public sealed class BattleCourtyardBuildings : IDisposable
    {
        public const float TowerRing = 5f;        // 탑 둘레 비움 반지름 (병사 전령이 곁에 서는 자리 포함)
        public const float RoadHalfWidth = 3f;    // 계단에서 탑으로 가는 길 (너비 6m)
        public const float MaxBuildingHeight = 2.6f;   // 건물(지붕 끝까지)
        public const float MaxPieceHeight = 3.0f;      // 굴뚝·종탑
        public const float TextureMeters = 8f;         // 반복 재질 한 장 = 8m (256px, 32px=1m)
        public const float PixelsPerMeter = 32f;
        const float WallInner = 2f;               // 성벽 두께 (마당은 half - 2까지)
        const float Gap = 0.3f;                   // 성벽 안쪽 면과 건물 사이 틈
        const float Overhang = 0.2f;              // 지붕 처마
        const float DoorWidth = 0.8f, DoorHeight = 1.3f, DoorDepth = 0.06f;
        const float RoofRise = 0.5f;              // 낮은 지붕 기울기
        const float FacadeLift = 0.012f;          // 앞면 데칼이 벽에서 마당 쪽으로 뜨는 거리
        const float WindowLift = 0.026f;          // 켜진 창은 데칼보다 조금 더
        const int EmptyBottomRows = 3;            // 데칼·소품은 맨 아래 3줄이 비어 있다 (밑선 = 마지막 불투명 줄 바로 아래)
        const string Folder = "BattleArt/Buildings/";

        public enum WallKind { Stone, Timber, Plank }
        public enum RoofKind { Slate, Tile }
        public enum PieceKind { Stone, Timber, Hay, Slate }
        public enum Facade { None, Repair, Barracks, Trap, Shop }                      // K-118 기능 건물 앞면 (문·간판)
        public enum PropArt { None, Chimney, Belfry, Fence, Hay, Woodpile, Well, Trough, Covered }   // Covered = 다른 소품 그림이 대신 그린다 (우물 기둥·지붕)

        static readonly string[] WallNames = { "rough_stone", "plaster_timber", "plank_wall" };   // WallKind 순서
        static readonly string[] RoofNames = { "slate_roof", "clay_tiles" };                      // RoofKind 순서
        static readonly string[] FacadeNames = { "repair", "barracks", "trap_workshop", "shop" };   // Facade 1.. 순서 (+ _day / _night / _window_emission)
        static readonly string[] PropNames = { "chimney", "belfry", "fence", "hay", "woodpile", "well", "trough" };   // PropArt 1.. 순서
        const int FacadeCount = 4;

        public readonly struct Plot
        {
            public readonly string Name;
            public readonly int Side;        // 붙는 성벽 (0 북 · 1 동 · 2 남 · 3 서)
            public readonly float Along;     // 그 성벽 가운데에서 오른쪽(성 안에서 바깥을 볼 때)으로 옮긴 거리
            public readonly float Width, Depth, WallHeight;
            public readonly bool Pitched;
            public readonly WallKind Wall;
            public readonly RoofKind Roof;
            public readonly Facade Front;    // 마당 쪽 면 데칼 (기능 건물만)

            public Plot(string name, int side, float along, float width, float depth, float wallHeight, bool pitched, WallKind wall, RoofKind roof, Facade front = Facade.None)
            {
                Name = name; Side = side; Along = along; Width = width; Depth = depth; WallHeight = wallHeight;
                Pitched = pitched; Wall = wall; Roof = roof; Front = front;
            }

            // 마당 윗면 위 가장 높은 곳
            public float TotalHeight => WallHeight + (Pitched ? RoofRise : 0.15f);
        }

        // 건물이 아닌 작은 구조물 (굴뚝·종탑·울타리·우물·건초·장작). Attached = 건물 지붕 위에 얹혀 건물과 겹쳐도 된다
        public readonly struct Piece
        {
            public readonly string Name;
            public readonly int Side;
            public readonly float Along, Back;   // Back = 성벽 쪽 틈 끝에서 마당 쪽으로 옮긴 깊이(사각형 가까운 쪽 끝)
            public readonly float Width, Depth, Y0, Height;
            public readonly PieceKind Kind;
            public readonly bool Attached;
            public readonly PropArt Art;         // K-118 그림이 있으면 상자 대신 이 그림

            public Piece(string name, int side, float along, float back, float width, float depth, float y0, float height, PieceKind kind, bool attached = false, PropArt art = PropArt.None)
            {
                Name = name; Side = side; Along = along; Back = back; Width = width; Depth = depth; Y0 = y0; Height = height;
                Kind = kind; Attached = attached; Art = art;
            }
        }

        // 정비 마당 그림의 왼쪽→오른쪽 순서(수리 · 병영 · 등대 · 함정 · 상점 · 계단)를 네 성벽에 돌려 놓았다
        public static readonly Plot[] Plots =
        {
            // 기능 건물 (정비 장소)
            new Plot("성벽 수리", 3, -6f, 5f, 3f, 1.9f, true, WallKind.Stone, RoofKind.Slate, Facade.Repair),
            new Plot("병영", 1, 6f, 6.5f, 3.5f, 2.0f, true, WallKind.Timber, RoofKind.Tile, Facade.Barracks),
            new Plot("함정 공방", 2, 8f, 4.5f, 3f, 1.8f, false, WallKind.Stone, RoofKind.Slate, Facade.Trap),
            new Plot("상점", 2, -8f, 4.5f, 3f, 1.9f, true, WallKind.Timber, RoofKind.Tile, Facade.Shop),
            // 장식 건물 (기능 없음)
            new Plot("대장간", 3, 5f, 4f, 3f, 1.8f, true, WallKind.Stone, RoofKind.Slate),
            new Plot("마구간", 1, -6f, 5f, 3f, 1.7f, true, WallKind.Plank, RoofKind.Tile),
            new Plot("예배당", 0, 8f, 4f, 3f, 1.9f, true, WallKind.Stone, RoofKind.Slate),
            new Plot("곡물창고", 0, -8.5f, 4.5f, 3f, 1.8f, true, WallKind.Plank, RoofKind.Tile),
            new Plot("작은 집", 2, -14f, 2.6f, 2.4f, 1.6f, true, WallKind.Timber, RoofKind.Slate),
            new Plot("창고", 0, -13f, 2.4f, 2.2f, 1.5f, true, WallKind.Stone, RoofKind.Tile),
        };

        public static readonly Piece[] Pieces =
        {
            new Piece("대장간 굴뚝", 3, 5.9f, 0.3f, 0.5f, 0.5f, 1.4f, 1.5f, PieceKind.Stone, true, PropArt.Chimney),
            new Piece("종탑", 0, 8f, 1.0f, 0.9f, 0.9f, 1.9f, 1.1f, PieceKind.Timber, true, PropArt.Belfry),
            new Piece("마구간 울타리 앞", 1, -6f, 4.9f, 5.4f, 0.1f, 0f, 0.6f, PieceKind.Timber, false, PropArt.Fence),
            new Piece("마구간 울타리 왼", 1, -3.4f, 3.3f, 0.1f, 1.6f, 0f, 0.6f, PieceKind.Timber, false, PropArt.Fence),
            new Piece("마구간 울타리 오른", 1, -8.6f, 3.3f, 0.1f, 1.6f, 0f, 0.6f, PieceKind.Timber, false, PropArt.Fence),
            new Piece("마구간 건초", 1, -6f, 3.4f, 1.4f, 0.9f, 0f, 0.5f, PieceKind.Hay, false, PropArt.Hay),
            new Piece("마구간 물통", 1, -7.7f, 3.5f, 1.4f, 0.5f, 0f, 0.5f, PieceKind.Timber, false, PropArt.Trough),
            new Piece("건초 더미", 0, -4.3f, 0.2f, 1.5f, 1.2f, 0f, 0.7f, PieceKind.Hay, false, PropArt.Hay),
            new Piece("장작더미", 3, 8.6f, 0.2f, 1.6f, 0.9f, 0f, 0.6f, PieceKind.Timber, false, PropArt.Woodpile),
            new Piece("우물 둘레", 2, 13.4f, 0.4f, 1.2f, 1.2f, 0f, 0.55f, PieceKind.Stone, false, PropArt.Well),
            new Piece("우물 기둥 왼", 2, 12.9f, 0.5f, 0.1f, 0.1f, 0.55f, 1.0f, PieceKind.Timber, true, PropArt.Covered),
            new Piece("우물 기둥 오른", 2, 13.9f, 0.5f, 0.1f, 0.1f, 0.55f, 1.0f, PieceKind.Timber, true, PropArt.Covered),
            new Piece("우물 지붕", 2, 13.4f, 0.4f, 1.5f, 1.4f, 1.55f, 0.1f, PieceKind.Slate, true, PropArt.Covered),
        };

        static Vector3 Out(int side) { var o = WallGeometry.Outward(side); return new Vector3(o.x, 0f, o.z); }
        static Vector3 Right(int side) { var r = WallGeometry.Right(side); return new Vector3(r.x, 0f, r.z); }

        static Rect Span(int side, float half, float along0, float along1, float back0, float back1)
        {
            Vector3 n = Out(side), r = Right(side);
            float plane = half - WallInner - Gap;
            Vector3 a = n * (plane - back0) + r * along0, b = n * (plane - back1) + r * along1;
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
        }

        // 바닥 위에서 본 건물이 차지하는 땅 (처마·문 포함, 세계 좌표 x·z 사각형) — 테스트가 쓴다
        public static Rect Footprint(in Plot p, float half)
        {
            float oh = p.Pitched ? Overhang : 0.1f;
            return Span(p.Side, half, p.Along - p.Width * 0.5f - oh, p.Along + p.Width * 0.5f + oh, -oh, p.Depth + Mathf.Max(oh, DoorDepth));
        }

        public static Rect Footprint(in Piece p, float half)
            => Span(p.Side, half, p.Along - p.Width * 0.5f, p.Along + p.Width * 0.5f, p.Back, p.Back + p.Depth);

        readonly GameObject root;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        readonly List<UnityEngine.Object> textures = new List<UnityEngine.Object>();
        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        readonly List<BattleCourtyardPropBillboards> billboardParts = new List<BattleCourtyardPropBillboards>();
        Material spriteMaterial;
        Texture2D dayAtlas, nightAtlas;
        MeshRenderer windowRenderer;
        bool night;

        // 확인용
        public bool HasWallArt { get; private set; }     // 벽·지붕 재질 그림이 입혀졌다
        public bool HasSpriteArt { get; private set; }   // 앞면 데칼·소품 그림판이 만들어졌다
        public bool Night => night;
        public int FacadeDecals { get; private set; }
        public int PropQuads { get; private set; }
        public IReadOnlyList<MeshRenderer> Renderers => renderers;
        public bool WindowsLit => windowRenderer != null && windowRenderer.enabled;
        public Texture AtlasInUse => spriteMaterial != null ? spriteMaterial.mainTexture : null;
        public int DrawCalls { get { int n = 0; foreach (var r in renderers) if (r != null && r.enabled) n++; return n; } }

        public BattleCourtyardBuildings(float half, Transform parent)
        {
            root = new GameObject("Courtyard Buildings (W31 K-118)");
            root.transform.SetParent(parent, false);

            var wallTex = new Texture2D[WallNames.Length];
            var roofTex = new Texture2D[RoofNames.Length];
            bool tiles = true;
            for (int i = 0; i < wallTex.Length; i++) { wallTex[i] = Resources.Load<Texture2D>(Folder + WallNames[i]); tiles &= wallTex[i] != null; }
            for (int i = 0; i < roofTex.Length; i++) { roofTex[i] = Resources.Load<Texture2D>(Folder + RoofNames[i]); tiles &= roofTex[i] != null; }
            HasWallArt = tiles;

            var walls = new Builder[WallNames.Length];
            var roofs = new Builder[RoofNames.Length];
            for (int i = 0; i < walls.Length; i++) walls[i] = new Builder();
            for (int i = 0; i < roofs.Length; i++) roofs[i] = new Builder();
            var doors = new Builder();       // 문: 그림이 있으면 판벽 재질 안에, 없으면 따로 (예전)
            var hay = new Builder();         // 건초 상자 (그림판이 없을 때만)

            bool sprites = BuildSpriteAtlases(out Rect[] rects, out Rect[] windowRects, out Vector2Int[] sizes, out Vector2Int atlasSize, out Vector2Int windowAtlasSize, out Texture2D windowAtlas);
            HasSpriteArt = sprites;

            float floor = BattleSim.WallHeight + BattleCourtyardSkin, plane = half - WallInner - Gap;
            var sprite = new SpriteMesh();
            var windows = new SpriteMesh();
            int index = 0;
            foreach (Plot p in Plots)
            {
                Builder wall = walls[(int)p.Wall];
                Builder roof = roofs[(int)p.Roof];
                Frame f = new Frame(Out(p.Side), Right(p.Side), plane, floor);
                float x0 = p.Along - p.Width * 0.5f, x1 = p.Along + p.Width * 0.5f;
                wall.SetOrigin(index); roof.SetOrigin(index); index++;
                wall.Box(f, x0, 0f, 0f, x1, p.Depth, p.WallHeight);
                if (p.Pitched) roof.Gable(f, x0 - Overhang, -Overhang, x1 + Overhang, p.Depth + Overhang, p.WallHeight, RoofRise);
                else roof.Box(f, x0 - 0.1f, -0.1f, p.WallHeight, x1 + 0.1f, p.Depth + 0.1f, p.WallHeight + 0.15f);   // 평지붕: 얇은 판

                if (sprites && p.Front != Facade.None)
                {
                    // 앞면 데칼: 문 중심 피벗(왼쪽에서 64px)을 건물 가운데에 맞춘다. 밑선 위 61줄만 쓴다
                    int k = (int)p.Front - 1;
                    float h = (sizes[k].y - EmptyBottomRows) / PixelsPerMeter, w = sizes[k].x / PixelsPerMeter;
                    float left = p.Along - 64f / PixelsPerMeter;
                    float b = p.Depth + FacadeLift;
                    Rect uv = Crop(rects[k], atlasSize.y);
                    sprite.Static(f.P(left, b, 0f), f.P(left, b, h), f.P(left + w, b, h), f.P(left + w, b, 0f), f.Inward, uv);
                    FacadeDecals++;
                    b = p.Depth + WindowLift;
                    windows.Static(f.P(left, b, 0f), f.P(left, b, h), f.P(left + w, b, h), f.P(left + w, b, 0f), f.Inward, Crop(windowRects[k], windowAtlasSize.y));
                }
                else
                {
                    // 문: 마당 쪽 면 가운데에 살짝 튀어나온 판
                    Builder d = HasWallArt ? walls[(int)WallKind.Plank] : doors;
                    d.SetOrigin(index);
                    d.Box(f, p.Along - DoorWidth * 0.5f, p.Depth, 0f, p.Along + DoorWidth * 0.5f, p.Depth + DoorDepth, DoorHeight);
                }
            }

            foreach (Piece q in Pieces)
            {
                if (sprites && q.Art == PropArt.Covered) continue;   // 우물 기둥·지붕은 우물 그림이 대신
                Frame f = new Frame(Out(q.Side), Right(q.Side), plane, floor);
                if (sprites && q.Art != PropArt.None)
                {
                    AddProp(sprite, f, q, rects, sizes, atlasSize);
                    continue;
                }

                Builder b = q.Kind == PieceKind.Stone ? walls[(int)WallKind.Stone] : q.Kind == PieceKind.Timber ? walls[(int)WallKind.Plank] : q.Kind == PieceKind.Hay ? hay : roofs[(int)RoofKind.Slate];
                b.SetOrigin(index++);
                b.Box(f, q.Along - q.Width * 0.5f, q.Back, q.Y0, q.Along + q.Width * 0.5f, q.Back + q.Depth, q.Y0 + q.Height);
            }

            Color white = new Color(0.95f, 0.95f, 0.95f);
            Spawn("Walls Stone", walls[(int)WallKind.Stone], MakeWall(wallTex[0], new Color(0.52f, 0.50f, 0.46f), white));
            Spawn("Walls Timber", walls[(int)WallKind.Timber], MakeWall(wallTex[1], new Color(0.46f, 0.34f, 0.24f), white));
            Spawn("Walls Plank", walls[(int)WallKind.Plank], MakeWall(wallTex[2], new Color(0.40f, 0.29f, 0.20f), white));
            Spawn("Roofs Slate", roofs[(int)RoofKind.Slate], MakeWall(roofTex[0], new Color(0.36f, 0.36f, 0.38f), white));
            Spawn("Roofs Tile", roofs[(int)RoofKind.Tile], MakeWall(roofTex[1], new Color(0.55f, 0.27f, 0.20f), white));
            Spawn("Doors", doors, MakeWall(null, new Color(0.20f, 0.13f, 0.09f), white));
            Spawn("Hay", hay, MakeWall(null, new Color(0.72f, 0.60f, 0.28f), white));

            if (sprites)
            {
                spriteMaterial = MakeClipMaterial("Courtyard Building Sprites (runtime)", dayAtlas, lit: true);
                SpawnSprites("Building Sprites", sprite, spriteMaterial);
                if (windows.QuadCount > 0)
                {
                    Material wm = MakeClipMaterial("Courtyard Building Windows (runtime)", windowAtlas, lit: false);
                    windowRenderer = SpawnSprites("Building Windows (night)", windows, wm, billboards: false);
                    windowRenderer.enabled = false;   // 밤에만
                }
            }
        }

        const float BattleCourtyardSkin = 0.01f;   // 마당 바닥판과 같은 높이 (BattleCourtyard.SkinLift)

        // ── 그림판 ──
        // 밑 3줄(빈 줄)을 잘라낸 UV (밑선이 판 맨 아래가 되게)
        static Rect Crop(Rect r, int atlasHeight)
        {
            float cut = EmptyBottomRows / (float)atlasHeight;
            return new Rect(r.xMin, r.yMin + cut, r.width, r.height - cut);
        }

        // 앞면 4(낮) + 소품 7 → 낮판, 앞면 4(밤) + 소품 7 → 밤판 (칸 크기가 같아 배치도 같다), 창문 발광 마스크 × 밤 그림 → 켜진 창 판
        bool BuildSpriteAtlases(out Rect[] rects, out Rect[] windowRects, out Vector2Int[] sizes, out Vector2Int atlasSize, out Vector2Int windowAtlasSize, out Texture2D windowAtlas)
        {
            rects = null; windowRects = null; sizes = null; atlasSize = default; windowAtlasSize = default; windowAtlas = null;
            int total = FacadeCount + PropNames.Length;
            var day = new Texture2D[total];
            var nightSrc = new Texture2D[total];
            var masks = new Texture2D[FacadeCount];
            for (int i = 0; i < FacadeCount; i++)
            {
                day[i] = Resources.Load<Texture2D>(Folder + FacadeNames[i] + "_day");
                nightSrc[i] = Resources.Load<Texture2D>(Folder + FacadeNames[i] + "_night");
                masks[i] = Resources.Load<Texture2D>(Folder + FacadeNames[i] + "_window_emission");
                if (masks[i] == null || !masks[i].isReadable || nightSrc[i] == null) return false;
                if (masks[i].width != nightSrc[i].width || masks[i].height != nightSrc[i].height) return false;   // 크기가 다르면 창 마스크를 못 쓴다 → 그림 없이 예전 블록
            }

            for (int i = 0; i < PropNames.Length; i++) { day[FacadeCount + i] = Resources.Load<Texture2D>(Folder + PropNames[i]); nightSrc[FacadeCount + i] = day[FacadeCount + i]; }
            sizes = new Vector2Int[total];
            for (int i = 0; i < total; i++)
            {
                if (day[i] == null || nightSrc[i] == null || !day[i].isReadable || !nightSrc[i].isReadable) return false;
                if (day[i].width != nightSrc[i].width || day[i].height != nightSrc[i].height) return false;
                sizes[i] = new Vector2Int(day[i].width, day[i].height);
            }

            dayAtlas = Pack(day, "Courtyard Buildings Day (runtime)", out rects, out atlasSize);
            nightAtlas = Pack(nightSrc, "Courtyard Buildings Night (runtime)", out _, out _);

            // 켜진 창: 마스크의 흰 픽셀만 밤 그림 색을 조금 밝혀 (따뜻하게, 절제해서) 쓴다
            var glow = new Texture2D[FacadeCount];
            for (int i = 0; i < FacadeCount; i++)
            {
                Color32[] n = nightSrc[i].GetPixels32(), m = masks[i].GetPixels32();
                var o = new Color32[n.Length];
                for (int k = 0; k < o.Length && k < m.Length; k++)
                {
                    if (m[k].a < 128 || m[k].r < 128) continue;
                    o[k] = new Color32((byte)Mathf.Min(255, n[k].r * 1.12f), (byte)Mathf.Min(255, n[k].g * 1.08f), (byte)Mathf.Min(255, n[k].b * 1.0f), 255);
                }

                glow[i] = new Texture2D(nightSrc[i].width, nightSrc[i].height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                glow[i].SetPixels32(o);
                glow[i].Apply(false, false);
            }

            windowAtlas = Pack(glow, "Courtyard Building Windows (runtime)", out windowRects, out windowAtlasSize);
            foreach (var g in glow) Kill(g);
            textures.Add(dayAtlas); textures.Add(nightAtlas); textures.Add(windowAtlas);
            return true;
        }

        // 선반식으로 한 장에 합친다 (사이 2화소 빈칸). 칸 크기만으로 배치가 정해져 낮·밤 판이 같은 자리를 쓴다
        static Texture2D Pack(Texture2D[] src, string name, out Rect[] rects, out Vector2Int size)
        {
            const int pad = 2;
            int width = 1024;
            foreach (var s in src) width = Mathf.Max(width, s.width + pad * 2);   // 가장 넓은 그림도 들어가게
            var pos = new Vector2Int[src.Length];
            int x = pad, y = pad, rowH = 0;
            for (int i = 0; i < src.Length; i++)
            {
                int w = src[i].width, h = src[i].height;
                if (x + w + pad > width) { x = pad; y += rowH + pad; rowH = 0; }
                pos[i] = new Vector2Int(x, y);
                x += w + pad;
                rowH = Mathf.Max(rowH, h);
            }

            int height = y + rowH + pad;
            var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixels32(new Color32[width * height]);
            rects = new Rect[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                atlas.SetPixels32(pos[i].x, pos[i].y, src[i].width, src[i].height, src[i].GetPixels32());
                rects[i] = new Rect(pos[i].x / (float)width, pos[i].y / (float)height, src[i].width / (float)width, src[i].height / (float)height);
            }

            atlas.Apply(false, true);   // 시스템 메모리 사본은 버린다
            size = new Vector2Int(width, height);
            return atlas;
        }

        // 소품 그림 한 장: 울타리는 벽을 따라 2m씩 이어 붙이고(고정 판), 나머지는 사진기를 보는 판
        void AddProp(SpriteMesh mesh, in Frame f, in Piece q, Rect[] rects, Vector2Int[] sizes, Vector2Int atlasSize)
        {
            int k = FacadeCount + (int)q.Art - 1;
            Rect uv = Crop(rects[k], atlasSize.y);
            float w = sizes[k].x / PixelsPerMeter, h = (sizes[k].y - EmptyBottomRows) / PixelsPerMeter;
            if (q.Art == PropArt.Fence)
            {
                bool alongX = q.Width >= q.Depth;   // 앞쪽 울타리는 성벽과 나란히, 옆 울타리는 마당 쪽으로
                float length = alongX ? q.Width : q.Depth;
                float start = alongX ? q.Along - q.Width * 0.5f : q.Back;
                for (float s = 0f; s < length - 1e-3f; s += w)
                {
                    float seg = Mathf.Min(w, length - s);
                    Rect part = new Rect(uv.xMin, uv.yMin, uv.width * seg / w, uv.height);
                    if (alongX)
                    {
                        float bb = q.Back + q.Depth * 0.5f;
                        mesh.Static(f.P(start + s, bb, 0f), f.P(start + s, bb, h), f.P(start + s + seg, bb, h), f.P(start + s + seg, bb, 0f), f.Inward, part);
                    }
                    else
                    {
                        mesh.Static(f.P(q.Along, start + s, 0f), f.P(q.Along, start + s, h), f.P(q.Along, start + s + seg, h), f.P(q.Along, start + s + seg, 0f), f.Right, part);
                    }
                    PropQuads++;
                }

                return;
            }

            // 굴뚝·종탑 꼭대기가 3m 안에 들도록 줄인다
            float scale = Mathf.Min(1f, (MaxPieceHeight - q.Y0) / h);
            Vector3 feet = f.P(q.Along, q.Back + q.Depth * 0.5f, q.Y0);
            mesh.Billboard(feet, w * scale * 0.5f, h * scale, uv);
            PropQuads++;
        }

        MeshRenderer SpawnSprites(string name, SpriteMesh sm, Material m, bool billboards = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            Mesh mesh = sm.ToMesh(name);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            renderers.Add(mr);
            if (billboards && sm.Billboards.Length > 0)
            {
                var b = go.AddComponent<BattleCourtyardPropBillboards>();
                billboardParts.Add(b);
                b.Init(mesh, sm.Verts, sm.Normals, sm.Billboards);
                b.Face(Vector3.forward);   // 첫 방향 (사진기가 정해지면 바로 바뀐다)
            }

            return mr;
        }

        // 낮·밤 전환: 낮판 ↔ 밤판 (창문만 다르다)과 켜진 창. 바뀔 때만 일한다
        public void Tick(bool isNight, Camera camera = null)
        {
            if (camera != null) foreach (var b in billboardParts) b.Follow(camera);
            if (isNight == night) return;
            night = isNight;
            if (spriteMaterial != null && dayAtlas != null && nightAtlas != null)
            {
                Texture t = night ? nightAtlas : dayAtlas;
                if (spriteMaterial.HasProperty("_BaseMap")) spriteMaterial.SetTexture("_BaseMap", t);
                spriteMaterial.mainTexture = t;
            }

            if (windowRenderer != null) windowRenderer.enabled = night;
        }

        void Spawn(string name, Builder b, Material m)
        {
            if (b.Count == 0) { Kill(m); materials.Remove(m); return; }
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            Mesh mesh = b.ToMesh(name);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            renderers.Add(mr);
        }

        // 벽·지붕: 반복 그림이 있으면 흰색 × 그림, 없으면 예전 색 한 가지
        Material MakeWall(Texture2D texture, Color flat, Color tint)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            m.name = "CourtyardBuildings (runtime)";
            Color color = texture != null ? tint : flat;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (texture != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
                m.mainTexture = texture;
            }

            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            materials.Add(m);
            return m;
        }

        // 투명 자르기 견본 재질을 복사 (빌드에서 키워드가 빠지지 않는다). 없으면 실행 중에 키워드를 켠다
        Material MakeClipMaterial(string name, Texture2D atlas, bool lit)
        {
            Material m = lit ? ClipMaterials.LitCopy(name) : ClipMaterials.UnlitCopy(name);
            if (m == null)
            {
                Shader shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
                m = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // 세운 그림은 양면
                m.EnableKeyword("_ALPHATEST_ON");
            }

            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", atlas);
            m.mainTexture = atlas;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (lit)
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            }

            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            materials.Add(m);
            return m;
        }

        public void Dispose()
        {
            Kill(root);
            foreach (Material m in materials) Kill(m);
            foreach (Mesh m in meshes) Kill(m);
            foreach (var t in textures) Kill(t);
            materials.Clear();
            meshes.Clear();
            textures.Clear();
            renderers.Clear();
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);   // 편집 모드 시험
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

        // 세운 그림 사각형 모음 (앞면 데칼·울타리는 제자리, 소품은 사진기를 보는 판). 한 메시
        sealed class SpriteMesh
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();
            readonly List<BattleCourtyardPropBillboards.Quad> boards = new List<BattleCourtyardPropBillboards.Quad>();

            public int QuadCount => verts.Count / 4;
            public Vector3[] Verts => verts.ToArray();
            public Vector3[] Normals => normals.ToArray();
            public BattleCourtyardPropBillboards.Quad[] Billboards => boards.ToArray();

            // 앞면이 normal 쪽을 보게 감는 방향을 맞춘 고정 사각형 (왼아래 · 왼위 · 오른위 · 오른아래)
            public void Static(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal, Rect uv)
            {
                int s = Add(bl, tl, tr, br, normal, uv);
                if (Vector3.Dot(Vector3.Cross(tl - bl, tr - bl), normal) < 0f) { tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 }); }
                else tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            // 사진기를 보는 판 (처음 방향은 BattleCourtyardPropBillboards가 바꾼다)
            public void Billboard(Vector3 feet, float halfWidth, float height, Rect uv)
            {
                Vector3 r = Vector3.right * halfWidth, u = Vector3.up * height;
                int s = Add(feet - r, feet - r + u, feet + r + u, feet + r, Vector3.back, uv);
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                boards.Add(new BattleCourtyardPropBillboards.Quad { First = s, Feet = feet, HalfWidth = halfWidth, Height = height });
            }

            int Add(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal, Rect uv)
            {
                int s = verts.Count;
                verts.Add(bl); verts.Add(tl); verts.Add(tr); verts.Add(br);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uvs.Add(new Vector2(uv.xMin, uv.yMin)); uvs.Add(new Vector2(uv.xMin, uv.yMax)); uvs.Add(new Vector2(uv.xMax, uv.yMax)); uvs.Add(new Vector2(uv.xMax, uv.yMin));
                return s;
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.MarkDynamic();
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                var b = mesh.bounds;
                b.Expand(4f);   // 소품 판이 돌아도 잘리지 않게 여유
                mesh.bounds = b;
                return mesh;
            }
        }

        // 상자·박공 지붕을 한 메시로 합친다. 면마다 법선 하나, UV는 미터 기준 (0.125/m = 256px 한 장이 8m) — 면 안에서 늘어나지 않는다
        sealed class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();
            Vector3 hint = Vector3.right;   // 윗면 UV의 가로 방향 (성벽과 나란히)
            Vector2 origin;                 // 건물마다 무늬 시작을 어긋나게 (같은 재질 건물이 똑같아 보이지 않게)

            public int Count => verts.Count;

            public void SetOrigin(int index)
            {
                origin = new Vector2((index * 0.37f) % 1f, (index * 0.61f) % 1f);
            }

            // x0..x1 성벽 따라, b0..b1 깊이, y0..y1 높이. 아랫면은 안 보이니 만들지 않는다
            public void Box(in Frame f, float x0, float b0, float y0, float x1, float b1, float y1)
            {
                hint = f.Right;
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
                hint = f.Right;
                float mid = (b0 + b1) * 0.5f;
                Vector3 e0a = f.P(x0, b1, eaveY), e0b = f.P(x1, b1, eaveY);   // 마당 쪽 처마
                Vector3 e1a = f.P(x0, b0, eaveY), e1b = f.P(x1, b0, eaveY);   // 성벽 쪽 처마
                Vector3 ra = f.P(x0, mid, eaveY + rise), rb = f.P(x1, mid, eaveY + rise);
                Quad(e0a, e0b, rb, ra, Vector3.Normalize(f.Inward * rise + Vector3.up * (b1 - mid)));
                Quad(e1b, e1a, ra, rb, Vector3.Normalize(-f.Inward * rise + Vector3.up * (mid - b0)));
                Tri(e0b, e1b, rb, f.Right);
                Tri(e1a, e0a, ra, -f.Right);
            }

            // 면 안의 두 축(가로 e1 · 세로 e2, 단위 길이)에 세계 좌표를 미터 × 0.125로 투영한다 → 늘어나지 않는다
            Vector2 Uv(Vector3 p, Vector3 normal)
            {
                Vector3 e1, e2;
                if (Mathf.Abs(normal.y) > 0.999f) { e1 = hint; e2 = Vector3.Cross(normal, e1); }
                else { e1 = Vector3.Normalize(Vector3.Cross(Vector3.up, normal)); e2 = Vector3.Normalize(Vector3.Cross(normal, e1)); }
                float k = 1f / TextureMeters;
                return new Vector2(Vector3.Dot(p, e1) * k + origin.x, Vector3.Dot(p, e2) * k + origin.y);
            }

            // 앞면 = 법선 쪽 (감는 방향이 어긋나면 뒤집는다)
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 t = a; a = d; d = t; t = b; b = c; c = t; }   // a↔d, b↔c: 순서 뒤집기
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int i = 0; i < 4; i++) { normals.Add(normal); }
                uvs.Add(Uv(a, normal)); uvs.Add(Uv(b, normal)); uvs.Add(Uv(c, normal)); uvs.Add(Uv(d, normal));
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 t = b; b = c; c = t; }
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                for (int i = 0; i < 3; i++) normals.Add(normal);
                uvs.Add(Uv(a, normal)); uvs.Add(Uv(b, normal)); uvs.Add(Uv(c, normal));
                tris.AddRange(new[] { s, s + 1, s + 2 });
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
