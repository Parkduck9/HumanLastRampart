// 땅 위 따뜻한 빛 웅덩이 (W15, 옥토패스식 고전 기법): 불·불바다·횃불·폭발 자리에 눕힌 부드러운 원형 덧칠 판 한 장씩.
// 개수 제한(PC 96 / 모바일 24)이 있는 인스턴싱 한 번에 그린다. 빛 계산이 아니라 덧셈이라 감쇠식·불 그림 가림과 상관없이 땅에 웅덩이가 보인다.
// - _Pos: xyz 가운데(땅 높이), w 반지름. _Col: rgb 색×세기, a 깜박임 씨앗
// - 가장자리로 갈수록 부드럽게 (1-r²)², 시간에 따라 조금 깜박인다. 덧셈 블렌드, 깊이 기록 없음
Shader "HumanLastRampart/GroundGlow"
{
    Properties { _Night ("Night multiplier", Float) = 1 }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-15" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GroundGlow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -3, -3
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4> _GlowPos;
            StructuredBuffer<float4> _GlowCol;
            float _Night;

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 color : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                float4 pr = _GlowPos[input.instanceID];
                float4 cs = _GlowCol[input.instanceID];
                float flicker = 0.88 + 0.12 * sin(_Time.y * 9.0 + cs.a * 31.0) * sin(_Time.y * 5.3 + cs.a * 17.0);
                Varyings output = (Varyings)0;
                float3 world = float3(pr.x + input.positionOS.x * pr.w * 2.0, pr.y + 0.05, pr.z + input.positionOS.z * pr.w * 2.0);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.positionOS.xz * 2.0;
                output.color = (half3)(cs.rgb * (flicker * _Night));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r2 = dot(input.uv, input.uv);
                float a = saturate(1.0 - r2);
                a = a * a;
                return half4(input.color * (half)a, 1.0h);
            }
            ENDHLSL
        }
    }
}
