using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using UnityEngine;

namespace HumanLastRampart.Story
{
    // 5번 스토리 화면 (K-050). StoryRequest.StoryId와 같은 이름의 이야기 에셋(Resources/Stories)을 재생하고 끝나면 Done.
    // 이동할 화면은 App이 정한다 (첫 이야기 → 전투, 패배 엔딩 → 메인).
    // 공용 ScreenView는 입력 막기·정리용으로만 쓰고 (빈 기본 창), 화면은 StoryPlayer가 그 위에 직접 그린다
    public sealed class StoryEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Story;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var request = context.Campaign?.NextStory;
            StoryKind kind = request != null ? request.Kind : StoryKind.Normal;
            StoryScript script = Load(request?.StoryId, kind);
            var view = new ScreenView(transform, "", "");
            view.HidePanel();   // K-047: 빈 양피지 판이 재생기 뒤에 비치지 않게
            var root = (RectTransform)view.PanelGroup.transform.parent;   // 화면 캔버스 (입력 막기 CanvasGroup이 있는 곳)
            StoryPlayer player = StoryPlayer.Create(root, script, ScreenView.UiFont);
            player.Finished += () => Complete(ScreenOutcome.Done);
            return view;
        }

        // 에셋이 없거나 비었으면 코드 안의 임시 이야기로 (흐름이 멈추지 않게). 경고만 남긴다
        static StoryScript Load(string storyId, StoryKind kind)
        {
            StoryScript script = string.IsNullOrEmpty(storyId) ? null : Resources.Load<StoryScript>("Stories/" + storyId);
            if (script != null && script.LineCount > 0)
            {
                if (script.Kind != kind) Debug.LogWarning($"[스토리] {storyId}의 종류({script.Kind})가 요청({kind})과 다릅니다");
                return script;
            }

            Debug.LogWarning($"[스토리] 이야기 에셋 '{storyId}'을 찾지 못해 임시 이야기를 씁니다");
            return StoryPlaceholder.Create(kind);
        }
    }
}
