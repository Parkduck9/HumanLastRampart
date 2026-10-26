// 주인공 둘레 투시 구멍 (W42, 사용자 2026-10-26 "건물이 가리면 주인공만 동그랗게 뚫어서 보이게").
// BattleSeeThrough.cs가 전역 값을 채운다. 가리는 면(HlrSeeThroughLit/Unlit 셰이더)이 이 구멍 안이면서 주인공보다 사진기에 가까울 때만 조각을 버린다.
// 도트 느낌을 지키려고 부드러운 가장자리도 4x4 정렬 디더(Bayer)로 — 반투명 판·추가 그리기 없음.
#ifndef HLR_SEE_THROUGH_INCLUDED
#define HLR_SEE_THROUGH_INCLUDED

float4 _HlrSeeThroughA;   // xy 주인공 중심(시야 평면 탄젠트 좌표 = 보기 좌표 xy / 깊이), z 구멍 반지름(탄젠트), w 주인공 깊이(m)
float4 _HlrSeeThroughB;   // x 켜짐 정도 0~1 (0이면 아무것도 안 함), y 깊이 여유(m), z 이 높이(월드 y) 아래는 안 뚫음(바닥), w 안쪽 비율(그 안은 완전히 뚫림)

static const float HlrBayer4[16] =
{
     0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
    12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
     3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
    15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0,
};

// 구멍에 들어가는 정도 0~1 (1 = 완전히 뚫림). 주인공보다 뒤이거나 바닥이면 0
float HlrSeeThroughCover(float3 positionWS)
{
    if (_HlrSeeThroughB.x <= 0.001) return 0.0;
    if (positionWS.y <= _HlrSeeThroughB.z) return 0.0;                      // 바닥(통로·마당 돌바닥)은 그대로
    float3 vpos = mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).xyz;
    float depth = -vpos.z;                                                  // 사진기에서 앞으로 거리
    if (depth >= _HlrSeeThroughA.w - _HlrSeeThroughB.y) return 0.0;         // 주인공과 같거나 뒤에 있는 것은 그대로
    float2 t = vpos.xy / max(depth, 1e-3);
    float d = length(t - _HlrSeeThroughA.xy) / max(_HlrSeeThroughA.z, 1e-5);   // 0 중심, 1 가장자리
    float inner = saturate(_HlrSeeThroughB.w);
    return saturate((1.0 - d) / max(1.0 - inner, 1e-3)) * _HlrSeeThroughB.x;
}

// 조각 버리기. positionCS는 프래그먼트 입력(SV_POSITION) — 화면 화소 좌표라 정렬 디더가 화소에 딱 붙는다
void HlrSeeThroughClip(float3 positionWS, float4 positionCS)
{
    float cover = HlrSeeThroughCover(positionWS);
    if (cover <= 0.0) return;
    uint2 p = (uint2)positionCS.xy & 3u;
    float threshold = HlrBayer4[p.y * 4u + p.x] + 0.5 / 16.0;
    clip(threshold - cover);   // cover가 문턱보다 크면 버린다 (cover 1이면 전부)
}

#endif
