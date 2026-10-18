using HumanLastRampart.UI;
using UnityEngine;

namespace HumanLastRampart.Meta
{
    // 정비 마당의 장부(수리·병영·훈련·용병·상점·함정 공방)는 한 번에 하나만 (K-047).
    // 새 장부가 열리면 앞 장부를 바로 치운다 — 닫히는 연출 중이면 그 자리에서 끝내고, 아직 열려 있으면 지운다.
    // (상점을 닫자마자 병영을 열면 닫히던 상점이 뒤에 비쳐 겹쳐 보였다 — 사진 r11 125)
    internal static class PrepLedger
    {
        static GameObject current;

        public static void Opened(GameObject shade)
        {
            if (current != null && current != shade)
            {
                UiAppear.FinishClosing(current);
                if (current != null)
                {
                    current.SetActive(false);
                    Object.Destroy(current);
                }
            }

            current = shade;
        }
    }
}
