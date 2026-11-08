using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "VillainTable", menuName = "VillainPub/Data/Villain Table")]
    public class VillainTable : GoogleSheetParser<VillainData>
    {
        [Header("참조 검증용")]
        [SerializeField] private StringTable _strings;

        /// <summary>제조 실패 + 시드로 만들어지는 기본 빌런(isDefault 행). 검증을 통과했다면 정확히 하나다.</summary>
        public VillainData DefaultVillain
        {
            get
            {
                foreach (VillainData row in Rows)
                    if (row.IsDefault) return row;
                return null;
            }
        }

        protected override void ValidateRows(List<string> errors)
        {
            if (_strings == null) errors.Add($"[{name}] 문자열 테이블이 연결되지 않았습니다.");

            int defaultCount = 0;
            foreach (VillainData row in Rows)
            {
                string context = $"[{name}] '{row.Id}'";
                UnitStats.Check(row.BaseHp, row.BaseAtk, context, errors);
                if (row.IsDefault) defaultCount++;

                if (_strings != null)
                {
                    _strings.CheckKey(row.NameStrKey, $"{context} nameStrKey", errors);
                    _strings.CheckKey(row.DescStrKey, $"{context} descStrKey", errors);
                    if (!string.IsNullOrEmpty(row.SkillStrKey))
                        _strings.CheckKey(row.SkillStrKey, $"{context} skillStrKey", errors);
                }
            }

            if (defaultCount != 1)
                errors.Add($"[{name}] isDefault(기본 빌런)인 행은 정확히 1개여야 합니다. (현재 {defaultCount}개)");
        }
    }

    [Serializable]
    public class VillainData : ISheetRow
    {
        [SerializeField] private string _id;
        [SerializeField] private string _nameStrKey;
        [SerializeField] private string _descStrKey;
        [SerializeField] private int _baseHp;
        [SerializeField] private int _baseAtk;
        // 아래 셋과 skillStrKey는 시트에 열만 있고 전투 규칙에서는 아직 쓰지 않는다(스탯 전용 프로토타입).
        [SerializeField] private int _baseDf;
        [SerializeField] private int _baseSpd;
        [SerializeField] private int _baseAtkRange;
        [SerializeField] private string _skillStrKey;
        [SerializeField] private bool _isDefault;

        public string Id => _id;
        public string NameStrKey => _nameStrKey;
        public string DescStrKey => _descStrKey;
        public int BaseHp => _baseHp;
        public int BaseAtk => _baseAtk;
        public int BaseDf => _baseDf;
        public int BaseSpd => _baseSpd;
        public int BaseAtkRange => _baseAtkRange;
        public string SkillStrKey => _skillStrKey;
        public bool IsDefault => _isDefault;
    }

    /// <summary>전투 유닛 테이블(Villain, Hero, Building)이 같이 쓰는 스탯 검증.</summary>
    internal static class UnitStats
    {
        public static void Check(int hp, int atk, string context, List<string> errors)
        {
            if (hp < 1) errors.Add($"{context}의 baseHp는 1 이상이어야 합니다. ({hp})");
            if (atk < 0) errors.Add($"{context}의 baseAtk가 음수입니다. ({atk})");
        }
    }
}
