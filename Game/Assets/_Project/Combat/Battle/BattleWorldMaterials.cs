using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 3D 모형(성벽·통로·난간·포탑·들판) 재질 (Codex 석재 질감 8표면과 같은 이름)
    // - 그림: Resources/BattleArt/World/<표면>_albedo, <표면>_normal → 유니버설 렌더 파이프라인 기본 조명 셰이더(Lit)
    // - 그림이 없으면 표면별 단색 — 지금 목업 회색 상자와 같은 색
    // - 같은 표면은 재질 하나를 같이 쓰고, 상자마다 크기에 맞는 반복은 Apply의 MaterialPropertyBlock으로 준다(1m당 32화소)
    public sealed class BattleWorldMaterials : IDisposable
    {
        public enum Surface
        {
            WallStone,      // 성벽 바깥면
            WalkwayStone,   // 통로 바닥
            ParapetTop,     // 난간 윗면
            ParapetSide,    // 난간 옆면
            TowerWall,      // 포탑 벽
            TowerRoof,      // 포탑 지붕
            FieldGrass,     // 들판 풀
            FieldDirt,      // 들판 흙
        }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        const string ShaderName = "Universal Render Pipeline/Lit";
        const string ResourceFolder = "BattleArt/World/";
        const float PixelsPerMeter = 32f;
        const float NormalStrength = 0.45f;   // Codex material.json 권장값 (normalStrengthSuggestion)

        // Codex 질감 폴더 이름과 같게 (wall_stone 등)
        static readonly string[] Names =
        {
            "wall_stone", "walkway_stone", "parapet_top", "parapet_side",
            "tower_wall", "tower_roof", "field_grass", "field_dirt",
        };

        readonly Dictionary<Surface, Entry> entries = new Dictionary<Surface, Entry>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        sealed class Entry
        {
            public Material Material;
            public Vector2 TileMeters;   // 그림 한 장이 덮는 미터 (그림 없으면 0)
        }

        public static string NameOf(Surface surface) => Names[(int)surface];

        public Material Get(Surface surface) => GetEntry(surface).Material;

        // 상자 하나의 반복 — 재질은 같이 쓰고 이 상자에만 _BaseMap_ST를 준다. 노멀맵도 같은 UV 변환을 쓴다
        // sizeMeters: 그림이 붙는 면의 가로·세로(m). 유니티 기본 상자는 면마다 UV 0~1이라 면 크기를 그대로 넣는다
        // offsetMeters: 그림을 얼마나 밀어서 시작할지(m). 성벽 따라 위치를 넣으면 이웃 조각과 그림이 끊김 없이 이어진다
        public void Apply(Renderer renderer, Surface surface, Vector2 sizeMeters, Vector2 offsetMeters = default)
        {
            if (renderer == null) return;
            Entry e = GetEntry(surface);
            if (e.TileMeters.x <= 0f || e.TileMeters.y <= 0f) return;   // 그림 없음 — 단색

            var st = new Vector4(sizeMeters.x / e.TileMeters.x, sizeMeters.y / e.TileMeters.y, offsetMeters.x / e.TileMeters.x, offsetMeters.y / e.TileMeters.y);
            renderer.GetPropertyBlock(block);
            block.SetVector(BaseMapStId, st);
            block.SetVector(MainTexStId, st);   // 대비책 재질(_MainTex)용
            renderer.SetPropertyBlock(block);
            block.Clear();
        }

        public bool HasArt(Surface surface) => GetEntry(surface).TileMeters.x > 0f;

        // 이 조각만 색을 곱한다 (성벽 체력 붉은 기 등). 반복 값(_BaseMap_ST)은 그대로 둔다
        public void Tint(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
            block.Clear();
        }

        Entry GetEntry(Surface surface)
        {
            if (entries.TryGetValue(surface, out Entry e)) return e;

            string name = NameOf(surface);
            var albedo = Resources.Load<Texture2D>(ResourceFolder + name + "_albedo");
            var normal = albedo != null ? Resources.Load<Texture2D>(ResourceFolder + name + "_normal") : null;

            Material m = CreateBase();
            m.name = "BattleWorld_" + name;
            SetColor(m, albedo != null ? Color.white : FallbackColor(surface));
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, Smoothness(surface));
            if (m.HasProperty(MetallicId)) m.SetFloat(MetallicId, 0f);

            var tile = Vector2.zero;
            if (albedo != null)
            {
                PixelSampling(albedo);
                if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, albedo);
                m.mainTexture = albedo;   // 대비책 재질(_MainTex)도 같이
                tile = new Vector2(albedo.width / PixelsPerMeter, albedo.height / PixelsPerMeter);

                if (normal != null && m.HasProperty(BumpMapId))
                {
                    PixelSampling(normal);
                    m.SetTexture(BumpMapId, normal);
                    if (m.HasProperty(BumpScaleId)) m.SetFloat(BumpScaleId, NormalStrength);
                    m.EnableKeyword("_NORMALMAP");
                }
            }

            e = new Entry { Material = m, TileMeters = tile };
            entries.Add(surface, e);
            return e;
        }

        // Lit 셰이더를 못 찾으면(빌드에서 빠짐 등) 상자 기본 재질을 복사해 쓴다
        static Material CreateBase()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader != null) return new Material(shader);

            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.SetActive(false);
            var m = new Material(probe.GetComponent<Renderer>().sharedMaterial);
            DestroyObject(probe);
            return m;
        }

        // 도트 질감 — 가져오기 설정이 빠져도 점 필터·반복으로 (README 참고)
        static void PixelSampling(Texture2D texture)
        {
            if (texture.filterMode != FilterMode.Point) texture.filterMode = FilterMode.Point;
            if (texture.wrapMode != TextureWrapMode.Repeat) texture.wrapMode = TextureWrapMode.Repeat;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        // 그림 없을 때 단색 (목업 회색 상자 색)
        static Color FallbackColor(Surface surface)
        {
            switch (surface)
            {
                case Surface.WallStone:
                case Surface.TowerWall:
                case Surface.WalkwayStone: return new Color(0.62f, 0.61f, 0.58f);
                case Surface.ParapetTop:
                case Surface.ParapetSide: return new Color(0.48f, 0.47f, 0.45f);
                case Surface.TowerRoof: return new Color(0.36f, 0.30f, 0.27f);
                case Surface.FieldGrass: return new Color(0.34f, 0.45f, 0.26f);
                case Surface.FieldDirt: return new Color(0.45f, 0.38f, 0.29f);
                default: return Color.gray;
            }
        }

        // 돌·흙은 거의 무광, 지붕은 조금
        static float Smoothness(Surface surface)
        {
            switch (surface)
            {
                case Surface.TowerRoof: return 0.2f;
                case Surface.FieldGrass:
                case Surface.FieldDirt: return 0.05f;
                default: return 0.12f;
            }
        }

        static void DestroyObject(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        public void Dispose()
        {
            foreach (Entry e in entries.Values) DestroyObject(e.Material);
            entries.Clear();
        }
    }
}
