using System;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using UnityEngine;

namespace HumanLastRampart.App.Flow
{
    // 화면마다 새로 만드는 연결 고리 (C-003 3-1, v1.1, v1.2)
    sealed class ScreenContext : IScreenContext
    {
        readonly GameFlow flow;
        bool valid = true;
        bool accepted;

        public ScreenId Screen { get; }

        public ScreenContext(GameFlow flow, ScreenId screen)
        {
            this.flow = flow;
            Screen = screen;
        }

        public CampaignState Campaign
        {
            get
            {
                ThrowIfInvalid(nameof(Campaign));
                return flow.Campaign;
            }
        }

        public IDefinitionLookup Definitions
        {
            get
            {
                ThrowIfInvalid(nameof(Definitions));
                return flow.Definitions;
            }
        }

        public void Complete(ScreenOutcome outcome) => Submit(outcome, false, default);

        public void CompleteNewGame(NewGameOptions options) => Submit(ScreenOutcome.StartNewGame, true, options);

        // 받아들인 신호를 App이 처리하지 못했을 때(예: 새 게임 거부) 다시 보낼 수 있게 한다
        internal void Reopen() => accepted = false;

        internal void Invalidate() => valid = false;

        void Submit(ScreenOutcome outcome, bool viaNewGame, NewGameOptions options)
        {
            if (!valid)
            {
                Debug.LogWarning($"[App] 이미 닫힌 화면({Screen})의 신호 {outcome}를 무시합니다.");
                return;
            }

            if (accepted)
            {
                Debug.LogWarning($"[App] {Screen} 화면이 신호를 두 번 보냈습니다. 두 번째 {outcome}는 무시합니다.");
                return;
            }

            if (!FlowRouter.IsAllowed(Screen, outcome, viaNewGame))
            {
                string how = viaNewGame ? "CompleteNewGame" : "Complete";
                Debug.LogError($"[App] {Screen} 화면은 {how}({outcome})를 보낼 수 없습니다 (C-003 3-1). 무시합니다.");
                return;
            }

            accepted = true;
            flow.OnScreenCompleted(this, outcome, options);
        }

        void ThrowIfInvalid(string member)
        {
            if (!valid)
            {
                throw new InvalidOperationException($"{Screen} 화면의 컨텍스트는 이미 무효입니다 ({member} 접근, C-003 3-1).");
            }
        }
    }
}