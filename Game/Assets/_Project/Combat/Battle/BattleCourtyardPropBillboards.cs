using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 마당 건물 소품(굴뚝·종탑·건초·장작·우물·물통)의 정면 그림을 사진기 쪽으로 돌린다 (W31, Codex K-118)
    // - 소품은 한 메시(그리기 1번)에 사각형으로 모여 있고, 사진기 방향(수평)이 바뀔 때만 이 사각형들의 정점을 다시 쓴다 (매 프레임 일 없음)
    // - 발 아래 가운데가 제자리, 판은 세로로 서서 사진기를 본다 (CourtyardBillboard와 같은 규칙)
    public sealed class BattleCourtyardPropBillboards : MonoBehaviour
    {
        public struct Quad
        {
            public int First;               // 정점 시작 번호 (왼아래 · 왼위 · 오른위 · 오른아래)
            public Vector3 Feet;            // 발 아래 가운데
            public float HalfWidth, Height;
        }

        const float YawEpsilon = 0.25f;     // 도

        Mesh mesh;
        Vector3[] verts;
        Vector3[] normals;
        Quad[] quads;
        Camera cam;
        float lastYaw = float.NaN;

        public int QuadCount => quads != null ? quads.Length : 0;
        public int FirstVertex => quads != null && quads.Length > 0 ? quads[0].First : 0;   // 확인용: 첫 소품 판의 정점 번호

        public void Init(Mesh target, Vector3[] vertexBuffer, Vector3[] normalBuffer, Quad[] list)
        {
            mesh = target; verts = vertexBuffer; normals = normalBuffer; quads = list;
        }

        void LateUpdate()
        {
            if (quads == null || quads.Length == 0) return;
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam == null) return;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (!float.IsNaN(lastYaw) && Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) < YawEpsilon) return;
            lastYaw = yaw;
            Face(forward);
        }

        // 판 앞면(법선)이 수평 보는 방향의 반대(= 사진기)를 향하게 정점을 다시 쓴다
        public void Face(Vector3 cameraForward)
        {
            cameraForward.y = 0f;
            if (cameraForward.sqrMagnitude < 1e-6f || mesh == null) return;
            cameraForward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, cameraForward);   // 사진기의 오른쪽
            Vector3 normal = -cameraForward;
            foreach (Quad q in quads)
            {
                Vector3 bl = q.Feet - right * q.HalfWidth, br = q.Feet + right * q.HalfWidth;
                Vector3 up = Vector3.up * q.Height;
                verts[q.First] = bl; verts[q.First + 1] = bl + up; verts[q.First + 2] = br + up; verts[q.First + 3] = br;
                for (int k = 0; k < 4; k++) normals[q.First + k] = normal;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
        }
    }
}
