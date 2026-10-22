// 등대 빛 번쩍 (W9, Codex K-102 빛기둥·K-106 렌즈 빛): 카메라를 보는 판에 더하기(SrcAlpha One)로 그린다.
// 개체 버퍼는 PerfLabSprite와 같은 32바이트: posFrame.xyz = 기준점(발) 위치, tintSize.rgb = 색 × 세기(1 넘으면 빛번짐), tintSize.a = 판 높이(m). 높이 0이면 안 그린다.
Shader "HumanLastRampart/BeaconGlow"
{
    Properties
    {
        _MainTex ("그림", 2D) = "white" {}
        _Pivot ("기준점 (칸 안 0~1, 왼쪽 아래)", Vector) = (0.5, 0, 0, 0)
        _Aspect ("가로 / 세로", Float) = 1
        _Push ("카메라에서 멀리 미는 거리 (m, 탑 뒤에 둔다)", Float) = 0.05
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Instance { float4 posFrame; float4 tintSize; };
            StructuredBuffer<Instance> _Instances;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Pivot;
            float _Aspect;
            float _Push;
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 color : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Instance data = _Instances[input.instanceID];
                Varyings output = (Varyings)0;
                float size = data.tintSize.a;
                if (size <= 0.0)
                {
                    output.positionCS = float4(0.0, 0.0, -2.0, 1.0);   // 안 그림
                    return output;
                }
                float3 right = normalize(UNITY_MATRIX_V[0].xyz);
                float3 up = normalize(UNITY_MATRIX_V[1].xyz);
                float3 forward = -normalize(UNITY_MATRIX_V[2].xyz);
                float3 world = data.posFrame.xyz + forward * _Push
                    + right * ((input.positionOS.x + 0.5 - _Pivot.x) * _Aspect * size)
                    + up * ((input.positionOS.y - _Pivot.y) * size);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.color = (half3)data.tintSize.rgb;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return half4(c.rgb * input.color, c.a);
            }
            ENDHLSL
        }
    }
}
