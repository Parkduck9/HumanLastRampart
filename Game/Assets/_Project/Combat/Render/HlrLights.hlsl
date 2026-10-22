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

// 한 점이 목록의 빛에서 받는 값. 확산(감싸는 빛)·반사·번짐을 따로 돌려준다. 반사는 PC에서만, 반사 마스크 있는 그림만
// 감쇠는 반경 기준 — 중심 1, 반경 절반 약 0.1, 반경 끝 0 (웅덩이 크기가 반경을 따른다). 색×세기는 3까지만 (폭발 24가 들판 전체를 덮지 않게)
// rimColor/rimWeight = 가장 센 빛 (테두리빛용), rimSide = 그 빛이 그림 오른쪽(+)/왼쪽(-)에 있는 정도
void HlrAccumulate(float3 posWS, float3 n, float3 viewDir, float specPower, bool specOn, float3 faceRight,
                   out half3 diffuse, out half3 spec, out half3 bounce, out float3 rimColor, out float rimWeight, out float rimSide)
{
    diffuse = 0; spec = 0; bounce = 0; rimColor = 0; rimWeight = 0; rimSide = 0;
    int count = min((int)_HlrLightCount, HLR_MAX_LIGHTS);
    bool high = _HlrQuality > 0.5;
    [loop] for (int i = 0; i < count; i++)
    {
        float3 toLight = _HlrLightPos[i].xyz - posWS;
        float radius = _HlrLightPos[i].w;
        float d2 = dot(toLight, toLight);
        float x = d2 / (radius * radius);
        if (x >= 1.0) continue;
        float3 l = toLight * rsqrt(max(d2, 1e-4));
        float att = (1.0 - x) * (1.0 - x) / (1.0 + 8.0 * x);
        float ndl = saturate((dot(n, l) + 0.5) / 1.5);
        float3 col = min(_HlrLightCol[i].rgb, 3.0);
        diffuse += (half3)col * (half)(att * ndl);
        if (high)
        {
            if (specOn)
            {
                float nh = saturate(dot(n, normalize(l + viewDir)));
                spec += (half3)col * (half)(att * pow(nh, specPower) * saturate(dot(n, l) * 2.0));
            }

            float w = att * dot(col, float3(0.3, 0.5, 0.2));
            if (w > rimWeight) { rimWeight = w; rimColor = col; rimSide = dot(l, faceRight); }
        }

        // 번짐: 큰 불 가까이는 땅이 불빛을 되쏘아 따뜻하게 물든다 (반경 안에서만, 가장자리로 갈수록 급히 줄어듦)
        float wide = 1.0 - x;
        bounce += (half3)col * (half3)(HlrBounceTint * (_HlrLightCol[i].a * wide * wide * wide * 0.06));
    }
}

// 등대 빛줄기 (HlrBeam.cs가 채운다): 한 점이 부채꼴 안에서 받는 세기 0~strength. BeamCone 셰이더와 같은 모양 (성벽 너머 어두운 띠 -> 켜짐 -> 완만히 줄어듦, 부드러운 가장자리)
float4 _HlrBeamA;      // x,y 방향(xz 단위), z 반폭(라디안), w 세기 (0 = 끔)
float4 _HlrBeamB;      // x 성벽 모서리+띠, y 켜지는 구간, z 가장자리 부드러움(라디안), w 줄어드는 길이
half4 _HlrBeamColor;

float HlrBeamFactor(float2 xz)
{
    if (_HlrBeamA.w <= 0.0) return 0.0;
    float r = max(length(xz), 1e-3);
    float a = acos(saturate(dot(xz / r, _HlrBeamA.xy)));
    float edge = 1.0 - smoothstep(_HlrBeamA.z - _HlrBeamB.z, _HlrBeamA.z + _HlrBeamB.z * 0.5, a);
    float t = dot(xz, _HlrBeamA.xy) - _HlrBeamB.x;
    float ramp = smoothstep(0.0, _HlrBeamB.y, t);
    float core = 0.25 + 0.75 * pow(saturate(1.0 - a / max(_HlrBeamA.z, 1e-3)), 0.8);   // 가운데 축이 가장 밝다
    return edge * core * ramp * exp(-max(t, 0.0) / _HlrBeamB.w) * _HlrBeamA.w;
}

#endif
