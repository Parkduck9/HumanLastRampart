using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 계단 → 등대 탑 의식용 돌길 (W26, 사용자 2026-10-23 "등대로 가는 길이 너무 초라하다"). W35: 네 성벽 모두 계단과 길이 있어 십자 모양으로 광장에 모인다
    // - 돌길: 계단 아래에서 탑 광장까지 조금 밝은 다듬은 돌 길 + 양쪽 연석(작은 돌 덩이를 틈 두고 줄 세움)
    // - 광장: 탑 밑동을 두른 둥근 돌바닥 + 낮은 돌 단 하나
    // - 등불 기둥 (K-122): 길 양쪽 4m마다 사진기를 보는 세운 그림. 낮엔 꺼진 쇠 기둥, 밤엔 불꽃 여섯 칸 그림. 땅 번짐(GroundGlow)만 쓴다 — 셰이더 빛 칸은 쓰지 않는다 (PC 14·모바일 6 상한 그대로)
    // - 깃발 (K-122): 탑 문 곁에 등불지기 깃발 두 개, 네 칸 펄럭임 그림
    // 전부 눈에 보이는 것만 — 발 높이·병사 길·전령 길·사진기는 건드리지 않는다 (돌길 윗면 +3cm, 광장 +4cm로 아주 낮게)
    // 그림: Resources/BattleArt/Courtyard/road_paving 이 있으면 그것을, 없으면 통로 돌(walkway_stone)을 밝게 쓴다 (Codex K-116이 오면 그림만 바꾼다)
    // 그리는 수: 돌길(네 갈래 합침) 1 + 연석·광장 단 1 + 등불 기둥 하나당 1 + 깃발 하나당 1 (재질은 낮 1·밤 2·깃발 2로 나눠 쓴다)
    public sealed class BattleRoad : IDisposable
    {
        const string Folder = "BattleArt/Courtyard/";
        const float SkinLift = 0.01f;                     // 마당 바닥(BattleCourtyard)과 같은 기준

        public const float HalfWidth = 1.1f;              // 돌길 반폭 (주인공 0.375m 기준 넉넉하게)
        public const float KerbWidth = 0.2f;
        // W41: 겹치는 윗면은 서로 1cm 이상 층을 둔다 (바닥 1 < 광장 아랫단 2 < 돌길 3 < 광장 윗단 4 < 땅 번짐 5cm). 5mm 층은 먼 곳에서 깜박였다
        public const float PavingLift = 0.03f;            // 돌길 윗면 (마당 바닥 위) — 주인공 발이 거의 안 묻힌다
        public const float KerbLift = 0.08f;
        public const float PlazaRadius = 3.0f;            // 탑 둘레 광장 윗단 반지름 (탑 받침 1.25m 밖)
        public const float PlazaRimRadius = 3.5f;         // 낮은 아랫단
        public const float PlazaLift = 0.04f, PlazaRimLift = 0.02f;   // GroundGlow 판(바닥 +5cm)보다 낮게 — 탑·문 앞 웅덩이가 광장에 안 묻힌다
        public const int RoadCount = 4;                   // W35: 네 성벽 가운데에서 광장으로 뻗은 십자 길
        public const float LampSpacing = 10f;             // 길 하나당 기둥 5~6쌍 (사용자 2026-10-23). 땅 번짐은 PC 14·모바일 4개 상한이라 사진기에 닿는 기둥만 쓴다
        public const float RoadBannerDistance = PlazaRimRadius + 1.2f;   // 길마다 광장 가장자리 바로 앞에 깃발 한 쌍
        public const float LampOffset = HalfWidth + KerbWidth + 0.3f;   // 길 가운데에서 기둥까지 (연석 바깥)
        public const float LampPostHeight = 0.9f;            // K-122 등불 기둥 보이는 높이 (그림 51화소 = 0.9m, 병사 키 0.35m의 약 2.5배)
        const string RoadFolder = "BattleArt/Road/";         // K-122 기둥·깃발 그림 (96×128 칸 ×3배, 발 (144,360))
        public const float LampVisibleMeters = LampPostHeight, BannerVisibleMeters = 1.5f;   // 깃발은 예전 깃대 1.5m 그대로
        const float LampVisiblePixels = 51f, BannerVisiblePixels = 58f;                      // manifest visible_height_native
        const int LampFrames = 6, BannerFrames = 4;
        const float LampFps = 10f, BannerFps = 4f;
        public const float LampPoolRadius = 1.8f;
        const float KerbBlock = 0.76f, KerbGap = 0.04f;
        const float RoadCobbleTileMeters = 1.7f;           // 돌길 그림 한 장이 덮는 미터 (마당 바닥 2.6m보다 촘촘하게)

        // 깃발 자리 (탑 문 곁, 몸체 받침 바깥 · 주인공 서는 줄(z = -TowerHeroBack)에서 비껴)
        public static readonly Vector2[] BannerSpots = { new Vector2(-3.3f, BattleTowerView.DoorZ - 1.3f), new Vector2(3.3f, BattleTowerView.DoorZ - 1.3f) };   // W35: 주인공 길(문 앞 줄 ±2.2, 동·서 비스듬한 길)에서 1m 이상 비껴 문 앞 좌우

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly GameObject root;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<MeshRenderer> lampRenderers = new List<MeshRenderer>();
        readonly List<Material> bannerMaterials = new List<Material>();
        Material lampDayMaterial;
        Material[] lampNightMaterials = new Material[0];   // 두 벌: 불꽃 칸이 엇갈려 기둥마다 따로 타 보인다
        bool nightShown;
        BattleWorldMaterials stone;
        bool ownsStone;

        public int RendererCount { get; private set; }
        public bool HasPavingArt { get; private set; }

        // ── 배치 (순수 계산: 시험·빛 번짐이 같이 쓴다) ──
        // 성벽 안쪽에서 계단 아래까지 거리: 길이 시작되는 곳 (탑 가운데에서 그 성벽 바깥 방향으로 잰 거리). 네 길 모두 같다
        public static float RoadStart(float half) => half - 2f - BattleCourtyard.StepCount * BattleCourtyard.TreadDepth;

        // 길 하나의 등불 기둥 한 쌍의 수 (길 위쪽 끝에서 2m 떨어져 시작, LampSpacing마다, 광장 가장자리 0.8m 앞까지)
        public static int LampPairs(float half)
        {
            float first = RoadStart(half) - 2f, last = PlazaRimRadius + 0.8f;
            return first < last ? 0 : Mathf.FloorToInt((first - last) / LampSpacing) + 1;
        }

        // 네 길 모두의 기둥 수
        public static int LampCount(float half) => LampPairs(half) * 2 * RoadCount;

        // 기둥 i가 선 길 (0 북 · 1 동 · 2 남 · 3 서)
        public static int LampRoad(int i) => (i >> 1) % RoadCount;

        // i번째 등불 기둥 발 위치 (마당 윗면 높이). i = (짝 번호 × 4 + 길) × 2 + 쪽 — 바깥 짝부터 네 길을 한 바퀴씩 (땅 번짐이 닿는 범위 안의 기둥을 고르는 순서)
        public static Vector3 LampBase(float half, int i)
        {
            BattleCourtyard.StairFrame(LampRoad(i), out Vector3 n, out Vector3 r);
            float dist = RoadStart(half) - 2f - ((i >> 1) / RoadCount) * LampSpacing;
            float side = (i & 1) == 0 ? -1f : 1f;
            return n * dist + r * (side * LampOffset) + Vector3.up * BattleSim.WallHeight;
        }

        // 길 가운데 선 위 점 (dist = 탑 가운데에서 그 성벽 쪽으로 거리)
        public static Vector3 CenterAt(float dist, int side = BattleCameraRig.StairsSide)
        {
            BattleCourtyard.StairFrame(side, out Vector3 n, out _);
            return n * dist + Vector3.up * BattleSim.WallHeight;
        }

        // 길 road의 광장 앞 깃발 (쪽 -1·+1)
        public static Vector3 RoadBannerBase(int road, int sideSign)
        {
            BattleCourtyard.StairFrame(road, out Vector3 n, out Vector3 r);
            return n * RoadBannerDistance + r * (sideSign * LampOffset) + Vector3.up * BattleSim.WallHeight;
        }

        public BattleRoad(float half, Transform parent, BattleWorldMaterials sharedStone = null)
        {
            root = new GameObject("Beacon Road (W26)");
            root.transform.SetParent(parent, false);
            float top = BattleSim.WallHeight + SkinLift;

            ownsStone = sharedStone == null;
            stone = sharedStone ?? new BattleWorldMaterials();
            Vector2 walkTile = TileOf(BattleWorldMaterials.Surface.WalkwayStone);
            Vector2 wallTile = TileOf(BattleWorldMaterials.Surface.WallStone);

            float start = RoadStart(half);
            float end = PlazaRadius - 0.3f;   // 광장 밑으로 살짝 파고든다
            float length = start - end;
            float kerbLength = start - PlazaRimRadius;   // 연석은 광장 아랫단 가장자리에서 끝난다 (광장 위로 삐져나오지 않게)
            int blocks = kerbLength >= KerbBlock ? Mathf.Max(1, Mathf.FloorToInt((kerbLength + KerbGap) / (KerbBlock + KerbGap))) : 0;
            float pitch = (kerbLength - KerbBlock) / Mathf.Max(1, blocks - 1);

            // 돌길 (밝은 다듬은 돌) — 네 길을 한 메시에. K-116 둥근 돌 그림을 더 촘촘하게 깔면 바닥과 구분되는 길이 된다
            Vector2 pavingTile = PavingArt() != null ? new Vector2(RoadCobbleTileMeters, RoadCobbleTileMeters) : walkTile;
            var paving = new BattleCourtyard.MeshBuilder(pavingTile);
            var kerbs = new BattleCourtyard.MeshBuilder(wallTile);   // 연석 + 광장 단 (성벽 돌)
            for (int road = 0; road < RoadCount; road++)
            {
                BattleCourtyard.StairFrame(road, out Vector3 n, out Vector3 r);
                float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
                Vector3 mid = n * ((start + end) * 0.5f);
                if (length >= KerbBlock)   // 아주 작은 성은 길 없이 광장만
                    paving.BoxYaw(mid + Vector3.up * (top - 0.004f), new Vector3(HalfWidth * 2f, PavingLift - SkinLift + 0.004f, length), yaw);

                for (int i = 0; i < blocks; i++)
                {
                    float d = start - KerbBlock * 0.5f - (blocks > 1 ? i * pitch : 0f);
                    foreach (float side in new[] { -1f, 1f })
                        kerbs.BoxYaw(n * d + r * (side * (HalfWidth + KerbWidth * 0.5f)) + Vector3.up * top, new Vector3(KerbWidth, KerbLift - SkinLift, KerbBlock), yaw);
                }
            }

            Spawn("Road Paving", paving, MakePaving(walkTile));
            HasPavingArt = PavingArt() != null;

            kerbs.Disc(Vector3.zero, PlazaRimRadius, top - 0.004f, BattleSim.WallHeight + PlazaRimLift, 28);
            kerbs.Disc(Vector3.zero, PlazaRadius, top - 0.004f, BattleSim.WallHeight + PlazaLift, 28);
            Spawn("Road Kerb And Plaza", kerbs, MakeStone(BattleWorldMaterials.Surface.WallStone));

            // K-122 등불 기둥·깃발: 사진기를 보는 세운 그림 (예전 상자 기둥·천 대신). 그림이 없으면 만들지 않는다
            var lampDayArt = Resources.Load<Texture2D>(RoadFolder + "lamp_post_day");
            var lampNightArt = Resources.Load<Texture2D>(RoadFolder + "lamp_post_night");
            if (lampDayArt != null && lampNightArt != null)
            {
                float pm = LampVisibleMeters / LampVisiblePixels;
                Mesh lampMesh = SpriteMesh(pm);
                lampDayMaterial = MakeSheet("Road Lamp Day (runtime)", lampDayArt, 1, true);
                lampNightMaterials = new[] { MakeSheet("Road Lamp Night A (runtime)", lampNightArt, LampFrames, false), MakeSheet("Road Lamp Night B (runtime)", lampNightArt, LampFrames, false) };
                int lamps = LampCount(half);
                for (int i = 0; i < lamps; i++)
                {
                    Vector3 b = LampBase(half, i);
                    b.y = top;
                    lampRenderers.Add(Billboard("Road Lamp " + i, lampMesh, lampDayMaterial, b, pm).GetComponent<MeshRenderer>());
                }
            }

            var bannerArt = Resources.Load<Texture2D>(RoadFolder + "beacon_banner");
            if (bannerArt != null)
            {
                float pm = BannerVisibleMeters / BannerVisiblePixels;
                Mesh bannerMesh = SpriteMesh(pm);
                foreach (Vector2 spot in BannerSpots)
                {
                    Material m = MakeSheet("Road Banner (runtime)", bannerArt, BannerFrames, true);
                    bannerMaterials.Add(m);
                    Billboard("Road Banner", bannerMesh, m, new Vector3(spot.x, top, spot.y), pm);
                }

                // 길마다 광장 가장자리 앞 깃발 한 쌍 (W35). 재질은 문 곁 깃발 둘을 나눠 쓴다 (펄럭임 박자만 엇갈리게)
                for (int road = 0; road < RoadCount; road++)
                    for (int s = 0; s < 2; s++)
                    {
                        Vector3 b = RoadBannerBase(road, s == 0 ? -1 : 1);
                        b.y = top;
                        Billboard("Road Banner", bannerMesh, bannerMaterials[s], b, pm);
                    }
            }
        }

        Vector2 TileOf(BattleWorldMaterials.Surface surface)
        {
            if (!stone.HasArt(surface)) return Vector2.one;
            var t = stone.Get(surface).mainTexture;
            return new Vector2(t.width / BattleWorldMaterials.PixelsPerMeter, t.height / BattleWorldMaterials.PixelsPerMeter);
        }

        // 성벽·통로 재질을 그대로 쓰되 돌길은 한 칸 밝게 (복사본 — 공유 재질은 그대로)
        Material MakeStone(BattleWorldMaterials.Surface surface) => stone.Get(surface);

        // 돌길 그림: Resources/BattleArt/Courtyard/road_paving (교체 자리). 없으면 K-116 마당 바닥 돌(courtyard_cobble_512)을 밝게
        static Texture2D PavingArt()
        {
            var art = Resources.Load<Texture2D>(Folder + "road_paving");
            return art != null ? art : Resources.Load<Texture2D>(Folder + "courtyard_cobble_512");
        }

        Material MakePaving(Vector2 walkTile)
        {
            var art = PavingArt();
            var m = new Material(stone.Get(BattleWorldMaterials.Surface.WalkwayStone)) { name = "Road Paving (runtime)" };
            if (art != null)
            {
                if (art.filterMode != FilterMode.Point) art.filterMode = FilterMode.Point;
                if (art.wrapMode != TextureWrapMode.Repeat) art.wrapMode = TextureWrapMode.Repeat;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", art);
                m.mainTexture = art;
                if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }   // 통로 돌 노멀맵은 이 그림과 안 맞는다
            }

            Color c = art != null ? new Color(1.28f, 1.25f, 1.18f) : (stone.HasArt(BattleWorldMaterials.Surface.WalkwayStone) ? new Color(1.18f, 1.16f, 1.1f) : new Color(0.72f, 0.7f, 0.66f));
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            ownedMaterials.Add(m);
            return m;
        }

        // 밤: 등불 기둥이 불꽃 여섯 칸 그림으로 바뀌고(밝기에 따라 환해진다), 낮에는 꺼진 쇠 기둥. 깃발은 낮밤 없이 네 칸으로 펄럭인다.
        // 땅 번짐은 그림이 아니라 BattleLights가 GroundGlow로 따로 낸다 (셰이더 빛 칸 안 씀). 재질의 칸 좌표·색만 바꾼다
        public void Tick(bool night, float level)
        {
            if (night != nightShown)
            {
                nightShown = night;
                for (int i = 0; i < lampRenderers.Count; i++)
                    lampRenderers[i].sharedMaterial = night ? lampNightMaterials[(i >> 1) & 1] : lampDayMaterial;
            }

            float time = Time.time;
            if (night)
            {
                float shade = Mathf.Lerp(0.95f, 1.25f, Mathf.Clamp01(level));
                for (int g = 0; g < lampNightMaterials.Length; g++)
                {
                    var m = lampNightMaterials[g];
                    if (m == null) continue;
                    SetFrame(m, (int)(time * LampFps + g * 3f) % LampFrames, LampFrames);
                    var c = new Color(shade, shade, shade, 1f);
                    if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
                }
            }

            for (int i = 0; i < bannerMaterials.Count; i++)
                SetFrame(bannerMaterials[i], (int)(time * BannerFps + i * 2f) % BannerFrames, BannerFrames);
        }

        static void SetFrame(Material m, int frame, int frames)
        {
            m.mainTextureOffset = new Vector2(frame / (float)frames, 0f);
        }

        // 가로로 이어 붙인 칸 그림 재질: 한 칸이 판 전체에 보이도록 칸 폭만큼 줄이고 offset으로 칸을 고른다. 투명 자르기 견본 복사 (빌드에서 키워드가 빠지지 않게)
        Material MakeSheet(string name, Texture2D art, int frames, bool lit)
        {
            Material m = lit ? ClipMaterials.LitCopy(name) : ClipMaterials.UnlitCopy(name);
            if (m == null)
            {
                Shader shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
                m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = name };
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_ALPHATEST_ON");
            }

            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", art);
            m.mainTexture = art;
            m.mainTextureScale = new Vector2(1f / frames, 1f);
            m.mainTextureOffset = Vector2.zero;
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            ownedMaterials.Add(m);
            return m;
        }

        // 사진기를 보는 세운 그림 하나 (96×128 칸, 발 기준점 (48,120) → 칸 아래에서 8화소 위). 같은 메시를 여럿이 나눠 쓴다
        GameObject Billboard(string name, Mesh mesh, Material material, Vector3 feet, float pixelMeters)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = feet + Vector3.down * (8f * pixelMeters);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<CourtyardBillboard>();
            RendererCount++;
            return go;
        }

        Mesh SpriteMesh(float pixelMeters)
        {
            var kit = new MeshKit();
            kit.Standing(96f * pixelMeters, 128f * pixelMeters, new Rect(0f, 0f, 1f, 1f));
            var mesh = kit.ToMesh("Road Sprite");
            meshes.Add(mesh);
            return mesh;
        }

        void Spawn(string name, BattleCourtyard.MeshBuilder builder, Material material)
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

        Material MakeLit(string name, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = name };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            ownedMaterials.Add(m);
            return m;
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
            meshes.Clear();
            ownedMaterials.Clear();
            if (ownsStone) stone?.Dispose();
        }
    }
}




