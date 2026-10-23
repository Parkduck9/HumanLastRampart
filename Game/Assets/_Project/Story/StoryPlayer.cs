using System;
using System.Collections.Generic;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HumanLastRampart.Story
{
    // 5번 스토리 화면 (K-050): 배경 + 좌우 인물(말하는 쪽 밝게, 듣는 쪽 어둡게) + 아래 대사 상자(이름표, 한 자씩 나오기).
    // 누르기(마우스·터치)·스페이스·엔터: 글이 나오는 중이면 끝까지 → 다 나왔으면 다음 줄. 마지막 줄 다음이면 Finished.
    // [자동] = 다 나온 뒤 잠깐 쉬고 저절로 다음 줄, [넘기기] = 바로 끝.
    // 대사 상자·이름표·넘김 표시는 Codex 그림(Docs/GDD/Story_에셋요청.md) 전까지 코드로 그린 임시 도트 모양 —
    // 슬롭 배제 기준(UI_NoSlop): 종이 바탕 + 잉크 2점 테두리 + 깎은 귀, 점 3px, 글자 32/24/20, 글자 기호 대신 그린 표시
    public sealed class StoryPlayer : MonoBehaviour
    {
        const float Dot = UiTokens.Dot;                    // UI 한 점 = 화면 3px (1080 기준)
        const float CharsPerSecond = 36f;
        const float AutoPause = 1.1f;            // 자동: 다 나온 뒤 기다림 (+ 글자 수에 비례)
        const float FadeClose = 0.3f, FadeOpen = 0.4f;
        const float ShakeTime = 0.45f;
        static readonly Vector2 PortraitSize = new Vector2(512f, 768f);   // 256×384 칸의 정수 2배 (점이 고르게)

        static readonly Color Listening = new Color(0.42f, 0.42f, 0.48f, 1f);

        public event Action Finished;
        public int LineIndex => index;
        public int LineTotal => lines.Count;
        public bool LineFullyShown => typing == false && fade == FadeState.None;
        public bool Auto => auto;

        List<(StoryScene scene, StoryLine line)> lines;
        CanvasGroup inputGate;   // 화면(ScreenView) 입력 막힘이면 키보드도 무시
        RectTransform stage, box, nameplate, nextMark;
        Image tail;
        Button autoButton;
        RawImage backdrop;
        AspectRatioFitter backdropFit;
        readonly RawImage[] portraits = new RawImage[2];   // 0 왼쪽 · 1 오른쪽
        Text body, speaker;
        Image fadeCover;
        Font font;

        int index = -1;
        string fullText = "";
        int lastTick;   // 글자 소리 (W24): 마지막으로 소리 낸 글자 수
        float typeStart;
        bool typing, auto, finished;
        float shownAt;
        float shakeUntil;
        StorySide speakingSide;
        enum FadeState { None, Closing, Opening }
        FadeState fade;
        float fadeStart;

        public static StoryPlayer Create(RectTransform screenRoot, StoryScript script, Font font)
        {
            var go = new GameObject("Story Player", typeof(RectTransform));
            go.transform.SetParent(screenRoot, false);
            go.transform.SetAsLastSibling();   // 기본 창보다 위
            var player = go.AddComponent<StoryPlayer>();
            player.font = font;
            player.inputGate = screenRoot.GetComponent<CanvasGroup>();
            player.lines = script.Flatten();
            player.Build((RectTransform)go.transform);
            return player;
        }

        // ── 만들기 ──
        void Build(RectTransform root)
        {
            Stretch(root);

            // 장면 (흔들림 대상): 바탕 → 배경 그림 → 인물 둘
            stage = NewRect(root, "Story Stage");
            Stretch(stage);
            NewImage(stage, "Night", new Color(0.06f, 0.07f, 0.08f));
            var backHolder = NewRect(stage, "Backdrop Holder");
            Stretch(backHolder);
            var backObj = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            backObj.transform.SetParent(backHolder, false);
            backdrop = backObj.GetComponent<RawImage>();
            backdrop.raycastTarget = false;
            backdropFit = backObj.GetComponent<AspectRatioFitter>();
            backdropFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backdrop.enabled = false;
            for (int i = 0; i < 2; i++)
            {
                var obj = new GameObject(i == 0 ? "Portrait Left" : "Portrait Right", typeof(RectTransform), typeof(RawImage));
                obj.transform.SetParent(stage, false);
                var rect = (RectTransform)obj.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(i == 0 ? 0.25f : 0.75f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = PortraitSize;
                rect.anchoredPosition = new Vector2(0f, 140f);
                portraits[i] = obj.GetComponent<RawImage>();
                portraits[i].raycastTarget = false;
                portraits[i].enabled = false;
            }

            // 아래 어둠띠: 상자 뒤 장면을 조금 눌러 글이 또렷하게
            var under = NewImage(root, "Box Shade", UiTokens.Alpha(UiTokens.Outline, 0.35f));
            under.rectTransform.anchorMin = Vector2.zero;
            under.rectTransform.anchorMax = new Vector2(1f, 0f);
            under.rectTransform.pivot = new Vector2(0.5f, 0f);
            under.rectTransform.sizeDelta = new Vector2(0f, 336f);

            // 대사 상자 (K-047): 옥토패스식 손그림 말풍선 틀(UiArt.Bubble) + 말하는 사람 쪽으로 꼬리
            Image bubble = Ui.Panel(root, "Story Box", UiPanel.Bubble);
            bubble.raycastTarget = false;
            box = bubble.rectTransform;
            box.anchorMin = new Vector2(0.06f, 0f);
            box.anchorMax = new Vector2(0.94f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            box.offsetMin = new Vector2(0f, UiTokens.S32);
            box.offsetMax = new Vector2(0f, UiTokens.S32 + 264f);
            // 꼬리 그림은 위가 넓고 아래 왼쪽이 뾰족 → 위아래를 뒤집어 넓은 쪽을 말풍선 윗변에 붙인다 (좌우는 ApplyLine이 말하는 쪽으로)
            tail = Ui.Image(box, "Story Tail", Color.white);
            tail.sprite = UiArt.Get(UiArt.BubbleTail);
            Ui.Place(tail.rectTransform, new Vector2(0.25f, 1f), new Vector2(0f, -Dot * 2f), UiArt.Size(tail.sprite, 2f), new Vector2(0.5f, 1f));
            tail.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            tail.gameObject.SetActive(false);
            body = Ui.Label(box, "Story Text", "", UiTokens.H2, UiTokens.Ink, TextAnchor.UpperLeft);
            body.font = font != null ? font : UiFonts.Story;
            body.fontSize = UiTokens.H2;   // Label이 갈무리용으로 맞춘 크기를 되돌림 (고운바탕은 11 배수 필요 없음)
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(64f, UiTokens.S48);
            body.rectTransform.offsetMax = new Vector2(-96f, -UiTokens.S48);
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.lineSpacing = 1.15f;

            // 이름표: 나무 간판(UiArt.Plate) + 황동 글(세계 글꼴), 상자 왼쪽 위에 걸침
            Image plate = Ui.Panel(box, "Story Nameplate", UiPanel.Plate);
            plate.raycastTarget = false;
            nameplate = plate.rectTransform;
            nameplate.anchorMin = nameplate.anchorMax = new Vector2(0f, 1f);
            nameplate.pivot = new Vector2(0f, 0.5f);
            nameplate.anchoredPosition = new Vector2(UiTokens.S48, 0f);
            nameplate.sizeDelta = new Vector2(240f, 56f);
            speaker = Ui.Label(nameplate, "Story Speaker", "", UiTokens.Text, UiTokens.BrassLight, TextAnchor.MiddleCenter, true);
            speaker.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(speaker.rectTransform);
            HumanLastRampart.UI.UiFonts.Bold(speaker);

            // 넘김 표시: 도트 아이콘(아래 화살), 다 나오면 한 점씩 까딱
            Image mark = Ui.Icon(box, "Story Next", UiArt.IconDown, 1f, UiTokens.Ink);
            nextMark = mark.rectTransform;
            nextMark.anchorMin = nextMark.anchorMax = new Vector2(1f, 0f);
            nextMark.pivot = new Vector2(1f, 0f);
            nextMark.anchoredPosition = new Vector2(-UiTokens.S48, UiTokens.S32);

            // 누르기 받는 판 (화면 전체, 인물·상자 위 / 단추 아래)
            var catcher = NewImage(root, "Story Advance", new Color(0f, 0f, 0f, 0f));
            Stretch(catcher.rectTransform);
            catcher.raycastTarget = true;
            var advance = catcher.gameObject.AddComponent<Button>();
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(Advance);

            // 오른쪽 위 단추: [자동] [넘기기] — 나무판, 고르면 황동 빛 테두리
            // K-047: Ui 나무 단추 (열쇠 = 보이는 글), 자동이 켜지면 빛 테두리가 남는다
            autoButton = Ui.Button(root, "자동", "자동", ToggleAuto, UiButtonKind.Tab);
            Ui.Place((RectTransform)autoButton.transform, new Vector2(1f, 1f), new Vector2(-UiTokens.S32 - 160f - UiTokens.S16, -UiTokens.S32), new Vector2(160f, 56f), new Vector2(1f, 1f));
            Button skip = Ui.Button(root, "넘기기", "넘기기", Skip);
            Ui.Place((RectTransform)skip.transform, new Vector2(1f, 1f), new Vector2(-UiTokens.S32, -UiTokens.S32), new Vector2(160f, 56f), new Vector2(1f, 1f));

            // 암전 덮개 (맨 위, 누르기 막지 않음)
            fadeCover = NewImage(root, "Story Fade", Color.black);
            Stretch(fadeCover.rectTransform);
            fadeCover.raycastTarget = false;
            fadeCover.color = new Color(0f, 0f, 0f, 0f);
        }

        void Start()
        {
            if (lines == null || lines.Count == 0) { Finish(); return; }
            // 첫 줄이 암전이면 까만 화면에서 열린다
            if (lines[0].line.Effect == StoryEffect.FadeBlack) fadeCover.color = Color.black;
            Go(0);
        }

        // ── 진행 ──
        void Go(int next)
        {
            index = next;
            if (index >= lines.Count) { Finish(); return; }
            StoryLine line = lines[index].line;
            typing = false;
            body.text = "";
            if (line.Effect == StoryEffect.FadeBlack)
            {
                // 이미 까맣다면(첫 줄) 바로 열고, 아니면 닫았다가 연다
                fade = fadeCover.color.a >= 0.99f ? FadeState.Opening : FadeState.Closing;
                fadeStart = Time.unscaledTime;
                if (fade == FadeState.Opening) ApplyLine();
                return;
            }

            ApplyLine();
            BeginTyping();
        }

        void ApplyLine()
        {
            var (scene, line) = lines[index];
            Texture2D back = line.Background != null ? line.Background : scene.Background;
            backdrop.enabled = back != null;
            if (back != null)
            {
                backdrop.texture = back;
                backdropFit.aspectRatio = (float)back.width / Mathf.Max(1, back.height);
            }

            // 장면이 바뀌면 서 있던 인물을 내린다
            if (index == 0 || !ReferenceEquals(lines[index - 1].scene, scene))
                foreach (RawImage p in portraits) p.enabled = false;

            if (line.Side != StorySide.None && line.Portrait != null)
            {
                RawImage slot = portraits[line.Side == StorySide.Left ? 0 : 1];
                slot.enabled = true;
                slot.texture = line.Portrait;
                line.Portrait.filterMode = FilterMode.Point;
                slot.uvRect = StoryCells.Uv(line.Portrait, line.Cell);
            }

            speakingSide = line.Side;
            bool named = line.Side != StorySide.None && !string.IsNullOrWhiteSpace(line.Speaker);
            nameplate.gameObject.SetActive(named);
            if (named)
            {
                speaker.text = line.Speaker;
                nameplate.sizeDelta = new Vector2(Mathf.Max(200f, speaker.preferredWidth + 64f), 56f);
            }

            // 말풍선 꼬리: 말하는 사람 쪽 (왼쪽 인물 = 0.25, 오른쪽 = 0.75, 해설이면 없음)
            tail.gameObject.SetActive(line.Side != StorySide.None);
            if (line.Side != StorySide.None)
            {
                bool right = line.Side == StorySide.Right;
                tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(right ? 0.75f : 0.25f, 1f);
                tail.rectTransform.localScale = new Vector3(right ? -1f : 1f, -1f, 1f);   // 끝이 인물 쪽을 향하게
            }

            if (line.Effect == StoryEffect.Shake) shakeUntil = Time.unscaledTime + ShakeTime;
            fullText = line.Text ?? "";
        }

        void BeginTyping()
        {
            typing = true;
            lastTick = 0;
            typeStart = Time.unscaledTime;
            body.text = "";
        }

        void ShowAll()
        {
            typing = false;
            body.text = fullText;
            shownAt = Time.unscaledTime;
        }

        // 누르기·스페이스·엔터
        public void Advance()
        {
            if (finished || !InputOpen || fade != FadeState.None) return;
            if (typing) { ShowAll(); return; }
            Go(index + 1);
        }

        public void ToggleAuto()
        {
            if (finished || !InputOpen) return;
            auto = !auto;
            Ui.SetOn(autoButton, auto);
        }

        public void Skip()
        {
            if (finished || !InputOpen) return;
            Finish();
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            Finished?.Invoke();
        }

        bool InputOpen => inputGate == null || inputGate.interactable;

        void Update()
        {
            if (finished) return;
            float now = Time.unscaledTime;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                Advance();

            // 암전
            if (fade == FadeState.Closing)
            {
                float t = Mathf.Clamp01((now - fadeStart) / FadeClose);
                fadeCover.color = new Color(0f, 0f, 0f, t);
                if (t >= 1f) { ApplyLine(); fade = FadeState.Opening; fadeStart = now; }
            }
            else if (fade == FadeState.Opening)
            {
                float t = Mathf.Clamp01((now - fadeStart) / FadeOpen);
                fadeCover.color = new Color(0f, 0f, 0f, 1f - t);
                if (t >= 1f) { fade = FadeState.None; BeginTyping(); }
            }

            // 한 자씩
            if (typing)
            {
                int visible = Mathf.Min(fullText.Length, Mathf.FloorToInt((now - typeStart) * CharsPerSecond));
                body.text = fullText.Substring(0, visible);
                if (visible >= lastTick + 3) { lastTick = visible; HumanLastRampart.Core.Audio.Sound.Play(HumanLastRampart.Core.Audio.AudioCues.StoryTick); }
                if (visible >= fullText.Length) ShowAll();
            }

            // 자동 넘김
            if (auto && !typing && fade == FadeState.None && index >= 0 && now - shownAt > AutoPause + fullText.Length * 0.03f)
                Go(index + 1);

            // 말하는 쪽 밝게·조금 위로, 듣는 쪽 어둡게
            for (int i = 0; i < 2; i++)
            {
                bool speaking = (i == 0 && speakingSide == StorySide.Left) || (i == 1 && speakingSide == StorySide.Right);
                Color target = speaking ? Color.white : Listening;
                portraits[i].color = Color.Lerp(portraits[i].color, target, Mathf.Clamp01(Time.unscaledDeltaTime * 10f));
                var rect = portraits[i].rectTransform;
                float y = Mathf.Lerp(rect.anchoredPosition.y, speaking ? 140f + Dot * 4f : 140f, Mathf.Clamp01(Time.unscaledDeltaTime * 10f));
                rect.anchoredPosition = new Vector2(0f, y);
            }

            // 흔들림
            if (now < shakeUntil)
            {
                float k = (shakeUntil - now) / ShakeTime * 14f;
                stage.anchoredPosition = new Vector2((Mathf.PerlinNoise(now * 40f, 0f) - 0.5f) * 2f * k, (Mathf.PerlinNoise(0f, now * 40f) - 0.5f) * 2f * k);
            }
            else stage.anchoredPosition = Vector2.zero;

            // 넘김 표시: 다 나왔을 때만, 0.5초마다 한 점 까딱 (점 크기 단위로 움직여 도트가 번지지 않게)
            bool ready = !typing && fade == FadeState.None;
            nextMark.gameObject.SetActive(ready);
            if (ready) nextMark.anchoredPosition = new Vector2(-UiTokens.S48, Mathf.Repeat(now, 1f) < 0.5f ? UiTokens.S32 : UiTokens.S32 - Dot);
        }

        // ── 그리기 도우미 (판·단추·글은 HumanLastRampart.UI.Ui 부품) ──
        static RectTransform NewRect(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        static Image NewImage(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

    }
}
