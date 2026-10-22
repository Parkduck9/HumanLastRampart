using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 단계 기계 + 화면을 매 프레임 돌리는 컴포넌트. 전투·정비 입구에서 Begin 한 줄로 시작한다.
    // 화면 주인(Source.Alive가 false)이 사라지면 스스로 정리하고 전투 시간 배율을 되돌린다
    public sealed class TutorialDirector : MonoBehaviour
    {
        TutorialMachine machine;
        TutorialView view;
        ITutorialSource source;

        public TutorialMachine Machine => machine;
        public TutorialStep Current => machine.Current;
        public bool Finished => machine == null || machine.Finished;
        public bool Next() => machine != null && machine.Next();

        public static TutorialDirector Begin(Transform parent, ITutorialSource source, TutorialStep first, TutorialStep last)
        {
            var go = new GameObject("Tutorial Director");
            go.transform.SetParent(parent, false);
            var director = go.AddComponent<TutorialDirector>();
            director.source = source;
            director.machine = new TutorialMachine(source, new TutorialState(), first, last);
            director.view = new TutorialView(go.transform, () => director.machine.Next(), () => director.machine.Skip());
            return director;
        }

        void Update()
        {
            if (machine == null) return;
            if (!source.Alive) { Destroy(gameObject); return; }
            machine.Update(Time.unscaledDeltaTime);
            view.Refresh(machine, source);
        }

        void OnDestroy()
        {
            view?.Dispose();
            source?.SetTimeScale(1f);
        }
    }
}
