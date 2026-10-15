using HumanLastRampart.Combat.PerfLab;
using Unity.Collections;
using Unity.Mathematics;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽 위를 달리는 병사(전령·우회 보고)의 달리기 그림 (미리 연결, 사용자 검토 전)
    // - 병종에 달리기 시트(UnitDef.RunSheet)가 있으면 달리는 동안 그 그림으로, 서 있는 병사는 원래 뒷모습
    // - 시트는 왼쪽을 보는 옆모습 4칸 → 둘레 좌표가 늘어나는 쪽(바깥을 볼 때 오른쪽)으로 달리면 좌우 뒤집기
    public sealed partial class BattleSim
    {
        const float RunFps = 8f;

        float[] sRunDir;                                // +1 오른쪽, -1 왼쪽, 0 서 있음
        byte[] sHasRunSheet;
        NativeArray<PerfLabInstance> runnerInstances;   // 달리는 병사만 크기가 있다 (나머지 0 = 안 그림)

        public NativeArray<PerfLabInstance> RunnerInstances => runnerInstances;

        // 확인용: 지금 달리기 그림으로 그리는 병사 수, 병사 i가 달리기 그림인지·뒤집혔는지·서 있는 그림이 숨었는지
        public int RunnerPictureCount()
        {
            int n = 0;
            for (int i = 0; i < soldierCount; i++) if (runnerInstances[i].TintSize.w > 0f) n++;
            return n;
        }

        public bool TryFirstRunner(out UnityEngine.Vector3 position)
        {
            for (int i = 0; i < soldierCount; i++)
            {
                if (runnerInstances[i].TintSize.w <= 0f) continue;
                position = new UnityEngine.Vector3(sPos[i].x, sPos[i].y, sPos[i].z);
                return true;
            }

            position = default;
            return false;
        }

        public bool IsRunnerPicture(int i, out bool flipped, out bool standingHidden)
        {
            flipped = runnerInstances[i].PosFrame.w < 0f;
            standingHidden = soldierInstances[i].TintSize.w <= 0f;
            return runnerInstances[i].TintSize.w > 0f;
        }

        void InitRunnerArt(Allocator p)
        {
            int n = math.max(1, soldierCount);
            sRunDir = new float[n];
            sHasRunSheet = new byte[n];
            runnerInstances = new NativeArray<PerfLabInstance>(n, p);
            foreach (var (unit, start, count) in soldierGroups)
            {
                if (unit == null || unit.RunSheet == null) continue;
                for (int i = start; i < start + count; i++) sHasRunSheet[i] = 1;
            }
        }

        void SetRunning(int soldier, float direction)
        {
            if (sRunDir != null && soldier >= 0 && soldier < sRunDir.Length) sRunDir[soldier] = direction;
        }

        // RefreshSoldierInstances에서 병사마다. 달리는 중이고 달리기 그림이 있으면 true (원래 그림은 숨긴다)
        bool WriteRunner(int i)
        {
            bool running = sAlive[i] != 0 && sAway[i] != 0 && sRunDir[i] != 0f && sHasRunSheet[i] != 0;
            if (!running)
            {
                runnerInstances[i] = new PerfLabInstance { PosFrame = new float4(sPos[i], 0f), TintSize = float4.zero };
                return false;
            }

            int frame = (int)math.floor(Elapsed * RunFps + i * 0.37f) % PerfLabArt.FramesPerAction;
            float w = sRunDir[i] > 0f ? -frame - 1 : frame;
            runnerInstances[i] = new PerfLabInstance
            {
                PosFrame = new float4(sPos[i], w),
                TintSize = new float4(new float3(1f, 1f, 1f) * Brightness, SoldierDrawSize),
            };
            return true;
        }
    }
}
