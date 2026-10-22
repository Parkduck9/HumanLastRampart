using HumanLastRampart.Core.Campaign;
using UnityEngine;

namespace HumanLastRampart.Story
{
    // 이야기 에셋이 없을 때만 쓰는 임시 이야기 (K-050). 실제 이야기는 사용자가 제작 도구로 쓴다.
    // 내용은 Resources/Stories의 임시 에셋과 같다 (그림은 Resources에서 찾을 수 있는 것만)
    public static class StoryPlaceholder
    {
        public static StoryScript Create(StoryKind kind)
        {
            var script = ScriptableObject.CreateInstance<StoryScript>();
            script.Kind = kind;
            script.Title = "임시 이야기";
            Texture2D wizard = Resources.Load<Texture2D>("StoryArt/wizard_front_chestnut");
            var scene = new StoryScene { Name = kind == StoryKind.DefeatEnding ? "무너진 성벽" : "고향 성벽" };
            script.Scenes.Add(scene);
            if (kind == StoryKind.DefeatEnding)
            {
                script.Id = "story_defeat";
                Add(scene, "", StorySide.None, null, "성벽이 무너지고, 횃불이 하나둘 꺼져 갔다.", StoryEffect.FadeBlack);
                Add(scene, "장교", StorySide.Left, null, "……다음에는, 반드시 지켜 내겠다.", StoryEffect.None);
            }
            else if (kind == StoryKind.RunComplete)
            {
                script.Id = "story_run1_complete";   // 임시 한 줄 — 실제 글은 조정 담당(W8)
                Add(scene, "", StorySide.None, null, "두 번째 등대에도 불이 켜졌다. 어둠 속에서 빛이 이어지기 시작했다.", StoryEffect.FadeBlack);
            }
            else
            {
                script.Id = "story_intro";
                Add(scene, "", StorySide.None, null, "장교 훈련을 마치고 삼 년. 나는 장교 옷을 입은 채 고향 성벽으로 돌아왔다.", StoryEffect.FadeBlack);
                Add(scene, "궁수", StorySide.Right, null, "장교님! 정말 돌아오셨군요. 북쪽 들판에 놈들이 보입니다!", StoryEffect.None);
                Add(scene, "장교", StorySide.Left, null, "옷깃을 여밀 틈도 없겠군. 모두 성벽 위로 올라라.", StoryEffect.None);
                Add(scene, "마법사", StorySide.Right, wizard, "제 불꽃도 준비됐어요. 지시만 내려 주세요.", StoryEffect.None);
                Add(scene, "장교", StorySide.Left, null, "이곳이 우리의 마지막 성벽이다. 한 걸음도 물러서지 마라!", StoryEffect.Shake);
            }

            return script;
        }

        static void Add(StoryScene scene, string speaker, StorySide side, Texture2D portrait, string text, StoryEffect effect) =>
            scene.Lines.Add(new StoryLine { Speaker = speaker, Side = side, Portrait = portrait, Text = text, Effect = effect });
    }
}
