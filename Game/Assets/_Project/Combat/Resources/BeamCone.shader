// 등대 빛줄기 (W15, 사용자 2026-10-22): 탑 꼭대기에서 성벽 너머로 뻗는 부채꼴 빛. 사진기 뒤·위의 탑에서 쏘므로
// 땅 위 빛은 성벽 바로 너머에서 (난간 그림자 같은) 짧은 어두운 띠 뒤에 시작해 바깥으로 갈수록 넓어지고, 가장자리는 부드럽다.
// - 부채꼴 모양은 시뮬레이션 그대로(탑 가운데에서 각도·폭) — 폭·느려짐·피해 숫자는 건드리지 않는다. 눈에 보이는 빛만 그린다
// - 덧셈 블렌드(빛 웅덩이), 깊이 기록 없음. 모든 값은 재질/블록 속성 (현재 방향·목표 방향 두 장을 따로 그린다)
// - _Mode 0 = 들판 위(땅 높이): 성벽 끝에서 어두운 띠(_Strip) → 올라오는 구간(_Ramp) → 완만히 줄어듦(_Fall). 1 = 마당 위: 탑에서 가까운 쪽부터 연하게, 2 = 꼭대기 불방에서 땅으로 비스듬히 내려오는 연한 판 (W20)
Shader "HumanLastRampart/BeamCone"
{
    Properties
    {
        _Mode ("0 field / 1 yard", Float) = 0
        _Dir ("Direction xz (unit)", Vector) = (0, 1, 0, 0)
        _Half ("Half angle (rad), soft band (rad), strength, castle half", Vector) = (0.5, 0.15, 1, 20)
        _Profile ("Strip, ramp, falloff length, unused", Vector) = (2.5, 8, 60, 0)
        _Color ("Beam color (HDR)", Color) = (1, 0.72, 0.36, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "BeamCone"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -2, -2
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Mode;
            float4 _Dir;
            float4 _Half;
            float4 _Profile;
            half4 _Color;

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;
                float r = max(length(xz), 1e-3);
                float c = dot(xz / r, _Dir.xy);
                float a = acos(saturate(c));
                float half_ = _Half.x, soft = _Half.y;
                float edge = 1.0 - smoothstep(half_ - soft, half_ + soft * 0.5, a);   // 가장자리 부드럽게
                float core = 0.25 + 0.75 * pow(saturate(1.0 - a / max(half_, 1e-3)), 0.8);     // 가운데가 조금 더 밝다
                float t = dot(xz, _Dir.xy) - _Half.w;                                  // 성벽 바깥 모서리에서 빛줄기 방향으로 잰 거리
                float profile;
                if (_Mode > 1.5)
                {
                    profile = (1.0 - smoothstep(0.0, _Half.w + 8.0, r)) * 0.8;   // 모드 2 (W20): 꼭대기 불방에서 내려오는 연한 판 — 탑에서 멀수록 옅어진다
                }
                else if (_Mode < 0.5)
                {
                    float ramp = smoothstep(_Profile.x, _Profile.x + _Profile.y, t);   // 어두운 띠 뒤에서 켜진다
                    float fall = exp(-max(t - _Profile.x, 0.0) / _Profile.z);   // 그림 셰이더(HlrBeamFactor)와 같은 기준점: 성벽 모서리 + 띠
                    profile = ramp * fall;
                }
                else
                {
                    profile = smoothstep(1.0, 9.0, r) * (1.0 - smoothstep(_Half.w - 4.0, _Half.w - 1.0, r)) * 0.55;   // 마당: 탑 곁은 비우고 성벽 가까이서 사라짐
                }

                half3 color = _Color.rgb * (half)(edge * core * profile * _Half.z * ComputeFogIntensity(input.fogFactor));   // 비·안개 날 멀리서는 흐려진다
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
