using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 사진기를 보는 세운 그림 (W27, 마당의 무기 걸이·수레·화살 묶음). 좌우로만 돌아 항상 정면을 보인다 (세로로는 눕지 않는다)
    // 프레임마다 새로 만드는 것 없음 — 사진기 한 번 찾아 두고 회전 값만 바꾼다
    public sealed class CourtyardBillboard : MonoBehaviour
    {
        Camera cam;

        void LateUpdate()
        {
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam == null) return;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.LookRotation(forward);   // 판의 앞면은 -z라 사진기 쪽을 본다
        }
    }
}
