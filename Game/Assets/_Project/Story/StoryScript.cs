using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using UnityEngine;

namespace HumanLastRampart.Story
{
    // 이야기 한 편 (K-050, 5번 스토리 화면). 장면 목록 → 장면마다 배경과 대사 줄 목록.
    // 에셋 위치: Story/Resources/Stories/{Id}.asset — StoryRequest.StoryId(story_intro, story_defeat)와 파일 이름이 같다.
    // 사용자가 편집기 창(메뉴 HLR/스토리 제작 도구)으로 직접 쓴다. 그림은 텍스처 그대로 끌어다 놓는다.
    [CreateAssetMenu(menuName = "HLR/스토리/이야기", fileName = "story_new")]
    public sealed class StoryScript : ScriptableObject
    {
        [SerializeField] string id = "story_new";            // StoryRequest.StoryId와 같게
        [SerializeField] string title = "새 이야기";          // 도구에서만 보이는 이름
        [SerializeField] StoryKind kind = StoryKind.Normal;  // 패배 엔딩이면 DefeatEnding
        [SerializeField, TextArea] string note = "";         // 작가 메모 (화면에 안 나옴)
        [SerializeField] List<StoryScene> scenes = new List<StoryScene>();

        public string Id { get => id; set => id = value; }
        public string Title { get => title; set => title = value; }
        public StoryKind Kind { get => kind; set => kind = value; }
        public string Note { get => note; set => note = value; }
        public List<StoryScene> Scenes => scenes;

        public int LineCount
        {
            get
            {
                int count = 0;
                foreach (StoryScene scene in scenes) count += scene.Lines.Count;
                return count;
            }
        }

        // 재생 순서대로 (장면, 줄) 펼치기
        public List<(StoryScene scene, StoryLine line)> Flatten()
        {
            var list = new List<(StoryScene, StoryLine)>();
            foreach (StoryScene scene in scenes)
                foreach (StoryLine line in scene.Lines)
                    list.Add((scene, line));
            return list;
        }

        // 저장 전·재생 전 검사. 문제 줄 위치를 한글로 돌려준다 (비면 문제 없음)
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(id)) problems.Add("이야기 ID가 비었습니다");
            if (LineCount == 0) problems.Add("대사 줄이 하나도 없습니다");
            for (int s = 0; s < scenes.Count; s++)
            {
                StoryScene scene = scenes[s];
                for (int l = 0; l < scene.Lines.Count; l++)
                {
                    StoryLine line = scene.Lines[l];
                    string where = $"장면 {s + 1} · {l + 1}줄";
                    if (string.IsNullOrWhiteSpace(line.Text)) problems.Add($"{where}: 대사가 비었습니다");
                    if (line.Side != StorySide.None && string.IsNullOrWhiteSpace(line.Speaker)) problems.Add($"{where}: 말하는 사람 이름이 비었습니다");
                }
            }

            return problems;
        }
    }

    [Serializable]
    public sealed class StoryScene
    {
        [SerializeField] string name = "새 장면";             // 도구에서만 보이는 이름
        [SerializeField] Texture2D background;                // 없으면 어두운 바탕
        [SerializeField] List<StoryLine> lines = new List<StoryLine>();

        public string Name { get => name; set => name = value; }
        public Texture2D Background { get => background; set => background = value; }
        public List<StoryLine> Lines => lines;
    }

    // 말하는 사람이 서는 쪽. None = 내레이션 (이름표 없음, 인물 모두 어둡게)
    public enum StorySide { None, Left, Right }

    // 줄 효과: 흔들림 = 화면이 잠깐 흔들림, 암전 = 까맣게 닫혔다가 이 줄과 함께 열림
    public enum StoryEffect { None, Shake, FadeBlack }

    [Serializable]
    public sealed class StoryLine
    {
        [SerializeField] string speaker = "";
        [SerializeField] StorySide side = StorySide.Left;
        [SerializeField] Texture2D portrait;                  // 없으면 그 쪽에 서 있던 인물 그대로
        [SerializeField] int cell;                            // 표정 칸: 256×384 칸 격자에서 왼쪽 위부터 번호 (0부터)
        [SerializeField, TextArea(2, 5)] string text = "";
        [SerializeField] Texture2D background;                // 없으면 장면 배경
        [SerializeField] StoryEffect effect = StoryEffect.None;

        public string Speaker { get => speaker; set => speaker = value; }
        public StorySide Side { get => side; set => side = value; }
        public Texture2D Portrait { get => portrait; set => portrait = value; }
        public int Cell { get => cell; set => cell = value; }
        public string Text { get => text; set => text = value; }
        public Texture2D Background { get => background; set => background = value; }
        public StoryEffect Effect { get => effect; set => effect = value; }

        public StoryLine Copy() => (StoryLine)MemberwiseClone();
    }

    // 인물 그림 시트의 칸 (기존 그림 규칙: 한 칸 256×384)
    public static class StoryCells
    {
        public const float CellWidth = 256f, CellHeight = 384f;

        public static int Columns(Texture texture) => texture == null ? 1 : Mathf.Max(1, Mathf.RoundToInt(texture.width / CellWidth));
        public static int Rows(Texture texture) => texture == null ? 1 : Mathf.Max(1, Mathf.RoundToInt(texture.height / CellHeight));
        public static int Count(Texture texture) => Columns(texture) * Rows(texture);

        // RawImage.uvRect / GUI.DrawTextureWithTexCoords용 (텍스처 좌표는 아래가 0)
        public static Rect Uv(Texture texture, int cell)
        {
            int cols = Columns(texture), rows = Rows(texture);
            cell = Mathf.Clamp(cell, 0, cols * rows - 1);
            int col = cell % cols, row = cell / cols;
            return new Rect((float)col / cols, 1f - (row + 1f) / rows, 1f / cols, 1f / rows);
        }
    }
}
