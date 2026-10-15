using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.Battle
{
    // 2번 지도 모드 카메라 (BattleMap.md, D-025 직교·D-044 M1)
    // 위에서 내려다보는 직교 시점, 북쪽이 위. 마우스 휠로 3단계(전체·한 면·가까이), A/D/W/S(화살표)로 이동.
    public sealed class BattleMapCamera
    {
        public enum Level { Full, Side, Close }

        const float SideHalfHeight = 80f;     // 한 면: 세로 약 160m (D-071 들판 160m에 맞춤, 예전 130)
        const float CloseHalfHeight = 40f;    // 가까이: 세로 약 80m — 성(40m)과 병력 묶음이 잘 보인다
        const float DotPixels = 7f;           // 화면에서 점 크기(픽셀)

        readonly Camera camera;
        readonly float castleHalf;
        readonly float fieldSize;
        Vector2 center;   // (x, z)

        public Level Current { get; private set; } = Level.Side;
        public float HalfHeight => Current == Level.Full ? fieldSize + 20f : Current == Level.Side ? SideHalfHeight : CloseHalfHeight;

        // 지도 점의 최소 크기(m) — 몇 단계로 보든 화면에서 비슷한 크기
        public float DotWorldSize => HalfHeight * 2f / Mathf.Max(1, Screen.height) * DotPixels;

        public BattleMapCamera(Camera camera, float castleHalf, float fieldSize)
        {
            this.camera = camera;
            this.castleHalf = castleHalf;
            this.fieldSize = fieldSize;
        }

        // 처음 열면 바라보던 성벽 쪽 '한 면' (M1)
        public void Open(int side)
        {
            Current = Level.Side;
            var n = WallGeometry.Outward(side);
            center = new Vector2(n.x, n.z) * (castleHalf + 40f);
            Apply();
        }

        // 전투 준비(D-071·072): 성 가운데를 가까이 — 병력 묶음 옮기기·명령 정하기가 먼저다. 휠로 넓혀 대포 조준
        public void OpenCastle()
        {
            Current = Level.Close;
            center = Vector2.zero;
            Apply();
        }

        public void SetLevel(Level level)
        {
            Current = level;
            Apply();
        }

        public void Update(float unscaledDt)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0f && Current != Level.Close) Current++;
                else if (scroll < 0f && Current != Level.Full) Current--;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // 화살표만 (D-073: Q·W는 마법, 지도는 왼쪽 버튼으로 끌어 옮긴다)
                Vector2 move = Vector2.zero;
                if (keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.downArrowKey.isPressed) move.y -= 1f;
                center += move * HalfHeight * 1.2f * unscaledDt;
            }

            Apply();
        }

        // 왼쪽 버튼으로 끌기 (D-073): 화면에서 움직인 화소만큼 반대로 — 손으로 지도를 잡아 미는 느낌
        public void DragBy(Vector2 screenDelta)
        {
            float metersPerPixel = HalfHeight * 2f / Mathf.Max(1, Screen.height);
            center -= screenDelta * metersPerPixel;
            Apply();
        }

        void Apply()
        {
            float limit = Current == Level.Full ? 0f : fieldSize;
            center = new Vector2(Mathf.Clamp(center.x, -limit, limit), Mathf.Clamp(center.y, -limit, limit));
            camera.orthographic = true;
            camera.orthographicSize = HalfHeight;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 1000f;
            camera.transform.SetPositionAndRotation(new Vector3(center.x, 500f, center.y), Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
