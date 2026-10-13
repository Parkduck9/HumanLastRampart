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

        public IReadOnlyList<WaveGroup> Groups => groups;
    }

    [Serializable]
    public sealed class WaveGroup
    {
        public WallSide Side;          // 어느 방향 들판에서 오는가
        public string EnemyDefId;
        public int Count;
        public float SpawnAtSeconds;   // 전투 시작 기준 등장 시각
    }
}