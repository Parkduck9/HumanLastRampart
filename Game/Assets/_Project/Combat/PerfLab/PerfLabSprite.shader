// 성능 시제품용 스프라이트 셰이더 (PERF_PROTOTYPE.md 4장)
// - 개체 데이터는 StructuredBuffer로 받고 SV_InstanceID로 읽는다 (DrawMeshInstancedProcedural)
// - 세로로 선 빌보드: 카메라 좌우 방향으로만 돌린다 (2.5D, 발 위치가 원점)
// - 도트 그림은 반투명 대신 알파 잘라내기 + 깊이 기록으로 그린다
Shader "HLR/PerfLabSprite"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _FrameCount ("Frame Count", Float) = 4
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PerfLabSprite"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // C# PerfLabInstance와 같은 배치 (32바이트)
            struct PerfLabInstance
            {
                float4 posFrame;    // xyz 발 위치, w 프레임 번호
                float4 tintSize;    // rgb 색, a 크기
            };

            StructuredBuffer<PerfLabInstance> _Instances;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _FrameCount;
            float _Cutoff;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 tint : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                PerfLabInstance data = _Instances[input.instanceID];
                float size = data.tintSize.a;

                // 뷰 행렬 첫 행 = 카메라 오른쪽 방향(월드). 높이 성분을 빼서 세로로 선 빌보드
                float3 camRight = float3(UNITY_MATRIX_V[0].x, 0.0, UNITY_MATRIX_V[0].z);
                float lenSq = dot(camRight, camRight);
                camRight = lenSq > 1e-6 ? camRight * rsqrt(lenSq) : float3(1.0, 0.0, 0.0);

                float3 world = data.posFrame.xyz
                    + camRight * (input.positionOS.x * size)
                    + float3(0.0, input.positionOS.y * size, 0.0);

                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = float2((input.uv.x + data.posFrame.w) / _FrameCount, input.uv.y);
                output.tint = (half3)data.tintSize.rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(color.a - _Cutoff);
                return half4(color.rgb * input.tint, 1.0h);
            }
            ENDHLSL
        }
    }
}
