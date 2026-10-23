using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HumanLastRampart.Core.Audio
{
    // 소리 은행 (자료 주도): 열쇠 → 소리 파일들·크기·높낮이 범위·동시 소리 수·다시 울리기까지 시간·2D/3D·갈래.
    // Resources/Audio/SoundBank 에 두면 AudioService가 읽는다. 항목이 비었거나 파일이 없으면 조용히 건너뛴다(경고 없음).
    [CreateAssetMenu(menuName = "HLR/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        public const string ResourcePath = "Audio/SoundBank";

        [Serializable]
        public sealed class Entry
        {
            public string Id;                       // AudioCues 열쇠
            public AudioClip[] Clips = Array.Empty<AudioClip>();   // 여럿이면 번갈아·무작위로
            [Range(0f, 1f)] public float Volume = 1f;
            public Vector2 PitchRange = new Vector2(0.95f, 1.05f);
            [Min(1)] public int MaxVoices = 4;      // 이 소리가 동시에 울릴 수 있는 수
            [Min(0f)] public float Cooldown = 0.05f;   // 다시 울리기까지 최소 시간(초)
            public bool Spatial3D;                  // true면 카메라와의 거리로 작아진다
            public float MinDistance = 8f, MaxDistance = 80f;
            public AudioBus Bus = AudioBus.Sfx;
            public bool Loop;
        }

        // 갈래마다 AudioMixer 그룹 (비어도 된다 — 크기는 AudioService가 직접 곱한다)
        [Serializable]
        public sealed class BusRoute
        {
            public AudioBus Bus;
            public AudioMixerGroup Group;
        }

        public List<Entry> Entries = new List<Entry>();
        public List<BusRoute> Routes = new List<BusRoute>();

        public AudioMixerGroup GroupOf(AudioBus bus)
        {
            for (int i = 0; i < Routes.Count; i++)
                if (Routes[i] != null && Routes[i].Bus == bus) return Routes[i].Group;
            return null;
        }
    }
}
