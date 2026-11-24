using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.Combat.Tests
{
    // 플레이어 빌드에서 Shader.Find가 null이 되지 않도록: 코드가 이름으로 찾는 셰이더는 Always Included이거나 Resources 폴더에 있어야 한다 (W44)
    public sealed class ShaderFindCoverageTests
    {
        static readonly Regex FindCall = new Regex("Shader\\.Find\\(\\s*\"([^\"]+)\"");

        [Test]
        public void EveryShaderFindNameIsAlwaysIncludedOrInResources()
        {
            var included = new HashSet<string>();
            var gs = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            SerializedProperty list = gs.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
            {
                var s = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (s != null) included.Add(s.name);
            }

            var names = new SortedSet<string>();
            string root = Path.Combine(Application.dataPath, "_Project");
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;   // 테스트 코드는 플레이어에 안 들어감
                string text = File.ReadAllText(file);
                foreach (Match m in FindCall.Matches(text)) names.Add(m.Groups[1].Value);
            }

            Assert.Greater(names.Count, 0);
            foreach (string n in names)
            {
                if (n.StartsWith("Hidden/")) continue;   // 엔진 내장
                if (included.Contains(n)) continue;
                Shader s = Shader.Find(n);
                string path = s != null ? AssetDatabase.GetAssetPath(s) : "";
                Assert.IsTrue(path.Contains("/Resources/"), $"Shader.Find(\"{n}\")가 Always Included도 Resources도 아님 (플레이어에서 null 위험)");
            }
        }
    }
}
