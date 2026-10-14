using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 대포 조준 (MAP-3, Battle.md 5.2, D-044 M2·M6)
    // - 성벽마다 대포 4문(대포 지점의 운용병 분대 하나 = 1문, 맨 앞 병사가 쏜다)
    // - 지도에서 찍은 지점으로 바꿀 때까지 계속 포격. 사거리(무기 정의) 밖은 찍을 수 없다
    // - 조준점 근처에 적이 없으면 쉰다("대기"). 조준점이 없으면 그 벽 앞 밀집한 곳 (C16-3)
    // - 조준은 전투마다 처음으로 (M6) — 저장하지 않는다
    public sealed partial class BattleSim
    {
        public struct CannonInfo
        {
            public int Side;
            public int Number;         // 그 성벽에서 1~4
            public string PostId;
            public Vector3 Position;
            public float Range;
            public bool HasAim;
            public Vector3 Aim;
            public bool Idle;          // 조준점 근처에 적이 없어 쉬는 중
            public bool Alive;         // 운용병이 남아 있는가
        }

        readonly List<(int soldier, int side, int number, string postId)> cannons = new List<(int, int, int, string)>();
        NativeArray<float3> sAim;
        NativeArray<byte> sHasAim;
        NativeArray<byte> sIdle;

        public int CannonCount => cannons.Count;

        void InitCannons(Allocator p)
        {
            int n = math.max(1, soldierCount);
            sAim = new NativeArray<float3>(n, p);
            sHasAim = new NativeArray<byte>(n, p);
            sIdle = new NativeArray<byte>(n, p);

            // 쏘는 대포 병사(무기 있음·범위 무기)를 성벽별로 지점 이름 순서대로 번호를 매긴다
            var found = new List<(int soldier, int side, string postId)>();
            foreach (SquadInfo squad in squads)
            {
                int shooter = squad.FirstSoldier;
                if (squad.State.Count <= 0 || sWeapon[shooter] < 0 || weapons[sWeapon[shooter]].AreaRadius <= 0f) continue;
                found.Add((shooter, squad.Side, squad.State.PostId ?? ""));
            }

            found.Sort((a, b) => a.side != b.side ? a.side.CompareTo(b.side) : string.CompareOrdinal(a.postId, b.postId));
            var numberBySide = new int[4];
            foreach (var (soldier, side, postId) in found)
            {
                cannons.Add((soldier, side, ++numberBySide[side], postId));
            }
        }

        public CannonInfo GetCannon(int index)
        {
            var (soldier, side, number, postId) = cannons[index];
            return new CannonInfo
            {
                Side = side,
                Number = number,
                PostId = postId,
                Position = sPos[soldier],
                Range = weapons[sWeapon[soldier]].Range,
                HasAim = sHasAim[soldier] != 0,
                Aim = sAim[soldier],
                Idle = !scheduled && sIdle[soldier] != 0,   // 작업이 도는 중엔 읽지 않는다 (그리기·HUD는 작업이 끝난 뒤 부름)
                Alive = sAlive[soldier] != 0,
            };
        }

        // 지도에서 찍은 지점으로 조준. 사거리 밖이면 false (알림)
        public bool SetCannonAim(int index, Vector3 point)
        {
            if (index < 0 || index >= cannons.Count) return false;
            var (soldier, side, number, _) = cannons[index];
            float3 from = sPos[soldier];
            float range = weapons[sWeapon[soldier]].Range;
            float dx = point.x - from.x, dz = point.z - from.z;
            if (dx * dx + dz * dz > range * range)
            {
                Notice?.Invoke($"{SideNames[side]} 대포 {number}: 사거리({range:0}m) 밖입니다");
                return false;
            }

            var aim = new float3(point.x, 0f, point.z);
            RunOrQueue(() =>
            {
                sAim[soldier] = aim;
                sHasAim[soldier] = 1;
                sIdle[soldier] = 0;
            });
            Notice?.Invoke($"{SideNames[side]} 대포 {number}: 조준점 지정");
            return true;
        }

        public void ClearCannonAim(int index)
        {
            if (index < 0 || index >= cannons.Count) return;
            var (soldier, side, number, _) = cannons[index];
            RunOrQueue(() =>
            {
                sHasAim[soldier] = 0;
                sIdle[soldier] = 0;
            });
            Notice?.Invoke($"{SideNames[side]} 대포 {number}: 조준 해제 (밀집한 곳 자동)");
        }
    }
}
