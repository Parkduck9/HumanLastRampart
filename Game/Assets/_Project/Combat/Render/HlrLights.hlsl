// 장면 점광원 목록 (HlrLights.cs가 채운다). 들판·그림 셰이더가 같이 쓴다.
#ifndef HLR_LIGHTS_INCLUDED
#define HLR_LIGHTS_INCLUDED

#define HLR_MAX_LIGHTS 16
float4 _HlrLightPos[HLR_MAX_LIGHTS];   // xyz 위치, w 닿는 거리
float4 _HlrLightCol[HLR_MAX_LIGHTS];   // rgb 색×세기, a 바닥 반사광(번짐) 세기
float _HlrLightCount;
float _HlrQuality;                     // 1 = PC 전체 계산, 0 = 모바일 약식 (확산만)
float _HlrNight;

static const float3 HlrBounceTint = float3(0.9, 0.5, 0.26);   // 불빛이 땅에 튀어 돌아오는 따뜻한 흙빛

// 한 점이 목록의 빛에서 받는 값. 확산(감싸는 빛)·반사·번짐을 따로 돌려준다. 반사·테두리는 PC에서만
void HlrAccumulate(float3 posWS, float3 n, float3 viewDir, float specPower,
                   out half3 diffuse, out half3 spec, out half3 bounce)
{
    diffuse = 0; spec = 0; bounce = 0;
    int count = min((int)_HlrLightCount, HLR_MAX_LIGHTS);
    [loop] for (int i = 0; i < count; i++)
    {
        float3 toLight = _HlrLightPos[i].xyz - posWS;
        float radius = _HlrLightPos[i].w;
        float d2 = dot(toLight, toLight);
        float r2 = radius * radius;
        if (d2 >= r2 * 2.5) continue;
        float3 l = toLight * rsqrt(max(d2, 1e-4));
        float a = saturate(1.0 - d2 / r2);
        float att = a * a / (d2 + 0.25);
        float ndl = saturate((dot(n, l) + 0.5) / 1.5);
        half3 c = (half3)_HlrLightCol[i].rgb;
        diffuse += c * (att * ndl);
        if (_HlrQuality > 0.5)
        {
            float nh = saturate(dot(n, normalize(l + viewDir)));
            spec += c * (att * pow(nh, specPower) * saturate(dot(n, l) * 2.0));
        }

        // 번짐: 큰 불 가까이는 땅이 불빛을 되쏘아 사방이 따뜻하게 물든다 (방향 없이 넓게, 약하게)
        float wide = saturate(1.0 - d2 / (r2 * 2.5));
        bounce += c * (HlrBounceTint * (_HlrLightCol[i].a * wide * wide * 0.09));
    }
}

#endif
