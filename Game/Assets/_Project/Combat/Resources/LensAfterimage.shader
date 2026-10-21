Shader "HumanLastRampart/LensAfterimage"
{
    Properties { _MainTex ("점", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // 기존 점과 같은 32바이트. 잔상 버퍼에서만 칸 번호 자리를 불투명도로 쓴다.
            struct Instance { float4 posFrame; float4 tintSize; };
            StructuredBuffer<Instance> _Instances;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Instance data = _Instances[input.instanceID];
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 world = data.posFrame.xyz + right * input.positionOS.x * data.tintSize.w
                    + up * (input.positionOS.y - 0.5) * data.tintSize.w;
                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.color = half4(data.tintSize.rgb, data.posFrame.w);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
