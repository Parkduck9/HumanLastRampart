using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    // W40 터치 조작 부품 (K-125 그림). 부품은 uGUI 눌림 이벤트 대신 터치 화면을 직접 읽는 TouchLayer가 손가락마다 하나씩 쥐여 준다
    // (여러 손가락 동시: 왼손 이동 패드 + 오른손 행동 단추). 그림은 전부 그림 크기 그대로 놓는다 (1920×1080 기준 캔버스).
    public abstract class TouchWidget
    {
        public RectTransform Rt;
        public float HitPad = 0f;   // 눌림 범위를 그림 밖으로 넓히지 않는다 (그림 밖은 그 밑의 판이 받아야 한다)

        public bool Visible
        {
            get => Rt != null && Rt.gameObject.activeSelf;
            set { if (Rt != null && Rt.gameObject.activeSelf != value) Rt.gameObject.SetActive(value); }
        }

        public abstract void Begin(Vector2 screen);
        public virtual void Drag(Vector2 screen) { }
        public abstract void End(Vector2 screen, bool canceled);
        public virtual void Tick(float dt) { }

        // 화면 화소 사각형 (오버레이 캔버스라 월드 좌표 = 화면 화소)
        static readonly Vector3[] corners = new Vector3[4];   // 매 프레임 새로 만들지 않는다 (메인 스레드 전용)

        public UnityEngine.Rect ScreenRect
        {
            get
            {
                Rt.GetWorldCorners(corners);
                float pad = HitPad * Rt.lossyScale.x;
                return UnityEngine.Rect.MinMaxRect(corners[0].x - pad, corners[0].y - pad, corners[2].x + pad, corners[2].y + pad);
            }
        }

        public bool Contains(Vector2 screen) => ScreenRect.Contains(screen);
    }

    // 둥근 행동 단추 (96 · 72): 보통 · 눌림 · 꺼짐 · 강조 + 강조 테 순환, 가운데 기호는 따로 겹친다. 손가락을 떼는 자리가 단추 안이면 눌린 것
    public sealed class TouchButton : TouchWidget
    {
        readonly Image face, glyph, pulse;
        readonly Sprite normal, pressed, disabled, highlighted;
        readonly Sprite[] pulseFrames = new Sprite[4];
        bool down;

        public readonly string Key;
        public bool Enabled = true;
        public bool Highlight;
        public Action Tapped;
        public bool IsDown => down;

        public TouchButton(Transform parent, string key, int size, string glyphId)
        {
            Key = key;
            Rt = Ui.Rect(parent, "Touch:" + key);
            Rt.sizeDelta = new Vector2(size, size);
            string prefix = "action_" + size + "_";
            normal = TouchArt.Get(prefix + "normal");
            pressed = TouchArt.Get(prefix + "pressed");
            disabled = TouchArt.Get(prefix + "disabled");
            highlighted = TouchArt.Get(prefix + "highlighted");
            for (int i = 0; i < 4; i++) pulseFrames[i] = TouchArt.Crop(prefix + "pulse_sheet", "f" + i, new Rect(i * size, 0f, size, size));
            face = Ui.Image(Rt, "Face", Color.white, true);
            face.sprite = normal;
            Ui.Stretch(face.rectTransform);
            pulse = Ui.Image(Rt, "Pulse", Color.white);
            Ui.Stretch(pulse.rectTransform);
            pulse.enabled = false;
            glyph = Ui.Image(Rt, "Glyph", Color.white);
            glyph.rectTransform.sizeDelta = new Vector2(48f, 48f);
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            SetGlyph(glyphId);
        }

        public void SetGlyph(string id) => glyph.sprite = TouchArt.Glyph(id);

        public string GlyphName => glyph.sprite != null ? glyph.sprite.name : "";

        bool inside;

        public override void Begin(Vector2 screen) { if (Enabled) { down = true; inside = true; } }
        public override void Drag(Vector2 screen) { inside = Contains(screen); }   // 손가락이 단추 밖으로 나가면 떼도 눌린 것이 아니다

        public override void End(Vector2 screen, bool canceled)
        {
            bool fire = down && !canceled && Enabled && inside;
            down = false;
            if (fire) Tapped?.Invoke();
        }

        public override void Tick(float dt)
        {
            face.sprite = !Enabled ? disabled : down ? pressed : Highlight ? highlighted : normal;
            glyph.color = Enabled ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            glyph.rectTransform.anchoredPosition = new Vector2(0f, down ? -3f : 0f);
            bool ring = Highlight && Enabled && !down;
            pulse.enabled = ring;
            if (ring) pulse.sprite = pulseFrames[(int)(Time.unscaledTime * 8f) % 4];
        }
    }

    // 가로 이동 패드 (312×144): 홈 + 손잡이. 좌우 = 성벽 따라 달리기, 위아래 = 통로 바깥·안쪽. 떼면 가운데로 돌아온다
    public sealed class TouchMoveSlider : TouchWidget
    {
        const float HalfTravel = 100f, DepthTravel = 26f, HomeY = 2f;   // 손잡이가 움직이는 범위 (그림 px)
        const float RunDead = 0.2f, DepthDead = 0.45f;                  // 가장자리 눌림 둔감대: 달리는 중 깊이가 흔들리지 않게
        readonly Image thumb;
        readonly Sprite thumbIdle, thumbPressed;
        Vector2 raw;

        public bool Held { get; private set; }
        public Vector2 Value { get; private set; }   // x: -1 왼쪽 ~ +1 오른쪽, y: +1 바깥쪽(위) ~ -1 안쪽(아래)

        public TouchMoveSlider(Transform parent)
        {
            Rt = Ui.Rect(parent, "Touch:Move Slider");
            Rt.sizeDelta = new Vector2(312f, 144f);
            Image hit = Ui.Image(Rt, "Hit", Color.clear, true);
            Ui.Stretch(hit.rectTransform);
            Image track = Ui.Image(Rt, "Track", Color.white);
            track.sprite = TouchArt.Get("move_slider_track");
            track.type = Image.Type.Sliced;
            Place(track.rectTransform, new Vector2(312f, 72f), new Vector2(0f, HomeY));
            // 위·아래 표식은 이동 패드 그림(move_slider_60_idle)의 윗줄·아랫줄 칸 (위 표식 9줄·아래 표식 12줄)
            Image up = Ui.Image(Rt, "Up Mark", Color.white);
            up.sprite = TouchArt.Crop("move_slider_60_idle", "up", new Rect(144f, 123f, 24f, 9f));
            Place(up.rectTransform, new Vector2(24f, 9f), new Vector2(0f, 55.5f));
            Image down = Ui.Image(Rt, "Down Mark", Color.white);
            down.sprite = TouchArt.Crop("move_slider_60_idle", "down", new Rect(144f, 12f, 24f, 12f));
            Place(down.rectTransform, new Vector2(24f, 12f), new Vector2(0f, -53.5f));
            thumbIdle = TouchArt.Get("move_thumb_idle");
            thumbPressed = TouchArt.Get("move_thumb_pressed");
            thumb = Ui.Image(Rt, "Thumb", Color.white);
            thumb.sprite = thumbIdle;
            Place(thumb.rectTransform, new Vector2(60f, 60f), new Vector2(0f, HomeY));
        }

        static void Place(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        public override void Begin(Vector2 screen)
        {
            Held = true;
            Follow(screen);
        }

        public override void Drag(Vector2 screen) => Follow(screen);

        public override void End(Vector2 screen, bool canceled)
        {
            Held = false;
            raw = Value = Vector2.zero;
        }

        void Follow(Vector2 screen)
        {
            Vector3 center = Rt.TransformPoint(Rt.rect.center);
            float scale = Mathf.Max(0.01f, Rt.lossyScale.x);
            raw = new Vector2(Mathf.Clamp((screen.x - center.x) / (HalfTravel * scale), -1f, 1f),
                Mathf.Clamp((screen.y - center.y - HomeY * scale) / (DepthTravel * scale), -1f, 1f));
            Value = new Vector2(Dead(raw.x, RunDead), Dead(raw.y, DepthDead));
        }

        static float Dead(float v, float dead) => Mathf.Abs(v) < dead ? 0f : Mathf.Sign(v) * (Mathf.Abs(v) - dead) / (1f - dead);

        public override void Tick(float dt)
        {
            thumb.sprite = Held ? thumbPressed : thumbIdle;
            thumb.rectTransform.anchoredPosition = new Vector2(raw.x * HalfTravel, HomeY + raw.y * DepthTravel);
        }
    }

    // 성벽 선택판 (192×192): 가운데 등대 + 북·동·남·서 네 조각. 누르는 동안 그 조각이 밝아지고, 같은 조각 안에서 떼면 선택 (0 북 · 1 동 · 2 남 · 3 서)
    public sealed class TouchCompass : TouchWidget
    {
        static readonly string[] Names = { "north", "east", "south", "west" };
        readonly Image[] pieces = new Image[4];
        int held = -1, current = -1;

        public Action<int> Picked;
        public bool Enabled = true;

        public TouchCompass(Transform parent)
        {
            Rt = Ui.Rect(parent, "Touch:Compass");
            Rt.sizeDelta = new Vector2(192f, 192f);
            Image back = Ui.Image(Rt, "Base", Color.white, true);
            back.sprite = TouchArt.Get("compass_base");
            Ui.Stretch(back.rectTransform);
            for (int i = 0; i < 4; i++)
            {
                pieces[i] = Ui.Image(Rt, "Pressed " + Names[i], Color.white);
                pieces[i].sprite = TouchArt.Get("compass_" + Names[i] + "_pressed");
                Ui.Stretch(pieces[i].rectTransform);
                pieces[i].enabled = false;
            }
        }

        // 화면 점이 어느 조각인가 (가운데 등대 둘레 조금은 없음 -1)
        public int ZoneAt(Vector2 screen)
        {
            Vector3 center = Rt.TransformPoint(Rt.rect.center);
            Vector2 d = new Vector2(screen.x - center.x, screen.y - center.y) / Mathf.Max(0.01f, Rt.lossyScale.x);
            if (d.magnitude < 20f || d.magnitude > 120f) return -1;
            if (Mathf.Abs(d.y) >= Mathf.Abs(d.x)) return d.y > 0f ? 0 : 2;
            return d.x > 0f ? 1 : 3;
        }

        public override void Begin(Vector2 screen) { if (Enabled) held = current = ZoneAt(screen); }
        public override void Drag(Vector2 screen) { current = ZoneAt(screen); }

        public override void End(Vector2 screen, bool canceled)
        {
            bool fire = Enabled && !canceled && held >= 0 && current == held;
            int side = held;
            held = current = -1;
            if (fire) Picked?.Invoke(side);
        }

        public override void Tick(float dt)
        {
            for (int i = 0; i < 4; i++) pieces[i].enabled = held == i;
        }
    }

    // 터치 층 하나: 맨 위 오버레이 캔버스 + 안전 영역 + 손가락 배정 + 파문·길게 누르기 고리
    public sealed class TouchLayer : MonoBehaviour
    {
        public const int SortingOrder = 400;

        Canvas canvas;
        RectTransform safe, feedback;
        readonly List<TouchWidget> widgets = new List<TouchWidget>();
        readonly Dictionary<int, TouchWidget> owners = new Dictionary<int, TouchWidget>();
        readonly HashSet<int> pressedNow = new HashSet<int>();
        readonly List<int> stale = new List<int>();
        const int RipplePool = 4;
        readonly Image[] rippleImages = new Image[RipplePool];
        readonly float[] rippleBorn = new float[RipplePool];
        Image ring;
        Rect lastSafe;
        bool wanted = true;

        public bool ShowRing;     // 길게 누르기 고리 (전투에서만)
        public bool Shown => canvas != null && canvas.enabled;
        public RectTransform Safe => safe;
        public IReadOnlyList<TouchWidget> Widgets => widgets;

        public static TouchLayer Create(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var layer = go.AddComponent<TouchLayer>();
            layer.canvas = go.GetComponent<Canvas>();
            layer.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            layer.canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;   // 다른 화면과 같은 기준
            layer.safe = Ui.Rect(go.transform, "Safe Area");
            layer.feedback = Ui.Rect(go.transform, "Feedback");
            Ui.Stretch(layer.feedback);
            layer.canvas.enabled = false;
            return layer;
        }

        // 아직 보이지 않아도 부품은 만들어 둔다. 보일지는 소유자(화면)가 매 프레임 Wanted로, 켜짐 판단은 TouchInput이 정한다
        public bool Wanted { get => wanted; set => wanted = value; }

        T Add<T>(T widget) where T : TouchWidget
        {
            widget.Rt.SetParent(safe, false);
            widgets.Add(widget);
            return widget;
        }

        // 시안 좌표(1920×1080 왼쪽 위 원점)로 놓는다. 가운데보다 왼쪽이면 왼쪽 아래 모서리, 오른쪽이면 오른쪽 아래 모서리 기준
        public static void PlaceMock(RectTransform rect, float x, float y, float w, float h)
        {
            bool left = x + w * 0.5f < 960f;
            Vector2 anchor = new Vector2(left ? 0f : 1f, 0f);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(left ? x : -(1920f - x - w), 1080f - y - h);
        }

        public TouchButton AddButton(string key, int size, string glyph, float x, float y, Action tapped)
        {
            var button = Add(new TouchButton(safe, key, size, glyph));
            button.Tapped = tapped;
            PlaceMock(button.Rt, x, y, size, size);
            return button;
        }

        public TouchMoveSlider AddMoveSlider(float x, float y)
        {
            var slider = Add(new TouchMoveSlider(safe));
            PlaceMock(slider.Rt, x, y, 312f, 144f);
            return slider;
        }

        public TouchCompass AddCompass(float x, float y, Action<int> picked)
        {
            var compass = Add(new TouchCompass(safe));
            compass.Picked = picked;
            PlaceMock(compass.Rt, x, y, 192f, 192f);
            return compass;
        }

        public TouchWidget Find(string key) => widgets.Find(w => w is TouchButton b && b.Key == key);

        void Update() => Pump();

        // 한 프레임 처리 (시험이 직접 부르기도 한다)
        public void Pump()
        {
            bool on = wanted && TouchInput.Active;
            if (canvas.enabled != on)
            {
                canvas.enabled = on;
                if (!on) ReleaseAll();
            }

            if (!on) return;
            ApplySafeArea();
            var touchscreen = Touchscreen.current;
            if (touchscreen != null) ReadTouches(touchscreen);
            foreach (TouchWidget widget in widgets) if (widget.Visible) widget.Tick(Time.unscaledDeltaTime);
            TickRipples();
            TickRing();
        }

        void ApplySafeArea()
        {
            Rect area = TouchInput.SafeArea;
            if (area == lastSafe && safe.anchorMax != Vector2.zero) return;
            lastSafe = area;
            safe.anchorMin = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
            safe.anchorMax = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        void ReadTouches(Touchscreen touchscreen)
        {
            pressedNow.Clear();
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed) continue;
                int id = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();
                pressedNow.Add(id);
                if (owners.TryGetValue(id, out TouchWidget owner)) { owner?.Drag(position); continue; }
                TouchWidget hit = null;
                for (int i = widgets.Count - 1; i >= 0 && hit == null; i--)
                    if (widgets[i].Visible && widgets[i].Contains(position)) hit = widgets[i];
                owners[id] = hit;
                hit?.Begin(position);
                Ripple(position);
            }

            stale.Clear();
            foreach (var pair in owners) if (!pressedNow.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (int id in stale)
            {
                TouchWidget owner = owners[id];
                owners.Remove(id);
                owner?.End(LastPosition(touchscreen, id, owner), false);
            }
        }

        // 뗀 프레임에는 터치 칸이 비어 위치를 읽지 못하므로, 마지막으로 그 단추 안에 있던 점(중심)을 쓴다
        static Vector2 LastPosition(Touchscreen touchscreen, int id, TouchWidget owner)
        {
            foreach (TouchControl touch in touchscreen.touches)
                if (touch.touchId.ReadValue() == id) return touch.position.ReadValue();
            return owner != null ? (Vector2)owner.Rt.TransformPoint(owner.Rt.rect.center) : Vector2.zero;
        }

        void ReleaseAll()
        {
            foreach (var pair in owners) pair.Value?.End(Vector2.zero, true);
            owners.Clear();
        }

        // ── 파문 (초당 12칸, 4칸) · 길게 누르기 고리 (8칸) ──
        // 화면 화소 → 화면 비율 자리 (BattleHud.Viewport와 같은 방식: 해상도·배율과 상관없이 그 자리)
        static void Anchor(RectTransform rect, Vector2 screen)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(screen.x / Mathf.Max(1, Screen.width), screen.y / Mathf.Max(1, Screen.height));
            rect.anchoredPosition = Vector2.zero;
        }

        void Ripple(Vector2 screen)
        {
            // 풀에서 쉬는 그림 하나를 쓴다 (다 쓰는 중이면 가장 오래된 것을 다시 쓴다)
            int pick = 0;
            for (int i = 0; i < RipplePool; i++)
            {
                if (rippleImages[i] == null)
                {
                    Image made = Ui.Image(feedback, "Ripple", Color.white);
                    made.rectTransform.sizeDelta = new Vector2(96f, 96f);
                    made.enabled = false;
                    rippleImages[i] = made;
                }

                if (!rippleImages[i].enabled) { pick = i; break; }
                if (rippleBorn[i] < rippleBorn[pick]) pick = i;
            }

            Image image = rippleImages[pick];
            Anchor(image.rectTransform, screen);
            image.sprite = TouchArt.Ripple(0);
            image.enabled = true;
            rippleBorn[pick] = Time.unscaledTime;
        }

        void TickRipples()
        {
            for (int i = 0; i < RipplePool; i++)
            {
                if (rippleImages[i] == null || !rippleImages[i].enabled) continue;
                int frame = (int)((Time.unscaledTime - rippleBorn[i]) * 12f);
                if (frame >= 4) rippleImages[i].enabled = false;   // 마지막 칸 뒤 숨긴다
                else rippleImages[i].sprite = TouchArt.Ripple(frame);
            }
        }

        void TickRing()
        {
            float progress = ShowRing ? PointerInput.RingProgress : 0f;
            if (progress <= 0f)
            {
                if (ring != null) ring.enabled = false;
                return;
            }

            if (ring == null)
            {
                ring = Ui.Image(feedback, "Long Press Ring", Color.white);
                ring.rectTransform.sizeDelta = new Vector2(96f, 96f);
                ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }

            ring.enabled = true;
            Anchor(ring.rectTransform, PointerInput.RingPosition);
            ring.sprite = TouchArt.Ring(Mathf.CeilToInt(progress * 8f));
        }
    }
}








