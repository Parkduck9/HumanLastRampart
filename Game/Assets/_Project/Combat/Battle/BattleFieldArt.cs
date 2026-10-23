using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Traps;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Battle
{
    // 들판 그림 (카드 K-031 — Codex K-024·K-025 시안 미리 연결, 사용자 화면 검토 전)
    // - 들판 소품: 넓은잎나무·침엽수·바위 2종·풀숲을 언덕 둘레와 들판에 흩뿌린다 (Resources/BattleArt/Props, 1m당 32화소 2.5D 판)
    //   적이 오는 깔때기(BattleSim D-065) 안은 비우고 작은 풀숲·바위만 드문드문 — 적이 나무에 가려지지 않게. 나무는 깔때기 사이 모서리 쪽과 들판 끝 바깥
    // - 시체 더미 3단계: 성벽 앞 칸마다 높이에 따라 corpse_pile_1~3 (BattleView의 상자 대신, 그림이 없으면 예전 상자)
    // - 함정 실물: 묻힌 표시(trap_1) — 묻힘·점화 중·불발, 터진 구덩이(trap_2) — 터짐. 실제 위치에 그린다
    // - 먼 산 띠(horizon_day/night)를 들판 바깥 둘레에, 하늘 색 띠(sky_day/night)를 그 뒤 큰 원통에 — 지도 모드에서는 숨긴다
    // 소품·더미·함정은 캐릭터와 같은 PerfLabSprite(카메라를 보는 판) 인스턴싱 — 소품은 처음 한 번만 버퍼에 넣는다
    // 그림이 없는 종류는 그리지 않는다
    public sealed class BattleFieldArt : IDisposable
    {
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int HasNormalId = Shader.PropertyToID("_HasNormal");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        static readonly int FixedRightId = Shader.PropertyToID("_FixedRight");

        const string Folder = "BattleArt/Props/";
        const float PixelsPerMeter = 32f;
        const int Seed = 31;                       // 소품 배치 씨앗 — 전투마다 같은 들판
        const float RefreshSeconds = 0.25f;        // 더미·함정 버퍼 갱신 간격 (BattleView 상자 더미와 같다)

        // 소품 배치 (K-025 manifest 규격안: 칸 크기·바닥 기준점)
        const int ForestCount = 260;               // 깔때기 밖 숲 (나무 위주)
        const int LaneBushCount = 60;              // 깔때기 안 작은 풀숲
        const int LaneRockCount = 20;
        const float ForestWallGap = 14f;           // 성벽에서 이만큼은 비운다 (시체 더미·가까운 함정 자리)
        const float LaneWallGap = 35f;             // 깔때기 안 소품은 이보다 먼 곳만
        const float ForestBeyond = 60f;            // 들판 끝(FieldSize) 바깥으로 이만큼까지 숲
        const float FunnelMargin = 6f;             // 깔때기 가장자리에서 나무를 이만큼 더 떨어뜨린다
        const float LaneScale = 0.6f;              // 깔때기 안 소품은 작게

        // 시체 더미 (K-024: 캔버스 64×64, 내용 폭 48화소, 지면 접점 (32,56))
        const float PileContentWidth = 48f / 64f;
        const float PilePivotY = (64f - 56f) / 64f;
        const float PileStage3Content = 40f / 64f;  // 3단계 내용 높이 / 캔버스
        const float PileStage2From = 0.6f, PileStage3From = 1.2f;   // 더미 높이(m) 기준 단계

        // 함정 (K-024: 캔버스 64×64, 내용 폭 56화소, 접점 (32,56))
        const float TrapCanvas = 2f;               // 32화소/m — 캔버스 2m, 내용 폭 1.75m
        const float TrapContentWidth = 56f / 64f;

        // 먼 산 띠·하늘 (K-025 backgrounds)
        const float HorizonBeyond = 120f;          // 들판 끝 바깥 거리 — 반경 = FieldSize + 이 값
        const int HorizonRepeats = 12;             // 둘레에 그림을 몇 번 (거울 반복 — 좌우 끝 이음새를 감춘다)
        const int RingSegments = 72;
        const float FogBandBeyond = 60f;           // K-111 안개 띠: 들판 끝 바깥 거리 (성에서 45m보다 훨씬 멀다)
        const int FogBandRepeats = 12;
        const float FogBandBaseV = 0.5f;           // 그림 어두운 띠 시작 높이(그림 높이 비율)
        const float HorizonSink = 3f;             // 아래 끝을 땅 밑으로 (떠 보이지 않게)
        const float SkyBeyond = 400f;
        const float SkyTop = 500f;

        enum PropKind { Oak, Pine, RockA, RockB, Bush }

        // 칸 크기(화소), 바닥 기준점(위에서 화소) — README 표
        static readonly (string file, int width, int height, int pivotFromTop)[] PropSpecs =
        {
            ("oak", 128, 160, 152),
            ("pine", 128, 224, 216),
            ("rock_a", 96, 80, 72),
            ("rock_b", 64, 112, 104),
            ("bush", 64, 48, 40),
        };

        readonly BattleSim sim;
        readonly Material material;
        readonly Mesh quad;
        readonly Bounds bounds;
        readonly GameObject root;

        readonly Kind[] props = new Kind[PropSpecs.Length];
        readonly Kind[] piles = new Kind[3];
        readonly Kind trapBuried, trapCrater;

        // 지형 손질·무작위 지형 판 (K-049, Codex K-030 전투 판 — 32화소/m, 바닥 기준 (폭/2, 높이−8))
        const int TreesPerCell = 3;
        const string TerrainFolder = "BattleArt/Terrain/";
        const float EarthWallBoardHeight = 1.5f;   // 320×96 판(10m 폭 그림)을 칸 폭 5m에 맞춘 높이
        const float RubbleSize = 1.2f;             // 허물어진 토벽 = 낮은 흙더미 (hill_small_side 작게)
        const float RockPileSize = 2.5f;           // 바위 덩이 (rocks_side)
        readonly Kind earthWallNS, earthWallEW, rubble, rockPiles;   // 토벽은 성벽과 나란히 선 판 (남북 면 / 동서 면)
        // 토벽 부서짐 3단계 (Codex K-068: 칸 256×128, 바닥 가운데, 판 5.6×2.8m) — [단계 0 멀쩡·1 금 감·2 무너짐][0 남북 면·1 동서 면]
        // 허물어진 칸은 잔해(earthwall_rubble). 새 그림이 없으면 예전 earthwall_side·hill_small_side 그대로
        const float K068BoardHeight = 2.8f;
        const float CrackedBelow = 0.67f, CollapsedBelow = 0.34f;   // 남은 체력 비율
        static readonly string[] WallStageFiles = { "earthwall_intact", "earthwall_cracked", "earthwall_collapsed" };
        // 방향: 0 = 동서로 긴 판, 1 = 남북으로 긴 판, 2·3 = 자유 배치 비스듬한 판 (45도·135도, 사용자 2026-10-20)
        readonly Kind[,] wallStages = new Kind[3, 4];
        readonly Kind[] newRubble = new Kind[4];
        static readonly Vector3[] WallOrientRight = { Vector3.right, Vector3.forward, new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f) };
        bool fallenTreeArt;
        int terrainVersion = -1;
        // 쓰러진 나무 (K-051): 나무 덩이 칸마다 그린 나무 판 (소품 종류, 판 번호, 칸) — 쓰러지면 숨기고 통나무 더미(logs_side)를 그린다
        readonly List<(int kind, int index, int cell)> terrainTrees = new List<(int, int, int)>();
        const float FallenLogsSize = 1.6f;
        readonly Kind fallenLogs;
        readonly Vector2[] pileJitter;             // 칸마다 조금 어긋나게 (성벽 따라, 바깥쪽)
        float nextRefresh;
        float lastShade = -1f;

        Material horizonMaterial, skyMaterial, fogBandMaterial;
        Mesh horizonMesh, skyMesh, fogBandMesh;
        GameObject horizonObject, skyObject, fogBandObject;
        bool backdropVisible = true;
        Weather backdropWeather;
        bool backdropTinted;

        sealed class Kind
        {
            public GraphicsBuffer Buffer;
            public MaterialPropertyBlock Block;
            public PerfLabInstance[] Instances;
            public int Count;
            public bool CastShadows;
        }

        public bool HasPileArt => piles[0] != null && piles[1] != null && piles[2] != null;
        public int PropCount { get { int n = 0; foreach (Kind k in props) if (k != null) n += k.Count; return n; } }

        public BattleFieldArt(BattleSim sim, Material material, Mesh quad, Transform parent)
        {
            this.sim = sim;
            this.material = material;
            this.quad = quad;
            float extent = sim.FieldSize + ForestBeyond + 20f;
            bounds = new Bounds(Vector3.zero, new Vector3(extent * 2f, 200f, extent * 2f));
            root = new GameObject("Battle Field Art (K-031)");
            root.transform.SetParent(parent, false);

            BuildProps();

            int bins = 4 * sim.Pile.Bins;
            for (int s = 0; s < 3; s++)
                piles[s] = MakeKind(Folder + "corpse_pile_" + (s + 1), bins, PerfLabArt.SheetLayout.Make(1, 1, 0.5f, PilePivotY, 1f), true);
            pileJitter = new Vector2[bins];
            var random = new System.Random(Seed + 1);
            for (int k = 0; k < bins; k++) pileJitter[k] = new Vector2(Range(random, -0.3f, 0.3f), Range(random, -0.6f, 0.6f));

            int traps = Mathf.Max(1, sim.TrapCount);
            var trapLayout = PerfLabArt.SheetLayout.Make(1, 1, 0.5f, PilePivotY, 1f);
            trapBuried = MakeKind(Folder + "trap_1", traps, trapLayout, false);
            trapCrater = MakeKind(Folder + "trap_2", traps, trapLayout, false);
            if (trapCrater != null)
            {
                trapCrater.Block.SetFloat("_GroundPlane", 1f);
                // 그림 중심을 지면의 함정 위치에 둔다. 카메라를 돌려도 판이 일어서지 않는다.
                trapCrater.Block.SetVector(PivotId, new Vector4(0.5f, 0.5f, 0f, 0f));
            }

            // 토벽·바위 덩이 판 (K-049). 토벽 판은 성벽과 나란히 고정 (남북 면 = 동서로 긴 판, 동서 면 = 남북으로 긴 판)
            TerrainLayout shape = sim.TerrainLayout;
            if (shape != null)
            {
                int walls = shape.EarthWallCells.Count;
                int orients = shape.FreeWalls.Count > 0 ? 4 : 2;
                for (int o = 0; o < orients; o++)
                {
                    for (int s = 0; s < 3; s++) wallStages[s, o] = MakeBoardKind(WallStageFiles[s], walls, WallOrientRight[o]);
                    newRubble[o] = MakeBoardKind("earthwall_rubble", walls, WallOrientRight[o]);
                }
                if (wallStages[0, 0] == null)
                {
                    earthWallNS = MakeTerrainKind("earthwall_side", 320, 96, walls, Vector3.right);
                    earthWallEW = MakeTerrainKind("earthwall_side", 320, 96, walls, Vector3.forward);
                }

                if (newRubble[0] == null) rubble = MakeTerrainKind("hill_small_side", 160, 96, walls, null);
                rockPiles = MakeTerrainKind("rocks_side", 160, 112, shape.RockCells.Count, null);
                fallenLogs = MakeBoardKind("fallen_tree", shape.TreeCells.Count, null);   // 쓰러진 나무 잔해 (K-051, 그림 K-068)
                fallenTreeArt = fallenLogs != null;
                if (fallenLogs == null) fallenLogs = MakeTerrainKind("logs_side", 160, 80, shape.TreeCells.Count, null);
            }

            BuildDressing();
            BuildBackdrop();
            BuildGroundFog();
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        Kind MakeKind(string path, int capacity, PerfLabArt.SheetLayout layout, bool castShadows)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            if (texture.filterMode != FilterMode.Point) texture.filterMode = FilterMode.Point;   // 가져오기 설정이 빠져도 도트 그대로
            var kind = new Kind { Block = new MaterialPropertyBlock(), Instances = new PerfLabInstance[Mathf.Max(1, capacity)], CastShadows = castShadows };
            kind.Block.SetTexture(MainTexId, texture);
            kind.Block.SetVector(GridId, layout.Grid);
            kind.Block.SetVector(PivotId, layout.Pivot);
            kind.Block.SetFloat(AspectId, layout.Aspect);
            kind.Block.SetFloat(HasNormalId, 0f);
            kind.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, kind.Instances.Length, PerfLabInstance.Stride);
            kind.Block.SetBuffer(InstancesId, kind.Buffer);
            return kind;
        }

        // ── 들판 소품 ──

        void BuildProps()
        {
            var lists = new List<(Vector3 feet, float size, bool flip)>[PropSpecs.Length];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<(Vector3, float, bool)>();
            var random = new System.Random(Seed);
            float outer = sim.FieldSize + ForestBeyond;

            void Add(PropKind kind, Vector2 p, float scale)
            {
                var spec = PropSpecs[(int)kind];
                float size = spec.height / PixelsPerMeter * scale * Range(random, 0.8f, 1.2f);
                lists[(int)kind].Add((new Vector3(p.x, 0f, p.y), size, random.NextDouble() < 0.5));
            }

            // 1. 깔때기 밖 숲: 나무 위주, 가끔 풀숲·바위
            for (int attempt = 0, placed = 0; placed < ForestCount && attempt < ForestCount * 20; attempt++)
            {
                var p = new Vector2(Range(random, -outer, outer), Range(random, -outer, outer));
                if (WallDistance(p) < ForestWallGap || InFunnel(p, FunnelMargin)) continue;
                if (p.magnitude > sim.FieldSize + HorizonBeyond - 15f) continue;   // 네모 모서리가 먼 산 띠 밖으로 나가지 않게
                double roll = random.NextDouble();
                PropKind kind = roll < 0.45 ? PropKind.Pine : roll < 0.8 ? PropKind.Oak : roll < 0.92 ? PropKind.Bush : roll < 0.96 ? PropKind.RockA : PropKind.RockB;
                Add(kind, p, 1f);
                placed++;
            }

            // 2. 언덕 둘레: 풀숲·바위를 둘러 놓고, 깔때기 밖이면 나무도 한두 그루
            foreach (Vector3 hill in sim.Hills)
            {
                var center = new Vector2(hill.x, hill.z);
                float radius = hill.y;
                int count = Mathf.Clamp(Mathf.RoundToInt(radius * 0.8f), 4, 14);
                for (int k = 0; k < count; k++)
                {
                    float a = (k + Range(random, -0.3f, 0.3f)) * Mathf.PI * 2f / count;
                    Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * Range(random, 0.85f, 1.15f);
                    if (WallDistance(p) < ForestWallGap) continue;
                    double roll = random.NextDouble();
                    bool lane = InFunnel(p, 0f);
                    if (roll < 0.15 && !lane) Add(random.NextDouble() < 0.5 ? PropKind.Oak : PropKind.Pine, p, 1f);
                    else if (roll < 0.6) Add(PropKind.Bush, p, lane ? LaneScale : 1f);
                    else Add(random.NextDouble() < 0.6 ? PropKind.RockA : PropKind.RockB, p, lane ? LaneScale : 0.8f);
                }
            }

            // 3. 깔때기 안: 성벽에서 먼 곳에 작은 풀숲·바위만 드문드문
            int lanes = 0;
            for (int attempt = 0; lanes < LaneBushCount + LaneRockCount && attempt < (LaneBushCount + LaneRockCount) * 20; attempt++)
            {
                var p = new Vector2(Range(random, -sim.FieldSize, sim.FieldSize), Range(random, -sim.FieldSize, sim.FieldSize));
                if (WallDistance(p) < LaneWallGap || !InFunnel(p, 0f)) continue;
                Add(lanes < LaneBushCount ? PropKind.Bush : PropKind.RockA, p, LaneScale);
                lanes++;
            }

            // 4. 무작위 지형의 나무 덩이 (K-049): 막힌 칸마다 나무 세 그루 — 적이 실제로 돌아가는 곳
            TerrainLayout shape = sim.TerrainLayout;
            if (shape != null)
            {
                foreach (int c in shape.TreeCells)
                {
                    Vector2 center = shape.Center(c);
                    for (int k = 0; k < TreesPerCell; k++)
                    {
                        var p = center + new Vector2(Range(random, -0.35f, 0.35f), Range(random, -0.35f, 0.35f)) * shape.CellSize;
                        PropKind tree = random.NextDouble() < 0.5 ? PropKind.Pine : PropKind.Oak;
                        terrainTrees.Add(((int)tree, lists[(int)tree].Count, c));
                        Add(tree, p, 0.9f);
                    }
                }
            }

            for (int i = 0; i < PropSpecs.Length; i++)
            {
                var spec = PropSpecs[i];
                if (lists[i].Count == 0) continue;
                var layout = PerfLabArt.SheetLayout.Make(1, 1, 0.5f, (spec.height - spec.pivotFromTop) / (float)spec.height, spec.width / (float)spec.height);
                Kind kind = MakeKind(Folder + spec.file, lists[i].Count, layout, true);
                if (kind == null) continue;
                for (int j = 0; j < lists[i].Count; j++)
                {
                    var (feet, size, flip) = lists[i][j];
                    kind.Instances[j] = new PerfLabInstance { PosFrame = new float4(feet.x, feet.y, feet.z, flip ? -1f : 0f), TintSize = new float4(1f, 1f, 1f, size) };
                }

                kind.Count = lists[i].Count;
                props[i] = kind;
            }
        }

        // 성벽 바깥면에서 거리 (네모 성 — 가장 가까운 면)
        float WallDistance(Vector2 p) => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - sim.CastleHalf;

        // 적이 오는 깔때기 안인가 (BattleSim 생성 규칙과 같은 식: 성벽 앞 반폭 → 들판 끝 FunnelFieldHalfWidth)
        bool InFunnel(Vector2 p, float margin)
        {
            float funnelBase = sim.CastleHalf * 0.95f;
            float slope = Mathf.Max(0f, (BattleSim.FunnelFieldHalfWidth - funnelBase) / Mathf.Max(1f, sim.FieldSize - sim.CastleHalf));
            for (int side = 0; side < 4; side++)
            {
                float3 n = WallGeometry.Outward(side);
                float3 r = WallGeometry.Right(side);
                float depth = p.x * n.x + p.y * n.z;
                if (depth < sim.CastleHalf - 1f) continue;
                float lateral = Mathf.Abs(p.x * r.x + p.y * r.z);
                if (lateral < funnelBase + (depth - sim.CastleHalf) * slope + margin) return true;
            }

            return false;
        }

        // ── 들판 꾸미기 (Codex K-068) ──
        // 세운 소품: 덤불·키 큰 풀은 깔때기 밖·가장자리, 꽂힌 무기·방패는 성벽 앞 싸움터(깔때기 안도 — 작아서 적을 안 가린다), 표지판 몇 개.
        // 바닥 무늬: 풀·꽃·자갈은 들판 전체, 흙 자리는 드문드문, 그을음·구덩이는 성벽 앞 싸움터에. 바닥에 납작하게 깐 반투명 판
        // (판 크기 = Codex manifest의 billboard_size_m / size_m)
        static readonly (string file, float width, float height)[] UprightSpecs =
        {
            ("k068_bush_01", 1.354f, 0.914f), ("k068_bush_02", 1.723f, 1.435f), ("k068_bush_03", 1.108f, 0.582f),
            ("k068_tallgrass_01", 1.149f, 1.029f), ("k068_tallgrass_02", 1.108f, 1.380f),
            ("k068_weapon_01", 0.96f, 1.029f), ("k068_weapon_02", 0.8f, 0.629f), ("k068_weapon_03", 1.124f, 0.857f),
            ("k068_shield", 0.862f, 0.739f), ("k068_signpost", 0.985f, 1.389f),
        };
        static readonly (string file, float width, float depth, int count, int zone)[] DecalSpecs =
        {
            // zone 0 들판 전체, 1 성벽 앞 싸움터(성벽 바깥 4~45m)
            ("ground_grass_01", 0.65f, 0.45f, 220, 0), ("ground_grass_02", 0.85f, 0.5f, 220, 0), ("ground_grass_03", 0.5f, 0.4f, 200, 0),
            ("ground_flowers_01", 0.7f, 0.5f, 90, 0), ("ground_flowers_02", 0.8f, 0.55f, 90, 0),
            ("ground_pebbles_01", 0.9f, 0.6f, 80, 0), ("ground_pebbles_02", 1.2f, 0.8f, 60, 0),
            ("ground_dirt_01", 2.4f, 1.7f, 45, 0), ("ground_dirt_02", 1.8f, 1.3f, 45, 0), ("ground_dirt_03", 3f, 2f, 35, 0),
            ("ground_scorch_01", 2.8f, 2.1f, 14, 1), ("ground_scorch_02", 4f, 2.6f, 10, 1),
            ("ground_crater_01", 2.2f, 1.8f, 10, 1), ("ground_crater_02", 3.6f, 2.8f, 6, 1),
        };
        const float DecalOpacity = 0.9f;
        const float SmallDecalScale = 2.5f;
        const float DecalLift = 0.03f;
        const float BattleBandNear = 4f, BattleBandFar = 45f;
        readonly Kind[] extraProps = new Kind[UprightSpecs.Length];
        readonly List<(GameObject go, Material material)> decals = new List<(GameObject, Material)>();
        bool decalsVisible = true;

        void BuildDressing()
        {
            var random = new System.Random(Seed + 68);
            float field = sim.FieldSize;
            Vector2 RandomField() => new Vector2(Range(random, -field, field), Range(random, -field, field));
            Vector2 RandomBattleBand()
            {
                int side = random.Next(4);
                float3 n = WallGeometry.Outward(side), r = WallGeometry.Right(side);
                float outward = sim.CastleHalf + Range(random, BattleBandNear, BattleBandFar);
                float along = Range(random, -sim.CastleHalf * 1.4f, sim.CastleHalf * 1.4f);
                return new Vector2(n.x * outward + r.x * along, n.z * outward + r.z * along);
            }

            Vector2 RandomNearWalls()
            {
                int side = random.Next(4);
                float3 n = WallGeometry.Outward(side), r = WallGeometry.Right(side);
                float outward = sim.CastleHalf + Range(random, BattleBandNear, 60f);
                float along = Range(random, -sim.CastleHalf * 2f, sim.CastleHalf * 2f);
                return new Vector2(n.x * outward + r.x * along, n.z * outward + r.z * along);
            }

            // 세운 소품
            var lists = new List<(Vector2 p, bool flip)>[UprightSpecs.Length];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<(Vector2, bool)>();
            void Put(int kind, Vector2 p) => lists[kind].Add((p, random.NextDouble() < 0.5));
            for (int attempt = 0, placed = 0; placed < 220 && attempt < 5000; attempt++)
            {
                Vector2 p = RandomField();
                if (WallDistance(p) < ForestWallGap || InFunnel(p, -4f)) continue;   // 깔때기 가운데는 비우고 가장자리까지
                Put(random.Next(5), p);   // 덤불 3·키 큰 풀 2
                placed++;
            }

            for (int k = 0; k < 90; k++)
            {
                Vector2 p = RandomBattleBand();
                if (WallDistance(p) < BattleBandNear) continue;
                Put(5 + random.Next(4), p);   // 꽂힌 무기 3·방패
            }

            for (int k = 0; k < 8; k++)
            {
                Vector2 p = RandomField();
                if (WallDistance(p) < 25f || InFunnel(p, 2f)) continue;
                Put(9, p);
            }

            for (int i = 0; i < UprightSpecs.Length; i++)
            {
                if (lists[i].Count == 0) continue;
                var spec = UprightSpecs[i];
                Kind kind = MakeKind(Folder + spec.file, lists[i].Count, PerfLabArt.SheetLayout.Make(1, 1, 0.5f, 0f, spec.width / spec.height), true);
                if (kind == null) continue;
                for (int j = 0; j < lists[i].Count; j++)
                {
                    var (p, flip) = lists[i][j];
                    kind.Instances[j] = new PerfLabInstance { PosFrame = new float4(p.x, 0f, p.y, flip ? -1f : 0f), TintSize = new float4(1f, 1f, 1f, spec.height * Range(random, 0.85f, 1.15f)) };
                }

                kind.Count = lists[i].Count;
                kind.Buffer.SetData(kind.Instances, 0, 0, kind.Count);
                extraProps[i] = kind;
            }

            // 바닥 무늬: 종류마다 납작한 판 묶음 하나 (메시 한 개)
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return;
            foreach (var spec in DecalSpecs)
            {
                var texture = Resources.Load<Texture2D>(Folder + spec.file);
                if (texture == null) continue;
                texture.filterMode = FilterMode.Point;
                var vertices = new List<Vector3>(spec.count * 4);
                var uvs = new List<Vector2>(spec.count * 4);
                var triangles = new List<int>(spec.count * 6);
                for (int k = 0; k < spec.count; k++)
                {
                    // 들판 전체 무늬도 70%는 성벽 가까이(사진기가 보는 곳)에 모은다
                    Vector2 p = spec.zone == 1 || random.NextDouble() < 0.7 ? RandomNearWalls() : RandomField();
                    if (WallDistance(p) < 2f) continue;
                    // 작은 무늬(풀·꽃·자갈)는 전투 사진기 거리에서 안 보여서 키운다 (발표 사진 확인 2026-10-20)
                    float scale = Range(random, 0.8f, 1.25f) * (spec.width < 1.5f ? SmallDecalScale : 1f);
                    float hw = spec.width * 0.5f * scale, hd = spec.depth * 0.5f * scale;
                    bool flip = random.NextDouble() < 0.5;
                    float y = DecalLift + k * 0.0004f;   // 겹칠 때 깜빡이지 않게 아주 조금씩 높이
                    int b = vertices.Count;
                    vertices.Add(new Vector3(p.x - hw, y, p.y - hd));
                    vertices.Add(new Vector3(p.x + hw, y, p.y - hd));
                    vertices.Add(new Vector3(p.x - hw, y, p.y + hd));
                    vertices.Add(new Vector3(p.x + hw, y, p.y + hd));
                    uvs.Add(new Vector2(flip ? 1f : 0f, 0f));
                    uvs.Add(new Vector2(flip ? 0f : 1f, 0f));
                    uvs.Add(new Vector2(flip ? 1f : 0f, 1f));
                    uvs.Add(new Vector2(flip ? 0f : 1f, 1f));
                    triangles.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }

                if (vertices.Count == 0) continue;
                var mesh = new Mesh { name = "Decals " + spec.file, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                var m = new Material(unlit) { name = "Battle Decal (runtime)" };
                m.SetTexture(BaseMapId, texture);
                m.mainTexture = texture;
                m.SetColor(BaseColorId, new Color(1f, 1f, 1f, DecalOpacity));
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent - 50;   // 땅 위 다른 반투명(안개)보다 먼저
                GameObject go = Place("Decals", mesh, m);
                decals.Add((go, m));
            }
        }

        void TintDecals(float shade)
        {
            foreach (var (_, m) in decals) m.SetColor(BaseColorId, new Color(shade, shade, shade, DecalOpacity));
        }

        void SetDecalsVisible(bool visible)
        {
            if (visible == decalsVisible) return;
            decalsVisible = visible;
            foreach (var (go, _) in decals) go.SetActive(visible);
        }

        // ── 매 프레임 ──

        // 작업이 끝난 뒤(sim.Complete 후). 지도 모드면 판 그림을 그리지 않고 먼 산·하늘을 숨긴다
        public void Draw(bool mapMode)
        {
            SetBackdropVisible(!mapMode);
            SetDecalsVisible(!mapMode);
            UpdateGroundFog(!mapMode && sim.CurrentWeather == Weather.Fog);
            if (mapMode) return;
            TintBackdrop();

            float shade = sim.Brightness;
            bool shadeChanged = !Mathf.Approximately(shade, lastShade);
            lastShade = shade;

            foreach (Kind kind in props)
            {
                if (kind == null) continue;
                if (shadeChanged)
                {
                    for (int i = 0; i < kind.Count; i++) kind.Instances[i].TintSize = new float4(shade, shade, shade, kind.Instances[i].TintSize.w);
                    kind.Buffer.SetData(kind.Instances, 0, 0, kind.Count);
                }

                Submit(kind);
            }

            if (shadeChanged)
                foreach (Kind kind in extraProps)
                {
                    if (kind == null) continue;
                    for (int i = 0; i < kind.Count; i++) kind.Instances[i].TintSize = new float4(shade, shade, shade, kind.Instances[i].TintSize.w);
                    kind.Buffer.SetData(kind.Instances, 0, 0, kind.Count);
                }

            foreach (Kind kind in extraProps) if (kind != null) Submit(kind);
            if (shadeChanged) TintDecals(shade);

            bool refresh = shadeChanged || Time.unscaledTime >= nextRefresh;
            if (refresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                RefreshPiles(shade);
                RefreshTraps(shade);
            }

            if (HasPileArt) foreach (Kind kind in piles) Submit(kind);
            if (trapBuried != null) Submit(trapBuried);
            if (trapCrater != null) Submit(trapCrater);

            // 토벽은 맞을수록 단계가 바뀌므로 주기마다 다시 맞춘다 (K-068)
            if (refresh || terrainVersion != sim.TerrainVersion) RefreshTerrainBoards(shade);
            foreach (Kind kind in TerrainWallKinds()) Submit(kind);
            if (rockPiles != null) Submit(rockPiles);
            if (fallenLogs != null) Submit(fallenLogs);
        }

        // 쓰러진 나무 칸: 그 칸 나무 판을 숨기고(크기 0) 통나무 더미를 놓는다
        void RefreshFallenTrees(TerrainLayout shape, float shade)
        {
            var touched = new HashSet<int>();
            foreach (var (kind, index, cell) in terrainTrees)
            {
                Kind k = props[kind];
                if (k == null || index >= k.Count) continue;
                bool fallen = sim.TerrainKindAt(cell) == TerrainCell.FallenTrees;
                float4 tint = k.Instances[index].TintSize;
                if (fallen == (tint.w <= 0f)) continue;
                if (fallen) k.Instances[index].TintSize = new float4(tint.x, tint.y, tint.z, 0f);
                touched.Add(kind);
            }

            foreach (int kind in touched) props[kind].Buffer.SetData(props[kind].Instances, 0, 0, props[kind].Count);

            if (fallenLogs == null) return;
            fallenLogs.Count = 0;
            foreach (int c in shape.TreeCells)
            {
                if (sim.TerrainKindAt(c) != TerrainCell.FallenTrees || fallenLogs.Count >= fallenLogs.Instances.Length) continue;
                Vector2 p = shape.Center(c);
                fallenLogs.Instances[fallenLogs.Count++] = new PerfLabInstance { PosFrame = new float4(p.x, 0f, p.y, (c & 1) == 0 ? 0f : -1f), TintSize = new float4(shade, shade, shade, fallenTreeArt ? K068BoardHeight : FallenLogsSize) };
            }

            if (fallenLogs.Count > 0) fallenLogs.Buffer.SetData(fallenLogs.Instances, 0, 0, fallenLogs.Count);
        }

        // 토벽(온전 / 무너진 틈)·바위 덩이 판을 지금 지형에 맞게 (토벽이 무너지면 sim.TerrainVersion이 바뀐다)
        void RefreshTerrainBoards(float shade)
        {
            terrainVersion = sim.TerrainVersion;
            TerrainLayout shape = sim.TerrainLayout;
            if (shape == null) return;
            foreach (Kind k in TerrainWallKinds()) k.Count = 0;
            freeWallCells.Clear();
            foreach (var piece in shape.FreeWalls) foreach (int c in piece.Cells) freeWallCells.Add(c);
            if (wallStages[0, 0] != null) RefreshFreeWalls(shape, shade);
            foreach (int c in shape.EarthWallCells)
            {
                if (wallStages[0, 0] != null && freeWallCells.Contains(c)) continue;   // 자유 조각은 조각마다 한 장으로 그렸다
                Vector2 p = shape.Center(c);
                bool broken = sim.TerrainKindAt(c) == TerrainCell.Breach;
                int side = WallGeometry.FieldSide(new float3(p.x, 0f, p.y));
                int orient = side == 0 || side == 2 ? 0 : 1;
                Kind kind;
                float size;
                if (wallStages[0, 0] != null || newRubble[0] != null)
                {
                    float health = sim.EarthWallHealth(c);
                    int stage = health < CollapsedBelow ? 2 : health < CrackedBelow ? 1 : 0;
                    kind = broken ? newRubble[orient] ?? rubble : wallStages[stage, orient] ?? (orient == 0 ? earthWallNS : earthWallEW);
                    size = kind == rubble ? RubbleSize : kind == earthWallNS || kind == earthWallEW ? EarthWallBoardHeight : K068BoardHeight;
                }
                else
                {
                    kind = broken ? rubble : orient == 0 ? earthWallNS : earthWallEW;
                    size = broken ? RubbleSize : EarthWallBoardHeight;
                }

                if (kind == null || kind.Count >= kind.Instances.Length) continue;
                kind.Instances[kind.Count++] = new PerfLabInstance
                {
                    PosFrame = new float4(p.x, 0f, p.y, 0f),
                    TintSize = new float4(shade, shade, shade, size),
                };
            }

            foreach (Kind k in TerrainWallKinds())
                if (k.Count > 0) k.Buffer.SetData(k.Instances, 0, 0, k.Count);

            if (rockPiles != null)
            {
                rockPiles.Count = 0;
                foreach (int c in shape.RockCells)
                {
                    Vector2 p = shape.Center(c);
                    if (rockPiles.Count < rockPiles.Instances.Length)
                        rockPiles.Instances[rockPiles.Count++] = new PerfLabInstance { PosFrame = new float4(p.x, 0f, p.y, (c & 1) == 0 ? 0f : -1f), TintSize = new float4(shade, shade, shade, RockPileSize) };
                }
            }

            if (rockPiles != null && rockPiles.Count > 0) rockPiles.Buffer.SetData(rockPiles.Instances, 0, 0, rockPiles.Count);
            RefreshFallenTrees(shape, shade);
        }

        readonly HashSet<int> freeWallCells = new HashSet<int>();

        // 칸 두 개 토벽 (사용자 2026-10-20 #12): 칸마다 조각 방향(0·45·90·135도)으로 돌린 판 한 장. 칸마다 금 간 정도·무너짐을 따로.
        // 대각선은 칸 대각선(7.07m)을 덮게 판을 조금 크게 — 이음 조각 그림(K-087)이 오면 바꾼다
        const float DiagonalBoardScale = 1.26f;

        void RefreshFreeWalls(TerrainLayout shape, float shade)
        {
            foreach (var piece in shape.FreeWalls)
            {
                int orient = Mathf.RoundToInt(Mathf.Repeat(piece.Rotation, 180f) / 45f) % 4;
                bool diagonal = orient == 1 || orient == 3;
                orient = orient == 0 ? 0 : orient == 2 ? 1 : orient == 1 ? 2 : 3;
                foreach (int c in piece.Cells)
                {
                    bool broken = sim.TerrainKindAt(c) == TerrainCell.Breach;
                    float health = sim.EarthWallHealth(c);
                    int stage = health < CollapsedBelow ? 2 : health < CrackedBelow ? 1 : 0;
                    Kind kind = broken ? newRubble[orient] ?? newRubble[0] : wallStages[stage, orient] ?? wallStages[stage, 0];
                    if (kind == null || kind.Count >= kind.Instances.Length) continue;
                    Vector2 p = shape.Center(c);
                    kind.Instances[kind.Count++] = new PerfLabInstance
                    {
                        PosFrame = new float4(p.x, 0f, p.y, 0f),
                        TintSize = new float4(shade, shade, shade, K068BoardHeight * (diagonal ? DiagonalBoardScale : 1f)),
                    };
                }
            }
        }

        // 토벽·잔해 판 종류 (있는 것만)
        IEnumerable<Kind> TerrainWallKinds()
        {
            if (earthWallNS != null) yield return earthWallNS;
            if (earthWallEW != null) yield return earthWallEW;
            if (rubble != null) yield return rubble;
            foreach (Kind k in wallStages) if (k != null) yield return k;
            foreach (Kind k in newRubble) if (k != null) yield return k;
        }

        // K-068 판 (칸 256×128, 바닥 가운데 기준) — 그림이 없으면 null
        Kind MakeBoardKind(string file, int capacity, Vector3? fixedRight)
        {
            if (capacity <= 0) return null;
            Kind kind = MakeKind(TerrainFolder + file, capacity, PerfLabArt.SheetLayout.Make(1, 1, 0.5f, 0f, 2f), true);
            if (kind != null && fixedRight.HasValue) kind.Block.SetVector(FixedRightId, new Vector4(fixedRight.Value.x, 0f, fixedRight.Value.z, 1f));
            return kind;
        }

        Kind MakeTerrainKind(string file, int width, int height, int capacity, Vector3? fixedRight)
        {
            if (capacity <= 0) return null;
            Kind kind = MakeKind(TerrainFolder + file, capacity, PerfLabArt.SheetLayout.Make(1, 1, 0.5f, 8f / height, width / (float)height), true);
            if (kind != null && fixedRight.HasValue) kind.Block.SetVector(FixedRightId, new Vector4(fixedRight.Value.x, 0f, fixedRight.Value.z, 1f));
            return kind;
        }

        void Submit(Kind kind)
        {
            if (kind.Count <= 0) return;
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, bounds, kind.Count, kind.Block,
                kind.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, true);
        }

        // 시체 더미: 칸마다 높이로 단계를 고른다. 판 크기는 칸 폭을 덮게, 3단계는 높이가 내용보다 높아지면 함께 키운다
        void RefreshPiles(float shade)
        {
            if (!HasPileArt) return;
            foreach (Kind kind in piles) kind.Count = 0;
            PileSpec spec = sim.Pile;
            NativeArray<float> heights = sim.PileHeights;
            float canvas = spec.BinWidth / PileContentWidth;
            int count = Mathf.Min(heights.Length, pileJitter.Length);
            for (int k = 0; k < count; k++)
            {
                float h = heights[k];
                if (h < 0.05f) continue;
                int stage = h < PileStage2From ? 0 : h < PileStage3From ? 1 : 2;
                float size = stage == 2 ? Mathf.Max(canvas, h / PileStage3Content) : canvas;

                int side = k / spec.Bins;
                int bin = k % spec.Bins;
                float3 n = WallGeometry.Outward(side);
                float3 r = WallGeometry.Right(side);
                float along = -sim.CastleHalf + (bin + 0.5f) * spec.BinWidth + pileJitter[k].x;
                float outward = sim.CastleHalf + 2f + pileJitter[k].y;   // 예전 상자 가운데 (성벽에서 2m)
                Kind kind = piles[stage];
                kind.Instances[kind.Count++] = new PerfLabInstance
                {
                    PosFrame = new float4(n.x * outward + r.x * along, 0f, n.z * outward + r.z * along, (k & 1) == 0 ? 0f : -1f),
                    TintSize = new float4(shade, shade, shade, size),
                };
            }

            foreach (Kind kind in piles)
                if (kind.Count > 0) kind.Buffer.SetData(kind.Instances, 0, 0, kind.Count);
        }

        // 함정: 터지면 구덩이(구덩이 반경을 덮게), 나머지는 묻힌 표시
        void RefreshTraps(float shade)
        {
            if (trapBuried == null && trapCrater == null) return;
            if (trapBuried != null) trapBuried.Count = 0;
            if (trapCrater != null) trapCrater.Count = 0;
            for (int i = 0; i < sim.TrapCount; i++)
            {
                BattleSim.TrapInfo trap = sim.GetTrap(i);
                bool exploded = trap.State == BattleSim.TrapState.Exploded;
                Kind kind = exploded ? trapCrater : trapBuried;
                if (kind == null || kind.Count >= kind.Instances.Length) continue;
                float size = exploded ? Mathf.Max(TrapCanvas, trap.CraterRadius * 2f / TrapContentWidth) : TrapCanvas;
                kind.Instances[kind.Count++] = new PerfLabInstance
                {
                    PosFrame = new float4(trap.Position.x, exploded ? 0.025f : 0f, trap.Position.z, 0f),
                    TintSize = new float4(shade, shade, shade, size),
                };
            }

            if (trapBuried != null && trapBuried.Count > 0) trapBuried.Buffer.SetData(trapBuried.Instances, 0, 0, trapBuried.Count);
            if (trapCrater != null && trapCrater.Count > 0) trapCrater.Buffer.SetData(trapCrater.Instances, 0, 0, trapCrater.Count);
        }

        // ── 먼 산·하늘 ──

        void BuildBackdrop()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return;
            bool night = sim.TimeOfDay == TimeOfDay.Night;

            var sky = Resources.Load<Texture2D>("BattleArt/Sky/" + (night ? "sky_night" : "sky_day"));
            if (sky != null)
            {
                sky.wrapMode = TextureWrapMode.Clamp;
                float radius = sim.FieldSize + SkyBeyond;
                skyMesh = Ring("Sky Ring", radius, -20f, SkyTop, 1f, v => Mathf.Clamp01(v / SkyTop));
                skyMaterial = NewUnlit(unlit, sky, "Battle Sky (runtime)", false);
                skyObject = Place("Sky", skyMesh, skyMaterial);
            }

            var horizon = Resources.Load<Texture2D>("BattleArt/Sky/" + (night ? "horizon_night" : "horizon_day"));
            if (horizon != null)
            {
                horizon.wrapModeU = TextureWrapMode.Mirror;
                horizon.wrapModeV = TextureWrapMode.Clamp;
                float radius = sim.FieldSize + HorizonBeyond;
                float width = 2f * Mathf.PI * radius / HorizonRepeats;
                float height = width * horizon.height / horizon.width;
                horizonMesh = Ring("Horizon Ring", radius, -HorizonSink, height - HorizonSink, HorizonRepeats, v => (v + HorizonSink) / height);
                horizonMaterial = NewUnlit(unlit, horizon, "Battle Horizon (runtime)", true);
                horizonObject = Place("Horizon", horizonMesh, horizonMaterial);
            }

            // K-111 먼 전장 안개 띠: 밤에만, 들판 끝 바깥(45m보다 훨씬 멀리)에 얇은 어두운 띠. 장면 안개(RenderSettings.fog)는 그대로 둔다
            var band = night ? Resources.Load<Texture2D>("BattleArt/Props/fog_band") : null;
            if (band != null)
            {
                float radius = sim.FieldSize + FogBandBeyond;
                float width = 2f * Mathf.PI * radius / FogBandRepeats;
                float height = width * band.height / band.width;
                float bottom = -height * FogBandBaseV;   // 그림의 어두운 띠(그림 높이 0.50~0.72)가 땅 높이부터 오르게
                fogBandMesh = Ring("Fog Band Ring", radius, bottom, bottom + height, FogBandRepeats, y => (y - bottom) / height);
                fogBandMaterial = NewUnlit(unlit, band, "Battle Far Fog Band (runtime)", false);
                fogBandMaterial.SetFloat("_Surface", 1f);   // 반투명: 알파 섞기, 깊이 안 씀
                fogBandMaterial.SetFloat("_Blend", 0f);
                fogBandMaterial.SetOverrideTag("RenderType", "Transparent");
                fogBandMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                fogBandMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (fogBandMaterial.HasProperty("_SrcBlendAlpha")) fogBandMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (fogBandMaterial.HasProperty("_DstBlendAlpha")) fogBandMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                fogBandMaterial.SetFloat("_ZWrite", 0f);
                fogBandMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                fogBandMaterial.renderQueue = (int)RenderQueue.Transparent - 100;   // 가장 먼 물체: 땅 그림 판(-50)·땅 안개 판보다 먼저 그린다
                fogBandObject = Place("Far Fog Band", fogBandMesh, fogBandMaterial);
            }
        }

        // 안쪽을 보는 원통 (u = 둘레 방향 × repeats, v = 높이 함수)
        static Mesh Ring(string name, float radius, float bottom, float top, float repeats, Func<float, float> v)
        {
            var vertices = new Vector3[(RingSegments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[RingSegments * 6];
            for (int i = 0; i <= RingSegments; i++)
            {
                float t = (float)i / RingSegments;
                float a = t * Mathf.PI * 2f;
                var flat = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                vertices[i * 2] = flat + Vector3.up * bottom;
                vertices[i * 2 + 1] = flat + Vector3.up * top;
                uvs[i * 2] = new Vector2(t * repeats, v(bottom));
                uvs[i * 2 + 1] = new Vector2(t * repeats, v(top));
            }

            for (int i = 0; i < RingSegments; i++)
            {
                int b = i * 2;
                triangles[i * 6] = b;
                triangles[i * 6 + 1] = b + 1;
                triangles[i * 6 + 2] = b + 2;
                triangles[i * 6 + 3] = b + 2;
                triangles[i * 6 + 4] = b + 1;
                triangles[i * 6 + 5] = b + 3;
            }

            var mesh = new Mesh { name = name, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 빛을 받지 않는 그림(낮·밤 그림이 따로 있다). 안개는 받는다 — 안개·비 날에는 먼 산·하늘이 안개색으로 묻힌다
        static Material NewUnlit(Shader shader, Texture2D texture, string name, bool alphaClip)
        {
            // 투명 자르기는 견본 재질(ClipUnlit, 키워드가 재질 파일에 켜져 있어 빌드에서 빠지지 않는다)을 복사한다 (W27 검토)
            var m = (alphaClip ? ClipMaterials.UnlitCopy(name) : null) ?? new Material(shader) { name = name };
            m.SetTexture(BaseMapId, texture);
            m.mainTexture = texture;
            m.SetColor(BaseColorId, Color.white);
            if (m.HasProperty(CullId)) m.SetFloat(CullId, (float)CullMode.Off);   // 원통 안쪽에서 본다
            if (alphaClip)
            {
                // 먼 산 띠의 투명한 하늘 부분을 잘라낸다 (그림에 반투명 테두리가 있으면 0.5 기준으로 끊긴다)
                if (m.HasProperty(AlphaClipId)) m.SetFloat(AlphaClipId, 1f);
                if (m.HasProperty(CutoffId)) m.SetFloat(CutoffId, 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }

            return m;
        }

        GameObject Place(string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = m;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // ── 땅에 깔리는 안개 (K-041 사용자 '안개도 좀 애매') ──
        // 안개 날씨면 들판 위 낮은 높이에 반투명 안개 판 3장을 겹쳐 깔고 서로 다른 방향으로 천천히 흘린다.
        // 판 색은 장면 안개색(RenderSettings.fogColor, BattleView.ApplyAtmosphere)을 따른다. 무늬는 실행 중 만든 반복 구름 무늬
        static readonly (float height, float tile, Vector2 drift, float alpha)[] FogLayers =
        {
            (0.35f, 28f, new Vector2(0.5f, 0.15f), 0.55f),
            (1.0f, 42f, new Vector2(-0.3f, 0.35f), 0.45f),
            (1.9f, 60f, new Vector2(0.2f, -0.25f), 0.35f),
        };

        Texture2D fogNoise;
        readonly List<(GameObject go, Material material)> fogSheets = new List<(GameObject, Material)>();
        bool fogVisible;

        void BuildGroundFog()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return;
            fogNoise = MakeFogNoise(128, Seed + 7);
            float size = (sim.FieldSize + 40f) * 2f;
            foreach (var layer in FogLayers)
            {
                var m = new Material(unlit) { name = "Battle Ground Fog (runtime)" };
                m.SetTexture(BaseMapId, fogNoise);
                m.mainTexture = fogNoise;
                m.SetTextureScale(BaseMapId, Vector2.one * (size / layer.tile));
                // 반투명 (URP Unlit: 겉면 종류 Transparent, 알파 섞기, 깊이 안 씀)
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                if (m.HasProperty(CullId)) m.SetFloat(CullId, (float)CullMode.Off);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Ground Fog";
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, layer.height, 0f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(size, size, 1f);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = m;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                fogSheets.Add((go, m));
            }
        }

        void UpdateGroundFog(bool on)
        {
            if (on != fogVisible)
            {
                fogVisible = on;
                foreach (var (go, _) in fogSheets) go.SetActive(on);
            }

            if (!on) return;
            Color fogColor = RenderSettings.fogColor;
            float t = Time.time;
            for (int i = 0; i < fogSheets.Count; i++)
            {
                var layer = FogLayers[i];
                Material m = fogSheets[i].material;
                m.SetColor(BaseColorId, new Color(fogColor.r, fogColor.g, fogColor.b, layer.alpha));
                m.SetTextureOffset(BaseMapId, layer.drift * (t / layer.tile));
            }
        }

        // 가장자리가 이어지는 구름 무늬 (값 잡음 두 겹). 흰색, 알파만 무늬
        static Texture2D MakeFogNoise(int size, int seed)
        {
            var random = new System.Random(seed);
            float[,] coarse = Lattice(random, 6), fine = Lattice(random, 14);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float n = Sample(coarse, u, v) * 0.7f + Sample(fine, u, v) * 0.3f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, n));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Ground Fog Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static float[,] Lattice(System.Random random, int cells)
        {
            var values = new float[cells, cells];
            for (int y = 0; y < cells; y++)
                for (int x = 0; x < cells; x++)
                    values[x, y] = (float)random.NextDouble();
            return values;
        }

        // 격자 값을 부드럽게 잇는다 (끝은 처음과 이어져 반복된다)
        static float Sample(float[,] values, float u, float v)
        {
            int cells = values.GetLength(0);
            float fx = u * cells, fy = v * cells;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
            x0 %= cells;
            y0 %= cells;
            float a = Mathf.Lerp(values[x0, y0], values[x1, y0], tx);
            float b = Mathf.Lerp(values[x0, y1], values[x1, y1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        void SetBackdropVisible(bool visible)
        {
            if (visible == backdropVisible) return;
            backdropVisible = visible;
            if (horizonObject != null) horizonObject.SetActive(visible);
            if (skyObject != null) skyObject.SetActive(visible);
            if (fogBandObject != null) fogBandObject.SetActive(visible);
        }

        // 날씨가 바뀌면 하늘·먼 산을 조금 어둡게 (BattleView.ApplyAtmosphere 하늘색과 같은 방향)
        void TintBackdrop()
        {
            if (backdropTinted && backdropWeather == sim.CurrentWeather) return;
            backdropTinted = true;
            backdropWeather = sim.CurrentWeather;
            Color tint;
            switch (backdropWeather)
            {
                case Weather.Cloudy: tint = new Color(0.82f, 0.84f, 0.86f); break;
                case Weather.Rain: tint = new Color(0.66f, 0.69f, 0.73f); break;
                case Weather.Fog: tint = new Color(0.9f, 0.9f, 0.9f); break;
                default: tint = Color.white; break;
            }

            if (skyMaterial != null) skyMaterial.SetColor(BaseColorId, tint);
            if (horizonMaterial != null) horizonMaterial.SetColor(BaseColorId, tint);
            if (fogBandMaterial != null) fogBandMaterial.SetColor(BaseColorId, tint);   // 먼 산과 같이 어둡게 (비 오는 밤에 띠만 밝아 보이지 않게)
        }

        public void Dispose()
        {
            foreach (Kind kind in props) kind?.Buffer.Release();
            foreach (Kind kind in piles) kind?.Buffer.Release();
            trapBuried?.Buffer.Release();
            trapCrater?.Buffer.Release();
            earthWallNS?.Buffer.Release();
            earthWallEW?.Buffer.Release();
            rubble?.Buffer.Release();
            rockPiles?.Buffer.Release();
            fallenLogs?.Buffer.Release();
            foreach (Kind kind in wallStages) kind?.Buffer.Release();
            foreach (Kind kind in newRubble) kind?.Buffer.Release();
            foreach (Kind kind in extraProps) kind?.Buffer.Release();
            foreach (var (go, material) in decals)
            {
                if (go != null && go.TryGetComponent(out MeshFilter filter)) UnityEngine.Object.Destroy(filter.sharedMesh);
                if (material != null) UnityEngine.Object.Destroy(material);
            }
            decals.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (horizonMaterial != null) UnityEngine.Object.Destroy(horizonMaterial);
            if (skyMaterial != null) UnityEngine.Object.Destroy(skyMaterial);
            if (horizonMesh != null) UnityEngine.Object.Destroy(horizonMesh);
            if (skyMesh != null) UnityEngine.Object.Destroy(skyMesh);
            if (fogBandMaterial != null) UnityEngine.Object.Destroy(fogBandMaterial);
            if (fogBandMesh != null) UnityEngine.Object.Destroy(fogBandMesh);
            foreach (var (_, material) in fogSheets) if (material != null) UnityEngine.Object.Destroy(material);
            fogSheets.Clear();
            if (fogNoise != null) UnityEngine.Object.Destroy(fogNoise);
        }
    }
}
