using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Definitions;
using Unity.Collections;
using Unity.Mathematics;

namespace HumanLastRampart.Combat.Battle
{
    // 동작 그림 상태 (K-032 미리 연결, 사용자 검토 전) — 그리기용만, 전투 계산에는 쓰지 않는다
    // - 병사 사격: 쏜 순간이 사격 시트 4번째 칸(놓기). 다음 발이 가까우면 앞 3칸(겨누기)을 미리 보여 준다.
    //   그 사이(계속 쏘는 중)는 마지막 칸(재장전)을 유지 (K-034)
    // - 병사·적 쓰러짐: FallSeconds 동안 쓰러짐 칸, 그 뒤 사라짐. 적은 개체마다 시작 시각(작업 안에서 잰다)
    // - 사다리 적: 오르는 동안 사다리 소품 (ladderInstances, 사다리 무리 구간만 올린다)
    // 칸 번호는 PerfLabArt.*Frame0 구간 — BattleView가 같은 버퍼를 시트마다 한 번씩 그린다
    public sealed partial class BattleSim
    {
        public const float FallSeconds = 0.7f;   // 쓰러짐 4칸 0.5초 + 마지막 칸 잠깐
        public const float FallFps = 8f;
        const float FireFps = 8f;                 // Codex 제안 속도. 공격 간격이 짧으면 그만큼 빨리
        const int FireReleaseFrame = 3;           // 사격 시트 6칸 중 놓기(쏘기) 칸
        const float DeployHeight = 0.5f;          // 사다리 오르기 첫 0.5m는 세우기 칸

        NativeArray<float> sFireTime;             // 병사가 마지막으로 쏜 시각 (SoldierFireJob이 쓴다)
        NativeArray<float> sFireLateral;          // 마지막으로 쏜 쪽 (+ 오른쪽). 사격 그림은 왼쪽을 겨눈 그림 → 오른쪽이면 뒤집는다
        const float FireFlipLateral = 0.5f;       // 거의 정면이면 뒤집지 않는다(m)
        const float GunMuzzleSide = 0.12f;        // 총병 사격 그림의 총구가 몸 가운데에서 옆으로 나온 거리(m, 병사 키 0.35m 기준)
        float[] sFallStart;                       // 병사가 쓰러진 시각 (0 = 아님)
        byte[] sFireFrames;                       // 병종 사격 시트 칸 수 (0 = 없음)
        byte[] sFireFacesRight;                   // 1 = 사격 그림이 오른쪽을 겨눔 (PerfLabArt.FireFacesRight)
        byte[] sFallFrames;
        NativeArray<float> eFallStart;            // 적이 쓰러지기 시작한 시각 (작업이 쓴다)
        NativeArray<PerfLabInstance> ladderInstances;
        readonly List<UnitDef> weaponUnits = new List<UnitDef>();   // 무기 번호 → 처음 그 무기를 든 병종 (투사체 그림)
        float wizardCastAt = -100f;

        public NativeArray<PerfLabInstance> LadderInstances => ladderInstances;
        public IReadOnlyList<UnitDef> WeaponUnits => weaponUnits;
        public bool WeaponIsArea(int weapon) => weapon >= 0 && weapon < weapons.Length && weapons[weapon].AreaRadius > 0f;
        // 병사 사격·쓰러짐 그림이 하나라도 있으면 병사 그림을 매 프레임 다시 쓴다 (BattleView)
        public bool HasSoldierPoseArt { get; private set; }
        // 마지막 마법이 나간 뒤 지난 시간 (마법사 시전 그림)
        public float WizardCastAge => Elapsed - wizardCastAt;

        void InitPoseArt(Allocator p)
        {
            int n = math.max(1, soldierCount);
            sFireTime = new NativeArray<float>(n, p);
            for (int i = 0; i < n; i++) sFireTime[i] = -100f;
            sFireLateral = new NativeArray<float>(n, p);
            sFallStart = new float[n];
            sFireFrames = new byte[n];
            sFireFacesRight = new byte[n];
            sFallFrames = new byte[n];
            InitIgnite(p);   // 대포 불붙이기 (K-046, BattleSim.Ignite.cs)
            foreach (var (unit, start, count) in soldierGroups)
            {
                if (unit == null || unit.Sheet == null) continue;   // 임시 그림 병종은 동작 그림도 쓰지 않는다 (BattleView와 같게)
                byte fire = PoseFrames(ProportionTrial.IsArcher(unit) ? ProportionTrial.ArcherFire(unit.FireSheet) : unit.FireSheet);   // 3등신 시험 그림은 4칸 (K-057)
                byte fall = (byte)(unit.DeathSheet != null ? math.min(PerfLabArt.FrameCount(unit.DeathSheet), PerfLabArt.PoseWindow) : 0);
                byte ignite = PoseFrames(unit.IgniteSheet);   // 대포병 불붙이기 (K-046)
                if (fire > 0 || fall > 0 || ignite > 0) HasSoldierPoseArt = true;
                byte faceRight = (byte)(PerfLabArt.FireFacesRight(unit.FireSheet) ? 1 : 0);
                for (int i = start; i < start + count; i++)
                {
                    sFireFacesRight[i] = faceRight;
                    sFireFrames[i] = fire;
                    sFallFrames[i] = fall;
                    sIgniteFrames[i] = ignite;
                }
            }

            int e = math.max(1, enemyCount);
            eFallStart = new NativeArray<float>(e, p);
            ladderInstances = new NativeArray<PerfLabInstance>(e, p);
        }

        // 따로 붙인 적 동작 시트 칸 수 (MakeKind에서)
        static byte PoseFrames(UnityEngine.Texture2D sheet) =>
            (byte)(sheet != null ? math.min(PerfLabArt.FrameCount(sheet), PerfLabArt.PoseWindow) : 0);

        void MarkSoldierFallen(int soldier)
        {
            if (sFallStart != null && soldier >= 0 && soldier < sFallStart.Length) sFallStart[soldier] = math.max(Elapsed, 1e-4f);
        }

        void MarkWizardCast() => wizardCastAt = Elapsed;

        // 3등신 시험 그림 바꿔 끼우기 (K-057): 그 병종 병사들의 사격 시트 칸 수만 바꾼다 (BattleView가 켜고 끌 때)
        public void SetFireSheetFrames(UnitDef unit, UnityEngine.Texture2D fireSheet)
        {
            byte frames = PoseFrames(fireSheet);
            foreach (var (u, start, count) in soldierGroups)
            {
                if (u != unit || u == null || u.Sheet == null) continue;
                for (int i = start; i < start + count; i++) sFireFrames[i] = frames;
                if (frames > 0) HasSoldierPoseArt = true;
            }
        }

        // RefreshSoldierInstances에서 병사마다: 그릴 칸 번호와 보이는지. 쓰러짐 그림이 끝났거나 없으면 false
        bool SoldierPose(int i, out float frame)
        {
            frame = 0f;
            if (sAlive[i] == 0)
            {
                if (sFallFrames[i] == 0 || sFallStart[i] <= 0f) return false;
                float age = Elapsed - sFallStart[i];
                if (age >= FallSeconds) return false;
                frame = PerfLabArt.FallFrame0 + math.min(math.floor(age * FallFps), sFallFrames[i] - 1);
                return true;
            }

            if (IgnitePose(i, out frame)) return true;   // 대포병 불붙이기 (K-046)
            int frames = sFireFrames[i];
            int w = sWeapon[i];
            if (frames == 0 || w < 0) return true;
            float interval = weapons[w].Interval * IntervalMultiplier
                * (CommanderOnWallSide(sSide[i]) ? Boost.HeroWallIntervalMul : 1f);
            float fps = math.max(FireFps, frames / math.max(interval, 0.05f));
            float since = Elapsed - sFireTime[i];
            float lead = FireReleaseFrame / fps;
            if (since >= 0f && since < (frames - FireReleaseFrame) / fps)
            {
                frame = PerfLabArt.FireFrame0 + math.min(FireReleaseFrame + (int)(since * fps), frames - 1);
            }
            else if (since < interval * 1.5f && sCooldown[i] > 0f && sCooldown[i] < lead && !(sIdle.IsCreated && sIdle[i] != 0))
            {
                // 계속 쏘는 중이고 다음 발이 가깝다: 겨누기 칸 (0 → 놓기 바로 앞)
                frame = PerfLabArt.FireFrame0 + math.clamp(FireReleaseFrame - 1 - (int)(sCooldown[i] * fps), 0, FireReleaseFrame - 1);
            }
            else if (since >= 0f && since < interval && sCooldown[i] >= lead)
            {
                // 재장전 (K-034 사용자 의견 5): 쏜 뒤 다음 겨누기 전까지 마지막 칸(재장전)을 유지한다.
                // 총병은 간격 2.5초라 예전에는 쏜 순간 앞뒤 0.75초만 동작이고 나머지는 서 있기 그림 → 모션이 없어 보였다
                frame = PerfLabArt.FireFrame0 + frames - 1;
            }

            // 사격 그림이 겨누는 쪽과 실제로 쏜 쪽(바깥을 볼 때 좌우)이 반대면 좌우 뒤집기 (사용자 지적 2026-10-20). 음수 칸 = 뒤집기
            if (frame >= PerfLabArt.FireFrame0 && sFireLateral.IsCreated)
            {
                float lateral = sFireLateral[i];
                bool artRight = sFireFacesRight != null && sFireFacesRight[i] != 0;
                if (artRight ? lateral < -FireFlipLateral : lateral > FireFlipLateral) frame = -frame - 1;
            }
            return true;
        }

        // 방금(window초 안에) 쏜 총병 자리 — near에서 가까운 max명 (총구 빛, BattleLights). 작업이 끝난 뒤에만 부른다
        public void RecentGunshots(List<UnityEngine.Vector3> into, float window, UnityEngine.Vector3 near, int max)
        {
            if (!sFireTime.IsCreated || max <= 0) return;
            var found = new List<(float d, UnityEngine.Vector3 p)>();
            foreach (var (unit, start, count) in soldierGroups)
            {
                if (unit == null || unit.Kind != UnitKind.Gunner) continue;
                for (int i = start; i < start + count; i++)
                {
                    if (sAlive[i] == 0 || Elapsed - sFireTime[i] > window) continue;
                    // 총구 자리: 투사체 출발점(Muzzle)과 같게 + 그림 속 총구가 쏜 쪽 옆으로 나와 있는 만큼
                    int side = sSide[i];
                    float3 m = sPos[i] + WallGeometry.Outward(side) * MuzzleForwardDistance + new float3(0f, SoldierDrawSize * MuzzleHeightRatio, 0f)
                        + WallGeometry.Right(side) * (sFireLateral[i] >= 0f ? GunMuzzleSide : -GunMuzzleSide);
                    var p = new UnityEngine.Vector3(m.x, m.y, m.z);
                    found.Add(((p - near).sqrMagnitude, p));
                }
            }

            found.Sort((a, b) => a.d.CompareTo(b.d));
            for (int k = 0; k < found.Count && k < max; k++) into.Add(found[k].p);
        }

        void ReleasePoseArt()
        {
            Release(ref sFireTime);
            Release(ref sFireLateral);
            Release(ref eFallStart);
            Release(ref ladderInstances);
            ReleaseIgnite();
        }
    }
}
