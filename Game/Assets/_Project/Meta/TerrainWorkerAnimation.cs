using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 지형 손질 작업 연출 (K-051, Codex K-030 병사 작업 시트): 지도 칸 위에서 병사가 잠깐 삽질·흙 나르기·통나무 나르기를 하고 사라진다.
    // 시트 1024×384 = 4칸(256×384, 발 (128,352)), 초당 5칸. 정비 화면 연출만 — 실제 병사가 들판에 나가지는 않는다 (질문 Q8)
    public sealed class TerrainWorkerAnimation : MonoBehaviour
    {
        const float Fps = 5f;
        Sprite[] frames;
        Image image;
        float age, seconds;

        // sheet: 작업 시트, seconds: 보여 줄 시간. 칸 그림(스프라이트)은 사라질 때 같이 지운다
        public static void Play(RectTransform cell, Texture2D sheet, float seconds, float heightPx)
        {
            if (cell == null || sheet == null) return;
            var go = new GameObject("Worker", typeof(RectTransform), typeof(Image), typeof(TerrainWorkerAnimation));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(cell, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 32f / 384f);              // 발 (128,352)
            rect.sizeDelta = new Vector2(heightPx * 256f / 384f, heightPx);
            rect.anchoredPosition = Vector2.zero;
            var anim = go.GetComponent<TerrainWorkerAnimation>();
            anim.image = go.GetComponent<Image>();
            anim.image.raycastTarget = false;
            anim.seconds = seconds;
            sheet.filterMode = FilterMode.Point;
            int count = Mathf.Max(1, sheet.width * 384 / (256 * Mathf.Max(1, sheet.height)));   // 가로 칸 수 (가져오기에서 크기가 바뀌어도 비율로)
            anim.frames = new Sprite[count];
            float w = sheet.width / (float)count;
            for (int k = 0; k < count; k++) anim.frames[k] = Sprite.Create(sheet, new Rect(k * w, 0f, w, sheet.height), new Vector2(0.5f, 32f / 384f), 100f);
            anim.image.sprite = anim.frames[0];
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (age >= seconds) { Destroy(gameObject); return; }
            image.sprite = frames[(int)(age * Fps) % frames.Length];
        }

        void OnDestroy()
        {
            if (frames == null) return;
            foreach (Sprite s in frames) if (s != null) Destroy(s);
        }
    }
}
