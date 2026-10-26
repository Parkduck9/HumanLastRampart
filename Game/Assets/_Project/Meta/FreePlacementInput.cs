using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HumanLastRampart.Meta
{
    // 함정 공방 지도의 자유 배치 입력 (사용자 2026-10-20): 마우스를 따라 놓일 모습이 움직이고,
    // 왼쪽 누르기 = 놓기, 오른쪽 누르기 = 45도 돌리기. 좌표는 지도 사각형 안의 위치(가운데 원점, 화소)
    public sealed class FreePlacementInput : MonoBehaviour, IPointerMoveHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler
    {
        internal Action<Vector2> Moved;
        internal Action<Vector2> Placed;
        internal Action Rotated;
        internal Action Left;

        // 시험용 (PlayProbe): 지도 안 위치(가운데 원점, 화소)로 마우스 움직임·놓기·돌리기를 흉내 낸다
        public void MoveTo(Vector2 local) => Moved?.Invoke(local);
        public void PlaceAt(Vector2 local) => Placed?.Invoke(local);
        public void Rotate() => Rotated?.Invoke();

        public void OnPointerEnter(PointerEventData eventData) => Moved?.Invoke(Local(eventData));
        public void OnPointerMove(PointerEventData eventData) => Moved?.Invoke(Local(eventData));
        public void OnPointerExit(PointerEventData eventData) => Left?.Invoke();

        // 터치 (W40): 오른쪽 누르기가 없으니 길게 누르면(0.5초, 거의 안 움직이고) 돌리기. 그 뒤 손을 떼는 클릭은 놓기로 치지 않는다
        float downAt;
        Vector2 downPoint;
        bool rotatedByHold;

        public void OnPointerDown(PointerEventData eventData)
        {
            downAt = Time.unscaledTime;
            downPoint = eventData.position;
            rotatedByHold = false;
        }

        static bool IsTouch(PointerEventData eventData) =>
            eventData is UnityEngine.InputSystem.UI.ExtendedPointerEventData extended && extended.pointerType == UnityEngine.InputSystem.UI.UIPointerType.Touch;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsTouch(eventData) && eventData.button == PointerEventData.InputButton.Left
                && Time.unscaledTime - downAt >= 0.5f && (eventData.position - downPoint).sqrMagnitude < 24f * 24f)
            {
                rotatedByHold = true;
                Rotated?.Invoke();
                Moved?.Invoke(Local(eventData));
                return;
            }

            if (rotatedByHold) { rotatedByHold = false; return; }
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

