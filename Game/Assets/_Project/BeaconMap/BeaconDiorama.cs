using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.Render;
using HumanLastRampart.Core.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HumanLastRampart.BeaconMap
{
    // 봉화선 지도의 입체 모형 (W10, Concept_Beacon_v1 '봉화선 지도 = 게임 안 입체 모형').
    // 낮은 언덕이 있는 어두운 땅 위에 요새 모형을 놓고, 진짜 유니티 조명(달빛 + 점화한 요새의 점광원 그림자)으로 비춘다.
    // 요새 상태: 점화(따뜻한 점광원·불꽃·그림자) / 불 꺼짐(차가운 돌·무너진 돌조각) / 안개 속(어둡고 안개가 덮임).
    // 길: 점화한 요새 사이는 따뜻한 점선(은은히 번쩍), 다음 요새 쪽은 어둡게.
    // 조정할 수치는 아래 상수에 모았다. 사진을 보고 여기만 고친다.
    public sealed class BeaconDiorama : MonoBehaviour
    {
        // ── 땅 ──
        const float GroundMinX = -30f, GroundMaxX = 38f, GroundMinZ = -16f, GroundMaxZ = 28f, GroundStep = 0.5f;
        const float PadFlatRadius = 3.2f, PadBlendRadius = 5.6f;   // 요새 둘레 평평한 터
        static readonly Color GroundColor = new Color(0.34f, 0.40f, 0.28f);
        const float GroundTileMeters = 4f;
        const float HillNoise = 0.11f;
        // x, z, 반지름, 높이
        static readonly Vector4[] Hills =
        {
            new Vector4(-5.2f, 5.6f, 4.2f, 0.75f),
            new Vector4(3.6f, -5.4f, 4.6f, 0.60f),
            new Vector4(10.2f, 7.4f, 3.8f, 0.85f),
            new Vector4(-13.5f, -3.5f, 5.0f, 0.95f),
            new Vector4(18.5f, -3.0f, 4.2f, 0.70f),
            new Vector4(0.6f, 8.6f, 5.5f, 0.50f),
            new Vector4(7.0f, 1.6f, 2.6f, 0.30f),
        };

        // ── 요새 모형 ──
        const float Side = 2.5f, WallThick = 0.42f, WallHeight = 0.8f, GateWidth = 0.8f;
        const float TowerSize = 0.74f, TowerHeight = 1.2f;
        const float KeepSize = 0.58f, KeepHeight = 1.35f;
        const float StoneTileMeters = 1.2f;   // 돌 그림 한 장이 덮는 모형 길이 (그림 128화소)
        static readonly Color StoneLit = new Color(0.95f, 0.88f, 0.78f);
        static readonly Color StoneDead = new Color(0.66f, 0.70f, 0.78f);
        static readonly Color StoneFog = new Color(0.24f, 0.26f, 0.33f);

        // ── 불빛 (점화한 요새) ──
        static readonly Color FireLight = new Color(1.0f, 0.58f, 0.24f);
        const float FireIntensity = 42f, FireRange = 16f, FireHeight = 1.95f;
        const float FlameSize = 1.5f, FlameFps = 10f;

        // ── 달빛·바탕 ──
        static readonly Color MoonColor = new Color(0.62f, 0.70f, 0.95f);
        const float MoonIntensity = 2.0f;
        static readonly Color Ambient = new Color(0.17f, 0.20f, 0.28f);
        static readonly Color Background = new Color(0.012f, 0.014f, 0.024f);

        // ── 화면 효과 (절제: 밤이 어둡고 읽혀야 한다) ──
        const float BloomThreshold = 1.0f, BloomIntensity = 0.38f, BloomScatter = 0.62f;
        const float VignetteIntensity = 0.30f;
        const float TiltStrength = 0.65f;

        // ── 길 ──
        const float DotSpacing = 0.42f, DotRadius = 0.2f, HaloRadius = 0.55f, DotLift = 0.13f;
        static readonly Color DotLit = new Color(1.0f, 0.80f, 0.45f);
        static readonly Color DotDim = new Color(0.80f, 0.60f, 0.40f);

        // ── 안개 (가장자리·미지의 요새만. 회색 안개막이 아니라 어두운 덩어리) ──
        static readonly Color FogEdgeColor = new Color(0.012f, 0.016f, 0.030f, 0.80f);
        static readonly Color FogSiteColor = new Color(0.15f, 0.19f, 0.29f, 0.85f);

        // ── 카메라 (전투 사진기처럼 비스듬한 2.5D) ──
        static readonly Vector3 CameraTarget = new Vector3(2.6f, 0f, 0.3f);
        const float CameraPitch = 38f, CameraDistance = 30f, CameraFov = 36f;
        const float IntroSeconds = 1.8f, IntroExtraDistance = 4.5f, IntroExtraPitch = 7f;
        const float DriftX = 0.75f, DriftZ = 0.35f, DriftYaw = 1.2f;

        public static float Height(float x, float z)
        {
            float h = 0f;
            foreach (Vector4 hill in Hills)
            {
                float d = Mathf.Sqrt((x - hill.x) * (x - hill.x) + (z - hill.y) * (z - hill.y));
                float t = Mathf.Clamp01(1f - d / hill.z);
                h += hill.w * t * t * (3f - 2f * t);
            }

            h += HillNoise * (Mathf.PerlinNoise(x * 0.21f + 17f, z * 0.21f + 5f) - 0.5f) * 2f;
            // 먼 가장자리는 살짝 낮아져 어둠에 묻힌다
            float edge = Mathf.Max(0f, Mathf.Abs(x - 4f) - 22f) * 0.12f;
            h -= edge;

            foreach (Vector2 site in BeaconMapModel.Sites)
            {
                float d = Mathf.Sqrt((x - site.x) * (x - site.x) + (z - site.y) * (z - site.y));
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PadFlatRadius, PadBlendRadius, d));
                h *= w;
            }

            return h;
        }

        // ── 바깥에서 보는 값 ──
        public Camera Cam { get; private set; }
        public bool IntroDone => elapsed >= IntroSeconds;
        public float Elapsed => elapsed;
        public int LightCount => lights.Count;
        public IReadOnlyList<Light> Lights => lights;
        public int FortressCount => fortresses.Count;
        public Mesh GroundMesh { get; private set; }
        public int DotCount => dots.Count;

        public Vector3 LabelAnchor(int index) => fortresses[index].LabelAnchor;
        public FortressMapState StateOf(int index) => fortresses[index].State;

        // ── 안쪽 ──
        sealed class Fortress
        {
            public FortressMapState State;
            public Vector3 LabelAnchor;
            public Light Light;
            public Flame Flame;
            public float Phase;
        }

        sealed class Flame
        {
            public Transform Transform;
            public Mesh Mesh;
            public Vector3[] Vertices;
            public int Frame;
            public float Timer;
        }

        struct Dot
        {
            public Vector3 Position;
            public float Base;       // 밝기 (점화한 길 1, 다음 요새 쪽 어둡게)
            public float Along;      // 길을 따라 간 거리 (번쩍임 위상)
        }

        sealed class FogBlob
        {
            public Transform Transform;
            public Vector3 Anchor;
            public float Phase, Speed, Reach;
        }

        readonly List<Object> owned = new List<Object>();
        readonly List<Fortress> fortresses = new List<Fortress>();
        readonly List<Light> lights = new List<Light>();
        readonly List<Dot> dots = new List<Dot>();
        readonly List<FogBlob> fog = new List<FogBlob>();
        Mesh dotMesh;
        Color[] dotColors;
        Material flameMaterial, dotMaterial, fogEdgeMaterial, fogSiteMaterial;
        Texture2D flameTexture;
        Material stoneLit, stoneDead, stoneFog;
        Vector2 stoneNormalTile;
        float elapsed;
        (float strength, float top, float bottom, float radius, float bottomScale, Color shafts) tiltBefore;
        bool tiltApplied, released, renderSettingsSaved;
        VolumeProfile profile;
        Material skyboxBefore;
        AmbientMode ambientModeBefore;
        Color ambientBefore;
        bool fogBefore;

        public static BeaconDiorama Create(Transform parent, Camera camera, BeaconMapModel model)
        {
            var root = new GameObject("Beacon Diorama");
            root.transform.SetParent(parent, false);
            var diorama = root.AddComponent<BeaconDiorama>();
            diorama.Build(camera, model);
            return diorama;
        }

        void Build(Camera camera, BeaconMapModel model)
        {
            Cam = camera;
            BuildAtmosphere();
            BuildLook();
            BuildGround();
            BuildMaterials();
            foreach (FortressNode node in model.Nodes) BuildFortress(node);
            BuildRoute(model);
            BuildFog(model);
            PlaceCamera();
        }

        // ── 바탕·달빛·화면 효과 ──────────────────────────────────────────
        void BuildAtmosphere()
        {
            // 화면이 닫힐 때 되돌릴 수 있게 지금 값을 적어 둔다
            skyboxBefore = RenderSettings.skybox;
            ambientModeBefore = RenderSettings.ambientMode;
            ambientBefore = RenderSettings.ambientLight;
            fogBefore = RenderSettings.fog;
            renderSettingsSaved = true;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.fog = false;

            var moon = new GameObject("Moon Light").AddComponent<Light>();
            moon.transform.SetParent(transform, false);
            moon.transform.rotation = Quaternion.Euler(52f, 28f, 0f);
            moon.type = LightType.Directional;
            moon.color = MoonColor;
            moon.intensity = MoonIntensity;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.85f;
        }

        void BuildLook()
        {
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Background;
            Cam.orthographic = false;
            Cam.fieldOfView = CameraFov;
            Cam.nearClipPlane = 0.5f;
            Cam.farClipPlane = 140f;
            Cam.allowHDR = true;
            UniversalAdditionalCameraData data = Cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }

            var host = new GameObject("Beacon Look (화면 효과)");
            host.transform.SetParent(transform, false);
            var volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Beacon Look (runtime)";
            volume.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.tint.Override(new Color(1f, 0.92f, 0.80f));
            bloom.highQualityFiltering.Override(false);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(0.55f);
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(10f);
            color.saturation.Override(6f);

            // 미니어처 흐림: 위·아래 띠만 아주 약하게 (전투 화면과 같은 장치)
            // 사용자가 정한 흐림 세기(설정)를 따른다 — 0이면 꺼진다. 끝날 때 값을 모두 되돌린다
            tiltBefore = (TiltShiftFeature.Strength, TiltShiftFeature.TopStart, TiltShiftFeature.BottomEnd, TiltShiftFeature.MaxRadiusPixels, TiltShiftFeature.BottomScale, TiltShiftFeature.ShaftColor);
            TiltShiftFeature.TopStart = 0.78f;
            TiltShiftFeature.BottomEnd = 0.14f;
            TiltShiftFeature.MaxRadiusPixels = 4f;
            TiltShiftFeature.BottomScale = 0.8f;
            TiltShiftFeature.ShaftColor = Color.clear;
            TiltShiftFeature.Strength = TiltStrength * GameSettings.TiltStrength;
            tiltApplied = true;
        }

        // ── 땅 ───────────────────────────────────────────────────────────
        void BuildGround()
        {
            int nx = Mathf.RoundToInt((GroundMaxX - GroundMinX) / GroundStep) + 1;
            int nz = Mathf.RoundToInt((GroundMaxZ - GroundMinZ) / GroundStep) + 1;
            var vertices = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    float x = GroundMinX + i * GroundStep, z = GroundMinZ + j * GroundStep;
                    vertices[j * nx + i] = new Vector3(x, Height(x, z), z);
                    uvs[j * nx + i] = new Vector2(x / GroundTileMeters, z / GroundTileMeters);
                }
            }

            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int j = 0; j < nz - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    int v00 = j * nx + i, v10 = v00 + 1, v01 = v00 + nx, v11 = v01 + 1;
                    triangles[t++] = v00; triangles[t++] = v01; triangles[t++] = v11;
                    triangles[t++] = v00; triangles[t++] = v11; triangles[t++] = v10;
                }
            }

            GroundMesh = new Mesh { name = "Beacon Ground", indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            GroundMesh.vertices = vertices;
            GroundMesh.uv = uvs;
            GroundMesh.triangles = triangles;
            GroundMesh.RecalculateNormals();
            GroundMesh.RecalculateBounds();
            owned.Add(GroundMesh);

            var ground = new GameObject("Beacon Ground");
            ground.transform.SetParent(transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = GroundMesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            Texture2D grass = Resources.Load<Texture2D>("BattleArt/World/field_grass_albedo");
            Material material = MakeLit(GroundColor, 0.05f);
            if (grass != null)
            {
                // 풀 그림이 있으면 어두운 색을 곱해 깐다 (그림이 밝아 그대로 쓰면 밤이 아니게 된다)
                material.SetTexture("_BaseMap", grass);
                material.mainTexture = grass;
                var tint = new Color(GroundColor.r * 1.5f, GroundColor.g * 1.5f, GroundColor.b * 1.5f, 1f);
                material.SetColor("_BaseColor", tint);
                material.SetColor("_Color", tint);
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        // 공유 Resources 그림은 읽기만 한다 (반복 설정은 가져오기 설정이 이미 Repeat라 건드리지 않는다)
        void BuildMaterials()
        {
            Texture2D albedo = Resources.Load<Texture2D>("BattleArt/World/wall_stone_albedo");
            Texture2D normal = albedo != null ? Resources.Load<Texture2D>("BattleArt/World/wall_stone_normal") : null;
            stoneLit = MakeStone(albedo, normal, StoneLit);
            stoneDead = MakeStone(albedo, normal, StoneDead);
            stoneFog = MakeStone(albedo, normal, StoneFog);
            stoneNormalTile = albedo != null ? new Vector2(StoneTileMeters, StoneTileMeters) : Vector2.zero;

            flameTexture = Resources.Load<Texture2D>("BattleArt/Effects/torch_flame");
            Shader sprite = Shader.Find("Sprites/Default");
            Texture2D disc = Own(SoftDisc());
            flameMaterial = MakeSprite(sprite, flameTexture != null ? flameTexture : disc, flameTexture != null ? Color.white : new Color(1f, 0.6f, 0.2f, 1f));
            dotMaterial = MakeSprite(sprite, disc, Color.white);
            fogEdgeMaterial = MakeSprite(sprite, Own(FogTexture(3)), FogEdgeColor);
            fogSiteMaterial = MakeSprite(sprite, Own(FogTexture(8)), FogSiteColor);
        }

        Material MakeStone(Texture2D albedo, Texture2D normal, Color tint)
        {
            Material m = MakeLit(tint, 0.14f);
            if (albedo != null)
            {
                m.SetTexture("_BaseMap", albedo);
                m.mainTexture = albedo;
                if (normal != null && m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", normal);
                    if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0.45f);
                    m.EnableKeyword("_NORMALMAP");
                }
            }

            return m;
        }

        Material MakeLit(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m;
            if (shader != null) m = new Material(shader);
            else
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                Kill(probe);
            }

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            owned.Add(m);
            return m;
        }

        Material MakeSprite(Shader shader, Texture2D texture, Color color)
        {
            var m = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader")) { mainTexture = texture, color = color };
            owned.Add(m);
            return m;
        }

        // ── 요새 ─────────────────────────────────────────────────────────
        void BuildFortress(FortressNode node)
        {
            var fortress = new Fortress { State = node.State, Phase = node.Number * 1.7f };
            float groundY = Height(node.Site.x, node.Site.y);
            var root = new GameObject($"Fortress {node.Number} ({node.State})").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(node.Site.x, groundY, node.Site.y);
            Material stone = node.State == FortressMapState.Lit ? stoneLit : node.State == FortressMapState.Dead ? stoneDead : stoneFog;

            float half = Side * 0.5f;
            float piece = (Side + WallThick - GateWidth) * 0.5f;
            float pieceX = GateWidth * 0.5f + piece * 0.5f;
            Box(root, stone, new Vector3(0f, WallHeight * 0.5f, half), new Vector3(Side + WallThick, WallHeight, WallThick));          // 뒤
            Box(root, stone, new Vector3(-pieceX, WallHeight * 0.5f, -half), new Vector3(piece, WallHeight, WallThick));               // 앞 (문 양옆)
            Box(root, stone, new Vector3(pieceX, WallHeight * 0.5f, -half), new Vector3(piece, WallHeight, WallThick));
            Box(root, stone, new Vector3(-half, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, Side - WallThick));         // 옆
            Box(root, stone, new Vector3(half, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, Side - WallThick));
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Box(root, stone, new Vector3(corner.x * half, TowerHeight * 0.5f, corner.y * half), new Vector3(TowerSize, TowerHeight, TowerSize));
                Box(root, stone, new Vector3(corner.x * half, TowerHeight + 0.06f, corner.y * half), new Vector3(TowerSize + 0.12f, 0.12f, TowerSize + 0.12f));
            }

            // 가운데 봉화대: 위에 화로
            Box(root, stone, new Vector3(0f, KeepHeight * 0.5f, 0f), new Vector3(KeepSize, KeepHeight, KeepSize));
            Box(root, stone, new Vector3(0f, KeepHeight + 0.05f, 0f), new Vector3(KeepSize + 0.16f, 0.10f, KeepSize + 0.16f));
            GameObject brazier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            KillCollider(brazier);
            brazier.name = "Brazier";
            brazier.transform.SetParent(root, false);
            brazier.transform.localPosition = new Vector3(0f, KeepHeight + 0.18f, 0f);
            brazier.transform.localScale = new Vector3(0.46f, 0.08f, 0.46f);
            brazier.GetComponent<MeshRenderer>().sharedMaterial = node.State == FortressMapState.Lit ? stoneLit : stone;

            fortress.LabelAnchor = root.position + new Vector3(0f, KeepHeight + 1.55f, 0f);

            if (node.State == FortressMapState.Dead) Rubble(root, node.Number);

            if (node.State == FortressMapState.Lit)
            {
                var lightObject = new GameObject("Beacon Light");
                lightObject.transform.SetParent(root, false);
                lightObject.transform.localPosition = new Vector3(0f, FireHeight, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = FireLight;
                light.intensity = FireIntensity;
                light.range = FireRange;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 1f;
                light.shadowBias = 0.02f;
                light.shadowNormalBias = 0.2f;
                UniversalAdditionalLightData lightData = light.GetUniversalAdditionalLightData();
                if (lightData != null) lightData.usePipelineSettings = false;
                fortress.Light = light;
                lights.Add(light);
                fortress.Flame = MakeFlame(root, new Vector3(0f, KeepHeight + 0.22f, 0f));
            }

            fortresses.Add(fortress);
        }

        void Box(Transform parent, Material material, Vector3 center, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            KillCollider(box);
            box.name = "Block";
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (stoneNormalTile.x > 0f)
            {
                // 큰 면(가로 × 세로)에 맞춰 돌 그림을 반복한다. 상자 면마다 UV가 0~1이라 가장 긴 두 변을 쓴다
                float a = Mathf.Max(size.x, size.z), b = size.y;
                var block = new MaterialPropertyBlock();
                var st = new Vector4(a / stoneNormalTile.x, b / stoneNormalTile.y, center.x * 0.37f, center.y * 0.37f);
                block.SetVector("_BaseMap_ST", st);
                renderer.SetPropertyBlock(block);
            }
        }

        // 무너진 돌조각 (불 꺼진 요새) — 모양과 그림자로 '꺼진 요새'를 읽게 한다
        void Rubble(Transform root, int seed)
        {
            var random = new System.Random(seed * 7919 + 13);
            for (int i = 0; i < 9; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float radius = 1.7f + (float)random.NextDouble() * 1.1f;
                float s = 0.18f + (float)random.NextDouble() * 0.22f;
                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                KillCollider(chunk);
                chunk.name = "Rubble";
                chunk.transform.SetParent(root, false);
                chunk.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, s * 0.4f, Mathf.Sin(angle) * radius - 0.3f);
                chunk.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 30f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 30f);
                chunk.transform.localScale = new Vector3(s * 1.4f, s, s);
                chunk.GetComponent<MeshRenderer>().sharedMaterial = stoneDead;
            }
        }

        Flame MakeFlame(Transform parent, Vector3 localPosition)
        {
            var flame = new Flame();
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            flame.Transform = go.transform;
            float w = FlameSize * 0.5f;
            flame.Vertices = new[] { new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(w, FlameSize, 0f), new Vector3(-w, FlameSize, 0f) };
            flame.Mesh = new Mesh { name = "Flame Quad" };
            flame.Mesh.vertices = flame.Vertices;
            flame.Mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            flame.Mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            owned.Add(flame.Mesh);
            SetFlameFrame(flame, 0);
            go.AddComponent<MeshFilter>().sharedMesh = flame.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = flameMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return flame;
        }

        void SetFlameFrame(Flame flame, int frame)
        {
            flame.Frame = frame;
            flame.Mesh.uv = flameTexture == null ? FlameFallbackUv : FlameUv[frame % FlameFrames];   // 미리 만든 배열 (프레임마다 새로 만들지 않는다)
        }

        const int FlameFrames = 6;
        static readonly Vector2[] FlameFallbackUv = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        static readonly Vector2[][] FlameUv = MakeFlameUv();

        static Vector2[][] MakeFlameUv()
        {
            var frames = new Vector2[FlameFrames][];
            for (int i = 0; i < FlameFrames; i++)
            {
                float u0 = i / (float)FlameFrames, u1 = (i + 1) / (float)FlameFrames;
                frames[i] = new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) };
            }

            return frames;
        }

        // ── 길 ───────────────────────────────────────────────────────────
        void BuildRoute(BeaconMapModel model)
        {
            float along = 0f;
            for (int i = 0; i + 1 < model.Nodes.Count; i++)
            {
                FortressNode a = model.Nodes[i], b = model.Nodes[i + 1];
                bool bothLit = a.State == FortressMapState.Lit && b.State == FortressMapState.Lit;
                // 마지막 점화 요새에서 다음 쪽으로 (1회차를 마치면 3번 요새 쪽 길은 그리지 않는다)
                bool towardNext = a.State == FortressMapState.Lit && !bothLit && !model.RunComplete;
                if (!bothLit && !towardNext) continue;
                float fraction = bothLit ? 1f : b.State == FortressMapState.Dead ? 1f : 0.45f;
                AddSegment(a.Site, b.Site, fraction, bothLit ? 1f : 0.5f, ref along);
            }

            if (dots.Count == 0) return;

            int count = dots.Count;
            var vertices = new Vector3[count * 8];
            var uv = new Vector2[count * 8];
            var triangles = new int[count * 12];
            dotColors = new Color[count * 8];
            for (int i = 0; i < count; i++)
            {
                for (int layer = 0; layer < 2; layer++)
                {
                    int v = i * 8 + layer * 4, t = i * 12 + layer * 6;
                    float r = layer == 0 ? HaloRadius : DotRadius;
                    Vector3 p = dots[i].Position;
                    vertices[v + 0] = p + new Vector3(-r, 0f, -r);
                    vertices[v + 1] = p + new Vector3(r, 0f, -r);
                    vertices[v + 2] = p + new Vector3(r, 0f, r);
                    vertices[v + 3] = p + new Vector3(-r, 0f, r);
                    uv[v + 0] = new Vector2(0f, 0f); uv[v + 1] = new Vector2(1f, 0f); uv[v + 2] = new Vector2(1f, 1f); uv[v + 3] = new Vector2(0f, 1f);
                    triangles[t + 0] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                    triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
                }
            }

            dotMesh = new Mesh { name = "Beacon Route Dots" };
            dotMesh.vertices = vertices;
            dotMesh.uv = uv;
            dotMesh.triangles = triangles;
            dotMesh.colors = dotColors;
            dotMesh.bounds = new Bounds(new Vector3(2f, 0f, 1f), new Vector3(60f, 6f, 40f));
            owned.Add(dotMesh);
            UpdateDotColors();

            var go = new GameObject("Beacon Route");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = dotMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = dotMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void AddSegment(Vector2 from, Vector2 to, float fraction, float brightness, ref float along)
        {
            // 굽이치는 길: 가운데 점을 길에 수직으로 조금 밀어 둥글린다
            Vector2 mid = (from + to) * 0.5f;
            Vector2 dir = (to - from).normalized;
            Vector2 control = mid + new Vector2(-dir.y, dir.x) * ((to - from).magnitude * 0.16f);
            const int samples = 80;
            var path = new List<Vector2>(samples + 1);
            for (int s = 0; s <= samples; s++)
            {
                float t = s / (float)samples;
                path.Add((1f - t) * (1f - t) * from + 2f * (1f - t) * t * control + t * t * to);
            }

            // 요새 둘레 담 밖에서 시작해 담 밖에서 끝낸다
            float clear = Side * 0.5f + 0.7f;
            float length = 0f;
            for (int s = 1; s < path.Count; s++) length += Vector2.Distance(path[s - 1], path[s]);
            float limit = length * fraction;
            float walked = 0f, nextDot = clear;
            for (int s = 1; s < path.Count; s++)
            {
                float step = Vector2.Distance(path[s - 1], path[s]);
                while (nextDot <= walked + step && nextDot <= limit && nextDot <= length - clear * (fraction >= 1f ? 1f : 0f))
                {
                    float f = (nextDot - walked) / Mathf.Max(0.0001f, step);
                    Vector2 p = Vector2.Lerp(path[s - 1], path[s], f);
                    float fade = fraction >= 1f ? 1f : Mathf.Lerp(1f, 0.25f, nextDot / Mathf.Max(0.01f, limit));   // 안개 쪽으로 사라진다
                    dots.Add(new Dot { Position = new Vector3(p.x, Height(p.x, p.y) + DotLift, p.y), Base = brightness * fade, Along = along + nextDot });
                    nextDot += DotSpacing;
                }

                walked += step;
            }

            along += length;
        }

        void UpdateDotColors()
        {
            if (dotMesh == null) return;
            for (int i = 0; i < dots.Count; i++)
            {
                Dot dot = dots[i];
                float wave = 0.78f + 0.22f * Mathf.Sin(elapsed * 2.3f - dot.Along * 0.8f);
                float b = dot.Base * wave;
                Color core = Color.Lerp(DotDim, DotLit, Mathf.Clamp01(dot.Base));
                dotColors[i * 8 + 0] = dotColors[i * 8 + 1] = dotColors[i * 8 + 2] = dotColors[i * 8 + 3] = new Color(core.r, core.g * 0.95f, core.b * 0.9f, 0.20f * b);   // 빛무리
                dotColors[i * 8 + 4] = dotColors[i * 8 + 5] = dotColors[i * 8 + 6] = dotColors[i * 8 + 7] = new Color(core.r, core.g, core.b, Mathf.Clamp01(0.95f * b));   // 점
            }

            dotMesh.colors = dotColors;
        }

        // ── 안개 ─────────────────────────────────────────────────────────
        void BuildFog(BeaconMapModel model)
        {
            // 가장자리: 고리 모양으로 어두운 덩어리 (가운데는 비워 둔다)
            Vector2 center = new Vector2(3.5f, 1.5f);
            const int ring = 20;
            for (int i = 0; i < ring; i++)
            {
                float a = i / (float)ring * Mathf.PI * 2f;
                float rx = 23f + 2.2f * Mathf.Sin(a * 3f), rz = 14f + 1.6f * Mathf.Cos(a * 2f);
                float x = center.x + Mathf.Cos(a) * rx, z = center.y + Mathf.Sin(a) * rz;
                float size = 8.5f + 2.5f * Mathf.Sin(a * 5f + 1f);
                AddFog(fogEdgeMaterial, new Vector3(x, 0.9f, z), size, size * 0.55f, i * 0.9f, 0.5f, 0.9f);
            }

            // 아직 모르는 요새 위: 겹쳐 쌓인 덩어리
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                FortressNode node = model.Nodes[i];
                if (node.State != FortressMapState.Fogged) continue;
                Vector3 p = new Vector3(node.Site.x, Height(node.Site.x, node.Site.y), node.Site.y);
                AddFog(fogSiteMaterial, p + new Vector3(0f, 0.55f, -0.6f), 6.4f, 3.2f, node.Number * 1.3f, 0.45f, 0.35f);
                AddFog(fogSiteMaterial, p + new Vector3(0.5f, 1.25f, 0.2f), 6.0f, 3.0f, node.Number * 1.3f + 2f, 0.38f, 0.35f);
                AddFog(fogSiteMaterial, p + new Vector3(-0.6f, 1.75f, 0.6f), 4.2f, 2.1f, node.Number * 1.3f + 4f, 0.5f, 0.3f);
            }
        }

        void AddFog(Material material, Vector3 position, float width, float height, float phase, float speed, float reach)
        {
            var go = new GameObject("Fog");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var mesh = new Mesh { name = "Fog Quad" };
            float w = width * 0.5f, h = height * 0.5f;
            mesh.vertices = new[] { new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(w, h, 0f), new Vector3(-w, h, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            fog.Add(new FogBlob { Transform = go.transform, Anchor = position, Phase = phase, Speed = speed, Reach = reach });
        }

        // ── 사진기 ───────────────────────────────────────────────────────
        void PlaceCamera()
        {
            // 처음에는 조금 멀고 높은 곳에서 천천히 다가와 자리를 잡고, 그 뒤로는 아주 느리게 떠다닌다
            float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / IntroSeconds));
            float drift = intro;
            Vector3 target = CameraTarget + new Vector3(Mathf.Sin(elapsed * 0.21f) * DriftX, 0f, Mathf.Cos(elapsed * 0.17f) * DriftZ) * drift;
            float yaw = Mathf.Sin(elapsed * 0.13f) * DriftYaw * drift;
            float pitch = CameraPitch + (1f - intro) * IntroExtraPitch;
            float distance = CameraDistance + (1f - intro) * IntroExtraDistance;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Cam.transform.rotation = rotation;
            Cam.transform.position = target - rotation * Vector3.forward * distance;
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            PlaceCamera();

            Quaternion face = Cam.transform.rotation;
            foreach (Fortress f in fortresses)
            {
                if (f.Light != null)
                {
                    float flicker = 1f + 0.05f * Mathf.Sin(elapsed * 7f + f.Phase) + 0.03f * Mathf.Sin(elapsed * 13.7f + f.Phase * 2f);
                    f.Light.intensity = FireIntensity * flicker;
                }

                if (f.Flame != null)
                {
                    f.Flame.Transform.rotation = face;
                    f.Flame.Timer += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    int frame = (int)(f.Flame.Timer * FlameFps + f.Phase * 3f) % 6;
                    if (frame != f.Flame.Frame) SetFlameFrame(f.Flame, frame);
                }
            }

            foreach (FogBlob blob in fog)
            {
                blob.Transform.rotation = face;
                float t = elapsed * blob.Speed * 0.35f + blob.Phase;
                blob.Transform.position = blob.Anchor + new Vector3(Mathf.Sin(t) * blob.Reach, 0f, Mathf.Cos(t * 0.8f) * blob.Reach * 0.4f);
            }

            UpdateDotColors();
        }

        void OnDestroy() { Release(); }

        // 만든 메시·재질·그림을 치우고 미니어처 흐림을 되돌린다 (에디터 시험은 OnDestroy가 안 불려 직접 부른다)
        public void Release()
        {
            if (released) return;
            released = true;
            if (tiltApplied)
            {
                TiltShiftFeature.Strength = tiltBefore.strength;
                TiltShiftFeature.TopStart = tiltBefore.top;
                TiltShiftFeature.BottomEnd = tiltBefore.bottom;
                TiltShiftFeature.MaxRadiusPixels = tiltBefore.radius;
                TiltShiftFeature.BottomScale = tiltBefore.bottomScale;
                TiltShiftFeature.ShaftColor = tiltBefore.shafts;
            }

            if (renderSettingsSaved)
            {
                RenderSettings.skybox = skyboxBefore;
                RenderSettings.ambientMode = ambientModeBefore;
                RenderSettings.ambientLight = ambientBefore;
                RenderSettings.fog = fogBefore;
            }

            // 볼륨 효과 항목은 프로필과 따로 만들어진 개체라 하나씩 먼저 지운다
            if (profile != null)
            {
                foreach (VolumeComponent component in profile.components) Kill(component);
                profile.components.Clear();
                Kill(profile);
                profile = null;
            }

            foreach (Object o in owned) Kill(o);
            owned.Clear();
        }

        // ── 도우미 ───────────────────────────────────────────────────────
        T Own<T>(T o) where T : Object
        {
            owned.Add(o);
            return o;
        }

        static void KillCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Kill(collider);
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // 부드러운 둥근 빛무리 (가운데 밝고 가장자리로 사라짐)
        static Texture2D SoftDisc()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Soft Disc", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 안개 덩어리: 둥근 가장자리 × 느린 덩이 무늬
        static Texture2D FogTexture(int seed)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Fog Cloud", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(1f - r);
                    edge = edge * edge * (3f - 2f * edge);
                    float n = Mathf.PerlinNoise(x * 0.045f + seed * 11f, y * 0.045f + seed * 7f) * 0.6f + Mathf.PerlinNoise(x * 0.11f + seed * 3f, y * 0.11f) * 0.4f;
                    float a = Mathf.Clamp01(edge * (0.35f + 0.9f * n));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
