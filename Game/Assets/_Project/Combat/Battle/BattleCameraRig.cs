using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanLastRampart.Combat.Battle
{
    // 1번 화면 카메라 (D-025: 평소 원근 낮은 카메라). 임시 조작 — 모서리 돌기·주인공 이동은 CY1-C3 설계 후
    // A/D(←/→) 성벽 따라 이동, Q/E 왼쪽/오른쪽 성벽 가운데까지 자동 달리기(D-046), Tab 전체 보기, C 카메라 방식 바꾸기
    // 기본은 자동(D-040): 멈춰 있으면 A(가깝게), 움직이면 B(조금 높게)로 부드럽게 바뀐다
    public sealed class BattleCameraRig
    {
        const float PanSpeed = BattleSim.HeroSpeed;   // 카메라 = 주인공 위치 대용. 주인공 달리기 속도로 (D-046)

        // 성벽 위 병사 뒤에서 바깥을 보는 카메라 후보 (CY2-C1). 뒤로 거리·병사 머리 위 높이·내려다보는 각도
        // 병사를 작게 그리므로(D-046) 거리·높이도 같은 비율로 줄였다 (원래 A 7·2.6 / B 12·6 / C 18·11)
        public struct ViewPreset
        {
            public string Name;
            public float Back;
            public float Up;
            public float Pitch;
        }

        public static readonly ViewPreset[] Presets =
        {
            new ViewPreset { Name = "A 낮게 (바퀴 1)", Back = 7f * BattleSim.WallFigureScale, Up = 2.6f * BattleSim.WallFigureScale, Pitch = 8f },
            new ViewPreset { Name = "B 조금 높게", Back = 12f * BattleSim.WallFigureScale, Up = 6f * BattleSim.WallFigureScale, Pitch = 16f },
            new ViewPreset { Name = "C 높게", Back = 18f * BattleSim.WallFigureScale, Up = 11f * BattleSim.WallFigureScale, Pitch = 25f },
        };

        public const float NearClip = 0.05f;

        // Q/E: 옆 성벽 가운데까지 자동으로 달린다 (D-046). A/D를 누르면 멈춘다
        int runDir;          // -1 왼쪽(Q), +1 오른쪽(E), 0 없음
        bool runTurned;      // 모서리를 돌았는가 (돌고 나면 가운데로)
        public bool AutoRunning => runDir != 0;

        const float BlendSeconds = 0.3f;       // A↔B 바뀌는 시간
        const float ReturnDelaySeconds = 0.6f;  // 멈춘 뒤 A로 돌아가기까지 기다림 (짧게 손을 뗐다 눌러도 출렁이지 않게)

        // -1 = 자동(A↔B), 0~2 = 후보 고정 (비교용)
        public int PresetIndex { get; private set; } = -1;
        public string PresetName => PresetIndex < 0 ? "자동 A↔B" : Presets[PresetIndex].Name;
        public float MoveBlend => moveBlend;   // 0 = A, 1 = B

        float moveBlend;
        float sinceMoved = 999f;

        readonly Camera camera;
        readonly float castleHalf;
        readonly float wallHeight;
        float along;
        bool overview;

        public int Side { get; private set; }
        public float Along => along;   // 지금 성벽에서 가운데 기준 좌우 위치 (주인공 위치 대용)
        public bool Overview => overview;

        public BattleCameraRig(Camera camera, float castleHalf, float wallHeight)
        {
            this.camera = camera;
            this.castleHalf = castleHalf;
            this.wallHeight = wallHeight;
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = NearClip;
            camera.farClipPlane = 1200f;
            Apply();
        }

        public void Update(float unscaledDt)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                float alongBefore = along;
                int sideBeforeRun = Side;
                if (runDir != 0) AutoRun(unscaledDt);
                Step(!Mathf.Approximately(alongBefore, along) || sideBeforeRun != Side, unscaledDt);
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                overview = !overview;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                PresetIndex = PresetIndex + 1 >= Presets.Length ? -1 : PresetIndex + 1;   // 자동 → A → B → C → 자동
            }

            // 성 안에서 바깥을 볼 때 왼쪽 벽 = 시계 반대 방향 (북 → 서)
            if (keyboard.qKey.wasPressedThisFrame) RunToNextWall(-1);
            if (keyboard.eKey.wasPressedThisFrame) RunToNextWall(1);

            float pan = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan += 1f;
            if (pan != 0f) runDir = 0;
            float before = along;
            int sideBefore = Side;
            if (runDir != 0) AutoRun(unscaledDt);
            else along = Mathf.Clamp(along + pan * PanSpeed * unscaledDt, -castleHalf, castleHalf);
            bool moved = !Mathf.Approximately(before, along) || sideBefore != Side;

            Step(moved, unscaledDt);
        }

        // dir -1 = 왼쪽 성벽(Q), +1 = 오른쪽 성벽(E). 달리는 중에 다시 누르면 지금 자리에서 그 방향 다음 성벽으로
        public void RunToNextWall(int dir)
        {
            runDir = dir < 0 ? -1 : 1;
            runTurned = false;
            overview = false;
        }

        void AutoRun(float dt)
        {
            float step = PanSpeed * dt;
            if (!runTurned)
            {
                float corner = runDir * castleHalf;
                along = Mathf.MoveTowards(along, corner, step);
                if (!Mathf.Approximately(along, corner)) return;
                // 모서리: 옆 성벽으로 넘어가 그 벽의 반대쪽 끝에서 시작 (오른쪽 벽이면 그 벽의 왼쪽 끝)
                Side = (Side + (runDir > 0 ? 1 : 3)) % 4;
                along = -corner;
                runTurned = true;
                return;
            }

            along = Mathf.MoveTowards(along, 0f, step);
            if (Mathf.Approximately(along, 0f)) runDir = 0;
        }

        // 움직였는지 알려 주고 카메라를 한 걸음 옮긴다 (나중에 주인공 이동이 생기면 그 이동으로 부른다)
        public void Step(bool moved, float unscaledDt)
        {
            sinceMoved = moved ? 0f : sinceMoved + unscaledDt;
            float target = sinceMoved < ReturnDelaySeconds ? 1f : 0f;
            moveBlend = Mathf.MoveTowards(moveBlend, target, unscaledDt / BlendSeconds);
            Apply();
        }

        public void SetPreset(int index)
        {
            PresetIndex = Mathf.Clamp(index, -1, Presets.Length - 1);
            Apply();
        }

        public void ShowSide(int side)
        {
            Side = side;
            along = 0f;
            overview = false;
            runDir = 0;
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

            // 성벽 위 병사들 뒤에서 바깥 들판을 본다. 자동이면 A와 B 사이를 부드럽게 섞는다
            ViewPreset p = PresetIndex >= 0 ? Presets[PresetIndex] : Blend(Presets[0], Presets[1], Mathf.SmoothStep(0f, 1f, moveBlend));
            Vector3 position = outward * (castleHalf - p.Back) + right * along + Vector3.up * (wallHeight + p.Up);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(outward) * Quaternion.Euler(p.Pitch, 0f, 0f));
        }

        static ViewPreset Blend(ViewPreset a, ViewPreset b, float t) => new ViewPreset
        {
            Name = a.Name,
            Back = Mathf.Lerp(a.Back, b.Back, t),
            Up = Mathf.Lerp(a.Up, b.Up, t),
            Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t),
        };
    }
}
