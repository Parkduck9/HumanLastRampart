using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    // 위급 신호 (S-27): 화면 가장자리 붉은 빛이 몇 초 동안 맥박치다 사라진다. 시간은 실제 시간
    [DisallowMultipleComponent]
    public sealed class UiPulse : MonoBehaviour
    {
        Graphic graphic;
        float until, strength;

        public static void Play(Graphic target, float seconds, float strength = 0.6f)
        {
            if (target == null) return;
            if (!target.TryGetComponent(out UiPulse pulse)) pulse = target.gameObject.AddComponent<UiPulse>();
            pulse.graphic = target;
            pulse.until = Time.realtimeSinceStartup + seconds;
            pulse.strength = strength;
            target.gameObject.SetActive(true);
        }

        void Update()
        {
            float left = until - Time.realtimeSinceStartup;
            if (graphic == null) return;
            if (left <= 0f)
            {
                graphic.color = UiTokens.Alpha(graphic.color, 0f);
                gameObject.SetActive(false);
                return;
            }

            float beat = 0.55f + 0.45f * Mathf.Sin(Time.realtimeSinceStartup * 6f);
            float fade = Mathf.Clamp01(left / 0.8f);
            graphic.color = UiTokens.Alpha(graphic.color, strength * beat * fade);
        }
    }
}
