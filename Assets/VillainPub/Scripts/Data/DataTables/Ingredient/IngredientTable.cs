using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "IngredientTable", menuName = "VillainPub/Data/Ingredient Table")]
    public class IngredientTable : GoogleSheetParser<IngredientData>
    {
        [Header("참조 검증용")]
        [SerializeField] private StringTable _strings;

        protected override void ValidateRows(List<string> errors)
        {
            if (_strings == null) errors.Add($"[{name}] 문자열 테이블이 연결되지 않았습니다.");

            foreach (IngredientData row in Rows)
            {
                string context = $"[{name}] '{row.Id}'";
                if (row.Type == IngredientGroup.None)
                    errors.Add($"{context}의 type이 지정되지 않았습니다.");
                if (row.Price < 0)
                    errors.Add($"{context}의 price가 음수입니다. ({row.Price})");

                if (_strings != null)
                {
                    _strings.CheckKey(row.NameStrKey, $"{context} nameStrKey", errors);
                    _strings.CheckKey(row.DescStrKey, $"{context} descStrKey", errors);
                }
            }
        }
    }

    /// <summary>
    /// 칵테일에서 재료가 맡는 역할. 시트에는 이름으로 적고 에셋에는 숫자로 저장되므로 새 값은 끝에만 추가한다.
    /// </summary>
    public enum IngredientGroup
    {
        None,
        Spirit,     // 베이스 술
        Liqueur,    // 리큐어·베르무트 등 맛을 바꾸는 술
        Citrus,     // 산미
        Sweetener,  // 단맛
        Mixer,      // 탄산 등 희석 음료
    }

    [Serializable]
    public class IngredientData : ISheetRow
    {
        [SerializeField] private string _id;
        [SerializeField] private int _price;
        [SerializeField] private IngredientGroup _type;
        [SerializeField] private string _nameStrKey;
        [SerializeField] private string _descStrKey;

        public string Id => _id;
        public int Price => _price;
        public IngredientGroup Type => _type;
        public string NameStrKey => _nameStrKey;
        public string DescStrKey => _descStrKey;
    }
}
