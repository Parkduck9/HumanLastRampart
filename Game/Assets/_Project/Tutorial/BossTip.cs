using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 우두머리 첫 예고 안내 (W48)가 게임 상태를 읽고 시간을 멈추는 길. 전투는 BattleBossTipSource, 시험은 가짜
    public interface IBossTipSource
    {
        bool Alive { get; }            // false면 화면이 사라진 것 — 안내도 정리된다
        bool BossWarning { get; }      // 우두머리 나오기 직전 예고 구간인가 (BattleSim.TryGetBossWarning)
        void SetTimeScale(float scale);
    }

    // 두 쪽짜리 설명 전용 안내 기계. 예고가 처음 뜨는 순간 시작하고, 그때 곧바로 "봤다"고 저장한다 (중간에 나가도 다시 뜨지 않게).
    // 보는 동안 전투 시간은 멈춘다. 9단계 첫 전투 튜토리얼과는 진행 번호도 순서도 따로다
    public sealed class BossTipMachine
    {
        public const int PageCount = 2;
        readonly IBossTipSource src;
        float appliedScale = -1f;

        public int Page { get; private set; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool Visible => Started && !Finished;
        public TutorialStepInfo Info => TutorialSteps.BossTip[Page];
        public float TimeScale => Visible ? 0f : 1f;

        public BossTipMachine(IBossTipSource source) { src = source; }

        public void Update()
        {
            if (Finished) return;
            if (!Started)
            {
                if (!src.BossWarning) return;
                if (TutorialState.BossTipSeen) { Finished = true; return; }   // 이미 본 저장이면 조용히 끝
                Started = true;
                TutorialState.MarkBossTipSeen();
            }

            Apply();
        }

        public bool Next()
        {
            if (!Visible) return false;
            if (++Page >= PageCount) { Page = PageCount - 1; Finished = true; }
            Apply();
            return true;
        }

        void Apply()
        {
            float scale = TimeScale;
            if (Mathf.Approximately(scale, appliedScale)) return;
            appliedScale = scale;
            src.SetTimeScale(scale);
        }
    }

    // 기계 + 화면을 매 프레임 돌리는 컴포넌트. 전투 입구에서 Begin 한 줄로 달아 둔다 (예고가 올 때까지는 아무것도 보이지 않는다)
    public sealed class BossTipDirector : MonoBehaviour
    {
        BossTipMachine machine;
        TutorialView view;
        IBossTipSource source;

        public BossTipMachine Machine => machine;

        public static BossTipDirector Begin(Transform parent, IBossTipSource source)
        {
            var go = new GameObject("Boss Tip Director");
            go.transform.SetParent(parent, false);
            var director = go.AddComponent<BossTipDirector>();
            director.source = source;
            director.machine = new BossTipMachine(source);
            director.view = new TutorialView(go.transform, () => director.machine.Next(), () => { });
            director.view.RefreshTip(director.machine);   // 예고 전에는 판이 보이지 않게 먼저 끈다
            return director;
        }

        void Update()
        {
            if (machine == null) return;
            if (!source.Alive || machine.Finished) { Destroy(gameObject); return; }
            machine.Update();
            view.RefreshTip(machine);
        }

        void OnDestroy()
        {
            view?.Dispose();
            if (machine != null && machine.Started) source?.SetTimeScale(1f);
        }
    }
}
