namespace HumanLastRampart.Combat.Battle
{
    // 대포 그림 효과용 읽기 (K-036) — 그리기만 쓰고 전투 계산은 바꾸지 않는다. 작업이 끝난 뒤(sim.Complete 후)에만 부른다
    // - 쏜 순간: 대포 병사의 마지막 사격 시각(sFireTime, SoldierFireJob이 쓴다)으로 잰다.
    //   예전(K-031)에는 대포알 투사체가 새로 생긴 칸을 찾아 짐작했는데, 투사체 그림 연결(K-032) 뒤 실제 플레이에서 반동·연기가 보이지 않았다
    public sealed partial class BattleSim
    {
        // 대포 index가 마지막으로 쏜 뒤 지난 전투 시간(초). 아직 안 쐈으면 큰 값
        public float CannonFireAge(int index)
        {
            if (index < 0 || index >= cannons.Count || !sFireTime.IsCreated) return 1000f;
            int soldier = cannons[index].soldier;
            return soldier >= 0 && soldier < sFireTime.Length ? Elapsed - sFireTime[soldier] : 1000f;
        }

        // 무기 번호의 범위 피해 반경(m). 범위 무기가 아니면 0 (대포알 떨어진 자리 폭발 크기)
        public float WeaponAreaRadius(int weapon) => weapon >= 0 && weapon < weapons.Length ? weapons[weapon].AreaRadius : 0f;
    }
}
