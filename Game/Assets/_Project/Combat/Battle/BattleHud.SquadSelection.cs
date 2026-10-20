using System.Collections.Generic;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    public sealed partial class BattleHud
    {
        readonly HashSet<int> boxGroups = new HashSet<int>();
        RectTransform selectionBox, squadWheel;
        SquadWheelGraphic wheelGraphic;
        Image squadWheelBlocker;

        public bool SquadWheelOpen => squadWheel != null && squadWheel.gameObject.activeSelf;

        // 깃발의 실제 화면 자리로 고른다. 이동 중·대포·전사한 묶음은 제외한다
        public List<int> GroupsInScreenRect(BattleSim sim, Rect rect)
        {
            var result = new List<int>();
            for (int i = 0; i < groupMarkerIndex.Count; i++)
            {
                int group = groupMarkerIndex[i];
                BattleSim.GroupInfo info = sim.GetGroup(group);
                Vector2 p = groupMarkerScreen[i];
                if (info.Movable && !info.Moving && info.Side >= 0 && info.Alive > 0
                    && p.x >= rect.xMin && p.x <= rect.xMax && p.y >= rect.yMin && p.y <= rect.yMax)
                    result.Add(group);
            }
            return result;
        }

        public void SetBoxGroups(IEnumerable<int> groups)
        {
            boxGroups.Clear();
            if (groups != null) foreach (int group in groups) boxGroups.Add(group);
        }

        public void ShowSelectionBox(Vector2 start, Vector2 end)
        {
            if (selectionBox == null)
            {
                var go = new GameObject("Squad Selection", typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                selectionBox = (RectTransform)go.transform;
                for (int i = 0; i < 4; i++)
                {
                    Image edge = NewImage(go.transform, "Edge", UiTokens.Brass);
                    edge.raycastTarget = false;
                    RectTransform r = edge.rectTransform;
                    r.anchorMin = i < 2 ? new Vector2(0f, i) : new Vector2(i - 2, 0f);
                    r.anchorMax = i < 2 ? new Vector2(1f, i) : new Vector2(i - 2, 1f);
                    r.sizeDelta = i < 2 ? new Vector2(0f, 2f) : new Vector2(2f, 0f);
                    r.anchoredPosition = Vector2.zero;
                }
            }
            selectionBox.gameObject.SetActive(true);
            selectionBox.anchorMin = Viewport(Vector2.Min(start, end));
            selectionBox.anchorMax = Viewport(Vector2.Max(start, end));
            selectionBox.offsetMin = selectionBox.offsetMax = Vector2.zero;
            selectionBox.SetAsLastSibling();
        }

        public void HideSelectionBox() => selectionBox?.gameObject.SetActive(false);

        // 네모 판 없이 황동 테두리의 고리. 화면 화소를 캔버스 좌표로 바꿔 그리기와 누르기 크기를 맞춘다
        public void ShowSquadWheel(Vector2 screen, bool amount, int count, int disabledSide = -1)
        {
            if (squadWheelBlocker == null)
            {
                squadWheelBlocker = NewImage(root.transform, "Squad Wheel Input", Color.clear);
                Ui.Stretch(squadWheelBlocker.rectTransform);
                squadWheelBlocker.raycastTarget = true;
            }
            squadWheelBlocker.gameObject.SetActive(true);
            squadWheelBlocker.transform.SetAsLastSibling();
            if (squadWheel != null)
            {
                squadWheel.gameObject.SetActive(false);
                Object.Destroy(squadWheel.gameObject);
            }
            var go = new GameObject("Squad Wheel", typeof(RectTransform), typeof(CanvasRenderer), typeof(SquadWheelGraphic));
            go.transform.SetParent(root.transform, false);
            squadWheel = (RectTransform)go.transform;
            Anchor(squadWheel, Viewport(screen), Vector2.zero, new Vector2(320f, 320f), new Vector2(0.5f, 0.5f));
            wheelGraphic = go.GetComponent<SquadWheelGraphic>();
            wheelGraphic.Segments = amount ? 3 : 4;
            wheelGraphic.Disabled = disabledSide;
            wheelGraphic.raycastTarget = true;
            string[] labels = amount ? new[] { "1분대 보내기", "1/2 보내기", "전체 보내기" } : SideNames;
            for (int i = 0; i < labels.Length; i++)
            {
                float angle = (90f - i * 360f / labels.Length) * Mathf.Deg2Rad;
                Text label = Ui.Outlined(NewText(go.transform, "Choice " + i, UiTokens.Small, TextAnchor.MiddleCenter));
                Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 104f,
                    new Vector2(148f, 32f), new Vector2(0.5f, 0.5f));
                label.text = labels[i];
                label.color = i == disabledSide ? UiTokens.BodyDim : UiTokens.Brass;
                label.raycastTarget = false;
            }
            Text center = Ui.Outlined(NewText(go.transform, "Count", UiTokens.Small, TextAnchor.MiddleCenter));
            Anchor(center.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 40f), new Vector2(0.5f, 0.5f));
            center.text = count + "명";
            center.raycastTarget = false;
            if (groupPopup != null) groupPopup.SetActive(false);
        }

        public int PickSquadWheel(Vector2 screen)
        {
            if (!SquadWheelOpen || !RectTransformUtility.ScreenPointToLocalPointInRectangle(squadWheel, screen, null, out Vector2 p)) return -1;
            if (p.magnitude < SquadWheelGraphic.Inner || p.magnitude > SquadWheelGraphic.Outer) return -1;
            float step = 360f / wheelGraphic.Segments;
            int index = Mathf.FloorToInt(Mathf.Repeat(90f - Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg + step * 0.5f, 360f) / step);
            return index == wheelGraphic.Disabled ? -1 : index;
        }

        public void CloseSquadWheel()
        {
            squadWheel?.gameObject.SetActive(false);
            squadWheelBlocker?.gameObject.SetActive(false);
        }
    }
}
