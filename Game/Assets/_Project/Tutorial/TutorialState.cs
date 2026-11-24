using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 튜토리얼 9단계 (사용자 결정 2026-10-20 · 2026-10-22 · 2026-10-27): 걷기 → 망원경 → 지도 → 분대 → 대포(말만) → 마법 → 빛줄기(탑) → 함정(정비) → 망원경 업그레이드(말만)
    public enum TutorialStep { Walk = 0, Telescope, Map, Squad, Cannon, Magic, Beam, Trap, Lens }

    // 저장 자리. 진행 데이터(CampaignState)에는 표시 칸이 없고 저장 형식을 바꾸지 않기로 했으므로 PlayerPrefs 한 칸만 쓴다 (시험은 메모리)
    public interface ITutorialStore
    {
        bool TryLoad(out int progress);
        void Save(int progress);
        void Clear();
    }

    public sealed class PlayerPrefsTutorialStore : ITutorialStore
    {
        // 값: 0~8 = 그 단계를 하던 중 (단계에 들어갈 때마다 저장), 9 = 끝(건너뛴 것 포함). 플레이어 단위 전역 키 (튜토리얼은 플레이어마다 한 번)
        // v2: 빛줄기 단계가 6번에 끼어 번호가 하나씩 밀렸다. v1 값은 처음 읽을 때 한 번 옮긴다 (TutorialState.MigrateV1)
        public const string Key = "HLR.Tutorial.v2";
        public const string LegacyKey = "HLR.Tutorial.v1";
        readonly string key;

        public PlayerPrefsTutorialStore(string key = Key) { this.key = key; }

        public bool TryLoad(out int progress)
        {
            progress = 0;
            if (key == Key && !PlayerPrefs.HasKey(Key) && PlayerPrefs.HasKey(LegacyKey))
            {
                PlayerPrefs.SetInt(Key, TutorialState.MigrateV1(PlayerPrefs.GetInt(LegacyKey)));
                PlayerPrefs.DeleteKey(LegacyKey);   // 한 번만 옮긴다 — 남겨 두면 Clear 뒤 옛 값이 되살아난다
                PlayerPrefs.Save();
            }

            if (!PlayerPrefs.HasKey(key)) return false;
            progress = Mathf.Clamp(PlayerPrefs.GetInt(key), 0, TutorialState.Total);
            return true;
        }

        public void Save(int progress) { PlayerPrefs.SetInt(key, progress); PlayerPrefs.Save(); }
        public void Clear()
        {
            PlayerPrefs.DeleteKey(key);
            if (key == Key) PlayerPrefs.DeleteKey(LegacyKey);   // 아직 옮기기 전의 옛 값도 지운다 (다시 읽을 때 되살아나지 않게)
        }
    }

    public sealed class MemoryTutorialStore : ITutorialStore
    {
        bool has;
        int value;
        public bool TryLoad(out int progress) { progress = value; return has; }
        public void Save(int progress) { has = true; value = progress; }
        public void Clear() { has = false; value = 0; }
    }

    // 단계 진행 표시와 저장. 전투 쪽은 Walk~Beam, 정비 쪽은 Trap~Lens (두 구간이 한 진행 번호를 이어 쓴다)
    public sealed class TutorialState
    {
        public const int Total = 9;
        public const int BattleSteps = 7;   // 0~6 전투 중, 7~8 정비

        // 옛 저장(8단계) 값 → 지금 번호: 0~5(걷기~마법)는 그대로, 6~7(함정·렌즈)은 하나씩 뒤로, 8(끝)은 9(끝). 더 큰 값도 끝으로
        public static int MigrateV1(int old)
        {
            if (old <= 5) return Mathf.Max(0, old);
            if (old >= 8) return Total;
            return old + 1;
        }

        // ── 저장·꺼짐 스위치 (전역) ──
        public static ITutorialStore Store = new PlayerPrefsTutorialStore();
        public static bool Suppressed;       // 시험이 다른 시험을 방해하지 않게 끈다. 켜 두면 튜토리얼이 시작되지 않는다

        public static bool ShouldStartBattle(int battleNumber)
        {
            if (Suppressed || battleNumber != 1) return false;
            return !Store.TryLoad(out int progress) || progress < BattleSteps;
        }

        // 1번째 전투를 마치고 정비에 들어왔을 때. 전투 구간을 다 못 했더라도 정비 구간부터 이어 간다.
        // 확인만 하며 저장 값은 바꾸지 않는다 (정비 구간 번호는 단계 기계가 시작할 때 저장한다)
        public static bool ShouldStartPrep(int battleNumber)
        {
            if (Suppressed || battleNumber != 2) return false;
            return Store.TryLoad(out int progress) && progress < Total;
        }

        // 구간 [first, last] 안에서 이어 할 단계: 저장된 번호를 구간 안으로 맞춘다 (없으면 first)
        public static TutorialStep ResumeStep(TutorialStep first, TutorialStep last)
        {
            if (!Store.TryLoad(out int progress)) return first;
            return (TutorialStep)Mathf.Clamp(progress, (int)first, (int)last);
        }

        public static bool Completed => Store.TryLoad(out int progress) && progress >= Total;

        // 설정에서 "튜토리얼 다시 보기"를 만들 때 쓰는 길
        public static void Reset() { Store.Clear(); BossTipStore.Clear(); }

        // ── 우두머리 첫 예고 안내 (W48): 본 적이 있는지 따로 한 칸 (옛 저장에는 없으므로 "아직 안 봄"으로 읽힌다 — 저장 형식 호환) ──
        public const string BossTipKey = "HLR.Tutorial.BossTip.v1";
        public static ITutorialStore BossTipStore = new PlayerPrefsTutorialStore(BossTipKey);
        public static bool BossTipSeen => BossTipStore.TryLoad(out int value) && value >= 1;
        public static void MarkBossTipSeen() => BossTipStore.Save(1);

        // 튜토리얼이 꺼져 있지 않고 아직 안 봤을 때만
        public static bool ShouldShowBossTip() => !Suppressed && !BossTipSeen;

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

        // 단계에 들어갈 때마다: 이 단계를 하던 중이라고 저장 (뒤로는 가지 않는다 — 이미 더 나아간 저장 값을 낮추지 않게)
        public void SaveStep(TutorialStep step)
        {
            if (Store.TryLoad(out int progress) && progress >= (int)step) return;
            Store.Save((int)step);
        }

        // 구간을 끝냈을 때: 다음에 이어 할 번호를 저장한다 (정비 구간까지 끝나면 8)
        public void SaveNext(TutorialStep last) => Store.Save((int)last + 1);
    }
}
