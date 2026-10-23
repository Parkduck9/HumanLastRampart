using HumanLastRampart.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Frontend
{
    // K-121 메인 화면 배경: 하늘 → 들판 → 요새 → 전경 네 겹을 천천히 어긋나게 흔든다 (한 칸에 몇 픽셀, 크게 움직이지 않는다).
    // 겹마다 같은 중심에서 1.035배 키워 가장자리에 여유를 둔다. 카메라 흔들림 설정이 꺼져 있으면 멈춘다 (움직임 줄이기 대용).
    public sealed class MenuParallax : MonoBehaviour
    {
        public const string SkyArt = "menu_0_sky", FieldArt = "menu_1_field", FortressArt = "menu_2_fortress", ForegroundArt = "menu_3_foreground";
        public const float Zoom = 1.035f, PeriodSeconds = 14f;
        static readonly string[] Layers = { SkyArt, FieldArt, FortressArt, ForegroundArt };
        static readonly float[] ReachX = { 2f, 4f, 6f, 10f };   // 1920판 기준 좌우 한도 (K-121)
        static readonly float[] ReachY = { 0.75f, 1.5f, 2.25f, 3f };   // 위아래 한도

        RectTransform[] holders;
        float clock;
        bool settled;

        public int LayerCount => holders == null ? 0 : holders.Length;

        // parent 맨 뒤에 네 겹을 깐다. 그림이 하나라도 없으면 아무것도 만들지 않고 false (옛 배경으로 되돌린다)
        public static MenuParallax Build(Transform parent)
        {
            var textures = new Texture2D[Layers.Length];
            for (int i = 0; i < Layers.Length; i++)
            {
                textures[i] = Resources.Load<Texture2D>("ScreenArt/" + Layers[i]);
                if (textures[i] == null) return null;
            }

            var root = new GameObject("Menu Parallax", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.transform.SetAsFirstSibling();
            Stretch((RectTransform)root.transform);
            var parallax = root.AddComponent<MenuParallax>();
            parallax.holders = new RectTransform[Layers.Length];
            for (int i = 0; i < Layers.Length; i++)
            {
                // 움직이는 쪽(옮김·확대)과 그림 맞춤 쪽(AspectRatioFitter)을 나눈다 — 맞춤이 위치를 되돌려 덮어쓰지 않게
                var mover = new GameObject("Layer " + Layers[i], typeof(RectTransform));
                mover.transform.SetParent(root.transform, false);
                var rect = (RectTransform)mover.transform;
                Stretch(rect);
                rect.localScale = new Vector3(Zoom, Zoom, 1f);
                var holder = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
                holder.transform.SetParent(mover.transform, false);
                Stretch((RectTransform)holder.transform);
                var image = holder.GetComponent<RawImage>();
                image.texture = textures[i];
                image.raycastTarget = false;
                var fit = holder.GetComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)textures[i].width / Mathf.Max(1, textures[i].height);
                parallax.holders[i] = rect;
            }

            parallax.Apply();
            return parallax;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (GameSettings.CameraShake) clock += Time.unscaledDeltaTime;
            Apply();
        }

        // 겹 i의 위치: 사인 곡선으로 천천히 오간다. 움직임을 끄면 기본 위치 (1920판을 그대로 겹친 모양)
        public Vector2 OffsetOf(int layer)
        {
            if (!GameSettings.CameraShake) return Vector2.zero;
            float phase = clock * Mathf.PI * 2f / PeriodSeconds;
            return new Vector2(Mathf.Sin(phase) * ReachX[layer], Mathf.Sin(phase * 0.5f) * ReachY[layer]);
        }

        void Apply()
        {
            if (holders == null) return;
            if (!GameSettings.CameraShake && settled) return;   // 움직임을 끄면 한 번 제자리로 놓은 뒤 건드리지 않는다
            settled = !GameSettings.CameraShake;
            for (int i = 0; i < holders.Length; i++)
                if (holders[i] != null) holders[i].anchoredPosition = OffsetOf(i);
        }
    }
}
