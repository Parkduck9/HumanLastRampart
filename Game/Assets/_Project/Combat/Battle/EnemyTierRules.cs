using Unity.Mathematics;

namespace HumanLastRampart.Combat.Battle
{
    // 적 변형 단계 배율 (등대 설계 9.5). 0 홀림(그대로) · 1 물듦 · 2 변형.
    // 순수 계산이라 시험과 그리기 작업(Burst)이 함께 쓴다.
    public static class EnemyTierRules
    {
        public const int MaxTier = 2;

        public static int Clamp(int tier) => tier < 0 ? 0 : (tier > MaxTier ? MaxTier : tier);

        public static float HpMultiplier(int tier) => Clamp(tier) == 0 ? 1f : (Clamp(tier) == 1 ? 1.25f : 1.5f);

        public static float SpeedMultiplier(int tier) => Clamp(tier) == 2 ? 1.15f : 1f;

        public static float ScaleMultiplier(int tier) => Clamp(tier) == 2 ? 1.15f : 1f;

        // 그림 색 곱: 어둡게 + 살짝 보라 (물듦·변형 같은 색)
        public static float3 TintMultiplier(int tier) => Clamp(tier) == 0 ? new float3(1f, 1f, 1f) : new float3(0.6f, 0.5f, 0.66f);

        // 변형(2단계)의 안개 일렁임 — 그림 카드가 오면 안개 연기 그림으로 바꾼다 (TODO: 눈 마스크·안개 스프라이트는 그림 카드 후)
        // 지금은 색이 천천히 어두워졌다 밝아지는 것으로 대신한다 (적 한 마리당 곱셈 몇 번이라 8000마리여도 싸다)
        public static float WispPulse(int tier, float time, int index) =>
            Clamp(tier) == 2 ? 0.88f + 0.12f * math.sin(time * 1.6f + index * 0.7f) : 1f;
    }
}
