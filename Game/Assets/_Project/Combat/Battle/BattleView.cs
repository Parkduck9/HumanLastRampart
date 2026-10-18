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
        static readonly int FrameWindowId = Shader.PropertyToID("_FrameWindow");
        static readonly int ShotModeId = Shader.PropertyToID("_ShotMode");
        static readonly int ShotColorId = Shader.PropertyToID("_ShotColor");
        static readonly int ScreenUprightId = Shader.PropertyToID("_ScreenUpright");
        static readonly int SheetScaleId = Shader.PropertyToID("_SheetScale");
        static readonly int ViewStretchId = Shader.PropertyToID("_ViewStretch");

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
        // 동작 그림 (K-032 미리 연결, 사용자 검토 전): 같은 개체 버퍼를 시트마다 한 번 더 그린다 (_FrameWindow 구간)
        readonly List<(MaterialPropertyBlock block, int count)> enemyPoseDraws = new List<(MaterialPropertyBlock, int)>();
        readonly List<(MaterialPropertyBlock block, int count)> soldierPoseDraws = new List<(MaterialPropertyBlock, int)>();
        readonly List<(GraphicsBuffer buffer, MaterialPropertyBlock block, int start, int count)> ladderDraws =
            new List<(GraphicsBuffer, MaterialPropertyBlock, int, int)>();
        // 투사체: 무기마다 한 번 (프레임 번호 = 무기 번호). art = 투사체 그림을 날아가는 방향으로 돌림, 아니면 예전 막대
        readonly List<(MaterialPropertyBlock block, bool art, bool area)> shotDraws = new List<(MaterialPropertyBlock, bool, bool)>();
        MaterialPropertyBlock wizardCastBlock;          // 시전 4칸 (마법이 나간 뒤 CastSeconds 동안)
        int wizardCastFrames;
        const float WizardCastFps = 6f;
        const float WizardCastSeconds = 1f;
        MaterialPropertyBlock heroCommandBlock;          // 명령 손짓 3칸 (휠 메뉴, SetHeroCommandArt)
        int heroCommandFrame = -1;
        // 화상 불꽃 (K-035): 불타는 적 몸 위에 작은 횃불 불꽃 (Resources BattleArt/Effects/torch_flame, 6칸 10fps). 없으면 붉은 깜빡임만
        const int FlameCapacity = 512;
        const int FlameFrames = 6;
        const float FlameFps = 10f;
        readonly GraphicsBuffer flameBuffer;
        readonly MaterialPropertyBlock flameBlock;
        readonly PerfLabInstance[] flameInstances;
        // 3등신 시험 그림 (K-057, ProportionTrial): 켜고 끌 때 궁수·주인공 블록의 그림만 바꿔 끼운다. 꺼져 있으면 아무것도 안 함
        UnitDef trialArcher;
        MaterialPropertyBlock trialArcherBlock, trialArcherFireBlock;
        readonly Texture2D heroSheetOriginal, heroIdleOriginal;
        readonly Texture heroNormalOriginal, heroIdleNormalOriginal;
        bool trialShown;
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
            var flame = Resources.Load<Texture2D>("BattleArt/Effects/torch_flame");
            if (flame != null)
            {
                flameBlock = new MaterialPropertyBlock();
                Apply(flameBlock, flame, PerfLabArt.SheetLayout.Make(FlameFrames, 1, 0.5f, 0f, 1f));
                flameInstances = new PerfLabInstance[FlameCapacity];
                flameBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, FlameCapacity, PerfLabInstance.Stride);
                flameBlock.SetBuffer(InstancesId, flameBuffer);
            }

            material = new Material(shader) { name = "BattleSprite (runtime)" };
            mapMaterial = new Material(shader) { name = "BattleMapDots (runtime)" };
            mapMaterial.SetFloat(MapModeId, 1f);
            dotTexture = CreateDotTexture();
            float extent = sim.FieldSize + 20f;
            bounds = new Bounds(Vector3.zero, new Vector3(extent * 2f, 200f, extent * 2f));

            foreach (var (def, start, count) in sim.EnemyGroups)
            {
                var block = new MaterialPropertyBlock();
                // 적은 각자 기본 시트 인물 키가 기준 (K-034) — 기본 시트는 배율 1(크기 그대로), 동작 시트만 그 키에 맞춘다
                float enemyFigurePx = def.Sheet != null ? PerfLabArt.FigurePx(def.Sheet) : PerfLabArt.FigureReferencePx;
                if (def.Sheet != null)
                {
                    ApplyFigure(block, def.Sheet, def.NormalSheet, enemyFigurePx);   // 칸 256×384 — 고블린 4×2, 정적 데모 1×1
                }
                else
                {
                    Apply(block, placeholderSheet, PerfLabArt.PlaceholderLayout, null, 1f, true);
                }

                var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), PerfLabInstance.Stride);
                block.SetBuffer(InstancesId, buffer);
                block.SetVector(FrameWindowId, PerfLabArt.Window(0));
                enemyDraws.Add((buffer, block, start, count));
                if (def.Sheet != null)
                {
                    // 공격·세우기·오르기·쓰러짐 시트 (K-032). 칸 번호 구간은 WriteBattleEnemyInstancesJob
                    AddPose(enemyPoseDraws, buffer, count, def.AttackSheet, PerfLabArt.AttackFrame0, enemyFigurePx);
                    AddPose(enemyPoseDraws, buffer, count, def.DeploySheet, PerfLabArt.DeployFrame0, enemyFigurePx);
                    AddPose(enemyPoseDraws, buffer, count, def.ClimbSheet, PerfLabArt.ClimbFrame0, enemyFigurePx);
                    AddPose(enemyPoseDraws, buffer, count, def.DeathSheet, PerfLabArt.FallFrame0, enemyFigurePx);
                }

                if (def.CarriesLadder && def.LadderSheet != null && count > 0)
                {
                    var ladderBlock = new MaterialPropertyBlock();
                    Apply(ladderBlock, def.LadderSheet, PerfLabArt.LadderLayout);
                    ladderBlock.SetFloat(ScreenUprightId, 0f);   // 사다리는 성벽에 기대 선 물건 — 화면 기준으로 눕히지 않고 세로 판 그대로 (K-040)
                    var ladderBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, PerfLabInstance.Stride);
                    ladderBlock.SetBuffer(InstancesId, ladderBuffer);
                    ladderDraws.Add((ladderBuffer, ladderBlock, start, count));
                }

                var mapBlock = new MaterialPropertyBlock();
                mapBlock.SetTexture(MainTexId, dotTexture);
                mapBlock.SetColor(MapColorId, def.IsSpecial ? MapSpecialColor : MapEnemyColor);
                mapBlock.SetBuffer(InstancesId, buffer);
                mapBlock.SetVector(FrameWindowId, PerfLabArt.Window(0, PerfLabArt.FallFrame0));   // 쓰러지는 적은 점으로 안 그림
                mapEnemyDraws.Add((mapBlock, start, count, def.IsSpecial));
            }

            soldierBlock = new MaterialPropertyBlock();
            Apply(soldierBlock, placeholderSheet, PerfLabArt.PlaceholderLayout, null, 1f, true);   // 그림 없는 병종용 (임시 그림은 칸 = 인물)
            soldierBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, sim.SoldierCount), PerfLabInstance.Stride);
            soldierBlock.SetBuffer(InstancesId, soldierBuffer);
            // 병종별 그림 (Codex 정적 뒷모습 데모, M-CX-049). 그림이 없으면 임시 그림
            foreach (var (unit, start, count) in sim.SoldierGroups)
            {
                if (count <= 0) continue;
                var block = new MaterialPropertyBlock();
                // 병사 시트는 모두 인물 키 = 병사 크기(0.35m)가 되게 (K-034: 서기·사격·쓰러짐·달리기 같은 크기)
                if (unit != null && unit.Sheet != null) ApplyFigure(block, unit.Sheet, unit.NormalSheet);
                else Apply(block, placeholderSheet, PerfLabArt.PlaceholderLayout, null, 1f, true);
                var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, PerfLabInstance.Stride);
                block.SetBuffer(InstancesId, buffer);
                block.SetVector(FrameWindowId, PerfLabArt.Window(0));
                soldierDraws.Add((buffer, block, start, count));
                if (unit != null && unit.Sheet != null)
                {
                    // 사격·쓰러짐 시트 (K-032). 칸 번호 구간은 BattleSim.SoldierPose
                    AddPose(soldierPoseDraws, buffer, count, unit.FireSheet, PerfLabArt.FireFrame0);
                    if (ProportionTrial.IsArcher(unit))
                    {
                        // 3등신 시험 그림으로 바꿔 끼울 블록 (K-057)
                        trialArcher = unit;
                        trialArcherBlock = block;
                        trialArcherFireBlock = unit.FireSheet != null ? soldierPoseDraws[soldierPoseDraws.Count - 1].block : null;
                    }

                    AddPose(soldierPoseDraws, buffer, count, unit.DeathSheet, PerfLabArt.FallFrame0);
                    AddPose(soldierPoseDraws, buffer, count, unit.IgniteSheet, PerfLabArt.IgniteFrame0);   // 대포병 불붙이기 (K-046)
                }

                if (unit != null && unit.RunSheet != null)
                {
                    var runBlock = new MaterialPropertyBlock();
                    ApplyFigure(runBlock, unit.RunSheet, unit.RunNormalSheet);   // 달리기 그림 인물이 작게 그려져 있어 배율로 키운다 (K-034)
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
            mapSoldierBlock.SetVector(FrameWindowId, PerfLabArt.Window(0, PerfLabArt.FallFrame0));   // 쓰러지는 병사는 점으로 안 그림
            markerBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MarkerCapacity, PerfLabInstance.Stride);
            markers = new NativeArray<PerfLabInstance>(MarkerCapacity, Allocator.Persistent);
            markerBlock = new MaterialPropertyBlock();
            markerBlock.SetTexture(MainTexId, dotTexture);
            markerBlock.SetColor(MapColorId, new Color(0f, 0f, 0f, 0f));   // 개체 색 사용
            markerBlock.SetFloat(MinWorldSizeId, 0f);
            markerBlock.SetBuffer(InstancesId, markerBuffer);

            shotBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sim.ShotCapacity, PerfLabInstance.Stride);
            // 무기마다 한 번: 그 무기를 든 병종의 투사체 그림(K-032)을 날아가는 방향으로, 없으면 예전 막대
            for (int w = 0; w < sim.WeaponUnits.Count; w++)
            {
                UnitDef unit = sim.WeaponUnits[w];
                var block = new MaterialPropertyBlock();
                bool art = unit != null && unit.ProjectileSheet != null;
                if (art) Apply(block, unit.ProjectileSheet, PerfLabArt.ProjectileLayout(unit.ProjectileSheet));
                else Apply(block, shotTexture, PerfLabArt.ArrowLayout);
                block.SetFloat(ShotModeId, art ? 2f : 1f);
                block.SetVector(FrameWindowId, PerfLabArt.Window(w, 1));
                block.SetBuffer(InstancesId, shotBuffer);
                shotDraws.Add((block, art, sim.WeaponIsArea(w)));
            }

            heroBlock = new MaterialPropertyBlock();
            if (heroSheet != null)
            {
                Apply(heroBlock, heroSheet, PerfLabArt.HeroLayout, heroNormalSheet, PerfLabArt.SheetScale(heroSheet), true);
            }
            else
            {
                heroPlaceholder = PerfLabArt.CreateHeroPlaceholderSheet();
                Apply(heroBlock, heroPlaceholder, PerfLabArt.HeroPlaceholderLayout, null, 1f, true);
            }

            heroBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, PerfLabInstance.Stride);
            heroBlock.SetBuffer(InstancesId, heroBuffer);
            heroSheetOriginal = heroSheet;            // 3등신 시험 그림에서 되돌릴 원래 그림 (K-057)
            heroIdleOriginal = heroIdleSheet;
            heroNormalOriginal = heroNormalSheet;
            heroIdleNormalOriginal = heroIdleNormalSheet;
            if (heroIdleSheet != null)
            {
                heroIdleBlock = new MaterialPropertyBlock();
                ApplyFigure(heroIdleBlock, heroIdleSheet, heroIdleNormalSheet);
                heroIdleBlock.SetBuffer(InstancesId, heroBuffer);
            }

            if (sim.WizardPresent && sim.WizardUnit != null && sim.WizardUnit.Sheet != null)
            {
                wizardBlock = new MaterialPropertyBlock();
                ApplyFigure(wizardBlock, sim.WizardUnit.Sheet, sim.WizardUnit.NormalSheet);   // 정적 뒷모습 1×1
                wizardBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, PerfLabInstance.Stride);
                wizardBlock.SetBuffer(InstancesId, wizardBuffer);
                if (sim.WizardUnit.CastSheet != null)
                {
                    // 시전 뒷모습 4칸 (K-032). 노멀맵은 정적 그림 짝뿐이라 붙이지 않는다
                    wizardCastBlock = new MaterialPropertyBlock();
                    ApplyFigure(wizardCastBlock, sim.WizardUnit.CastSheet);
                    wizardCastBlock.SetBuffer(InstancesId, wizardBuffer);
                    wizardCastFrames = PerfLabArt.FrameCount(sim.WizardUnit.CastSheet);
                }
            }

            root = new GameObject("Battlefield");
            root.transform.SetParent(parent, false);
            props = new BattlePropView(sim, material, quad, bounds);
            world = new BattleWorldView(sim, root.transform);
            lights = new BattleLights(sim, root.transform);   // 밤 횃불·폭발 빛 (K-010, D-062 4단계)
            effectArt = new BattleEffectsView(sim, material, quad, bounds);   // 효과 도트 그림 (K-015)
            effectArt.Attach(new BattleFieldArt(sim, material, quad, root.transform));   // 들판 소품·시체 더미·함정·먼 산·하늘 (K-031) — 그리기·정리는 effectArt가 함께
            BuildEnvironment();
        }

        // normal = 짝 노멀맵(같은 칸 배치). 없으면 _HasNormal 0 — 셰이더가 기본 감싸는 빛으로 그린다 (D-062 G4)
        // sheetScale = 시트별 인물 크기 배율, figure = 캐릭터(납작함 보정 켬) — K-034
        static void Apply(MaterialPropertyBlock block, Texture texture, PerfLabArt.SheetLayout layout, Texture normal = null,
            float sheetScale = 1f, bool figure = false)
        {
            block.SetTexture(MainTexId, texture);
            block.SetVector(GridId, layout.Grid);
            block.SetVector(PivotId, layout.Pivot);
            block.SetFloat(AspectId, layout.Aspect);
            if (normal != null) block.SetTexture(NormalTexId, normal);
            block.SetFloat(HasNormalId, normal != null ? 1f : 0f);
            block.SetFloat(SheetScaleId, sheetScale);
            block.SetFloat(ViewStretchId, figure ? 1f : 0f);
        }

        // 캐릭터 시트 (칸 256×384): 시트별 인물 키를 기준 키에 맞춘다 (K-034). referencePx = 그 개체의 기준 인물 키
        static void ApplyFigure(MaterialPropertyBlock block, Texture sheet, Texture normal = null, float referencePx = PerfLabArt.FigureReferencePx) =>
            Apply(block, sheet, PerfLabArt.FromSheet(sheet), normal, PerfLabArt.SheetScale(sheet, referencePx), true);

        // 동작 시트 하나 (K-032): 같은 버퍼를 frame0부터 16칸 구간만 그 시트로 그린다. 시트가 없으면 아무것도 안 함
        static void AddPose(List<(MaterialPropertyBlock block, int count)> draws, GraphicsBuffer buffer, int count, Texture2D sheet, int frame0,
            float referencePx = PerfLabArt.FigureReferencePx)
        {
            if (sheet == null || count <= 0) return;
            var block = new MaterialPropertyBlock();
            ApplyFigure(block, sheet, null, referencePx);
            block.SetVector(FrameWindowId, PerfLabArt.Window(frame0));
            block.SetBuffer(InstancesId, buffer);
            draws.Add((block, count));
        }

        // 주인공 명령 손짓 (K-032): 휠 메뉴를 열 때 SetHero의 command 칸으로 그린다. 시트가 없으면 예전 그대로
        public void SetHeroCommandArt(Texture2D sheet, Texture2D normal = null)
        {
            if (sheet == null) return;
            heroCommandBlock = new MaterialPropertyBlock();
            ApplyFigure(heroCommandBlock, sheet, normal);
            heroCommandBlock.SetBuffer(InstancesId, heroBuffer);
        }

        public bool HasHeroCommandArt => heroCommandBlock != null;

        // 3등신 시험 그림 켜기·끄기 (K-057): 궁수 서기·사격, 주인공 시트·서기 블록의 그림을 바꾼다. 버퍼·칸 구간은 그대로.
        // 시험 그림은 인물 키 192px라 SheetScale이 기존 그림과 같은 월드 키로 맞춘다. 원래 노멀맵은 칸이 안 맞아 시험 그림에는 뺀다
        void ApplyProportionTrial()
        {
            trialShown = ProportionTrial.Enabled;
            if (trialArcher != null && trialArcherBlock != null)
            {
                Texture2D sheet = ProportionTrial.ArcherStatic(trialArcher.Sheet);
                ApplyFigure(trialArcherBlock, sheet, ProportionTrial.Normal(sheet, trialArcher.Sheet, trialArcher.NormalSheet));
                Texture2D fire = ProportionTrial.ArcherFire(trialArcher.FireSheet);
                if (trialArcherFireBlock != null && fire != null) ApplyFigure(trialArcherFireBlock, fire);
                sim.SetFireSheetFrames(trialArcher, fire);   // 시험 사격은 4칸 (놓기 = 4번째 칸, 기존과 같은 자리)
            }

            if (heroSheetOriginal != null)
            {
                Texture2D hero = ProportionTrial.HeroGrid(heroSheetOriginal);
                Apply(heroBlock, hero, PerfLabArt.HeroLayout, ProportionTrial.Normal(hero, heroSheetOriginal, heroNormalOriginal), PerfLabArt.SheetScale(hero), true);
            }

            if (heroIdleBlock != null && heroIdleOriginal != null)
            {
                Texture2D idle = ProportionTrial.HeroIdle(heroIdleOriginal);
                ApplyFigure(heroIdleBlock, idle, ProportionTrial.Normal(idle, heroIdleOriginal, heroIdleNormalOriginal));
            }

            sim.SoldiersDirty = true;
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
        // 병사(1.4×0.25)보다 조금 크게. K-034부터 개체 크기 = 인물 실제 키 (시트 배율이 칸 안 여백을 뺀다)
        public const float HeroDrawSize = 1.5f * BattleSim.WallFigureScale;

        // command = 명령 손짓 칸 (0 이상이면 손짓 시트로, 뒤집지 않음 — K-032)
        public void SetHero(Vector3 feet, int frame, bool flip, bool visible, bool idle = false, int command = -1)
        {
            heroVisible = visible;
            heroCommandFrame = heroCommandBlock != null ? command : -1;
            heroIdle = idle && heroIdleBlock != null && heroCommandFrame < 0;
            if (heroIdle) frame = 0;
            if (heroCommandFrame >= 0)
            {
                frame = heroCommandFrame;
                flip = false;
            }

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
            if (ProportionTrial.Enabled != trialShown) ApplyProportionTrial();   // 3등신 시험 그림 (K-057) — 꺼져 있으면 건너뜀
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

            if (!mapMode)
            {
                // 공격·세우기·오르기·쓰러짐 (K-032) — 위에서 올린 같은 버퍼
                foreach (var (block, count) in enemyPoseDraws)
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
                foreach (var (buffer, block, start, count) in ladderDraws)
                {
                    buffer.SetData(sim.LadderInstances, start, 0, count);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
                }

                // 화상 불꽃 (K-035)
                if (flameBuffer != null && sim.BurningCount > 0)
                {
                    int flames = sim.WriteBurnFlames(flameInstances, Time.time, FlameFrames, FlameFps);
                    if (flames > 0)
                    {
                        flameBuffer.SetData(flameInstances, 0, 0, flames);
                        Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, flames, flameBlock);
                    }
                }
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

            if (sim.HasSoldierPoseArt) sim.SoldiersDirty = true;   // 사격·쓰러짐 칸은 매 프레임 바뀐다 (K-032)
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
                    foreach (var (block, count) in soldierPoseDraws)
                        Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
                    foreach (var (_, block, _, count) in runnerDraws)
                        Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, count, block);
                }
            }

            if (!mapMode)
            {
                props.Draw();
                shotBuffer.SetData(sim.ShotInstances);
                float shotShade = sim.Brightness;
                foreach (var (block, art, area) in shotDraws)
                {
                    // 그림은 흰색 × 밝기, 예전 막대는 화살·총알 연노랑 × 밝기, 대포알 어두운 회색
                    Color color = art ? new Color(shotShade, shotShade, shotShade, 1f)
                        : area ? new Color(0.15f, 0.15f, 0.15f, 1f) : new Color(0.95f * shotShade, 0.9f * shotShade, 0.75f * shotShade, 1f);
                    block.SetColor(ShotColorId, color);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, sim.ShotCapacity, block);
                }

                if (heroVisible)
                {
                    heroBuffer.SetData(heroInstance);
                    MaterialPropertyBlock heroDraw = heroCommandFrame >= 0 ? heroCommandBlock : heroIdle ? heroIdleBlock : heroBlock;
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, 1, heroDraw);
                }

                if (wizardBuffer != null)
                {
                    Vector3 feet = sim.WizardFeet;
                    float shade = sim.Brightness;
                    // 마법이 나간 직후 시전 4칸, 마지막 칸 유지 (K-032)
                    float castAge = sim.WizardCastAge;
                    bool casting = wizardCastBlock != null && castAge >= 0f && castAge < WizardCastSeconds;
                    float wizardFrame = casting ? Mathf.Min(Mathf.FloorToInt(castAge * WizardCastFps), wizardCastFrames - 1) : 0f;
                    wizardInstance[0] = new PerfLabInstance
                    {
                        PosFrame = new float4(feet.x, feet.y, feet.z, wizardFrame),
                        TintSize = new float4(shade, shade, shade, HeroDrawSize),   // 주인공과 같은 크기
                    };
                    wizardBuffer.SetData(wizardInstance);
                    Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, 1, casting ? wizardCastBlock : wizardBlock);
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
            // K-041 안개 날씨: 가까이부터 짙게 — 시야(25m) 언저리에서 거의 묻히고, 적 그림(PerfLabSprite)도 이제 안개를 받는다.
            // 땅에 깔리는 안개 판은 BattleFieldArt가 같은 안개색으로 그린다
            if (battle.CurrentWeather == HumanLastRampart.Core.CombatContracts.Weather.Fog)
            {
                RenderSettings.fogColor = Color.Lerp(sky, Color.white, night ? 0.05f : 0.15f);
                RenderSettings.fogStartDistance = 2f;
                RenderSettings.fogEndDistance = visible + 15f;
            }
        }

        // 마법 효과 (임시 모양): 불바다 = 주황 원판, 폭발 대기 = 어두운 노랑 원판, 터짐 = 밝은 공
        void DrawEffects()
        {
            IReadOnlyList<BattleSim.SpellEffect> list = sim.Effects;
            int discs = 0, flashes = 0;
            foreach (BattleSim.SpellEffect e in list)
            {
                if (!e.Fire && !e.Pending && effectArt != null && effectArt.HasBurstArt) continue;   // 터짐은 도트 그림으로 (K-015). 원판은 그대로
                if (e.Fire && effectArt != null && effectArt.HasFireArt) continue;   // 불바다는 불 그림만 — 바닥 빨간 원판 빼기 (K-036, 사용자 피드백 12-b). 지도에는 주황 원이 남는다
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
            if (effectArt.DrawsPiles) return;   // 시체 더미 그림(K-024)이 있으면 BattleFieldArt가 그림으로 그린다 (K-031)
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

        // 마법을 고른 동안 마우스가 가리키는 땅 위치 (K-036). DrawMapMarkers 앞에 부른다. 고른 마법이 없으면 spell = -1
        int previewSpell = -1;
        Vector3? spellPreview;

        public void SetSpellPreview(int spell, Vector3? point)
        {
            previewSpell = spell;
            spellPreview = point;
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

            // 마법 범위 미리 보기 (K-036, 사용자 피드백 8): 고른 마법의 반경 원이 마우스를 따라다닌다.
            // 폭발 노랑·불바다 주황(찍은 뒤 원과 같은 색), 성벽에서 150m 밖이거나 재사용 대기 중이면 회색 + 가운데 점 없음
            if (showSpellRange && spellPreview.HasValue && previewSpell >= 0 && previewSpell < sim.SpellCount)
            {
                BattleSim.SpellInfo spell = sim.GetSpell(previewSpell);
                Vector3 at = spellPreview.Value;
                float fromWalls = Mathf.Max(Mathf.Abs(at.x), Mathf.Abs(at.z)) - sim.CastleHalf;
                bool usable = fromWalls <= BattleSim.SpellCastRange && spell.Ready;
                Color c = !usable ? new Color(0.55f, 0.55f, 0.58f) : spell.IsFire ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.95f, 0.2f);
                int dots = Mathf.Clamp(Mathf.RoundToInt(spell.Radius * 2.5f), 24, 64);
                Ring(at, spell.Radius, c, dot * 1.1f, dots);
                Ring(at, spell.Radius * 0.96f, c, dot * 0.6f, dots);
                if (usable) Add(at, c, dot * 1.4f);
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

            // 지형 (K-049, D-077): 토벽 갈색·무너진 틈 옅은 갈색·나무 덩이 초록·바위 덩이 회색 (언덕은 3D 둔덕이 보인다)
            sim.ForEachTerrainMark((p, c) => Add(p, c, dot * 2.6f));

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
                    // 사거리 원은 칠 수 있는 들판 쪽(K-042: 자기 들판 + 모서리 2문만 옆 들판)만 — 맞은편·닿지 않는 옆 들판 쪽 호는 그리지 않는다
                    const int ringDots = 72;
                    for (int k = 0; k < ringDots; k++)
                    {
                        float a = k * Mathf.PI * 2f / ringDots;
                        Vector3 p = c.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * c.Range;
                        if (!CannonReaches(c, p)) continue;
                        Add(p, selected ? new Color(1f, 0.9f, 0.3f) : new Color(0.8f, 0.8f, 0.8f), dot * (selected ? 0.7f : 0.45f));
                    }

                    // 고른 대포: 쏠 수 있는 구역을 옅은 점 격자로 채운다 (성 안은 빼고)
                    if (selected)
                    {
                        const float step = 5f;
                        int steps = Mathf.CeilToInt(c.Range / step);
                        for (int gz = -steps; gz <= steps; gz++)
                        {
                            for (int gx = -steps; gx <= steps; gx++)
                            {
                                Vector3 p = c.Position + new Vector3(gx * step, 0f, gz * step);
                                if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) < sim.CastleHalf + 1f) continue;
                                if ((p - c.Position).sqrMagnitude > c.Range * c.Range || !CannonReaches(c, p)) continue;
                                Add(p, new Color(1f, 0.85f, 0.35f), dot * 0.35f);
                            }
                        }
                    }
                }

                Add(c.Position, selected ? new Color(1f, 0.9f, 0.2f) : Color.white, dot * (selected ? 2.4f : 1.7f));
                // 불붙이는 중 (K-046): 대포 점 위에 깜빡이는 주황 불씨 — 곧 발사
                if (c.Igniting && Mathf.Repeat(Time.unscaledTime, 0.3f) < 0.18f)
                    Add(c.Position, new Color(1f, 0.5f, 0.05f), dot * 1.1f);
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

        // 대포가 그 지점의 들판 면을 칠 수 있는가 (BattleSim.CannonCanReach와 같은 규칙, 사거리는 부르는 쪽이 본다)
        static bool CannonReaches(BattleSim.CannonInfo c, Vector3 p) =>
            (c.Reach & (1 << WallGeometry.FieldSide(new Unity.Mathematics.float3(p.x, 0f, p.z)))) != 0;

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
            foreach (var draw in ladderDraws) draw.buffer.Release();
            ladderDraws.Clear();
            flameBuffer?.Release();
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
