using UnityEngine;

namespace HumanLastRampart.Combat.Render
{
    // 밤 밝기 한 손잡이 (사용자 2026-10-20 "밤이 너무 어둡다" — 단계 세 개를 사진으로 비교해 고른다).
    // Level 0 = 예전 밤(어두움), 1 = 많이 밝게. 값 하나로 그림 밝기·노출·가장자리 어둡게·환경광·달빛·횃불을 함께 움직인다
    public static class NightTuning
    {
        static float level = DefaultLevel;
        public const float DefaultLevel = 0.4f;   // 사용자 결정 2026-10-22: "밤을 훨씬 어둡게, 옥토패스식 — 빛 받는 곳만 또렷" (이전 2026-10-20 0.65). 달빛 바닥은 형체가 보일 만큼만

        public static float Level
        {
            get => level;
            set { level = Mathf.Clamp01(value); Version++; }
        }

        public static int Version { get; private set; }   // 바뀌면 횃불·화면 효과를 다시 정한다

        // 첫 비교(0~1 폭이 좁아 차이가 거의 안 보임) 뒤 넓힘 — 1이면 "달 밝은 밤"
        public static float SpriteBrightness => Mathf.Lerp(0.5f, 0.95f, level);   // 빛 안 받는 그림의 밤 밝기
        public static float Exposure => Mathf.Lerp(-1.1f, 0.5f, level);
        public static float Vignette => Mathf.Lerp(0.52f, 0.28f, level);
        public static float AmbientScale => Mathf.Lerp(0.3f, 1.8f, level);
        public static Color AmbientFloor => Color.Lerp(Color.black, new Color(0.14f, 0.17f, 0.27f), level);   // 달빛 바닥 (형체는 늘 보이게)
        public static float SunScale => Mathf.Lerp(0.15f, 0.6f, level);   // 달(해 빛을 밤에 줄인 값)
        public static float TorchIntensity => Mathf.Lerp(0.45f, 0.9f, level);
        public static float TorchRange => Mathf.Lerp(1.3f, 2.4f, level);
    }
}


