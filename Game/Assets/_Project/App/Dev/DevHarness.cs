#if UNITY_EDITOR
using System.Collections.Generic;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanLastRampart.App.Dev
{
    // 개발용 시험 진입 (P2-C06, 에디터 전용)
    // Boot 없이 화면 씬 하나만 열고 Play하면: 임시 GameFlow를 만들고, 프로젝트의 모든 카탈로그를 연결하고,
    // 임시 진행 데이터(현실 난이도)로 그 화면의 OnEnter를 부른다. 결과 화면은 임시 승리 결과로 연다.
    static class DevHarness
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfScreenSceneAlone()
        {
            if (GameFlow.Instance != null)
            {
                return;   // Boot에서 정상 시작
            }

            Scene scene = SceneManager.GetActiveScene();
            IScreenEntry entry = null;
            foreach (ScreenId screen in System.Enum.GetValues(typeof(ScreenId)))
            {
                entry = GameFlow.FindSingleEntry(scene, screen, out _);
                if (entry != null)
                {
                    break;
                }
            }

            if (entry == null)
            {
                return;   // 화면 씬이 아니다 (PerfLab 등)
            }

            Scene boot = SceneManager.CreateScene("DevBoot");
            var go = new GameObject("Dev GameFlow");
            SceneManager.MoveGameObjectToScene(go, boot);
            GameFlow flow = go.AddComponent<GameFlow>();
            flow.ConfigureCatalogs(FindAllCatalogs());
            flow.AdoptOnStart(scene, entry);
        }

        static IEnumerable<DefinitionCatalog> FindAllCatalogs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog != null)
                {
                    yield return catalog;
                }
            }
        }
    }
}
#endif