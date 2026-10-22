using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 전투 하나의 적 구성 — 방향별 무리 목록 (C-010 v1.2, D-026 B8). 적 수는 현실 기준 (D-031 K1).
    [CreateAssetMenu(menuName = "HLR/정의/웨이브", fileName = "WaveDef")]
    public sealed class WaveDef : DefinitionBase
    {
        [SerializeField] List<WaveGroup> groups = new List<WaveGroup>();

        // 선봉 마릿수 (기본 난이도 기준, 난이도 배율을 곱한다). 0이면 전투 번호로 20/30/45/60
        [SerializeField] int vanguardCount = 0;

        public IReadOnlyList<WaveGroup> Groups => groups;
        public int VanguardCount => vanguardCount;
    }

    // 전투 단계 (나안, Parallel_Wave 6장). Auto = 적 종류로 정한다 (사다리 고블린·오우거 → 공성, 나머지 → 본대)
    public enum WavePhase
    {
        Auto = 0,
        Vanguard = 1,
        Main = 2,
        Siege = 3,
        Final = 4,
    }

    [Serializable]
    public sealed class WaveGroup
    {
        public WallSide Side;          // 어느 방향 들판에서 오는가
        public string EnemyDefId;
        public int Count;
        public float SpawnAtSeconds;   // 전투 시작 기준 등장 시각
        public WavePhase Phase;        // 단계 (Auto = 적 종류로 정함)
        public float StreamPerSecond;  // 흘러나오는 속도 (초당 마릿수). 0이면 전투 규칙의 기본 속도
    }
}