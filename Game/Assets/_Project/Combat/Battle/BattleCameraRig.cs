using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.Battle
{
    // 1번 화면 카메라 (D-025: 평소 원근 낮은 카메라). 임시 조작 — 모서리 돌기·주인공 이동은 CY1-C3 설계 후
    // A/D(←/→) 성벽 따라 이동, Q/E 왼쪽/오른쪽 성벽으로, Tab 전체 보기
    public sealed class BattleCameraRig
    {
        const float PanSpeed = 25f;

        readonly Camera camera;
        readonly float castleHalf;
        readonly float wallHeight;
        float along;
        bool overview;

        public int Side { get; private set; }
        public bool Overview => overview;

        public BattleCameraRig(Camera camera, float castleHalf, float wallHeight)
        {
            this.camera = camera;
            this.castleHalf = castleHalf;
            this.wallHeight = wallHeight;
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1200f;
            Apply();
        }

        public void Update(float unscaledDt)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                overview = !overview;
            }

            // 성 안에서 바깥을 볼 때 왼쪽 벽 = 시계 반대 방향 (북 → 서)
            if (keyboard.qKey.wasPressedThisFrame) { Side = (Side + 3) % 4; along = 0f; }
            if (keyboard.eKey.wasPressedThisFrame) { Side = (Side + 1) % 4; along = 0f; }

            float pan = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan += 1f;
            along = Mathf.Clamp(along + pan * PanSpeed * unscaledDt, -castleHalf, castleHalf);

            Apply();
        }

        public void ShowSide(int side)
        {
            Side = side;
            along = 0f;
            overview = false;
            Apply();
        }

        void Apply()
        {
            var n = WallGeometry.Outward(Side);
            var r = WallGeometry.Right(Side);
            var outward = new Vector3(n.x, 0f, n.z);
            var right = new Vector3(r.x, 0f, r.z);
            if (overview)
            {
                // 성 전체를 위에서 비스듬히 (지도 모드 D-025의 임시 대용)
                Quaternion look = Quaternion.LookRotation(outward) * Quaternion.Euler(60f, 0f, 0f);
                camera.transform.SetPositionAndRotation(-outward * castleHalf * 2.2f + Vector3.up * castleHalf * 3.2f, look);
                return;
            }

            // 성벽 위 병사들 바로 뒤에서 바깥 들판을 본다 (PerfLab 카메라 A와 같은 높이·각도)
            Vector3 position = outward * (castleHalf - 7f) + right * along + Vector3.up * (wallHeight + 2.6f);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(outward) * Quaternion.Euler(8f, 0f, 0f));
        }
    }
}
