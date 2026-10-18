using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HumanLastRampart.Combat.Render
{
    // 옥토패스식 화면 효과 (D-062, BattleHD2D.md 3장 — K-044에서 다듬음): 빛번짐·가장자리 어둡게·필름 톤·색 보정·미니어처 흐림·빛줄기
    // 전투마다 전역 볼륨 하나를 만들고 날씨·밤낮이 바뀔 때 값을 바꾼다. 지도 모드에서는 모두 끈다(지도는 읽혀야 한다). G 키로 켜고 끔(비교용)
    // 조정할 수치는 아래 상수에 모았다. 화면 확인 뒤 여기만 고치면 된다
    public sealed class BattleLook
    {
        public static bool Enabled = true;
        public static bool LitActive { get; private set; }   // 캐릭터가 빛을 받는 중 (2단계) — 이때는 밝기 숫자로 어둡게 하지 않는다
        static readonly int SpriteLitId = Shader.PropertyToID("_SpriteLit");

        // ── 미니어처 흐림 (TiltShiftFeature, PC 렌더러) ── 가운데(성벽·병사)는 선명, 위쪽 먼 들판·아래쪽 가까운 곳만 은은하게
        const float TiltTopStart = 0.68f;        // 화면 아래에서 이 높이(0~1)부터 위쪽이 흐려진다 (예전 0.72)
        const float TiltBottomEnd = 0.16f;       // 화면 맨 아래 이 높이까지 흐려진다 (예전 0.06)
        const float TiltBottomScale = 0.7f;      // 아래쪽(가까운 쪽)은 위쪽보다 약하게
        const float TiltRadius = 4f;             // 가장 흐린 곳 반경(1080 화면 화소) — '심하지 않게' (예전 3.5)
        const float TiltFogScale = 0.6f;         // 안개 날은 이미 흐리니 덜

        // ── 빛번짐 (Bloom) ── 불·폭발·횃불(스스로 빛나는 그림, 1 넘는 색)만 부드럽게 번진다
        const float BloomThreshold = 0.9f;
        const float BloomScatter = 0.72f;        // 클수록 넓고 부드럽게
        const float BloomDay = 0.7f, BloomNight = 1.15f, BloomFogExtra = 0.15f;
        static readonly Color BloomTint = new Color(1f, 0.93f, 0.82f);   // 따뜻한 번짐

        // ── 톤 매핑·노출 ── ACES(필름 느낌)는 중간 밝기를 조금 어둡게 하므로 노출을 올려 맞춘다
        const float ExposureDay = 0.3f, ExposureNight = -0.15f;

        // ── 색 보정 ── 낮·불빛은 따뜻하게, 밤은 푸르게. 그림자는 살짝 차갑게, 밝은 곳은 따뜻하게(필름처럼 색이 갈린다)
        const float TemperatureDay = 10f, TemperatureNight = -24f;
        const float TintNight = 6f;                                      // 밤은 아주 조금 자주 쪽 (달빛)
        static readonly Color SplitShadowsDay = new Color(0.44f, 0.48f, 0.56f);
        static readonly Color SplitHighlightsDay = new Color(0.6f, 0.54f, 0.44f);
        static readonly Color SplitShadowsNight = new Color(0.38f, 0.44f, 0.6f);
        static readonly Color SplitHighlightsNight = new Color(0.58f, 0.52f, 0.44f);   // 밤에도 횃불 빛은 따뜻하게
        const float SplitBalance = -15f;                                 // 음수 = 그림자 색이 조금 더 넓게

        // ── 가장자리 어둡게 (Vignette) ──
        const float VignetteDay = 0.26f, VignetteNight = 0.38f, VignetteRainExtra = 0.06f;
        const float VignetteSmoothness = 0.5f;

        // ── 빛줄기 (TiltShift 세로 패스에 더함) ── 맑은 낮에 위에서 비스듬히 내려오는 느낌. 세기는 아주 약하게
        static readonly Color ShaftsClear = new Color(1f, 0.86f, 0.62f) * 0.07f;
        static readonly Color ShaftsCloudy = new Color(0.9f, 0.9f, 0.85f) * 0.03f;
        static readonly Color ShaftsNight = new Color(0.55f, 0.65f, 1f) * 0.025f;   // 달빛
        const float ShaftSlant = 0.45f;

        readonly Camera camera;
        readonly GameObject host;
        readonly Volume volume;
        readonly VolumeProfile profile;
        readonly Bloom bloom;
        readonly Vignette vignette;
        readonly ColorAdjustments color;
        readonly WhiteBalance white;
        readonly SplitToning split;
        readonly Tonemapping tonemap;
        bool mapMode;
        float tiltScale = 1f;
        Color shafts = Color.clear;

        public BattleLook(Camera camera, Transform parent)
        {
            this.camera = camera;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
            camera.allowHDR = true;   // 1 넘는 불빛이 잘리지 않고 번지게 (파이프라인 HDR은 이미 켬)

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
            split = profile.Add<SplitToning>(true);
            tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);   // K-044: 필름 느낌 (예전 Neutral)
            bloom.highQualityFiltering.Override(false);    // 성능: 고품질 거르기는 끈다 (수천 개체 전투)
            bloom.tint.Override(BloomTint);
            vignette.smoothness.Override(VignetteSmoothness);
            vignette.rounded.Override(false);
            split.balance.Override(SplitBalance);
            ApplyTilt();
            Refresh();
        }

        // 날씨·밤낮이 바뀔 때 (BattleView.ApplyAtmosphere에서)
        public void Apply(Weather weather, bool night)
        {
            float contrast = 14f, saturation = 14f, temperature = TemperatureDay;
            float bloomIntensity = night ? BloomNight : BloomDay;
            float vignetteIntensity = night ? VignetteNight : VignetteDay;
            tiltScale = 1f;
            shafts = night ? ShaftsNight : ShaftsClear;
            switch (weather)
            {
                case Weather.Cloudy:
                    contrast = 8f; saturation = 0f; temperature = 2f;
                    shafts = night ? Color.clear : ShaftsCloudy;
                    break;
                case Weather.Rain:
                    contrast = 6f; saturation = -12f; temperature = -8f;
                    vignetteIntensity += VignetteRainExtra;
                    shafts = Color.clear;
                    break;
                case Weather.Fog:
                    contrast = -6f; saturation = -10f; temperature = -4f;
                    bloomIntensity += BloomFogExtra;
                    tiltScale = TiltFogScale;
                    shafts = Color.clear;
                    break;
            }

            if (night)
            {
                temperature = TemperatureNight + (temperature - TemperatureDay) * 0.5f;
                saturation -= 10f;
            }

            color.postExposure.Override(night ? ExposureNight : ExposureDay);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            white.temperature.Override(temperature);
            white.tint.Override(night ? TintNight : 0f);
            split.shadows.Override(night ? SplitShadowsNight : SplitShadowsDay);
            split.highlights.Override(night ? SplitHighlightsNight : SplitHighlightsDay);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(BloomThreshold);
            bloom.scatter.Override(BloomScatter);
            vignette.intensity.Override(vignetteIntensity);
            vignette.color.Override(night ? new Color(0.02f, 0.03f, 0.08f) : new Color(0.06f, 0.05f, 0.03f));
            ApplyTilt();
            Refresh();
        }

        // 미니어처 흐림·빛줄기 값을 렌더러 기능에 넘긴다 (켜고 끔은 Refresh의 Strength)
        void ApplyTilt()
        {
            TiltShiftFeature.TopStart = TiltTopStart;
            TiltShiftFeature.BottomEnd = TiltBottomEnd;
            TiltShiftFeature.BottomScale = TiltBottomScale;
            TiltShiftFeature.MaxRadiusPixels = TiltRadius * tiltScale;
            TiltShiftFeature.ShaftColor = shafts;
            TiltShiftFeature.ShaftSlant = ShaftSlant;
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
            // 설정 창(K-062): 화면 효과 끔이면 G 키 끔과 같다. 미니어처 흐림 세기는 흐림 반경에만 곱한다(빛줄기는 그대로)
            bool on = Enabled && GameSettings.ScreenEffects && !mapMode;
            volume.weight = on ? 1f : 0f;
            TiltShiftFeature.Strength = on ? 1f : 0f;   // 지도에서는 흐림·빛줄기 끔
            TiltShiftFeature.MaxRadiusPixels = TiltRadius * tiltScale * GameSettings.TiltStrength;
            // 2단계: 빛 받는 캐릭터·그림자. 지도에서도 꺼 둔다(지도는 점만)
            LitActive = Enabled;
            Shader.SetGlobalFloat(SpriteLitId, LitActive ? 1f : 0f);
        }

        public void Dispose()
        {
            TiltShiftFeature.Strength = 0f;
            TiltShiftFeature.ShaftColor = Color.clear;
            LitActive = false;
            Shader.SetGlobalFloat(SpriteLitId, 0f);
            if (host != null) Object.Destroy(host);
            if (profile != null) Object.Destroy(profile);
        }
    }
}
