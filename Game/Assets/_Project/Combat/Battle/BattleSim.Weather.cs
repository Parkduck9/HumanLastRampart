using HumanLastRampart.Core.CombatContracts;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 날씨·밤낮 효과 (CY2-C4, Battle.md 6장, D-027·D-028·D-029)
    // - 날씨는 전투 준비 때 정한 예보(BattleForecast)대로 전투 중 바뀐다 (스토리 힌트와 같은 값, D-031 K4)
    // - 비: 아군 공격 속도 −20% / 불 피해 감소(불 무기는 마법 이후) / 소리 지르기 실패↑
    // - 안개·밤: 시야(안개 25m·밤 30m, 겹치면 더 짧은 쪽) 너머 적은 흐리게, 먼 거리 명중 −20%(곱하기)
    // - 흐림: 방향별 적 규모를 알 수 없다 (HUD에서 숨김)
    public sealed partial class BattleSim
    {
        const float ClearVisibleDistance = 10000f;

        public Weather CurrentWeather { get; private set; }
        public TimeOfDay TimeOfDay => setup.Forecast.TimeOfDay;
        public bool WeatherDirty { get; set; } = true;   // 화면(빛·안개) 다시 맞출 때

        public bool IsRaining => CurrentWeather == Weather.Rain;
        public bool LowVisibility => CurrentWeather == Weather.Fog || TimeOfDay == TimeOfDay.Night;
        public bool HidesDirections => CurrentWeather == Weather.Cloudy;

        // 지금 시야(성벽에서 m)
        public float VisibleDistance
        {
            get
            {
                float visible = ClearVisibleDistance;
                if (CurrentWeather == Weather.Fog) visible = Mathf.Min(visible, rules != null ? rules.FogVisibleDistance : 25f);
                if (TimeOfDay == TimeOfDay.Night) visible = Mathf.Min(visible, rules != null ? rules.NightVisibleDistance : 30f);
                return visible;
            }
        }

        // 공격 간격 배율: 공격 속도 0.8배 → 간격 1.25배
        float IntervalMultiplier => IsRaining ? 1f / Mathf.Max(0.05f, rules != null ? rules.RainAttackSpeedMultiplier : 0.8f) : 1f;

        float FarAccuracyMultiplier => LowVisibility ? 1f - (rules != null ? rules.LowVisibilityFarAccuracyPenalty : 0.2f) : 1f;

        // 불 피해 배율 — 마법(불바다)이 들어오면 쓴다. 전투 중 바뀐 비 = 소나기
        public float FireDamageMultiplier
        {
            get
            {
                if (!IsRaining) return 1f;
                bool shower = setup.Forecast.Changes && setup.Forecast.IsShower && setup.Forecast.InitialWeather != Weather.Rain;
                return shower ? (rules != null ? rules.ShowerFireDamageMultiplier : 0.5f) : (rules != null ? rules.RainFireDamageMultiplier : 0.1f);
            }
        }

        // 그림 밝기 (빛을 받지 않는 그림이라 직접 어둡게). 빛 받는 그림(D-062 2단계)이면 해·달 빛이 대신하므로 1
        public float Brightness
        {
            get
            {
                if (HumanLastRampart.Combat.Render.BattleLook.LitActive) return 1f;
                float b = TimeOfDay == TimeOfDay.Night ? 0.5f : 1f;
                if (IsRaining) b *= 0.8f;
                else if (CurrentWeather == Weather.Fog) b *= 0.85f;
                else if (CurrentWeather == Weather.Cloudy) b *= 0.92f;
                return b;
            }
        }

        static readonly string[] WeatherNames = { "맑음", "흐림", "비", "안개" };
        public static string WeatherName(Weather w) => WeatherNames[(int)w];

        void InitWeather()
        {
            CurrentWeather = setup.Forecast.InitialWeather;
        }

        void UpdateWeather()
        {
            Weather now = setup.Forecast.WeatherAt(Elapsed);
            if (now == CurrentWeather) return;
            Weather before = CurrentWeather;
            CurrentWeather = now;
            WeatherDirty = true;
            SoldiersDirty = true;
            Notice?.Invoke(DescribeChange(before, now));
        }

        string DescribeChange(Weather before, Weather now)
        {
            if (now == Weather.Rain) return setup.Forecast.InitialWeather != Weather.Rain ? "소나기가 쏟아집니다 — 공격 속도가 느려집니다" : "다시 비가 옵니다";
            if (before == Weather.Rain) return "비가 그쳤습니다";
            if (now == Weather.Fog) return "안개가 끼었습니다 — 먼 곳이 보이지 않습니다";
            if (before == Weather.Fog) return "안개가 걷혔습니다";
            return $"날씨가 바뀌었습니다: {WeatherName(before)} → {WeatherName(now)}";
        }
    }
}
