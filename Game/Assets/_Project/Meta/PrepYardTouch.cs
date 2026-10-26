using HumanLastRampart.UI;
using UnityEngine;

namespace HumanLastRampart.Meta
{
    // W40 정비 마당(3번 화면) 터치 조작: 왼쪽 아래 이동 패드(좌우·위아래로 걷기) + 오른쪽 아래 상호작용 단추 (E와 같다).
    // PrepYard.Update가 Axis를 한 줄로 부른다 — 터치가 꺼져 있으면 아무것도 만들지 않고 0을 돌려준다.
    // 판·창은 이미 눌러서 쓰는 uGUI라 따로 고칠 것이 없다 (마우스 올림·오른쪽 클릭에 기대지 않는다)
    public sealed class PrepYardTouch : MonoBehaviour
    {
        PrepYard yard;
        TouchLayer layer;
        TouchMoveSlider slider;
        TouchButton interact;

        public TouchLayer Layer => layer;
        public TouchMoveSlider Slider => slider;
        public TouchButton Interact => interact;

        // 이번 프레임의 걷기 입력 (x 오른쪽 · y 북쪽/위)
        public static Vector2 Axis(PrepYard yard)
        {
            var touch = yard.GetComponent<PrepYardTouch>();
            if (touch == null)
            {
                if (!TouchInput.Active) return Vector2.zero;
                touch = yard.gameObject.AddComponent<PrepYardTouch>();
                touch.Build(yard);
            }

            return touch.Read();
        }

        void Build(PrepYard owner)
        {
            yard = owner;
            layer = TouchLayer.Create("Prep Yard Touch Layer");   // 마당과 같이 사라진다 (OnDestroy)
            slider = layer.AddMoveSlider(60f, 750f);
            interact = layer.AddButton("interact", 96, "interact", 1740f, 816f, () =>
            {
                if (yard != null && yard.CanMove) yard.Nearest()?.Use?.Invoke();
            });
        }

        Vector2 Read()
        {
            bool can = yard.CanMove;
            layer.Wanted = can;
            bool near = can && yard.Nearest() != null;
            interact.Enabled = near;
            interact.Highlight = near;
            return can && layer.Shown ? slider.Value : Vector2.zero;
        }

        void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
