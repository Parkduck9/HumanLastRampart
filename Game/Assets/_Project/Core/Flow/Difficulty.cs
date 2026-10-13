namespace HumanLastRampart.Core.Flow
{
    // 난이도 (D-026 B4, D-031 K1). 판 도중 바꿀 수 없다 (D-027 Q8).
    // 쉬움: 지도 모드에서 시간 정지 + 적 수 절반 / 현실: 시간 계속 흐름, 최대 동시 적 50,000
    public enum Difficulty
    {
        Easy,
        Realistic,
    }

    // 메인 화면이 새 게임을 시작할 때 넘기는 선택 (C-003 v1.2)
    public struct NewGameOptions
    {
        public Difficulty Difficulty;

        public NewGameOptions(Difficulty difficulty)
        {
            Difficulty = difficulty;
        }
    }
}