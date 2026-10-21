using System;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 동작 그림과 법선을 한 쌍으로 저장해, 크기가 다른 옛 법선이 새 그림에 붙지 않게 한다.
    [Serializable]
    public struct SheetNormalPair
    {
        public Texture2D Sheet;
        public Texture2D Normal;

        public static Texture2D Match(Texture2D sheet, Texture2D normal) => sheet != null && normal != null
            && sheet.width == normal.width && sheet.height == normal.height ? normal : null;
    }
}
