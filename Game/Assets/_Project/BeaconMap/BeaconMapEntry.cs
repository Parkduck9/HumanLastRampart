using System.Collections.Generic;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.BeaconMap
{
    // 봉화선 지도 화면 (W10, Concept_Beacon_v1 9.3·9.7). 요새를 점화한 뒤 다음 정비 앞에서 보이고(계속 → 다음 화면),
    // 정비 화면의 작은 단추로도 열린다(보기 전용 → 돌아가기 → 정비).
    //
    // 만든 방식: 다른 화면(결과·정비·스토리)과 같은 '화면 씬 + 진입 컴포넌트' 틀(BeaconMap.unity).
    // 씬에는 진입 컴포넌트와 사진기뿐이고, 입체 모형(땅·요새·불빛·길·안개)은 OnEnter에서 코드로 만든다.
    // 글자·단추는 공용 ScreenView 캔버스 위에 얹는다 (바탕만 걷어 내 모형이 보이게).
    [DefaultExecutionOrder(100)]
    public sealed class BeaconMapEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.BeaconMap;

        public BeaconDiorama Diorama { get; private set; }
        public BeaconMapModel Model { get; private set; }

        RectTransform rootRect;
        readonly List<(int index, RectTransform rect)> labels = new List<(int, RectTransform)>();

        protected override ScreenView BuildView(IScreenContext context)
        {
            var campaign = context.Campaign;
            bool preview = campaign != null && campaign.BeaconMapPreview;
            Model = BeaconMapModel.Build(campaign, preview);

            var view = new ScreenView(transform, "", "");
            view.HidePanel();
            rootRect = (RectTransform)view.PanelGroup.transform.parent;   // 화면 캔버스
            Transform background = rootRect.Find("Background");           // 어두운 바탕은 걷어 낸다 — 뒤에 입체 모형이 보인다
            if (background != null) background.GetComponent<Image>().enabled = false;

            Diorama = BeaconDiorama.Create(transform, FindCamera(), Model);
            BuildHeader(rootRect, Model);
            BuildLabels(rootRect, Model);

            string key = preview ? "돌아가기" : "계속";
            Button next = Ui.Button(rootRect, key, key, () => Complete(ScreenOutcome.Done), UiButtonKind.Primary);
            Ui.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-UiTokens.S48 * 2f, UiTokens.S48 * 1.5f), new Vector2(288f, 64f), new Vector2(1f, 0f));
            view.SetFirst(next);
            return view;
        }

        static void BuildHeader(RectTransform root, BeaconMapModel model)
        {
            RectTransform box = Ui.Rect(root, "Map Header");
            Ui.Place(box, new Vector2(0f, 1f), new Vector2(UiTokens.S48 * 2f, -UiTokens.S48), new Vector2(900f, 120f), new Vector2(0f, 1f));   // 제목 64 + 간격 4 + 부제 44 = 112 (넘치지 않게)
            var column = box.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.spacing = UiTokens.S4;

            Text title = Ui.Outlined(Ui.Label(box, "Title", model.Title, UiTokens.H1, UiTokens.BrassLight, TextAnchor.MiddleLeft, true));
            Ui.Size(title, -1f, 64f);
            Text sub = Ui.Outlined(Ui.Label(box, "Subtitle", model.Subtitle, UiTokens.H2, UiTokens.Body, TextAnchor.MiddleLeft, true));
            Ui.Size(sub, -1f, 44f);
        }

        void BuildLabels(RectTransform root, BeaconMapModel model)
        {
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                FortressNode node = model.Nodes[i];
                if (node.State == FortressMapState.Fogged) continue;   // 모르는 요새는 이름도 모른다

                bool lit = node.State == FortressMapState.Lit;
                string text = $"요새 {node.Number}";
                if (lit && node.Garrison > 0) text += $"\n수비대 {node.Garrison:N0}";
                else if (node.Nights > 0) text += $"\n밤 {node.Night}/{node.Nights}";   // 지키는 요새는 밤 진행
                else if (!lit) text += "\n불 꺼진 요새";

                RectTransform rect = Ui.Rect(root, $"Fortress Label {node.Number}");
                Ui.Place(rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 84f), new Vector2(0.5f, 0f));
                Text label = Ui.Outlined(Ui.Label(rect, "Text", text, UiTokens.Text, lit ? UiTokens.BrassLight : UiTokens.BodyDim, TextAnchor.LowerCenter, true));
                Ui.Stretch(label.rectTransform);
                labels.Add((i, rect));
            }
        }

        Camera FindCamera()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                Camera found = root.GetComponentInChildren<Camera>(true);
                if (found != null) return found;
            }

            // 씬에 사진기가 없으면(개발용 임시 화면 등) 직접 만든다
            var go = new GameObject("Beacon Camera", typeof(Camera));
            go.transform.SetParent(transform, false);
            return go.GetComponent<Camera>();
        }

        // 요새 이름표는 화면 위 글자라, 모형이 떠다니는 동안 요새 머리 위를 따라간다
        void LateUpdate()
        {
            if (Diorama == null || Diorama.Cam == null || rootRect == null) return;
            foreach ((int index, RectTransform rect) in labels)
            {
                // 화면 비율이 달라도(사진 찍을 때 등) 사진기 화면 비율 위치 = 캔버스 비율 위치라 어긋나지 않는다
                Vector3 view = Diorama.Cam.WorldToViewportPoint(Diorama.LabelAnchor(index));
                bool visible = view.z > 0f;
                rect.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector2 size = rootRect.rect.size;
                rect.anchoredPosition = new Vector2((view.x - 0.5f) * size.x, (view.y - 0.5f) * size.y);
            }
        }
    }
}
