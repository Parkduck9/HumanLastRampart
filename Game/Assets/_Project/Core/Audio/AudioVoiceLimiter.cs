using UnityEngine;

namespace HumanLastRampart.Core.Audio
{
    // 소리 수 제한 (순수 계산 — 시각은 밖에서 받는다. 시뮬레이션·난수와 무관한 표현 전용)
    // - 소리마다 다시 울리기까지(cooldown), 소리마다 동시 수(cueMax), 전체 동시 수(globalMax)
    // - 울린 소리는 끝나는 시각만 기억한다 (고정 배열, 매 프레임 할당 없음)
    public sealed class AudioVoiceLimiter
    {
        readonly float[] lastStart;      // 소리마다 마지막으로 울린 시각
        readonly int[] voiceCue;         // 울리는 중인 소리 목록 (소리 번호)
        readonly float[] voiceEnd;       // 끝나는 시각
        readonly int globalMax;
        int used;                        // 위 배열의 쓰는 칸 수

        public int GlobalMax => globalMax;

        public AudioVoiceLimiter(int cueCount, int globalMax)
        {
            this.globalMax = Mathf.Max(1, globalMax);
            lastStart = new float[Mathf.Max(1, cueCount)];
            for (int i = 0; i < lastStart.Length; i++) lastStart[i] = float.NegativeInfinity;
            voiceCue = new int[this.globalMax];
            voiceEnd = new float[this.globalMax];
        }

        // 지금 울려도 되면 true 하고 기록한다. 안 되면 false (기록 없음)
        public bool TryBegin(int cue, float now, float cooldown, int cueMax, float duration)
        {
            if (cue < 0 || cue >= lastStart.Length) return false;
            Purge(now);
            if (now - lastStart[cue] < cooldown) return false;
            if (used >= globalMax) return false;
            int same = 0;
            for (int i = 0; i < used; i++) if (voiceCue[i] == cue) same++;
            if (same >= Mathf.Max(1, cueMax)) return false;

            lastStart[cue] = now;
            voiceCue[used] = cue;
            voiceEnd[used] = now + Mathf.Max(0.01f, duration);
            used++;
            return true;
        }

        public int ActiveCount(float now)
        {
            Purge(now);
            return used;
        }

        public int ActiveCount(int cue, float now)
        {
            Purge(now);
            int n = 0;
            for (int i = 0; i < used; i++) if (voiceCue[i] == cue) n++;
            return n;
        }

        public void Clear()
        {
            used = 0;
            for (int i = 0; i < lastStart.Length; i++) lastStart[i] = float.NegativeInfinity;
        }

        void Purge(float now)
        {
            for (int i = used - 1; i >= 0; i--)
            {
                if (voiceEnd[i] > now) continue;
                used--;
                voiceCue[i] = voiceCue[used];
                voiceEnd[i] = voiceEnd[used];
            }
        }
    }

    // 한 프레임에 쌓인 같은 소리(화살 맞음 수백 개 등)를 하나로 묶는다: 개수와 중심 자리
    public sealed class AudioAggregator
    {
        readonly int[] counts;
        readonly Vector3[] sums;

        public AudioAggregator(int cueCount)
        {
            counts = new int[Mathf.Max(1, cueCount)];
            sums = new Vector3[Mathf.Max(1, cueCount)];
        }

        public void Add(int cue, int count, Vector3 position)
        {
            if (cue < 0 || cue >= counts.Length || count <= 0) return;
            counts[cue] += count;
            sums[cue] += position * count;
        }

        // 쌓인 것이 있으면 개수·중심을 주고 비운다
        public bool Take(int cue, out int count, out Vector3 center)
        {
            count = 0;
            center = Vector3.zero;
            if (cue < 0 || cue >= counts.Length || counts[cue] <= 0) return false;
            count = counts[cue];
            center = sums[cue] / count;
            counts[cue] = 0;
            sums[cue] = Vector3.zero;
            return true;
        }

        public int Pending(int cue) => cue >= 0 && cue < counts.Length ? counts[cue] : 0;
        public int CueCount => counts.Length;
    }

    public static class AudioMath
    {
        // 묶음 개수 → 크기 배수. 1개 = 1, 두 배마다 +0.2, 최대 1.8 (많다고 한없이 커지지 않게)
        public static float VolleyGain(int count)
        {
            if (count <= 1) return 1f;
            return Mathf.Min(1.8f, 1f + Mathf.Log(count, 2f) * 0.2f);
        }

        // 적 수 → 함성 크기 0~1 (수천이면 거의 최대)
        public static float CrowdLevel(int enemies)
        {
            if (enemies <= 0) return 0f;
            return Mathf.Clamp01(Mathf.Log10(enemies + 1f) / 3.7f);
        }

        // 거리 감쇠 (선형): min 안은 1, max 밖은 0
        public static float Attenuation(float distance, float min, float max)
        {
            if (distance <= min) return 1f;
            if (distance >= max || max <= min) return 0f;
            return 1f - (distance - min) / (max - min);
        }
    }
}
