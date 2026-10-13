using System;
using System.Collections.Generic;

namespace HumanLastRampart.Core.Campaign
{
    // 일반 자동 공격의 표적 고르기 (C-011 v1.1, D-028·D-029 R1 명령어 6개)
    public enum TargetPriority
    {
        LowestHpRatio,    // 체력 낮은 적부터 (기본)
        HighestHpRatio,   // 체력 높은 적부터
        Nearest,          // 가까운 적부터
        ClimbingFirst,    // 오르는 적부터 (사다리·시체 더미)
        SpecialFirst,     // 특수 적 먼저 (없으면 체력 낮은 적)
        DensestArea,      // 밀집한 곳 쏘기
    }

    // 전체 명령(4면 지도) + 성벽 명령. 성벽 명령이 있으면 그 성벽은 그것을 따른다.
    // 전체 명령을 새로 내리면 성벽 명령은 모두 지운다 (D-031 K3).
    [Serializable]
    public sealed class TargetingSettings
    {
        public TargetPriority GlobalPriority = TargetPriority.LowestHpRatio;
        public List<WallTargetRule> WallRules = new List<WallTargetRule>();

        public static TargetingSettings CreateDefault() => new TargetingSettings();

        public TargetPriority PriorityFor(string wallId)
        {
            WallTargetRule rule = WallRules.Find(r => r.WallId == wallId);
            return rule != null ? rule.Priority : GlobalPriority;
        }

        public void SetGlobal(TargetPriority priority)
        {
            GlobalPriority = priority;
            WallRules.Clear();
        }

        public void SetWall(string wallId, TargetPriority priority)
        {
            WallTargetRule rule = WallRules.Find(r => r.WallId == wallId);
            if (rule == null)
            {
                WallRules.Add(new WallTargetRule { WallId = wallId, Priority = priority });
            }
            else
            {
                rule.Priority = priority;
            }
        }

        public TargetingSettings Clone()
        {
            var copy = new TargetingSettings { GlobalPriority = GlobalPriority };
            foreach (WallTargetRule rule in WallRules)
            {
                copy.WallRules.Add(new WallTargetRule { WallId = rule.WallId, Priority = rule.Priority });
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class WallTargetRule
    {
        public string WallId;
        public TargetPriority Priority;
    }
}