using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.PerfLab
{
    // 해상도 모드 (D-021 G1)
    // - 내부 절반 + 2배 확대: 카메라를 화면의 절반 크기 렌더텍스처에 그리고, 점 필터로 화면 전체에 늘려 보인다
    //   (4K 화면이면 1920×1080으로 그려 정확히 2배 — 도트가 번지지 않는다)
    // - 네이티브: 화면 해상도 그대로 그린다
    public sealed class PerfLabResolution : MonoBehaviour
    {
        Camera sceneCamera;
        Camera presentCamera;
        RawImage presentImage;
        RenderTexture target;
        int targetScreenWidth;
        int targetScreenHeight;

        string description = "";

        public bool HalfScale { get; private set; } = true;

        // 매 프레임 불려도 메모리를 새로 잡지 않도록, 모드나 화면 크기가 바뀔 때만 다시 만든다.
        public string Describe() => description;

        public void Init(Camera camera)
        {
            sceneCamera = camera;

            // 화면에 아무 카메라도 그리지 않으면 Game 창에 경고가 뜨므로, 아무것도 안 그리는 검은 카메라를 둔다.
            var presentObject = new GameObject("PerfLab Present Camera", typeof(Camera));
            presentObject.transform.SetParent(transform, false);
            presentCamera = presentObject.GetComponent<Camera>();
            presentCamera.cullingMask = 0;
            presentCamera.clearFlags = CameraClearFlags.SolidColor;
            presentCamera.backgroundColor = Color.black;
            presentCamera.depth = sceneCamera.depth + 1;

            var canvasObject = new GameObject("PerfLab Present", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;   // 성능 표시(1000)보다 아래

            var imageObject = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            presentImage = imageObject.GetComponent<RawImage>();
            presentImage.raycastTarget = false;

            Apply();
        }

        public void SetHalfScale(bool half)
        {
            HalfScale = half;
            Apply();
        }

        void LateUpdate()
        {
            // Game 창 크기가 바뀌면 렌더텍스처(또는 설명)를 다시 만든다.
            if (Screen.width != targetScreenWidth || Screen.height != targetScreenHeight)
            {
                Apply();
            }
        }

        void OnDestroy()
        {
            ReleaseTarget();
        }

        void Apply()
        {
            if (sceneCamera == null)
            {
                return;
            }

            ReleaseTarget();
            targetScreenWidth = Screen.width;
            targetScreenHeight = Screen.height;
            if (HalfScale)
            {
                target = new RenderTexture(Mathf.Max(1, Screen.width / 2), Mathf.Max(1, Screen.height / 2), 24)
                {
                    name = "PerfLab Half Target",
                    filterMode = FilterMode.Point,
                };
                target.Create();
                sceneCamera.targetTexture = target;
                presentImage.texture = target;
            }

            presentImage.enabled = HalfScale;
            presentCamera.enabled = HalfScale;
            description = HalfScale
                ? $"내부 {target.width}x{target.height} → 화면 {Screen.width}x{Screen.height}"
                : $"네이티브 {Screen.width}x{Screen.height}";
        }

        void ReleaseTarget()
        {
            if (sceneCamera != null)
            {
                sceneCamera.targetTexture = null;
            }

            if (target != null)
            {
                target.Release();
                Destroy(target);
                target = null;
            }
        }
    }
}
