using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    // 단추 손맛 (S-22): 올리면 글이 한 점 떠오르고 황동 빛 테두리가 돈다, 누르면 눌린 그림 + 글이 한 점 내려간다,
    // 고른 단추(On)는 빛 테두리가 계속 남는다, 못 누르면 바탕이 어두워지고 글이 옅어진다. 밝기 배율(엔진 기본)은 쓰지 않는다
    [DisallowMultipleComponent]
    public sealed class UiButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public Button Button;
        public Image Face;
        public Image Glow;
        public RectTransform Content;
        public CanvasGroup ContentGroup;
        public Sprite Normal, Down;
        public Color FaceColor = Color.white;
        public bool On;

        bool hover, pressed, selected;
        float glow, last = -1f;

        public void OnPointerEnter(PointerEventData eventData) => hover = true;
        public void OnPointerExit(PointerEventData eventData) { hover = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) => selected = false;

        void OnDisable() { hover = pressed = false; }

        void Update()
        {
            float now = Time.realtimeSinceStartup;   // 시험의 고정 시간 간격(captureDeltaTime)과 상관없이 실제 시간으로
            float dt = last < 0f ? 0f : Mathf.Min(now - last, 0.1f);
            last = now;
            bool live = Button == null || Button.IsInteractable();
            float target = !live ? (On ? 0.35f : 0f) : On ? 1f : hover || selected ? 0.8f : 0f;
            glow = Mathf.MoveTowards(glow, target, dt * 9f);
            if (Glow != null)
            {
                Glow.enabled = glow > 0.01f;
                Glow.color = new Color(1f, 1f, 1f, glow);
            }

            if (Face != null)
            {
                if (Normal != null) Face.sprite = live && pressed && Down != null ? Down : Normal;
                Face.color = live ? FaceColor : new Color(FaceColor.r * 0.55f, FaceColor.g * 0.55f, FaceColor.b * 0.55f, FaceColor.a);
            }

            if (Content != null)
            {
                float y = !live ? 0f : pressed ? -UiTokens.Dot : hover ? UiTokens.Dot : 0f;
                Content.anchoredPosition = new Vector2(0f, y);
            }

            if (ContentGroup != null) ContentGroup.alpha = live ? 1f : 0.45f;
        }
    }
}
