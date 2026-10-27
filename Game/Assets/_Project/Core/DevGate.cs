using System;
using UnityEngine;

namespace HumanLastRampart.Core
{
    // 개발용 단축키(F12 시험 단추, F8 성능 표시) 허용 여부 (W46). 배포 빌드에서는 막는다.
    // 허용: 에디터, 개발 빌드, 또는 실행 인자 -hlrSmoke (연기 시험).
    public static class DevGate
    {
        public const string SmokeFlag = "-hlrSmoke";

        static bool? cached;

        public static bool Allowed => cached ??= Evaluate(Application.isEditor, Debug.isDebugBuild, Environment.GetCommandLineArgs());

        public static bool Evaluate(bool isEditor, bool isDebugBuild, string[] args)
            => isEditor || isDebugBuild || (args != null && Array.IndexOf(args, SmokeFlag) >= 0);
    }
}