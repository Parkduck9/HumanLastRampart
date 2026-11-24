// URP LitForwardPass 위에 투시 구멍만 얹는다 (W42). 원래 조각 셰이더 이름을 바꿔 불러온 뒤, 같은 이름의 새 함수가 구멍 검사 후 원래 것을 부른다
#ifndef HLR_SEE_THROUGH_LIT_PASS_INCLUDED
#define HLR_SEE_THROUGH_LIT_PASS_INCLUDED

#include "Assets/_Project/Combat/Render/HlrSeeThrough.hlsl"

#define LitPassFragment LitPassFragmentBase
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#undef LitPassFragment

void LitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    HlrSeeThroughClip(input.positionWS, input.positionCS);
#ifdef _WRITE_RENDERING_LAYERS
    LitPassFragmentBase(input, outColor, outRenderingLayers);
#else
    LitPassFragmentBase(input, outColor);
#endif
}

#endif
