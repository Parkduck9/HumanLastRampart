using System.IO;
using System.Xml;
using NUnit.Framework;
using UnityEditor;

namespace HumanLastRampart.App.Tests
{
    // Microsoft Store 1.0 포장 (W46): 매니페스트 템플릿이 올바른 XML이고, 버전이 1.0.0 / 1.0.0.0 으로 맞는지 확인한다
    public sealed class MsixPackagingTests
    {
        static string MsixDir => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "Tools", "Build", "msix"));

        [Test]
        public void BundleVersion_Is_1_0_0() => Assert.AreEqual("1.0.0", PlayerSettings.bundleVersion);

        [Test]
        public void ManifestTemplate_IsWellFormed_FullTrust_Version1000()
        {
            string path = Path.Combine(MsixDir, "AppxManifest.template.xml");
            Assert.IsTrue(File.Exists(path), path);
            string text = File.ReadAllText(path);
            var doc = new XmlDocument();
            Assert.DoesNotThrow(() => doc.LoadXml(text), "매니페스트 템플릿이 올바른 XML이 아님");
            StringAssert.Contains("runFullTrust", text);
            StringAssert.Contains("Version=\"1.0.0.0\"", text);
            StringAssert.Contains("Executable=\"Lightbearers.exe\"", text);
            StringAssert.Contains("{IdentityName}", text);
            StringAssert.Contains("{Publisher}", text);
            StringAssert.Contains("{PublisherDisplayName}", text);
        }

        [Test]
        public void StoreIdentity_HasThreeFields()
        {
            string text = File.ReadAllText(Path.Combine(MsixDir, "store-identity.json"));
            StringAssert.Contains("\"IdentityName\"", text);
            StringAssert.Contains("\"Publisher\"", text);
            StringAssert.Contains("\"PublisherDisplayName\"", text);
        }
    }
}