using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;

namespace HumanLastRampart.Story
{
    public sealed class StoryEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Story;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var request = context.Campaign?.NextStory;
            bool ending = request != null && request.Kind == StoryKind.DefeatEnding;
            var view = new ScreenView(transform, ending ? "패배 엔딩" : "스토리",
                ending ? "성벽의 방어가 끝났습니다." : "성벽을 지키기 위한 여정이 시작됩니다.");
            var next = view.AddButton(ending ? "메인으로" : "다음", () => Complete(ScreenOutcome.Done));
            view.MarkPrimary(next);   // 주된 다음 행동 (D-066)
            return view;
        }
    }
}
