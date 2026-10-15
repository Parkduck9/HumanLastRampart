using HumanLastRampart.Core.CombatContracts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HumanLastRampart.Combat.Render
{
    // 옥토패스식 화면 효과 1단계 (D-062, BattleHD2D.md 3장): 빛번짐·가장자리 어둡게·색 보정·미니어처 흐림
    // 전투마다 전역 볼륨 하나를 만들고 날씨·밤낮이 바뀔 때 값을 바꾼다. 지도 모드에서는 끈다. G 키로 켜고 끔(비교용)
    public sealed class BattleLook
    {
        public static bool Enabled = true;
        public static bool LitActive { get; private set; }   // 캐릭터가 빛을 받는 중 (2단계) — 이때는 밝기 숫자로 어둡게 하지 않는다
        static readonly int SpriteLitId = Shader.PropertyToID("_SpriteLit");

        readonly Camera camera;
        readonly GameObject host;
        readonly Volume volume;
        readonly VolumeProfile profile;
        readonly Bloom bloom;
        readonly Vignette vignette;
        readonly ColorAdjustments color;
        readonly WhiteBalance white;
        readonly Tonemapping tonemap;
        bool mapMode;

        public BattleLook(Camera camera, Transform parent)
        {
            this.camera = camera;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            host = new GameObject("Battle Look (화면 효과)");
            host.transform.SetParent(parent, false);
            volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Battle Look (runtime)";
            volume.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            vignette = profile.Add<Vignette>(true);
            color = profile.Add<ColorAdjustments>(true);
            white = profile.Add<WhiteBalance>(true);
            tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.Neutral);   // 도트 색을 크게 바꾸지 않는 쪽
            bloom.highQualityFiltering.Override(true);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);
            Refresh();
        }

        // 날씨·밤낮이 바뀔 때 (BattleView.ApplyAtmosphere에서)
        public void Apply(Weather weather, bool night)
        {
            float exposure = 0.15f, contrast = 14f, saturation = 12f, temperature = 8f, bloomIntensity = 0.55f, vignetteIntensity = 0.28f;
            switch (weather)
            {
                case Weather.Cloudy: contrast = 8f; saturation = -2f; temperature = 0f; break;
                case Weather.Rain: contrast = 6f; saturation = -14f; temperature = -10f; vignetteIntensity = 0.34f; break;
                case Weather.Fog: contrast = -6f; saturation = -10f; temperature = -4f; bloomIntensity = 0.7f; break;
            }

            if (night)
            {
                exposure -= 0.35f;
                temperature -= 22f;
                saturation -= 12f;
                vignetteIntensity += 0.12f;
                bloomIntensity += 0.35f;   // 횃불·폭발 빛이 번지게 (4단계에서 빛을 넣으면 살아난다)
            }

            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            white.temperature.Override(temperature);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            vignette.intensity.Override(vignetteIntensity);
            vignette.color.Override(night ? new Color(0.02f, 0.03f, 0.08f) : new Color(0.06f, 0.05f, 0.03f));
            Refresh();
        }

        public void SetMapMode(bool map)
        {
            if (map == mapMode) return;
            mapMode = map;
            Refresh();
        }

        // 켜고 끔이 바뀌었을 수 있으니 매 프레임 불러도 된다
        public void Refresh()
        {
            bool on = Enabled && !mapMode;
            volume.weight = on ? 1f : 0f;
            TiltShiftFeature.Strength = on ? 1f : 0f;
            // 2단계: 빛 받는 캐릭터·그림자. 지도에서도 꺼 둔다(지도는 점만)
            LitActive = Enabled;
            Shader.SetGlobalFloat(SpriteLitId, LitActive ? 1f : 0f);
        }

        public void Dispose()
        {
            TiltShiftFeature.Strength = 0f;
            LitActive = false;
            Shader.SetGlobalFloat(SpriteLitId, 0f);
            if (host != null) Object.Destroy(host);
            if (profile != null) Object.Destroy(profile);
        }
    }
}
