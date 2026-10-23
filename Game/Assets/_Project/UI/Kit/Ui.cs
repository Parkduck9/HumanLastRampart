using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    public enum UiPanel { Parchment, Wood, Band, Slip, SlipDanger, Bubble, Plate }

    // Normal = 나무 단추, Primary = 황동 단추(화면의 주된 행동 하나), Quiet = 바탕 없는 글 단추(닫기·되돌아가기),
    // Tab = 고르기 탭(고르면 빛 테두리가 남는다), Icon = 아이콘만 있는 네모 단추
    public enum UiButtonKind { Normal, Primary, Quiet, Tab, Icon }

    // K-047 UI 부품 한 벌 (S-28): 모든 화면(전투 HUD·지도·정비·결과·메인·스토리)이 이것만 쓴다.
    // 단추 이름은 "btn:" + 열쇠 — 보이는 글자가 바뀌어도 시험(PlayProbe)은 열쇠로 찾는다
    public static class Ui
    {
        public const string KeyPrefix = "btn:";

        // ── 기본 ──
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(Transform parent, string name, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) => Place(rect, anchor, position, size, anchor);

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // 레이아웃 안에서의 크기 (음수면 정하지 않음)
        public static LayoutElement Size(Component c, float width, float height, bool flexibleWidth = false)
        {
            var element = c.GetComponent<LayoutElement>();
            if (element == null) element = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) element.preferredWidth = element.minWidth = width;
            if (height >= 0f) element.preferredHeight = element.minHeight = height;
            element.flexibleWidth = flexibleWidth ? 1f : -1f;
            return element;
        }

        public static HorizontalLayoutGroup Row(Transform parent, string name, float spacing = UiTokens.S8, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, name);
            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = align;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            return row;
        }

        public static VerticalLayoutGroup Column(Transform parent, string name, float spacing = UiTokens.S8, int padding = 0)
        {
            var rect = Rect(parent, name);
            var column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = spacing;
            column.padding = new RectOffset(padding, padding, padding, padding);
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            return column;
        }

        // ── 판 (S-09·S-11: 재질 그림 + 도트 계단 모서리) ──
        public static Image Panel(Transform parent, string name, UiPanel kind)
        {
            Image image = Image(parent, name, Color.white, true);   // 판은 클릭을 막는다 (뒤 지도·마당이 같이 눌리지 않게)
            SetPanel(image, kind);
            return image;
        }

        public static void SetPanel(Image image, UiPanel kind)
        {
            string art = kind == UiPanel.Parchment ? UiArt.PanelParchment : kind == UiPanel.Wood ? UiArt.PanelWood
                : kind == UiPanel.Band ? UiArt.PanelBand : kind == UiPanel.Slip ? UiArt.Slip : kind == UiPanel.SlipDanger ? UiArt.SlipDanger
                : kind == UiPanel.Bubble ? UiArt.Bubble : UiArt.Plate;
            image.sprite = UiArt.Get(art);
            // 큰 판은 가운데 무늬가 늘어나 흐려지지 않게 바둑판처럼 반복, 작은 판은 늘이기
            image.type = kind == UiPanel.Parchment || kind == UiPanel.Wood || kind == UiPanel.Band ? UnityEngine.UI.Image.Type.Tiled : UnityEngine.UI.Image.Type.Sliced;
            image.fillCenter = true;
            image.color = Color.white;
        }

        // 판 안쪽 여백(테두리 6점 + 한 단계) — 판 안 내용은 여기서부터
        public const float PanelPad = UiTokens.S24;

        // ── 글 ──
        public static Text Label(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, bool display = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = display ? UiFonts.Display : UiFonts.Body;
            label.fontSize = display ? size : UiFonts.SnapPixel(size);   // 갈무리는 11의 배수 크기에서 또렷 (W16)
            label.text = text ?? "";
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.lineSpacing = 1.05f;
            // 종이 위 작은 글(24 이하)은 또렷한 본문 글꼴 굵게 — 가는 획이 종이 무늬에 묻혀 회색으로 보였다 (사진 r11)
            bool onPaper = color == UiTokens.Ink || color == UiTokens.InkSoft || color == UiTokens.DangerInk || color == UiTokens.GoodInk;
            if (onPaper && size <= UiTokens.Text)
            {
                label.font = UiFonts.BodyBold;   // 굵은 글꼴 파일 사용 (가짜 굵기 X)
                label.fontSize = UiFonts.SnapPixel(size);
            }
            else if (!display && size >= UiTokens.Text && size < UiTokens.H2)
                UiFonts.Bold(label);   // W16: Text(24)와 Small(20)이 같은 22가 되므로 위계는 굵기로 (Text = Bold)

            return label;
        }

        // 장면 위에 바로 놓는 글: 도트식 짙은 외곽선 (S-08)
        public static T Outlined<T>(T graphic) where T : Graphic
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(UiTokens.Outline.r, UiTokens.Outline.g, UiTokens.Outline.b, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            return graphic;
        }

        // ── 아이콘 (S-13·S-14: 같은 격자·외곽선·정수 배율) ──
        public static Image Icon(Transform parent, string name, string art, float scale = 1f, Color? tint = null)
        {
            Sprite sprite = UiArt.Get(art);
            Image image = Image(parent, name, tint ?? Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            ((RectTransform)image.transform).sizeDelta = UiArt.Size(sprite, scale);
            Size(image, ((RectTransform)image.transform).sizeDelta.x, ((RectTransform)image.transform).sizeDelta.y);
            return image;
        }

        // Codex HudArt 아이콘(Resources/HudArt/…) — 없으면 null. 크기는 바라는 px에 가장 가까운 정수 배율로 맞춘다 (S-14: 점이 뭉개지지 않게)
        public static Image HudIcon(Transform parent, string name, string path, float px)
        {
            Sprite sprite = UiArt.Hud(path);
            if (sprite == null) return null;
            Image image = Image(parent, name, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            Vector2 size = IntegerSize(sprite, px);
            ((RectTransform)image.transform).sizeDelta = size;
            Size(image, size.x, size.y);
            return image;
        }

        public static Vector2 IntegerSize(Sprite sprite, float px)
        {
            float native = Mathf.Max(sprite.rect.width, sprite.rect.height);
            float scale = Mathf.Max(1f, Mathf.Round(px / Mathf.Max(1f, native)));
            return new Vector2(sprite.rect.width, sprite.rect.height) * scale;
        }

        // ── 단추 ──
        public static Button Button(Transform parent, string key, string label, Action onClick, UiButtonKind kind = UiButtonKind.Normal, string icon = null)
        {
            var go = new GameObject(KeyPrefix + key, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(UiButtonFeel));
            go.transform.SetParent(parent, false);
            var face = go.GetComponent<Image>();
            var button = go.GetComponent<Button>();
            var feel = go.GetComponent<UiButtonFeel>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            feel.Button = button;
            feel.Face = face;
            face.raycastTarget = true;

            bool brass = kind == UiButtonKind.Primary;
            if (kind == UiButtonKind.Quiet)
            {
                face.color = new Color(0f, 0f, 0f, 0f);
                feel.FaceColor = face.color;
            }
            else
            {
                // K-123 상태 그림: 보통 · 올림 · 눌림 · 꺼짐 · 켜짐 (탭은 꺼짐/켜짐 탭 그림, 아이콘 단추는 리벳 틀). 없는 그림은 null → 빛 테두리·어둡게로 대신
                feel.Normal = UiArt.Get(brass ? UiArt.ButtonBrass : kind == UiButtonKind.Tab ? UiArt.TabOff : kind == UiButtonKind.Icon ? UiArt.IconButtonFrame : UiArt.ButtonWood);
                if (feel.Normal == null) feel.Normal = UiArt.Get(brass ? UiArt.ButtonBrass : UiArt.ButtonWood);
                feel.Down = UiArt.Get(brass ? UiArt.ButtonBrassDown : UiArt.ButtonWoodDown);
                feel.Hover = UiArt.Get(brass ? UiArt.ButtonBrassHover : UiArt.ButtonWoodHover);
                feel.Disabled = UiArt.Get(brass ? UiArt.ButtonBrassDisabled : UiArt.ButtonWoodDisabled);
                feel.OnSprite = UiArt.Get(brass ? UiArt.ButtonBrassOn : kind == UiButtonKind.Tab ? UiArt.TabOn : UiArt.ButtonWoodOn);
                face.sprite = feel.Normal;
                face.type = UnityEngine.UI.Image.Type.Sliced;
                face.color = Color.white;
                feel.FaceColor = Color.white;
            }

            Image glow = Image(go.transform, "Glow", new Color(1f, 1f, 1f, 0f));
            glow.sprite = UiArt.Get(UiArt.Glow);
            glow.type = UnityEngine.UI.Image.Type.Sliced;
            glow.enabled = false;
            Stretch(glow.rectTransform, -UiTokens.Dot);
            feel.Glow = glow;

            RectTransform content = Rect(go.transform, "Content");
            Stretch(content);
            feel.Content = content;
            feel.ContentGroup = content.gameObject.AddComponent<CanvasGroup>();
            feel.ContentGroup.blocksRaycasts = false;

            Color ink = brass ? UiTokens.Ink : kind == UiButtonKind.Quiet ? UiTokens.InkSoft : UiTokens.Body;   // Quiet = 종이 판 위 글 단추
            if (kind == UiButtonKind.Icon)
            {
                Image picture = Icon(content, "Icon", icon ?? UiArt.IconClose, 1f, Color.white);   // K-123 아이콘은 색이 그림에 들어 있어 흰색으로
                Place(picture.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, picture.rectTransform.sizeDelta);
            }
            else
            {
                float left = UiTokens.S16;
                if (icon != null)
                {
                    Image picture = Icon(content, "Icon", icon, 1f, brass ? UiTokens.Ink : UiTokens.Brass);
                    Place(picture.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiTokens.S16, 0f), picture.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
                    left = UiTokens.S16 + picture.rectTransform.sizeDelta.x + UiTokens.S8;
                }

                Text text = Label(content, "Label", label, UiTokens.Text, ink, icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, brass);
                if (brass) { text.font = UiFonts.BodyBold; text.fontSize = UiFonts.SnapPixel(UiTokens.Text); text.fontStyle = FontStyle.Normal; }   // 황동 위 글은 또렷한 본문 글꼴 굵게 (명조 작은 글은 번졌다)
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(icon != null ? left : UiTokens.S16, 0f);
                text.rectTransform.offsetMax = new Vector2(-UiTokens.S16, 0f);
            }

            if (onClick != null) button.onClick.AddListener(() => onClick());
            // 소리 (W24): 주 행동 = 확인, 닫기·취소 글 = 취소, 나머지 = 누름. 은행이 비었으면 조용
            string clickCue = kind == UiButtonKind.Primary ? HumanLastRampart.Core.Audio.AudioCues.UiConfirm
                : IsCancelLabel(label) ? HumanLastRampart.Core.Audio.AudioCues.UiCancel : HumanLastRampart.Core.Audio.AudioCues.UiClick;
            button.onClick.AddListener(() => HumanLastRampart.Core.Audio.Sound.Play(clickCue));
            return button;
        }

        static bool IsCancelLabel(string label) =>
            label != null && (label.Contains("닫기") || label.Contains("취소") || label.Contains("뒤로") || label.Contains("돌아가기"));

        public static Button IconButton(Transform parent, string key, string icon, Action onClick, float size = 48f)
        {
            Button button = Button(parent, key, null, onClick, UiButtonKind.Icon, icon);
            ((RectTransform)button.transform).sizeDelta = new Vector2(size, size);
            Size(button, size, size);
            if (size >= 64f)
            {
                // 큰 단추는 K-123 48점 아이콘 (작은 단추에서는 24점 아이콘)
                Transform t = button.transform.Find("Content/Icon");
                var image = t != null ? t.GetComponent<Image>() : null;
                Sprite big = UiArt.Get(UiArt.Big(icon));
                if (image != null && big != null)
                {
                    image.sprite = big;
                    image.rectTransform.sizeDelta = UiArt.Size(big);
                }
            }

            return button;
        }

        public static Text LabelOf(Button button)
        {
            Transform t = button != null ? button.transform.Find("Content/Label") : null;
            return t != null ? t.GetComponent<Text>() : null;
        }

        public static void SetLabel(Button button, string text)
        {
            Text label = LabelOf(button);
            if (label != null) label.text = text;
        }

        // 고른 상태 (빛 테두리가 남는다 + 탭 글은 황동)
        public static void SetOn(Button button, bool on)
        {
            if (button == null) return;
            var feel = button.GetComponent<UiButtonFeel>();
            if (feel != null) feel.On = on;
            Text label = LabelOf(button);
            if (label != null && feel != null && feel.Normal != null && (feel.Normal.name == UiArt.ButtonWood || feel.Normal.name == UiArt.TabOff)) label.color = on ? UiTokens.Brass : UiTokens.Body;
        }

        public static void SetIcon(Button button, string icon, Color? tint = null)
        {
            Transform t = button != null ? button.transform.Find("Content/Icon") : null;
            if (t == null) return;
            var image = t.GetComponent<Image>();
            bool big = image.sprite != null && image.sprite.name.EndsWith("_48");   // 큰 아이콘 단추는 큰 짝 그림으로 바꾼다
            image.sprite = UiArt.Get(big ? UiArt.Big(icon) : icon);
            if (tint.HasValue) image.color = tint.Value;
        }

        // ── 키 표시 (S-15): [키] 동사 ──
        public static RectTransform KeyHint(Transform parent, string key, string verb, bool overScene = true)
        {
            HorizontalLayoutGroup row = Row(parent, "Key " + key, UiTokens.S8);
            Image cap = Image(row.transform, "Cap", Color.white);
            cap.sprite = UiArt.Get(UiArt.KeyCap);
            cap.type = UnityEngine.UI.Image.Type.Sliced;
            Text keyText = Label(cap.transform, "Key", key, UiTokens.Small, UiTokens.Ink, TextAnchor.MiddleCenter);
            HumanLastRampart.UI.UiFonts.Bold(keyText);
            keyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(keyText.rectTransform);
            keyText.rectTransform.offsetMin = new Vector2(0f, UiTokens.Dot);   // 키 아래 두께만큼 위로
            float width = Mathf.Max(33f, keyText.preferredWidth + UiTokens.S16);
            Size(cap, width, 33f);
            Text verbText = Label(row.transform, "Verb", verb, UiTokens.Small, overScene ? UiTokens.Body : UiTokens.Ink);
            verbText.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (overScene) Outlined(verbText);
            Size(verbText, verbText.preferredWidth + 2f, 33f);
            return (RectTransform)row.transform;
        }

        // ── 어둠막·확인 창 (S-02: 판이 열리면 뒤 화면을 누른다) ──
        public static Image Shade(Transform parent, string name)
        {
            Image shade = Image(parent, name, UiTokens.Night, true);
            Stretch(shade.rectTransform);
            UiAppear.Show(shade.gameObject);
            return shade;
        }

        public static GameObject Confirm(Transform parent, string title, string line,
            string yesKey, string yesLabel, Action yes, string noKey, string noLabel, Action no)
        {
            Image shade = Shade(parent, "Confirm");
            GameObject root = shade.gameObject;
            Image box = Panel(shade.transform, "Box", UiPanel.Parchment);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 300f));
            UiAppear.Show(box.gameObject, new Vector2(0f, -UiTokens.S24));
            Text heading = Label(box.transform, "Title", title, UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleCenter, true);
            Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -UiTokens.S48), new Vector2(640f, 48f), new Vector2(0.5f, 0.5f));
            if (!string.IsNullOrEmpty(line))
            {
                Text body = Label(box.transform, "Line", line, UiTokens.Text, UiTokens.InkSoft, TextAnchor.MiddleCenter);
                Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(640f, 36f), new Vector2(0.5f, 0.5f));
            }

            Button ok = Button(box.transform, yesKey, yesLabel, () => { Close(root); yes?.Invoke(); }, UiButtonKind.Primary);
            Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(-140f, UiTokens.S48), new Vector2(256f, UiTokens.ButtonHeight), new Vector2(0.5f, 0f));
            Button cancel = Button(box.transform, noKey, noLabel, () => { Close(root); no?.Invoke(); });
            Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(140f, UiTokens.S48), new Vector2(256f, UiTokens.ButtonHeight), new Vector2(0.5f, 0f));
            return root;
        }

        public static void Close(GameObject go) => UiAppear.Hide(go, true);

        // ── 막대 (S-20·S-25) ──
        public static UiBar Bar(Transform parent, string name, float width, float height, bool colorByRatio = true)
        {
            // K-123 막대: 홈 + 채움(좋음·황동·위험)·잔상 그림. 높이 24 이상은 큰 것(48×24), 아래는 작은 것(48×15). 그림이 없으면 색 칠하기로 대신
            bool large = height >= 24f;
            Sprite trackArt = UiArt.Get(large ? UiArt.BarTrackLarge : UiArt.BarTrackSmall);
            Sprite good = UiArt.Get(UiArt.BarFill("good", large)), brassArt = UiArt.Get(UiArt.BarFill("brass", large));
            Sprite danger = UiArt.Get(UiArt.BarFill("danger", large)), lagArt = UiArt.Get(UiArt.BarFill("lag", large));
            bool art = trackArt != null && good != null && brassArt != null && danger != null && lagArt != null;
            Image track = Image(parent, name, Color.white);
            track.sprite = art ? trackArt : UiArt.Get(UiArt.BarTrack);
            track.type = UnityEngine.UI.Image.Type.Sliced;
            ((RectTransform)track.transform).sizeDelta = new Vector2(width, height);
            Size(track, width, height);
            RectTransform inner = Rect(track.transform, "Inner");
            Stretch(inner, art ? 0f : UiTokens.Dot);   // K-123 채움 그림은 홈과 같은 크기라 안쪽으로 줄이지 않는다
            Image lag = Image(inner, "Lag", art ? Color.white : UiTokens.BrassLight);
            Stretch(lag.rectTransform);
            Image fill = Image(inner, "Fill", art ? Color.white : UiTokens.Good);
            Stretch(fill.rectTransform);
            if (art)
            {
                lag.sprite = lagArt;
                fill.sprite = good;
                lag.type = fill.type = UnityEngine.UI.Image.Type.Sliced;
            }

            var bar = track.gameObject.AddComponent<UiBar>();
            if (art) { bar.GoodArt = good; bar.BrassArt = brassArt; bar.DangerArt = danger; }
            bar.Fill = fill.rectTransform;
            bar.FillImage = fill;
            bar.Lag = lag.rectTransform;
            bar.LagImage = lag;
            bar.ColorByRatio = colorByRatio;
            return bar;
        }
    }
}
