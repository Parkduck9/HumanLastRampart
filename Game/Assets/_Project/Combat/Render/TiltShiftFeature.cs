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

        [SerializeField] Shader shader;
        Material material;
        TiltShiftPass pass;

        public override void Create()
        {
            pass = new TiltShiftPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Strength <= 0f || renderingData.cameraData.cameraType != CameraType.Game) return;
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
                Material.SetVector(ParamsId, new Vector4(TopStart, BottomEnd, radius, 0f));
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, temp, Material, 0), "미니어처 흐림 가로");
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(temp, source, Material, 1), "미니어처 흐림 세로");
            }
        }
    }
}
