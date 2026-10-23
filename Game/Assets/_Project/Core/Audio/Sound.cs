using UnityEngine;

namespace HumanLastRampart.Core.Audio
{
    // 소리 서비스가 지키는 약속 (실제 구현은 App/Audio/AudioService). 화면 코드는 아래 정적 입구 Sound만 부른다.
    public interface IAudioSink
    {
        void Play(string cue);
        void PlayAt(string cue, Vector3 position, float gain);
        void Report(string cue, int count, Vector3 position);
        void Music(string cue);
        void Ambience(string cue, float level);
        void StopAmbience();                 // 주변 소리 모두 서서히 끔
        void SetMapMode(bool on);            // 지도(2번 화면)에서는 거리 감쇠 없이 낸다
    }

    // 정적 입구. 서비스가 등록되기 전·끈 뒤에는 아무 일도 하지 않는다 (소리 은행이 비어도 마찬가지로 조용)
    public static class Sound
    {
        public static IAudioSink Sink;

        public static void Play(string cue) => Sink?.Play(cue);
        public static void PlayAt(string cue, Vector3 position, float gain = 1f) => Sink?.PlayAt(cue, position, gain);
        public static void Report(string cue, int count, Vector3 position) => Sink?.Report(cue, count, position);
        public static void Music(string cue) => Sink?.Music(cue);
        public static void Ambience(string cue, float level) => Sink?.Ambience(cue, level);
        public static void StopAmbience() => Sink?.StopAmbience();
        public static void SetMapMode(bool on) => Sink?.SetMapMode(on);
    }
}
