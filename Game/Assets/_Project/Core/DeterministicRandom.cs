namespace HumanLastRampart.Core
{
    // 같은 씨앗이면 어느 기기·실행에서도 같은 값을 내는 무작위 (SplitMix64).
    // 예보(BattleForecast)·회수(TrapRules)처럼 "다시 열어도 결과가 같아야 하는" 곳에 쓴다.
    // UnityEngine.Random·System.Random·string.GetHashCode는 실행 환경에 따라 달라질 수 있어 쓰지 않는다.
    public struct DeterministicRandom
    {
        ulong state;

        public DeterministicRandom(ulong seed)
        {
            state = seed;
        }

        public static DeterministicRandom From(int seed, int a, string salt)
        {
            ulong value = (ulong)(uint)seed;
            value = Mix(value ^ ((ulong)(uint)a << 32));
            value = Mix(value ^ StableHash(salt));
            return new DeterministicRandom(value);
        }

        public static DeterministicRandom From(int seed, string key, string salt)
        {
            ulong value = Mix((ulong)(uint)seed ^ StableHash(key));
            value = Mix(value ^ StableHash(salt));
            return new DeterministicRandom(value);
        }

        public ulong NextULong()
        {
            state += 0x9E3779B97F4A7C15UL;
            return Mix(state);
        }

        // [0, 1)
        public float NextFloat() => (NextULong() >> 40) * (1f / (1 << 24));

        // [0, count)
        public int NextInt(int count) => count <= 0 ? 0 : (int)(NextULong() % (ulong)count);

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public bool Chance(float probability) => NextFloat() < probability;

        static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // FNV-1a 64비트: 문자열을 실행 환경과 상관없이 같은 수로
        public static ulong StableHash(string text)
        {
            ulong hash = 14695981039346656037UL;
            if (text == null)
            {
                return hash;
            }

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}