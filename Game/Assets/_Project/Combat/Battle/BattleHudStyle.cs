using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // K-047: 전투 화면 색·테두리·단추는 모두 HumanLastRampart.UI.Ui·UiTokens 한 벌로 옮겼다 (S-28 — 같은 것을 두 번 만들지 않는다).
    // 여기는 예전 이름으로 Codex HudArt 그림을 부르던 곳을 위한 얇은 길만 남긴다
    public static class HudStyle
    {
        public static Sprite Art(string path, Vector4 border = default) => HumanLastRampart.UI.UiArt.Hud(path, border);

        public static Image ArtImage(Transform parent, string name, string path, Vector2 size)
        {
            Image image = HumanLastRampart.UI.Ui.HudIcon(parent, name, path, size.x);
            return image;
        }
    }
}
