using System;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 함정 공방 지도의 자유 배치 입력 (사용자 2026-10-20): 마우스를 따라 놓일 모습이 움직이고,
    // 왼쪽 누르기 = 놓기, 오른쪽 누르기 = 45도 돌리기. 좌표는 지도 사각형 안의 위치(가운데 원점, 화소)
    // 터치 (W40): 오른쪽 누르기가 없으므로 0.5초 길게 누르는 순간 돌린다 (길게 누르기 고리가 자란다). 그 뒤 손을 떼는 클릭은 놓기가 아니다
    public sealed class FreePlacementInput : MonoBehaviour, IPointerMoveHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Action<Vector2> Moved;
        public Action<Vector2> Placed;
        public Action Rotated;
        public Action Left;

        // 시험용 (PlayProbe): 지도 안 위치(가운데 원점, 화소)로 마우스 움직임·놓기·돌리기를 흉내 낸다
        public void MoveTo(Vector2 local) => Moved?.Invoke(local);
        public void PlaceAt(Vector2 local) => Placed?.Invoke(local);
        public void Rotate() => Rotated?.Invoke();

        public void OnPointerEnter(PointerEventData eventData) => Moved?.Invoke(Local(eventData));
        public void OnPointerMove(PointerEventData eventData) => Moved?.Invoke(Local(eventData));
        public void OnPointerExit(PointerEventData eventData) => Left?.Invoke();

        bool holding, rotatedByHold;
        float downAt;
        Vector2 downScreen, downLocal;
        Image ring;

        public bool HoldRotated => rotatedByHold;

        public void OnPointerDown(PointerEventData eventData)
        {
            rotatedByHold = false;
            holding = IsTouch(eventData) && eventData.button == PointerEventData.InputButton.Left;
            downAt = PointerInput.Now;
            downScreen = eventData.position;
            downLocal = Local(eventData);
        }

        public void OnPointerUp(PointerEventData eventData) => EndHold();

        static bool IsTouch(PointerEventData eventData) =>
            eventData is UnityEngine.InputSystem.UI.ExtendedPointerEventData extended && extended.pointerType == UnityEngine.InputSystem.UI.UIPointerType.Touch;

        // 길게 누르는 동안 고리를 키우고, 0.5초에 돌린다 (손가락이 24화소 넘게 움직이면 취소)
        public void Tick(Vector2 fingerScreen)
        {
            if (!holding) return;
            if ((fingerScreen - downScreen).sqrMagnitude > 24f * 24f) { EndHold(); return; }
            float t = PointerInput.Now - downAt;
            if (t >= PointerInput.LongPressSeconds && !rotatedByHold)
            {
                rotatedByHold = true;
                Rotated?.Invoke();
                Moved?.Invoke(downLocal);
            }

            ShowRing(rotatedByHold ? 1f : t < PointerInput.RingShowAfter ? 0f : Mathf.Clamp01(t / PointerInput.LongPressSeconds));
        }

        void Update()
        {
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            if (holding && touchscreen != null) Tick(touchscreen.primaryTouch.position.ReadValue());
        }

        void EndHold()
        {
            holding = false;
            if (ring != null) ring.enabled = false;
        }

        void OnDisable() => EndHold();

        void ShowRing(float progress)
        {
            if (progress <= 0f) { if (ring != null) ring.enabled = false; return; }
            if (ring == null)
            {
                ring = Ui.Image(transform, "Hold Ring", Color.white);
                ring.rectTransform.sizeDelta = new Vector2(96f, 96f);
                ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }

            ring.enabled = true;
            ring.rectTransform.anchoredPosition = downLocal;
            ring.sprite = TouchArt.Ring(Mathf.CeilToInt(progress * 8f));
            ring.transform.SetAsLastSibling();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (rotatedByHold) { rotatedByHold = false; return; }   // 길게 눌러 돌렸으면 손을 뗄 때 놓지 않는다
            if (eventData.button == PointerEventData.InputButton.Right) { Rotated?.Invoke(); Moved?.Invoke(Local(eventData)); }
            else if (eventData.button == PointerEventData.InputButton.Left) Placed?.Invoke(Local(eventData));
        }

        Vector2 Local(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.enterEventCamera ?? eventData.pressEventCamera, out Vector2 local);
            return local;
        }
    }
}
