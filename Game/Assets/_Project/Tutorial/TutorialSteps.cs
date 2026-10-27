using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 한 단계의 글·표정·표시. 글은 이 표만 고치면 바뀐다 (장교가 공손하게, 짧게)
    public sealed class TutorialStepInfo
    {
        public string Title;
        public string Body;
        public bool ExplainOnly;      // true = [다음] 단추로 넘어간다 (설명만). false = 플레이어가 직접 해야 넘어간다
        public string ExplainBody;    // 직접 해 볼 수 없는 상황(예: 함정 살 골드 없음)에서 대신 보여 주는 설명 글. 없으면 Body 그대로
        public int Expression;        // 장교 표정 칸: 0 기본 / 1 설명(손들기) / 2 긴급 / 3 만족
        public Chip[] Chips;          // 조작 표시
        public bool Pointer;          // 가리킬 자리가 있으면 고리·화살표를 그린다

        public struct Chip
        {
            public int Icon;          // input_chips_32 칸 (0 Shift+끌기 / 1 오른쪽 클릭 / 2 휠 / 3 E / 4 Esc), -1 = 글만
            public string Text;
            public Chip(int icon, string text) { Icon = icon; Text = text; }
        }
    }

    public static class TutorialSteps
    {
        public const float ReadSeconds = 2.5f;   // 새 단계가 뜬 뒤 이 시간은 전투 시간을 멈춰 두고 읽게 한다
        public const float ActionTimeScale = 0.25f;   // 직접 해 보는 동안은 천천히
        public const float WalkMeters = 6f;      // 걷기 단계 완료: 이만큼 걸어야 한다
        public const float BeamTurnDegrees = 90f;   // 빛줄기 단계 완료: 빛줄기 방향 명령이 이만큼(도) 이상 바꾸어야 한다 (다른 성벽 쪽 한 번이면 90도)
        // 단계 → 그림 칸 (step_icons_24 · step_pips 모두 8칸 = 옛 8단계). 빛줄기 전용 그림이 아직 없어 망원경 렌즈(7)를 빌려 쓴다 — 전용 그림은 Codex 몫
        public static readonly int[] ArtColumn = { 0, 1, 2, 3, 4, 5, 7, 6, 7 };

        static readonly TutorialStepInfo[] table =
        {
            new TutorialStepInfo
            {
                Title = "성벽 위를 걷기", Expression = 0, Pointer = true,
                Body = "지휘관님, 먼저 성벽 위를 걸어 보십시오.\n병사들 곁으로 가야 명령을 내릴 수 있습니다.",
                Chips = new[] { new TutorialStepInfo.Chip(-1, "A D  /  ← →") },
            },
            new TutorialStepInfo
            {
                Title = "망원경", Expression = 1, ExplainOnly = true,
                Body = "지도를 연 뒤 오른쪽 버튼을 누르고 있으면\n그 둘레의 멀리 있는 적 무리를 볼 수 있습니다.",
                Chips = new[] { new TutorialStepInfo.Chip(1, "누르고 있기") },
            },
            new TutorialStepInfo
            {
                Title = "지도 열기", Expression = 0,
                Body = "이번에는 지도를 펼쳐 보십시오.\n멀리까지 한눈에 보입니다.",
                Chips = new[] { new TutorialStepInfo.Chip(-1, "Tab") },
            },
            new TutorialStepInfo
            {
                Title = "분대 보내기", Expression = 1, Pointer = true,
                Body = "병사들을 다른 성벽으로 보내 보겠습니다.\nShift를 누른 채 끌어 깃발을 감싸고, 둥근 표식에서 보낼 방향을 고르십시오.\n성벽을 눌러 수량을 고른 뒤 보낼 수도 있습니다.",
                Chips = new[] { new TutorialStepInfo.Chip(0, "상자로 고르기") },
            },
            new TutorialStepInfo
            {
                Title = "대포 (설명)", Expression = 1, ExplainOnly = true,
                Body = "대포는 말씀만 드리겠습니다.\n지도에서 대포 표시를 누르고 들판을 누르면 그곳을 겨눕니다.\n성벽 명령에서 먼저 노릴 적도 정할 수 있습니다.",
            },
            new TutorialStepInfo
            {
                Title = "마법 쓰기", Expression = 2,
                Body = "마법사에게 마법을 쓰게 하십시오.\n지도에서 마법을 고르고, 적이 모인 들판을 눌러 한 번 써 보십시오.",
                Chips = new[] { new TutorialStepInfo.Chip(-1, "Q  /  W") },
            },
            new TutorialStepInfo
            {
                Title = "등대 빛줄기 돌리기", Expression = 1, Pointer = true,
                Body = "등대 빛줄기를 돌려 보십시오. 빛 안의 적은 느려지고 더 크게 맞습니다.\n계단에서 E로 탑에 내려가 다시 E를 누르고, 방향을 고르십시오.",
                Chips = new[] { new TutorialStepInfo.Chip(3, "계단 · 탑 명령"), new TutorialStepInfo.Chip(-1, "방향 단추") },
            },
            new TutorialStepInfo
            {
                Title = "함정 놓기", Expression = 1,
                Body = "함정 공방에서 함정을 사서 지도 위에 하나 놓아 보십시오.\n다음 전투에서 적이 지나갈 길목이 좋습니다.",
                ExplainBody = "함정 공방에서는 함정을 사서 지도 위에 놓을 수 있습니다.\n지금은 살 돈이 모자라니 말씀만 드리겠습니다. 적이 지나갈 길목이 좋습니다.",
                Chips = new[] { new TutorialStepInfo.Chip(3, "함정 공방") },
            },
            new TutorialStepInfo
            {
                Title = "망원경 업그레이드 (설명)", Expression = 3, ExplainOnly = true,
                Body = "상점에서 망원경 렌즈를 사서 장착하면\n더 넓게 보이고, 시야에서 벗어난 적도 잠시 지도에 남습니다.",
            },
        };

        // 우두머리 첫 예고 때 한 번 뜨는 두 쪽 안내 (W48). 8단계 표 밖이라 진행 번호·순서에 끼지 않는다. IconCell = step_icons_24 칸
        public static readonly TutorialStepInfo[] BossTip =
        {
            new TutorialStepInfo
            {
                Title = "성벽손 오우거 (1/2)", Expression = 2, ExplainOnly = true,
                Body = "지휘관님, 빛줄기 밖에서는 화살과 총이 거의 듣지 않습니다.\n빛 안에서는 온전히 맞고 빛에도 타지만, 걸음이 빨라집니다.",
                Chips = new[] { new TutorialStepInfo.Chip(3, "탑에서 빛줄기 돌리기") },
            },
            new TutorialStepInfo
            {
                Title = "성벽손 오우거 (2/2)", Expression = 1, ExplainOnly = true,
                Body = "오르는 동안 대포를 맞히면 크게 다치고, 많이 다치면 떨어집니다.\n폭탄 함정은 잠시 쓰러뜨립니다. 통로에 오르게 두면 안 됩니다!",
                Chips = new[] { new TutorialStepInfo.Chip(-1, "지도에서 대포 겨누기"), new TutorialStepInfo.Chip(-1, "폭탄 함정") },
            },
        };
        public static readonly int[] BossTipIcons = { 5, 4 };   // 쪽별 제목 그림 칸 (마법 · 대포)

        public static TutorialStepInfo Get(TutorialStep step) => table[(int)step];
    }
}
