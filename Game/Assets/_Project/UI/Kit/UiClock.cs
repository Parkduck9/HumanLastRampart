using UnityEngine;

namespace HumanLastRampart.UI
{
    // UI 움직임 시계 (K-047 고침): 게임 시간 배속·멈춤(timeScale)과 상관없이 흐른다.
    // 한 프레임의 길이 = max(unscaledDeltaTime, 지난 프레임부터 흐른 실제 시간).
    // - 보통 플레이: 둘이 같다 (실제 시간)
    // - 시험(PlayProbe)의 captureDeltaTime: 프레임은 몇 ms 만에 지나가도 unscaledDeltaTime이 고정 간격(1/20초)이라 연출이 몇 프레임 안에 끝난다
    //   (실제 시간만 쓰면 판이 투명한 채로 사진이 찍혔다 — 2026-10-19 사진 44·50)
    public struct UiClock
    {
        float last;
        bool started;

        public float Tick(float max = 0.1f)
        {
            float now = Time.realtimeSinceStartup;
            float real = started ? now - last : 0f;
            last = now;
            bool first = !started;
            started = true;
            if (first) return 0f;
            return Mathf.Clamp(Mathf.Max(Time.unscaledDeltaTime, real), 0f, max);
        }
    }
}
