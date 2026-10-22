using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 전투 규칙 값 모음 (C-010 v1.2). 1개만 둔다. 모두 시작값이며 시험 플레이로 조정한다.
    [CreateAssetMenu(menuName = "HLR/정의/전투 규칙", fileName = "BattleRulesDef")]
    public sealed class BattleRulesDef : DefinitionBase
    {
        [Header("난이도 (D-031 K1)")]
        [SerializeField] float easyEnemyMultiplier = 0.5f;
        [Tooltip("v1.3: 쉬움에서 전투 번호별 배율 (0번 칸 = 1번 전투). 0이거나 칸이 없으면 위 기본값")]
        [SerializeField] float[] easyEnemyMultiplierByBattle = new float[0];
        [Tooltip("현실에서 전투 번호별 배율 (0번 칸 = 1번 전투). 0이거나 칸이 없으면 1")]
        [SerializeField] float[] realisticEnemyMultiplierByBattle = new float[0];
        [Tooltip("무리가 나오는 속도(초당 마리). 0이면 예전처럼 무리마다 12초 안에 모두 나온다")]
        [SerializeField] float spawnPerSecond = 0f;
        [SerializeField] int maxConcurrentEnemiesRealistic = 50000;
        [SerializeField] int maxConcurrentEnemiesEasy = 25000;
        [Tooltip("진 밤 다음 낮이 시작될 때, 이 비율(%)보다 체력이 낮은 성벽을 최대 체력의 이 비율까지 되돌린다 (W11, 사용자가 바꿀 수 있는 기본값). 0이면 끔")]
        [Range(0, 100)] [SerializeField] int lostNightWallFloorPercent = 30;

        [Header("날씨 (D-027 Q4, D-028 C5)")]
        [SerializeField] float weatherChangeChance = 0.15f;
        [SerializeField] float showerStopChance = 0.5f;
        [SerializeField] float revertChance = 0.15f;          // 소나기 외 변화가 원래로 돌아올 확률
        [SerializeField] float changeWindowStart = 30f;       // 변화가 일어날 수 있는 시각 범위(초)
        [SerializeField] float changeWindowEnd = 240f;
        [SerializeField] float revertDelayMin = 30f;
        [SerializeField] float revertDelayMax = 120f;

        [Header("날씨·밤낮 효과 (D-027 Q5, D-028 C4, D-029)")]
        [SerializeField] float rainAttackSpeedMultiplier = 0.8f;
        [SerializeField] float rainFireDamageMultiplier = 0.1f;
        [SerializeField] float showerFireDamageMultiplier = 0.5f;
        [SerializeField] float fogVisibleDistance = 25f;
        [SerializeField] float nightVisibleDistance = 30f;
        [SerializeField] float lowVisibilityFarAccuracyPenalty = 0.2f;

        [Header("명령 전달 (D-028, D-030)")]
        [SerializeField] float shoutNotHeardAdjacent = 0.10f;
        [SerializeField] float shoutNotHeardOpposite = 0.30f;
        [SerializeField] float shoutMisheardAdjacent = 0.15f;
        [SerializeField] float shoutMisheardOpposite = 0.30f;
        [SerializeField] float shoutBadConditionBonus = 0.10f;  // 비·밤일 때 더함
        [SerializeField] float messengerSpeedRatio = 0.7f;

        [Header("패배·등반 (D-026 B10)")]
        [SerializeField] float garrisonLossDefeatRatio = 0.7f;
        [SerializeField] float corpseHeightPerKill = 0.02f;     // 시체 더미 높이 증가(칸당, m)

        [Header("1회차 회수 (C-015·C-016)")]
        [SerializeField] float recoverySpotChance = 0.6f;       // 탐지 거리 안의 함정을 알아챌 확률
        [SerializeField] int recoveryExplosionLoss = 3;         // 회수 중 폭발 1회당 잃는 병사

        public float EasyEnemyMultiplier => easyEnemyMultiplier;

        public float SpawnPerSecond => spawnPerSecond;

        // 현실 n번 전투의 적 배율 (기본 1)
        public float RealisticEnemyMultiplierFor(int battleNumber)
        {
            int index = battleNumber - 1;
            bool has = realisticEnemyMultiplierByBattle != null && index >= 0 && index < realisticEnemyMultiplierByBattle.Length && realisticEnemyMultiplierByBattle[index] > 0f;
            return has ? realisticEnemyMultiplierByBattle[index] : 1f;
        }

        // 쉬움 n번 전투의 적 배율 (D-047: 4번만 0.4)
        public float EasyEnemyMultiplierFor(int battleNumber)
        {
            int index = battleNumber - 1;
            bool has = easyEnemyMultiplierByBattle != null && index >= 0 && index < easyEnemyMultiplierByBattle.Length && easyEnemyMultiplierByBattle[index] > 0f;
            return has ? easyEnemyMultiplierByBattle[index] : easyEnemyMultiplier;
        }
        public int MaxConcurrentEnemiesRealistic => maxConcurrentEnemiesRealistic;
        public int MaxConcurrentEnemiesEasy => maxConcurrentEnemiesEasy;
        public int LostNightWallFloorPercent => lostNightWallFloorPercent;
        public float WeatherChangeChance => weatherChangeChance;
        public float ShowerStopChance => showerStopChance;
        public float RevertChance => revertChance;
        public float ChangeWindowStart => changeWindowStart;
        public float ChangeWindowEnd => changeWindowEnd;
        public float RevertDelayMin => revertDelayMin;
        public float RevertDelayMax => revertDelayMax;
        public float RainAttackSpeedMultiplier => rainAttackSpeedMultiplier;
        public float RainFireDamageMultiplier => rainFireDamageMultiplier;
        public float ShowerFireDamageMultiplier => showerFireDamageMultiplier;
        public float FogVisibleDistance => fogVisibleDistance;
        public float NightVisibleDistance => nightVisibleDistance;
        public float LowVisibilityFarAccuracyPenalty => lowVisibilityFarAccuracyPenalty;
        public float ShoutNotHeardAdjacent => shoutNotHeardAdjacent;
        public float ShoutNotHeardOpposite => shoutNotHeardOpposite;
        public float ShoutMisheardAdjacent => shoutMisheardAdjacent;
        public float ShoutMisheardOpposite => shoutMisheardOpposite;
        public float ShoutBadConditionBonus => shoutBadConditionBonus;
        public float MessengerSpeedRatio => messengerSpeedRatio;
        public float GarrisonLossDefeatRatio => garrisonLossDefeatRatio;
        public float CorpseHeightPerKill => corpseHeightPerKill;
        public float RecoverySpotChance => recoverySpotChance;
        public int RecoveryExplosionLoss => recoveryExplosionLoss;
    }
}