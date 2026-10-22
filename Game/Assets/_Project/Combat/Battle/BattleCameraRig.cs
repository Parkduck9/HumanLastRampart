using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.Battle
{
    // 1번 화면 카메라와 주인공 이동 (D-025·D-040·D-046, D-061 옥토패스식 이동·카메라)
    // - A/D(←/→): 성벽을 따라 늘 달린다. 끝까지 가면 모서리를 돌아 옆 성벽으로 이어서 간다 (카메라는 부드럽게 돈다)
    // - W/S(↑/↓): 성벽 통로 안에서 바깥쪽/안쪽으로 걷는다 (뒷모습/앞모습)
    // - C 카메라 방식 바꾸기. Tab은 지도(BattleEntry, D-071) — 예전 3D 전체 보기는 시험 도구 비교용으로만 남김
    // - Q/E 자동 달리기 키는 없앴다 (D-069 사용자 지시: A/D 누르고 있는 동안만 달리기, 모서리 돌기는 그대로)
    // 기본은 자동: 멈춰 있으면 A(가깝게), 움직이면 B(조금 넓게)로 부드럽게 바뀐다
    public sealed class BattleCameraRig
    {
        const float RunSpeed = BattleSim.HeroSpeed;          // 늘 달리기 (D-061) — 성벽 한 면 40m 약 8초 (D-071)
        const float DepthSpeed = BattleSim.HeroWalkSpeed;    // 통로 안쪽·바깥쪽 걷기
        const float TurnDegreesPerSecond = 300f;             // 모서리에서 카메라가 도는 빠르기 (90도 약 0.3초)
        const float FollowSharpness = 14f;                   // 카메라가 주인공을 따라붙는 빠르기

        // 주인공 뒤 위에서 바깥 들판을 보는 카메라 후보. 뒤로 = 주인공 발에서 뒤로 거리, 위로 = 성벽 윗면에서 높이
        public struct ViewPreset
        {
            public string Name;
            public float Back;
            public float Up;
            public float Pitch;
        }

        public static readonly ViewPreset[] Presets =
        {
            // 사용자 지시 (2026-10-16): 멈춤·걷기·달리기 모두 옥토패스 참고 사진 정도로 뒤로 — 인물 키가 화면 높이의 약 13% (예전 A는 약 37%)
            new ViewPreset { Name = "A 기본", Back = 3.0f, Up = 2.1f, Pitch = 24f },
            new ViewPreset { Name = "B 움직일 때", Back = 3.2f, Up = 2.25f, Pitch = 25f },
            new ViewPreset { Name = "C 높게", Back = 3.2f, Up = 2.75f, Pitch = 28f },
        };

        public const float NearClip = 0.05f;
        public const float HeroInset = 1f;      // 주인공 처음 자리: 성벽 바깥 모서리에서 1m 안쪽 (병사 줄 0.5m 바로 뒤)
        public const float InsetMin = 0.75f;    // 통로 바깥쪽 끝 (병사 줄 바로 뒤)
        public const float InsetMax = 1.8f;     // 통로 안쪽 끝 (성벽 두께 2m)

        // 주인공 모습 (그리기용). 서 있으면 뒷모습, 옆으로 달리기, 안쪽·바깥쪽 걷기는 앞·뒷모습
        public enum HeroPose { Idle, Walk, Run }
        public HeroPose Pose { get; private set; }
        public bool FacingRight { get; private set; }
        public bool FacingFront { get; private set; }   // 안쪽(카메라 쪽)으로 걸으면 앞모습
        public float WalkInput { get; set; }    // 키보드 대신 좌우 입력(-1~1) — 시험·나중 패드용
        public float DepthInput { get; set; }   // 키보드 대신 앞뒤 입력(+1 바깥쪽, -1 안쪽)

        // 가운데 등대 탑 (W9, Concept_Beacon_v1 9.7 4차 결정): 계단은 북쪽 성벽 통로 가운데의 안쪽 끝 (마당이 성벽 윗면과 같은 높이라 '내려간다'는 이음 연출).
        // 이유: 주인공 이동 모델은 (성벽 번호, 따라 along, 통로 안쪽 inset)뿐이라, 정면(북쪽)의 가운데 한 점이 가장 단순하고 모든 전투 지도에 있다.
        // 내려가는 동안 발 위치를 성벽에서 탑 앞까지 부드럽게 옮기고(TowerSeconds), 탑에 있는 동안은 성벽을 비운 것 (OnWall = false)
        public const int StairsSide = 0;
        public const float StairsReach = 3f;            // 계단 가운데에서 이 거리(m) 안이고
        public const float StairsInsetMin = InsetMax - 0.5f;   // 통로 안쪽 끝 가까이면 E로 내려갈 수 있다
        public const float TowerSeconds = 1.6f;         // 계단을 오르내리는 시간
        public const float TowerHeroBack = 0.8f;        // 탑 앞에서 서는 자리: 탑 가운데에서 남쪽(사진기 쪽)으로

        float towerBlend;       // 0 = 성벽 위, 1 = 탑 앞
        bool towerTarget;       // 가려는 곳 (true = 탑)

        public bool TowerTarget => towerTarget;
        public bool AtTower => towerTarget && towerBlend >= 1f;
        public bool InTransition => towerTarget ? towerBlend < 1f : towerBlend > 0f;
        public bool OnWall => !towerTarget && towerBlend <= 0f;        // 성벽을 지키고 있는가. 내려가려는 순간부터 오르는 걸 마칠 때까지 false
        public float TowerBlend => towerBlend;
        public Vector3 TowerFeet => new Vector3(0f, wallHeight, -TowerHeroBack);
        public bool NearStairs => Side == StairsSide && Mathf.Abs(along) <= StairsReach && inset >= StairsInsetMin;

        // E: 계단 가까이서 탑으로 내려가기. 가까이 있지 않으면 false
        public bool GoDown()
        {
            if (towerTarget || towerBlend > 0f || !NearStairs) return false;
            towerTarget = true;
            runDir = 0;
            return true;
        }

        // 탑에서 성벽으로 올라가기 (내려가는 중에도 되돌릴 수 있다)
        public bool GoUp()
        {
            if (!towerTarget) return false;
            towerTarget = false;
            return true;
        }

        public Vector3 HeroFeet
        {
            get
            {
                Vector3 wall = FeetAt(Side, along, inset);
                return towerBlend <= 0f ? wall : Vector3.Lerp(wall, TowerFeet, Mathf.SmoothStep(0f, 1f, towerBlend));
            }
        }

        public float Inset => inset;

        // 옆 성벽 가운데까지 자동으로 달린다 (D-046). 키 연결은 D-069로 없앴고 시험 도구만 부른다. A/D를 누르면 멈춘다
        int runDir;
        bool runTurned;
        public bool AutoRunning => runDir != 0;

        const float BlendSeconds = 0.3f;
        const float ReturnDelaySeconds = 0.6f;

        public int PresetIndex { get; private set; } = -1;
        public string PresetName => PresetIndex < 0 ? "자동 A↔B" : Presets[PresetIndex].Name;
        public float MoveBlend => moveBlend;

        float moveBlend;
        float sinceMoved = 999f;

        readonly Camera camera;
        readonly float castleHalf;
        readonly float wallHeight;
        float along;
        float inset = HeroInset;
        bool overview;
        float yaw;                 // 지금 카메라가 보는 방향(도) — 모서리에서 부드럽게 돈다
        Vector3 smoothedFeet;
        bool snap = true;

        public int Side { get; private set; }
        public float Along => along;
        public bool Overview => overview;
        public float Yaw => yaw;

        public BattleCameraRig(Camera camera, float castleHalf, float wallHeight)
        {
            this.camera = camera;
            this.castleHalf = castleHalf;
            this.wallHeight = wallHeight;
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = NearClip;
            camera.farClipPlane = 1200f;
            yaw = SideYaw(Side);
            Apply(0f);
        }

        Vector3 FeetAt(int side, float alongAt, float insetAt)
        {
            var n = WallGeometry.Outward(side);
            var r = WallGeometry.Right(side);
            return new Vector3(n.x, 0f, n.z) * (castleHalf - insetAt) + new Vector3(r.x, 0f, r.z) * alongAt + Vector3.up * wallHeight;
        }

        static float SideYaw(int side)
        {
            var n = WallGeometry.Outward(side);
            return Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
        }

        // 주인공이 갈 수 있는 성벽 따라 끝: 모서리 안쪽 점에서 옆 성벽과 이어진다
        float Limit => castleHalf - inset;

        public void Update(float unscaledDt)
        {
            var keyboard = Keyboard.current;
            float pan = 0f, depth = 0f;
            if (keyboard != null)
            {
                if (keyboard.cKey.wasPressedThisFrame) PresetIndex = PresetIndex + 1 >= Presets.Length ? -1 : PresetIndex + 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) depth += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) depth -= 1f;
            }

            // 탑으로 내려가는 중·탑 앞·올라오는 중에는 성벽 이동 입력을 받지 않는다 (W9)
            float towerBefore = towerBlend;
            towerBlend = Mathf.MoveTowards(towerBlend, towerTarget ? 1f : 0f, unscaledDt / TowerSeconds);
            if (towerTarget || towerBlend > 0f)
            {
                runDir = 0;
                bool walking = !Mathf.Approximately(towerBefore, towerBlend);
                Pose = walking ? HeroPose.Walk : HeroPose.Idle;
                FacingFront = walking && towerTarget;   // 내려갈 때는 사진기 쪽으로 걸어오는 앞모습, 올라갈 때는 뒷모습
                Step(walking, unscaledDt);
                return;
            }

            if (pan == 0f) pan = Mathf.Clamp(WalkInput, -1f, 1f);
            if (depth == 0f) depth = Mathf.Clamp(DepthInput, -1f, 1f);
            if (pan != 0f || depth != 0f) runDir = 0;

            float alongBefore = along, insetBefore = inset;
            int sideBefore = Side;
            if (runDir != 0)
            {
                AutoRun(unscaledDt);
            }
            else
            {
                if (pan != 0f) MoveAlong(pan * RunSpeed * unscaledDt);
                if (depth != 0f) SetInset(inset - depth * DepthSpeed * unscaledDt);
            }

            bool sideways = !Mathf.Approximately(alongBefore, along) || sideBefore != Side;
            bool deeper = !Mathf.Approximately(insetBefore, inset);
            if (runDir != 0 || sideways)
            {
                Pose = HeroPose.Run;
                FacingRight = runDir != 0 ? runDir > 0 : pan > 0f;
            }
            else if (deeper)
            {
                Pose = HeroPose.Walk;
                FacingFront = depth < 0f;
            }
            else
            {
                Pose = HeroPose.Idle;
                FacingFront = false;
            }

            Step(sideways || deeper, unscaledDt);
        }

        // 성벽 따라 움직이기. 끝을 넘으면 모서리를 돌아 옆 성벽으로 (+ = 바깥을 볼 때 오른쪽)
        void MoveAlong(float delta)
        {
            along += delta;
            float limit = Limit;
            if (along > limit)
            {
                float over = along - limit;
                Side = (Side + 1) % 4;
                along = -limit + over;
            }
            else if (along < -limit)
            {
                float over = -limit - along;
                Side = (Side + 3) % 4;
                along = limit - over;
            }
        }

        void SetInset(float value)
        {
            float next = Mathf.Clamp(value, InsetMin, InsetMax);
            // 모서리 근처에서 안쪽으로 들어가면 끝이 줄어든다 — 끝 안에 머문다
            inset = next;
            along = Mathf.Clamp(along, -Limit, Limit);
        }

        // dir -1 = 왼쪽 성벽(Q), +1 = 오른쪽 성벽(E). 달리는 중에 다시 누르면 지금 자리에서 그 방향 다음 성벽으로
        public void RunToNextWall(int dir)
        {
            runDir = dir < 0 ? -1 : 1;
            runTurned = false;
            overview = false;
        }

        void AutoRun(float dt)
        {
            float step = RunSpeed * dt;
            if (!runTurned)
            {
                int sideBefore = Side;
                MoveAlong(runDir * step);
                if (Side != sideBefore) runTurned = true;
                return;
            }

            along = Mathf.MoveTowards(along, 0f, step);
            if (Mathf.Approximately(along, 0f)) runDir = 0;
        }

        public void Step(bool moved, float unscaledDt)
        {
            sinceMoved = moved ? 0f : sinceMoved + unscaledDt;
            float target = sinceMoved < ReturnDelaySeconds ? 1f : 0f;
            moveBlend = Mathf.MoveTowards(moveBlend, target, unscaledDt / BlendSeconds);
            Apply(unscaledDt);
        }

        public void SetPreset(int index)
        {
            PresetIndex = Mathf.Clamp(index, -1, Presets.Length - 1);
            snap = true;
            Apply(0f);
        }

        public void ShowSide(int side)
        {
            Side = side;
            along = 0f;
            inset = HeroInset;
            overview = false;
            runDir = 0;
            towerTarget = false;
            towerBlend = 0f;
            snap = true;
            Apply(0f);
        }

        void Apply(float dt)
        {
            float targetYaw = SideYaw(Side);
            Vector3 feet = HeroFeet;
            if (snap || dt <= 0f)
            {
                yaw = targetYaw;
                smoothedFeet = feet;
                snap = false;
            }
            else
            {
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, TurnDegreesPerSecond * dt);
                smoothedFeet = Vector3.Lerp(smoothedFeet, feet, 1f - Mathf.Exp(-FollowSharpness * dt));
            }

            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = heading * Vector3.forward;
            if (overview)
            {
                // 성 전체를 위에서 비스듬히 (지도 모드 D-025의 임시 대용)
                camera.transform.SetPositionAndRotation(-forward * castleHalf * 2.2f + Vector3.up * castleHalf * 3.2f, heading * Quaternion.Euler(60f, 0f, 0f));
                return;
            }

            ViewPreset p = PresetIndex >= 0 ? Presets[PresetIndex] : Blend(Presets[0], Presets[1], Mathf.SmoothStep(0f, 1f, moveBlend));
            Vector3 position = smoothedFeet - forward * p.Back + Vector3.up * p.Up;
            camera.transform.SetPositionAndRotation(position, heading * Quaternion.Euler(p.Pitch, 0f, 0f));
            ApplyShake(dt);
        }

        // ── 화면 흔들림 (K-041 마법·함정 폭발) ── 효과 쪽(BattleEffectsView)이 부르고, 카메라 자리를 정한 뒤 위에 얹기만 한다
        const float ShakeFalloff = 90f;          // 이 거리(m) 밖 폭발은 흔들지 않는다
        const float ShakeDecay = 3.5f;           // 1초에 세기가 줄어드는 양
        const float ShakeMeters = 0.1f;          // 세기 1일 때 최대 흔들림 (카메라가 주인공 1m 뒤라 몇 cm만 흔들려도 보인다)
        static float shakePending;
        static Vector3 shakeAt;
        float shake;

        public static void Shake(float strength, Vector3 at)
        {
            if (!HumanLastRampart.Core.Settings.GameSettings.CameraShake) return;   // 설정 창에서 끔 (K-062)
            if (strength <= shakePending) return;
            shakePending = strength;
            shakeAt = at;
        }

        void ApplyShake(float dt)
        {
            if (shakePending > 0f)
            {
                float distance = Vector3.Distance(camera.transform.position, shakeAt);
                shake = Mathf.Max(shake, shakePending * Mathf.Clamp01(1f - distance / ShakeFalloff));
                shakePending = 0f;
            }

            if (shake <= 0.001f || overview) return;
            shake = Mathf.Max(0f, shake - ShakeDecay * dt);
            float t = Time.unscaledTime * 38f;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * ShakeMeters * shake);
            camera.transform.position += camera.transform.rotation * offset;
        }

        static ViewPreset Blend(ViewPreset a, ViewPreset b, float t) => new ViewPreset
        {
            Name = a.Name,
            Back = Mathf.Lerp(a.Back, b.Back, t),
            Up = Mathf.Lerp(a.Up, b.Up, t),
            Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t),
        };
    }
}
