using System.IO;
using System.Text.RegularExpressions;
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
            StringAssert.Contains("Version=\"{Version}\"", text);   // 패키지 버전은 package-msix.ps1 이 bundleVersion 에서 채운다
            StringAssert.Contains("Executable=\"Lightbearers.exe\"", text);
            StringAssert.Contains("{IdentityName}", text);
            StringAssert.Contains("{Publisher}", text);
            StringAssert.Contains("{PublisherDisplayName}", text);
        }

        // 매니페스트가 가리키는 로고마다 .scale-100 파일이 있어야 한다 (이름이 바뀌거나 빠지면 묶을 때·인증 때 실패하므로 미리 잡는다)
        [Test]
        public void ManifestLogos_HaveScale100Files()
        {
            string text = File.ReadAllText(Path.Combine(MsixDir, "AppxManifest.template.xml"));
            var matches = Regex.Matches(text, @"Images\\([A-Za-z0-9]+)\.png");
            Assert.GreaterOrEqual(matches.Count, 5);
            foreach (Match m in matches)
            {
                string file = Path.Combine(MsixDir, "Images", m.Groups[1].Value + ".scale-100.png");
                Assert.IsTrue(File.Exists(file), "로고 없음: " + file);
            }
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