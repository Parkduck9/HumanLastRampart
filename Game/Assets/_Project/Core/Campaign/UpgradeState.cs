using System;

namespace HumanLastRampart.Core.Campaign
{
    // 강화·상점·용병 진행 데이터 (C-004 v1.6 초안, K-058, D-079)

    // 가진 아이템 한 줄. 영웅 아이템은 한 개씩(Count 1), 소모품은 쌓아 둔 수, 방패 난간은 성벽마다 한 줄
    [Serializable]
    public sealed class OwnedItem
    {
        public string ItemId;      // ItemDef
        public int Count = 1;
        public string EquippedBy;  // 영웅 아이템: 끼운 영웅 InstanceId (가방에 있으면 null)
        public string WallId;      // 성벽 아이템(방패 난간): 붙인 성벽 (WallIds)

        public OwnedItem Clone() => (OwnedItem)MemberwiseClone();
    }

    // 병종 훈련 단계 (0 = 훈련 안 함)
    [Serializable]
    public sealed class TrainingState
    {
        public string UnitDefId;
        public int Level;

        public TrainingState Clone() => (TrainingState)MemberwiseClone();
    }

    // 용병 계약 한 줄 = 한 성벽의 같은 종류 용병 무리. 전투에는 분대처럼 서고 ContractId가 분대 ID 노릇을 한다
    [Serializable]
    public sealed class MercenaryContract
    {
        public string ContractId;      // "merc_" 고유 ID
        public string MercenaryDefId;  // MercenaryDef
        public string WallId;          // 서는 성벽
        public int Count;

        public MercenaryContract Clone() => (MercenaryContract)MemberwiseClone();
    }
}
