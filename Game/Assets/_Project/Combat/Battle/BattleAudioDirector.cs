using HumanLastRampart.Core.Audio;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 소리 지휘 (W24, 표현 전용): 시뮬레이션 상태를 매 프레임 읽기만 하고 바꾸지 않는다 (결정성·난수와 무관).
    // 지난 프레임과 달라진 것만 AudioService에 알린다 — 같은 프레임 여럿은 AudioService가 한 소리로 묶는다.
    // 소리 은행이 비어 있으면 AudioService가 모두 조용히 버린다.
    // - 단계 바뀜 → 뿔나팔 / 죽은 수 늘어남 → 죽음 묶음 (일제 사격·착탄은 BattleEffectsView가 투사체 칸에서 센다)
    // - 성벽 체력 줄어듦 → 성벽 맞음, 0이 되면 무너짐 / 전령 출발 → 발소리 / 등불 밝기 바뀜 → 점화
    // - 낮·밤 전투 음악, 적 수에 비례한 함성, 바람·밤 귀뚜라미
    public sealed class BattleAudioDirector
    {
        const float WindLevel = 0.5f, CricketLevel = 0.6f;
        const float WallHitGap = 0.3f;          // 면마다 성벽 맞음 소리 최소 간격 (공성 중 매 프레임 울림 방지)
        const float RefreshSeconds = 0.25f;     // 함성·주변 소리 크기 갱신 간격

        readonly int[] wallHp = new int[4];
        WavePhase phase = (WavePhase)(-1);
        int kills;
        readonly float[] wallHitAt = new float[4];   // 면마다 마지막 성벽 맞음 소리 시각
        int couriersFree = int.MaxValue;
        LampBrightness lamp = (LampBrightness)(-1);
        bool beamTurning;
        bool started;
        float nextRefresh;

        // LateUpdate에서 sim.Complete 뒤 한 번. listener = 소리를 듣는 카메라 자리
        public void Tick(BattleSim sim, Vector3 listener, bool mapOpen)
        {
            if (sim == null) return;
            if (!started) Begin(sim);
            Sound.SetMapMode(mapOpen);   // 지도에서는 거리 감쇠 없이

            WavePhase now = sim.CurrentPhase;
            if (now != phase)
            {
                phase = now;
                Sound.PlayAt(AudioCues.WaveHorn, listener);
            }

            int killNow = sim.Kills;
            if (killNow > kills) Sound.Report(AudioCues.EnemyDeath, killNow - kills, listener);
            kills = killNow;

            for (int side = 0; side < 4; side++)
            {
                int hp = sim.Sides[side].WallHp;
                if (hp < wallHp[side])
                {
                    if (Time.unscaledTime - wallHitAt[side] >= WallHitGap) { wallHitAt[side] = Time.unscaledTime; Sound.Report(AudioCues.WallHit, 1, listener); }
                    if (hp <= 0 && wallHp[side] > 0) Sound.PlayAt(AudioCues.WallBreach, listener);
                }

                wallHp[side] = hp;
            }

            bool turning = !Mathf.Approximately(sim.BeamAngleDegrees, sim.BeamTargetDegrees);   // 일꾼들이 윈치로 빛줄기를 돌리는 중
            if (turning && !beamTurning) Sound.PlayAt(AudioCues.BeaconBeamTurn, listener);
            beamTurning = turning;

            int free = sim.TowerCouriersFree;
            if (free < couriersFree) Sound.Report(AudioCues.CourierRun, 1, listener);
            couriersFree = free;

            if (sim.Lamp != lamp)
            {
                if (lamp != (LampBrightness)(-1)) Sound.PlayAt(AudioCues.BeaconIgnite, listener);
                lamp = sim.Lamp;
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                RefreshAmbience(sim);
            }
        }

        void Begin(BattleSim sim)
        {
            started = true;
            for (int side = 0; side < 4; side++) wallHp[side] = sim.Sides[side].WallHp;
            kills = sim.Kills;
            couriersFree = sim.TowerCouriersFree;
            beamTurning = !Mathf.Approximately(sim.BeamAngleDegrees, sim.BeamTargetDegrees);
            lamp = sim.Lamp;
            bool night = sim.TimeOfDay == TimeOfDay.Night;
            Sound.Music(night ? AudioCues.MusicNight : AudioCues.MusicBattle);
            Sound.Ambience(AudioCues.AmbWind, WindLevel);
        }

        void RefreshAmbience(BattleSim sim)
        {
            int alive = Mathf.Max(0, sim.Spawned - sim.Kills);
            float crowd = AudioMath.CrowdLevel(alive);
            Sound.Ambience(AudioCues.AmbCrowd, crowd);
            if (sim.TimeOfDay == TimeOfDay.Night) Sound.Ambience(AudioCues.AmbCrickets, CricketLevel * (1f - crowd));
        }
    }
}
