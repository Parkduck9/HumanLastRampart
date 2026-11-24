using System.Collections.Generic;
using System.IO;
using HumanLastRampart.Core.Campaign;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Story.Editor
{
    // 스토리 제작 도구 (K-050): 메뉴 HLR/스토리 제작 도구.
    // 왼쪽 = 이야기 고르기 · 장면 목록 · 줄 목록(추가·복사·삭제·위아래), 가운데 = 고른 줄 편집, 아래 = 게임 화면처럼 미리보기.
    // 바꾸는 것마다 되돌리기(Ctrl+Z)가 된다. [저장]을 누르면 에셋 파일에 쓴다.
    public sealed class StoryEditorWindow : EditorWindow
    {
        const string Folder = "Assets/_Project/Story/Resources/Stories";
        static readonly string[] SideNames = { "내레이션", "왼쪽", "오른쪽" };
        static readonly string[] EffectNames = { "없음", "흔들림", "암전" };
        static readonly string[] KindNames = { "보통 이야기", "패배 엔딩", "1회차 완료" };

        StoryScript script;
        int sceneIndex, lineIndex;
        Vector2 listScroll, editScroll;
        List<string> problems = new List<string>();
        bool showStoryInfo;

        [MenuItem("HLR/스토리 제작 도구")]
        public static void Open() => GetWindow<StoryEditorWindow>("스토리 제작").minSize = new Vector2(980f, 640f);

        void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
            if (script == null)
            {
                var all = FindStories();
                if (all.Count > 0) script = all[0];
            }
        }

        void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        static List<StoryScript> FindStories()
        {
            var list = new List<StoryScript>();
            foreach (string guid in AssetDatabase.FindAssets("t:StoryScript"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<StoryScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }

            return list;
        }

        void OnGUI()
        {
            DrawToolbar();
            if (script == null)
            {
                EditorGUILayout.HelpBox("이야기를 고르거나 [새 이야기]를 누르세요.", MessageType.Info);
                return;
            }

            ClampSelection();
            EditorGUILayout.BeginHorizontal();
            DrawLists();
            EditorGUILayout.BeginVertical();
            editScroll = EditorGUILayout.BeginScrollView(editScroll);
            DrawStoryInfo();
            DrawScene();
            DrawLine();
            EditorGUILayout.EndScrollView();
            DrawPreview();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // ── 위 띠: 이야기 고르기 · 새로 만들기 · 검사 · 저장 ──
        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var stories = FindStories();
            var names = new string[stories.Count];
            int current = -1;
            for (int i = 0; i < stories.Count; i++)
            {
                names[i] = $"{stories[i].Title}  ({stories[i].Id})";
                if (stories[i] == script) current = i;
            }

            int picked = EditorGUILayout.Popup(current, names, EditorStyles.toolbarPopup, GUILayout.Width(320f));
            if (picked != current && picked >= 0) { script = stories[picked]; sceneIndex = lineIndex = 0; problems.Clear(); }
            if (GUILayout.Button("새 이야기", EditorStyles.toolbarButton, GUILayout.Width(80f))) CreateStory();
            GUILayout.FlexibleSpace();
            if (script != null)
            {
                if (GUILayout.Button("검사", EditorStyles.toolbarButton, GUILayout.Width(60f))) problems = script.Validate();
                if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60f))) Save();
            }

            EditorGUILayout.EndHorizontal();
            foreach (string problem in problems) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        void CreateStory()
        {
            Directory.CreateDirectory(Folder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/story_new.asset");
            var asset = CreateInstance<StoryScript>();
            asset.Id = Path.GetFileNameWithoutExtension(path);
            asset.Title = "새 이야기";
            var scene = new StoryScene { Name = "첫 장면" };
            scene.Lines.Add(new StoryLine { Side = StorySide.None, Text = "여기에 첫 대사를 쓰세요." });
            asset.Scenes.Add(scene);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            script = asset;
            sceneIndex = lineIndex = 0;
        }

        void Save()
        {
            problems = script.Validate();
            // 파일 이름과 ID를 맞춘다 — 게임은 ID로 Resources/Stories/{ID}를 찾는다
            string path = AssetDatabase.GetAssetPath(script);
            if (!string.IsNullOrWhiteSpace(script.Id) && Path.GetFileNameWithoutExtension(path) != script.Id)
            {
                string error = AssetDatabase.RenameAsset(path, script.Id);
                if (!string.IsNullOrEmpty(error)) problems.Add("파일 이름을 ID로 바꾸지 못했습니다: " + error);
            }

            EditorUtility.SetDirty(script);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent(problems.Count == 0 ? "저장했습니다" : $"저장했습니다 — 확인할 곳 {problems.Count}개"));
        }

        // ── 왼쪽: 장면·줄 목록 ──
        void DrawLists()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330f));
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            EditorGUILayout.LabelField("장면", EditorStyles.boldLabel);
            for (int s = 0; s < script.Scenes.Count; s++)
            {
                bool selected = s == sceneIndex;
                if (GUILayout.Toggle(selected, $"{s + 1}. {script.Scenes[s].Name}  · {script.Scenes[s].Lines.Count}줄", "Button") && !selected)
                { sceneIndex = s; lineIndex = 0; GUI.FocusControl(null); }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("장면 추가")) Change("장면 추가", () =>
            {
                var scene = new StoryScene { Name = "새 장면" };
                if (script.Scenes.Count > 0) scene.Background = script.Scenes[sceneIndex].Background;
                scene.Lines.Add(new StoryLine());
                script.Scenes.Insert(Mathf.Min(sceneIndex + 1, script.Scenes.Count), scene);
                sceneIndex = Mathf.Min(sceneIndex + 1, script.Scenes.Count - 1); lineIndex = 0;
            });
            GUI.enabled = script.Scenes.Count > 1;
            if (GUILayout.Button("장면 삭제") && EditorUtility.DisplayDialog("장면 삭제", $"'{script.Scenes[sceneIndex].Name}' 장면과 대사 {script.Scenes[sceneIndex].Lines.Count}줄을 지울까요?", "지우기", "그만두기"))
                Change("장면 삭제", () => { script.Scenes.RemoveAt(sceneIndex); sceneIndex = Mathf.Max(0, sceneIndex - 1); lineIndex = 0; });
            GUI.enabled = sceneIndex > 0;
            if (GUILayout.Button("▲", GUILayout.Width(28f))) Change("장면 위로", () => { Swap(script.Scenes, sceneIndex, sceneIndex - 1); sceneIndex--; });
            GUI.enabled = sceneIndex < script.Scenes.Count - 1;
            if (GUILayout.Button("▼", GUILayout.Width(28f))) Change("장면 아래로", () => { Swap(script.Scenes, sceneIndex, sceneIndex + 1); sceneIndex++; });
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("대사 줄", EditorStyles.boldLabel);
            if (script.Scenes.Count > 0)
            {
                List<StoryLine> lines = script.Scenes[sceneIndex].Lines;
                for (int l = 0; l < lines.Count; l++)
                {
                    StoryLine line = lines[l];
                    string who = line.Side == StorySide.None ? "(내레이션)" : string.IsNullOrWhiteSpace(line.Speaker) ? "(이름 없음)" : line.Speaker;
                    string text = string.IsNullOrEmpty(line.Text) ? "" : line.Text.Replace('\n', ' ');
                    if (text.Length > 22) text = text.Substring(0, 22) + "…";
                    var style = new GUIStyle("Button") { alignment = TextAnchor.MiddleLeft };
                    bool selected = l == lineIndex;
                    if (GUILayout.Toggle(selected, $"{l + 1}. {who}: {text}", style) && !selected) { lineIndex = l; GUI.FocusControl(null); }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("줄 추가")) Change("줄 추가", () =>
                {
                    // 새 줄은 지금 줄의 말하는 사람·쪽·그림을 이어받는다 (같은 사람이 이어 말할 때 편하게)
                    var line = lines.Count > 0 ? lines[lineIndex].Copy() : new StoryLine();
                    line.Text = "";
                    line.Effect = StoryEffect.None;
                    lines.Insert(Mathf.Min(lineIndex + 1, lines.Count), line);
                    lineIndex = Mathf.Min(lineIndex + 1, lines.Count - 1);
                });
                GUI.enabled = lines.Count > 0;
                if (GUILayout.Button("복사")) Change("줄 복사", () => { lines.Insert(lineIndex + 1, lines[lineIndex].Copy()); lineIndex++; });
                GUI.enabled = lines.Count > 1;
                if (GUILayout.Button("삭제")) Change("줄 삭제", () => { lines.RemoveAt(lineIndex); lineIndex = Mathf.Max(0, lineIndex - 1); });
                GUI.enabled = lineIndex > 0;
                if (GUILayout.Button("▲", GUILayout.Width(28f))) Change("줄 위로", () => { Swap(lines, lineIndex, lineIndex - 1); lineIndex--; });
                GUI.enabled = lineIndex < lines.Count - 1;
                if (GUILayout.Button("▼", GUILayout.Width(28f))) Change("줄 아래로", () => { Swap(lines, lineIndex, lineIndex + 1); lineIndex++; });
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ── 이야기 정보 (접힘) ──
        void DrawStoryInfo()
        {
            showStoryInfo = EditorGUILayout.Foldout(showStoryInfo, $"이야기 정보 — {script.Title}", true);
            if (!showStoryInfo) return;
            EditorGUI.indentLevel++;
            string id = EditorGUILayout.TextField(new GUIContent("ID", "게임이 찾는 이름. 첫 이야기 = story_intro, 패배 엔딩 = story_defeat"), script.Id);
            string title = EditorGUILayout.TextField("제목 (도구에서만)", script.Title);
            int kind = EditorGUILayout.Popup("종류", (int)script.Kind, KindNames);
            EditorGUILayout.LabelField("작가 메모 (화면에 안 나옴)");
            string note = EditorGUILayout.TextArea(script.Note, GUILayout.MinHeight(40f));
            if (id != script.Id || title != script.Title || kind != (int)script.Kind || note != script.Note)
                Change("이야기 정보", () => { script.Id = id; script.Title = title; script.Kind = (StoryKind)kind; script.Note = note; });
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(6f);
        }

        void DrawScene()
        {
            if (script.Scenes.Count == 0) return;
            StoryScene scene = script.Scenes[sceneIndex];
            EditorGUILayout.LabelField($"장면 {sceneIndex + 1}", EditorStyles.boldLabel);
            string name = EditorGUILayout.TextField("장면 이름", scene.Name);
            var background = (Texture2D)EditorGUILayout.ObjectField("배경 그림", scene.Background, typeof(Texture2D), false);
            if (name != scene.Name || background != scene.Background)
                Change("장면 편집", () => { scene.Name = name; scene.Background = background; });
            EditorGUILayout.Space(6f);
        }

        // ── 고른 줄 편집 ──
        void DrawLine()
        {
            if (script.Scenes.Count == 0 || script.Scenes[sceneIndex].Lines.Count == 0) return;
            StoryLine line = script.Scenes[sceneIndex].Lines[lineIndex];
            EditorGUILayout.LabelField($"{lineIndex + 1}번째 줄", EditorStyles.boldLabel);

            int side = GUILayout.Toolbar((int)line.Side, SideNames);
            string speaker = line.Speaker;
            Texture2D portrait = line.Portrait;
            int cell = line.Cell;
            if (side != (int)StorySide.None)
            {
                EditorGUILayout.BeginHorizontal();
                speaker = EditorGUILayout.TextField("말하는 사람", line.Speaker);
                // 이 이야기에 이미 나온 이름 바로 고르기 (그 사람의 마지막 그림·표정도 함께)
                foreach (var known in KnownSpeakers())
                    if (GUILayout.Button(known.name, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    { speaker = known.name; if (known.portrait != null) { portrait = known.portrait; cell = known.cell; } }
                EditorGUILayout.EndHorizontal();
                portrait = (Texture2D)EditorGUILayout.ObjectField("인물 그림", portrait, typeof(Texture2D), false);
                if (portrait != null) cell = CellPicker(portrait, cell);
                else EditorGUILayout.HelpBox("그림이 없으면 그 쪽에 서 있던 인물이 그대로 남습니다.", MessageType.None);
            }

            EditorGUILayout.LabelField("대사");
            var area = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 14 };
            string text = EditorGUILayout.TextArea(line.Text, area, GUILayout.MinHeight(70f));
            int effect = GUILayout.Toolbar((int)line.Effect, EffectNames);
            var background = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("이 줄만 배경", "비우면 장면 배경"), line.Background, typeof(Texture2D), false);

            if (side != (int)line.Side || speaker != line.Speaker || portrait != line.Portrait || cell != line.Cell || text != line.Text
                || effect != (int)line.Effect || background != line.Background)
                Change("줄 편집", () =>
                {
                    line.Side = (StorySide)side; line.Speaker = speaker; line.Portrait = portrait; line.Cell = cell;
                    line.Text = text; line.Effect = (StoryEffect)effect; line.Background = background;
                });
        }

        IEnumerable<(string name, Texture2D portrait, int cell)> KnownSpeakers()
        {
            var seen = new Dictionary<string, (Texture2D, int)>();
            foreach (var (_, line) in script.Flatten())
                if (!string.IsNullOrWhiteSpace(line.Speaker))
                {
                    if (!seen.ContainsKey(line.Speaker) || line.Portrait != null)
                        seen[line.Speaker] = (line.Portrait != null ? line.Portrait : seen.TryGetValue(line.Speaker, out var old) ? old.Item1 : null, line.Cell);
                }

            foreach (var pair in seen) yield return (pair.Key, pair.Value.Item1, pair.Value.Item2);
        }

        // 표정 칸 고르기: 시트를 칸으로 나눠 작은 그림 단추로
        static int CellPicker(Texture2D sheet, int cell)
        {
            int cols = StoryCells.Columns(sheet), count = StoryCells.Count(sheet);
            if (count <= 1) return 0;
            EditorGUILayout.LabelField($"표정 칸 ({count}칸 중 {cell + 1}번)");
            const float w = 48f, h = 72f;
            for (int start = 0; start < count; start += cols)
            {
                Rect row = GUILayoutUtility.GetRect(cols * (w + 4f), h + 4f, GUILayout.ExpandWidth(false));
                for (int c = 0; c < cols && start + c < count; c++)
                {
                    int index = start + c;
                    var r = new Rect(row.x + c * (w + 4f), row.y, w, h);
                    EditorGUI.DrawRect(r, index == cell ? new Color(0.84f, 0.69f, 0.42f) : new Color(0.18f, 0.18f, 0.18f));
                    GUI.DrawTextureWithTexCoords(new Rect(r.x + 2f, r.y + 2f, w - 4f, h - 4f), sheet, StoryCells.Uv(sheet, index));
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) cell = index;
                }
            }

            return Mathf.Clamp(cell, 0, count - 1);
        }

        // ── 미리보기: 게임 화면 모양 그대로(16:9) — 이 줄까지의 장면 상태 ──
        void DrawPreview()
        {
            if (script.Scenes.Count == 0 || script.Scenes[sceneIndex].Lines.Count == 0) return;
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
            float width = Mathf.Min(position.width - 360f, 720f);
            Rect frame = GUILayoutUtility.GetRect(width, width * 9f / 16f, GUILayout.ExpandWidth(false));
            float k = frame.width / 1920f;
            StoryScene scene = script.Scenes[sceneIndex];
            StoryLine line = scene.Lines[lineIndex];
            EditorGUI.DrawRect(frame, new Color(0.06f, 0.07f, 0.08f));
            Texture2D back = line.Background != null ? line.Background : scene.Background;
            if (back != null) GUI.DrawTexture(frame, back, ScaleMode.ScaleAndCrop);

            // 이 장면에서 이 줄까지 좌우에 서 있는 인물
            var stand = new (Texture2D tex, int cell)[2];
            for (int l = 0; l <= lineIndex; l++)
            {
                StoryLine before = scene.Lines[l];
                if (before.Side != StorySide.None && before.Portrait != null) stand[before.Side == StorySide.Left ? 0 : 1] = (before.Portrait, before.Cell);
            }

            for (int i = 0; i < 2; i++)
            {
                if (stand[i].tex == null) continue;
                bool speaking = (int)line.Side == i + 1;
                float pw = 512f * k, ph = 768f * k, cx = frame.x + frame.width * (i == 0 ? 0.25f : 0.75f);
                var r = new Rect(cx - pw / 2f, frame.yMax - (140f + (speaking ? 12f : 0f)) * k - ph, pw, ph);
                GUI.color = speaking ? Color.white : new Color(0.42f, 0.42f, 0.48f);
                GUI.DrawTextureWithTexCoords(r, stand[i].tex, StoryCells.Uv(stand[i].tex, stand[i].cell));
                GUI.color = Color.white;
            }

            // 대사 상자 (게임과 같은 자리·색)
            var box = new Rect(frame.x + frame.width * 0.06f, frame.yMax - (32f + 264f) * k, frame.width * 0.88f, 264f * k);
            EditorGUI.DrawRect(box, new Color(0.227f, 0.165f, 0.118f));
            EditorGUI.DrawRect(new Rect(box.x + 6f * k, box.y + 6f * k, box.width - 12f * k, box.height - 12f * k), new Color(0.91f, 0.85f, 0.71f));
            var ink = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = Mathf.Max(9, Mathf.RoundToInt(32f * k)), normal = { textColor = new Color(0.227f, 0.165f, 0.118f) } };
            GUI.Label(new Rect(box.x + 64f * k, box.y + 48f * k, box.width - 160f * k, box.height - 96f * k), line.Text, ink);
            if (line.Side != StorySide.None && !string.IsNullOrWhiteSpace(line.Speaker))
            {
                var plate = new Rect(box.x + 48f * k, box.y - 28f * k, 240f * k, 56f * k);
                EditorGUI.DrawRect(plate, new Color(0.231f, 0.165f, 0.122f));
                var brass = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(9, Mathf.RoundToInt(24f * k)), normal = { textColor = new Color(0.835f, 0.694f, 0.42f) } };
                GUI.Label(plate, line.Speaker, brass);
            }

            if (line.Effect != StoryEffect.None)
                GUI.Label(new Rect(frame.x + 8f, frame.y + 6f, 200f, 18f), line.Effect == StoryEffect.Shake ? "효과: 흔들림" : "효과: 암전 뒤 열림", EditorStyles.whiteMiniLabel);
            EditorGUILayout.LabelField("게임에서는 글이 한 자씩 나오고, 말하는 쪽 인물이 밝아집니다. 실제 재생은 Play로 확인하세요.", EditorStyles.miniLabel);
        }

        // 바꾸기 전에 되돌리기 기록 + 저장 표시
        void Change(string label, System.Action apply)
        {
            Undo.RecordObject(script, label);
            apply();
            EditorUtility.SetDirty(script);
            problems.Clear();
        }

        void ClampSelection()
        {
            sceneIndex = Mathf.Clamp(sceneIndex, 0, Mathf.Max(0, script.Scenes.Count - 1));
            int lines = script.Scenes.Count > 0 ? script.Scenes[sceneIndex].Lines.Count : 0;
            lineIndex = Mathf.Clamp(lineIndex, 0, Mathf.Max(0, lines - 1));
        }

        static void Swap<T>(List<T> list, int a, int b) { T t = list[a]; list[a] = list[b]; list[b] = t; }
    }
}
