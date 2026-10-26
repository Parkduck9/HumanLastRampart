using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // K-089: 고정 배경, 실제 3D 바닥·충돌과 화면 평면 인물을 맞춘 정비 마당.
    public sealed class PrepYard : MonoBehaviour
    {
        [Serializable] public sealed class Layout { public Place[] stations; public Occluder[] occluders, blocks; }   // blocks: 그림 속 건물·탑 자리 (그림 없이 걷기만 막는다, K-120)
        [Serializable] public sealed class Place { public string id, name; public float x, y; public float[] rect, sign; }
        [Serializable] public sealed class Occluder { public string id; public float row; public Vector2[] polygon; }
        public sealed class Station
        {
            public string Name;
            public float X;
            public Action Use;
            public RectTransform Sign;
            public BoxCollider Trigger;
            public Place Place;
        }

        const int YardLayer = 31;
        const float WalkSpeed = 5f;
        readonly List<Station> stations = new List<Station>();
        readonly List<Collider> obstacles = new List<Collider>();
        readonly List<Vector2[]> blocks = new List<Vector2[]>();   // 건물·탑 자리 (바닥 좌표 다각형, K-120)
        const float BodyRadius = 0.1f;
        readonly List<(Occluder data, RawImage image)> overlays = new List<(Occluder, RawImage)>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        RectTransform strip, prompt, viewport;
        CanvasGroup blocker, screenInput;
        GameObject confirm, world;
        Camera yardCamera;
        RenderTexture target;
        Mesh heroMesh, shadowMesh;
        CapsuleCollider body;
        Material heroMaterial, shadowMaterial;
        Texture2D mask, heroSheet, idleSheet;
        Layout layout;
        Vector2 ground, facing = Vector2.down;
        int columns = 4, rows = 4;
        float cellWidth, foot, figure;
        bool walking;

        public int StationCount => stations.Count;
        public float HeroX => HeroPixel.x;
        public Vector2 HeroGround => ground;
        public Vector2 HeroPixel => PrepYardProjection.Project(ground);
        public Camera WorldCamera => yardCamera;
        public int ObstacleCount => obstacles.Count + blocks.Count;   // 가림 그림 충돌 + 건물·탑 자리
        // 등대 명령 판처럼 마당 위에 뜬 창 (W9). 떠 있는 동안 걷지 못한다
        GameObject modal;
        public bool ModalOpen => modal != null;

        public void OpenModal(Func<Transform, GameObject> build)
        {
            if (modal != null || confirm != null) return;
            modal = build(transform.parent);
        }

        public void CloseModal()
        {
            if (modal != null) Destroy(modal);
            modal = null;
        }

        // K-120: 가운데 등대 탑은 정비 마당 그림에 이미 그려져 있다 (따로 겹쳐 그리지 않는다). 탑 자리는 layout.json blocks의 장애물로 막는다.
        // 그림 속 등롱 불빛은 고정이라, 등실 유리 자리에 작은 덧그림(어둠 덮개 + 불꽃 + 점화의 밤 테두리 빛)을 올려 복구 상태를 보인다 (W36)
        RawImage characterPicture;
        RawImage lampPicture;
        Texture2D lampTexture;
        CampaignState beacon;
        float lampLight = -1f;
        bool lampIgnition;

        // 덧그림 격자: 정비 그림의 3배 픽셀 칸(48칸 = 144픽셀)을 그대로 쓴다. 왼쪽 위 (1845,108), 등실 유리는 칸 11~38
        public const int LampGrid = 48;
        const int LampOriginX = 1845, LampOriginY = 108, LampScale = 3;
        const int GlassL = 11, GlassR = 38, GlassT = 11, GlassB = 38;
        public float LampLightNow => lampLight;
        public bool LampIgnitionNow => lampIgnition;

        // 등실 불빛 세기 0~1: 복구 0 = 꺼짐, 그다음 약함(0.25)에서 강함(0.85)까지, 점화의 밤 = 1 (정비 마당 W36. 전투 쪽 TowerFlameStrength는 꺼짐이 없어 따로 둔다)
        public static float LampLight(float repair, bool ignitionNight)
        {
            if (ignitionNight) return 1f;
            if (repair <= 0f) return 0f;
            return 0.25f + 0.6f * Mathf.Clamp01(repair / BeaconRules.RepairMax);
        }

        // 그림 속 등롱을 가리는 어둠 덮개 투명도: 불빛이 약할수록 짙다 (0.25 단계로 끊어 흐린 번짐을 피한다)
        public static float LampCoverAlpha(float light) => Mathf.Round(Mathf.Clamp01((0.8f - light) / 0.8f) * 4f) / 4f;

        public static int LampFlameRows(float light) => light <= 0f ? 0 : Mathf.RoundToInt(2f + light * 14f);

        // 덧그림 픽셀을 칠한다 (왼쪽 위 기준 칸). 시험도 이 함수를 그대로 부른다
        public static Color32[] PaintLamp(float light, bool ignition)
        {
            var px = new Color32[LampGrid * LampGrid];
            void Put(int x, int top, Color32 c)
            {
                if (x < 0 || x >= LampGrid || top < 0 || top >= LampGrid) return;
                px[(LampGrid - 1 - top) * LampGrid + x] = c;
            }
            // 테두리 빛: 유리 사각형에서 맨해튼 거리 띠 (모서리가 45도로 잘린 딱딱한 픽셀)
            if (light >= 0.5f)
            {
                int rings = ignition ? 2 : 1;
                for (int y = 0; y < LampGrid; y++)
                    for (int x = 0; x < LampGrid; x++)
                    {
                        int d = Mathf.Max(GlassL - x, 0, x - GlassR) + Mathf.Max(GlassT - y, 0, y - GlassB);
                        if (d < 1 || d > 3 * rings) continue;
                        Put(x, y, d <= 3 ? new Color32(255, 196, 96, 64) : new Color32(255, 196, 96, 34));
                    }
            }
            float cover = LampCoverAlpha(light);
            if (cover > 0f)
            {
                byte a = (byte)Mathf.RoundToInt(cover * 255f);
                for (int y = GlassT; y <= GlassB; y++)
                    for (int x = GlassL; x <= GlassR; x++)
                    {
                        bool bar = x == 17 || x == 31 || y == 21;   // 유리 쇠살대 (그림 속 틀 자리)
                        Put(x, y, bar ? new Color32(14, 12, 16, a) : new Color32(22, 28, 42, a));
                    }
                if (light <= 0f)   // 꺼진 등: 차가운 유리 반사 몇 칸
                    foreach (var p in new[] { (13, 13), (14, 13), (13, 14), (22, 24), (23, 24), (35, 15) })
                        Put(p.Item1, p.Item2, new Color32(66, 86, 118, a));
            }
            int rows = LampFlameRows(light);
            if (rows > 0)
            {
                int baseRow = 35, center = 24, maxHalf = Mathf.RoundToInt(1f + light * 4f);
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)rows;
                    int half = Mathf.RoundToInt(maxHalf * Mathf.Pow(1f - t, 0.7f));
                    for (int dx = -half; dx <= half; dx++)
                    {
                        int ax = Mathf.Abs(dx);
                        Color32 c = ax == half && half > 0 ? new Color32(235, 98, 24, 255)
                            : (ax <= half - 2 && t < 0.55f ? new Color32(255, 244, 176, 255) : new Color32(255, 200, 64, 255));
                        Put(center + dx, baseRow - r, c);
                    }
                }
            }
            return px;
        }

        // 등대 복구가 바뀐 뒤 불러 덧그림을 다시 칠한다 (등대 창·위쪽 줄의 수리 단추가 부른다)
        public void RefreshTower()
        {
            if (lampTexture == null) return;
            bool ignition = beacon != null && BeaconRules.IsIgnitionNight(beacon);
            lampLight = LampLight(beacon != null ? beacon.BeaconRepair : 0f, ignition);
            lampIgnition = ignition;
            lampTexture.SetPixels32(PaintLamp(lampLight, ignition));
            lampTexture.Apply(false);
        }

        public void BindBeacon(CampaignState campaign)
        {
            beacon = campaign;
            RefreshTower();
        }

        public bool CanMove => confirm == null && modal == null && (blocker == null || blocker.interactable)
            && (screenInput == null || screenInput.interactable);
        public bool IsOccluderVisible(string id) => overlays.Exists(o => o.data.id == id && o.image.enabled);

        public static PrepYard Build(ScreenView view, UnitDef unit, bool backdrop = false)
        {
            RectTransform layer = view.AddBackdropLayer("Prep Yard");
            var yard = layer.gameObject.AddComponent<PrepYard>();
            yard.blocker = view.PanelGroup;
            yard.screenInput = layer.GetComponentInParent<CanvasGroup>();
            yard.heroSheet = unit != null ? unit.Sheet : null;
            yard.idleSheet = unit != null ? unit.IdleSheet : null;
            yard.BuildScene(layer);
            return yard;
        }

        T Own<T>(T obj) where T : UnityEngine.Object { owned.Add(obj); return obj; }

        RawImage Picture(string name, Texture texture)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(strip, false);
            var image = obj.GetComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(3840f, 1080f);
            return image;
        }

        void BuildScene(RectTransform layer)
        {
            layout = JsonUtility.FromJson<Layout>(Resources.Load<TextAsset>("PrepYard/layout").text);
            viewport = layer;
            layer.gameObject.AddComponent<RectMask2D>();
            strip = new GameObject("Panorama", typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(layer, false);
            strip.anchorMin = strip.anchorMax = strip.pivot = Vector2.zero;
            strip.sizeDelta = new Vector2(3840f, 1080f);
            Picture("K089 Background", Resources.Load<Texture2D>("PrepYard/prep_yard"));
            lampTexture = Own(new Texture2D(LampGrid, LampGrid, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            var lampObject = new GameObject("Beacon Lamp Glow", typeof(RectTransform), typeof(RawImage));
            lampObject.transform.SetParent(strip, false);
            lampPicture = lampObject.GetComponent<RawImage>();
            lampPicture.texture = lampTexture; lampPicture.raycastTarget = false;
            var lampRect = lampPicture.rectTransform;
            lampRect.anchorMin = lampRect.anchorMax = lampRect.pivot = Vector2.zero;
            lampRect.sizeDelta = Vector2.one * (LampGrid * LampScale);
            lampRect.anchoredPosition = new Vector2(LampOriginX, 1080f - LampOriginY - LampGrid * LampScale);
            RefreshTower();
            mask = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            mask.LoadImage(Resources.Load<TextAsset>("PrepYard/walkable_mask").bytes);
            mask.filterMode = FilterMode.Point;

            // 원본 씬·패키지 설정을 바꾸지 않는 독립 공간. 화면이 닫힐 때 함께 해제한다.
            world = new GameObject("Prep Yard 3D");
            world.transform.position = new Vector3(0f, -1000f, 0f);
            var cameraObject = new GameObject("Prep Yard Camera", typeof(Camera));
            cameraObject.transform.SetParent(world.transform, false);
            yardCamera = cameraObject.GetComponent<Camera>();
            cameraObject.transform.localPosition = new Vector3(0f, 7.26037736f, -3.80821918f);
            cameraObject.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            yardCamera.cullingMask = 1 << YardLayer;
            yardCamera.clearFlags = CameraClearFlags.SolidColor;
            yardCamera.backgroundColor = Color.clear;
            yardCamera.allowHDR = false; yardCamera.allowMSAA = false;
            yardCamera.nearClipPlane = 0.1f; yardCamera.farClipPlane = 100f;
            target = Own(new RenderTexture(3840, 1080, 16, RenderTextureFormat.ARGB32));
            target.filterMode = FilterMode.Point; target.Create();
            yardCamera.targetTexture = target;
            var projection = Matrix4x4.Perspective(2f * Mathf.Atan(540f / 892.515593f) * Mathf.Rad2Deg,
                3840f / 1080f, 0.1f, 100f);
            projection.m12 = 2f * 455.294118f / 1080f - 1f;
            yardCamera.projectionMatrix = projection;
            characterPicture = Picture("World Character And Shadow", target);

            var floor = new GameObject("Walkable Ground", typeof(BoxCollider));
            floor.transform.SetParent(world.transform, false);
            floor.GetComponent<BoxCollider>().center = new Vector3(0f, -0.1f, 4f);
            floor.GetComponent<BoxCollider>().size = new Vector3(65f, 0.2f, 8f);
            var heroBody = new GameObject("Hero Collider", typeof(CapsuleCollider));
            heroBody.transform.SetParent(world.transform, false);
            body = heroBody.GetComponent<CapsuleCollider>();
            body.height = 1.7f; body.radius = BodyRadius; body.center = Vector3.up * 0.85f; body.isTrigger = true;   // K-120: 건물 모서리에 걸리지 않게 반지름 0.12 → 0.1
            var rigid = heroBody.AddComponent<Rigidbody>();
            rigid.isKinematic = true; rigid.useGravity = false;

            var shader = Resources.Load<Shader>("PrepYard/PrepYardSprite");
            if (shader == null) throw new InvalidOperationException("정비 마당 그림 셰이더 없음");
            heroMaterial = Own(new Material(shader));
            shadowMaterial = Own(new Material(shader));
            shadowMaterial.SetColor("_Tint", new Color(0f, 0f, 0f, 0.28f));
            shadowMaterial.renderQueue = 2990;
            heroMesh = Sprite("Hero Billboard", heroMaterial);
            shadowMesh = Sprite("Ground Projected Shadow", shadowMaterial);
            if (heroSheet != null)
            {
                rows = Mathf.Max(1, heroSheet.height / 384); cellWidth = heroSheet.width / (float)columns;
                foot = cellWidth == 288f ? 360f : 352f;
                figure = cellWidth == 288f ? 219f : 300f;
            }
            foreach (var data in layout.occluders)
            {
                overlays.Add((data, Picture("Occluder " + data.id, Resources.Load<Texture2D>("PrepYard/" + data.id))));
                var obj = new GameObject("Obstacle " + data.id, typeof(MeshCollider));
                obj.transform.SetParent(world.transform, false);
                var collider = obj.GetComponent<MeshCollider>();
                collider.sharedMesh = Own(ObstacleMesh(data.polygon));
                obstacles.Add(collider);
            }
            // 건물·탑 자리는 바닥 좌표 다각형으로 직접 막는다 (속이 빈 충돌 메시는 큰 건물 가운데·모서리에서 겹침 판정이 들쭉날쭉했다)
            if (layout.blocks != null)
                foreach (var data in layout.blocks) blocks.Add(Array.ConvertAll(data.polygon, PrepYardProjection.Ground));
            prompt = Ui.KeyHint(strip, "E", "들어가기");
            prompt.anchorMin = prompt.anchorMax = Vector2.zero; prompt.pivot = new Vector2(0.5f, 0f);
            prompt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hints = Ui.Row(layer, "Key Hints", UiTokens.S24);
            Ui.Place((RectTransform)hints.transform, Vector2.zero, new Vector2(32f, 32f), new Vector2(650f, 40f), Vector2.zero);
            Ui.KeyHint(hints.transform, "W A S D · 방향키", "이동");
            // 처음 서는 자리: 어느 장소 칸에도 걸리지 않는 탑 앞 빈 땅 (시작하자마자 E 안내가 뜨지 않게)
            ground = PrepYardProjection.Ground(new Vector2(1920f, 920f));
            foreach (Vector2 pixel in new[] { new Vector2(1920f, 920f), new Vector2(1920f, 960f), new Vector2(1500f, 900f), new Vector2(2300f, 900f) })
            {
                Vector2 candidate = PrepYardProjection.Ground(pixel);
                bool inStation = Array.Exists(layout.stations, s => new Rect(s.rect[0], s.rect[1], s.rect[2], s.rect[3]).Contains(pixel));
                if (!inStation && CanStand(candidate)) { ground = candidate; break; }
            }
            PlaceHero();
        }

        Mesh Sprite(string name, Material material)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.layer = YardLayer; obj.transform.SetParent(world.transform, false);
            var mesh = Own(new Mesh { name = name });
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }

        static Mesh ObstacleMesh(Vector2[] polygon)
        {
            int n = polygon.Length;
            var vertices = new Vector3[n * 2]; var triangles = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Vector2 p = PrepYardProjection.Ground(polygon[i]);
                vertices[i] = new Vector3(p.x, 0f, p.y); vertices[i + n] = vertices[i] + Vector3.up * 3f;
                int j = (i + 1) % n;
                triangles.AddRange(new[] { i, j, j + n, i, j + n, i + n });
            }
            var mesh = new Mesh { name = "Prep Obstacle Sides" };
            mesh.vertices = vertices; mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals();
            return mesh;
        }

        public void AddStation(string name, float x, Color color, Action use, Vector2? signAt = null)
        {
            var data = Array.Find(layout.stations, p => p.name == name);
            if (data == null) throw new InvalidOperationException("정비 장소 좌표 없음: " + name);
            var station = new Station { Name = name, X = data.x, Use = use, Place = data };
            var obj = new GameObject("Trigger " + data.id, typeof(BoxCollider));
            obj.transform.SetParent(world.transform, false);
            Vector2 a = PrepYardProjection.Ground(new Vector2(data.rect[0], data.rect[1]));
            Vector2 b = PrepYardProjection.Ground(new Vector2(data.rect[0] + data.rect[2], data.rect[1] + data.rect[3]));
            station.Trigger = obj.GetComponent<BoxCollider>(); station.Trigger.isTrigger = true;
            station.Trigger.center = new Vector3((a.x + b.x) / 2f, 0.85f, (a.y + b.y) / 2f);
            station.Trigger.size = new Vector3(Mathf.Abs(b.x - a.x), 1.7f, Mathf.Abs(b.y - a.y));
            Image plate = Ui.Panel(strip, "Place " + name, UiPanel.Plate); plate.raycastTarget = false;
            Vector2 signPixel = signAt ?? (data.sign != null && data.sign.Length >= 2 ? new Vector2(data.sign[0], data.sign[1]) : new Vector2(data.x, data.y - 180f));   // K-120: 간판 자리는 layout.json sign (건물 지붕 위)
            Ui.Place(plate.rectTransform, Vector2.zero, new Vector2(signPixel.x, 1080f - signPixel.y), new Vector2(240f, 52f), new Vector2(0.5f, 0.5f));
            Text sign = Ui.Label(plate.transform, "Name", name, UiTokens.H2, UiTokens.BrassLight, TextAnchor.MiddleCenter, true);
            Ui.Stretch(sign.rectTransform); station.Sign = plate.rectTransform; stations.Add(station);
            prompt.SetAsLastSibling();
        }

        void Update()
        {
            var keyboard = Keyboard.current; Vector2 input = Vector2.zero;
            if (CanMove && keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            }
            input += PrepYardTouch.Axis(this);   // W40 터치 이동 패드 (터치가 꺼져 있으면 0)
            if (modal != null && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseModal();   // Esc로 등대 창 닫기 (S5)
            if (beacon != null)
            {
                bool ignitionNow = BeaconRules.IsIgnitionNight(beacon);   // 한 프레임에 한 번만 계산
                if (LampLight(beacon.BeaconRepair, ignitionNow) != lampLight || ignitionNow != lampIgnition) RefreshTower();   // 성벽 수리로 점화의 밤이 되는 경우까지 따라간다
            }
            walking = Move(input, Time.unscaledDeltaTime);
            if (CanMove && keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) Nearest()?.Use?.Invoke();
            PlaceHero();
        }

        static float EdgeDistance(Vector2[] polygon, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[j], edge = polygon[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(1e-6f, edge.sqrMagnitude));
                best = Mathf.Min(best, (p - (a + edge * t)).magnitude);
            }
            return best;
        }

        static bool InsidePolygon(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y)
                    && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
            return inside;
        }

        public bool CanStand(Vector2 point)
        {
            if (!PrepYardProjection.Walkable(mask, point)) return false;
            foreach (var block in blocks) if (InsidePolygon(block, point) || EdgeDistance(block, point) < BodyRadius) return false;   // K-120 건물·탑 자리
            Vector3 position = world.transform.position + new Vector3(point.x, 0f, point.y);
            foreach (Collider obstacle in obstacles)
                if (Physics.ComputePenetration(body, position, Quaternion.identity, obstacle, obstacle.transform.position,
                    obstacle.transform.rotation, out _, out _)) return false;
            return true;
        }

        // 작은 간격으로 검사해 낮은 프레임에서도 장애물을 건너뛰지 않고 벽을 따라 미끄러진다.
        public bool Move(Vector2 input, float seconds)
        {
            if (!CanMove || seconds <= 0f || input.sqrMagnitude < 0.001f) return false;
            input = Vector2.ClampMagnitude(input, 1f); facing = input;
            Vector2 before = ground, delta = input * WalkSpeed * Mathf.Min(seconds, 0.25f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.04f)); delta /= steps;
            for (int i = 0; i < steps; i++)
            {
                Vector2 p = ground + new Vector2(delta.x, 0f); if (CanStand(p)) ground = p;
                p = ground + new Vector2(0f, delta.y); if (CanStand(p)) ground = p;
            }
            return (ground - before).sqrMagnitude > 0.000001f;
        }

        public bool SetGround(Vector2 position)
        {
            if (!CanMove || !CanStand(position)) return false;
            ground = position; walking = false; PlaceHero(); return true;
        }

        public Station Nearest()
        {
            Vector2 pixel = HeroPixel;
            return stations.Find(s => new Rect(s.Place.rect[0], s.Place.rect[1], s.Place.rect[2], s.Place.rect[3]).Contains(pixel));
        }

        // 시험용 바로가기. 일반 플레이는 장소까지 이동한 뒤 E를 누른다.
        public bool Use(string stationName)
        {
            if (!CanMove) return false;
            Station station = stations.Find(s => s.Name == stationName);
            if (station == null || !SetGround(PrepYardProjection.Ground(new Vector2(station.Place.x, station.Place.y)))) return false;
            station.Use?.Invoke(); return true;
        }

        public void AskNextBattle(Action start)
        {
            if (confirm != null) return;
            confirm = Ui.Confirm(transform.parent, "성벽 위로 올라갈까요?", "다음 전투가 바로 시작됩니다",
                "다음 전투 시작", "출정", () => { confirm = null; start?.Invoke(); },
                "더 둘러보기", "더 둘러보기", () => confirm = null);
        }

        static void Quad(Mesh mesh, Vector3 right, Vector3 up, Vector3 origin, float width, float height, float pivot)
        {
            mesh.vertices = new[] { origin - right * width / 2f - up * height * pivot,
                origin + right * width / 2f - up * height * pivot,
                origin + right * width / 2f + up * height * (1f - pivot), origin - right * width / 2f + up * height * (1f - pivot) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
        }

        void PlaceHero()
        {
            Vector2 pixel = HeroPixel;
            float height = viewport.rect.height > 1f ? viewport.rect.height : 1080f, scale = height / 1080f;
            float width = viewport.rect.width > 1f ? viewport.rect.width / scale : 1920f;
            strip.localScale = Vector3.one * scale;
            strip.anchoredPosition = new Vector2(Mathf.Clamp(width / 2f - pixel.x, Mathf.Min(0f, width - 3840f), 0f) * scale, 0f);
            body.transform.localPosition = new Vector3(ground.x, 0f, ground.y);
            foreach (var o in overlays) o.image.enabled = pixel.y <= o.data.row;
            Station near = Nearest(); prompt.gameObject.SetActive(CanMove && near != null);
            if (near != null) prompt.anchoredPosition = new Vector2(near.X, 1080f - near.Place.y + 90f);
            Texture2D texture = heroSheet;
            int row = Mathf.Abs(facing.y) > Mathf.Abs(facing.x) ? (facing.y > 0f ? 0 : 3) : 1;
            row = Mathf.Min(row, rows - 1);
            int column = walking ? (int)(Time.unscaledTime * 8f) % columns : 0, cols = columns, rowCount = rows;
            float localFoot = foot, localWidth = cellWidth, localFigure = figure;
            if (!walking && facing.y > 0f && idleSheet != null)
            {
                texture = idleSheet; cols = rowCount = 1; column = row = 0;
                localWidth = idleSheet.width; localFoot = localWidth == 288f ? 360f : 352f; localFigure = localWidth == 288f ? 219f : 300f;
            }
            if (texture == null) return;
            bool flip = row == 1 && facing.x > 0f;
            Vector4 uv = new Vector4((column + (flip ? 1 : 0)) / (float)cols,
                1f - (row + 1f) / rowCount, (flip ? -1f : 1f) / cols, 1f / rowCount);
            heroMaterial.SetTexture("_BaseMap", texture); heroMaterial.SetVector("_UVRect", uv);
            shadowMaterial.SetTexture("_BaseMap", texture); shadowMaterial.SetVector("_UVRect", uv);
            float cellHeight = texture.height / (float)rowCount;
            float h = cellHeight * 1.7f / localFigure, w = localWidth * 1.7f / localFigure, pivot = (cellHeight - localFoot) / cellHeight;
            Vector3 origin = new Vector3(ground.x, 0f, ground.y);
            Quad(heroMesh, yardCamera.transform.right, yardCamera.transform.up, origin, w, h, pivot);
            // 바닥에 놓인 동적 실루엣. 배경에 이미 그려진 고정 그림자는 그대로 둔다.
            Quad(shadowMesh, Vector3.right, new Vector3(0.32f, 0f, 0.62f), origin + Vector3.up * 0.012f, w, h, pivot);
        }

        void OnDestroy()
        {
            if (yardCamera != null) yardCamera.targetTexture = null;
            if (target != null) target.Release();
            if (world != null) Destroy(world);
            foreach (var obj in owned) if (obj != null) Destroy(obj);
        }
    }
}
