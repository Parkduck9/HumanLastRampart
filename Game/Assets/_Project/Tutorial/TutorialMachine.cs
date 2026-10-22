using System;
using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 튜토리얼이 게임 상태를 읽고 시간을 늦추는 길. 전투는 BattleTutorialSource, 정비는 PrepTutorialSource, 시험은 가짜가 채운다.
    // 판정은 모두 "단계가 시작된 때의 값보다 늘었는가"로 한다 (이미 해 둔 일로 건너뛰지 않게)
    public interface ITutorialSource
    {
        bool Alive { get; }                 // false면 화면이 사라진 것 — 튜토리얼도 정리된다
        void Tick(float dt);                // 매 프레임 처음에 한 번: 게임 상태를 읽어 아래 값을 갱신
        float WalkedMeters { get; }         // 주인공이 걸은 거리 합 (m)
        bool MapOpen { get; }
        int SquadSends { get; }             // 분대를 보내라는 명령이 나간 횟수 (묶음이 움직이기 시작했거나 전령이 떠난 것)
        int SpellCasts { get; }             // 마법을 쓴 횟수
        int TrapCount { get; }              // 설치된 함정 수
        bool IsAvailable(TutorialStep step);   // false면 이 단계는 할 수 없으므로 건너뛴다 (예: 마법사 없음)
        bool TryGetTargetScreen(TutorialStep step, out Vector2 screen);   // 고리로 가리킬 화면 자리(화소)
        void SetTimeScale(float scale);     // 전투 시간 배율 (0 = 멈춤)
    }

    // 단계 기계: 정해진 구간(first~last)을 차례로 지나며, 직접 해야 하는 단계는 상태를 읽어 끝내고 설명 단계는 [다음]으로 끝낸다.
    // 화면(View)과 분리해 두어 EditMode에서 가짜 Source로 시험한다
    public sealed class TutorialMachine
    {
        readonly ITutorialSource src;
        readonly TutorialState state;
        readonly TutorialStep last;
        float stepTime;
        float baseWalk;
        int baseSquad, baseSpell, baseTrap;
        float appliedScale = -1f;

        public TutorialStep Current { get; private set; }
        public bool Finished { get; private set; }
        public bool Skipped => state.Skipped;
        public float StepTime => stepTime;
        public TutorialState State => state;
        public TutorialStepInfo Info => TutorialSteps.Get(Current);

        public event Action<TutorialStep> StepEntered;
        public event Action<TutorialStep, bool> StepCompleted;   // (단계, 건너뜀 여부 — 할 수 없는 단계였는가)
        public event Action RangeFinished;                       // 구간을 끝까지 마쳤을 때 (건너뛰기는 아님)

        public TutorialMachine(ITutorialSource source, TutorialState tutorialState, TutorialStep first, TutorialStep lastStep)
        {
            src = source;
            state = tutorialState;
            last = lastStep;
            Current = first;
            Enter();
        }

        void Enter()
        {
            stepTime = 0f;
            baseWalk = src.WalkedMeters;
            baseSquad = src.SquadSends;
            baseSpell = src.SpellCasts;
            baseTrap = src.TrapCount;
            StepEntered?.Invoke(Current);
        }

        // 전투 시간 배율: 설명 단계와 새 단계의 읽는 시간은 멈춤, 직접 해 보는 동안은 천천히, 끝나면 정상
        public float TimeScale => Finished ? 1f : Info.ExplainOnly || stepTime < TutorialSteps.ReadSeconds ? 0f : TutorialSteps.ActionTimeScale;

        public void Update(float dt)
        {
            if (Finished) { Apply(); return; }
            src.Tick(dt);
            stepTime += dt;
            if (!src.IsAvailable(Current)) { Complete(true); Apply(); return; }
            if (!Info.ExplainOnly && Detected()) Complete(false);
            Apply();
        }

        bool Detected()
        {
            switch (Current)
            {
                case TutorialStep.Walk: return src.WalkedMeters - baseWalk >= TutorialSteps.WalkMeters;
                case TutorialStep.Map: return src.MapOpen;
                case TutorialStep.Squad: return src.SquadSends > baseSquad;
                case TutorialStep.Magic: return src.SpellCasts > baseSpell;
                case TutorialStep.Trap: return src.TrapCount > baseTrap;
                default: return false;
            }
        }

        // [다음] 단추: 설명만 하는 단계에서만 통한다. 직접 해야 하는 단계는 눌러도 넘어가지 않는다
        public bool Next()
        {
            if (Finished || !Info.ExplainOnly) return false;
            Complete(false);
            Apply();
            return true;
        }

        public void Skip()
        {
            if (Finished) return;
            Finished = true;
            state.MarkSkipped();
            Apply();
        }

        void Complete(bool unavailable)
        {
            TutorialStep done = Current;
            state.MarkDone(done);
            StepCompleted?.Invoke(done, unavailable);
            if (done >= last)
            {
                Finished = true;
                state.SaveNext(last);
                RangeFinished?.Invoke();
                return;
            }

            Current = done + 1;
            Enter();
        }

        void Apply()
        {
            float scale = TimeScale;
            if (Mathf.Approximately(scale, appliedScale)) return;
            appliedScale = scale;
            src.SetTimeScale(scale);
        }
    }
}
