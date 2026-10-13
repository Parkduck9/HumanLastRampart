using System;

namespace HumanLastRampart.Core.CombatContracts
{
    // 날씨 4종과 밤낮 (D-026 B8, D-027 Q4·Q5)
    public enum Weather
    {
        Clear,
        Cloudy,
        Rain,
        Fog,
    }

    public enum TimeOfDay
    {
        Day,
        Night,
    }

    // 한 전투의 날씨 예보. 전투 시작 전에 BattleForecast로 미리 정한다 (D-031 K4).
    // 변화는 최대 한 번, 되돌아오는 것은 원래 날씨로만 (D-027 Q4).
    [Serializable]
    public struct WeatherForecast
    {
        public Weather InitialWeather;
        public TimeOfDay TimeOfDay;

        public bool Changes;             // 전투 중 날씨가 한 번 바뀌는가 (15%, D-028 C5)
        public float ChangeAtSeconds;    // 바뀌는 시각 (전투 시작 기준 초)
        public Weather ChangedWeather;   // 바뀐 날씨
        public bool IsShower;            // 전투 중 비로 바뀐 경우 = 소나기 (불 피해 감소 50%)

        public bool Reverts;             // 원래 날씨로 돌아오는가 (소나기는 50%)
        public float RevertAtSeconds;

        // 주어진 시각의 날씨
        public Weather WeatherAt(float seconds)
        {
            if (!Changes || seconds < ChangeAtSeconds)
            {
                return InitialWeather;
            }

            if (Reverts && seconds >= RevertAtSeconds)
            {
                return InitialWeather;
            }

            return ChangedWeather;
        }
    }
}