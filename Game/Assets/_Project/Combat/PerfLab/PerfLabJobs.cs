using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HumanLastRampart.Combat.PerfLab
{
    // 셰이더 PerfLabInstance와 같은 배치 (32바이트). 바꾸면 셰이더도 같이 바꾼다.
    [StructLayout(LayoutKind.Sequential)]
    public struct PerfLabInstance
    {
        public float4 PosFrame;  // xyz 발 위치, w 프레임 번호
        public float4 TintSize;  // rgb 색, a 크기

        public const int Stride = 32;
    }

    // 적 행군: 성벽(z = wallZ) 쪽으로 직진하고, 닿으면 멈춰 공격 상태가 된다.
    // 순서 2에서는 공유 흐름장 대신 직진만 한다 (흐름장은 순서 4 비교 항목).
    [BurstCompile]
    public struct EnemyMoveJob : IJobParallelFor
    {
        public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> Speeds;
        public NativeArray<byte> AtWall;
        public float DeltaTime;
        public float StopZ;

        public void Execute(int index)
        {
            if (AtWall[index] != 0)
            {
                return;
            }

            float3 position = Positions[index];
            position.z -= Speeds[index] * DeltaTime;
            if (position.z <= StopZ)
            {
                position.z = StopZ;
                AtWall[index] = 1;
            }

            Positions[index] = position;
        }
    }

    // 그리기 데이터 작성: 위치와 걷기 프레임을 셰이더 버퍼 형식으로 옮긴다.
    [BurstCompile]
    public struct WriteEnemyInstancesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> AnimPhases;
        [ReadOnly] public NativeArray<byte> AtWall;
        [WriteOnly] public NativeArray<PerfLabInstance> Instances;
        public float Time;
        public float AnimFps;
        public int FrameCount;
        public float4 TintSize;

        public void Execute(int index)
        {
            // 성벽에 닿은 적은 공격 자세(마지막 프레임)로 고정한다.
            float frame = AtWall[index] != 0
                ? FrameCount - 1
                : math.floor(math.frac(Time * AnimFps / FrameCount + AnimPhases[index]) * FrameCount);

            Instances[index] = new PerfLabInstance
            {
                PosFrame = new float4(Positions[index], frame),
                TintSize = TintSize,
            };
        }
    }
}
