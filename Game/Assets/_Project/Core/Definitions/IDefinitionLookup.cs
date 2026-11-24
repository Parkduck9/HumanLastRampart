using System.Collections.Generic;

namespace HumanLastRampart.Core.Definitions
{
    // 정의 찾기 (C-010 3). App이 카탈로그를 합쳐 구현하고 IScreenContext.Definitions로 넘긴다.
    public interface IDefinitionLookup
    {
        T Get<T>(string id) where T : DefinitionBase;          // 없거나 종류가 다르면 null + 에러 로그
        IReadOnlyList<T> All<T>() where T : DefinitionBase;
    }
}