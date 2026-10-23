using HumanLastRampart.Core.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HumanLastRampart.Core.Tests
{
    // 소리 (W24): 동시 수 제한, 쿨다운, 묶음, 은행이 비었을 때의 침묵
    public sealed class AudioTests
    {
        [Test]
        public void Limiter_CooldownBlocksRapidRepeat()
        {
            var limiter = new AudioVoiceLimiter(2, 8);
            Assert.IsTrue(limiter.TryBegin(0, 0f, 0.1f, 4, 1f));
            Assert.IsFalse(limiter.TryBegin(0, 0.05f, 0.1f, 4, 1f), "쿨다운 안");
            Assert.IsTrue(limiter.TryBegin(0, 0.11f, 0.1f, 4, 1f), "쿨다운 뒤");
            Assert.IsTrue(limiter.TryBegin(1, 0.05f, 0.1f, 4, 1f), "다른 소리는 따로");
        }

        [Test]
        public void Limiter_PerCueAndGlobalVoiceLimits()
        {
            var limiter = new AudioVoiceLimiter(3, 5);
            for (int i = 0; i < 2; i++) Assert.IsTrue(limiter.TryBegin(0, i, 0f, 2, 10f));
            Assert.IsFalse(limiter.TryBegin(0, 2f, 0f, 2, 10f), "소리마다 동시 수 2");
            for (int i = 0; i < 3; i++) Assert.IsTrue(limiter.TryBegin(1, i, 0f, 9, 10f));
            Assert.IsFalse(limiter.TryBegin(2, 3f, 0f, 9, 10f), "전체 동시 수 5");
            Assert.AreEqual(5, limiter.ActiveCount(3f));
            Assert.IsTrue(limiter.TryBegin(2, 11.5f, 0f, 9, 10f), "끝난 소리는 자리를 비운다");
            Assert.AreEqual(2, limiter.ActiveCount(11.5f), "남은 1개 + 방금 시작한 1개");
        }

        [Test]
        public void Limiter_ThousandsOfRequestsStayWithinCap()
        {
            var limiter = new AudioVoiceLimiter(1, 24);
            int started = 0;
            for (int i = 0; i < 24000; i++) if (limiter.TryBegin(0, i * 0.0001f, 0.02f, 6, 0.5f)) started++;
            Assert.LessOrEqual(started, 6 * 5 + 6, "2.4초 동안 쿨다운 0.02·동시 6개 안쪽");
            Assert.LessOrEqual(limiter.ActiveCount(2.4f), 6);
        }

        [Test]
        public void Aggregator_MergesCountAndCenter()
        {
            var agg = new AudioAggregator(2);
            agg.Add(1, 3, new Vector3(0f, 0f, 0f));
            agg.Add(1, 1, new Vector3(4f, 0f, 0f));
            Assert.AreEqual(0, agg.Pending(0));
            Assert.IsTrue(agg.Take(1, out int count, out Vector3 center));
            Assert.AreEqual(4, count);
            Assert.AreEqual(1f, center.x, 0.001f);
            Assert.IsFalse(agg.Take(1, out _, out _), "가져가면 비워진다");
            Assert.Greater(AudioMath.VolleyGain(500), AudioMath.VolleyGain(2));
            Assert.LessOrEqual(AudioMath.VolleyGain(100000), 1.8f);
            Assert.AreEqual(1f, AudioMath.VolleyGain(1));
        }

        [Test]
        public void Math_CrowdAndAttenuation()
        {
            Assert.AreEqual(0f, AudioMath.CrowdLevel(0));
            Assert.Greater(AudioMath.CrowdLevel(5000), AudioMath.CrowdLevel(50));
            Assert.LessOrEqual(AudioMath.CrowdLevel(24000), 1f);
            Assert.AreEqual(1f, AudioMath.Attenuation(3f, 8f, 80f));
            Assert.AreEqual(0f, AudioMath.Attenuation(100f, 8f, 80f));
            Assert.AreEqual(0.5f, AudioMath.Attenuation(44f, 8f, 80f), 0.001f);
        }

        [Test]
        public void CueList_UniqueIds()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (AudioCues.Info info in AudioCues.All) Assert.IsTrue(seen.Add(info.Id), "중복 열쇠 " + info.Id);
            Assert.Greater(seen.Count, 20);
        }

        AudioService MakeService(SoundBank bank, int voices, out GameObject go)
        {
            go = new GameObject("AudioServiceTest");
            var service = go.AddComponent<AudioService>();
            service.Init(bank, voices);
            return service;
        }

        [Test]
        public void Service_EmptyBankIsSilentAndErrorFree()
        {
            AudioService service = MakeService(null, 4, out GameObject go);
            try
            {
                Assert.IsFalse(service.PlayAt(AudioCues.CannonFire, Vector3.zero, 1f, true));
                service.ReportCue(AudioCues.ArrowHit, 500, Vector3.zero);
                service.SetMusic(AudioCues.MusicDay);
                service.SetAmbience(AudioCues.AmbCrowd, 1f);
                service.Tick(0.1f);
                Assert.AreEqual(0, service.StartedCount);
                Assert.IsFalse(service.HasAnySound);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Service_EntryWithoutClipsIsSilent()
        {
            var bank = ScriptableObject.CreateInstance<SoundBank>();
            bank.Entries.Add(new SoundBank.Entry { Id = AudioCues.CannonFire, Clips = new AudioClip[] { null } });
            AudioService service = MakeService(bank, 4, out GameObject go);
            try
            {
                Assert.IsFalse(service.PlayAt(AudioCues.CannonFire, Vector3.zero, 1f, true));
                Assert.AreEqual(0, service.StartedCount);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(bank); }
        }

        [Test]
        public void Service_VoiceLimitCooldownAndAggregation()
        {
            AudioClip clip = AudioClip.Create("t", 44100, 1, 44100, false);
            var bank = ScriptableObject.CreateInstance<SoundBank>();
            bank.Entries.Add(new SoundBank.Entry { Id = AudioCues.CannonFire, Clips = new[] { clip }, MaxVoices = 2, Cooldown = 0f });
            bank.Entries.Add(new SoundBank.Entry { Id = AudioCues.ArrowHit, Clips = new[] { clip }, MaxVoices = 4, Cooldown = 0.5f });
            AudioService service = MakeService(bank, 8, out GameObject go);
            try
            {
                for (int i = 0; i < 5; i++) service.PlayAt(AudioCues.CannonFire, Vector3.zero, 1f, false);
                Assert.AreEqual(2, service.StartedCount, "동시 2개까지");

                int before = service.StartedCount;
                for (int i = 0; i < 1000; i++) service.ReportCue(AudioCues.ArrowHit, 1, Vector3.zero);
                service.Tick(0.016f);
                Assert.AreEqual(before + 1, service.StartedCount, "1000번 알려도 한 소리");

                for (int i = 0; i < 100; i++) service.ReportCue(AudioCues.ArrowHit, 1, Vector3.zero);
                service.Tick(0.016f);
                Assert.AreEqual(before + 1, service.StartedCount, "쿨다운 안에서는 또 울리지 않는다");
                service.Tick(0.6f);
                service.ReportCue(AudioCues.ArrowHit, 3, Vector3.zero);
                service.Tick(0.016f);
                Assert.AreEqual(before + 2, service.StartedCount, "쿨다운 뒤에는 다시");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); }
        }
    }
}
