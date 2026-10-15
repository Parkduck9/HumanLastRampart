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
        static readonly int NormalTexId = Shader.PropertyToID("_NormalTex");
        static readonly int HasNormalId = Shader.PropertyToID("_HasNormal");

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
        const int MarkerCapacity = 2048;   // 대포 사거리 원 + 함정 표시 (D-050)
        readonly GraphicsBuffer markerBuffer;
        readonly MaterialPropertyBlock markerBlock;
        NativeArray<PerfLabInstance> markers;
        readonly Mesh quad;
        readonly Texture2D placeholderSheet;
        readonly Texture2D shotTexture;
        readonly List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)> enemyDraws =
            new List<(GraphicsBuffer, MaterialPropertyBlock, int, int)>();
        readonly GraphicsBuffer soldierBuffer;   // 전체 병사 (지도 점)
        readonly MaterialPropertyBlock soldierBlock;
        readonly List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)> soldierDraws =
            new List<(GraphicsBuffer, MaterialPropertyBlock, int, int)>();
        // 달리는 병사(전령·우회 보고) — 병종별 달리기 그림, 미리 연결 (사용자 검토 전)
        readonly List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)> runnerDraws =
            new List<(GraphicsBuffer, MaterialPropertyBlock, int, int)>();
        readonly GraphicsBuffer runnerBuffer;   // 전체 (지도 점)
        readonly MaterialPropertyBlock mapRunnerBlock;
        readonly BattlePropView props;          // 성벽 난간·대포 그림
        readonly BattleWorldView world;         // 성벽·포탑 3D 목업 (K-001)
        readonly BattleLights lights;           // 밤 횃불·폭발 빛 (K-010)
        readonly BattleEffectsView effectArt;   // 마법·함정 폭발, 불바다, 맞음 불꽃, 횃불 불꽃 그림 (K-015)
        HumanLastRampart.Combat.Render.BattleLook look;   // 화면 효과 (빛번짐·색 보정·미니어처 흐림)
        readonly GraphicsBuffer shotBuffer;
        readonly MaterialPropertyBlock shotBlock;
        // 주인공 (성벽 위 한 명). 시트가 없으면 임시 그림 — 같은 4행 순서 (PerfLabArt.HeroLayout)
        readonly GraphicsBuffer heroBuffer;
        readonly MaterialPropertyBlock heroBlock;
        readonly Texture2D heroPlaceholder;
        readonly PerfLabInstance[] heroInstance = new PerfLabInstance[1];
        bool heroVisible;
        bool heroIdle;
        readonly MaterialPropertyBlock heroIdleBlock;   // 서 있기 1칸 (D-066) — 없으면 시트 0번 칸
        readonly GraphicsBuffer wizardBuffer;          // 마법사 (D-058) — 그림이 있을 때만
        readonly MaterialPropertyBlock wizardBlock;
        readonly PerfLabInstance[] wizardInstance = new PerfLabInstance[1];
        readonly Renderer[] walls = new Renderer[4];
        Light sunLight;
        Transform[] pileBlocks;          // 시체 더미 칸마다 상자 (높이가 생기면 만든다)
        readonly List<Renderer> effectDiscs = new List<Renderer>();     // 마법 효과 (MAP-4) — 임시 모양
        readonly List<Renderer> effectFlashes = new List<Renderer>();
        Material pileMaterial;
        float nextPileRefresh;
        readonly Bounds bounds;
        readonly GameObject root;

        public BattleView(BattleSim sim, Shader shader, Transform parent, Texture2D heroSheet = null, Texture2D heroIdleSheet = null,
            Texture2D heroNormalSheet = null, Texture2D heroIdleNormalSheet = null)
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
                    Apply(block, def.Sheet, PerfLabArt.FromSheet(def.Sheet), def.NormalSheet);   // 칸 256×384 — 고블린 4×2, 정적 데모 1×1
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
            Apply(soldierBlock, placeholderSheet, PerfLabArt.PlaceholderLayout);   // 그림 없는 병종용
            soldierBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, sim.SoldierCount), PerfLabInstance.Stride);
            soldierBlock.SetBuffer(InstancesId, soldierBuffer);
            // 병종별 그림 (Codex 정적 뒷모습 데모, M-CX-049). 그림이 없으면 임시 그림
            foreach (var (unit, start, count) in sim.SoldierGroups)
            {
                if (count <= 0) continue;
                var block = new MaterialPropertyBlock();
                if (unit != null && unit.Sheet != null) Apply(block, unit.Sheet, PerfLabArt.FromSheet(unit.Sheet), unit.NormalSheet);
                else Apply(block, placeholderSheet, PerfLabArt.PlaceholderLayout);
                var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, PerfLabInstance.Stride);
                block.SetBuffer(InstancesId, buffer);
                soldierDraws.Add((buffer, block, start, count));

                if (unit != null && unit.RunSheet != null)
                {
                    var runBlock = new MaterialPropertyBlock();
                    Apply(runBlock, unit.RunSheet, PerfLabArt.FromSheet(unit.RunSheet), unit.RunNormalSheet);
                    var runBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, PerfLabInstance.Stride);
                    runBlock.SetBuffer(InstancesId, runBuffer);
                    runnerDraws.Add((runBuffer, runBlock, start, count));
                }
            }

            runnerBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, sim.SoldierCount), PerfLabInstance.Stride);
            mapRunnerBlock = new MaterialPropertyBlock();
            mapRunnerBlock.SetTexture(MainTexId, dotTexture);
            mapRunnerBlock.SetColor(MapColorId, MapSoldierColor);
            mapRunnerBlock.SetBuffer(InstancesId, runnerBuffer);
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

            heroBlock = new MaterialPropertyBlock();
            if (heroSheet != null)
            {
                Apply(heroBlock, heroSheet, PerfLabArt.HeroLayout, heroNormalSheet);
            }
            else
            {
                heroPlaceholder = PerfLabArt.CreateHeroPlaceholderSheet();
                Apply(heroBlock, heroPlaceholder, PerfLabArt.HeroPlaceholderLayout);
            }

            heroBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, PerfLabInstance.Stride);
            heroBlock.SetBuffer(InstancesId, heroBuffer);
            if (heroIdleSheet != null)
            {
                heroIdleBlock = new MaterialPropertyBlock();
                Apply(heroIdleBlock, heroIdleSheet, PerfLabArt.FromSheet(heroIdleSheet), heroIdleNormalSheet);
                heroIdleBlock.SetBuffer(InstancesId, heroBuffer);
            }

            if (sim.WizardPresent && sim.WizardUnit != null && sim.WizardUnit.Sheet != null)
            {
                wizardBlock = new MaterialPropertyBlock();
                Apply(wizardBlock, sim.WizardUnit.Sheet, PerfLabArt.FromSheet(sim.WizardUnit.Sheet), sim.WizardUnit.NormalSheet);   // 정적 뒷모습 1×1
                wizardBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, PerfLabInstance.Stride);
                wizardBlock.SetBuffer(InstancesId, wizardBuffer);
            }

            root = new GameObject("Battlefield");
            root.transform.SetParent(parent, false);
            props = new BattlePropView(sim, material, quad, bounds);
            world = new BattleWorldView(sim, root.transform);
            lights = new BattleLights(sim, root.transform);   // 밤 횃불·폭발 빛 (K-010, D-062 4단계)
            effectArt = new BattleEffectsView(sim, material, quad, bounds);   // 효과 도트 그림 (K-015)
            BuildEnvironment();
        }

        // normal = 짝 노멀맵(같은 칸 배치). 없으면 _HasNormal 0 — 셰이더가 기본 감싸는 빛으로 그린다 (D-062 G4)
        static void Apply(MaterialPropertyBlock block, Texture texture, PerfLabArt.SheetLayout layout, Texture normal = null)
        {
            block.SetTexture(MainTexId, texture);
            block.SetVector(GridId, layout.Grid);
            block.SetVector(PivotId, layout.Pivot);
            block.SetFloat(AspectId, layout.Aspect);
            if (normal != null) block.SetTexture(NormalTexId, normal);
            block.SetFloat(HasNormalId, normal != null ? 1f : 0f);
        }

        void BuildEnvironment()
        {
            float half = sim.CastleHalf;
            float field = sim.FieldSize;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            // 지도 전체 보기(세로 ±field+20)를 넓은 화면 비율로 봐도 회색 가장자리가 없게 가로·세로 두 배로 깐다
            ground.transform.localScale = new Vector3((field + 20f) * 4f / 10f, 1f, (field + 20f) * 4f / 10f);
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

            // 언덕 (함정을 놓을 수 없는 곳, D-050) — 낮고 넓은 둔덕. 적은 그냥 지나간다(1회차는 통행 영향 없음)
            foreach (Vector3 hill in sim.Hills)
            {
                var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mound.name = "Hill";
                UnityEngine.Object.Destroy(mound.GetComponent<Collider>());
                mound.transform.SetParent(root.transform, false);
                mound.transform.localPosition = new Vector3(hill.x, -0.6f, hill.z);
                mound.transform.localScale = new Vector3(hill.y * 2f, 2.4f, hill.y * 2f);   // 꼭대기 0.6m — 적이 파묻혀 보이지 않게 낮게
                Tint(mound.GetComponent<Renderer>(), new Color(0.36f, 0.43f, 0.24f));
            }

            pileBlocks = new Transform[4 * sim.Pile.Bins];
            pileMaterial = new Material(walls[0].sharedMaterial) { name = "Corpse Pile (runtime)", color = new Color(0.32f, 0.22f, 0.14f) };

            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.SetParent(root.transform, false);
            // 3D 목업이면 해를 옆에서 비춰 난간·탑 그림자가 통로에 보이게 (카메라 뒤에서 비추면 그림자가 성벽 밖으로만 떨어진다)
            sun.transform.rotation = BattleWorldView.Active ? Quaternion.Euler(42f, 125f, 0f) : Quaternion.Euler(50f, -30f, 0f);
            sunLight = sun.GetComponent<Light>();
            sunLight.type = LightType.Directional;
        }

        static void Tint(Renderer renderer, Color color)
        {
            renderer.material.color = color;
        }

        static Vector3 ToVector(Unity.Mathematics.float3 v) => new Vector3(v.x, v.y, v.z);

        // 주인공 한 명 (성벽 위). frame = 시트 칸 번호(행×4+열), flip이면 좌우 뒤집기
        public const float HeroDrawSize = 1.5f * BattleSim.WallFigureScale;   // 병사(1.4×0.25)보다 조금 크게

        public void SetHero(Vector3 feet, int frame, bool flip, bool visible, bool idle = false)
        {
            heroVisible = visible;
            heroIdle = idle && heroIdleBlock != null;
            if (heroIdle) frame = 0;
            float shade = sim.Brightness;
            heroInstance[0] = new PerfLabInstance
            {
                PosFrame = new float4(feet.x, feet.y, feet.z, flip ? -frame - 1 : frame),
                TintSize = new float4(shade, shade, shade, HeroDrawSize),
            };
        }

        // 작업이 끝난 뒤(sim.Complete 후) 부른다. 지도 모드면 같은 데이터를 점으로 그린다(mapDotSize = 최소 점 크기 m)
        public void Draw(bool mapMode = false, float mapDotSize = 0f)
        {
            if (look != null)
            {
                bool litBefore = HumanLastRampart.Combat.Render.BattleLook.LitActive;
                look.SetMapMode(mapMode);
                look.Refresh();
                if (litBefore != HumanLastRampart.Combat.Render.BattleLook.LitActive) sim.SoldiersDirty = true;   // 밝기 숫자가 바뀌었으니 병사 그림 다시
            }
            lights?.Update(mapMode);
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
                foreach (var (buffer, _, start, count) in soldierDraws) buffer.SetData(sim.SoldierInstances, start, 0, count);
                runnerBuffer.SetData(sim.RunnerInstances);
                foreach (var (buffer, _, start, count) in runnerDraws) buffer.SetData(sim.RunnerInstances, start, 0, count);
                sim.SoldiersDirty = false;
            }

            if (sim.SoldierCount > 0)
            {
                if (mapMode)
                {
                    mapSoldierBlock.SetFloat(MinWorldSizeId, mapDotSize * 0.8f);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, mapMaterial, bounds, sim.SoldierCount, mapSoldierBlock);
                    mapRunnerBlock.SetFloat(MinWorldSizeId, mapDotSize * 0.8f);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, mapMaterial, bounds, sim.SoldierCount, mapRunnerBlock);
                }
                else
                {
                    foreach (var (_, block, _, count) in soldierDraws)
                        Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
                    foreach (var (_, block, _, count) in runnerDraws)
                        Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
                }
            }

            if (!mapMode)
            {
                props.Draw();
                shotBuffer.SetData(sim.ShotInstances);
                Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, sim.ShotCapacity, shotBlock);
                if (heroVisible)
                {
                    heroBuffer.SetData(heroInstance);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, 1, heroIdle ? heroIdleBlock : heroBlock);
                }

                if (wizardBuffer != null)
                {
                    Vector3 feet = sim.WizardFeet;
                    float shade = sim.Brightness;
                    wizardInstance[0] = new PerfLabInstance
                    {
                        PosFrame = new float4(feet.x, feet.y, feet.z, 0f),
                        TintSize = new float4(shade, shade, shade, HeroDrawSize),   // 주인공과 같은 크기
                    };
                    wizardBuffer.SetData(wizardInstance);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, 1, wizardBlock);
                }
            }

            if (Time.unscaledTime >= nextPileRefresh)
            {
                nextPileRefresh = Time.unscaledTime + 0.25f;
                RefreshPiles();
            }

            DrawEffects();
            effectArt?.Draw(mapMode);

            // 성벽 색: 체력이 줄수록 붉게
            for (int side = 0; side < 4; side++)
            {
                BattleSim.SideStatus status = sim.Sides[side];
                float ratio = status.WallMaxHp > 0 ? Mathf.Clamp01((float)status.WallHp / status.WallMaxHp) : 1f;
                walls[side].material.color = Color.Lerp(new Color(0.75f, 0.2f, 0.15f), new Color(0.72f, 0.7f, 0.66f), ratio);
                world?.SetWallHealth(side, ratio);   // 질감 판(통로·바깥면)에도 같은 붉은 기
            }
        }

        // 날씨·밤낮 연출 (CY2-C4): 하늘색·햇빛·안개. 화면 씬이 활성 씬이라 RenderSettings는 이 전투에만 쓰인다
        public void ApplyAtmosphere(Camera camera, BattleSim battle)
        {
            bool night = battle.TimeOfDay == HumanLastRampart.Core.CombatContracts.TimeOfDay.Night;
            if (look == null) look = new HumanLastRampart.Combat.Render.BattleLook(camera, root.transform);   // 화면 효과 (D-062 1단계)
            look.Apply(battle.CurrentWeather, night);
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

        // 마법 효과 (임시 모양): 불바다 = 주황 원판, 폭발 대기 = 어두운 노랑 원판, 터짐 = 밝은 공
        void DrawEffects()
        {
            IReadOnlyList<BattleSim.SpellEffect> list = sim.Effects;
            int discs = 0, flashes = 0;
            foreach (BattleSim.SpellEffect e in list)
            {
                if (!e.Fire && !e.Pending && effectArt != null && effectArt.HasBurstArt) continue;   // 터짐은 도트 그림으로 (K-015). 원판은 그대로
                if (e.Fire || e.Pending)
                {
                    Renderer disc = Pooled(effectDiscs, discs++, PrimitiveType.Cylinder, "Spell Disc");
                    disc.transform.localPosition = new Vector3(e.Position.x, 0.06f, e.Position.z);
                    disc.transform.localScale = new Vector3(e.Radius * 2f, 0.03f, e.Radius * 2f);
                    float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f + e.Position.x);
                    disc.material.color = e.Fire ? new Color(1f, 0.35f, 0.05f) * flicker : new Color(0.55f, 0.5f, 0.1f);
                }
                else
                {
                    Renderer flash = Pooled(effectFlashes, flashes++, PrimitiveType.Sphere, "Spell Flash");
                    float grow = 1f - Mathf.Clamp01(e.TimeLeft / 0.4f);
                    flash.transform.localPosition = new Vector3(e.Position.x, 1f, e.Position.z);
                    flash.transform.localScale = Vector3.one * e.Radius * (0.6f + grow * 1.2f);
                    flash.material.color = Color.Lerp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.4f, 0.1f), grow);
                }
            }

            // 터진 함정 구덩이 (실제 위치, 어두운 원) — D-050
            for (int t = 0; t < sim.TrapCount; t++)
            {
                BattleSim.TrapInfo trap = sim.GetTrap(t);
                if (trap.State != BattleSim.TrapState.Exploded) continue;
                Renderer crater = Pooled(effectDiscs, discs++, PrimitiveType.Cylinder, "Trap Crater");
                crater.transform.localPosition = new Vector3(trap.Position.x, 0.04f, trap.Position.z);
                crater.transform.localScale = new Vector3(trap.CraterRadius * 2f, 0.02f, trap.CraterRadius * 2f);
                crater.material.color = new Color(0.18f, 0.13f, 0.09f);
            }

            for (int i = discs; i < effectDiscs.Count; i++) effectDiscs[i].gameObject.SetActive(false);
            for (int i = flashes; i < effectFlashes.Count; i++) effectFlashes[i].gameObject.SetActive(false);
        }

        Renderer Pooled(List<Renderer> pool, int index, PrimitiveType type, string name)
        {
            while (pool.Count <= index)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root.transform, false);
                pool.Add(go.GetComponent<Renderer>());
            }

            pool[index].gameObject.SetActive(true);
            return pool[index];
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
        public void DrawMapMarkers(int selectedSide, int selectedCannon, float dot, bool showSpellRange = false, Vector3? lens = null)
        {
            int n = 0;
            void Add(Vector3 p, Color c, float size)
            {
                if (n >= MarkerCapacity) return;
                markers[n++] = new PerfLabInstance { PosFrame = new float4(p.x, 0.5f, p.z, 0f), TintSize = new float4(c.r, c.g, c.b, size) };
            }

            void Ring(Vector3 center, float radius, Color c, float size, int count)
            {
                for (int k = 0; k < count; k++)
                {
                    float a = k * Mathf.PI * 2f / count;
                    Add(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, c, size);
                }
            }

            // 망원경 렌즈 (하늘색 원, MAP-5)
            if (lens.HasValue)
            {
                Ring(lens.Value, BattleSim.TelescopeRadius, new Color(0.4f, 0.9f, 1f), dot * 1.2f, 48);
                Ring(lens.Value, BattleSim.TelescopeRadius * 0.96f, new Color(0.4f, 0.9f, 1f), dot * 0.8f, 48);
            }

            // 마법사 위치 (보라 점, D-058)
            if (sim.WizardPresent) Add(sim.WizardFeet, new Color(0.75f, 0.45f, 1f), dot * 1.8f);

            // 마법 효과: 불바다(주황 원), 터질 폭발(노랑 원)
            foreach (BattleSim.SpellEffect e in sim.Effects)
            {
                if (e.Fire) Ring(e.Position, e.Radius, new Color(1f, 0.45f, 0.1f), dot * 1.1f, 28);
                else if (e.Pending) Ring(e.Position, e.Radius, new Color(1f, 0.95f, 0.2f), dot * 1.1f, 28);
            }

            // 마법을 고른 동안: 쓸 수 있는 범위(성벽에서 150m) 네모 점선
            if (showSpellRange)
            {
                float edge = sim.CastleHalf + BattleSim.SpellCastRange;
                for (int k = -20; k <= 20; k++)
                {
                    float t = k / 20f * edge;
                    Color c = new Color(0.75f, 0.5f, 1f);
                    Add(new Vector3(t, 0f, edge), c, dot * 0.6f);
                    Add(new Vector3(t, 0f, -edge), c, dot * 0.6f);
                    Add(new Vector3(edge, 0f, t), c, dot * 0.6f);
                    Add(new Vector3(-edge, 0f, t), c, dot * 0.6f);
                }
            }

            // 우회 적 보고를 받은 면 (D-053): 성벽 앞에 빨간 줄이 깜빡인다
            bool blinkOn = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.35f;
            for (int side = 0; side < 4 && blinkOn; side++)
            {
                if (!sim.SideAlert(side)) continue;
                var outward = WallGeometry.Outward(side);
                var right = WallGeometry.Right(side);
                for (int k = -12; k <= 12; k++)
                {
                    Vector3 p = new Vector3(outward.x, 0f, outward.z) * (sim.CastleHalf + 12f) + new Vector3(right.x, 0f, right.z) * (k / 12f * sim.CastleHalf);
                    Add(p, new Color(1f, 0.15f, 0.15f), dot * 1.6f);
                }
            }

            // 함정 (D-050): 설치 때 남긴 표시 위치에만. 묻힘(주황) · 점화 중(노랑, 피해 거리 원) · 터짐(구덩이 모양) · 불발(회색)
            for (int i = 0; i < sim.TrapCount; i++)
            {
                BattleSim.TrapInfo t = sim.GetTrap(i);
                if (!t.Marked) continue;
                switch (t.State)
                {
                    case BattleSim.TrapState.Armed:
                        Add(t.Mark, new Color(1f, 0.55f, 0.1f), dot * 1.8f);
                        Ring(t.Mark, 2.5f, new Color(1f, 0.55f, 0.1f), dot * 0.6f, 10);
                        break;
                    case BattleSim.TrapState.Fusing:
                        Add(t.Mark, new Color(1f, 0.95f, 0.3f), dot * 2.4f);
                        Ring(t.Mark, t.DamageRadius, new Color(1f, 0.95f, 0.3f), dot * 0.8f, 24);
                        break;
                    case BattleSim.TrapState.Exploded:
                        Add(t.Mark, new Color(0.12f, 0.08f, 0.05f), dot * 2.2f);
                        Ring(t.Mark, t.CraterRadius, new Color(0.45f, 0.28f, 0.12f), dot * 0.9f, 16);
                        Ring(t.Mark, t.CraterRadius + 1.6f, new Color(0.9f, 0.4f, 0.1f), dot * 0.5f, 8);   // 터져 나간 자국
                        break;
                    default:
                        Add(t.Mark, new Color(0.55f, 0.55f, 0.55f), dot * 1.6f);
                        Ring(t.Mark, 2.5f, new Color(0.55f, 0.55f, 0.55f), dot * 0.5f, 10);
                        break;
                }
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
            look?.Dispose();
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
            foreach (var draw in soldierDraws) draw.buffer.Release();
            soldierDraws.Clear();
            foreach (var draw in runnerDraws) draw.buffer.Release();
            runnerDraws.Clear();
            runnerBuffer?.Release();
            props?.Dispose();
            world?.Dispose();
            lights?.Dispose();
            effectArt?.Dispose();
            soldierBuffer?.Release();
            shotBuffer?.Release();
            heroBuffer?.Release();
            wizardBuffer?.Release();
            if (heroPlaceholder != null) UnityEngine.Object.Destroy(heroPlaceholder);
            if (material != null) UnityEngine.Object.Destroy(material);
            if (quad != null) UnityEngine.Object.Destroy(quad);
            if (placeholderSheet != null) UnityEngine.Object.Destroy(placeholderSheet);
            if (shotTexture != null) UnityEngine.Object.Destroy(shotTexture);
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
