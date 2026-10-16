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
            public int Reach;          // 칠 수 있는 들판 면 (비트 0~3 = 북·동·남·서, K-042)
        }

        readonly List<(int soldier, int side, int number, string postId)> cannons = new List<(int, int, int, string)>();
        NativeArray<float3> sAim;
        NativeArray<byte> sHasAim;
        NativeArray<byte> sIdle;
        NativeArray<byte> sCannonReach;   // 병사별 — 대포 쏘는 병사만 0이 아니다 (사격 작업이 읽음)

        public int CannonCount => cannons.Count;

        void InitCannons(Allocator p)
        {
            int n = math.max(1, soldierCount);
            sAim = new NativeArray<float3>(n, p);
            sHasAim = new NativeArray<byte>(n, p);
            sIdle = new NativeArray<byte>(n, p);
            sCannonReach = new NativeArray<byte>(n, p);

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

            InitCannonReach();
        }

        // 대포가 칠 수 있는 들판 (K-042 사용자 요청): 자기 성벽 앞 들판 + 모서리에 가까운 2문만 그 모서리 옆 성벽의 들판.
        // 맞은편 들판은 못 친다. 들판은 성 가운데에서 대각선으로 4면을 나눈다 (WallGeometry.FieldSide).
        // 성벽 따라 오른쪽(바깥을 볼 때) 순서로 줄 세워 왼쪽 끝 2문 = 왼쪽 옆 성벽, 오른쪽 끝 2문 = 오른쪽 옆 성벽 (북쪽이면 1·2번 서쪽, 3·4번 동쪽)
        void InitCannonReach()
        {
            for (int side = 0; side < 4; side++)
            {
                var row = new List<(float along, int soldier)>();
                foreach (var (soldier, s, _, _) in cannons)
                    if (s == side) row.Add((math.dot(CannonSpot(soldier), WallGeometry.Right(side)), soldier));
                row.Sort((a, b) => a.along.CompareTo(b.along));
                for (int i = 0; i < row.Count; i++)
                {
                    int reach = 1 << side;
                    if (i < 2) reach |= 1 << ((side + 3) % 4);               // 왼쪽 옆 성벽 (바깥을 볼 때 왼쪽)
                    if (i >= row.Count - 2) reach |= 1 << ((side + 1) % 4);  // 오른쪽 옆 성벽
                    sCannonReach[row[i].soldier] = (byte)reach;
                }
            }
        }

        // 이 대포가 그 지점을 칠 수 있는가: 사거리 안 + 칠 수 있는 들판 면. reason = 안 될 때 알림 문구
        public bool CannonCanReach(int index, Vector3 point, out string reason)
        {
            reason = null;
            if (index < 0 || index >= cannons.Count) { reason = "대포 없음"; return false; }
            var (soldier, side, number, _) = cannons[index];
            float3 from = CannonSpot(soldier);   // 운용병 2명이 양옆에 서므로 대포 자리 기준 (K-039)
            float range = weapons[sWeapon[soldier]].Range;
            float dx = point.x - from.x, dz = point.z - from.z;
            if (dx * dx + dz * dz > range * range)
            {
                reason = $"{SideNames[side]} 대포 {number}: 사거리({range:0}m) 밖입니다";
                return false;
            }

            int field = WallGeometry.FieldSide(new float3(point.x, 0f, point.z));
            if ((sCannonReach[soldier] & (1 << field)) == 0)
            {
                reason = $"{SideNames[side]} 대포 {number}: {SideNames[field]} 들판은 닿지 않습니다";
                return false;
            }

            return true;
        }

        public CannonInfo GetCannon(int index)
        {
            var (soldier, side, number, postId) = cannons[index];
            return new CannonInfo
            {
                Side = side,
                Number = number,
                PostId = postId,
                Position = CannonSpot(soldier),   // 운용병 2명이 양옆에 서므로 대포 자리는 따로 (K-039)
                Range = weapons[sWeapon[soldier]].Range,
                HasAim = sHasAim[soldier] != 0,
                Aim = sAim[soldier],
                Idle = !scheduled && sIdle[soldier] != 0,   // 작업이 도는 중엔 읽지 않는다 (그리기·HUD는 작업이 끝난 뒤 부름)
                Alive = sAlive[soldier] != 0,
                Reach = sCannonReach[soldier],
            };
        }

        // 지도에서 찍은 지점으로 조준. 사거리 밖이거나 칠 수 없는 들판이면 false (알림)
        public bool SetCannonAim(int index, Vector3 point)
        {
            if (index < 0 || index >= cannons.Count) return false;
            var (soldier, side, number, _) = cannons[index];
            if (!CannonCanReach(index, point, out string reason))
            {
                Notice?.Invoke(reason);
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

        // 어떤 점에서 range 안, 성 밖에서 적이 가장 많이 모인 격자 칸의 가운데 (없으면 null).
        // 작업이 끝난 뒤(그리기 단계·측정 도구)에서만 부른다 — 지난 프레임 격자를 읽는다
        public Vector3? DensestPointNear(Vector3 from, float range)
        {
            if (scheduled) return null;
            var c = new float3(from.x, 0f, from.z);
            int2 minCell = grid.CellOf(c - new float3(range, 0f, range));
            int2 maxCell = grid.CellOf(c + new float3(range, 0f, range));
            int best = 0;
            Vector3 bestPoint = default;
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int cell = grid.IndexOf(new int2(x, z));
                    int count = cellCount[cell];
                    if (count <= best) continue;
                    var center = new Vector3(grid.MinX + (x + 0.5f) * grid.CellSize, 0f, grid.MinZ + (z + 0.5f) * grid.CellSize);
                    if (math.max(math.abs(center.x), math.abs(center.z)) < CastleHalf + 2f) continue;   // 성 안·성벽 위 제외
                    float dx = center.x - from.x, dz = center.z - from.z;
                    if (dx * dx + dz * dz > range * range) continue;
                    best = count;
                    bestPoint = center;
                }
            }

            return best > 0 ? bestPoint : (Vector3?)null;
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
