using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HumanLastRampart.Combat.Render
{
    // 미니어처 흐림 (D-062 G1 '약하게'): 화면 위·아래 띠만 점점 흐리게 — 들판 가운데는 선명(적이 보여야 한다)
    // PC 렌더러에 붙어 있다. 값은 BattleLook이 날씨·지도 모드에 따라 정한다(Strength 0이면 아무것도 안 한다)
    public sealed class TiltShiftFeature : ScriptableRendererFeature
    {
        public static float Strength;                 // 0 = 끔, 1 = 기본 세기
        public static float TopStart = 0.72f;         // 화면 아래에서 이 높이(0~1)부터 위쪽이 흐려진다
        public static float BottomEnd = 0.06f;        // 화면 맨 아래 이 높이까지 흐려진다
        public static float MaxRadiusPixels = 3.5f;   // 가장 흐린 곳의 흐림 반경 (1080 화면 기준)
        public static float BottomScale = 1f;         // 아래쪽(가까운 쪽) 흐림 배율 — 위쪽(먼 들판)보다 약하게 (K-044)
        public static Color ShaftColor = Color.clear; // 빛줄기 색×세기 (K-044). 검정이면 계산 안 함
        public static float ShaftSlant = 0.45f;       // 빛줄기 기울기 (화면 아래로 갈수록 가로로 이만큼 밀림)
        // 값은 BattleLook이 날씨·밤낮에 맞춰 정한다. Strength 0이면 빛줄기도 같이 꺼진다(같은 패스)

        [SerializeField] Shader shader;
        Material material;
        TiltShiftPass pass;

        public override void Create()
        {
            pass = new TiltShiftPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 화면 효과를 끈 사진기(시험 도구의 글씨 전용 사진기 등)에는 걸지 않는다
            if (Strength <= 0f || renderingData.cameraData.cameraType != CameraType.Game || !renderingData.cameraData.postProcessEnabled) return;
            if (material == null)
            {
                if (shader == null) shader = Shader.Find("HLR/TiltShift");
                if (shader == null) return;
                material = CoreUtils.CreateEngineMaterial(shader);
            }

            pass.Material = material;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class TiltShiftPass : ScriptableRenderPass
        {
            static readonly int ParamsId = Shader.PropertyToID("_TiltShift");
            static readonly int ShaftsId = Shader.PropertyToID("_Shafts");
            static readonly int ShaftTimeId = Shader.PropertyToID("_ShaftTime");
            public Material Material;

            public TiltShiftPass()
            {
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (Material == null || resources.isActiveTargetBackBuffer) return;
                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_TiltShiftTemp";
                desc.clearBuffer = false;
                TextureHandle temp = renderGraph.CreateTexture(desc);
                float radius = MaxRadiusPixels * Mathf.Max(0.5f, desc.height / 1080f) * Strength;
                Material.SetVector(ParamsId, new Vector4(TopStart, BottomEnd, radius, BottomScale));
                Material.SetVector(ShaftsId, new Vector4(ShaftColor.r * Strength, ShaftColor.g * Strength, ShaftColor.b * Strength, ShaftSlant));
                Material.SetFloat(ShaftTimeId, Time.time);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, temp, Material, 0), "미니어처 흐림 가로");
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(temp, source, Material, 1), "미니어처 흐림 세로");
            }
        }
    }
}
