using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 1회차 시작 설정 (C-010, D-015 D3 — Claude 작성). 카탈로그 전체에서 정확히 1개.
    [CreateAssetMenu(menuName = "HLR/정의/시작 설정", fileName = "CampaignStartDef")]
    public sealed class CampaignStartDef : DefinitionBase
    {
        [SerializeField] int startGold = 100;
        [SerializeField] string firstStoryId;
        [SerializeField] List<WallStart> walls = new List<WallStart>();          // 정확히 4개
        [SerializeField] List<HeroStart> heroes = new List<HeroStart>();        // 주인공·마법사 등
        [SerializeField] List<SquadStart> squads = new List<SquadStart>();      // 첫 배치 지점 포함 (D-015 D5)
        [SerializeField] List<string> waveByBattle = new List<string>();        // 전투 번호(1부터) → WaveDef ID
        [SerializeField] List<string> terrainOrder = new List<string>();        // 전투 번호 순서로 반복 (P1-C14)

        public int StartGold => startGold;
        public string FirstStoryId => firstStoryId;
        public IReadOnlyList<WallStart> Walls => walls;
        public IReadOnlyList<HeroStart> Heroes => heroes;
        public IReadOnlyList<SquadStart> Squads => squads;
        public IReadOnlyList<string> WaveByBattle => waveByBattle;
        public IReadOnlyList<string> TerrainOrder => terrainOrder;

        public int BattleCount => waveByBattle.Count;   // 1회차 = 4 (D-018)

        // 전투 번호(1부터) → 웨이브. 범위 밖이면 null (마지막 전투 뒤 = 메인 복귀, D-022 I5)
        public string WaveFor(int battleNumber) =>
            battleNumber >= 1 && battleNumber <= waveByBattle.Count ? waveByBattle[battleNumber - 1] : null;

        // 밤 번호(이긴 밤 수 + 1) → 웨이브·지형. BeaconRules.BattleSlot으로 기존 전투 1~4번 자리에 대응 (W8)
        public string WaveForNight(int night) => night < 1 ? null : WaveFor(BeaconRules.BattleSlot(night, waveByBattle.Count));
        public string TerrainForNight(int night) => night < 1 ? null : TerrainFor(BeaconRules.BattleSlot(night, waveByBattle.Count));

        public string TerrainFor(int battleNumber) =>
            terrainOrder.Count == 0 || battleNumber < 1 ? null : terrainOrder[(battleNumber - 1) % terrainOrder.Count];
    }

    [Serializable]
    public sealed class WallStart
    {
        public WallSide Side;
        public int MaxHp = 1000;
    }

    [Serializable]
    public sealed class HeroStart
    {
        public string UnitDefId;
        public string PostId;      // null이면 미배치
    }

    [Serializable]
    public sealed class SquadStart
    {
        public string UnitDefId;
        public int Count;
        public string PostId;
    }
}