using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.PerfLab
{
    // 대량 전투 성능 시제품 순서 2 (Docs/GDD/PERF_PROTOTYPE.md)
    // - 적: 데이터 배열 + Burst Job으로 성벽까지 행군
    // - 아군: 성벽 위에 고정 배치 (사격은 순서 3)
    // - 그리기: 개체마다 GameObject 없이 종류별 1회 인스턴싱 호출
    // 조작: 1/2/3 단계 전환(S1~S3), C 카메라 원근/직교 전환, F8 성능 표시
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

        [SerializeField] Shader spriteShader;
        [SerializeField] int startStage;
        [SerializeField] float wallLength = 400f;
        [SerializeField] float wallHeight = 4f;
        [SerializeField] float spawnDepthMin = 80f;
        [SerializeField] float spawnDepthMax = 320f;
        [SerializeField] float enemySpeedMin = 3f;
        [SerializeField] float enemySpeedMax = 6f;
        [SerializeField] float animFps = 8f;
        [SerializeField] uint randomSeed = 12345;

        static readonly float4 EnemyTintSize = new float4(0.85f, 0.25f, 0.2f, 1.6f);
        static readonly float4 AllyTintSize = new float4(0.3f, 0.5f, 0.95f, 1.4f);
        static readonly int InstancesId = Shader.PropertyToID("_Instances");

        NativeArray<float3> enemyPositions;
        NativeArray<float> enemySpeeds;
        NativeArray<float> enemyAnimPhases;
        NativeArray<byte> enemyAtWall;
        NativeArray<PerfLabInstance> enemyInstances;
        NativeArray<PerfLabInstance> allyInstances;

        GraphicsBuffer enemyBuffer;
        GraphicsBuffer allyBuffer;
        MaterialPropertyBlock enemyProperties;
        MaterialPropertyBlock allyProperties;
        Material material;
        Mesh quad;
        Texture2D atlas;
        Bounds drawBounds;

        JobHandle frameJob;
        bool jobScheduled;
        int stageIndex;
        int enemyCount;
        int allyCount;

        Camera viewCamera;
        bool orthographic;
        PerfLabOverlay overlay;

        void Awake()
        {
            if (spriteShader == null)
            {
                spriteShader = Shader.Find("HLR/PerfLabSprite");
            }

            if (spriteShader == null)
            {
                Debug.LogError("[PerfLab] HLR/PerfLabSprite 셰이더를 찾지 못했습니다. PerfLab 씬 만들기 메뉴로 씬을 다시 만들어 주세요.");
                enabled = false;
                return;
            }

            quad = PerfLabArt.CreateQuad();
            atlas = PerfLabArt.CreateAtlas();
            material = new Material(spriteShader) { name = "PerfLabSprite (runtime)" };
            material.SetTexture("_MainTex", atlas);
            material.SetFloat("_FrameCount", PerfLabArt.FrameCount);
            enemyProperties = new MaterialPropertyBlock();
            allyProperties = new MaterialPropertyBlock();
            drawBounds = new Bounds(new Vector3(0f, 0f, spawnDepthMax * 0.5f), new Vector3(wallLength * 2f, 100f, spawnDepthMax * 2f + 100f));

            BuildEnvironment();
            overlay = gameObject.AddComponent<PerfLabOverlay>();

            stageIndex = Mathf.Clamp(startStage, 0, Stages.Length - 1);
            Spawn(Stages[stageIndex]);
        }

        void Update()
        {
            HandleInput();

            frameJob = new EnemyMoveJob
            {
                Positions = enemyPositions,
                Speeds = enemySpeeds,
                AtWall = enemyAtWall,
                DeltaTime = Time.deltaTime,
                StopZ = 1f,
            }.Schedule(enemyCount, 256);

            frameJob = new WriteEnemyInstancesJob
            {
                Positions = enemyPositions,
                AnimPhases = enemyAnimPhases,
                AtWall = enemyAtWall,
                Instances = enemyInstances,
                Time = Time.time,
                AnimFps = animFps,
                FrameCount = PerfLabArt.FrameCount,
                TintSize = EnemyTintSize,
            }.Schedule(enemyCount, 256, frameJob);

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
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, drawBounds, enemyCount, enemyProperties);
            Graphics.DrawMeshInstancedProcedural(quad, 0, material, drawBounds, allyCount, allyProperties);
        }

        void OnDestroy()
        {
            frameJob.Complete();
            jobScheduled = false;
            ReleaseData();

            if (material != null) Destroy(material);
            if (atlas != null) Destroy(atlas);
            if (quad != null) Destroy(quad);
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            int requested = -1;
            if (keyboard.digit1Key.wasPressedThisFrame) requested = 0;
            if (keyboard.digit2Key.wasPressedThisFrame) requested = 1;
            if (keyboard.digit3Key.wasPressedThisFrame) requested = 2;
            if (requested >= 0 && requested != stageIndex)
            {
                stageIndex = requested;
                Spawn(Stages[stageIndex]);
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                orthographic = !orthographic;
                ApplyCamera();
            }
        }

        void Spawn(Stage stage)
        {
            ReleaseData();

            enemyCount = stage.Enemies;
            allyCount = stage.Allies;

            enemyPositions = new NativeArray<float3>(enemyCount, Allocator.Persistent);
            enemySpeeds = new NativeArray<float>(enemyCount, Allocator.Persistent);
            enemyAnimPhases = new NativeArray<float>(enemyCount, Allocator.Persistent);
            enemyAtWall = new NativeArray<byte>(enemyCount, Allocator.Persistent);
            enemyInstances = new NativeArray<PerfLabInstance>(enemyCount, Allocator.Persistent);
            allyInstances = new NativeArray<PerfLabInstance>(allyCount, Allocator.Persistent);

            var random = new Unity.Mathematics.Random(randomSeed == 0 ? 1u : randomSeed);
            float halfLength = wallLength * 0.5f;
            for (int i = 0; i < enemyCount; i++)
            {
                enemyPositions[i] = new float3(
                    random.NextFloat(-halfLength, halfLength),
                    0f,
                    random.NextFloat(spawnDepthMin, spawnDepthMax));
                enemySpeeds[i] = random.NextFloat(enemySpeedMin, enemySpeedMax);
                enemyAnimPhases[i] = random.NextFloat();
            }

            // 아군: 성벽 위에 가로로 줄지어 세우고, 넘치면 뒤 줄로
            int columns = math.max(1, (int)(wallLength / 0.8f));
            float spacing = wallLength / columns;
            for (int i = 0; i < allyCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                allyInstances[i] = new PerfLabInstance
                {
                    PosFrame = new float4(-halfLength + (column + 0.5f) * spacing, wallHeight, -0.5f - row * 0.8f, 0f),
                    TintSize = AllyTintSize,
                };
            }

            enemyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(1, enemyCount), PerfLabInstance.Stride);
            allyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, math.max(1, allyCount), PerfLabInstance.Stride);
            allyBuffer.SetData(allyInstances);
            enemyProperties.SetBuffer(InstancesId, enemyBuffer);
            allyProperties.SetBuffer(InstancesId, allyBuffer);

            if (overlay != null)
            {
                overlay.StageName = $"{stage.Name} (1/2/3 단계, C 카메라)";
                overlay.EnemyCount = enemyCount;
                overlay.AllyCount = allyCount;
            }
        }

        void ReleaseData()
        {
            if (enemyPositions.IsCreated) enemyPositions.Dispose();
            if (enemySpeeds.IsCreated) enemySpeeds.Dispose();
            if (enemyAnimPhases.IsCreated) enemyAnimPhases.Dispose();
            if (enemyAtWall.IsCreated) enemyAtWall.Dispose();
            if (enemyInstances.IsCreated) enemyInstances.Dispose();
            if (allyInstances.IsCreated) allyInstances.Dispose();

            enemyBuffer?.Release();
            enemyBuffer = null;
            allyBuffer?.Release();
            allyBuffer = null;
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
