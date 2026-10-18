using System.Collections.Generic;

namespace HumanLastRampart.Combat.PerfLab
{
    // 대포·대포병 그림 규격 한곳 (K-060, Codex K-052 대포 끌기 3차 시안 — 사용자 확정 전 미리 연결).
    // 그림을 바꿔 끼울 때는 이 파일의 이름·칸·기준점만 고친다. 측정값은 Docs/References/codex_k052_v1/manifest.json·README
    public static class CannonArt
    {
        // ── 대포병 (칸 384×384, 6칸, 발 (192,352), 인물 키 210px) ──
        public const int CrewCellPx = 384;
        public const int CrewFrames = 6;
        public const float CrewFigurePx = 210f;          // 칸 안 인물 키 → 병사 크기(0.35m)에 맞추는 배율 = 384 / 210
        public const float CrewStepPx = 32f;             // 걷기 한 칸마다 발이 왼쪽으로 32px (발 미끄러짐 없게 이동 속도와 맞춘다)
        // 불붙이기 시트는 병종 정의(unit_cannon_crew.igniteSheet)가 가리키는 그림 파일을 K-052 것으로 바꿔 끼웠다 (Art/Characters/Fire/cannon_crew_ignite6.png)
        public const string IgniteSheetName = "cannon_crew_ignite6";
        // 끄는 동작 (Resources/BattleArt/Cannon/) — 그림이 왼쪽으로 끈다(포가 꼬리 왼쪽, 포구 오른쪽)
        public const string PullSheet = "BattleArt/Cannon/crew_pull6";   // 앞에서 당기는 병사
        public const string PushSheet = "BattleArt/Cannon/crew_push6";   // 뒤(바퀴 옆 화면 앞쪽)에서 미는 병사

        // ── 끄는 대포 (칸 768×384, 놓는 기준점 (336,352) = 앞바퀴 접지, 앞바퀴 지름 101px·중심 (336.5,301.5)) ──
        public const string HaulCannon = "BattleArt/Cannon/haul_cannon_body";       // 포가 + 포신 + 바퀴 (정지 그림)
        public const string HaulChassis = "BattleArt/Cannon/haul_cannon_chassis";   // 바퀴 뺀 포가·포신 (바퀴 회전 재생용)
        public const string HaulWheel = "BattleArt/Cannon/haul_cannon_wheel";       // 바퀴 한 개
        public const float HaulCellWidthPx = 768f, HaulCellHeightPx = 384f;
        public const float HaulPivotXPx = 336f, HaulPivotYPx = 352f;
        public const float WheelDiameterPx = 101f;
        public const float WheelCenterXPx = 336.5f, WheelCenterYPx = 301.5f;
        // 병사 자리 (대포 기준점에서 그림 화소, + = 오른쪽): 시안 합성(haul_*.png) — 앞 병사는 견인 쪽(왼쪽) 포가 꼬리 앞, 뒤 병사는 바퀴 옆
        // haul_1.png에서 읽은 값 (칸 원점 차이) — 정확한 합성 좌표가 오면 고친다
        public const float PullOffsetPx = -391f;
        public const float PushOffsetPx = 188f;

        // ── 설치 대포 크기 (사격구 안, 뒷모습 cannon_recoil) ──
        // Codex 측정: 정면 설치 대포 폭 ÷ 서 있는 대포병 키 = 345 ÷ 300 ≈ 1.15. 병사 키 0.35m → 보이는 폭 약 0.40m (끄는 대포와 같은 축척)
        public const float InstalledWidthPerSoldier = 345f / 300f;
        public const float RecoilVisibleWidthFraction = 234f / 256f;   // cannon_recoil 칸 256 중 보이는 폭 (alphaBBox 11~245)

        // 그림 화소 → m (대포병 인물 키 210px = 병사 키)
        public static float MetersPerPx(float soldierHeight) => soldierHeight / CrewFigurePx;

        // 칸 크기가 Codex 공통(256×384)과 다른 시트 (시트 이름 → 칸 폭 px). PerfLabArt.FromSheet·FrameCount가 쓴다
        public static readonly Dictionary<string, int> CellWidthPx = new Dictionary<string, int>
        {
            { IgniteSheetName, CrewCellPx },
            { "crew_pull6", CrewCellPx },
            { "crew_push6", CrewCellPx },
        };

        // 자동 측정표(PerfLabSheetHeights)에 아직 없는 시트의 인물 키 — 측정 도구를 다시 돌리면 그 값이 우선
        public static readonly Dictionary<string, float> FigurePx = new Dictionary<string, float>
        {
            { IgniteSheetName, CrewFigurePx },
            { "crew_pull6", CrewFigurePx },
            { "crew_push6", CrewFigurePx },
        };
    }
}
