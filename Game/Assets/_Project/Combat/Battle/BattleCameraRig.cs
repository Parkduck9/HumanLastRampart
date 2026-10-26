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

        // 가운데 등대 탑 (W9, Concept_Beacon_v1 9.7 4차 결정): 계단은 네 성벽 모두 통로 가운데의 안쪽 끝 (W35, 사용자 2026-10-23 "4군데에서 다 갈 수 있어야해").
        // 마당이 성벽 윗면과 같은 높이라 '내려간다'는 이음 연출. 주인공 이동 모델은 (성벽 번호, 따라 along, 통로 안쪽 inset)뿐이라, 면마다 가운데 한 점이 가장 단순하다.
        // 내려가는 동안 발 위치를 성벽에서 탑 앞까지 거리에 맞춰 옮기고(평지 HeroSpeed · 계단 HeroWalkSpeed), 탑에 있는 동안은 성벽을 비운 것 (OnWall = false)
        // 탑에서는 W 북 · A 서 · D 동 · S 남 성벽으로 올라간다 (사진기가 늘 북쪽을 보므로 화면 위·왼·오른·아래와 같다)
        public const int StairsSide = 0;                // 기준 면(북): 계단·길의 자기 좌표 기준이자 옛 이름. 계단은 0~3 모두에 있다
        public const float StairsReach = 3f;            // 계단 가운데에서 이 거리(m) 안이고
        public const float StairsInsetMin = InsetMax - 0.5f;   // 통로 안쪽 끝 가까이면 E로 내려갈 수 있다
        public const float TowerHeroBack = 1.45f;       // 탑 앞(문 앞)에서 서는 자리: 탑 가운데에서 남쪽(문 쪽)으로. 높은 탑(W20)의 아래 반폭 1.1m 밖
        public const float TripSecondsMax = 5f;         // 한쪽 길(성벽 ↔ 탑)은 길어도 이 시간(게임 시간) 안에 — 걷기·계단 모습은 그대로 두고 속도만 올린다 (사용자 2026-10-23)
        public const float NorthLaneZ = 1.6f;           // 북쪽 길: 이보다 북쪽이면 사진기가 남쪽을 봐 탑 몸체 안에 들어가지 않는다 (탑 쪽이면 북쪽)
        public const float TowerYawRadius = 9f;        // 탑에서 이만큼(m) 안에서는 사진기가 북쪽(문 쪽)을 본다. 밖에서는 길이 닿는 성벽의 바깥을 본다 (사진기가 탑 몸체에 들어가지 않게)
        const float TowerFollowSharpness = 30f;         // 길을 오르내릴 때 카메라가 발을 따라붙는 빠르기 (평소 14)

        // 길 위 상태: 성벽에서 탑 앞까지 걸어야 하는 시간(pathTotal) 중 얼마나 걸었나(pathTime). 0 = 성벽 위, pathTotal = 탑 앞
        float pathTime;
        float pathTotal;
        float pathScale = 1f;         // 길이 길면 속도를 이만큼 곱해 TripSecondsMax 안에 닿는다
        bool waitRelease;             // 올라온 뒤 누르고 있던 이동 키는 뗄 때까지 무시
        int pathSide;                 // 지금 길이 닿는 성벽
        bool arriveMiddle;            // 올라가면 그 성벽 가운데에 선다 (false = 내려온 자리 그대로)
        bool towerTarget;             // 가려는 곳 (true = 탑)
        readonly Vector3[] pathPts = new Vector3[PathMax];
        readonly float[] pathSpeeds = new float[PathMax];
        int pathCount;
        const int PathMax = 8;

        public bool TowerTarget => towerTarget;
        public bool AtTower => towerTarget && pathTotal > 0f && pathTime >= pathTotal;
        public bool InTransition => towerTarget ? pathTime < pathTotal : pathTime > 0f;
        public bool OnWall => !towerTarget && pathTime <= 0f;        // 성벽을 지키고 있는가. 내려가려는 순간부터 오르는 걸 마칠 때까지 false
        public float TowerBlend => pathTotal > 0f ? Mathf.Clamp01(pathTime / pathTotal) : 0f;   // 0 = 성벽 위, 1 = 탑 앞 (시간 기준)
        public int PathSide => pathSide;
        public float PathSeconds => pathTotal;
        // 성벽(계단)에서 탑 문 앞까지 가는 길 (W20): 탑 몸체를 뚫지 않게 탑 옆(±SideLane)을 돌아 문 앞 줄로 들어간다.
        public const float SideLane = 2.2f;   // BattleTowerView.BaseHalf(1.1) + 1.1
        // W26·W35: 길은 의식 돌길(BattleRoad)을 따라간다 — 성벽 위에서 길 가운데로 옆걸음 → 계단(걷기) → 돌길 직진 → 광장 가장자리 → (북쪽 면만 탑 옆을 돌고 동·서는 비스듬히 문 줄로) → 문 앞.
        // 꼭짓점은 모두 탑 몸체(반폭 1.1 + 받침 0.15) 밖이고 선분도 몸체를 지나지 않는다
        public const float PlazaEntryZ = 2.7f;   // 광장 가장자리 안 (탑 가운데에서 그 성벽 쪽으로)
        public const float PlazaWalkZ = 2.3f;

        // 길 꼭짓점과 구간 속도를 채운다. pts[0] = 성벽 발 자리, 구간 i(pts[i-1] → pts[i])의 속도 spd[i]. 걸리는 시간(초)을 돌려준다
        float BuildPath(int side, Vector3 wall, Vector3[] pts, float[] spd, out int count)
        {
            Vector3 end = TowerFeet;
            float y = wall.y;
            var n = WallGeometry.Outward(side);
            var r = WallGeometry.Right(side);
            Vector3 nv = new Vector3(n.x, 0f, n.z), rv = new Vector3(r.x, 0f, r.z);
            float aWall = Vector3.Dot(wall, nv), lWall = Vector3.Dot(wall, rv);
            float aFoot = Mathf.Min(aWall, castleHalf - BattleCourtyard.WallThickness - BattleCourtyard.StepCount * BattleCourtyard.TreadDepth);   // 계단 맨 아래
            Vector3 On(float a) => nv * a + Vector3.up * y;

            pts[0] = wall;
            pts[1] = On(aWall);                                  // 계단 가운데 줄로 옆걸음 (달리기)
            pts[2] = On(aFoot);                                  // 계단 내려가기 (걷기)
            pts[3] = On(PlazaEntryZ);                            // 돌길을 곧장 달려 광장 가장자리
            count = 4;
            if (side == 0)
            {
                float sx = lWall >= 0f ? 1f : -1f;               // 북쪽 면의 오른쪽(+x)·왼쪽(-x) 중 가까운 쪽으로 돈다
                pts[count++] = new Vector3(sx * SideLane, y, PlazaWalkZ);   // 광장에서 탑 옆으로 돌기
                pts[count++] = new Vector3(sx * SideLane, y, end.z);        // 탑 옆 · 문 앞 줄
            }
            else if (side == 1 || side == 3)
            {
                pts[count++] = new Vector3((side == 1 ? 1f : -1f) * SideLane, y, end.z);   // 탑 옆 · 문 앞 줄로 비스듬히
            }

            pts[count++] = end;
            float total = 0f;
            for (int i = 1; i < count; i++)
            {
                spd[i] = i == 2 ? BattleSim.HeroWalkSpeed : BattleSim.HeroSpeed;   // 계단만 걷고 나머지 평지는 성벽 달리기 속도
                total += Vector3.Distance(pts[i - 1], pts[i]) / spd[i];
            }

            return total;
        }

        static Vector3 SamplePath(Vector3[] pts, float[] spd, int count, float seconds)
        {
            float d = Mathf.Max(0f, seconds);
            for (int i = 1; i < count; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]) / spd[i];
                if (d <= seg || i == count - 1) return Vector3.Lerp(pts[i - 1], pts[i], seg > 1e-5f ? Mathf.Clamp01(d / seg) : 1f);
                d -= seg;
            }

            return pts[count - 1];
        }

        // 시험·확인용: side 성벽 wall 자리에서 문 앞까지 길 위 한 점 (t = 0..1, 걸리는 시간 기준). 상태는 건드리지 않는다
        public Vector3 TowerPath(int side, Vector3 wall, float t)
        {
            var pts = new Vector3[PathMax];
            var spd = new float[PathMax];
            float total = BuildPath(side, wall, pts, spd, out int count);
            return SamplePath(pts, spd, count, Mathf.Clamp01(t) * total);
        }

        // 걸리는 시간(초, TripSecondsMax로 자른 값)
        public float TowerPathSeconds(int side, Vector3 wall) => Mathf.Min(TripSecondsMax, BuildPath(side, wall, new Vector3[PathMax], new float[PathMax], out _));

        // 길을 새로 짜서 instance 배열에 담고, 걸리는 시간(자른 값)을 돌려준다. 원래 시간이 길면 pathScale이 1보다 커져 같은 길을 더 빨리 걷는다
        float Plan(int side, Vector3 wall)
        {
            float raw = BuildPath(side, wall, pathPts, pathSpeeds, out pathCount);
            pathScale = Mathf.Max(1f, raw / TripSecondsMax);
            return raw / pathScale;
        }

        public float PathSpeedScale => pathScale;

        // side 성벽의 (along, inset) 발 자리 (시험·확인용)
        public Vector3 WallFeet(int side, float alongAt, float insetAt) => FeetAt(side, alongAt, insetAt);

        public Vector3 TowerFeet => new Vector3(0f, wallHeight, -TowerHeroBack);
        public bool NearStairs => Mathf.Abs(along) <= StairsReach && inset >= StairsInsetMin;   // 네 성벽 모두 가운데에 계단 (W35)

        // E: 계단 가까이서 탑으로 내려가기. 가까이 있지 않으면 false
        public bool GoDown()
        {
            if (towerTarget || pathTime > 0f || !NearStairs) return false;
            pathSide = Side;
            pathTotal = Plan(pathSide, FeetAt(Side, along, inset));
            pathTime = 0f;
            arriveMiddle = false;
            towerTarget = true;
            runDir = 0;
            return true;
        }

        // 탑 오르내리는 시간 진행 (전투 시간 기준 — BattleEntry가 멈춤·지도 중에도 부른다: 지도를 열어도 내려가는 중에 얼어붙지 않는다). 움직였으면 true
        public bool TickTower(float dt)
        {
            float before = pathTime;
            pathTime = Mathf.MoveTowards(pathTime, towerTarget ? pathTotal : 0f, dt);
            if (!towerTarget && before > 0f && pathTime <= 0f && arriveMiddle)
            {
                // 다른 성벽(또는 같은 성벽 가운데)으로 올라왔다: 그 성벽 가운데에 서고 사진기는 그 성벽 바깥을 본다
                Side = pathSide;
                along = 0f;
                inset = HeroInset;
                arriveMiddle = false;
            }

            if (!towerTarget && before > 0f && pathTime <= 0f) waitRelease = true;   // 누르고 있던 이동 키는 뗄 때까지 무시
            return !Mathf.Approximately(before, pathTime);
        }

        // 탑에서 성벽으로 올라가기 (W35: W 북 0 · D 동 1 · S 남 2 · A 서 3). 올라간 성벽 가운데에 선다.
        // 내려가는 중이면 내려오던 성벽으로만 그 자리에서 되돌아 올라간다 (성벽에 닿을 때까지 OnWall은 false)
        public bool GoUp(int side)
        {
            if (!towerTarget || side < 0 || side > 3) return false;
            if (pathTime < pathTotal)
            {
                if (side != pathSide) return false;
                towerTarget = false;
                return true;
            }

            // 내려온 성벽의 키면 떠난 자리 그대로(단추와 같다), 다른 성벽이면 그 성벽 가운데
            bool back = side == Side;
            pathSide = side;
            pathTotal = Plan(side, back ? FeetAt(Side, along, inset) : FeetAt(side, 0f, HeroInset));
            pathTime = pathTotal;
            arriveMiddle = !back;
            towerTarget = false;
            return true;
        }

        // 왔던 성벽의 내려온 자리로 올라가기 (명령 판 [성벽으로 올라가기]·시험)
        public bool GoUp()
        {
            if (!towerTarget) return false;
            if (pathTime >= pathTotal)
            {
                pathSide = Side;
                pathTotal = Plan(Side, FeetAt(Side, along, inset));
                pathTime = pathTotal;
                arriveMiddle = false;
            }

            towerTarget = false;
            return true;
        }

        public Vector3 HeroFeet
        {
            get
            {
                if (!towerTarget && pathTime <= 0f) return FeetAt(Side, along, inset);
                return SamplePath(pathPts, pathSpeeds, pathCount, pathTime * pathScale);
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

        // towerDt = 탑 오르내리기에 쓰는 전투 시간(배속 반영, 멈춤·지도 시간 멈춤이면 0). 생략하면 unscaledDt (시험)
        public void Update(float unscaledDt, float towerDt = -1f)
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
            Vector3 feetBefore = HeroFeet;
            bool walking = TickTower(towerDt >= 0f ? towerDt : unscaledDt);
            if (towerTarget || pathTime > 0f)
            {
                runDir = 0;
                TowerPose(walking, HeroFeet - feetBefore);
                Step(walking, unscaledDt);
                return;
            }

            if (waitRelease)
            {
                if (pan == 0f && depth == 0f) waitRelease = false;   // 탑에서 누른 키를 뗐다
                else pan = depth = 0f;
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

        // 길 위 모습 (W35): 사진기 기준으로 가는 쪽에 맞춘다 — 멀어지면 뒷모습, 다가오면 앞모습, 옆으로 가면 옆모습 달리기
        void TowerPose(bool walking, Vector3 delta)
        {
            delta.y = 0f;
            if (!walking || delta.sqrMagnitude < 1e-10f)
            {
                Pose = HeroPose.Idle;
                FacingFront = false;
                return;
            }

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 dir = delta.normalized;
            float ahead = Vector3.Dot(dir, forward), sideways = Vector3.Dot(dir, right);
            if (Mathf.Abs(sideways) > Mathf.Abs(ahead))
            {
                Pose = HeroPose.Run;
                FacingRight = sideways > 0f;
            }
            else
            {
                Pose = HeroPose.Walk;
                FacingFront = ahead < 0f;   // 사진기 쪽으로 걸어오면 앞모습
            }
        }

        // 사진기가 보려는 방향(도): 성벽 위에서는 그 성벽 바깥. 길 위에서는 탑에서 멀면 길이 닿는 성벽 바깥, 가까우면(탑 앞 포함) 북쪽 (문이 남쪽이라 문과 탑이 보인다)
        float TargetYaw(Vector3 feet)
        {
            if (!towerTarget && pathTime <= 0f) return SideYaw(Side);
            if (AtTower) return 0f;
            if (pathSide == 0)
            {
                // 북쪽 길: 사진기가 늘 주인공 남쪽(탑 쪽)에 있으면 광장 가까이에서 탑 몸체 안에 들어간다 → 길 위(탑에서 9m 안)에서는 남쪽을 봐 사진기가 북쪽에 선다
                if (feet.x * feet.x + feet.z * feet.z > TowerYawRadius * TowerYawRadius) return 0f;
                return feet.z > NorthLaneZ ? 180f : 0f;
            }

            return feet.x * feet.x + feet.z * feet.z > TowerYawRadius * TowerYawRadius ? SideYaw(pathSide) : 0f;
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
            pathTime = 0f;
            pathTotal = 0f;
            arriveMiddle = false;
            snap = true;
            Apply(0f);
        }

        void Apply(float dt)
        {
            Vector3 feet = HeroFeet;
            float targetYaw = TargetYaw(feet);
            if (snap || dt <= 0f)
            {
                yaw = targetYaw;
                smoothedFeet = feet;
                snap = false;
            }
            else
            {
                // 북쪽 길의 180도 돌기는 탑 몸체 옆(바깥쪽)으로 돈다: 안쪽으로 돌면 사진기가 탑을 지나간다
                if ((towerTarget || pathTime > 0f) && pathSide == 0 && Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) > 100f) targetYaw = feet.x >= 0f ? 270f : 90f;
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, TurnDegreesPerSecond * dt);
                // 길을 오르내리는 동안(W35)은 거리에 맞춘 속도(초속 5m 안팎)라 발을 그대로 따라갈 필요는 없고, 조금 더 바짝 따라붙기만 한다
                float sharpness = towerTarget || pathTime > 0f ? TowerFollowSharpness : FollowSharpness;
                smoothedFeet = Vector3.Lerp(smoothedFeet, feet, 1f - Mathf.Exp(-sharpness * dt));
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
