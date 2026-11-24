#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HumanLastRampart.Combat.PerfLab
{
    // 메뉴 HLR > 성능 시제품 > 성능 시험 빌드 (D-022 H2, D-023)
    // - PerfLab 씬 하나만 Windows 64비트 Development Build로 만든다
    // - 빌드 씬 목록(EditorBuildSettings)은 바꾸지 않는다: 씬 경로를 빌드 옵션으로만 넘긴다
    // - GPU 시간 기록(Frame Timing Stats)은 빌드하는 동안만 켜고 원래 값으로 되돌린다
    // - 결과물: Game/Builds/PerfLab/ (gitignore 대상)
    // 실행하면 2초 뒤 자동 측정 → 끝나면 스스로 종료. 결과는 Player.log와 CSV:
    //   %USERPROFILE%\AppData\LocalLow\<회사명>\<제품명>\
    static class PerfLabBuildMenu
    {
        const string ScenePath = "Assets/_Project/Combat/PerfLab/PerfLab.unity";

        [MenuItem("HLR/성능 시제품/성능 시험 빌드 (PerfLab만)")]
        static void Build()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("PerfLab", "PerfLab 씬이 없습니다: " + ScenePath, "확인");
                return;
            }

            string outputFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/PerfLab"));
            string exePath = Path.Combine(outputFolder, "PerfLab.exe");

            bool previousFrameTiming = PlayerSettings.enableFrameTimingStats;
            PlayerSettings.enableFrameTimingStats = true;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = previousFrameTiming;
            }

            string logFolder = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", PlayerSettings.companyName, PlayerSettings.productName);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[PerfLab][BUILD] 성공 · {exePath} · {report.summary.totalSize / (1024 * 1024)}MB · {report.summary.totalTime.TotalSeconds:F0}초 · 결과 위치 {logFolder}");
                EditorUtility.RevealInFinder(exePath);
            }
            else
            {
                Debug.LogError($"[PerfLab][BUILD] 실패 · {report.summary.result} · 오류 {report.summary.totalErrors}개");
            }
        }
    }
}
#endif
