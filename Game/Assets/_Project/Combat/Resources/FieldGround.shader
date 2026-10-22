// 들판 땅 (W15, 사용자 2026-10-22 "너무 하얗고 완전 평면"): 풀·흙 질감 위에 큰 무늬(흙 맨땅·진흙·명암 얼룩)와 잔 자갈·풀포기를 더하고,
// 눈에 안 띄게 낮게 출렁이는 높이(시뮬레이션은 평지 그대로 — 눈에만 보임)와 노멀맵으로 빛이 스치게 한다.
// - 높이는 0 이하(땅이 꺼지는 쪽)라 발이 묻히지 않는다. 성벽에서 멀수록 커진다
// - 달·해 빛(그림자 받음) + 장면 점광원 목록(HlrLights: 횃불·불바다·폭발의 확산·반사·번짐) + 주변광. 모바일(_HlrQuality 0)은 반사·잔 무늬를 줄인다
Shader "HumanLastRampart/FieldGround"
{
    Properties
    {
        _GrassTex ("Grass albedo", 2D) = "gray" {}
        _GrassNrm ("Grass normal", 2D) = "bump" {}
        _DirtTex ("Dirt albedo", 2D) = "gray" {}
        _DirtNrm ("Dirt normal", 2D) = "bump" {}
        _TileMeters ("Texture tile (m)", Float) = 8
        _Darken ("Albedo darken (0..1)", Float) = 0.5
        _Relief ("Height undulation (m)", Float) = 0.14
        _NormalGain ("Normal gain", Float) = 5.0
        _CastleHalf ("Castle half size", Float) = 20
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        float _TileMeters;
        float _Darken;
        float _Relief;
        float _NormalGain;
        float _CastleHalf;

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float VNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // 값 잡음은 0.5 근처에 몰리므로 대비를 넓힌다
        float Stretch(float v) { return saturate((v - 0.5) * 2.4 + 0.5); }

        // 눈에만 보이는 높이 (0 ~ -_Relief). 성벽 가까이는 0 (성벽·성 마당과 맞닿는 곳은 평평하게)
        float GroundHeight(float2 xz)
        {
            float h = VNoise(xz / 26.0) * 0.65 + VNoise(xz / 12.0 + 7.3) * 0.35;
            float fromWall = max(abs(xz.x), abs(xz.y)) - _CastleHalf;
            float fade = smoothstep(5.0, 22.0, fromWall);
            return -h * _Relief * fade;
        }
        ENDHLSL

        Pass
        {
            Name "FieldGround"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Combat/Render/HlrLights.hlsl"

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_GrassNrm); SAMPLER(sampler_GrassNrm);
            TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
            TEXTURE2D(_DirtNrm); SAMPLER(sampler_DirtNrm);

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 world = TransformObjectToWorld(input.positionOS);
                float h = GroundHeight(world.xz);
                // 기울기(큰 출렁임의 방향)는 꼭짓점에서 한 번만 잰다 — 그림자·빛이 완만한 언덕을 스치게
                float e = 1.2;
                float hx = GroundHeight(world.xz + float2(e, 0.0));
                float hz = GroundHeight(world.xz + float2(0.0, e));
                float3 n = normalize(float3(-(hx - h) / e * 14.0, 1.0, -(hz - h) / e * 14.0));   // 기울기를 크게 과장 — 빛이 스치는 느낌만 키운다
                world.y += h;
                output.positionWS = world;
                output.normalWS = n;
                output.positionCS = TransformWorldToHClip(world);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;
                bool high = _HlrQuality > 0.5;

                // 큰 무늬: 흙 맨땅·진흙·명암 얼룩
                float n1 = Stretch(VNoise(xz / 12.0 + 11.0) * 0.7 + VNoise(xz / 27.0) * 0.3);
                float n2 = Stretch(VNoise(xz / 5.5 + 3.0));
                float n3 = Stretch(VNoise(xz / 2.2 + 9.0));
                float dirtMask = smoothstep(0.4, 0.58, n1 * 0.7 + n2 * 0.3);
                float mudMask = smoothstep(0.5, 0.66, Stretch(VNoise(xz / 7.5 + 50.0)) * 0.7 + n3 * 0.3) * (1.0 - dirtMask * 0.4);

                float2 uvG = xz / _TileMeters;
                float2 uvD = xz / _TileMeters + float2(0.37, 0.61);
                half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uvG).rgb;
                half3 dirt = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uvD).rgb;
                half3 albedo = lerp(grass, dirt, (half)dirtMask);
                albedo = lerp(albedo, albedo * albedo * 1.15h, (half)_Darken);   // 너무 하얗지 않게: 중간 밝기를 눌러 대비를 키운다
                albedo *= lerp(half3(0.3h, 0.24h, 0.18h), half3(1, 1, 1), (half)(1.0 - mudMask));
                albedo *= (half)lerp(0.55, 1.3, n2 * 0.55 + n3 * 0.45);

                // 노멀: 질감 노멀 + 잔 자갈·풀포기
                half3 tg = UnpackNormal(SAMPLE_TEXTURE2D(_GrassNrm, sampler_GrassNrm, uvG));
                half3 td = UnpackNormal(SAMPLE_TEXTURE2D(_DirtNrm, sampler_DirtNrm, uvD));
                half3 tn = lerp(tg, td, (half)dirtMask);
                float2 bump = tn.xy * _NormalGain;

                if (high)
                {
                    float2 cell = floor(xz / 0.45);
                    float rnd = Hash21(cell);
                    float2 local = frac(xz / 0.45);
                    float2 center = float2(Hash21(cell + 3.7), Hash21(cell + 9.1)) * 0.6 + 0.2;
                    float2 off = (local - center) * 0.45;
                    float d = length(off);
                    float radius = lerp(0.035, 0.085, Hash21(cell + 5.3));
                    if (rnd < 0.2)
                    {
                        float stone = smoothstep(radius, radius * 0.55, d);
                        half3 stoneColor = half3(0.26h, 0.25h, 0.23h) * (half)lerp(0.7, 1.25, Hash21(cell + 1.3));
                        albedo = lerp(albedo, stoneColor, (half)(stone * 0.9));
                        bump += -off / max(radius, 0.01) * stone * 0.9;   // 작은 돔: 빛이 스치면 한쪽이 밝다
                    }
                    else if (rnd < 0.34)
                    {
                        float tuft = smoothstep(radius * 1.4, 0.0, d);
                        albedo *= (half)lerp(1.0, 0.62, tuft);
                        albedo += half3(0.012h, 0.03h, 0.0h) * (half)tuft;
                    }
                }

                float3 nGeo = normalize(input.normalWS);
                float3 n = normalize(float3(nGeo.x + bump.x, nGeo.y, nGeo.z + bump.y));

                // 빛: 달·해(그림자 받음) + 주변광 + 점광원 목록
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 sun = mainLight.color * (saturate(dot(n, mainLight.direction)) * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(n);
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                half3 diffuse, spec, bounce;
                float3 rimC; float rimW, rimS;
                HlrAccumulate(input.positionWS, n, viewDir, lerp(10.0, 30.0, saturate(dirtMask + 0.2)), true, float3(1, 0, 0), diffuse, spec, bounce, rimC, rimW, rimS);

                half3 color = albedo * (sun * 1.6h + ambient * 0.6h + diffuse + bounce);   // 주변광은 줄이고 달빛 비중을 키워 요철·무늬가 읽히게
                color += spec * (half)(0.02 * (1.0 - dirtMask * 0.4)) * (half3)lerp(half3(0.6, 0.55, 0.5), albedo * 2.0h, 0.4h);   // 은은한 반사 (젖은 흙이 빛을 머금는 정도)
                return half4(MixFog(color, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.5

            struct DAttributes { float3 positionOS : POSITION; };
            struct DVaryings { float4 positionCS : SV_POSITION; };

            DVaryings DepthVert(DAttributes input)
            {
                DVaryings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                world.y += GroundHeight(world.xz);
                output.positionCS = TransformWorldToHClip(world);
                return output;
            }

            half4 DepthFrag(DVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}




















