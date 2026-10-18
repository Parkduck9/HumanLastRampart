using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    // 판이 한 장면 만에 툭 켜지고 꺼지지 않게 (S-24): 열 때 0.18초 동안 밀려 올라오며 나타나고, 닫을 때 0.12초 동안 사라진다.
    // 레이아웃이 자리를 정하지 않는 맨 위 판에만 붙인다 (자리를 직접 옮기므로). 시간은 실제 시간 — 시험의 고정 시간 간격과 상관없다
    [DisallowMultipleComponent]
    public sealed class UiAppear : MonoBehaviour
    {
        CanvasGroup group;
        RectTransform rect;
        Vector2 home, from;
        float t = 1f;
        UiClock clock;
        static readonly System.Collections.Generic.HashSet<UiAppear> moving = new System.Collections.Generic.HashSet<UiAppear>();

        // 지금 열리거나 닫히는 판이 있는가 (시험이 사진을 찍기 전에 기다린다)
        public static bool AnyAnimating { get { moving.RemoveWhere(a => a == null); return moving.Count > 0; } }
        bool closing, destroyWhenClosed, moves;
        Action done;

        public bool Closing => closing;

        // 보이기 (offset = 처음 자리에서 얼마나 떨어져서 들어오는지)
        public static UiAppear Show(GameObject go, Vector2 offset = default)
        {
            if (go == null) return null;
            UiAppear appear = Ensure(go);
            if (!go.activeSelf) go.SetActive(true);
            appear.Begin(offset);
            return appear;
        }

        // 숨기기. destroy면 다 사라진 뒤 지운다 (그동안 안의 단추는 다시 눌리지 않는다)
        public static void Hide(GameObject go, bool destroy = false, Action onDone = null)
        {
            if (go == null) return;
            if (!go.activeInHierarchy)
            {
                if (destroy) UnityEngine.Object.Destroy(go); else go.SetActive(false);
                onDone?.Invoke();
                return;
            }

            Ensure(go).End(destroy, onDone);
        }

        // (?? 는 Unity의 '빈 컴포넌트'를 null로 보지 않으므로 쓰지 않는다)
        static UiAppear Ensure(GameObject go) => go.TryGetComponent(out UiAppear found) ? found : go.AddComponent<UiAppear>();

        // 매 프레임 불러도 되는 켜고 끄기 (이미 그 상태면 아무것도 안 한다)
        public static void Toggle(GameObject go, bool visible, Vector2 offset = default)
        {
            if (go == null) return;
            go.TryGetComponent(out UiAppear appear);
            if (visible)
            {
                if (go.activeSelf && (appear == null || !appear.closing)) return;
                Show(go, offset);
            }
            else if (go.activeSelf && (appear == null || !appear.closing)) Hide(go);
        }

        void Prepare()
        {
            if (group == null) group = TryGetComponent(out CanvasGroup found) ? found : gameObject.AddComponent<CanvasGroup>();
            if (rect == null) rect = (RectTransform)transform;
        }

        void Begin(Vector2 offset)
        {
            Prepare();
            if (closing) rect.anchoredPosition = home;   // 닫히던 중이면 제자리에서 다시
            closing = false;
            destroyWhenClosed = false;
            done = null;
            home = rect.anchoredPosition;
            from = offset;
            moves = offset != Vector2.zero;
            group.blocksRaycasts = true;
            t = 0f;
            clock = default;
            moving.Add(this);
            Apply();
        }

        void End(bool destroy, Action onDone)
        {
            Prepare();
            if (!closing) home = moves && t < 1f ? home : rect.anchoredPosition;
            closing = true;
            destroyWhenClosed = destroy;
            done = onDone;
            group.blocksRaycasts = false;
            if (destroy)
                foreach (Button b in GetComponentsInChildren<Button>(true)) b.interactable = false;
            t = 1f;
            clock = default;
            moving.Add(this);
        }

        void Update()
        {
            float dt = clock.Tick(0.05f);
            if (closing)
            {
                t -= dt / UiTokens.Close;
                if (t <= 0f)
                {
                    t = 0f;
                    Apply();
                    closing = false;
                    rect.anchoredPosition = home;
                    Action finished = done;
                    done = null;
                    if (destroyWhenClosed) Destroy(gameObject);
                    else
                    {
                        group.alpha = 1f;
                        group.blocksRaycasts = true;
                        gameObject.SetActive(false);
                    }

                    moving.Remove(this);
                    finished?.Invoke();
                    return;
                }
            }
            else if (t < 1f) t = Mathf.Min(1f, t + dt / UiTokens.Open);
            else { moving.Remove(this); return; }

            Apply();
        }

        // 열리는 중에 꺼지면 다 열린 모양으로 (다음에 켜질 때 반투명으로 남지 않게)
        void OnDisable()
        {
            moving.Remove(this);
            if (!closing && group != null && t < 1f) { t = 1f; Apply(); }
        }

        void OnDestroy() => moving.Remove(this);

        void Apply()
        {
            float e = 1f - (1f - t) * (1f - t);   // 빨리 나와서 부드럽게 멈춤
            group.alpha = e;
            if (moves) rect.anchoredPosition = home + from * (1f - e);
        }
    }
}
