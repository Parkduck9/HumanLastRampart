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
        readonly List<(float score, Vector3 pos, float radius, Color color, float intensity, float bounce)> flashCandidates =
            new List<(float, Vector3, float, Color, float, float)>();
        readonly List<(float distance, Light light)> torchCandidates = new List<(float, Light)>();

        static int CompareFlash((float score, Vector3 pos, float radius, Color color, float intensity, float bounce) a,
                                (float score, Vector3 pos, float radius, Color color, float intensity, float bounce) b) => b.score.CompareTo(a.score);

        static int CompareTorch((float distance, Light light) a, (float distance, Light light) b) => a.distance.CompareTo(b.distance);

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
                float d = Vector3.Distance(f.transform.position, view);
                float score = f.intensity * f.range / (1f + d * 0.1f);
                flashCandidates.Add((score, f.transform.position, f.range, f.color, f.intensity, f.intensity >= BigLightIntensity ? 1f : 0.25f));
            }
            flashCandidates.Sort(CompareFlash);

            // 횃불 몫을 남겨 둔다 (벽 위 병사가 늘 빛을 받게)
            int torchReserve = torchesOn ? Mathf.Max(2, max / 3) : 0;
            int flashSlots = max - torchReserve;
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
            }

            HlrLights.Upload(torchNight);
        }
    }
}
