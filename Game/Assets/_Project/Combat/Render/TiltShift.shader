// 미니어처 흐림 (TiltShiftFeature): 화면 위·아래 띠만 가로·세로 두 번 흐린다. 가운데 띠는 그대로
// _TiltShift: x 위쪽 흐림 시작 높이, y 아래쪽 흐림 끝 높이, z 가장 흐린 곳 반경(화소)
Shader "HLR/TiltShift"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _TiltShift;

        float BlurAmount(float y)
        {
            float top = saturate((y - _TiltShift.x) / max(1.0 - _TiltShift.x, 1e-4));
            float bottom = saturate((_TiltShift.y - y) / max(_TiltShift.y, 1e-4));
            float m = max(top, bottom);
            return m * m;   // 가장자리로 갈수록 빠르게
        }

        half4 Blur(Varyings input, float2 direction)
        {
            float2 uv = input.texcoord;
            float radius = BlurAmount(uv.y) * _TiltShift.z;
            if (radius < 0.05) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            float2 stepUv = direction * _BlitTexture_TexelSize.xy * (radius / 4.0);
            // 9칸 가우스 무게
            const float w[5] = { 0.2270270, 0.1945946, 0.1216216, 0.0540540, 0.0162162 };
            half4 sum = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * w[0];
            [unroll] for (int k = 1; k < 5; k++)
            {
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + stepUv * k) * w[k];
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - stepUv * k) * w[k];
            }
            return sum;
        }
        ENDHLSL

        Pass
        {
            Name "TiltShiftHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target { return Blur(input, float2(1, 0)); }
            ENDHLSL
        }

        Pass
        {
            Name "TiltShiftVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target { return Blur(input, float2(0, 1)); }
            ENDHLSL
        }
    }
}
