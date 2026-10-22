using System;
using System.Collections;
using System.IO;
using UnityEngine;

// W18: 시험 사이로 새는 전역 상태를 시험마다 처음으로 되돌린다.
// 시험이 중간에 실패하면 끝줄의 Time.captureDeltaTime = 0 같은 정리가 실행되지 않아 다음 시험이 이어받는다
// (고정 간격이 남으면 프레임 수로 기다리는 시험의 시간 흐름이 달라진다). SetUp·TearDown이 실패 여부와 상관없이 정리한다.
public sealed partial class PlayProbe
{
    string isolationPreviousSaveFolder;
    string isolationSaveFolder;

    [NUnit.Framework.SetUp]
    public void IsolateGlobalState()
    {
        ResetTimeAndFlags();
        // 저장은 시험마다 빈 임시 폴더 (이전 시험이 남긴 저장·사용자 저장이 메인 메뉴·이어하기에 끼어들지 않게)
        isolationPreviousSaveFolder = HumanLastRampart.Core.Save.CampaignSave.Folder;
        isolationSaveFolder = Path.Combine(Path.GetTempPath(), "hlr_probe_iso_" + Guid.NewGuid().ToString("N"));
        HumanLastRampart.Core.Save.CampaignSave.Folder = isolationSaveFolder;
        HumanLastRampart.Core.Settings.GameSettings.Load();
    }

    [NUnit.Framework.TearDown]
    public void RestoreGlobalState()
    {
        ResetTimeAndFlags();
        if (isolationPreviousSaveFolder != null) HumanLastRampart.Core.Save.CampaignSave.Folder = isolationPreviousSaveFolder;
        try { if (isolationSaveFolder != null && Directory.Exists(isolationSaveFolder)) Directory.Delete(isolationSaveFolder, true); }
        catch (IOException) { }
        isolationPreviousSaveFolder = isolationSaveFolder = null;
        HumanLastRampart.App.Flow.GameFlow.TestSeedOverride = 0;
    }

    // 결과 뒤 길이 둘이다 (바로 정비 / 점화한 밤이면 봉화선 지도 → 정비). 정비에 닿을 때까지 지도가 나오면 [계속]을 누른다
    static IEnumerator PassBeaconMapToPrep(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            var flow = HumanLastRampart.App.Flow.GameFlow.Instance;
            if (flow != null && flow.CurrentScreen == HumanLastRampart.Core.Flow.ScreenId.Prep) yield break;
            if (flow != null && flow.CurrentScreen == HumanLastRampart.Core.Flow.ScreenId.BeaconMap)
            {
                var go = FindReady("계속");
                if (go != null) { go.onClick.Invoke(); yield return null; continue; }
            }
            yield return null;
        }
    }

    static void ResetTimeAndFlags()
    {
        Time.captureDeltaTime = 0f;
        Time.timeScale = 1f;
        KeepPreparation = false;
        HumanLastRampart.App.Flow.GameFlow.TestSeedOverride = 20261022;   // 새 게임 씨앗 고정 (시험 결과가 실행마다 달라지지 않게)
    }
}
