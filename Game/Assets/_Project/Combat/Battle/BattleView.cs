using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Definitions;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 전장 그리기: 바닥·성벽 4면(임시 상자)·빛, 적·병사·투사체는 GPU 인스턴싱 (D-024)
    public sealed class BattleView : IDisposable
    {
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int MapModeId = Shader.PropertyToID("_MapMode");
        static readonly int MapColorId = Shader.PropertyToID("_MapColor");
        static readonly int MinWorldSizeId = Shader.PropertyToID("_MinWorldSize");

        // 2번 지도 모드 점 색 (BattleMap.md 2장)
        static readonly Color MapEnemyColor = new Color(1f, 0.72f, 0.2f, 1f);
        static readonly Color MapSpecialColor = new Color(1f, 0.15f, 0.15f, 1f);
        static readonly Color MapSoldierColor = new Color(0.35f, 0.6f, 1f, 1f);

        readonly BattleSim sim;
        readonly Material material;
        readonly Material mapMaterial;     // 같은 버퍼를 점으로 그리는 지도용
        readonly Texture2D dotTexture;
        readonly List<(MaterialPropertyBlock block, int start, int count, bool special)> mapEnemyDraws =
            new List<(MaterialPropertyBlock, int, int, bool)>();
        readonly MaterialPropertyBlock mapSoldierBlock;
        // 지도 표시(대포 위치·사거리 원·조준점) — 점 셰이더로 그린다 (MAP-3)
        const int MarkerCapacity = 512;
        readonly GraphicsBuffer markerBuffer;
        readonly MaterialPropertyBlock markerBlock;
        NativeArray<PerfLabInstance> markers;
        readonly Mesh quad;
        readonly Texture2D placeholderSheet;
        readonly Texture2D shotTexture;
        readonly List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)> enemyDraws =
            new List<(GraphicsBuffer, MaterialPropertyBlock, int, int)>();
        readonly GraphicsBuffer soldierBuffer;
        readonly MaterialPropertyBlock soldierBlock;
        readonly GraphicsBuffer shotBuffer;
        readonly MaterialPropertyBlock shotBlock;
        readonly Renderer[] walls = new Renderer[4];
        Light sunLight;
        Transform[] pileBlocks;          // 시체 더미 칸마다 상자 (높이가 생기면 만든다)
        Material pileMaterial;
        float nextPileRefresh;
        readonly Bounds bounds;
        readonly GameObject root;

        public BattleView(BattleSim sim, Shader shader, Transform parent)
        {
            this.sim = sim;
            quad = PerfLabArt.CreateQuad();
            placeholderSheet = PerfLabArt.CreatePlaceholderSheet();
            shotTexture = PerfLabArt.CreateArrowTexture();
            material = new Material(shader) { name = "BattleSprite (runtime)" };
            mapMaterial = new Material(shader) { name = "BattleMapDots (runtime)" };
            mapMaterial.SetFloat(MapModeId, 1f);
            dotTexture = CreateDotTexture();
            float extent = sim.FieldSize + 20f;
            bounds = new Bounds(Vector3.zero, new Vector3(extent * 2f, 200f, extent * 2f));

            foreach (var (def, start, count) in sim.EnemyGroups)
            {
                var block = new MaterialPropertyBlock();
                if (def.Sheet != null)
                {
                    Apply(block, def.Sheet, PerfLabArt.GoblinLayout);   // 4열×2행 고블린 규격 (EnemyDef 주석)
                }
                else
                {
                    Apply(block, placeholderSheet, PerfLabArt.PlaceholderLayout);
                }

                var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), PerfLabInstance.Stride);
                block.SetBuffer(InstancesId, buffer);
                enemyDraws.Add((buffer, block, start, count));

                var mapBlock = new MaterialPropertyBlock();
                mapBlock.SetTexture(MainTexId, dotTexture);
                mapBlock.SetColor(MapColorId, def.IsSpecial ? MapSpecialColor : MapEnemyColor);
                mapBlock.SetBuffer(InstancesId, buffer);
                mapEnemyDraws.Add((mapBlock, start, count, def.IsSpecial));
            }

            soldierBlock = new MaterialPropertyBlock();
            Apply(soldierBlock, placeholderSheet, PerfLabArt.PlaceholderLayout);   // 병사 그림은 Codex 아트 이후
            soldierBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, sim.SoldierCount), PerfLabInstance.Stride);
            soldierBlock.SetBuffer(InstancesId, soldierBuffer);
            mapSoldierBlock = new MaterialPropertyBlock();
            mapSoldierBlock.SetTexture(MainTexId, dotTexture);
            mapSoldierBlock.SetColor(MapColorId, MapSoldierColor);
            mapSoldierBlock.SetBuffer(InstancesId, soldierBuffer);
            markerBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MarkerCapacity, PerfLabInstance.Stride);
            markers = new NativeArray<PerfLabInstance>(MarkerCapacity, Allocator.Persistent);
            markerBlock = new MaterialPropertyBlock();
            markerBlock.SetTexture(MainTexId, dotTexture);
            markerBlock.SetColor(MapColorId, new Color(0f, 0f, 0f, 0f));   // 개체 색 사용
            markerBlock.SetFloat(MinWorldSizeId, 0f);
            markerBlock.SetBuffer(InstancesId, markerBuffer);

            shotBlock = new MaterialPropertyBlock();
            Apply(shotBlock, shotTexture, PerfLabArt.ArrowLayout);
            shotBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sim.ShotCapacity, PerfLabInstance.Stride);
            shotBlock.SetBuffer(InstancesId, shotBuffer);

            root = new GameObject("Battlefield");
            root.transform.SetParent(parent, false);
            BuildEnvironment();
        }

        static void Apply(MaterialPropertyBlock block, Texture texture, PerfLabArt.SheetLayout layout)
        {
            block.SetTexture(MainTexId, texture);
            block.SetVector(GridId, layout.Grid);
            block.SetVector(PivotId, layout.Pivot);
            block.SetFloat(AspectId, layout.Aspect);
        }

        void BuildEnvironment()
        {
            float half = sim.CastleHalf;
            float field = sim.FieldSize;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3((field + 20f) * 2f / 10f, 1f, (field + 20f) * 2f / 10f);
            Tint(ground.GetComponent<Renderer>(), new Color(0.42f, 0.5f, 0.3f));

            var yard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            yard.name = "Castle Yard";
            yard.transform.SetParent(root.transform, false);
            yard.transform.localPosition = new Vector3(0f, BattleSim.WallHeight * 0.5f - 0.05f, 0f);
            yard.transform.localScale = new Vector3(half * 2f - 3f, BattleSim.WallHeight - 0.1f, half * 2f - 3f);
            Tint(yard.GetComponent<Renderer>(), new Color(0.45f, 0.42f, 0.38f));

            for (int side = 0; side < 4; side++)
            {
                Vector3 n = ToVector(WallGeometry.Outward(side));
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall " + sim.Sides[side].WallId;
                wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = n * (half - 1f) + Vector3.up * BattleSim.WallHeight * 0.5f;
                wall.transform.localRotation = Quaternion.LookRotation(n);
                wall.transform.localScale = new Vector3(half * 2f + 2f, BattleSim.WallHeight, 2f);
                walls[side] = wall.GetComponent<Renderer>();
            }

            pileBlocks = new Transform[4 * sim.Pile.Bins];
            pileMaterial = new Material(walls[0].sharedMaterial) { name = "Corpse Pile (runtime)", color = new Color(0.32f, 0.22f, 0.14f) };

            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sunLight = sun.GetComponent<Light>();
            sunLight.type = LightType.Directional;
        }

        static void Tint(Renderer renderer, Color color)
        {
            renderer.material.color = color;
        }

        static Vector3 ToVector(Unity.Mathematics.float3 v) => new Vector3(v.x, v.y, v.z);

        // 작업이 끝난 뒤(sim.Complete 후) 부른다. 지도 모드면 같은 데이터를 점으로 그린다(mapDotSize = 최소 점 크기 m)
        public void Draw(bool mapMode = false, float mapDotSize = 0f)
        {
            foreach (var (buffer, block, start, count) in enemyDraws)
            {
                if (count <= 0) continue;
                buffer.SetData(sim.EnemyInstances, start, 0, count);
                if (!mapMode) Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
            }

            if (mapMode)
            {
                foreach (var (block, _, count, special) in mapEnemyDraws)
                {
                    if (count <= 0) continue;
                    block.SetFloat(MinWorldSizeId, special ? mapDotSize * 2.2f : mapDotSize);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, mapMaterial, bounds, count, block);
                }
            }

            if (sim.SoldiersDirty)
            {
                sim.RefreshSoldierInstances();
                soldierBuffer.SetData(sim.SoldierInstances);
                sim.SoldiersDirty = false;
            }

            if (sim.SoldierCount > 0)
            {
                if (mapMode)
                {
                    mapSoldierBlock.SetFloat(MinWorldSizeId, mapDotSize * 0.8f);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, mapMaterial, bounds, sim.SoldierCount, mapSoldierBlock);
                }
                else
                {
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, sim.SoldierCount, soldierBlock);
                }
            }

            if (!mapMode)
            {
                shotBuffer.SetData(sim.ShotInstances);
                Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, sim.ShotCapacity, shotBlock);
            }

            if (Time.unscaledTime >= nextPileRefresh)
            {
                nextPileRefresh = Time.unscaledTime + 0.25f;
                RefreshPiles();
            }

            // 성벽 색: 체력이 줄수록 붉게
            for (int side = 0; side < 4; side++)
            {
                BattleSim.SideStatus status = sim.Sides[side];
                float ratio = status.WallMaxHp > 0 ? Mathf.Clamp01((float)status.WallHp / status.WallMaxHp) : 1f;
                walls[side].material.color = Color.Lerp(new Color(0.75f, 0.2f, 0.15f), new Color(0.72f, 0.7f, 0.66f), ratio);
            }
        }

        // 날씨·밤낮 연출 (CY2-C4): 하늘색·햇빛·안개. 화면 씬이 활성 씬이라 RenderSettings는 이 전투에만 쓰인다
        public void ApplyAtmosphere(Camera camera, BattleSim battle)
        {
            bool night = battle.TimeOfDay == HumanLastRampart.Core.CombatContracts.TimeOfDay.Night;
            Color sky;
            float sunIntensity;
            switch (battle.CurrentWeather)
            {
                case HumanLastRampart.Core.CombatContracts.Weather.Cloudy: sky = new Color(0.58f, 0.62f, 0.66f); sunIntensity = 0.7f; break;
                case HumanLastRampart.Core.CombatContracts.Weather.Rain: sky = new Color(0.42f, 0.46f, 0.5f); sunIntensity = 0.5f; break;
                case HumanLastRampart.Core.CombatContracts.Weather.Fog: sky = new Color(0.72f, 0.74f, 0.76f); sunIntensity = 0.6f; break;
                default: sky = new Color(0.45f, 0.6f, 0.75f); sunIntensity = 1f; break;
            }

            if (night)
            {
                sky = Color.Lerp(sky, new Color(0.03f, 0.05f, 0.12f), 0.8f);
                sunIntensity *= 0.25f;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = sky;
            if (sunLight != null)
            {
                sunLight.intensity = sunIntensity;
                sunLight.color = night ? new Color(0.6f, 0.7f, 1f) : Color.white;
            }

            RenderSettings.ambientLight = sky * (night ? 0.6f : 1f);
            RenderSettings.fog = battle.LowVisibility || battle.IsRaining;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            float visible = Mathf.Min(battle.VisibleDistance, battle.IsRaining ? 140f : 10000f);
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = visible + 40f;
        }

        // 시체 더미: 성벽 앞 칸마다 높이만큼 어두운 상자 (그림은 이후 Codex 아트)
        void RefreshPiles()
        {
            PileSpec spec = sim.Pile;
            NativeArray<float> heights = sim.PileHeights;
            for (int k = 0; k < pileBlocks.Length; k++)
            {
                float h = heights[k];
                if (h < 0.05f)
                {
                    continue;
                }

                if (pileBlocks[k] == null)
                {
                    int side = k / spec.Bins;
                    int bin = k % spec.Bins;
                    Vector3 n = ToVector(WallGeometry.Outward(side));
                    Vector3 r = ToVector(WallGeometry.Right(side));
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.name = $"Pile {side}-{bin}";
                    UnityEngine.Object.Destroy(block.GetComponent<Collider>());
                    block.transform.SetParent(root.transform, false);
                    block.transform.localRotation = Quaternion.LookRotation(n);
                    block.transform.localPosition = n * (sim.CastleHalf + 2f) + r * (-sim.CastleHalf + (bin + 0.5f) * spec.BinWidth);
                    block.GetComponent<Renderer>().sharedMaterial = pileMaterial;
                    pileBlocks[k] = block.transform;
                }

                Transform t = pileBlocks[k];
                Vector3 p = t.localPosition;
                t.localPosition = new Vector3(p.x, h * 0.5f, p.z);
                t.localScale = new Vector3(spec.BinWidth, h, 4f);
            }
        }

        // 지도 대포 표시: 모든 대포 위치(흰), 고른 벽 대포의 사거리 원(회색 점선), 조준점(빨강, 쉬는 중이면 회색), 고른 대포(노랑)
        public void DrawMapMarkers(int selectedSide, int selectedCannon, float dot)
        {
            int n = 0;
            void Add(Vector3 p, Color c, float size)
            {
                if (n >= MarkerCapacity) return;
                markers[n++] = new PerfLabInstance { PosFrame = new float4(p.x, 0.5f, p.z, 0f), TintSize = new float4(c.r, c.g, c.b, size) };
            }

            for (int i = 0; i < sim.CannonCount; i++)
            {
                BattleSim.CannonInfo c = sim.GetCannon(i);
                if (!c.Alive) continue;
                bool selected = i == selectedCannon;
                if (c.Side == selectedSide)
                {
                    const int ringDots = 72;
                    for (int k = 0; k < ringDots; k++)
                    {
                        float a = k * Mathf.PI * 2f / ringDots;
                        Add(c.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * c.Range, selected ? new Color(1f, 0.9f, 0.3f) : new Color(0.8f, 0.8f, 0.8f), dot * (selected ? 0.7f : 0.45f));
                    }
                }

                Add(c.Position, selected ? new Color(1f, 0.9f, 0.2f) : Color.white, dot * (selected ? 2.4f : 1.7f));
                if (c.HasAim)
                {
                    Color aimColor = c.Idle ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.2f, 0.2f);
                    Add(c.Aim, aimColor, dot * 2.2f);
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * Mathf.PI * 2f / 12f;
                        Add(c.Aim + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 5f, aimColor, dot * 0.6f);
                    }
                }
            }

            for (int k = n; k < MarkerCapacity; k++) markers[k] = default;
            markerBuffer.SetData(markers);
            if (n > 0) Graphics.DrawMeshInstancedProcedural(quad, 0, mapMaterial, bounds, n, markerBlock);
        }

        // 지도 점: 가장자리가 부드럽지 않은 둥근 점 (알파 잘라내기)
        static Texture2D CreateDotTexture()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "MapDot", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    pixels[y * size + x] = dx * dx + dy * dy <= size * size * 0.25f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            if (mapMaterial != null) UnityEngine.Object.Destroy(mapMaterial);
            markerBuffer?.Release();
            if (markers.IsCreated) markers.Dispose();
            if (dotTexture != null) UnityEngine.Object.Destroy(dotTexture);
            if (pileMaterial != null) UnityEngine.Object.Destroy(pileMaterial);
            foreach (var draw in enemyDraws)
            {
                draw.buffer.Release();
            }

            enemyDraws.Clear();
            soldierBuffer?.Release();
            shotBuffer?.Release();
            if (material != null) UnityEngine.Object.Destroy(material);
            if (quad != null) UnityEngine.Object.Destroy(quad);
            if (placeholderSheet != null) UnityEngine.Object.Destroy(placeholderSheet);
            if (shotTexture != null) UnityEngine.Object.Destroy(shotTexture);
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
