using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HumanLastRampart.Meta
{
    // 함정 공방 지도의 자유 배치 입력 (사용자 2026-10-20): 마우스를 따라 놓일 모습이 움직이고,
    // 왼쪽 누르기 = 놓기, 오른쪽 누르기 = 45도 돌리기. 좌표는 지도 사각형 안의 위치(가운데 원점, 화소)
    public sealed class FreePlacementInput : MonoBehaviour, IPointerMoveHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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

        public void OnPointerClick(PointerEventData eventData)
        {
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
