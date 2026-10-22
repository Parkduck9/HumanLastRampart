using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Render
{
    // 장면 점광원 목록 (W15, 사용자 2026-10-22 옥토패스식 빛): 횃불·불바다·폭발 빛을 "개수 제한 있는 목록"으로 셰이더(들판·그림)에 넘긴다.
    // 이유: 유니티 기본 추가 조명은 개체(큰 판·그림 묶음) 하나당 4개까지만 받아서 흩어진 적·불이 서로 빛을 못 받았다.
    // - 셰이더는 HlrLights.hlsl로 목록을 돌며 면별 확산·반사·번짐(바닥 반사광)·테두리빛을 계산한다. 실제 Light 개수는 그대로
    // - 상한: PC 14개, 모바일(모바일 파이프라인 에셋·핸드헬드)은 6개와 약식 계산 (Quality 0)
    public static class HlrLights
    {
        public const int Capacity = 16;
        public static bool ForceLow;   // 확인용: 모바일 약식 경로를 PC에서 시험

        static readonly int PosId = Shader.PropertyToID("_HlrLightPos");
        static readonly int ColId = Shader.PropertyToID("_HlrLightCol");
        static readonly int CountId = Shader.PropertyToID("_HlrLightCount");
        static readonly int QualityId = Shader.PropertyToID("_HlrQuality");
        static readonly int NightId = Shader.PropertyToID("_HlrNight");

        static readonly Vector4[] pos = new Vector4[Capacity];
        static readonly Vector4[] col = new Vector4[Capacity];
        static int count;

        public static bool Low
        {
            get
            {
                if (ForceLow || Application.isMobilePlatform) return true;
                RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
                return asset != null && asset.name.Contains("Mobile");
            }
        }

        public static int Max => Low ? 6 : 14;
        public static int Count => count;

        public static void Begin() => count = 0;

        // radius = 빛이 닿는 거리(m), color = 빛 색 × 세기, bounce = 바닥 반사광(번짐) 세기 0~1 (불바다·폭발처럼 큰 빛만)
        public static bool Add(Vector3 position, float radius, Color color, float intensity, float bounce)
        {
            if (count >= Max) return false;
            pos[count] = new Vector4(position.x, position.y, position.z, Mathf.Max(0.1f, radius));
            col[count] = new Vector4(color.r * intensity, color.g * intensity, color.b * intensity, bounce);
            count++;
            return true;
        }

        public static void Upload(bool night)
        {
            Shader.SetGlobalVectorArray(PosId, pos);
            Shader.SetGlobalVectorArray(ColId, col);
            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalFloat(QualityId, Low ? 0f : 1f);
            Shader.SetGlobalFloat(NightId, night ? 1f : 0f);
        }

        public static void Clear()
        {
            count = 0;
            Shader.SetGlobalFloat(CountId, 0f);
            Shader.SetGlobalFloat(NightId, 0f);
        }
    }
}
