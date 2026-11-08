using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "BuildingTable", menuName = "VillainPub/Data/Building Table")]
    public class BuildingTable : GoogleSheetParser<BuildingData>
    {
        [Header("참조 검증용")]
        [SerializeField] private StringTable _strings;

        protected override void ValidateRows(List<string> errors)
        {
            if (_strings == null) errors.Add($"[{name}] 문자열 테이블이 연결되지 않았습니다.");

            foreach (BuildingData row in Rows)
            {
                string context = $"[{name}] '{row.Id}'";
                UnitStats.Check(row.BaseHp, 0, context, errors);
                if (row.Reward < 0) errors.Add($"{context}의 reward가 음수입니다. ({row.Reward})");

                if (_strings != null)
                {
                    _strings.CheckKey(row.NameStrKey, $"{context} nameStrKey", errors);
                    _strings.CheckKey(row.DescStrKey, $"{context} descStrKey", errors);
                }
            }
        }
    }

    /// <summary>건물은 공격하지 않는다(공격력 0). 부수면 reward만큼 돈을 받는다(져도 받음).</summary>
    [Serializable]
    public class BuildingData : ISheetRow
    {
        [SerializeField] private string _id;
        [SerializeField] private string _nameStrKey;
        [SerializeField] private string _descStrKey;
        [SerializeField] private int _baseHp;
        [SerializeField] private int _reward;

        public string Id => _id;
        public string NameStrKey => _nameStrKey;
        public string DescStrKey => _descStrKey;
        public int BaseHp => _baseHp;
        public int Reward => _reward;
    }
}
