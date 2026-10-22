using System;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Tutorial
{
    // 튜토리얼 화면: 장교 흉상(단계별 표정) · 안내 판 · 조작 표시 · 강조 고리와 화살표 · 8칸 진행 표시 · [다음]/[튜토리얼 건너뛰기].
    // 자기 캔버스(맨 위)에 그린다. 그림은 Resources/HudArt/Tutorial (K-097, K-093) — 없으면 색 판으로 대신한다.
    // 1920×1080 기준. 판 바깥(고리 등)은 클릭을 막지 않는다
    public sealed class TutorialView : IDisposable
    {
        const string Art = "HudArt/Tutorial/";
        readonly GameObject root;
        readonly RectTransform canvasRect;
        readonly RawImage bust, ring, pointer, icon;
        readonly Text title, body;
        readonly RectTransform chipRow;
        readonly RawImage[] pips = new RawImage[TutorialState.Total];
        readonly Texture2D pipTex, ringTex, chipTex;
        readonly Texture2D[] pointerTex = new Texture2D[4];   // 위·오른쪽·아래·왼쪽
        readonly GameObject boxObject;
        public readonly Button NextButton, SkipButton;
        int shownStep = -1;
        bool shownExplain;
        bool disposed;

        static Texture2D Tex(string name)
        {
            var texture = Resources.Load<Texture2D>(Art + name);
            if (texture != null) texture.filterMode = FilterMode.Point;
            return texture;
        }

        static void Cell(RawImage image, int index, int count) => image.uvRect = new Rect(index / (float)count, 0f, 1f / count, 1f);

        RawImage Sheet(Transform parent, string name, Texture2D texture, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = texture != null;
            ((RectTransform)go.transform).sizeDelta = size;
            return image;
        }

        public TutorialView(Transform owner, Action onNext, Action onSkip)
        {
            root = new GameObject("Tutorial UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)root.transform;

            // 고리·화살표 (판 뒤쪽 레이어)
            ringTex = Tex("highlight_ring_pulse");
            ring = Sheet(root.transform, "Ring", ringTex, new Vector2(192f, 192f));
            for (int i = 0; i < 4; i++) pointerTex[i] = Tex(new[] { "pointer_bounce_up", "pointer_bounce_right", "pointer_bounce_down", "pointer_bounce_left" }[i]);
            pointer = Sheet(root.transform, "Pointer", pointerTex[2], new Vector2(96f, 96f));
            ring.gameObject.SetActive(false);
            pointer.gameObject.SetActive(false);

            // 8칸 진행 표시 (위 가운데)
            pipTex = Tex("step_pips");
            RectTransform strip = Ui.Rect(root.transform, "Step Pips");
            Ui.Place(strip, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(8 * 56f, 56f), new Vector2(0.5f, 1f));
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i] = Sheet(strip, "Pip " + i, pipTex, new Vector2(56f, 56f));
                Ui.Place(pips[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(i * 56f, 0f), new Vector2(56f, 56f), new Vector2(0f, 0.5f));
            }

            // 안내 판: 왼쪽 아래
            boxObject = new GameObject("Tutorial Box", typeof(RectTransform));
            boxObject.transform.SetParent(root.transform, false);
            var box = (RectTransform)boxObject.transform;
            // 아래 가운데, 왼쪽 아래 키 안내·맨 아래 띠보다 위
            Ui.Place(box, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1100f, 230f), new Vector2(0.5f, 0f));

            bust = Sheet(box, "Officer", Tex("officer_expressions"), new Vector2(144f, 144f));
            Ui.Place(bust.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(144f, 144f), new Vector2(0f, 0f));

            Image panel = Ui.Image(box, "Panel", Color.white, false);   // 판 자체는 클릭을 막지 않는다 (아래 단추만 받는다)
            Sprite sprite = UiArt.Hud("Tutorial/instruction_box_9slice", new Vector4(24f, 24f, 24f, 24f));
            if (sprite != null) { panel.sprite = sprite; panel.type = Image.Type.Sliced; }
            else panel.color = UiTokens.Paper;
            Ui.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(150f, 0f), new Vector2(950f, 230f), new Vector2(0f, 0f));

            icon = Sheet(panel.transform, "Step Icon", Tex("step_icons_24"), new Vector2(60f, 60f));
            Ui.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(34f, -30f), new Vector2(60f, 60f), new Vector2(0f, 1f));
            title = Ui.Label(panel.transform, "Title", "", 30, UiTokens.Ink, TextAnchor.MiddleLeft);
            HumanLastRampart.UI.UiFonts.Bold(title);
            Ui.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(106f, -26f), new Vector2(520f, 44f), new Vector2(0f, 1f));
            body = Ui.Label(panel.transform, "Body", "", UiTokens.Text, UiTokens.Ink, TextAnchor.UpperLeft);
            Ui.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(34f, -82f), new Vector2(880f, 96f), new Vector2(0f, 1f));

            chipTex = Tex("input_chips_32");
            HorizontalLayoutGroup row = Ui.Row(panel.transform, "Chips", 12f);
            chipRow = (RectTransform)row.transform;
            Ui.Place(chipRow, new Vector2(0f, 0f), new Vector2(34f, 22f), new Vector2(640f, 54f), new Vector2(0f, 0f));

            NextButton = Ui.Button(panel.transform, "튜토리얼 다음", "다음", onNext, UiButtonKind.Primary);
            Ui.Place((RectTransform)NextButton.transform, new Vector2(1f, 0f), new Vector2(-34f, 22f), new Vector2(200f, 56f), new Vector2(1f, 0f));
            SkipButton = Ui.Button(panel.transform, "튜토리얼 건너뛰기", "튜토리얼 건너뛰기", onSkip);
            Ui.Place((RectTransform)SkipButton.transform, new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(300f, 48f), new Vector2(1f, 1f));
        }

        // 매 프레임: 지금 단계의 글·표시를 맞추고 고리를 목표 위에 놓는다
        public void Refresh(TutorialMachine machine, ITutorialSource source)
        {
            if (disposed) return;
            bool visible = !machine.Finished;
            root.SetActive(visible);
            if (!visible) return;

            TutorialStepInfo info = machine.Info;
            int step = (int)machine.Current;
            bool explain = machine.ExplainOnly;   // 직접 해 볼 수 없어 설명으로 바뀐 단계도 포함
            if (step != shownStep || explain != shownExplain)
            {
                bool stepChanged = step != shownStep;
                shownStep = step;
                shownExplain = explain;
                title.text = info.Title;
                body.text = explain && !string.IsNullOrEmpty(info.ExplainBody) ? info.ExplainBody : info.Body;
                Cell(bust, info.Expression, 4);
                Cell(icon, step, 8);
                NextButton.gameObject.SetActive(explain);
                if (stepChanged) BuildChips(info);
                // 진행 표시는 단계가 바뀔 때만 다시 맞춘다 (매 프레임 uvRect를 다시 쓰지 않는다)
                for (int i = 0; i < pips.Length; i++) Cell(pips[i], i * 3 + (i < step ? 2 : i == step ? 1 : 0), 24);
            }

            // 목표 화면 좌표(화소)를 화면 비율로 바꿔 앵커에 건다 — 캔버스 배율·피벗과 무관하다 (BattleHud.Viewport와 같은 방식)
            bool target = false;
            Vector2 screen = Vector2.zero;
            if (info.Pointer && ringTex != null && source.TryGetTargetScreen(machine.Current, out screen))
                target = Screen.width > 0 && Screen.height > 0;
            ring.gameObject.SetActive(target);
            pointer.gameObject.SetActive(target && pointerTex[2] != null);
            if (target)
            {
                float t = Time.unscaledTime;
                Cell(ring, (int)(t * 8f) % 6, 6);
                var viewport = new Vector2(screen.x / Screen.width, screen.y / Screen.height);
                RectTransform ringRect = ring.rectTransform, pointerRect = pointer.rectTransform;
                ringRect.anchorMin = ringRect.anchorMax = viewport;
                ringRect.anchoredPosition = Vector2.zero;
                pointerRect.anchorMin = pointerRect.anchorMax = viewport;
                pointerRect.anchoredPosition = new Vector2(0f, 150f);
                Cell(pointer, (int)(t * 6f) % 4, 4);
            }
        }

        void BuildChips(TutorialStepInfo info)
        {
            for (int i = chipRow.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(chipRow.GetChild(i).gameObject);
            if (info.Chips == null) return;
            foreach (TutorialStepInfo.Chip chip in info.Chips)
            {
                Image back = Ui.Image(chipRow, "Chip", Color.white);
                Sprite sprite = UiArt.Hud("Tutorial/hint_chip_9slice", new Vector4(24f, 24f, 24f, 24f));
                if (sprite != null) { back.sprite = sprite; back.type = Image.Type.Sliced; }
                else back.color = UiTokens.Wood;
                var layout = back.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(18, 18, 6, 6);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                Ui.Size(back, -1f, 54f);
                if (chip.Icon >= 0 && chipTex != null)
                {
                    RawImage picture = Sheet(back.transform, "Chip Icon", chipTex, new Vector2(42f, 42f));
                    Cell(picture, chip.Icon, 5);
                    Ui.Size(picture, 42f, 42f);
                }

                // 칩 글: 본문 크기(24) 굵게, 밝은 글자에 짙은 외곽선 — 칩이 어두운 갈색이라 작은 글이 묻히지 않게
                Text label = Ui.Label(back.transform, "Chip Text", chip.Text, UiTokens.Text, Color.white, TextAnchor.MiddleCenter);
                HumanLastRampart.UI.UiFonts.Bold(label);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                Ui.Outlined(label);
                Ui.Size(label, Mathf.Ceil(label.preferredWidth) + 8f, 42f);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
