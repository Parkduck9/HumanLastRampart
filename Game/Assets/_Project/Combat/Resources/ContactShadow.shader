// 접촉 그림자 (W3, K-098): 발밑 부드러운 타원 한 장. PerfLabSprite와 같은 32바이트 개체 버퍼(_Instances)를 그대로 읽어
// 개체 데이터를 다시 올리지 않는다 (CPU 반복 없음). 크기 0(숨김)은 그리지 않는다.
// - 판은 바닥(X/Z)에 눕고, 가로축은 사진기 오른쪽(수평)을 따른다 — 타원이 화면에서 늘 가로로 넓다
// - 발이 땅(_GroundY)에서 떠 있으면(사다리 오르는 적) _FadeHeight까지 옅어진다. _FadeHeight 0이면 안 옅어짐(성벽 위 병사)
// - 알파 블렌드, 깊이 기록 없음, 안개 색으로 섞는다
Shader "HumanLastRampart/ContactShadow"
{
    Properties
    {
        _MainTex ("Blob (K-098, RGB 검정·A 타원)", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 0.65
        _WidthPerSize ("Width = instance size x this", Float) = 1
        _DepthRatio ("Depth / width", Float) = 0.5
        _GroundY ("Ground height for fade", Float) = 0
        _FadeHeight ("Fade-out height (0 off)", Float) = 0
        _Lift ("Lift above feet (m)", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -2, -2
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Instance { float4 posFrame; float4 tintSize; };   // PerfLabInstance와 같은 배치
            StructuredBuffer<Instance> _Instances;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _Opacity;
            float _WidthPerSize;
            float _DepthRatio;
            float _GroundY;
            float _FadeHeight;
            float _Lift;

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };   // x -0.5..0.5, y 0..1
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fade : TEXCOORD1; float fogFactor : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Instance data = _Instances[input.instanceID];
                Varyings output = (Varyings)0;
                float size = data.tintSize.a;
                if (size <= 0.0)
                {
                    output.positionCS = float4(0.0, 0.0, -2.0, 1.0);   // 숨김 (잘려 나감)
                    return output;
                }

                float width = size * _WidthPerSize;
                float depth = width * _DepthRatio;
                float3 right = float3(UNITY_MATRIX_V[0].x, 0.0, UNITY_MATRIX_V[0].z);
                right = dot(right, right) > 1e-6 ? normalize(right) : float3(1.0, 0.0, 0.0);
                float3 forward = float3(-right.z, 0.0, right.x);
                // 사다리를 오르는 적은 발이 공중에 있으므로 그림자는 땅에 붙이고 높이에 따라 옅어지게만 한다
                float3 basePos = data.posFrame.xyz;
                if (_FadeHeight > 0.0) basePos.y = _GroundY;
                float3 world = basePos + float3(0.0, _Lift, 0.0)
                    + right * (input.positionOS.x * width) + forward * ((input.positionOS.y - 0.5) * depth);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.fade = _FadeHeight > 0.0 ? (half)saturate(1.0 - (data.posFrame.y - _GroundY) / _FadeHeight) : 1.0h;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Opacity * input.fade;
                return half4(MixFog(half3(0.0h, 0.0h, 0.0h), input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
