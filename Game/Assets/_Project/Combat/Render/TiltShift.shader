// 미니어처 흐림 (TiltShiftFeature): 화면 위·아래 띠만 가로·세로 두 번 흐린다. 가운데 띠는 그대로
// _TiltShift: x 위쪽 흐림 시작 높이, y 아래쪽 흐림 끝 높이, z 가장 흐린 곳 반경(화소), w 아래쪽 흐림 배율(가까운 쪽은 더 약하게)
// _Shafts (K-044): 비스듬히 내려오는 빛줄기 흉내 — 세로 패스 끝에 더한다. xyz 색(세기 곱함), w 기울기(화면 가로 이동 / 세로)
//   빛줄기는 화면 위쪽에서 시작해 아래로 갈수록 사라지는 줄무늬. 텍스처를 더 읽지 않는 계산만이라 싸다
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
        float4 _Shafts;
        float _ShaftTime;

        float BlurAmount(float y)
        {
            float top = saturate((y - _TiltShift.x) / max(1.0 - _TiltShift.x, 1e-4));
            float bottom = saturate((_TiltShift.y - y) / max(_TiltShift.y, 1e-4)) * _TiltShift.w;
            float m = max(top, bottom);
            return m * m;   // 가장자리로 갈수록 빠르게
        }

        // 빛줄기: 기울어진 좌표 하나로 굵기가 다른 사인 줄 세 겹 → 부드러운 띠. 위가 밝고 화면 높이 40% 아래로는 없다
        half3 Shafts(float2 uv)
        {
            if (dot(_Shafts.xyz, _Shafts.xyz) < 1e-6) return 0;
            float x = uv.x * (_ScreenParams.x / _ScreenParams.y) + (1.0 - uv.y) * _Shafts.w;
            float bands = sin(x * 7.0 + _ShaftTime * 0.05) * 0.5 + 0.5;
            bands *= sin(x * 13.0 - 1.7 + _ShaftTime * 0.03) * 0.5 + 0.5;
            bands = bands * 0.7 + (sin(x * 3.0 + 0.6) * 0.5 + 0.5) * 0.3;
            float fade = saturate((uv.y - 0.4) / 0.6);
            return _Shafts.xyz * (bands * fade * fade);
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
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = Blur(input, float2(0, 1));
                color.rgb += Shafts(input.texcoord) * (1.0 - color.rgb * 0.5);   // 밝은 곳은 덜 더해 하얗게 날지 않게
                return color;
            }
            ENDHLSL
        }
    }
}
