using NUnit.Framework;

namespace HumanLastRampart.Core.Tests
{
    // 배포 빌드에서는 개발 단축키가 막히고, 에디터·개발 빌드·-hlrSmoke 에서만 열리는지 (W46)
    public sealed class DevGateTests
    {
        [Test]
        public void ReleasePlayer_IsBlocked() => Assert.IsFalse(DevGate.Evaluate(false, false, new[] { "Lightbearers.exe" }));

        [Test]
        public void Editor_DevBuild_Smoke_AreAllowed()
        {
            Assert.IsTrue(DevGate.Evaluate(true, false, new string[0]));
            Assert.IsTrue(DevGate.Evaluate(false, true, new string[0]));
            Assert.IsTrue(DevGate.Evaluate(false, false, new[] { "Lightbearers.exe", "-hlrSmoke" }));
        }
    }
}