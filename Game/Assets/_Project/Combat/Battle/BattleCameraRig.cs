using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.Battle
{
    // 1번 화면 카메라 (D-025: 평소 원근 낮은 카메라). 임시 조작 — 모서리 돌기·주인공 이동은 CY1-C3 설계 후
    // A/D(←/→) 성벽 따라 이동, Q/E 왼쪽/오른쪽 성벽으로, Tab 전체 보기, C 카메라 높이 후보 바꾸기 (CY2-C1 비교용)
    public sealed class BattleCameraRig
    {
        const float PanSpeed = 25f;

        // 성벽 위 병사 뒤에서 바깥을 보는 카메라 후보 (CY2-C1). 뒤로 거리·병사 머리 위 높이·내려다보는 각도
        public struct ViewPreset
        {
            public string Name;
            public float Back;
            public float Up;
            public float Pitch;
        }

        public static readonly ViewPreset[] Presets =
        {
            new ViewPreset { Name = "A 낮게 (바퀴 1)", Back = 7f, Up = 2.6f, Pitch = 8f },
            new ViewPreset { Name = "B 조금 높게", Back = 12f, Up = 6f, Pitch = 16f },
            new ViewPreset { Name = "C 높게", Back = 18f, Up = 11f, Pitch = 25f },
        };

        public int PresetIndex { get; private set; } = 1;
        public string PresetName => Presets[PresetIndex].Name;

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

            if (keyboard.cKey.wasPressedThisFrame)
            {
                PresetIndex = (PresetIndex + 1) % Presets.Length;
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

        public void SetPreset(int index)
        {
            PresetIndex = Mathf.Clamp(index, 0, Presets.Length - 1);
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

            // 성벽 위 병사들 뒤에서 바깥 들판을 본다 (후보마다 거리·높이·각도가 다르다)
            ViewPreset p = Presets[PresetIndex];
            Vector3 position = outward * (castleHalf - p.Back) + right * along + Vector3.up * (wallHeight + p.Up);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(outward) * Quaternion.Euler(p.Pitch, 0f, 0f));
        }
    }
}
