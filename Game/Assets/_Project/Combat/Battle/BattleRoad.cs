using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 계단 → 등대 탑 의식용 돌길 (W26, 사용자 2026-10-23 "등대로 가는 길이 너무 초라하다").
    // - 돌길: 계단 아래에서 탑 광장까지 조금 밝은 다듬은 돌 길 + 양쪽 연석(작은 돌 덩이를 틈 두고 줄 세움)
    // - 광장: 탑 밑동을 두른 둥근 돌바닥 + 낮은 돌 단 하나
    // - 등불 기둥: 길 양쪽 4m마다 쇠 기둥 + 화로. 밤에는 불꽃이 켜지고 땅 번짐(GroundGlow)만 쓴다 — 셰이더 빛 칸은 쓰지 않는다 (PC 14·모바일 6 상한 그대로)
    // - 깃발: 탑 문 곁에 등불지기 깃발 두 개
    // 전부 눈에 보이는 것만 — 발 높이·병사 길·전령 길·사진기는 건드리지 않는다 (돌길 윗면 +2cm, 광장 +2.5cm로 아주 낮게)
    // 그림: Resources/BattleArt/Courtyard/road_paving 이 있으면 그것을, 없으면 통로 돌(walkway_stone)을 밝게 쓴다 (Codex K-116이 오면 그림만 바꾼다)
    // 그리는 수: 돌길 1 + 연석·광장 단 1 + 쇠 1 + 문양 1 + 깃발 1 = 5번
    public sealed class BattleRoad : IDisposable
    {
        const string Folder = "BattleArt/Courtyard/";
        const float SkinLift = 0.01f;                     // 마당 바닥(BattleCourtyard)과 같은 기준

        public const float HalfWidth = 1.1f;              // 돌길 반폭 (주인공 0.375m 기준 넉넉하게)
        public const float KerbWidth = 0.2f;
        public const float PavingLift = 0.02f;            // 돌길 윗면 (마당 바닥 위) — 주인공 발이 거의 안 묻힌다
        public const float KerbLift = 0.08f;
        public const float PlazaRadius = 3.0f;            // 탑 둘레 광장 윗단 반지름 (탑 받침 1.25m 밖)
        public const float PlazaRimRadius = 3.5f;         // 낮은 아랫단
        public const float PlazaLift = 0.025f, PlazaRimLift = 0.015f;   // GroundGlow 판(바닥 +3cm)보다 낮게 — 탑·문 앞 웅덩이가 광장에 안 묻힌다 (돌길 윗면 2cm와도 다르게)
        public const float LampSpacing = 4f;
        public const float LampOffset = HalfWidth + KerbWidth + 0.3f;   // 길 가운데에서 기둥까지 (연석 바깥)
        public const float LampPostHeight = 0.7f;
        public const float LampPoolRadius = 1.8f;
        public const float LampBowlTop = 0.01f + LampPostHeight + 0.07f;   // 마당 윗면 위 화로 그릇 윗면 높이 (불꽃 그림이 서는 자리)
        const float KerbBlock = 0.76f, KerbGap = 0.04f;
        const float RoadCobbleTileMeters = 1.7f;           // 돌길 그림 한 장이 덮는 미터 (마당 바닥 2.6m보다 촘촘하게)

        // 깃발 자리 (탑 문 곁, 몸체 받침 바깥 · 주인공 서는 줄(z = -TowerHeroBack)에서 비껴)
        public static readonly Vector2[] BannerSpots = { new Vector2(-2.05f, BattleTowerView.DoorZ + 0.1f), new Vector2(2.05f, BattleTowerView.DoorZ + 0.1f) };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly GameObject root;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Material flameMaterial;
        readonly Color flameBase = new Color(1f, 0.38f, 0.08f);   // 붉은 주황 (밝기 배수를 올려도 노랑·연두로 날아가지 않게)
        BattleWorldMaterials stone;
        bool ownsStone;

        public int RendererCount { get; private set; }
        public bool HasPavingArt { get; private set; }

        // ── 배치 (순수 계산: 시험·빛 번짐이 같이 쓴다) ──
        // 계단 쪽 성벽 안쪽에서 계단 아래까지 거리: 길이 시작되는 곳 (탑 가운데에서 계단 바깥 방향으로 잰 거리)
        public static float RoadStart(float half) => half - 2f - BattleCourtyard.StepCount * BattleCourtyard.TreadDepth;

        // 등불 기둥 한 쌍의 수 (길 위쪽 끝에서 2m 떨어져 시작, 4m마다, 광장 가장자리 0.8m 앞까지)
        public static int LampPairs(float half)
        {
            float first = RoadStart(half) - 2f, last = PlazaRimRadius + 0.8f;
            return first < last ? 0 : Mathf.FloorToInt((first - last) / LampSpacing) + 1;
        }

        public static int LampCount(float half) => LampPairs(half) * 2;

        // i번째 등불 기둥 발 위치 (마당 윗면 높이). 짝수 = 한쪽, 홀수 = 반대쪽
        public static Vector3 LampBase(float half, int i)
        {
            BattleCourtyard.StairFrame(out Vector3 n, out Vector3 r);
            float dist = RoadStart(half) - 2f - (i / 2) * LampSpacing;
            float side = (i & 1) == 0 ? -1f : 1f;
            return n * dist + r * (side * LampOffset) + Vector3.up * BattleSim.WallHeight;
        }

        // 길 가운데 선 위 점 (dist = 탑 가운데에서 계단 쪽으로 거리)
        public static Vector3 CenterAt(float dist)
        {
            BattleCourtyard.StairFrame(out Vector3 n, out _);
            return n * dist + Vector3.up * BattleSim.WallHeight;
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

            BattleCourtyard.StairFrame(out Vector3 n, out Vector3 r);
            float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
            float start = RoadStart(half);
            float end = PlazaRadius - 0.3f;   // 광장 밑으로 살짝 파고든다
            float length = start - end;
            Vector3 mid = n * ((start + end) * 0.5f);

            // 돌길 (밝은 다듬은 돌) — 한 상자. K-116 둥근 돌 그림을 더 촘촘하게 깔면 바닥과 구분되는 길이 된다
            Vector2 pavingTile = PavingArt() != null ? new Vector2(RoadCobbleTileMeters, RoadCobbleTileMeters) : walkTile;
            var paving = new BattleCourtyard.MeshBuilder(pavingTile);
            if (length >= KerbBlock)   // 아주 작은 성은 길 없이 광장만
                paving.BoxYaw(mid + Vector3.up * (top - 0.004f), new Vector3(HalfWidth * 2f, PavingLift - SkinLift + 0.004f, length), yaw);
            Spawn("Road Paving", paving, MakePaving(walkTile));
            HasPavingArt = PavingArt() != null;

            // 연석 + 광장 단 (성벽 돌)
            var kerbs = new BattleCourtyard.MeshBuilder(wallTile);
            float kerbLength = start - PlazaRimRadius;   // 연석은 광장 아랫단 가장자리에서 끝난다 (광장 위로 삐져나오지 않게)
            int blocks = kerbLength >= KerbBlock ? Mathf.Max(1, Mathf.FloorToInt((kerbLength + KerbGap) / (KerbBlock + KerbGap))) : 0;
            float pitch = (kerbLength - KerbBlock) / Mathf.Max(1, blocks - 1);
            for (int i = 0; i < blocks; i++)
            {
                float d = start - KerbBlock * 0.5f - (blocks > 1 ? i * pitch : 0f);
                foreach (float side in new[] { -1f, 1f })
                    kerbs.BoxYaw(n * d + r * (side * (HalfWidth + KerbWidth * 0.5f)) + Vector3.up * top, new Vector3(KerbWidth, KerbLift - SkinLift, KerbBlock), yaw);
            }

            kerbs.Disc(Vector3.zero, PlazaRimRadius, top - 0.004f, BattleSim.WallHeight + PlazaRimLift, 28);
            kerbs.Disc(Vector3.zero, PlazaRadius, top - 0.004f, BattleSim.WallHeight + PlazaLift, 28);
            Spawn("Road Kerb And Plaza", kerbs, MakeStone(BattleWorldMaterials.Surface.WallStone));

            // 쇠 (기둥·화로 그릇·깃발 대) + 깃발 문양(밤에 은은히 빛남) + 깃발 천. 기둥 불꽃은 횃불 불꽃 그림(BattleEffectsView)이 밤에 얹는다
            var iron = new BattleCourtyard.MeshBuilder(Vector2.one);
            var flame = new BattleCourtyard.MeshBuilder(Vector2.one);
            int lamps = LampCount(half);
            for (int i = 0; i < lamps; i++)
            {
                Vector3 b = LampBase(half, i);
                b.y = top;
                iron.BoxYaw(b, new Vector3(0.14f, 0.06f, 0.14f), yaw);
                iron.BoxYaw(b, new Vector3(0.05f, LampPostHeight, 0.05f), yaw);
                iron.BoxYaw(b + Vector3.up * LampPostHeight, new Vector3(0.2f, 0.07f, 0.2f), yaw);   // 화로 그릇
            }

            var cloth = new BattleCourtyard.MeshBuilder(Vector2.one);
            foreach (Vector2 spot in BannerSpots)
            {
                Vector3 b = new Vector3(spot.x, top, spot.y);
                iron.BoxYaw(b, new Vector3(0.05f, 1.5f, 0.05f), 0f);                                  // 깃대
                iron.BoxYaw(b + Vector3.up * 1.42f, new Vector3(0.5f, 0.035f, 0.04f), 0f);            // 가로대
                cloth.BoxYaw(b + new Vector3(0f, 0.68f, -0.04f), new Vector3(0.38f, 0.74f, 0.012f), 0f);   // 천 (남쪽 = 문 쪽 면이 보인다)
                flame.BoxYaw(b + new Vector3(0f, 1.0f, -0.052f), new Vector3(0.14f, 0.14f, 0.01f), 45f);   // 등불 문양 (금빛 마름모)
            }

            Spawn("Road Iron", iron, MakeLit("Road Iron (runtime)", new Color(0.12f, 0.115f, 0.11f), 0.35f));
            flameMaterial = MakeUnlit("Road Flame (runtime)", flameBase * 0.5f);
            Spawn("Road Emblem", flame, flameMaterial);
            Spawn("Road Banner", cloth, MakeLit("Road Banner (runtime)", new Color(0.13f, 0.2f, 0.42f), 0.1f));
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

        // 밤: 불꽃이 켜지고 등불 밝기에 따라 환해진다. 낮에는 꺼진 숯불. 재질 색만 바꾼다
        public void Tick(bool night, float level)
        {
            if (flameMaterial == null) return;
            float flick = 1f + 0.12f * Mathf.Sin(Time.time * 9.1f) + 0.06f * Mathf.Sin(Time.time * 17.3f + 1f);
            Color c = night ? flameBase * (Mathf.Lerp(0.9f, 1.5f, Mathf.Clamp01(level)) * flick) : flameBase * 0.35f;
            c.a = 1f;
            flameMaterial.SetColor(BaseColorId, c);
            flameMaterial.SetColor(ColorId, c);
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
            var m = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
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




