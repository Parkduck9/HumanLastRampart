using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 마지막 관측 자리만 기억한다. 잔상은 512칸을 돌려 쓰고, 관측 중인 적은 적 번호로 찾는다.
    public sealed class LensAfterimage
    {
        public const int Capacity = 512;
        public const float Lifetime = 4f;
        public struct Dot
        {
            public int Enemy;
            public Vector3 Position;
            public bool Special;
            public float Since;
        }

        readonly Dot[] dots = new Dot[Capacity];
        readonly int[] slotOf, seenFrame;
        readonly bool[] inside, special;
        readonly Vector3[] last;
        int frame, next;

        public LensAfterimage(int enemies)
        {
            slotOf = new int[enemies];
            seenFrame = new int[enemies];
            inside = new bool[enemies];
            special = new bool[enemies];
            last = new Vector3[enemies];
            for (int i = 0; i < enemies; i++) slotOf[i] = -1;
            for (int i = 0; i < Capacity; i++) dots[i].Enemy = -1;
        }

        public void Begin() { frame++; }

        public void Observe(int enemy, Vector3 position, bool isSpecial)
        {
            seenFrame[enemy] = frame;
            inside[enemy] = true;
            last[enemy] = position;
            special[enemy] = isSpecial;
            int slot = slotOf[enemy];
            if (slot >= 0) dots[slot].Enemy = -1;
            slotOf[enemy] = -1;
        }

        public void End(float now)
        {
            for (int e = 0; e < inside.Length; e++)
            {
                if (!inside[e] || seenFrame[e] == frame) continue;
                inside[e] = false;
                int replaced = dots[next].Enemy;
                if (replaced >= 0) slotOf[replaced] = -1;
                dots[next] = new Dot { Enemy = e, Position = last[e], Special = special[e], Since = now };
                slotOf[e] = next;
                next = (next + 1) % Capacity;
            }
        }

        public bool TryGet(int slot, float now, out Dot dot, out float alpha)
        {
            dot = dots[slot];
            alpha = Mathf.Clamp01(1f - (now - dot.Since) / Lifetime);
            return dot.Enemy >= 0 && alpha > 0f;
        }
    }
}
