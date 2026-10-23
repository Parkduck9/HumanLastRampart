using System;
using System.Collections.Generic;
using HumanLastRampart.Combat.Render;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 셰이더 점광원 목록 채우기 (W15, 사용자 2026-10-22 옥토패스식 빛): 실제 Light 개수는 그대로 두고,
    // 켜진 번쩍·불바다·폭발 빛과 가까운 횃불 중 눈에 띄는 것만 골라 HlrLights(PC 14·모바일 6개 상한)에 넣는다.
    // 들판 땅과 그림 셰이더가 이 목록으로 면별 확산·반사·바닥 번짐·테두리빛을 계산하고, 불은 깜박임이 그대로 번진다
    public sealed partial class BattleLights
    {
        const float TorchShaderRange = 1.6f;   // 셰이더가 보는 횃불 닿는 거리 = Light 범위 × 이 값 (작은 따뜻한 섬)
        const float BigLightIntensity = 5f;    // 이 이상은 큰 불·폭발 — 바닥 번짐을 준다
        const float BigLightPull = 0.5f;       // 큰 불 빛 위치를 사진기 쪽으로 반경 × 이 값 당긴다 (불 그림 앞 땅에 웅덩이가 보이게)
        const float BigLightPullMax = 8f;
        const float BigLightHeight = 0.4f;
        const float LowPoolScale = 0.55f;   // 저사양: 마당 웅덩이 반지름을 줄여 화면을 덮는 덧칠 면적을 아낀다

        // 마당 빛 내보내기 (W21): 땅 웅덩이(탑 둘레·문 앞, 호박빛 = 횃불 웅덩이 색) + PC만 문등 셰이더 빛 한 칸.
        // 문등 칸은 횃불·번쩍 몫을 나누기 전에 넣는다 (SplitSlots가 HlrLights.Count를 탑 몫으로 센다). 시험이 이 함수로 PC·모바일을 확인한다
        public static void EmitCourtyard(bool low, float level, bool lanternOn, Vector3 lanternPos, float lanternIntensity)
        {
            float s = low ? LowPoolScale : 1f;
            GroundGlow.Add(new Vector3(0f, BattleSim.WallHeight, 0f), CourtyardPoolRadius * s, GroundGlow.Torch, 0.3f * level);
            GroundGlow.Add(new Vector3(0f, BattleSim.WallHeight, BattleTowerView.DoorZ - DoorLanternOut - 0.2f), DoorPoolRadius, GroundGlow.Torch, 0.6f * level);
            if (lanternOn && DoorLanternUsesSlot(low))
                HlrLights.Add(lanternPos, DoorLanternRange * TorchShaderRange, DoorLanternColor, lanternIntensity, 0.15f);
        }

        readonly List<(float score, Vector3 pos, float radius, Color color, float intensity, float bounce)> flashCandidates =
            new List<(float, Vector3, float, Color, float, float)>();
        readonly List<(float distance, Light light)> torchCandidates = new List<(float, Light)>();

        // 비교 대리자는 한 번만 만든다 (메서드 그룹을 Sort에 넘기면 프레임마다 새로 만든다)
        static readonly Comparison<(float score, Vector3 pos, float radius, Color color, float intensity, float bounce)> CompareFlash =
            (a, b) => b.score.CompareTo(a.score);
        static readonly Comparison<(float distance, Light light)> CompareTorch = (a, b) => a.distance.CompareTo(b.distance);

        // 빛 목록 칸 나누기: 탑(이미 넣은 칸) -> 횃불 몫(max/3, 최소 2) -> 나머지는 번쩍·불바다·폭발. 시험도 이 함수를 쓴다
        public static void SplitSlots(int max, int usedByTower, bool torchesOn, out int torchReserve, out int flashSlots)
        {
            torchReserve = torchesOn ? Mathf.Max(2, max / 3) : 0;
            flashSlots = Mathf.Max(0, max - torchReserve - usedByTower);
        }

        void GatherShaderLights()
        {
            HlrLights.Begin();
            int max = HlrLights.Max;
            Vector3 view = ViewPosition;

            flashCandidates.Clear();
            for (int i = 0; i < flashes.Count; i++)
            {
                Light f = flashes[i];
                if (!f.enabled || f.intensity < 0.05f) continue;
                Vector3 pos = f.transform.position;
                float d = Vector3.Distance(pos, view);
                float score = f.intensity * f.range / (1f + d * 0.1f);
                bool big = f.intensity >= BigLightIntensity;
                if (big)
                {
                    // 불 그림(세로 판)이 불 바로 밑 땅을 가리므로, 셰이더용 빛은 사진기 쪽으로 당기고 낮춰 불 앞 땅에 주황 웅덩이가 생기게 한다
                    Vector3 toView = view - pos;
                    toView.y = 0f;
                    if (toView.sqrMagnitude > 1e-4f)
                        pos += toView.normalized * Mathf.Min(f.range * BigLightPull, BigLightPullMax);
                    pos.y = BigLightHeight;
                }

                flashCandidates.Add((score, pos, f.range, f.color, f.intensity, big ? 1f : 0.25f));
                if (big)   // 폭발·큰 불: 땅 웅덩이 (그림 쪽 빛과 따로)
                    GroundGlow.Add(new Vector3(f.transform.position.x, 0f, f.transform.position.z), Mathf.Min(f.range * 0.35f, 7f), f.color, Mathf.Min(f.intensity * 0.03f, 0.8f));
            }

            flashCandidates.Sort(CompareFlash);

            // 등대 탑 불빛 (W9): URP Light라 병사·들판에는 닿지 않으므로 셰이더 목록에 맨 먼저 넣는다 (한 칸 고정)
            if (towerLight != null && towerLight.enabled && max > 0)
                HlrLights.Add(towerLight.transform.position, towerLight.range * 1.3f, towerLight.color, towerLight.intensity, 0f);

            // 마당 빛 (W21): 탑 둘레·문 앞 따뜻한 웅덩이(모바일 포함), 문등은 PC에서만 칸 하나 (밑동 병사·일꾼이 보이게)
            if (towerLight != null && towerLight.enabled)
                EmitCourtyard(HlrLights.Low, courtyardLevel, doorLantern != null && doorLantern.enabled,
                    doorLantern != null ? doorLantern.transform.position : Vector3.zero, doorLantern != null ? doorLantern.intensity : 0f);

            // 횃불 몫을 남겨 둔다 (벽 위 병사가 늘 빛을 받게)
            SplitSlots(max, HlrLights.Count, torchesOn, out _, out int flashSlots);
            for (int i = 0; i < flashCandidates.Count && i < flashSlots; i++)
            {
                var c = flashCandidates[i];
                HlrLights.Add(c.pos, c.radius, c.color, c.intensity, c.bounce);
            }

            if (!torchesOn) { HlrLights.Upload(torchNight); return; }
            torchCandidates.Clear();
            for (int i = 0; i < torches.Count; i++)
            {
                Light t = torches[i];
                if (!t.enabled) continue;
                torchCandidates.Add(((t.transform.position - view).sqrMagnitude, t));
            }

            torchCandidates.Sort(CompareTorch);
            for (int i = 0; i < torchCandidates.Count; i++)
            {
                Light t = torchCandidates[i].light;
                if (!HlrLights.Add(t.transform.position, t.range * TorchShaderRange, t.color, t.intensity, 0.15f)) break;
                if (i < 6) GroundGlow.Add(new Vector3(t.transform.position.x, BattleSim.WallHeight, t.transform.position.z), t.range * 1.1f, GroundGlow.Torch, 0.18f);   // 가까운 횃불 몇 개: 통로 위 작은 웅덩이
            }

            HlrLights.Upload(torchNight);
        }
    }
}
