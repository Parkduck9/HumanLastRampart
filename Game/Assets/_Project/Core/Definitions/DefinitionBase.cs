using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 모든 정의 데이터의 부모 (C-010). 기본 수치만 담고 게임 중에 바꾸지 않는다.
    // Id: 영문 소문자·숫자·_ (예: enemy_goblin). 한 번 쓰기 시작하면 바꾸지 않는다.
    public abstract class DefinitionBase : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;   // 화면에 보일 한국어 이름 (Q-A6)

        public string Id => id;
        public string DisplayName => displayName;
    }
}