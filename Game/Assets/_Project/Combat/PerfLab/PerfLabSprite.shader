// 성능 시제품용 스프라이트 셰이더 (PERF_PROTOTYPE.md 4장)
// - 개체 데이터는 StructuredBuffer로 받고 SV_InstanceID로 읽는다 (DrawMeshInstancedProcedural)
// - 세로로 선 빌보드: 카메라 좌우 방향으로만 돌린다 (2.5D)
// - 시트는 열×행 격자. 프레임 번호 = 행 × 열 수 + 열 (왼쪽 위부터). 음수면 좌우 뒤집기 (-번호-1)
// - 도트 그림은 반투명 대신 알파 잘라내기 + 깊이 기록으로 그린다
// 종류별 값(_MainTex, _Grid, _Pivot, _Aspect)은 MaterialPropertyBlock으로 넘긴다.
Shader "HLR/PerfLabSprite"
{
    Properties
    {
        _MainTex ("Sheet", 2D) = "white" {}
        _Grid ("Grid (columns, rows)", Vector) = (4, 1, 0, 0)
        _Pivot ("Pivot (x, y in cell 0..1, from bottom-left)", Vector) = (0.5, 0, 0, 0)
        _Aspect ("Cell width / height", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        // 2번 지도 모드(BattleMap.md): 화면을 정면으로 보는 점으로 그린다. 0이면 기존 세로 빌보드 (PerfLab 그대로)
        _MapMode ("Map dot mode", Float) = 0
        _MapColor ("Map dot color (a=0: instance tint)", Color) = (0, 0, 0, 0)
        _MinWorldSize ("Map dot minimum size (world)", Float) = 0
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
                float4 tintSize;    // rgb 색, a 크기(높이). 0이면 그리지 않음
            };

            StructuredBuffer<PerfLabInstance> _Instances;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Grid;
            float4 _Pivot;
            float _Aspect;
            float _Cutoff;
            float _MapMode;
            float4 _MapColor;
            float _MinWorldSize;

            struct Attributes
            {
                float3 positionOS : POSITION;   // x -0.5..0.5, y 0..1
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

                if (_MapMode > 0.5)
                {
                    // 지도: 카메라를 정면으로 보는 점(가운데 기준), 너무 작으면 최소 크기. 크기 0(숨김)은 그대로 숨김
                    float dot = size > 0.0 ? max(size, _MinWorldSize) : 0.0;
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    float3 mapWorld = data.posFrame.xyz + right * (input.positionOS.x * dot) + up * ((input.positionOS.y - 0.5) * dot);
                    Varyings mapOut;
                    mapOut.positionCS = TransformWorldToHClip(mapWorld);
                    mapOut.uv = input.uv;
                    mapOut.tint = _MapColor.a > 0.0 ? (half3)_MapColor.rgb : (half3)data.tintSize.rgb;
                    return mapOut;
                }

                // 뷰 행렬 첫 행 = 카메라 오른쪽 방향(월드). 높이 성분을 빼서 세로로 선 빌보드
                float3 camRight = float3(UNITY_MATRIX_V[0].x, 0.0, UNITY_MATRIX_V[0].z);
                float lenSq = dot(camRight, camRight);
                camRight = lenSq > 1e-6 ? camRight * rsqrt(lenSq) : float3(1.0, 0.0, 0.0);

                // 프레임 번호가 음수면 좌우 뒤집기: 실제 번호 = -w - 1 (주인공 오른쪽 보기 등)
                bool flip = data.posFrame.w < 0.0;
                float frame = flip ? -data.posFrame.w - 1.0 : data.posFrame.w;
                float pivotX = flip ? 1.0 - _Pivot.x : _Pivot.x;
                float localX = (input.positionOS.x + 0.5 - pivotX) * _Aspect * size;
                float localY = (input.positionOS.y - _Pivot.y) * size;
                float3 world = data.posFrame.xyz + camRight * localX + float3(0.0, localY, 0.0);

                float columns = max(_Grid.x, 1.0);
                float rows = max(_Grid.y, 1.0);
                float column = fmod(frame, columns);
                float row = floor(frame / columns);
                float u = flip ? 1.0 - input.uv.x : input.uv.x;

                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = float2((column + u) / columns, 1.0 - (row + 1.0 - input.uv.y) / rows);
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
