using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 채우기 (W19 임시 → W27 Codex K-116 안마당 그림 연결, 보이는 것만 — 전투 계산은 그대로)
    // - 바닥: K-116 courtyard_cobble_512(이음새 없는 둥근 돌)를 점 필터로 반복해 네 성벽 안쪽에만 깐다 (조명은 다른 땅과 같은 Lit 셰이더). 그림이 없으면 예전 courtyard_cobble
    //   게임 속 세계는 병사 키 0.35m라 Codex 설명서의 '1m'보다 작다 — 돌 하나가 병사 발 크기쯤이 되게 한 장을 CobbleTile512Meters로 줄여 깐다 (모든 소품은 같은 비율 PropScale)
    // - 데칼: 흙·짚·깨진 돌·물웅덩이 투명 그림을 길·계단·탑 둘레를 피해 듬성듬성 (한 장 그림으로 합쳐 그리기 1번)
    // - 계단: 네 성벽 모두(W35) 통로 안쪽 끝 가운데 (네 벌을 한 메시로 합쳐 그리는 수는 그대로). 마당과 성벽 윗면이 같은 높이(테스트가 약속)라 진짜로 내려가지는 못하므로
    //   '통로 끝 약간 솟은 층계참 + 낮아지는 계단 5단'을 짓고, 주인공 그림만 그 높이를 따라 오르내리게 한다(HeroLift). 그림 높이 말고는 아무것도 안 바뀐다
    //   K-116 계단 윗면·앞면(stair_step_atlas)과 옆돌(stair_flank)을 면마다 맞는 칸으로 입힌다 (면 크기에 맞춰 늘리지 않고 칸 그대로)
    // - 소품: 상자·통은 K-116 props_box_atlas 면 칸, 무기 걸이·수레·화살 묶음은 사진기를 보는 세운 그림(CourtyardBillboard). 길(탑으로 가는 가운데 줄)은 비워 둔다
    // - 그리는 수: 바닥 1 + 데칼 1 + 계단 2(윗면·옆돌) + 소품 1 + 세운 그림 3 = 8번 (자리마다 합친 메시). 그림이 없으면 예전 임시 모양으로 돌아간다
    public sealed class BattleCourtyard : IDisposable
    {
        const string Folder = "BattleArt/Courtyard/";
        const float SkinLift = 0.01f;            // 통로 덮는 판과 같은 높이 (BattleWorldView.SkinLift)
        public const float WallThickness = 2f;
        const float CobbleTileMeters = 2.8f;     // (옛 그림) 돌바닥 그림 한 장이 덮는 미터 (돌 하나가 병사 키(0.35m)와 비슷한 크기)
        const float CobbleTile512Meters = 2.6f;  // K-116 바닥 한 장(512화소, 설명서 16m)이 덮는 미터 — 돌 하나 약 0.18m
        public const float PropScale = 0.3f / 32f;   // K-116 소품·데칼 한 화소 = m (설명서 32화소 = 1m 의 0.3배 — 상자 한 변 0.3m)
        const int DecalCount = 16;
        const float DecalLift = 0.01f;           // 바닥(+1cm) 위 1cm (먼 전체 보기에서 깜박이지 않게). 데칼은 길·광장을 피해 놓으므로 길 윗면(+3cm)과 겹치지 않는다
        const float DecalLayer = 0.01f;          // 데칼끼리 겹치면 위로 한 층씩 (W41)
        static readonly string[] SpriteNames = { "decal_dirt", "decal_puddle", "decal_straw", "decal_cracked", "weapon_rack", "cart", "arrow_bundles" };
        const int SpriteDirt = 0, SpritePuddle = 1, SpriteStraw = 2, SpriteCracked = 3, SpriteRack = 4, SpriteCart = 5, SpriteArrows = 6;
        static readonly Vector2 FlankTile = new Vector2(1.2f, 0.6f);   // 옆돌 그림(4×2m)의 0.3배

        // 계단 크기 (병사 키 0.35m, 주인공 0.375m 기준으로 아주 낮게)
        // 층계참은 성벽 바깥 모서리에서 1.5m보다 안쪽(통로 안쪽 0.45m)에서 끝난다 — 처음 서는 자리(1m)·병사 이동 길(1.4m)을 덮지 않게 (W19 검토)
        public const float StairHalfWidth = 1.5f;   // W26: 의식 길(반폭 1.1 + 연석)보다 넓게 — 계단이 진짜 계단처럼 읽히게
        const float StairWallWidth = 0.2f;          // 계단 양옆 낮은 돌담 두께
        const float StairWallRise = 0.2f;           // 돌담이 각 단 윗면 위로 솟는 높이
        public const float LandingDepth = 0.45f;    // 통로 안쪽 끝(성벽 안쪽 가장자리)에서 바깥쪽으로 층계참 깊이
        public const float LandingHeight = 0.1f;
        public const int StepCount = 5;             // 층계참 → 마당 (마지막 한 단은 마당 바닥)
        public const float TreadDepth = 0.25f;
        const float EdgeRamp = 0.15f;               // 높이 보정이 상자 모서리 안쪽에서 서서히 0이 되는 폭 (모서리에서 정확히 0)

        readonly GameObject root;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();
        BattleWorldMaterials stone;
        bool ownsStone;

        // 옛 마당 상자 윗면이 새 바닥에 덮였는가 (BattleView가 헛그리기를 끈다)
        public bool HasFloor { get; }

        // 시험·확인용
        public Texture FloorTexture { get; private set; }
        public bool HasK116Floor { get; private set; }
        public int DecalsPlaced { get; private set; }
        public bool HasK116Stairs { get; private set; }
        public bool HasK116Props { get; private set; }
        public int BillboardCount { get; private set; }
        public int RendererCount { get; private set; }
        public Transform Root => root.transform;

        public BattleCourtyard(float half, Transform parent, BattleWorldMaterials sharedStone = null)
        {
            root = new GameObject("Courtyard (W27)");
            root.transform.SetParent(parent, false);
            float top = BattleSim.WallHeight;

            BuildFloor(half, top);
            HasFloor = true;
            BuildStairs(half, top, sharedStone);
            BuildProps(half, top);
            BuildSprites(half, top);
        }

        // 기준 성벽(북, BattleCameraRig.StairsSide)의 바깥 방향·오른쪽 (하드코딩 없음)
        public static void StairFrame(out Vector3 n, out Vector3 r) => StairFrame(BattleCameraRig.StairsSide, out n, out r);

        // side 성벽의 바깥 방향·오른쪽 (W35: 계단은 네 성벽 모두에 있다)
        public static void StairFrame(int side, out Vector3 n, out Vector3 r)
        {
            var o = WallGeometry.Outward(side);
            var rr = WallGeometry.Right(side);
            n = new Vector3(o.x, 0f, o.z);
            r = new Vector3(rr.x, 0f, rr.z);
        }

        // 주인공 그림 높이 보정 (m, 발 위치 기준). 네 계단·층계참 위에서만 0보다 크다
        public static float HeroLift(Vector3 feet, float half)
        {
            float best = 0f;
            for (int side = 0; side < 4; side++) best = Mathf.Max(best, HeroLift(side, feet, half));
            return best;
        }

        public static float HeroLift(int side, Vector3 feet, float half)
        {
            StairFrame(side, out Vector3 n, out Vector3 r);
            Vector3 d = new Vector3(feet.x, 0f, feet.z) - n * (half - WallThickness);   // 성벽 안쪽 가장자리 가운데에서
            float lz = Vector3.Dot(d, n);   // + = 통로 쪽(바깥), - = 마당 쪽
            float lx = Mathf.Abs(Vector3.Dot(d, r));
            float wx = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StairHalfWidth - EdgeRamp, StairHalfWidth, lx));
            if (wx <= 0f) return 0f;
            float lift;
            if (lz >= 0f)
            {
                if (lz >= LandingDepth) return 0f;
                lift = LandingHeight * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LandingDepth - EdgeRamp, LandingDepth, lz)));
            }
            else
            {
                int k = Mathf.FloorToInt(-lz / TreadDepth);
                lift = k >= StepCount - 1 ? 0f : TreadTop(k);
            }

            return lift * wx;
        }

        // k번째 단 윗면 높이 (k = 0이 층계참 바로 아래)
        static float TreadTop(int k) => LandingHeight * (1f - (k + 1f) / StepCount);

        // ── 바닥 ──
        void BuildFloor(float half, float top)
        {
            var cobble = Resources.Load<Texture2D>(Folder + "courtyard_cobble_512");   // K-116
            HasK116Floor = cobble != null;
            if (cobble != null)
            {
                cobble.filterMode = FilterMode.Point;          // 가져오기 설정이 빠져도 도트 그대로, 양방향 반복
                cobble.wrapMode = TextureWrapMode.Repeat;
            }
            else cobble = Resources.Load<Texture2D>(Folder + "courtyard_cobble");

            FloorTexture = cobble;
            float tile = HasK116Floor ? CobbleTile512Meters : CobbleTileMeters;
            float size = (half - WallThickness) * 2f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Courtyard Floor";
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, top + SkinLift, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.GetComponent<Renderer>();
            Material m = MakeLit(cobble, new Color(0.5f, 0.47f, 0.42f), 0.05f);
            if (cobble != null)
            {
                var scale = new Vector2(size / tile, size / tile);
                m.mainTextureScale = scale;
                if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", scale);
            }

            renderer.sharedMaterial = m;
            RendererCount++;
        }

        // ── 계단 ──
        void BuildStairs(float half, float top, BattleWorldMaterials sharedStone)
        {
            ownsStone = sharedStone == null;
            stone = sharedStone ?? new BattleWorldMaterials();   // 성벽 쪽 재질 묶음을 넘겨받아 같은 재질을 두 벌 만들지 않는다
            var stepAtlas = Resources.Load<Texture2D>(Folder + "stair_step_atlas_64x16");
            var flank = Resources.Load<Texture2D>(Folder + "stair_flank_128x64");
            if (stepAtlas != null && flank != null)
            {
                BuildStairsK116(half, top, stepAtlas, flank);
                HasK116Stairs = true;
                return;
            }

            var surface = BattleWorldMaterials.Surface.WallStone;
            Vector2 tile = new Vector2(1f, 1f);
            if (stone.HasArt(surface))
            {
                var t = stone.Get(surface).mainTexture;
                tile = new Vector2(t.width / BattleWorldMaterials.PixelsPerMeter, t.height / BattleWorldMaterials.PixelsPerMeter);
            }

            var b = new MeshBuilder(tile);
            // 계단 쪽 성벽 안쪽 가장자리를 원점으로 한 자기 좌표 (z + = 통로 쪽, x = 성벽 따라 오른쪽, y = 통로 윗면 위로)
            b.Box(new Vector3(-StairHalfWidth, 0f, 0f), new Vector3(StairHalfWidth, LandingHeight, LandingDepth));   // 층계참
            for (int k = 0; k < StepCount - 1; k++)
                b.Box(new Vector3(-StairHalfWidth, 0f, -(k + 1) * TreadDepth), new Vector3(StairHalfWidth, TreadTop(k), -k * TreadDepth));

            // 양옆 낮은 돌담 (W26): 각 단을 따라 한 단씩 낮아지고, 맨 아래는 마당에 닿는 기둥으로 끝난다 — 높이 계산(HeroLift)은 그대로, 다만 계단 폭이 1.5로 넓어져 높이 보정 구간도 같이 넓어진다 (의도)
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s * StairHalfWidth, x1 = s * (StairHalfWidth + StairWallWidth);
                float xa = Mathf.Min(x0, x1), xb = Mathf.Max(x0, x1);
                // (층계참 옆에는 담을 세우지 않는다 — 통로 안쪽 끝을 걷는 주인공 발이 돌에 묻히지 않게)
                for (int k = 0; k < StepCount; k++)
                    b.Box(new Vector3(xa, 0f, -(k + 1) * TreadDepth), new Vector3(xb, (k < StepCount - 1 ? TreadTop(k) : 0f) + StairWallRise, -k * TreadDepth));
                b.Box(new Vector3(xa - 0.04f, 0f, -StepCount * TreadDepth - 0.06f), new Vector3(xb + 0.04f, StairWallRise + 0.08f, -(StepCount - 1) * TreadDepth - 0.06f));   // 맨 아래 기둥
            }

            var go = new GameObject("Courtyard Stairs");
            go.transform.SetParent(root.transform, false);
            var mesh = AroundWalls(b.ToMesh("Courtyard Stairs"), "Courtyard Stairs", half, top);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = stone.Get(surface);
            RendererCount++;
        }

        // 계단 한 벌(성벽 안쪽 가장자리 가운데 기준 자기 좌표)을 네 성벽 가운데에 돌려 놓고 한 메시로 합친다 (W35: 그리는 수는 그대로)
        Mesh AroundWalls(Mesh local, string name, float half, float top)
        {
            var parts = new CombineInstance[4];
            for (int side = 0; side < 4; side++)
            {
                StairFrame(side, out Vector3 n, out _);
                parts[side] = new CombineInstance
                {
                    mesh = local,
                    transform = Matrix4x4.TRS(n * (half - WallThickness) + Vector3.up * top, Quaternion.LookRotation(n), Vector3.one),
                };
            }

            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(parts, true, true);
            Kill(local);
            ownedMeshes.Add(mesh);
            return mesh;
        }

        // ── 계단 (K-116): 윗면·앞면은 한 장 칸 그림(stair_step_atlas)의 윗면·앞면 칸, 옆돌은 stair_flank (가로 반복, 위쪽 덮개돌 줄이 위로 오게) ──
        // 계단 쪽 성벽 안쪽 가장자리를 원점으로 한 자기 좌표 (z + = 통로 쪽, x = 성벽 따라 오른쪽, y = 통로 윗면 위로) — 기존 계단 모양 그대로
        void BuildStairsK116(float half, float top, Texture2D stepAtlas, Texture2D flank)
        {
            stepAtlas.filterMode = FilterMode.Point; stepAtlas.wrapMode = TextureWrapMode.Clamp;
            flank.filterMode = FilterMode.Point; flank.wrapModeU = TextureWrapMode.Repeat; flank.wrapModeV = TextureWrapMode.Clamp;
            Rect tread = MeshKit.Px(0, 0, 64, 10, 64, 16), riser = MeshKit.Px(0, 10, 64, 6, 64, 16);
            var steps = new MeshKit();   // 윗면·앞면 (칸 그림)
            var sides = new MeshKit();   // 옆돌 (반복 그림)
            float W = StairHalfWidth;

            // 층계참: 윗면 + 마당 쪽 앞면(0.08~0.1m) + 통로 쪽 앞면 + 옆
            TopFace(steps, -W, W, 0f, LandingDepth, LandingHeight, tread);
            FrontFace(steps, -W, W, 0f, TreadTop(0), LandingHeight, riser, true);
            FrontFace(steps, -W, W, LandingDepth, 0f, LandingHeight, riser, false);
            SideFlank(sides, -W, 0f, LandingDepth, LandingHeight, false);
            SideFlank(sides, W, 0f, LandingDepth, LandingHeight, true);

            // 계단 단 (윗면 + 아래 단과의 높이 차만큼 앞면)
            for (int k = 0; k < StepCount - 1; k++)
            {
                float z0 = -(k + 1) * TreadDepth, z1 = -k * TreadDepth;
                TopFace(steps, -W, W, z0, z1, TreadTop(k), tread);
                FrontFace(steps, -W, W, z0, k + 1 < StepCount - 1 ? TreadTop(k + 1) : 0f, TreadTop(k), riser, true);
            }

            // 양옆 낮은 돌담 (W26 모양): 한 단씩 낮아지고 맨 아래는 마당에 닿는 기둥으로 끝난다
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s * W, x1 = s * (W + StairWallWidth);
                float xa = Mathf.Min(x0, x1), xb = Mathf.Max(x0, x1);
                for (int k = 0; k < StepCount; k++)
                    WallBlock(steps, sides, xa, xb, -(k + 1) * TreadDepth, -k * TreadDepth, (k < StepCount - 1 ? TreadTop(k) : 0f) + StairWallRise, tread);
                WallBlock(steps, sides, xa - 0.04f, xb + 0.04f, -StepCount * TreadDepth - 0.06f, -(StepCount - 1) * TreadDepth - 0.06f, StairWallRise + 0.08f, tread);   // 맨 아래 기둥
            }

            SpawnMesh("Courtyard Stairs", AroundWalls(steps.ToMesh("Courtyard Stairs"), "Courtyard Stairs", half, top), MakeLit(stepAtlas, Color.gray, 0.08f));
            SpawnMesh("Courtyard Stair Walls", AroundWalls(sides.ToMesh("Courtyard Stair Walls"), "Courtyard Stair Walls", half, top), MakeLit(flank, Color.gray, 0.08f));
        }

        // 윗면: 가로로는 한 장(칸 전체), 세로(z)는 칸 그대로
        static void TopFace(MeshKit kit, float xa, float xb, float z0, float z1, float y, Rect uv)
            => kit.Face(new Vector3(xa, y, z0), new Vector3(xa, y, z1), new Vector3(xb, y, z1), new Vector3(xb, y, z0), Vector3.up, uv);

        // 앞면 (마당 쪽 = 바깥 법선 -z, 통로 쪽 = +z). 높이 yLow~yHigh 구간을 칸 그림 한 장으로
        static void FrontFace(MeshKit kit, float xa, float xb, float z, float yLow, float yHigh, Rect uv, bool towardYard)
        {
            if (yHigh - yLow < 1e-4f) return;
            if (towardYard) kit.Face(new Vector3(xa, yLow, z), new Vector3(xa, yHigh, z), new Vector3(xb, yHigh, z), new Vector3(xb, yLow, z), Vector3.back, uv);
            else kit.Face(new Vector3(xb, yLow, z), new Vector3(xb, yHigh, z), new Vector3(xa, yHigh, z), new Vector3(xa, yLow, z), Vector3.forward, uv);
        }

        // 옆돌 한 면 (x 고정, z0~z1). 높이 맨 위가 그림 맨 위(덮개돌)가 되게 위에서부터 높이만큼, 가로는 미터에 맞춰 반복
        static void SideFlank(MeshKit kit, float x, float z0, float z1, float height, bool outwardPlusX)
        {
            if (height < 1e-4f) return;
            float v0 = 1f - height / FlankTile.y;
            var uv = Rect.MinMaxRect(z0 / FlankTile.x, v0, z1 / FlankTile.x, 1f);
            if (outwardPlusX) kit.Face(new Vector3(x, 0f, z0), new Vector3(x, height, z0), new Vector3(x, height, z1), new Vector3(x, 0f, z1), Vector3.right, uv);
            else kit.Face(new Vector3(x, 0f, z1), new Vector3(x, height, z1), new Vector3(x, height, z0), new Vector3(x, 0f, z0), Vector3.left, Rect.MinMaxRect(-z1 / FlankTile.x, v0, -z0 / FlankTile.x, 1f));
        }

        // 돌담 한 칸: 윗면(칸 그림), 마주 보는 옆(안·바깥), 앞뒤 끝 (옆돌 그림)
        static void WallBlock(MeshKit steps, MeshKit sides, float xa, float xb, float z0, float z1, float height, Rect tread)
        {
            TopFace(steps, xa, xb, z0, z1, height, tread);
            SideFlank(sides, xb, z0, z1, height, true);
            SideFlank(sides, xa, z0, z1, height, false);
            float v0 = 1f - height / FlankTile.y;
            sides.Face(new Vector3(xa, 0f, z0), new Vector3(xa, height, z0), new Vector3(xb, height, z0), new Vector3(xb, 0f, z0), Vector3.back, Rect.MinMaxRect(xa / FlankTile.x, v0, xb / FlankTile.x, 1f));
            sides.Face(new Vector3(xb, 0f, z1), new Vector3(xb, height, z1), new Vector3(xa, height, z1), new Vector3(xa, 0f, z1), Vector3.forward, Rect.MinMaxRect(-xb / FlankTile.x, v0, -xa / FlankTile.x, 1f));
        }

        // ── 소품 ──
        void BuildProps(float half, float top)
        {
            var atlas = Resources.Load<Texture2D>(Folder + "props_box_atlas");   // K-116 상자·통 면 칸
            if (atlas != null)
            {
                BuildPropsK116(half, top, atlas);
                HasK116Props = true;
                return;
            }

            var crateTex = Resources.Load<Texture2D>(Folder + "crate_wood");
            var barrelTex = Resources.Load<Texture2D>(Folder + "barrel_wood");
            float y = top + SkinLift;
            StairFrame(out Vector3 sn, out Vector3 sr);   // 계단 쪽 성벽 기준 (dz = 성벽 안쪽 가장자리에서 마당 쪽으로)
            Vector3 At(float x, float dz, float lift = 0f) => sn * (half - WallThickness - dz) + sr * x + Vector3.up * (y + lift);
            float edge = half - 4f;

            var crates = new MeshBuilder(Vector2.one);
            // (가운데 x=0 줄은 비움: 계단·탑으로 가는 길)
            AddCrate(crates, At(-3.4f, 0.6f), 0.34f, 0.3f, 12f);
            AddCrate(crates, At(-3.0f, 1.1f), 0.26f, 0.24f, -20f);
            AddCrate(crates, At(-3.4f, 0.6f, 0.3f), 0.24f, 0.22f, 30f);
            AddCrate(crates, At(-edge, 0.8f), 0.34f, 0.3f, -8f);
            AddCrate(crates, new Vector3(6.2f, y, 4.6f), 0.3f, 0.28f, 15f);   // (W35: 동쪽 길(z = 0 줄)에서 비껴)
            // 무기 걸이: 기둥 둘 + 가로대 + 세운 자루 넷
            AddRack(crates, At(edge, 0.7f));

            var barrels = new MeshBuilder(Vector2.one);
            AddBarrel(barrels, At(3.5f, 0.6f), 0.15f, 0.34f);
            AddBarrel(barrels, At(3.9f, 1.05f), 0.13f, 0.3f);
            AddBarrel(barrels, new Vector3(-6.4f, y, 3.9f), 0.15f, 0.34f);   // (W35: 서쪽 길에서 비껴)
            AddBarrel(barrels, At(-edge + 0.8f, 1.5f), 0.14f, 0.32f);

            Spawn("Courtyard Crates", crates, MakeLit(crateTex, new Color(0.6f, 0.52f, 0.45f), 0.1f));
            Spawn("Courtyard Barrels", barrels, MakeLit(barrelTex, new Color(0.6f, 0.52f, 0.45f), 0.1f));
        }

        // K-116 상자·통: 상자는 칸 그림의 앞·오른쪽·뒤·왼쪽·윗면, 통은 둘레 띠 + 윗면 원판. 한 장 그림 → 한 메시
        void BuildPropsK116(float half, float top, Texture2D atlas)
        {
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;
            float y = top + SkinLift;
            StairFrame(out Vector3 sn, out Vector3 sr);
            Vector3 At(float x, float dz, float lift = 0f) => sn * (half - WallThickness - dz) + sr * x + Vector3.up * (y + lift);
            float edge = half - 4f;
            var kit = new MeshKit();
            // (가운데 x=0 줄은 비움: 계단·탑으로 가는 길)
            AddCrateK116(kit, At(-3.4f, 0.6f), 0.34f, 12f);
            AddCrateK116(kit, At(-3.0f, 1.1f), 0.26f, -20f);
            AddCrateK116(kit, At(-3.4f, 0.6f, 0.34f), 0.24f, 30f);
            AddCrateK116(kit, At(-edge, 0.8f), 0.34f, -8f);
            AddCrateK116(kit, new Vector3(6.2f, y, 4.6f), 0.3f, 15f);   // (W35: 동쪽 길(z = 0 줄)에서 비껴)
            kit.Barrel(At(3.5f, 0.6f), 0.15f, 0.34f, 10, BattleTowerInterior.Atlas.BarrelSide, BattleTowerInterior.Atlas.BarrelTop);
            kit.Barrel(At(3.9f, 1.05f), 0.13f, 0.3f, 10, BattleTowerInterior.Atlas.BarrelSide, BattleTowerInterior.Atlas.BarrelTop);
            kit.Barrel(new Vector3(-6.4f, y, 3.9f), 0.15f, 0.34f, 10, BattleTowerInterior.Atlas.BarrelSide, BattleTowerInterior.Atlas.BarrelTop);
            kit.Barrel(At(-edge + 0.8f, 1.5f), 0.14f, 0.32f, 10, BattleTowerInterior.Atlas.BarrelSide, BattleTowerInterior.Atlas.BarrelTop);
            SpawnAt("Courtyard Props", kit, MakeLit(atlas, Color.gray, 0.1f), Vector3.zero, Quaternion.identity);
        }

        static void AddCrateK116(MeshKit kit, Vector3 baseCenter, float size, float yaw)
            => kit.Box(baseCenter, new Vector3(size, size, size), yaw, BattleTowerInterior.Atlas.CrateFront, BattleTowerInterior.Atlas.CrateRight, BattleTowerInterior.Atlas.CrateBack, BattleTowerInterior.Atlas.CrateLeft, BattleTowerInterior.Atlas.CrateTop);

        // ── 데칼 + 사진기를 보는 세운 그림 (K-116 흙·짚·깨진 돌·물웅덩이, 무기 걸이·수레·화살 묶음) ──
        // 일곱 장을 한 장으로 합쳐 같은 재질을 쓴다 (읽을 수 있게 가져온 그림만 — 하나라도 없으면 모두 건너뛴다). 투명 부분은 잘라낸다
        void BuildSprites(float half, float top)
        {
            var sources = new Texture2D[SpriteNames.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = Resources.Load<Texture2D>(Folder + SpriteNames[i]);
                if (sources[i] == null || !sources[i].isReadable) return;
            }

            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Courtyard Sprites (runtime)" };
            Rect[] rects = atlas.PackTextures(sources, 2, 1024, true);   // 합친 뒤 시스템 메모리 사본은 버린다
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;
            ownedObjects.Add(atlas);
            // 투명 자르기 견본 재질(빌드에서 키워드가 빠지지 않는다)을 복사. 없으면 실행 중에 키워드를 켠다
            Material material = ClipMaterials.LitCopy("Courtyard Sprites (runtime)");
            if (material != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", atlas);
                material.mainTexture = atlas;
                ownedMaterials.Add(material);
            }
            else
            {
                material = MakeLit(atlas, Color.gray, 0.05f);
                material.name = "Courtyard Sprites (runtime)";
                if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);   // 세운 그림은 양면
                material.EnableKeyword("_ALPHATEST_ON");
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            // 데칼: 깨끗한 가운데(탑 둘레 광장·돌길·계단)를 피해 듬성듬성. 같은 걸음으로 격자 배치하지 않는다
            StairFrame(out Vector3 n, out Vector3 r);
            var decals = new MeshKit();
            float roadClear = BattleRoad.HalfWidth + BattleRoad.KerbWidth + 2.2f;   // 길·등불 기둥·계단 줄 (네 성벽 모두)
            var rng = new System.Random(20261023);
            float limit = half - WallThickness - 0.8f;
            float y = top + SkinLift + DecalLift;
            int[] kinds = { SpriteDirt, SpriteDirt, SpriteStraw, SpriteDirt, SpriteStraw, SpriteCracked, SpritePuddle, SpriteStraw, SpriteCracked, SpriteDirt, SpritePuddle, SpriteStraw, SpriteDirt, SpriteCracked, SpriteStraw, SpriteDirt };
            int placed = 0;
            var placedRects = new List<(float x, float z, float w, float d, int level)>();
            for (int attempt = 0; attempt < 600 && placed < DecalCount; attempt++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * limit, z = ((float)rng.NextDouble() * 2f - 1f) * limit;
                var p = new Vector3(x, 0f, z);
                if (p.magnitude < BattleRoad.PlazaRimRadius + 1.2f) continue;                        // 탑 광장
                if (OnAnyRoad(p, roadClear)) continue;                                               // 돌길·등불 기둥·계단 (십자)
                int kind = kinds[placed % kinds.Length];
                if (kind == SpritePuddle && Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < limit - 5f) continue;   // 물웅덩이는 가장자리 낮은 곳에만
                Texture2D src = sources[kind];
                float w = src.width * PropScale, d = src.height * PropScale;
                int turns = rng.Next(4);
                if (turns % 2 == 1) { float swap = w; w = d; d = swap; }   // 90도 돌리면 가로세로가 바뀐다
                // 겹친 데칼은 같은 높이면 겹친 곳이 깜박인다 (W41): 먼저 놓은 겹친 데칼보다 한 층(1cm) 높게. 겹치지 않으면 층 0 (모양·자리는 그대로)
                int level = 0;
                foreach (var (px, pz, pw, pd, plevel) in placedRects)
                    if (Mathf.Abs(x - px) < (w + pw) * 0.5f && Mathf.Abs(z - pz) < (d + pd) * 0.5f) level = Mathf.Max(level, plevel + 1);
                placedRects.Add((x, z, w, d, level));
                decals.Flat(new Vector3(x, y + level * DecalLayer, z), src.width * PropScale, src.height * PropScale, turns, rects[kind]);
                placed++;
            }

            DecalsPlaced = placed;
            SpawnAt("Courtyard Decals", decals, material, Vector3.zero, Quaternion.identity);

            // 세운 그림: 무기 걸이·화살 묶음은 계단 쪽 성벽 오른쪽 끝, 수레는 왼쪽 끝. 바닥 접지선은 그림 높이 − 2화소 (원본 한 칸 아래 2화소가 투명)
            float edge = half - 4f;
            Vector3 At(float x, float dz) => n * (half - WallThickness - dz) + r * x + Vector3.up * (top + SkinLift);
            AddBillboard("Courtyard Weapon Rack", material, sources[SpriteRack], rects[SpriteRack], At(edge, 0.7f));
            AddBillboard("Courtyard Arrow Bundles", material, sources[SpriteArrows], rects[SpriteArrows], At(edge - 1.3f, 0.9f));
            AddBillboard("Courtyard Cart", material, sources[SpriteCart], rects[SpriteCart], At(-edge + 2.4f, 1.5f));
        }

        // 가운데에서 네 성벽으로 뻗은 길 띠(가운데에서 옆으로 clear 안, 성벽 쪽)에 드는가
        public static bool OnAnyRoad(Vector3 p, float clear)
        {
            for (int side = 0; side < 4; side++)
            {
                StairFrame(side, out Vector3 n, out Vector3 r);
                if (Vector3.Dot(p, n) > 0f && Mathf.Abs(Vector3.Dot(p, r)) < clear) return true;
            }

            return false;
        }

        void AddBillboard(string name, Material material, Texture2D src, Rect uv, Vector3 feet)
        {
            var kit = new MeshKit();
            kit.Standing(src.width * PropScale, src.height * PropScale, uv);
            var go = SpawnAt(name, kit, material, feet + Vector3.down * (2f * PropScale), Quaternion.identity);
            go.AddComponent<CourtyardBillboard>();
            BillboardCount++;
        }

        void SpawnMesh(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            RendererCount++;
        }

        GameObject SpawnAt(string name, MeshKit kit, Material material, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            var mesh = kit.ToMesh(name);
            ownedMeshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            RendererCount++;
            return go;
        }

        void Spawn(string name, MeshBuilder builder, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = builder.ToMesh(name);
            ownedMeshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            RendererCount++;
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);   // 편집 모드 시험
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
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
                m.mainTexture = texture;
            }

            ownedMaterials.Add(m);
            return m;
        }

        public void Dispose()
        {
            Kill(root);
            foreach (Material m in ownedMaterials) Kill(m);
            foreach (Mesh m in ownedMeshes) Kill(m);
            foreach (UnityEngine.Object o in ownedObjects) Kill(o);
            ownedMaterials.Clear();
            ownedMeshes.Clear();
            ownedObjects.Clear();
            if (ownsStone) stone?.Dispose();
        }

        // 상자·원통을 한 메시로 합친다. uvTile = 그림 한 장이 덮는 미터 (돌 질감은 면 크기에 맞춰 반복, 나무는 면마다 한 장)
        internal sealed class MeshBuilder
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

            // 낮은 원판 (W26 광장): 바닥 높이 y0에서 y1까지, 윗면 + 옆면. 윗면 그림은 위에서 본 미터 좌표대로 깐다
            public void Disc(Vector3 center, float radius, float y0, float y1, int segments)
            {
                float tu = meters ? 1f / tile.x : 1f, tv = meters ? 1f / tile.y : 1f;
                int c = verts.Count;
                verts.Add(new Vector3(center.x, y1, center.z)); normals.Add(Vector3.up); uvs.Add(new Vector2(center.x * tu, center.z * tv));
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    var p = new Vector3(center.x + Mathf.Cos(a) * radius, y1, center.z + Mathf.Sin(a) * radius);
                    verts.Add(p); normals.Add(Vector3.up); uvs.Add(new Vector2(p.x * tu, p.z * tv));
                }

                for (int i = 0; i < segments; i++) tris.AddRange(new[] { c, c + 2 + i, c + 1 + i });

                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    int s = verts.Count;
                    verts.Add(center + d0 * radius + Vector3.up * y0); verts.Add(center + d1 * radius + Vector3.up * y0);
                    verts.Add(center + d1 * radius + Vector3.up * y1); verts.Add(center + d0 * radius + Vector3.up * y1);
                    normals.Add(d0); normals.Add(d1); normals.Add(d1); normals.Add(d0);
                    float u = (a0 * radius) * tu, w = (a1 * radius) * tu, v = (y1 - y0) * tv;
                    uvs.Add(new Vector2(u, 0f)); uvs.Add(new Vector2(w, 0f)); uvs.Add(new Vector2(w, v)); uvs.Add(new Vector2(u, v));
                    tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });   // 바깥쪽에서 볼 때 시계 방향
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
