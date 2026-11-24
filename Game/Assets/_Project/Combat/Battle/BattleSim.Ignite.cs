using Unity.Collections;
using Unity.Mathematics;

namespace HumanLastRampart.Combat.Battle
{
    // 대포 불붙이기 (K-046, 사용자 요청 2026-10-16): 쏠 때가 되면 대포병이 도화선에 불을 붙이고, 닿은 뒤 1초에 발사
    // - 쏠 대상·조준점은 불붙이기를 시작할 때 정해 고정한다(그 사이 지도에서 조준을 바꾸면 다음 발부터).
    //   자동 표적은 걸어오는 적의 발사 시점 위치를 미리 잰다(불붙이는 시간만큼 더 앞으로)
    // - 쏘는 대포병이 쓰러지거나(분대 마지막 한 명) 전령으로 빠지면 취소
    // - 재장전은 발사 시점부터 센다. 전체 발사 간격은 그대로 — 간격 안에 불붙이기 시간이 들어간다(간격 − 불붙이기 뒤에 다시 불붙이기)
    // - 시간 값과 그림 칸은 여기 한곳 (Codex K-043 시안 timeline: 0.5초 4번째 칸 도화선 접촉, 1.5초 발사)
    public sealed partial class BattleSim
    {
        public const float CannonIgniteLead = 0.5f;     // 불붙이는 막대를 뻗어 도화선에 닿기까지
        public const float CannonFuseSeconds = 1f;      // 닿은 뒤 발사까지 (사용자: '불을 붙이고 1초 뒤')
        public const float CannonIgniteTotal = CannonIgniteLead + CannonFuseSeconds;
        public const float IgniteFps = 6f;              // 불붙이기 시트 6칸: 들기 → 뻗기 → 접촉(0.5초) → 반짝임 → 거두기, 마지막 칸 유지

        NativeArray<float> sFuse;                       // 발사까지 남은 시간 (0 = 불붙이는 중 아님). 사격 작업이 쓴다
        NativeArray<Shot> sPendingShot;                 // 불붙이기 시작 때 정한 포탄
        byte[] sIgniteFrames;                           // 병종 불붙이기 시트 칸 수 (0 = 없음)

        void InitIgnite(Allocator p)
        {
            int n = math.max(1, soldierCount);
            sFuse = new NativeArray<float>(n, p);
            sPendingShot = new NativeArray<Shot>(n, p);
            sIgniteFrames = new byte[n];
        }

        // 대포 i가 불붙이는 중인가 (지도 표시). 작업이 도는 중엔 읽지 않는다
        bool IsIgniting(int soldier) => !scheduled && sFuse.IsCreated && sFuse[soldier] > 0f;

        // 불붙이기 그림 칸 (SoldierPose에서). 불붙이는 중이 아니면 false
        bool IgnitePose(int i, out float frame)
        {
            frame = 0f;
            if (sIgniteFrames == null || sIgniteFrames[i] == 0 || !sFuse.IsCreated || sFuse[i] <= 0f) return false;
            float age = CannonIgniteTotal - sFuse[i];
            frame = PerfLab.PerfLabArt.IgniteFrame0 + math.min(math.floor(math.max(age, 0f) * IgniteFps), sIgniteFrames[i] - 1);
            return true;
        }

        void ReleaseIgnite()
        {
            Release(ref sFuse);
            Release(ref sPendingShot);
        }
    }
}
