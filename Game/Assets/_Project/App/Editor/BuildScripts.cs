using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HumanLastRampart.App.Editor
{
    // 윈도 PC 빌드 (W29). batchmode -executeMethod HumanLastRampart.App.Editor.BuildScripts.BuildWindows
    // 장면은 EditorBuildSettings 그대로, 출력은 저장소 밖 C:\Unity\1\HLR-builds\win. 실패하면 종료 코드 1.
    // 스크립팅 백엔드: IL2CPP 모듈(win64 il2cpp 변형)이 깔려 있으면 IL2CPP, 아니면 Mono. 프로젝트 설정은 빌드 뒤 원래대로 돌려 놓는다.
    public static class BuildScripts
    {
        const string OutputDir = @"C:\Unity\1\HLR-builds\win";
        const string ExeName = "Lightbearers.exe";

        public static void BuildWindows() => Run(false);

        // 개발 빌드: development 켬, 스크립트 디버깅은 끔 (프로파일러·로그 확인용)
        public static void BuildWindowsDev() => Run(true);

        [MenuItem("HLR/Build/Windows Release")]
        static void MenuRelease() => Run(false, false);

        [MenuItem("HLR/Build/Windows Development")]
        static void MenuDev() => Run(true, false);

        static bool Il2CppInstalled()
        {
            string variations = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "windowsstandalonesupport", "Variations");
            return Directory.Exists(variations) && Directory.GetDirectories(variations, "win64_player_*_il2cpp").Length > 0;
        }

        static void Run(bool development, bool exitOnFinish = true)
        {
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(target);
            ScriptingImplementation backend = Il2CppInstalled() ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
            int code = 1;
            try
            {
                PlayerSettings.SetScriptingBackend(target, backend);
                string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0) throw new InvalidOperationException("EditorBuildSettings에 켜진 장면이 없습니다.");

                Directory.CreateDirectory(OutputDir);
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(OutputDir, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? BuildOptions.Development : BuildOptions.None,   // 스크립트 디버깅(AllowDebugging)은 켜지 않는다
                };

                Debug.Log($"[Build] 시작: {(development ? "개발" : "배포")} 빌드, 백엔드 {backend}, 장면 {scenes.Length}개 → {options.locationPathName}");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"[Build] 결과 {summary.result}, 크기 {summary.totalSize / 1048576f:F1}MB, 시간 {summary.totalTime.TotalMinutes:F1}분, 오류 {summary.totalErrors}, 경고 {summary.totalWarnings}, 백엔드 {backend}");
                code = summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[Build] 예외: " + e);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, previousBackend);
            }

            if (exitOnFinish) EditorApplication.Exit(code);
        }
    }
}
