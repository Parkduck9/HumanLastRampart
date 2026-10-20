using UnityEngine;

namespace HumanLastRampart.Combat.Render
{
    // 밤 밝기 한 손잡이 (사용자 2026-10-20 "밤이 너무 어둡다" — 단계 세 개를 사진으로 비교해 고른다).
    // Level 0 = 예전 밤(어두움), 1 = 많이 밝게. 값 하나로 그림 밝기·노출·가장자리 어둡게·환경광·달빛·횃불을 함께 움직인다
    public static class NightTuning
    {
        static float level = DefaultLevel;
        public const float DefaultLevel = 0.65f;   // 사용자 선택 2026-10-20: 처음 0.4 → "0.65로 변경" (사진 Docs/References/night_levels_20261020.png)

        public static float Level
        {
            get => level;
            set { level = Mathf.Clamp01(value); Version++; }
        }

        public static int Version { get; private set; }   // 바뀌면 횃불·화면 효과를 다시 정한다

        // 첫 비교(0~1 폭이 좁아 차이가 거의 안 보임) 뒤 넓힘 — 1이면 "달 밝은 밤"
        public static float SpriteBrightness => Mathf.Lerp(0.5f, 0.95f, level);   // 빛 안 받는 그림의 밤 밝기
        public static float Exposure => Mathf.Lerp(-0.15f, 0.9f, level);
        public static float Vignette => Mathf.Lerp(0.38f, 0.18f, level);
        public static float AmbientScale => Mathf.Lerp(0.6f, 2.6f, level);
        public static Color AmbientFloor => Color.Lerp(Color.black, new Color(0.22f, 0.25f, 0.34f), level);   // 달빛 바닥 (형체는 늘 보이게)
        public static float SunScale => Mathf.Lerp(0.25f, 0.85f, level);   // 달(해 빛을 밤에 줄인 값)
        public static float TorchIntensity => Mathf.Lerp(0.8f, 1.6f, level);
        public static float TorchRange => Mathf.Lerp(2f, 4f, level);
    }
}
