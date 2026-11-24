using UnityEngine;

namespace HumanLastRampart.Combat.Render
{
    // 등대 빛줄기 셰이더 값 (W15): 땅 위 빛(BeamCone 셰이더)과 그림이 받는 빛(PerfLabSprite)이 같은 부채꼴·어두운 띠·부드러운 가장자리를 쓰도록 전역으로도 넘긴다.
    // 수치는 보이는 빛에만 쓰인다 (폭·느려짐·피해는 BattleSim.Lamp 그대로)
    public static class HlrBeam
    {
        public const float Strip = 3.5f;     // 성벽 바깥 모서리 바로 너머의 어두운 띠 (m) — 난간 그림자
        public const float Ramp = 10f;        // 띠 뒤에서 켜지는 구간 (m)
        public const float FallLength = 30f; // 바깥으로 갈수록 줄어드는 길이 (m)
        public static readonly Color BeamColor = new Color(1f, 0.72f, 0.36f);

        static readonly int AId = Shader.PropertyToID("_HlrBeamA");
        static readonly int BId = Shader.PropertyToID("_HlrBeamB");
        static readonly int ColorId = Shader.PropertyToID("_HlrBeamColor");

        public static float SoftRadians(float halfRadians) => halfRadians * 0.3f;

        // 현재 빛줄기를 그림 셰이더에 넘긴다. strength 0이면 끔
        public static void Set(float angleDegrees, float widthDegrees, float strength, float castleHalf)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            float half = widthDegrees * 0.5f * Mathf.Deg2Rad;
            Shader.SetGlobalVector(AId, new Vector4(Mathf.Cos(rad), Mathf.Sin(rad), half, strength));
            Shader.SetGlobalVector(BId, new Vector4(castleHalf + Strip, Ramp, SoftRadians(half), FallLength));
            Shader.SetGlobalColor(ColorId, BeamColor);
        }

        public static void Clear() => Shader.SetGlobalVector(AId, Vector4.zero);
    }
}
