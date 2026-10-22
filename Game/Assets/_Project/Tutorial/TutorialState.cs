using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 튜토리얼 8단계 (사용자 결정 2026-10-20 · 2026-10-22): 걷기 → 망원경 → 지도 → 분대 → 대포(말만) → 마법 → 함정(정비) → 망원경 업그레이드(말만)
    public enum TutorialStep { Walk = 0, Telescope, Map, Squad, Cannon, Magic, Trap, Lens }

    // 저장 자리. 진행 데이터(CampaignState)에는 표시 칸이 없고 저장 형식을 바꾸지 않기로 했으므로 PlayerPrefs 한 칸만 쓴다 (시험은 메모리)
    public interface ITutorialStore
    {
        bool TryLoad(out int progress);
        void Save(int progress);
        void Clear();
    }

    public sealed class PlayerPrefsTutorialStore : ITutorialStore
    {
        public const string Key = "HLR.Tutorial.v1";   // 값: 0~7 = 그 단계를 하던 중, 8 = 끝(건너뛴 것 포함)

        public bool TryLoad(out int progress)
        {
            progress = 0;
            if (!PlayerPrefs.HasKey(Key)) return false;
            progress = Mathf.Clamp(PlayerPrefs.GetInt(Key), 0, TutorialState.Total);
            return true;
        }

        public void Save(int progress) { PlayerPrefs.SetInt(Key, progress); PlayerPrefs.Save(); }
        public void Clear() { PlayerPrefs.DeleteKey(Key); }
    }

    public sealed class MemoryTutorialStore : ITutorialStore
    {
        bool has;
        int value;
        public bool TryLoad(out int progress) { progress = value; return has; }
        public void Save(int progress) { has = true; value = progress; }
        public void Clear() { has = false; value = 0; }
    }

    // 단계 진행 표시와 저장. 전투 쪽은 Walk~Magic, 정비 쪽은 Trap~Lens (두 구간이 한 진행 번호를 이어 쓴다)
    public sealed class TutorialState
    {
        public const int Total = 8;
        public const int BattleSteps = 6;   // 0~5 전투 중, 6~7 정비

        // ── 저장·꺼짐 스위치 (전역) ──
        public static ITutorialStore Store = new PlayerPrefsTutorialStore();
        public static bool Suppressed;       // 시험이 다른 시험을 방해하지 않게 끈다. 켜 두면 튜토리얼이 시작되지 않는다

        public static bool ShouldStartBattle(int battleNumber)
        {
            if (Suppressed || battleNumber != 1) return false;
            return !Store.TryLoad(out int progress) || progress < BattleSteps;
        }

        // 1번째 전투를 마치고 정비에 들어왔을 때. 전투 구간을 다 못 했더라도 정비 구간부터 이어 간다
        public static bool ShouldStartPrep(int battleNumber)
        {
            if (Suppressed || battleNumber != 2) return false;
            if (!Store.TryLoad(out int progress) || progress >= Total) return false;
            if (progress < BattleSteps) Store.Save(BattleSteps);
            return true;
        }

        public static bool Completed => Store.TryLoad(out int progress) && progress >= Total;

        // 설정에서 "튜토리얼 다시 보기"를 만들 때 쓰는 길
        public static void Reset() => Store.Clear();

        // ── 한 번의 진행 (인스턴스) ──
        readonly bool[] done = new bool[Total];
        public bool IsDone(TutorialStep step) => done[(int)step];
        public bool Skipped { get; private set; }

        public void MarkDone(TutorialStep step) => done[(int)step] = true;

        public void MarkSkipped()
        {
            Skipped = true;
            Store.Save(Total);
        }

        // 구간을 끝냈을 때: 다음에 이어 할 번호를 저장한다 (정비 구간까지 끝나면 8)
        public void SaveNext(TutorialStep last) => Store.Save((int)last + 1);
    }
}
