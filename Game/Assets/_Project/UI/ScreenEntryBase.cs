using System;
using HumanLastRampart.Core.Flow;
using UnityEngine;

namespace HumanLastRampart.UI
{
    // 승인된 C-003 수명: OnEnter부터 입력, 완료 후 차단, OnExit에서 구독/화면 정리.
    public abstract class ScreenEntryBase : MonoBehaviour, IScreenEntry
    {
        IScreenContext context;
        bool acceptingInput;
        protected ScreenView View { get; private set; }
        public abstract ScreenId Id { get; }

        public void OnEnter(IScreenContext screenContext)
        {
            if (screenContext == null) throw new ArgumentNullException(nameof(screenContext));
            OnExit();
            context = screenContext;
            try
            {
                View = BuildView(screenContext);
                acceptingInput = true;
                View.SetInput(true);
                View.FocusFirst();
            }
            catch
            {
                OnExit();
                throw;
            }
        }

        protected abstract ScreenView BuildView(IScreenContext screenContext);

        protected void ShowDifficulty()
        {
            if (!acceptingInput) return;
            View.ShowDifficulty(() => StartNewGame(Difficulty.Easy),
                () => StartNewGame(Difficulty.Realistic));
        }

        protected void Complete(ScreenOutcome outcome)
        {
            if (!acceptingInput || context == null) return;
            var current = context;
            acceptingInput = false;
            View.SetInput(false);
            current.Complete(outcome);
        }

        void StartNewGame(Difficulty difficulty)
        {
            if (!acceptingInput || context == null) return;
            var current = context;
            acceptingInput = false;
            View.SetInput(false);
            current.CompleteNewGame(new NewGameOptions(difficulty));
        }

        public void OnExit()
        {
            acceptingInput = false;
            context = null;
            View?.Dispose();
            View = null;
        }

        protected virtual void OnDestroy() { OnExit(); }
    }
}
