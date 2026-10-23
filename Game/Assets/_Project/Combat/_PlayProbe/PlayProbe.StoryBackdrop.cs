using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.Story;
using HumanLastRampart.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;
using HumanLastRampart.Core.Flow;

// W32: 이야기 장면마다 K-117 배경(죽은 등대 / 다시 켜진 등대) + 대사 상자 사진 (1920×1080)
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator StoryBackdropShots()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);

        string[] ids = { "story_intro", "story_fortress1_arrive", "story_fortress2_arrive", "story_defeat", "story_fortress1_relit", "story_run1_complete" };
        foreach (string id in ids)
        {
            var script = Resources.Load<StoryScript>("Stories/" + id);
            Assert.IsNotNull(script, id);
            // 임시 전체 화면 캔버스 위에 재생기만 올린다 (실제 화면 흐름과 같은 StoryPlayer)
            var canvasObj = new GameObject("Probe Story Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            StoryPlayer player = StoryPlayer.Create((RectTransform)canvasObj.transform, script, UiFonts.Story);
            for (int i = 0; i < 6; i++) yield return null;

            int shotsInStory = 0;
            for (int guard = 0; guard < 12 && shotsInStory < 2; guard++)
            {
                for (int i = 0; i < 120 && !player.LineFullyShown; i++)
                {
                    if (i > 30) player.Advance();   // 암전이 끝난 뒤에도 글이 나오는 중이면 끝까지
                    yield return new WaitForSecondsRealtime(0.03f);   // 재생기 연출은 실제 시간이라, 프레임이 빠른 기계에서도 끝나게 (W34)
                }
                for (int i = 0; i < 4; i++) yield return null;
                if (!player.LineFullyShown) continue;
                bool speaking = SpeakerText() != "";
                if (shotsInStory == 0 || speaking)
                {
                    Note($"{id}: {SpeakerText()} — {StoryText()}");
                    yield return Capture($"{id}_{(speaking ? "speak" : "narr")}", 1920, 1080);
                    shotsInStory++;
                }
                player.Advance();   // 다음 줄
                for (int i = 0; i < 4; i++) yield return null;
            }

            Object.Destroy(canvasObj);
            yield return null;
            Assert.GreaterOrEqual(shotsInStory, 1, id);
        }

        File.WriteAllText(Path.Combine(Out, "story_backdrop.txt"), log.ToString(), new UTF8Encoding(false));
        Time.captureDeltaTime = 0f;
    }
}
