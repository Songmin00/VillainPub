using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "HeroTable", menuName = "VillainPub/Data/Hero Table")]
    public class HeroTable : GoogleSheetParser<HeroData>
    {
        [Header("참조 검증용")]
        [SerializeField] private StringTable _strings;

        protected override void ValidateRows(List<string> errors)
        {
            if (_strings == null) errors.Add($"[{name}] 문자열 테이블이 연결되지 않았습니다.");

            foreach (HeroData row in Rows)
            {
                string context = $"[{name}] '{row.Id}'";
                UnitStats.Check(row.BaseHp, row.BaseAtk, context, errors);

                if (_strings != null)
                {
                    _strings.CheckKey(row.NameStrKey, $"{context} nameStrKey", errors);
                    _strings.CheckKey(row.DescStrKey, $"{context} descStrKey", errors);
                }
            }
        }
    }

    [Serializable]
    public class HeroData : ISheetRow
    {
        [SerializeField] private string _id;
        [SerializeField] private string _nameStrKey;
        [SerializeField] private string _descStrKey;
        [SerializeField] private int _baseHp;
        [SerializeField] private int _baseAtk;

        public string Id => _id;
        public string NameStrKey => _nameStrKey;
        public string DescStrKey => _descStrKey;
        public int BaseHp => _baseHp;
        public int BaseAtk => _baseAtk;
    }
}
