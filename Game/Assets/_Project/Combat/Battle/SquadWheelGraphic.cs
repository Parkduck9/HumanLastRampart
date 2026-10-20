using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 그림 파일 없이도 표시되는 원형 메뉴. 네 방향은 북·동·남·서 순서다
    public sealed class SquadWheelGraphic : MaskableGraphic
    {
        public const float Inner = 48f, Outer = 158f;
        public int Segments = 4;
        public int Disabled = -1;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float step = 360f / Segments;
            for (int i = 0; i < Segments; i++)
            {
                float start = 90f - i * step - step * 0.5f + 1f;
                float end = start + step - 2f;
                Arc(vh, start, end, Inner, Outer, i == Disabled ? UiTokens.BodyDim : UiTokens.Brass);
                Arc(vh, start + 1f, end - 1f, Inner + 2f, Outer - 2f, i == Disabled ? UiTokens.WoodDark : UiTokens.Wood);
            }
        }

        static void Arc(VertexHelper vh, float start, float end, float inner, float outer, Color tint)
        {
            const int slices = 32;
            for (int j = 0; j < slices; j++)
            {
                float a = Mathf.Lerp(start, end, j / (float)slices) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(start, end, (j + 1f) / slices) * Mathf.Deg2Rad;
                Vector2 u = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), v = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int n = vh.currentVertCount;
                vh.AddVert(u * inner, tint, Vector2.zero);
                vh.AddVert(u * outer, tint, Vector2.zero);
                vh.AddVert(v * outer, tint, Vector2.zero);
                vh.AddVert(v * inner, tint, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2);
                vh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}
