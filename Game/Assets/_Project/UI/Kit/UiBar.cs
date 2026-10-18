using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    // 막대 (S-25): 줄어든 부분이 잠깐 밝게 남았다가 따라 줄어든다 (무엇이 방금 줄었는지 보이게).
    // 비율에 따라 색이 바뀌고(좋음 → 황동 → 위험), 30% 아래면 맥박치듯 밝아졌다 어두워진다
    [DisallowMultipleComponent]
    public sealed class UiBar : MonoBehaviour
    {
        public RectTransform Fill, Lag;
        public Image FillImage, LagImage;
        public bool ColorByRatio = true;
        public Color FixedColor = Color.white;

        float value = 1f, lag = 1f, hold;
        UiClock clock;
        bool started;

        public float Value => value;

        public void Set(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (!started) { started = true; value = lag = ratio; Apply(); return; }
            if (ratio < value - 0.0001f) hold = 0.45f;   // 방금 줄었다 — 밝은 꼬리를 잠깐 붙잡아 둔다
            if (ratio > lag) lag = ratio;
            value = ratio;
            Apply();
        }

        void Update()
        {
            float dt = clock.Tick();   // 게임 배속·멈춤과 상관없는 UI 시계
            if (hold > 0f) hold -= dt;
            else if (lag > value) lag = Mathf.MoveTowards(lag, value, dt * 0.6f);
            Apply();
        }

        void Apply()
        {
            if (Fill != null) Fill.anchorMax = new Vector2(value, 1f);
            if (Lag != null) Lag.anchorMax = new Vector2(Mathf.Max(lag, value), 1f);
            if (FillImage == null) return;
            Color c = !ColorByRatio ? FixedColor : value >= 0.7f ? UiTokens.Good : value >= 0.35f ? UiTokens.Brass : UiTokens.Danger;
            if (ColorByRatio && value < 0.3f && value > 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 7f);
                c = Color.Lerp(UiTokens.Danger, UiTokens.DangerLight, pulse);
            }

            FillImage.color = c;
            if (LagImage != null) LagImage.color = UiTokens.BrassLight;
        }
    }
}
