using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 가운데 높은 등대 탑 (W20 임시 → W27 Codex K-115 그림 연결). 키는 성벽 높이의 3배
    // - 몸체: K-115 석벽·띠·지붕 재질(32화소 = 1m, 면마다 미터 단위 UV라 늘어나지 않는다)로 쌓은 아래층(열린 문 구멍 + 안쪽 방) + 위로 좁아지는 탑 + 띠 셋 + 둘레길 + 귀퉁이 돌 + 불방 기둥 + 지붕
    // - 아래층 속(BattleTowerInterior): 방 안쪽 벽·나선 돌계단·기름통·석탄 자루·연장. 문은 늘 열려 있다 (문 그림의 나무 문짝을 뚫어 놓은 사본) — 사진기에서 돌림대와 일꾼이 보인다
    // - 불방: 모서리 돌기둥(몸체 메시에 포함) + 사방 창틀·철창 그림 + 안쪽 따뜻한 번짐. 불꽃·화로 그림은 BattleBeaconView가 가운데에 세운다
    // - 상태: 꺼짐(dead) = 등대 복구 0 (BeaconRules.TowerFlameStrength 최솟값) — 꺼진 창틀·문·화로·불꽃 없음. 그 밖에는 점등(lit) 그림. 불꽃 세기는 BeaconView가 꺼짐·약함·보통·강함으로 고른다
    // - 합쳐 그리기 8~9번, 프레임마다 새로 만드는 것 없음 (밝기·상태는 재질 색·그림만 바꾼다)
    // 빛줄기·탑 불빛·불꽃은 모두 LampHeight(불방 가운데 높이)에서 나간다 (BattleBeamView.OriginHeight, BattleLights, BattleBeaconView)
    public sealed class BattleTowerView : IDisposable
    {
        public const float Height = 3f * BattleSim.WallHeight;   // 마당 윗면 위로 탑 전체 키 (성벽 높이의 3배)
        public const float BaseHalf = 1.1f;                      // 아래 반폭 (병사 전령이 곁에 서는 자리가 이 밖이어야 한다)
        public const float DoorZ = -BaseHalf;                    // 문이 있는 면 (남쪽)
        const float TopHalf = 0.78f;
        public const float GroundHeight = 2.8f;                  // 아래층(속이 빈 방) 높이. 그 위는 위로 좁아지는 통짜 탑
        const float GalleryY = 0.72f * Height;                   // 둘레길 바닥
        public const float LampRoomHeight = 1.0f;                // 불방 키 (창틀 그림 높이에서 위 띠를 뺀 값)
        public const float LampBase = GalleryY + 0.3f;           // 불방 바닥 (둘레길 윗면)
        public const float LampFloor = GalleryY + 0.34f;         // 불방 바닥 높이 (불꽃이 서는 자리)
        public const float LampHeight = LampBase + LampRoomHeight * 0.5f;   // 마당 윗면 위 불방 가운데 높이 (빛이 나가는 자리)
        public const float LampOuterHalf = 0.82f;                // 불방 바깥 반폭 (창틀 그림 88화소 = 1.64m)
        // K-115 불방 부품 그림은 원본 96×128 칸, 창틀 폭 88화소를 불방 바깥 폭에 맞춘다. 원본 한 화소 = 이만큼(m)
        public const float SpriteMeters = LampOuterHalf * 2f / 88f;
        public const float SpriteCellMeters = 128f * SpriteMeters;   // 칸 높이 (스프라이트 크기 인자)
        public const float BrazierLift = 7f * SpriteMeters;          // 창틀 받침에서 화로 받침까지 (조립 칸 (48,113))
        public const float FlameLift = 21f * SpriteMeters;           // 창틀 받침에서 불꽃 바닥까지 (조립 칸 (48,99))
        // K-115 석벽·띠·지붕 그림은 어두운 색(평균 명도 약 0.25)이라 Codex 목업 낮 사진(돌 평균 약 0.45)에 맞춰 밝게 곱한다
        const float WallBrightness = 1.7f, TrimBrightness = 2.0f, RoofBrightness = 1.8f;
        const float DoorSpriteMeters = 0.0216f;                      // 문 그림 한 화소 (구멍 폭 38화소 = 0.82m)
        public const float DeadStrength = 0.36f;                     // 이보다 약하면 꺼진 탑 (BeaconRules.TowerFlameStrength 최솟값 0.35 = 복구 0)

        // 불꽃 세기 → 그림 단계: 0 약함(warming), 1 보통(lit), 2 강함(bright). 꺼진 탑은 -1 (불꽃 없음)
        public static bool IsDead(float flameStrength) => flameStrength < DeadStrength;
        public static int FlameKind(float flameStrength) => IsDead(flameStrength) ? -1 : flameStrength < 0.55f ? 0 : flameStrength < 0.84f ? 1 : 2;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        readonly GameObject root;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Material lampMaterial;          // 불방 안 번짐 (K-115 lamp_interior_glow, 없으면 만든 그라디언트)
        readonly Material windowMaterial, doorMaterial;   // 창틀·문 (그림만 꺼짐/점등으로 바꾼다)
        readonly Texture2D windowDead, windowLit, doorDead, doorLit;
        readonly Renderer glowRenderer;
        readonly bool glowIsSprite;
        BattleWorldMaterials stone;
        bool ownsStone;
        bool dead;

        // 시험·확인용
        public Vector3 LampWorld => new Vector3(0f, BattleSim.WallHeight + LampHeight, 0f);
        public int RendererCount { get; private set; }
        public bool HasStoneArt { get; private set; }
        public bool HasTowerArt { get; private set; }        // K-115 석벽·띠·지붕·창·문 그림이 모두 붙었는가
        public bool Dead => dead;
        public Texture WindowTexture => windowMaterial != null ? windowMaterial.mainTexture : null;
        public Texture DoorTexture => doorMaterial != null ? doorMaterial.mainTexture : null;
        public Material WallMaterial { get; private set; }
        public Material TrimMaterial { get; private set; }
        public Material RoofMaterial { get; private set; }

        public BattleTowerView(Transform parent, BattleWorldMaterials sharedStone = null)
        {
            root = new GameObject("Beacon Tower (W27)");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, BattleSim.WallHeight, 0f);

            ownsStone = sharedStone == null;
            stone = sharedStone ?? new BattleWorldMaterials();
            var surface = BattleWorldMaterials.Surface.WallStone;
            HasStoneArt = stone.HasArt(surface);

            // K-115 재질 (없으면 예전처럼 성벽 돌로 쌓는다)
            Texture2D wallTex = Load("tower_wall_albedo", true, true), trimTex = Load("tower_trim_albedo", true, false), roofTex = Load("tower_roof_albedo", true, true);
            Material wallMat, trimMat, roofMat;
            Vector2 wallTile, trimTile, roofTile;
            if (wallTex != null)
            {
                wallMat = MakeLit("Tower Wall (runtime)", wallTex, 0.1f, null, WallBrightness);
                wallTile = new Vector2(wallTex.width / BattleWorldMaterials.PixelsPerMeter, wallTex.height / BattleWorldMaterials.PixelsPerMeter);   // 한 장 8×8m
            }
            else
            {
                wallMat = stone.Get(surface);
                wallTile = HasStoneArt ? TileOf(wallMat) : Vector2.one;
            }

            trimMat = trimTex != null ? MakeLit("Tower Trim (runtime)", trimTex, 0.12f, null, TrimBrightness) : wallMat;
            trimTile = trimTex != null ? new Vector2(trimTex.width / BattleWorldMaterials.PixelsPerMeter, trimTex.height / BattleWorldMaterials.PixelsPerMeter) : wallTile;   // 한 장 8×2m
            roofMat = roofTex != null ? MakeLit("Tower Roof (runtime)", roofTex, 0.2f, null, RoofBrightness) : wallMat;
            roofTile = roofTex != null ? new Vector2(roofTex.width / BattleWorldMaterials.PixelsPerMeter, roofTex.height / BattleWorldMaterials.PixelsPerMeter) : wallTile;   // 한 장 4×4m
            WallMaterial = wallMat; TrimMaterial = trimMat; RoofMaterial = roofMat;

            var wall = new Builder(wallTile);
            var trim = trimTex != null ? new Builder(trimTile, true) : wall;
            var roof = roofTex != null ? new Builder(roofTile) : wall;

            // ── 아래층: 받침 + 네 벽(남쪽에는 열린 문 구멍) + 천장 ──
            const float f = BattleTowerInterior.FloorY, t = BattleTowerInterior.WallThickness, inner = BattleTowerInterior.InnerHalf;
            float open = BattleTowerInterior.OpeningHalf, openTop = BattleTowerInterior.OpeningTop;
            wall.Box(new Vector3(-BaseHalf - 0.15f, 0f, -BaseHalf - 0.15f), new Vector3(BaseHalf + 0.15f, f, BaseHalf + 0.15f));   // 받침 한 단 (윗면 = 방 바닥)
            wall.Box(new Vector3(-BaseHalf, f, inner), new Vector3(BaseHalf, GroundHeight, BaseHalf));                               // 북
            wall.Box(new Vector3(inner, f, -inner), new Vector3(BaseHalf, GroundHeight, inner));                                      // 동
            wall.Box(new Vector3(-BaseHalf, f, -inner), new Vector3(-inner, GroundHeight, inner));                                    // 서
            wall.Box(new Vector3(-BaseHalf, f, -BaseHalf), new Vector3(-open, GroundHeight, -inner));                                 // 남: 문 구멍 왼쪽
            wall.Box(new Vector3(open, f, -BaseHalf), new Vector3(BaseHalf, GroundHeight, -inner));                                   // 남: 문 구멍 오른쪽
            wall.Box(new Vector3(-open, openTop, -BaseHalf), new Vector3(open, GroundHeight, -inner));                                // 남: 문 구멍 위(이마돌)
            wall.Box(new Vector3(-inner, GroundHeight - 0.1f, -inner), new Vector3(inner, GroundHeight, inner));                      // 천장 (아랫면이 방 안에서 보인다)
            BattleTowerInterior.BuildStairSteps(wall);

            // ── 위층: 위로 좁아지는 통짜 탑 + 띠 셋 ──
            float shaftTop = GalleryY;
            wall.Frustum(GroundHeight, BaseHalf, shaftTop, TopHalf);
            foreach (float bandY in new[] { GroundHeight - 0.12f, 0.43f * Height, GalleryY - 0.5f })   // 처마돌·가운데 띠·둘레길 밑 띠
            {
                float h = HalfAt(bandY) + 0.1f;
                trim.Box(new Vector3(-h, bandY, -h), new Vector3(h, bandY + 0.42f, h));
            }

            float g = TopHalf + 0.4f;
            trim.Box(new Vector3(-g, GalleryY, -g), new Vector3(g, GalleryY + 0.3f, g));                                    // 둘레길 바닥
            foreach (int sx in new[] { -1, 1 })                                                                              // 귀퉁이 돌출부
                foreach (int sz in new[] { -1, 1 })
                    wall.Box(new Vector3(sx * g - 0.14f, GalleryY + 0.3f, sz * g - 0.14f), new Vector3(sx * g + 0.14f, GalleryY + 0.62f, sz * g + 0.14f));

            // ── 불방: 모서리 기둥 (창틀 그림 양옆 돌기둥 폭 0.37m) + 위 띠 + 지붕 ──
            float lampTop = LampBase + LampRoomHeight, rimTop = lampTop + 18f * SpriteMeters;
            const float pillar = 0.37f;
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    wall.Box(new Vector3(sx > 0 ? LampOuterHalf - pillar : -LampOuterHalf, LampBase, sz > 0 ? LampOuterHalf - pillar : -LampOuterHalf),
                        new Vector3(sx > 0 ? LampOuterHalf : -LampOuterHalf + pillar, lampTop, sz > 0 ? LampOuterHalf : -LampOuterHalf + pillar));
            trim.Box(new Vector3(-LampOuterHalf, lampTop, -LampOuterHalf), new Vector3(LampOuterHalf, rimTop - 0.02f, LampOuterHalf));        // 불방 지붕 테 (윗면은 처마 윗면보다 2cm 낮게 — 같은 높이로 겹쳐 깜박이지 않게, 처마 속이라 안 보인다. W41)
            roof.Box(new Vector3(-LampOuterHalf - 0.14f, rimTop - 0.07f, -LampOuterHalf - 0.14f), new Vector3(LampOuterHalf + 0.14f, rimTop, LampOuterHalf + 0.14f));   // 처마 (아랫면)
            roof.Frustum(rimTop, LampOuterHalf + 0.14f, Height - 0.28f, 0.14f);                                              // 뾰족 지붕
            wall.Box(new Vector3(-0.05f, Height - 0.28f, -0.05f), new Vector3(0.05f, Height, 0.05f));                       // 꼭대기 꽂이

            Spawn("Tower Stone", wall, wallMat);
            if (trim != wall) Spawn("Tower Trim", trim, trimMat);
            if (roof != wall) Spawn("Tower Roof", roof, roofMat);

            // ── 아래층 속 소품 ──
            var atlasTex = Resources.Load<Texture2D>("BattleArt/Courtyard/props_box_atlas");
            var atlasKit = new MeshKit();
            var darkKit = new MeshKit();
            BattleTowerInterior.BuildProps(atlasKit, darkKit);
            if (atlasTex != null) Spawn("Tower Interior Props", atlasKit, MakeLit("Tower Props (runtime)", atlasTex, 0.1f));
            Spawn("Tower Interior Dark", darkKit, MakeLit("Tower Dark Props (runtime)", null, 0.05f, new Color(0.1f, 0.09f, 0.08f)));

            // ── 불방 속: 안쪽을 보는 상자 + 따뜻한 번짐 (K-115 lamp_interior_glow, 반투명) ──
            var glowTex = Resources.Load<Texture2D>("BattleArt/Tower/lamp_interior_glow");
            var glow = new Builder(Vector2.one);
            float gi = LampOuterHalf - pillar;   // 기둥 사이 안쪽
            glow.BoxInside(new Vector3(-gi, LampBase + 0.04f, -gi), new Vector3(gi, lampTop - 0.02f, gi));
            glowIsSprite = glowTex != null;
            if (glowIsSprite)
            {
                glowTex.filterMode = FilterMode.Bilinear;
                glowTex.wrapMode = TextureWrapMode.Clamp;
                var sprite = Shader.Find("Sprites/Default");
                lampMaterial = new Material(sprite != null ? sprite : Shader.Find("Universal Render Pipeline/Unlit")) { name = "Tower Lamp Glow (runtime)" };
                lampMaterial.mainTexture = glowTex;
                lampMaterial.color = Color.white;
                ownedMaterials.Add(lampMaterial);
            }
            else
            {
                lampMaterial = MakeUnlit("Tower Lamp (runtime)", Color.white);
                var gradient = MakeGlowTexture();
                lampMaterial.SetTexture(BaseMapId, gradient);
                lampMaterial.mainTexture = gradient;
            }

            glowRenderer = Spawn("Tower Lamp", glow, lampMaterial);

            // ── 불방 창틀·철창 (사방) + 열린 문 ──
            windowDead = Load("lamp_window_dead", false, false); windowLit = Load("lamp_window_lit", false, false);
            var doorDeadSource = Load("door_dead", false, false); var doorLitSource = Load("door_lit", false, false);
            doorDead = CarveDoor(doorDeadSource, "Tower Door Dead (runtime)"); doorLit = CarveDoor(doorLitSource, "Tower Door Lit (runtime)");
            if (windowLit != null)
            {
                var kit = new MeshKit();
                float s = SpriteMeters, y0 = LampBase - 8f * s, y1 = y0 + 128f * s;
                foreach (Vector3 n in new[] { new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), Vector3.right, Vector3.left })
                    SpriteFace(kit, n, LampOuterHalf + 0.012f, -48f * s, 48f * s, y0, y1);
                windowMaterial = MakeClip("Tower Window (runtime)", windowLit);
                Spawn("Tower Windows", kit, windowMaterial);
            }

            if (doorLit != null)
            {
                var kit = new MeshKit();
                float s = DoorSpriteMeters, y0 = f - 8f * s;
                SpriteFace(kit, new Vector3(0f, 0f, -1f), BaseHalf + 0.02f, -43f * s, 53f * s, y0, y0 + 128f * s);   // 문 구멍 가운데(그림 x 43)가 탑 가운데
                doorMaterial = MakeClip("Tower Door (runtime)", doorLit);
                Spawn("Tower Door", kit, doorMaterial);
            }

            HasTowerArt = wallTex != null && trimTex != null && roofTex != null && windowLit != null && windowDead != null && doorLit != null && doorDead != null && glowIsSprite;
            dead = false;
        }

        static Texture2D Load(string name, bool repeatU, bool repeatV)
        {
            var tex = Resources.Load<Texture2D>("BattleArt/Tower/" + name);
            if (tex == null) return null;
            if (tex.filterMode != FilterMode.Point) tex.filterMode = FilterMode.Point;   // 가져오기 설정이 빠져도 도트 그대로
            tex.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return tex;
        }

        static Vector2 TileOf(Material m)
        {
            var t = m.mainTexture;
            return t != null ? new Vector2(t.width / BattleWorldMaterials.PixelsPerMeter, t.height / BattleWorldMaterials.PixelsPerMeter) : Vector2.one;
        }

        // 바깥에서 본 오른쪽(r) 방향으로 폭이 퍼지는 세운 그림 한 장 (n = 바깥 법선). xLeft·xRight = 가운데에서 왼·오른쪽 거리
        static void SpriteFace(MeshKit kit, Vector3 n, float dist, float xLeft, float xRight, float y0, float y1)
        {
            Vector3 r = Vector3.Cross(Vector3.up, -n);   // 바깥에서 탑을 바라볼 때 오른쪽
            Vector3 o = n * dist;
            kit.Face(o + r * xLeft + Vector3.up * y0, o + r * xLeft + Vector3.up * y1, o + r * xRight + Vector3.up * y1, o + r * xRight + Vector3.up * y0, n, new Rect(0f, 0f, 1f, 1f));
        }

        // 문 그림(원본 96×128)의 나무 문짝 자리를 투명하게 뚫은 사본: 둥근 아치 구멍 (x 25~61, 위 반원 중심 (43, 44) 반지름 19, 아래는 바닥 y 118까지)
        // 읽을 수 없는 그림(가져오기 설정 없음)이면 뚫지 않고 그대로 쓴다
        Texture2D CarveDoor(Texture2D source, string name)
        {
            if (source == null) return null;
            if (!source.isReadable || source.width != 96 || source.height != 128) return source;
            Color32[] px = source.GetPixels32();
            for (int y = 0; y < 128; y++)
                for (int x = 25; x <= 61; x++)
                {
                    bool inside = y >= 44 ? y <= 118 : (x - 43) * (x - 43) + (y - 44) * (y - 44) <= 19 * 19;
                    if (!inside) continue;
                    int i = (127 - y) * 96 + x;   // 유니티는 맨 아래 줄이 0번
                    px[i] = new Color32(0, 0, 0, 0);
                }

            var tex = new Texture2D(96, 128, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            ownedObjects.Add(tex);
            return tex;
        }

        // 가운데가 밝고 가장자리로 어두워지는 따뜻한 번짐 (K-115 그림이 없을 때의 대비책). 한 번만 만든다
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
        static float HalfAt(float y) => Mathf.Lerp(BaseHalf, TopHalf, Mathf.InverseLerp(GroundHeight, GalleryY, y));

        // 상태·밝기: 꺼진 탑은 꺼진 창틀·문 그림, 아니면 점등 그림 (바뀔 때만 그림을 바꾼다). 밤에는 등불 밝기(level)에 맞춰 환하고 낮에는 어둡게. 재질 색만 바꾼다 (새로 만드는 것 없음)
        public void Tick(bool night, float level, float flameStrength)
        {
            bool nowDead = IsDead(flameStrength);
            if (nowDead != dead || windowMaterial != null && windowMaterial.mainTexture == null)
            {
                dead = nowDead;
                SetTexture(windowMaterial, dead ? windowDead : windowLit);
                SetTexture(doorMaterial, dead ? doorDead : doorLit);
            }

            if (lampMaterial == null) return;
            float k = night ? Mathf.Lerp(0.8f, 1.4f, Mathf.Clamp01(flameStrength)) * Mathf.Max(0.3f, level) : 0.55f;
            if (glowRenderer != null) glowRenderer.enabled = !dead;   // 꺼진 탑의 불방 속은 어둡다
            Color c = glowIsSprite ? new Color(1f, 1f, 1f, night ? Mathf.Clamp01(0.55f + 0.3f * k) : 0.45f) : Color.white * k;
            if (!glowIsSprite) c.a = 1f;
            lampMaterial.SetColor(BaseColorId, c);
            lampMaterial.SetColor(ColorId, c);

            // 창틀·문은 빛을 받지 않는 그림이라 낮·밤 색을 직접 준다 (돌 벽이 밤에 어두워지는 만큼)
            float v = night ? (dead ? 0.34f : Mathf.Lerp(0.5f, 0.78f, Mathf.Clamp01(level))) : (dead ? 0.74f : 0.88f);
            Color tint = night ? new Color(v * 0.9f, v * 0.95f, v * 1.1f, 1f) : new Color(v, v, v, 1f);
            if (windowMaterial != null) { windowMaterial.SetColor(BaseColorId, tint); windowMaterial.SetColor(ColorId, tint); }
            if (doorMaterial != null) { doorMaterial.SetColor(BaseColorId, tint); doorMaterial.SetColor(ColorId, tint); }
        }

        static void SetTexture(Material m, Texture2D tex)
        {
            if (m == null || tex == null) return;
            m.SetTexture(BaseMapId, tex);
            m.mainTexture = tex;
        }

        Renderer Spawn(string name, Builder builder, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = builder.ToMesh(name);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            RendererCount++;
            return r;
        }

        Renderer Spawn(string name, MeshKit kit, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = kit.ToMesh(name);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            RendererCount++;
            return r;
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

        // 빛을 받는 재질 (석벽·띠·지붕·나무 소품)
        Material MakeLit(string name, Texture2D texture, float smoothness, Color? fallback = null, float brightness = 1f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = name };
            Color tint = texture != null ? new Color(brightness, brightness, brightness, 1f) : fallback ?? Color.gray;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, tint);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, tint);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (texture != null)
            {
                if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, texture);
                m.mainTexture = texture;
            }

            ownedMaterials.Add(m);
            return m;
        }

        // 빛 없는 그림 + 투명 부분 자르기 (창틀의 빈 칸·문 구멍·아치 바깥). 그림 가장자리는 완전 투명이라 0.5 기준으로 끊어도 된다
        Material MakeClip(string name, Texture2D texture)
        {
            var m = ClipMaterials.UnlitCopy(name);   // 빌드에서 키워드가 빠지지 않게 견본 재질을 복사한다
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = name };
                if (m.HasProperty(AlphaClipId)) m.SetFloat(AlphaClipId, 1f);
                if (m.HasProperty(CutoffId)) m.SetFloat(CutoffId, 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
            }

            SetTexture(m, texture);
            m.SetColor(BaseColorId, Color.white);
            m.renderQueue = (int)RenderQueue.AlphaTest;
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
        // trim: 띠 그림(8×2m)은 가로만 반복 — 띠 높이만큼 위쪽 줄을 잘라 쓴다 (세로로 늘리지 않는다)
        internal sealed class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();
            readonly Vector2 tile;
            readonly bool meters;
            readonly bool trim;

            public Builder(Vector2 tile, bool trim = false)
            {
                this.tile = tile;
                this.trim = trim;
                meters = tile != Vector2.one;
            }

            public void Box(Vector3 min, Vector3 max)
            {
                Vector3 s = max - min;
                Quad(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.forward, s.x, s.y, false);
                Quad(new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.back, s.x, s.y, false);
                Quad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Vector3.right, s.z, s.y, false);
                Quad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Vector3.left, s.z, s.y, false);
                Quad(new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.up, s.x, s.z, true);
                Quad(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.down, s.x, s.z, true);   // 아랫면 (둘레길·띠·지붕 테를 아래에서 볼 때)
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
                Quad(new Vector3(h0, y0, -h0), new Vector3(-h0, y0, -h0), new Vector3(-h1, y1, -h1), new Vector3(h1, y1, -h1), new Vector3(0f, ny, -nh), w, len, false);
                // 북(+z)
                Quad(new Vector3(-h0, y0, h0), new Vector3(h0, y0, h0), new Vector3(h1, y1, h1), new Vector3(-h1, y1, h1), new Vector3(0f, ny, nh), w, len, false);
                // 동(+x)
                Quad(new Vector3(h0, y0, h0), new Vector3(h0, y0, -h0), new Vector3(h1, y1, -h1), new Vector3(h1, y1, h1), new Vector3(nh, ny, 0f), w, len, false);
                // 서(-x)
                Quad(new Vector3(-h0, y0, -h0), new Vector3(-h0, y0, h0), new Vector3(-h1, y1, h1), new Vector3(-h1, y1, -h1), new Vector3(-nh, ny, 0f), w, len, false);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float width, float height, bool horizontal)
            {
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                float u = meters ? width / tile.x : 1f, v = meters ? height / tile.y : 1f;
                float ou = meters ? (a.x + a.z) / tile.x : 0f, ov = meters ? a.y / tile.y : 0f;
                if (trim && meters)
                {
                    // 띠: 세로는 잘라 쓴다. 옆면은 위쪽 줄(돌 한 줄 + 줄눈)을 띠 높이만큼, 윗·아랫면은 위쪽 돌 한 줄 모양
                    v = horizontal ? 0.16f : Mathf.Min(1f, height / tile.y);
                    ov = 1f - v;
                }

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
