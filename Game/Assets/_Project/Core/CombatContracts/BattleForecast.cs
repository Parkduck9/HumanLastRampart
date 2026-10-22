using HumanLastRampart.Core.Definitions;

namespace HumanLastRampart.Core.CombatContracts
{
    // 날씨 예보 (D-031 K4). 상태가 없는 계산: 같은 (판 시드, 전투 번호, 규칙)이면 언제 불러도 같은 결과.
    // App은 BattleSetup을 채울 때, 스토리(Codex)는 전투 전에 힌트 대사를 고를 때 같은 함수를 부른다.
    public static class BattleForecast
    {
        public static WeatherForecast Get(int rngSeed, int battleNumber, BattleRulesDef rules)
        {
            var random = DeterministicRandom.From(rngSeed, battleNumber, "forecast");
            var forecast = new WeatherForecast
            {
                InitialWeather = (Weather)random.NextInt(4),   // 맑음·흐림·비·안개 무작위 (D-027 Q4)
                TimeOfDay = TimeOfDay.Night,                   // 전투는 언제나 밤 (등대 설계 B6)
            };
            random.NextInt(2);   // 예전 낮밤 뽑기 난수는 소비해 아래 날씨 변화 난수 순서를 그대로 둔다 (결정성)

            float changeChance = rules != null ? rules.WeatherChangeChance : 0.15f;
            if (!random.Chance(changeChance))
            {
                return forecast;
            }

            forecast.Changes = true;
            forecast.ChangeAtSeconds = random.Range(
                rules != null ? rules.ChangeWindowStart : 30f,
                rules != null ? rules.ChangeWindowEnd : 240f);

            // 바뀌는 방향: 비 → 그침(흐림), 안개 → 걷힘(맑음), 맑음·흐림 → 비(소나기) 또는 안개 (D-027 Q4)
            switch (forecast.InitialWeather)
            {
                case Weather.Rain:
                    forecast.ChangedWeather = Weather.Cloudy;
                    break;
                case Weather.Fog:
                    forecast.ChangedWeather = Weather.Clear;
                    break;
                default:
                    forecast.ChangedWeather = random.Chance(0.5f) ? Weather.Rain : Weather.Fog;
                    break;
            }

            forecast.IsShower = forecast.ChangedWeather == Weather.Rain;

            // 되돌아오는 것은 원래 날씨로만. 소나기는 50% 확률로 그친다 (D-028 C5)
            float revertChance = forecast.IsShower
                ? (rules != null ? rules.ShowerStopChance : 0.5f)
                : (rules != null ? rules.RevertChance : 0.15f);
            if (random.Chance(revertChance))
            {
                forecast.Reverts = true;
                forecast.RevertAtSeconds = forecast.ChangeAtSeconds + random.Range(
                    rules != null ? rules.RevertDelayMin : 30f,
                    rules != null ? rules.RevertDelayMax : 120f);
            }

            return forecast;
        }
    }
}