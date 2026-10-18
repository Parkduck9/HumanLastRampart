using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    // 0·3·4·5번 화면 공용 틀 (K-047, Docs/GDD/UI_NoSlop.md). 판은 양피지(임명장·보고서·장부), 정비 위쪽은 황동 계기 띠.
    // 부품은 Ui 한 벌만 쓴다. 단추 이름은 "btn:" + 열쇠 — 시험(PlayProbe)은 열쇠로 찾는다
    public sealed class ScreenView : IDisposable
    {
        readonly GameObject root;
        readonly CanvasGroup input;
        readonly RectTransform panel;
        readonly Image background;
        RectTransform actions;
        GameObject popup;
        Button firstButton;
        bool disposed;
        readonly bool storyPresentation;

        public ScreenView(Transform owner, string title, string description)
            : this(owner, title, description, false, null, null) { }

        // 스토리 전용 배치: 배경 그림 + 오른쪽 인물 + 아래 손그림 말풍선 대사
        public ScreenView(Transform owner, string title, string description, Texture2D character, string speaker)
            : this(owner, title, description, true, character, speaker) { }

        ScreenView(Transform owner, string title, string description, bool story, Texture2D character, string speaker)
        {
            storyPresentation = story;
            root = new GameObject("Screen UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            input = root.GetComponent<CanvasGroup>();
            SetInput(false);

            background = Ui.Image(root.transform, "Background", new Color(0.06f, 0.055f, 0.06f, 1f), true);
            Ui.Stretch(background.rectTransform);
            if (storyPresentation) panel = MakeStory(title, description, character, speaker);
            else
            {
                panel = MakePanel(root.transform, "Content");
                if (!string.IsNullOrEmpty(title))
                {
                    Text heading = Ui.Label(panel, "Title", title, UiTokens.H1, UiTokens.Ink, TextAnchor.MiddleLeft, true);
                    Ui.Size(heading, -1f, 64f);
                    Rule(panel);
                }

                if (!string.IsNullOrEmpty(description))
                {
                    Text body = Ui.Label(panel, "Description", description, UiTokens.Text, UiTokens.InkSoft, TextAnchor.UpperLeft);
                    Ui.Size(body, -1f, Mathf.Max(36f, body.preferredHeight));
                }
            }

        }

        bool appeared;

        // 그림 위주 화면(결과 보고서 등)용: 판 크기를 바꾸고, 세로 목록 안에 빈 구역을 하나 받아 직접 채운다
        public void SetPanelSize(float width, float height) { panel.sizeDelta = new Vector2(width, height); }

        public RectTransform AddArea(string name, float height)
        {
            var rect = Ui.Rect(panel, name);
            Ui.Size(rect, -1f, height);
            if (actions != null) actions.SetAsLastSibling();   // 단추 줄은 늘 맨 아래
            return rect;
        }

        public static Font UiFont => UiFonts.Body;

        // 정비 마당(D-076)용: 판을 화면 위쪽 황동 계기 띠로 바꾼다 (요약만 — AddArea로 채운다)
        public void DockTop(float width, float height)
        {
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -UiTokens.S32);
            panel.sizeDelta = new Vector2(width, height);
            Ui.SetPanel(panel.GetComponent<Image>(), UiPanel.Band);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 16, 16);
            layout.childAlignment = TextAnchor.MiddleLeft;
            if (actions != null) Ui.Size(actions, -1f, 0f).ignoreLayout = true;   // 숨긴 단추 줄은 띠 높이를 차지하지 않는다
        }

        public RectTransform AddBackdropLayer(string name)
        {
            var rect = Ui.Rect(root.transform, name);
            rect.SetSiblingIndex(1);   // 바탕(0) 바로 위, 판 아래
            Ui.Stretch(rect);
            return rect;
        }

        public CanvasGroup PanelGroup => panel.GetComponent<CanvasGroup>();

        // K-027 화면 배경 그림 (Art/UI/Resources/ScreenArt/이름). 화면을 덮도록(비율 유지, 넘치는 쪽은 잘림) 깐다. 없으면 false
        public bool SetBackdrop(string artName, float dim = 0f)
        {
            var texture = Resources.Load<Texture2D>("ScreenArt/" + artName);
            if (texture == null) return false;
            Backdrop(background.transform, texture, dim);
            return true;
        }

        static void Backdrop(Transform parent, Texture2D texture, float dim)
        {
            var obj = new GameObject("Backdrop Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            obj.transform.SetParent(parent, false);
            obj.transform.SetAsFirstSibling();
            var image = obj.GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;
            var fit = obj.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = (float)texture.width / Mathf.Max(1, texture.height);
            if (dim > 0f)
            {
                // 판 뒤 장면을 누르는 어둠 (가장자리가 더 어둡게 — 미니어처 흐림처럼 가운데로 눈이 간다)
                Image shade = Ui.Image(parent, "Backdrop Dim", UiTokens.Alpha(UiTokens.Night, dim));
                Ui.Stretch(shade.rectTransform);
                Image edge = Ui.Image(parent, "Backdrop Edge", UiTokens.Alpha(Color.black, Mathf.Min(1f, dim + 0.25f)));
                edge.sprite = UiArt.Get(UiArt.Vignette);
                Ui.Stretch(edge.rectTransform);
            }
        }

        // 메인 화면(K-027 배경)용: 판을 왼쪽 제목 여백으로 — 배경 오른쪽의 장교·빛을 가리지 않게
        public void DockLeft(float width, float height)
        {
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = new Vector2(128f, 0f);
            panel.sizeDelta = new Vector2(width, height);
        }

        // 단추를 보이지 않게 숨기되 누를 수 있는 상태로 둔다 — 마당 상호작용(E)과 시험이 같은 길로 부른다
        public static void HideButton(Button button)
        {
            var group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var element = button.GetComponent<LayoutElement>();
            if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            ((RectTransform)button.transform).sizeDelta = Vector2.zero;
        }

        public Button AddButton(string text, UnityAction action) => AddButton(text, text, action, false);

        // key = 시험이 찾는 열쇠, label = 보이는 글. primary면 황동 단추 (화면마다 하나)
        public Button AddButton(string key, string label, UnityAction action, bool primary)
        {
            Button button;
            if (storyPresentation)
            {
                button = Ui.Button(panel, key, label, () => action(), UiButtonKind.Primary, UiArt.IconPlay);
                Ui.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(-UiTokens.S32, UiTokens.S24), new Vector2(208f, 56f), new Vector2(1f, 0f));
            }
            else
            {
                if (actions == null)
                {
                    HorizontalLayoutGroup row = Ui.Row(panel, "Actions", UiTokens.S16, TextAnchor.MiddleRight);
                    actions = (RectTransform)row.transform;
                    Ui.Size(row, -1f, 64f);
                }

                button = Ui.Button(actions, key, label, () => action(), primary ? UiButtonKind.Primary : UiButtonKind.Normal);
                Ui.Size(button, primary ? 288f : 224f, 64f);
            }

            if (firstButton == null || primary) firstButton = button;
            return button;
        }

        public void SetInput(bool active)
        {
            input.interactable = active;
            input.blocksRaycasts = active;
            if (active && !appeared)
            {
                // 화면이 열릴 때 판이 밀려 올라온다 (S-24). 자리(DockTop·DockLeft)가 정해진 뒤에 시작한다
                appeared = true;
                UiAppear.Show(panel.gameObject, new Vector2(0f, -UiTokens.S24));
            }
        }

        public void FocusFirst() { Focus(firstButton); }

        // 난이도: 카드 두 장 (이름 + 한 줄). 돌아가기는 판 모서리의 닫기 표시
        public void ShowDifficulty(UnityAction easy, UnityAction realistic)
        {
            if (popup != null || disposed) return;
            panel.GetComponent<CanvasGroup>().interactable = false;
            Image shade = Ui.Shade(root.transform, "Difficulty Popup");
            popup = shade.gameObject;
            Image box = Ui.Panel(shade.transform, "Difficulty", UiPanel.Parchment);
            Ui.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 400f));
            UiAppear.Show(box.gameObject, new Vector2(0f, -UiTokens.S24));
            Text heading = Ui.Label(box.transform, "Title", "어떤 싸움으로 나설까", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(UiTokens.S48, -UiTokens.S48), new Vector2(700f, 48f), new Vector2(0f, 0.5f));
            Button close = Ui.IconButton(box.transform, "돌아가기", UiArt.IconClose, ClosePopup, 48f);
            Ui.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-UiTokens.S24, -UiTokens.S24), new Vector2(48f, 48f), new Vector2(1f, 1f));
            Button first = Card(box.transform, "쉬움", "쉬움", "적이 절반\n지도를 펴면 시간이 멈춤", -200f, easy);
            Card(box.transform, "현실", "현실", "적이 그대로\n지도를 펴도 시간이 흐름", 200f, realistic);
            Focus(first);
        }

        static Button Card(Transform parent, string key, string name, string line, float x, UnityAction action)
        {
            Button card = Ui.Button(parent, key, name, () => action());
            Ui.Place((RectTransform)card.transform, new Vector2(0.5f, 0f), new Vector2(x, UiTokens.S48), new Vector2(360f, 208f), new Vector2(0.5f, 0f));
            Text title = Ui.LabelOf(card);
            title.font = UiFonts.Display;
            title.fontSize = UiTokens.H2;
            title.color = UiTokens.Brass;
            title.alignment = TextAnchor.UpperCenter;
            title.rectTransform.offsetMax = new Vector2(-UiTokens.S16, -UiTokens.S32);
            Text detail = Ui.Label(card.transform.Find("Content"), "Detail", line, UiTokens.Text, UiTokens.Body, TextAnchor.LowerCenter);
            Ui.Stretch(detail.rectTransform, UiTokens.S32);
            return card;
        }

        void ClosePopup()
        {
            if (popup == null || disposed || !input.interactable) return;
            ClearSelection(popup.transform);
            Ui.Close(popup);
            popup = null;
            panel.GetComponent<CanvasGroup>().interactable = true;
            FocusFirst();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SetInput(false);
            ClearSelection(root.transform);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                button.onClick.RemoveAllListeners();
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
        }

        // 스토리: 배경 그림(없으면 어두운 바탕) + 오른쪽 인물 + 아래 손그림 말풍선(이름 나무판 + 대사)
        RectTransform MakeStory(string title, string description, Texture2D character, string speaker)
        {
            var art = Resources.Load<Texture2D>("ScreenArt/main");
            if (art != null) Backdrop(background.transform, art, 0.55f);

            if (character != null)
            {
                var holder = Ui.Rect(root.transform, "Story Character");
                holder.anchorMin = new Vector2(0.58f, 0.30f);
                holder.anchorMax = new Vector2(0.90f, 0.96f);
                holder.offsetMin = holder.offsetMax = Vector2.zero;
                var obj = new GameObject("Character", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
                obj.transform.SetParent(holder, false);
                var image = obj.GetComponent<RawImage>();
                image.texture = character;
                image.color = Color.white;
                image.raycastTarget = false;
                var fit = obj.GetComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)character.width / Mathf.Max(1, character.height);
            }

            Image bubble = Ui.Panel(root.transform, "Story Dialogue", UiPanel.Bubble);
            var rect = bubble.rectTransform;
            rect.anchorMin = new Vector2(0.06f, 0.05f);
            rect.anchorMax = new Vector2(0.94f, 0.31f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            bubble.gameObject.AddComponent<CanvasGroup>();
            // 말풍선 꼬리: 오른쪽 인물 쪽으로
            Image tail = Ui.Image(rect, "Tail", Color.white);
            tail.sprite = UiArt.Get(UiArt.BubbleTail);
            // 그림은 위가 넓고 아래 왼쪽이 뾰족하다 → 위아래·좌우를 뒤집어 넓은 쪽을 말풍선 윗변에 붙이고 끝이 오른쪽 위를 향하게
            Ui.Place(tail.rectTransform, new Vector2(0.78f, 1f), new Vector2(0f, -UiTokens.Dot * 2f), UiArt.Size(tail.sprite, 2f), new Vector2(0.5f, 1f));
            tail.rectTransform.localScale = new Vector3(-1f, -1f, 1f);

            if (!string.IsNullOrEmpty(speaker))
            {
                Image plate = Ui.Panel(rect, "Speaker Plate", UiPanel.Plate);
                Ui.Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(UiTokens.S48, UiTokens.S16), new Vector2(0f, 56f), new Vector2(0f, 0f));
                var row = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(24, 24, 8, 8);
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = false;
                plate.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                Text name = Ui.Label(plate.transform, "Story Speaker", speaker, UiTokens.Text, UiTokens.BrassLight, TextAnchor.MiddleCenter, true);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            Text body = Ui.Label(rect, "Story Line", description, UiTokens.H2, UiTokens.Ink, TextAnchor.UpperLeft);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(UiTokens.S48, 96f);
            body.rectTransform.offsetMax = new Vector2(-UiTokens.S48, -UiTokens.S48);
            return rect;
        }

        static RectTransform MakePanel(Transform parent, string name)
        {
            Image image = Ui.Panel(parent, name, UiPanel.Parchment);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(960f, 720f);
            image.gameObject.AddComponent<CanvasGroup>();
            var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 48, 48);
            layout.spacing = UiTokens.S24;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        // 제목 아래 잉크 한 줄 (한 점 = 3px)
        static void Rule(Transform parent)
        {
            Image line = Ui.Image(parent, "Rule", UiTokens.Alpha(UiTokens.Ink, 0.55f));
            Ui.Size(line, -1f, UiTokens.Dot);
        }

        static void Focus(Button button)
        {
            if (button != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        static void ClearSelection(Transform owner)
        {
            var system = EventSystem.current;
            if (system != null && system.currentSelectedGameObject != null &&
                system.currentSelectedGameObject.transform.IsChildOf(owner))
                system.SetSelectedGameObject(null);
        }
    }
}
