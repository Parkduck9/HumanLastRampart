#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HumanLastRampart.Combat.PerfLab
{
    // 메뉴 HLR > 성능 시제품 > PerfLab 씬 만들기
    // 빈 씬에 PerfLabBootstrap 하나를 두고 저장한다. 빌드 씬 목록에는 넣지 않는다 (PERF_PROTOTYPE.md 3장).
    static class PerfLabSceneMenu
    {
        const string ScenePath = "Assets/_Project/Combat/PerfLab/PerfLab.unity";
        const string ShaderPath = "Assets/_Project/Combat/PerfLab/PerfLabSprite.shader";

        [MenuItem("HLR/성능 시제품/PerfLab 씬 만들기")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("PerfLab", "PerfLab 씬이 이미 있습니다. 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PerfLab");
            var bootstrap = root.AddComponent<PerfLabBootstrap>();

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("spriteShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[PerfLab] 씬을 만들었습니다: " + ScenePath);
        }
    }
}
#endif
