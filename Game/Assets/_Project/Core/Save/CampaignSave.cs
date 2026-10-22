using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using UnityEngine;

namespace HumanLastRampart.Core.Save
{
    // 진행 저장 슬롯 1개 (K-062, C-004 v1.5 초안): 정비 화면에 들어올 때 App이 자동 저장, 메인 '이어하기'가 불러온다.
    // - 진행 데이터(CampaignState)를 필드 목록을 손으로 적지 않고 JsonUtility로 통째로 저장한다 → 필드가 늘어도(K-058 등) 따라간다.
    //   [Serializable] 공개 필드만 저장된다(사전·속성·null 허용 값 형식은 저장되지 않으니 진행 데이터에 쓰지 않는다)
    // - JsonUtility는 null인 객체 필드를 빈 객체로 저장한다 → 저장할 때 null이던 맨 위 필드 이름을 적어 두고 불러올 때 다시 null로 둔다
    // - 판 번호(FormatVersion)를 넣는다. 불러올 때는 새 CampaignState(필드 기본값)에 덮어쓰므로, 예전 파일에 없는 새 필드는 기본값.
    //   이 코드보다 새 판 번호의 파일은 읽지 않는다
    // - 쓰기는 임시 파일에 쓴 뒤 바꿔 끼운다(쓰다 끊겨도 예전 저장이 남게)
    public static class CampaignSave
    {
        public const int FormatVersion = 1;
        public const string ContractVersion = "C-004 v1.5 초안";
        const string FileName = "slot1.json";

        // 저장 폴더 — 기본은 Application.persistentDataPath/save. 시험은 임시 폴더로 바꾼다
        static string folder;
        public static string Folder
        {
            get => folder ?? (folder = Path.Combine(Application.persistentDataPath, "save"));
            set => folder = value;
        }

        public static string SlotPath => Path.Combine(Folder, FileName);
        public static bool HasSave => File.Exists(SlotPath);

        [Serializable]
        sealed class SaveFile
        {
            public int FormatVersion;
            public string Contract;
            public string SavedAtUtc;
            public int BattleNumber;          // 미리 보기용 (메인에서 '몇 번째 전투 앞' 표시)
            public int FortressNumber;        // W8: 미리 보기용 요새·밤 (예전 파일에는 없어 0)
            public int NightNumber;
            public Difficulty Difficulty;
            public ScreenId ResumeScreen;     // 이어할 화면 — 1판은 늘 정비(Prep)
            public List<string> NullFields = new List<string>();
            public string Campaign;           // CampaignState JSON (따로 넣어 불러올 때 기본값 객체에 덮어쓴다)
        }

        public struct Summary
        {
            public int BattleNumber;
            public int FortressNumber;   // W8: 0이면 예전 저장 (요새·밤 정보 없음)
            public int NightNumber;
            public Difficulty Difficulty;
            public string SavedAtUtc;
        }

        public static bool Save(CampaignState campaign, ScreenId resume, out string error)
        {
            error = null;
            if (campaign == null) { error = "진행 데이터가 없습니다"; return false; }
            try
            {
                var file = new SaveFile
                {
                    FormatVersion = FormatVersion,
                    Contract = ContractVersion,
                    SavedAtUtc = DateTime.UtcNow.ToString("o"),
                    BattleNumber = campaign.BattleNumber,
                    FortressNumber = campaign.FortressNumber,
                    NightNumber = campaign.NightNumber,
                    Difficulty = campaign.Difficulty,
                    ResumeScreen = resume,
                    Campaign = JsonUtility.ToJson(campaign),
                };
                foreach (FieldInfo field in NullableFields())
                    if (field.GetValue(campaign) == null) file.NullFields.Add(field.Name);

                Directory.CreateDirectory(Folder);
                string temp = SlotPath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(file, true), new System.Text.UTF8Encoding(false));
                if (File.Exists(SlotPath)) File.Delete(SlotPath);
                File.Move(temp, SlotPath);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryLoad(out CampaignState campaign, out ScreenId resume, out string error)
        {
            campaign = null;
            resume = ScreenId.Prep;
            if (!TryReadFile(out SaveFile file, out error)) return false;
            try
            {
                var loaded = new CampaignState();   // 필드 기본값 위에 덮어쓴다 — 예전 파일에 없는 새 필드는 기본값
                JsonUtility.FromJsonOverwrite(file.Campaign, loaded);
                foreach (FieldInfo field in NullableFields())
                    if (file.NullFields.Contains(field.Name)) field.SetValue(loaded, null);
                campaign = loaded;
                resume = file.ResumeScreen;
                return true;
            }
            catch (Exception exception)
            {
                error = "진행 데이터를 읽지 못했습니다: " + exception.Message;
                return false;
            }
        }

        public static bool TryPeek(out Summary summary)
        {
            summary = default;
            if (!TryReadFile(out SaveFile file, out _)) return false;
            summary = new Summary { BattleNumber = file.BattleNumber, FortressNumber = file.FortressNumber, NightNumber = file.NightNumber, Difficulty = file.Difficulty, SavedAtUtc = file.SavedAtUtc };
            return true;
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(SlotPath)) File.Delete(SlotPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[저장] 저장 파일을 지우지 못했습니다: " + exception.Message);
            }
        }

        static bool TryReadFile(out SaveFile file, out string error)
        {
            file = null;
            error = null;
            try
            {
                if (!File.Exists(SlotPath)) { error = "저장된 진행이 없습니다"; return false; }
                file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SlotPath));
            }
            catch (Exception exception)
            {
                error = "저장 파일을 읽지 못했습니다: " + exception.Message;
                return false;
            }

            if (file == null || string.IsNullOrEmpty(file.Campaign)) { error = "저장 파일이 비어 있거나 깨졌습니다"; return false; }
            if (file.FormatVersion > FormatVersion) { error = $"더 새 판의 저장 파일입니다 (파일 {file.FormatVersion}, 읽을 수 있는 판 {FormatVersion})"; return false; }
            return true;
        }

        // 맨 위의 객체 필드(문자열·목록 제외) — JsonUtility가 null을 빈 객체로 바꾸는 자리
        static IEnumerable<FieldInfo> NullableFields()
        {
            foreach (FieldInfo field in typeof(CampaignState).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Type type = field.FieldType;
                if (!type.IsClass || type == typeof(string)) continue;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) continue;
                yield return field;
            }
        }
    }
}
