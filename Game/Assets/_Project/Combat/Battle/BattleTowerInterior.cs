using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 등대 탑 아래층 속 (W27, 사용자 요청: 일꾼은 탑 밖이 아니라 안에서 돌림대를 민다)
    // - 열린 문 구멍 안: 가운데 큰 돌림대(캡스턴) + 안쪽 벽 따라 오르는 나선 돌계단 + 기름통·석탄 자루·연장
    // - 일꾼 셋: 빛줄기가 도는 동안 돌림대 둘레를 돌며 민다. 쉬는 동안 한 명이 안쪽 계단을 올라 불방에 갔다 오는 길을 되풀이한다
    // - 이 클래스는 좌표·길·메시 만들기만 한다 (순수 계산). 그리기는 BattleTowerView(정적 소품)·BattleBeaconView(돌림대·일꾼)가 한다
    // 좌표는 탑 가운데·마당 윗면 기준(탑 로컬): x = 동, z = 북(문은 남쪽 -z), y = 마당 윗면 위 높이
    public static class BattleTowerInterior
    {
        public const float FloorY = 0.14f;                                           // 받침 윗면 = 아래층 바닥
        public const float WallThickness = 0.22f;
        public const float InnerHalf = BattleTowerView.BaseHalf - WallThickness;     // 방 안쪽 반폭 0.88
        public const float OpeningHalf = 0.41f;                                      // 열린 문 구멍 반폭 (문 그림 구멍 38화소)
        public const float OpeningHeight = 2.05f;                                    // 바닥 위 구멍 높이
        public const float OpeningTop = FloorY + OpeningHeight;

        // 돌림대: 가운데 기둥 + 위로 올라가는 축 + 십자 막대. 일꾼은 막대 끝 안쪽 WorkerOrbit 반지름 원 위를 걷는다
        public const float CapstanDrumRadius = 0.2f;
        public const float CapstanBarHalf = 0.5f;
        // 막대 높이 = K-127 일꾼 시트의 손 높이 (발 위 원본 35화소 = 게임 105화소, 1화소 = Mpp). 옛 K-110 미는 그림은 0.28m였다 (W39)
        public static readonly float CapstanBarY = 105f * BattleBeaconView.Mpp;
        public const float WorkerOrbit = 0.4f;
        public const float PushDegreesPerSecond = 28f;       // 미는 동안 도는 빠르기 (일꾼 걸음 약 0.28m/s)
        public const int WorkerCount = 3;
        public const int ClimberIndex = 2;

        // 나선 계단: 서쪽 벽(남 → 북)을 따라 올라 북쪽 벽(서 → 동)으로 꺾인다. 맨 위는 천장 밑
        public const int StairSteps = 14;
        const float StairWidth = 0.36f;
        const float StairLine = InnerHalf - StairWidth * 0.5f;   // 벽에서 계단 가운데까지 (0.70)
        const float LegOneStart = -0.55f, LegOneEnd = StairLine;                 // 서쪽 벽 줄: z
        const float LegTwoStart = -StairLine, LegTwoEnd = 0.1f;                  // 북쪽 벽 줄: x
        const float LegOneLength = LegOneEnd - LegOneStart;
        const float StairLength = LegOneLength + (LegTwoEnd - LegTwoStart);
        public static readonly float StairTopY = BattleTowerView.GroundHeight - 0.22f;
        public static readonly float StairRise = (StairTopY - FloorY) / StairSteps;

        // 불방 안 일꾼 자리 (불꽃 가운데에서 한 걸음 옆 앞쪽)
        static readonly Vector3 LampStand = new Vector3(-0.32f, BattleTowerView.LampBase, -0.18f);
        static readonly Vector3 LampHatch = new Vector3(0f, BattleTowerView.LampBase, 0.2f);

        // 계단 오르내리는 한 바퀴 (전투 시간 초): 기다림 → 계단 발치 → 오름 → (벽 속) → 불방 → 돌봄 → 돌아섬 → (벽 속) → 내림 → 제자리
        const float WaitSeconds = 5f, ToFootSeconds = 2f, ClimbSeconds = 5.5f, UpHiddenSeconds = 3.5f, EnterSeconds = 1f, TendSeconds = 3f, LeaveSeconds = 1f, DownHiddenSeconds = 3.5f, DescendSeconds = 5.5f, ToStationSeconds = 2f;
        public const float ClimbCycleSeconds = WaitSeconds + ToFootSeconds + ClimbSeconds + UpHiddenSeconds + EnterSeconds + TendSeconds + LeaveSeconds + DownHiddenSeconds + DescendSeconds + ToStationSeconds;

        public enum ClimbPhase { Wait, Walk, Hidden, Tend }

        // W31 (Codex K-119): 같은 길에서 일꾼 동작 그림을 고른다. 올라갈 때 기름통을 들고(운반 걷기), 내려올 때는 빈손
        public enum ClimbMotion { Wait, WalkCarry, ClimbUp, WalkEmpty, ClimbDown, Tend, Hidden }

        // i번째 일꾼이 돌림대 둘레에서 서는 자리 (탑 로컬 발 위치). capstanDegrees = 돌림대가 지금까지 돈 각도
        public static Vector3 WorkerStation(int i, float capstanDegrees)
        {
            float a = (capstanDegrees + 60f + i * 120f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * WorkerOrbit, FloorY, Mathf.Sin(a) * WorkerOrbit);
        }

        // 계단 위 길: s = 0(발치)..1(맨 위). 발 높이는 밟는 단 윗면을 따라 단마다 껑충 오른다
        public static Vector3 StairPoint(float s)
        {
            s = Mathf.Clamp01(s);
            float d = s * StairLength;
            float x, z;
            if (d <= LegOneLength) { x = -StairLine; z = LegOneStart + d; }
            else { x = LegTwoStart + (d - LegOneLength); z = StairLine; }
            float u = s * StairSteps;
            int f = Mathf.Min(Mathf.FloorToInt(u), StairSteps - 1);
            float g = u - f;
            float h = f + 1 + (f + 1 < StairSteps ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, g)) : 0f);
            return new Vector3(x, FloorY + StairRise * h, z);
        }

        // 기다림에서 시작해 cycleTime(초) 때 일꾼이 있는 곳. 벽 속으로 들어가면 Hidden (그리지 않는다)
        public static ClimbPhase Climber(float cycleTime, Vector3 station, out Vector3 feet) => Climber(cycleTime, station, out feet, out _);

        public static ClimbPhase Climber(float cycleTime, Vector3 station, out Vector3 feet, out ClimbMotion motion)
        {
            float t = Mathf.Repeat(cycleTime, ClimbCycleSeconds);
            Vector3 foot = StairPoint(0f);
            Vector3 footFloor = new Vector3(foot.x, FloorY, foot.z);
            feet = station;
            motion = ClimbMotion.Wait;
            if (t < WaitSeconds) return ClimbPhase.Wait;
            t -= WaitSeconds;
            if (t < ToFootSeconds)
            {
                float k = t / ToFootSeconds;
                feet = Vector3.Lerp(station, footFloor, k);
                feet.y = Mathf.Lerp(FloorY, foot.y, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, k)));   // 맨 끝에 첫 단을 밟는다
                motion = ClimbMotion.WalkCarry;   // 기름통을 들고 계단 발치로
                return ClimbPhase.Walk;
            }

            t -= ToFootSeconds;
            if (t < ClimbSeconds) { feet = StairPoint(t / ClimbSeconds); motion = ClimbMotion.ClimbUp; return ClimbPhase.Walk; }
            t -= ClimbSeconds;
            if (t < UpHiddenSeconds) { feet = StairPoint(1f); motion = ClimbMotion.Hidden; return ClimbPhase.Hidden; }
            t -= UpHiddenSeconds;
            if (t < EnterSeconds) { feet = Vector3.Lerp(LampHatch, LampStand, t / EnterSeconds); motion = ClimbMotion.WalkCarry; return ClimbPhase.Walk; }
            t -= EnterSeconds;
            if (t < TendSeconds) { feet = LampStand; motion = ClimbMotion.Tend; return ClimbPhase.Tend; }
            t -= TendSeconds;
            if (t < LeaveSeconds) { feet = Vector3.Lerp(LampStand, LampHatch, t / LeaveSeconds); motion = ClimbMotion.WalkEmpty; return ClimbPhase.Walk; }
            t -= LeaveSeconds;
            if (t < DownHiddenSeconds) { feet = StairPoint(1f); motion = ClimbMotion.Hidden; return ClimbPhase.Hidden; }
            t -= DownHiddenSeconds;
            if (t < DescendSeconds) { feet = StairPoint(1f - t / DescendSeconds); motion = ClimbMotion.ClimbDown; return ClimbPhase.Walk; }
            t -= DescendSeconds;
            float back = Mathf.Clamp01(t / ToStationSeconds);
            feet = Vector3.Lerp(footFloor, station, back);
            feet.y = Mathf.Lerp(foot.y, FloorY, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.3f, back)));
            motion = ClimbMotion.WalkEmpty;
            return ClimbPhase.Walk;
        }

        // ── 메시 (만드는 시점에만) ──
        // 계단 돌 단 (탑 벽 돌 재질 — BattleTowerView.Builder가 미터 단위로 UV를 준다)
        internal static void BuildStairSteps(BattleTowerView.Builder b)
        {
            float step = StairLength / StairSteps;
            for (int i = 0; i < StairSteps; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step, top = FloorY + (i + 1) * StairRise;
                if (a1 <= LegOneLength + 1e-4f) LegOne(b, a0, a1, top);
                else if (a0 >= LegOneLength - 1e-4f) LegTwo(b, a0, a1, top, false);
                else
                {
                    LegOne(b, a0, LegOneLength, top);   // 모퉁이 칸은 끝까지 서쪽 줄이 차지하고
                    LegTwo(b, LegOneLength, a1, top, true);
                }
            }
        }

        static void LegOne(BattleTowerView.Builder b, float a0, float a1, float top)
        {
            float z0 = LegOneStart + a0, z1 = a1 >= LegOneLength - 1e-4f ? InnerHalf : LegOneStart + a1;   // 모퉁이에서는 벽 안쪽까지
            b.Box(new Vector3(-InnerHalf, FloorY, z0), new Vector3(-InnerHalf + StairWidth, top, z1));
        }

        static void LegTwo(BattleTowerView.Builder b, float a0, float a1, float top, bool afterCorner)
        {
            float x0 = LegTwoStart + (a0 - LegOneLength), x1 = LegTwoStart + (a1 - LegOneLength);
            if (afterCorner) x0 = -InnerHalf + StairWidth;   // 모퉁이 칸 서쪽 줄 조각 바로 옆에서 이어 붙인다
            if (x1 <= x0 + 1e-4f) return;                    // 모퉁이를 겨우 넘긴 칸은 서쪽 줄 조각이 이미 덮었다
            b.Box(new Vector3(x0, FloorY, InnerHalf - StairWidth), new Vector3(x1, top, InnerHalf));
        }

        // 그림 판(K-116 props_box_atlas 128×128) 칸
        internal static class Atlas
        {
            public static readonly Rect CrateFront = MeshKit.Px(0, 0, 32, 32, 128, 128);
            public static readonly Rect CrateRight = MeshKit.Px(32, 0, 32, 32, 128, 128);
            public static readonly Rect CrateBack = MeshKit.Px(64, 0, 32, 32, 128, 128);
            public static readonly Rect CrateLeft = MeshKit.Px(96, 0, 32, 32, 128, 128);
            public static readonly Rect CrateTop = MeshKit.Px(0, 32, 32, 32, 128, 128);
            public static readonly Rect BarrelTop = MeshKit.Px(64, 32, 32, 32, 128, 128);
            public static readonly Rect BarrelSide = MeshKit.Px(0, 64, 100, 32, 128, 128);
        }

        // 돌림대 (돌아가는 한 덩어리, 원점 = 탑 가운데 바닥). 나무 칸 그림
        public static Mesh BuildCapstanMesh()
        {
            var kit = new MeshKit();
            var a = Atlas.CrateFront;
            kit.Barrel(Vector3.zero, CapstanDrumRadius, 0.3f, 8, Atlas.BarrelSide, Atlas.BarrelTop);                                        // 감는 통
            kit.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.1f, BattleTowerView.GroundHeight - FloorY - 0.4f, 0.1f), 0f, a);             // 천장으로 올라가는 축 (빛줄기를 돌리는 축)
            kit.Box(new Vector3(0f, CapstanBarY, 0f), new Vector3(CapstanBarHalf * 2f, 0.06f, 0.06f), 0f, a);                              // 십자 막대
            kit.Box(new Vector3(0f, CapstanBarY + 0.02f, 0f), new Vector3(0.06f, 0.06f, CapstanBarHalf * 2f), 0f, a);
            return kit.ToMesh("Tower Capstan");
        }

        // 탑 안 소품: 기름통·상자(그림 판), 석탄 자루·연장(단색). 돌림대·계단·일꾼 자리를 피해 동쪽·북동쪽 벽가
        internal static void BuildProps(MeshKit atlas, MeshKit dark)
        {
            float y = FloorY;
            atlas.Barrel(new Vector3(0.66f, y, 0.50f), 0.14f, 0.36f, 10, Atlas.BarrelSide, Atlas.BarrelTop);
            atlas.Barrel(new Vector3(0.66f, y, 0.08f), 0.14f, 0.36f, 10, Atlas.BarrelSide, Atlas.BarrelTop);
            atlas.Barrel(new Vector3(0.58f, y, -0.36f), 0.14f, 0.36f, 10, Atlas.BarrelSide, Atlas.BarrelTop);
            atlas.Box(new Vector3(0.30f, y, 0.72f), new Vector3(0.24f, 0.24f, 0.24f), 12f, Atlas.CrateFront, Atlas.CrateRight, Atlas.CrateBack, Atlas.CrateLeft, Atlas.CrateTop);

            var full = new Rect(0f, 0f, 1f, 1f);
            dark.Box(new Vector3(0.58f, y, 0.76f), new Vector3(0.2f, 0.16f, 0.2f), 20f, full);                   // 석탄 자루 셋
            dark.Box(new Vector3(0.44f, y, 0.58f), new Vector3(0.18f, 0.13f, 0.18f), -15f, full);
            dark.Box(new Vector3(0.52f, y + 0.16f, 0.74f), new Vector3(0.15f, 0.12f, 0.15f), 35f, full);
            dark.Box(new Vector3(-0.82f, y, -0.80f), new Vector3(0.03f, 0.8f, 0.03f), 0f, full);                  // 연장 둘 (자루 + 머리)
            dark.Box(new Vector3(-0.82f, y + 0.78f, -0.80f), new Vector3(0.2f, 0.03f, 0.04f), 0f, full);
            dark.Box(new Vector3(-0.74f, y, -0.83f), new Vector3(0.03f, 0.7f, 0.03f), 0f, full);
            dark.Box(new Vector3(-0.74f, y + 0.66f, -0.83f), new Vector3(0.04f, 0.1f, 0.12f), 0f, full);
        }
    }
}
