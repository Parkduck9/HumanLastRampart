using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using UnityEngine;

namespace HumanLastRampart.Story
{
    public sealed class StoryEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Story;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var request = context.Campaign?.NextStory;
            bool ending = request != null && request.Kind == StoryKind.DefeatEnding;
            // 원본 Art는 읽기 전용. 동일 PNG의 Story 소유 Resources 사본을 쓴다.
            var character = Resources.Load<Texture2D>("StoryArt/wizard_front_chestnut");
            // K-047: 화면 이름("스토리") 대신 말하는 사람과 대사만 — 옥토패스식 손그림 말풍선
            var view = new ScreenView(transform, null,
                ending ? "성벽의 방어가 끝났습니다." : "성벽을 지키기 위한 여정이 시작됩니다.",   // 대사는 스토리 도구가 생기면 그쪽에서 (5번 화면)
                character, "마법사");
            view.AddButton(ending ? "메인으로" : "다음", ending ? "처음으로" : "다음", () => Complete(ScreenOutcome.Done), true);
            return view;
        }
    }
}
