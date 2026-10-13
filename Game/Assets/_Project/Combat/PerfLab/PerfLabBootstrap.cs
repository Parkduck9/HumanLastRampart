using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.PerfLab
{
    // 대량 전투 성능 시제품 순서 3 (Docs/GDD/PERF_PROTOTYPE.md, 구조: Docs/collab/view/PERFLAB_ARCH.html)
    // ① 적 행군 → ② 공간 격자 → ③④ 표적·발사 → ④ 화살 풀 → ⑤ 화살 이동·명중 → ⑥ 사건 처리 → ⑦ 그리기 데이터
    // 조작: 1/2/3 단계 전환(S1~S3), C 카메라 원근/직교, R 해상도(내부 절반+2배 / 네이티브), B 자동 측정, F8 성능 표시
    public sealed class PerfLabBootstrap : MonoBehaviour
    {
        struct Stage
        {
            public string Name;
            public int Allies;
            public int Enemies;
        }

        static readonly Stage[] Stages =
        {
            new Stage { Name = "S1", Allies = 1000, Enemies = 5000 },
            new Stage { Name = "S2", Allies = 3000, Enemies = 10000 },
            new Stage { Name = "S3", Allies = 10000, Enemies = 50000 },
        };

        [Header("그림")]
        [SerializeField] Shader spriteShader;
        [SerializeField] Texture2D enemySheet;          // 고블린 4열×2행. 비어 있으면 임시 그림
        [SerializeField] float enemySize = 2.4f;        // 칸 높이(월드 단위)
        [SerializeField] float allySize = 1.4f;
        [SerializeField] float arrowSize = 0.9f;
        [SerializeField] float walkFps = 6f;
        [SerializeField] float attackFps = 7f;

        [Header("전장")]
        [SerializeField] int startStage;
        [SerializeField] float wallLength = 400f;
        [SerializeField] float wallHeight = 4f;
        [SerializeField] float spawnDepthMin = 80f;
        [SerializeField] float spawnDepthMax = 320f;
        [SerializeField] float benchmarkSpawnDepthMin = 10f;   // 자동 측정 때는 가까이서 출발해 바로 교전
        [SerializeField] float enemySpeedMin = 3f;
        [SerializeField] float enemySpeedMax = 6f;
        [SerializeField] float cellSize = 4f;
        [SerializeField] uint randomSeed = 12345;

        [Header("전투 (시험값)")]
        [SerializeField] int enemyMaxHp = 30;
        [SerializeField] int arrowDamage = 10;
        [SerializeField] float allyRange = 50f;
        [SerializeField] float fireInterval = 1f;
        [SerializeField] float retryInterval = 0.25f;
        [SerializeField] float arrowSpeed = 45f;
        [SerializeField] int arrowCapacity = 32768;

        static readonly float4 EnemyTint = new float4(1f, 1f, 1f, 1f);
        static readonly float4 PlaceholderEnemyTint = new float4(0.85f, 0.25f, 0.2f, 1f);
        static readonly float4 AllyTint = new float4(0.3f, 0.5f, 0.95f, 1f);
        static readonly float4 ArrowTint = new float4(0.95f, 0.9f, 0.75f, 1f);

        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int PivotId = Shader.PropertyToID("_Pivot");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        // 적
        NativeArray<float3> enemyPositions;
        NativeArray<float> enemySpeeds;
        NativeArray<float> enemyStopZ;
        NativeArray<float> enemyAnimPhases;
        NativeArray<byte> enemyAtWall;
        NativeArray<int> enemyHp;
        NativeArray<int> enemyGeneration;
        NativeArray<PerfLabInstance> enemyInstances;

        // 아군
        NativeArray<float3> allyPositions;
        NativeArray<float> allyCooldowns;
        NativeArray<PerfLabInstance> allyInstances;

        // 격자
        GridSpec grid;
        NativeArray<int> cellOfEnemy;
        NativeArray<int> cellStart;
        NativeArray<int> cellCount;
        NativeArray<int> cellCursor;
        NativeArray<int> sortedEnemies;

        // 화살 풀
        NativeArray<byte> arrowActive;
        NativeArray<byte> arrowEnded;
        NativeArray<float3> arrowPositions;
        NativeArray<float3> arrowVelocities;
        NativeArray<float> arrowLife;
        NativeArray<EnemyHandle> arrowTargets;
        NativeArray<int> freeSlots;
        NativeArray<int> freeCount;
        NativeArray<PerfLabInstance> arrowInstances;

        // 사건·통계
        NativeQueue<ArrowSpawn> spawnQueue;
        NativeQueue<HitEvent> hitQueue;
        NativeArray<Unity.Mathematics.Random> randomState;
        NativeArray<int> stats;

        GraphicsBuffer enemyBuffer;
        GraphicsBuffer allyBuffer;
        GraphicsBuffer arrowBuffer;
        MaterialPropertyBlock enemyProperties;
        MaterialPropertyBlock allyProperties;
        MaterialPropertyBlock arrowProperties;
        Material material;
        Mesh quad;
        Texture2D placeholderSheet;
        Texture2D arrowTexture;
        Bounds drawBounds;
        float4 enemyTintSize;

        JobHandle frameJob;
        bool jobScheduled;
        int stageIndex;
        int enemyCount;
        int allyCount;

        Camera viewCamera;
        bool orthographic;
        PerfLabOverlay overlay;
        PerfLabResolution resolution;
        PerfLabBenchmark benchmark;
        int requestedStage = -1;
        bool requestedClose;

        public int AllyCount => allyCount;
        public int EnemyCount => enemyCount;
        public string StageLabel => Stages[stageIndex].Name;
        public int ActiveArrows { get; private set; }
        public int DeathsTotal { get; private set; }
        public int PoolOverflowTotal { get; private set; }

        // 단계 변경은 다음 Update 맨 앞(작업 예약 전)에 적용한다 — 실행 중인 Job의 배열을 지우지 않기 위해
        public void RequestStage(int index, bool closeSpawn)
        {
            requestedStage = Mathf.Clamp(index, 0, Stages.Length - 1);
            requestedClose = closeSpawn;
        }

        void Awake()
        {
            if (spriteShader == null)
            {
                spriteShader = Shader.Find("HLR/PerfLabSprite");
            }

            if (spriteShader == null)
            {
                Debug.LogError("[PerfLab] HLR/PerfLabSprite 셰이더를 찾지 못했습니다.");
                enabled = false;
                return;
            }

            quad = PerfLabArt.CreateQuad();
            placeholderSheet = PerfLabArt.CreatePlaceholderSheet();
            arrowTexture = PerfLabArt.CreateArrowTexture();
            material = new Material(spriteShader) { name = "PerfLabSprite (runtime)" };

            enemyProperties = new MaterialPropertyBlock();
            allyProperties = new MaterialPropertyBlock();
            arrowProperties = new MaterialPropertyBlock();
            if (enemySheet != null)
            {
                ApplyLayout(enemyProperties, enemySheet, PerfLabArt.GoblinLayout);
                enemyTintSize = new float4(EnemyTint.xyz, enemySize);
            }
            else
            {
                Debug.LogWarning("[PerfLab] 적 시트(enemySheet)가 비어 있어 임시 그림을 씁니다.");
                ApplyLayout(enemyProperties, placeholderSheet, PerfLabArt.PlaceholderLayout);
                enemyTintSize = new float4(PlaceholderEnemyTint.xyz, 1.6f);
            }

            ApplyLayout(allyProperties, placeholderSheet, PerfLabArt.PlaceholderLayout);
            ApplyLayout(arrowProperties, arrowTexture, PerfLabArt.ArrowLayout);
            drawBounds = new Bounds(new Vector3(0f, 0f, spawnDepthMax * 0.5f), new Vector3(wallLength * 2f, 100f, spawnDepthMax * 2f + 100f));

            float halfLength = wallLength * 0.5f;
            grid = new GridSpec
            {
                MinX = -halfLength - 10f,
                MinZ = -20f,
                CellSize = cellSize,
                Columns = (int)math.ceil((wallLength + 20f) / cellSize),
                Rows = (int)math.ceil((spawnDepthMax + 30f) / cellSize),
            };

            BuildEnvironment();
            overlay = gameObject.AddComponent<PerfLabOverlay>();
            resolution = gameObject.AddComponent<PerfLabResolution>();
            resolution.Init(viewCamera);
            benchmark = gameObject.AddComponent<PerfLabBenchmark>();
            benchmark.Init(this, resolution);

            stageIndex = Mathf.Clamp(startStage, 0, Stages.Length - 1);
            Spawn(Stages[stageIndex], false);
        }

        void Update()
        {
            HandleInput();

            float dt = Time.deltaTime;
            float halfLength = wallLength * 0.5f;

            JobHandle handle = new EnemyMoveJob
            {
                Positions = enemyPositions,
                Speeds = enemySpeeds,
                StopZ = enemyStopZ,
                AtWall = enemyAtWall,
                DeltaTime = dt,
            }.Schedule(enemyCount, 256);

            handle = new AssignCellsJob
            {
                Positions = enemyPositions,
                CellOfEnemy = cellOfEnemy,
                Grid = grid,
            }.Schedule(enemyCount, 512, handle);

            handle = new BuildGridJob
            {
                CellOfEnemy = cellOfEnemy,
                CellStart = cellStart,
                CellCount = cellCount,
                Cursor = cellCursor,
                SortedEnemies = sortedEnemies,
            }.Schedule(handle);

            handle = new TargetAndFireJob
            {
                AllyPositions = allyPositions,
                AllyCooldowns = allyCooldowns,
                EnemyPositions = enemyPositions,
                EnemySpeeds = enemySpeeds,
                EnemyAtWall = enemyAtWall,
                EnemyHp = enemyHp,
                EnemyGeneration = enemyGeneration,
                CellStart = cellStart,
                CellCount = cellCount,
                SortedEnemies = sortedEnemies,
                Spawns = spawnQueue.AsParallelWriter(),
                Grid = grid,
                DeltaTime = dt,
                Range = allyRange,
                FireInterval = fireInterval,
                RetryInterval = retryInterval,
                ArrowSpeed = arrowSpeed,
                EnemyMaxHp = enemyMaxHp,
            }.Schedule(allyCount, 64, handle);

            handle = new AllocateArrowsJob
            {
                Spawns = spawnQueue,
                FreeSlots = freeSlots,
                FreeCount = freeCount,
                ArrowActive = arrowActive,
                ArrowPositions = arrowPositions,
                ArrowVelocities = arrowVelocities,
                ArrowLife = arrowLife,
                ArrowTargets = arrowTargets,
                Stats = stats,
                ArrowSpeed = arrowSpeed,
            }.Schedule(handle);

            handle = new MoveArrowsJob
            {
                ArrowActive = arrowActive,
                ArrowPositions = arrowPositions,
                ArrowVelocities = arrowVelocities,
                ArrowLife = arrowLife,
                ArrowTargets = arrowTargets,
                ArrowEnded = arrowEnded,
                EnemyPositions = enemyPositions,
                EnemyGeneration = enemyGeneration,
                CellStart = cellStart,
                CellCount = cellCount,
                SortedEnemies = sortedEnemies,
                Hits = hitQueue.AsParallelWriter(),
                Grid = grid,
                DeltaTime = dt,
                TargetHitRadius = 1.2f,
                SplashHitRadius = 0.9f,
                Damage = arrowDamage,
            }.Schedule(arrowCapacity, 256, handle);

            handle = new ApplyHitsJob
            {
                Hits = hitQueue,
                EnemyHp = enemyHp,
                EnemyGeneration = enemyGeneration,
                EnemyPositions = enemyPositions,
                EnemyAtWall = enemyAtWall,
                RandomState = randomState,
                ArrowEnded = arrowEnded,
                FreeSlots = freeSlots,
                FreeCount = freeCount,
                Stats = stats,
                EnemyMaxHp = enemyMaxHp,
                RespawnMinX = -halfLength,
                RespawnMaxX = halfLength,
                RespawnMinZ = spawnDepthMax - 40f,
                RespawnMaxZ = spawnDepthMax,
            }.Schedule(handle);

            JobHandle enemyDraw = new WriteEnemyInstancesJob
            {
                Positions = enemyPositions,
                AnimPhases = enemyAnimPhases,
                AtWall = enemyAtWall,
                Instances = enemyInstances,
                Time = Time.time,
                WalkFps = walkFps,
                AttackFps = attackFps,
                FramesPerAction = PerfLabArt.FramesPerAction,
                TintSize = enemyTintSize,
            }.Schedule(enemyCount, 256, handle);

            JobHandle arrowDraw = new WriteArrowInstancesJob
            {
                ArrowActive = arrowActive,
                ArrowPositions = arrowPositions,
                Instances = arrowInstances,
                TintSize = new float4(ArrowTint.xyz, arrowSize),
            }.Schedule(arrowCapacity, 512, handle);

            frameJob = JobHandle.CombineDependencies(enemyDraw, arrowDraw);
            jobScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        void LateUpdate()
        {
            if (!jobScheduled)
            {
                return;
            }

            frameJob.Complete();
            jobScheduled = false;

            enemyBuffer.SetData(enemyInstances);
            arrowBuffer.SetData(arrowInstances);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, drawBounds, enemyCount, enemyProperties);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, drawBounds, allyCount, allyProperties);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, drawBounds, arrowCapacity, arrowProperties);

            ActiveArrows = stats[PerfLabStat.ActiveArrows];
            DeathsTotal = stats[PerfLabStat.DeathsTotal];
            PoolOverflowTotal = stats[PerfLabStat.PoolOverflowTotal];
            if (overlay != null)
            {
                overlay.ActiveArrows = ActiveArrows;
                overlay.DeathsTotal = DeathsTotal;
                overlay.PoolOverflowTotal = PoolOverflowTotal;
                overlay.ResolutionName = resolution != null ? resolution.Describe() : "";
                overlay.BenchmarkText = benchmark != null && benchmark.Running ? "자동 측정 중… (끝날 때까지 조작하지 마세요)" : (benchmark != null ? benchmark.LastSummary : "");
            }
        }

        void OnDestroy()
        {
            frameJob.Complete();
            jobScheduled = false;
            ReleaseData();

            if (material != null) Destroy(material);
            if (placeholderSheet != null) Destroy(placeholderSheet);
            if (arrowTexture != null) Destroy(arrowTexture);
            if (quad != null) Destroy(quad);
        }

        static void ApplyLayout(MaterialPropertyBlock block, Texture texture, PerfLabArt.SheetLayout layout)
        {
            block.SetTexture(MainTexId, texture);
            block.SetVector(GridId, layout.Grid);
            block.SetVector(PivotId, layout.Pivot);
            block.SetFloat(AspectId, layout.Aspect);
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            bool benchmarking = benchmark != null && benchmark.Running;
            if (keyboard != null && !benchmarking)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) RequestStage(0, false);
                if (keyboard.digit2Key.wasPressedThisFrame) RequestStage(1, false);
                if (keyboard.digit3Key.wasPressedThisFrame) RequestStage(2, false);

                if (keyboard.cKey.wasPressedThisFrame)
                {
                    orthographic = !orthographic;
                    ApplyCamera();
                }

                if (keyboard.rKey.wasPressedThisFrame && resolution != null)
                {
                    resolution.SetHalfScale(!resolution.HalfScale);
                }

                if (keyboard.bKey.wasPressedThisFrame && benchmark != null)
                {
                    orthographic = false;
                    ApplyCamera();
                    benchmark.Begin();
                }
            }

            if (requestedStage >= 0)
            {
                stageIndex = requestedStage;
                Spawn(Stages[stageIndex], requestedClose);
                requestedStage = -1;
            }
        }

        void Spawn(Stage stage, bool closeSpawn)
        {
            ReleaseData();

            enemyCount = stage.Enemies;
            allyCount = stage.Allies;
            var p = Allocator.Persistent;

            enemyPositions = new NativeArray<float3>(enemyCount, p);
            enemySpeeds = new NativeArray<float>(enemyCount, p);
            enemyStopZ = new NativeArray<float>(enemyCount, p);
            enemyAnimPhases = new NativeArray<float>(enemyCount, p);
            enemyAtWall = new NativeArray<byte>(enemyCount, p);
            enemyHp = new NativeArray<int>(enemyCount, p);
            enemyGeneration = new NativeArray<int>(enemyCount, p);
            enemyInstances = new NativeArray<PerfLabInstance>(enemyCount, p);

            allyPositions = new NativeArray<float3>(allyCount, p);
            allyCooldowns = new NativeArray<float>(allyCount, p);
            allyInstances = new NativeArray<PerfLabInstance>(allyCount, p);

            int cells = grid.Columns * grid.Rows;
            cellOfEnemy = new NativeArray<int>(enemyCount, p);
            cellStart = new NativeArray<int>(cells, p);
            cellCount = new NativeArray<int>(cells, p);
            cellCursor = new NativeArray<int>(cells, p);
            sortedEnemies = new NativeArray<int>(enemyCount, p);

            arrowActive = new NativeArray<byte>(arrowCapacity, p);
            arrowEnded = new NativeArray<byte>(arrowCapacity, p);
            arrowPositions = new NativeArray<float3>(arrowCapacity, p);
            arrowVelocities = new NativeArray<float3>(arrowCapacity, p);
            arrowLife = new NativeArray<float>(arrowCapacity, p);
            arrowTargets = new NativeArray<EnemyHandle>(arrowCapacity, p);
            freeSlots = new NativeArray<int>(arrowCapacity, p);
            freeCount = new NativeArray<int>(1, p);
            arrowInstances = new NativeArray<PerfLabInstance>(arrowCapacity, p);
            for (int i = 0; i < arrowCapacity; i++)
            {
                freeSlots[i] = arrowCapacity - 1 - i;
            }

            freeCount[0] = arrowCapacity;

            spawnQueue = new NativeQueue<ArrowSpawn>(p);
            hitQueue = new NativeQueue<HitEvent>(p);
            stats = new NativeArray<int>(PerfLabStat.Count, p);
            randomState = new NativeArray<Unity.Mathematics.Random>(1, p);

            var random = new Unity.Mathematics.Random(randomSeed == 0 ? 1u : randomSeed);
            float halfLength = wallLength * 0.5f;
            float depthMin = closeSpawn ? benchmarkSpawnDepthMin : spawnDepthMin;
            for (int i = 0; i < enemyCount; i++)
            {
                enemyPositions[i] = new float3(
                    random.NextFloat(-halfLength, halfLength),
                    0f,
                    random.NextFloat(depthMin, spawnDepthMax));
                enemySpeeds[i] = random.NextFloat(enemySpeedMin, enemySpeedMax);
                float phase = random.NextFloat();
                enemyAnimPhases[i] = phase;
                enemyStopZ[i] = 1.5f + phase * 6f;   // 성벽 앞에 여러 줄로 선다
                enemyHp[i] = enemyMaxHp;
            }

            // 아군: 성벽 위에 가로로 줄지어 세우고, 넘치면 뒤 줄로. 재장전은 흩어서 시작
            int columns = math.max(1, (int)(wallLength / 0.8f));
            float spacing = wallLength / columns;
            for (int i = 0; i < allyCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                var position = new float3(-halfLength + (column + 0.5f) * spacing, wallHeight, -0.5f - row * 0.8f);
                allyPositions[i] = position;
                allyCooldowns[i] = random.NextFloat(0f, fireInterval);
                allyInstances[i] = new PerfLabInstance
                {
                    PosFrame = new float4(position, 0f),
                    TintSize = new float4(AllyTint.xyz, allySize),
                };
            }

            randomState[0] = random;

            enemyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(1, enemyCount), PerfLabInstance.Stride);
            allyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(1, allyCount), PerfLabInstance.Stride);
            arrowBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(1, arrowCapacity), PerfLabInstance.Stride);
            allyBuffer.SetData(allyInstances);
            enemyProperties.SetBuffer(InstancesId, enemyBuffer);
            allyProperties.SetBuffer(InstancesId, allyBuffer);
            arrowProperties.SetBuffer(InstancesId, arrowBuffer);

            if (overlay != null)
            {
                overlay.StageName = $"{stage.Name} (1/2/3 단계, C 카메라)";
                overlay.EnemyCount = enemyCount;
                overlay.AllyCount = allyCount;
            }
        }

        void ReleaseData()
        {
            Dispose(ref enemyPositions);
            Dispose(ref enemySpeeds);
            Dispose(ref enemyStopZ);
            Dispose(ref enemyAnimPhases);
            Dispose(ref enemyAtWall);
            Dispose(ref enemyHp);
            Dispose(ref enemyGeneration);
            Dispose(ref enemyInstances);
            Dispose(ref allyPositions);
            Dispose(ref allyCooldowns);
            Dispose(ref allyInstances);
            Dispose(ref cellOfEnemy);
            Dispose(ref cellStart);
            Dispose(ref cellCount);
            Dispose(ref cellCursor);
            Dispose(ref sortedEnemies);
            Dispose(ref arrowActive);
            Dispose(ref arrowEnded);
            Dispose(ref arrowPositions);
            Dispose(ref arrowVelocities);
            Dispose(ref arrowLife);
            Dispose(ref arrowTargets);
            Dispose(ref freeSlots);
            Dispose(ref freeCount);
            Dispose(ref arrowInstances);
            Dispose(ref stats);
            Dispose(ref randomState);
            if (spawnQueue.IsCreated) spawnQueue.Dispose();
            if (hitQueue.IsCreated) hitQueue.Dispose();

            enemyBuffer?.Release();
            enemyBuffer = null;
            allyBuffer?.Release();
            allyBuffer = null;
            arrowBuffer?.Release();
            arrowBuffer = null;
        }

        static void Dispose<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        // 시험용 성벽·바닥·빛·카메라 (실제 전투 화면 구성이 아니다)
        void BuildEnvironment()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(transform, false);
            wall.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, -1f);
            wall.transform.localScale = new Vector3(wallLength, wallHeight, 2f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(0f, 0f, spawnDepthMax * 0.5f);
            ground.transform.localScale = new Vector3(wallLength / 10f * 1.2f, 1f, (spawnDepthMax + 60f) / 10f);

            var lightObject = new GameObject("Sun", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightObject.GetComponent<Light>().type = LightType.Directional;

            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                var cameraObject = new GameObject("PerfLab Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                viewCamera = cameraObject.GetComponent<Camera>();
            }

            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(0.45f, 0.6f, 0.75f);
            viewCamera.farClipPlane = 800f;
            ApplyCamera();
        }

        // 원근: 성벽 뒤 낮은 카메라 / 직교: 기울여 내려다보는 카메라 (카메라 목업 P1-C07·08과 같은 두 방식)
        void ApplyCamera()
        {
            if (viewCamera == null)
            {
                return;
            }

            viewCamera.orthographic = orthographic;
            if (orthographic)
            {
                viewCamera.transform.SetPositionAndRotation(new Vector3(0f, 90f, -90f), Quaternion.Euler(35f, 0f, 0f));
                viewCamera.orthographicSize = 70f;
            }
            else
            {
                viewCamera.transform.SetPositionAndRotation(new Vector3(0f, 14f, -30f), Quaternion.Euler(18f, 0f, 0f));
                viewCamera.fieldOfView = 50f;
            }
        }
    }
}
