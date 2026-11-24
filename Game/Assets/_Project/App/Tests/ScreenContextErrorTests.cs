using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace HumanLastRampart.App.Tests
{
    // C-003 v1.3: 화면이 데이터 오류를 알리면 그 컨텍스트는 이후 신호를 받지 않는다
    public sealed class ScreenContextErrorTests
    {
        GameObject host;
        GameFlow flow;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("FlowHost");
            flow = host.AddComponent<GameFlow>();   // 에디터 시험이라 Start는 돌지 않는다 (현재 화면 없음)
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void AfterReport_CompleteIsIgnored()
        {
            var context = new ScreenContext(flow, ScreenId.Result);
            context.ReportDataError("골드 범위 초과");

            LogAssert.Expect(LogType.Warning, new Regex("데이터 오류를 알렸으므로"));
            context.Complete(ScreenOutcome.Done);
        }

        [Test]
        public void SecondReport_KeepsSingleNotice()
        {
            var context = new ScreenContext(flow, ScreenId.Result);
            context.ReportDataError("첫 오류");

            LogAssert.Expect(LogType.Warning, new Regex("다시 알렸습니다"));
            context.ReportDataError("두 번째 오류");
        }

        [Test]
        public void ReportOnInvalidContext_IsIgnored()
        {
            var context = new ScreenContext(flow, ScreenId.Prep);
            context.Invalidate();

            LogAssert.Expect(LogType.Warning, new Regex("이미 닫힌 화면"));
            context.ReportDataError("늦은 알림");
            Assert.DoesNotThrow(() => context.Complete(ScreenOutcome.Done));
        }
    }
}