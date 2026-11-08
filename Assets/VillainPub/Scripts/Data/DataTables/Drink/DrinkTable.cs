using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "DrinkTable", menuName = "VillainPub/Data/Drink Table")]
    public class DrinkTable : GoogleSheetParser<DrinkData>
    {
        [Header("참조 검증용")]
        [SerializeField] private RecipeTable _recipes;
        [SerializeField] private StringTable _strings;
        [SerializeField] private VillainTable _villains;

        protected override void ValidateRows(List<string> errors)
        {
            if (_recipes == null) errors.Add($"[{name}] 레시피 테이블이 연결되지 않았습니다.");
            if (_strings == null) errors.Add($"[{name}] 문자열 테이블이 연결되지 않았습니다.");
            if (_villains == null) errors.Add($"[{name}] 빌런 테이블이 연결되지 않았습니다.");

            // Drink와 Recipe는 1:1이다. 두 음료가 같은 레시피를 가리키면 판정 결과가 모호해진다.
            var drinkByRecipe = new Dictionary<string, string>();

            foreach (DrinkData row in Rows)
            {
                string context = $"[{name}] '{row.Id}'";
                if (row.Price < 0)
                    errors.Add($"{context}의 price가 음수입니다. ({row.Price})");

                if (string.IsNullOrEmpty(row.RecipeKey))
                    errors.Add($"{context}의 recipeKey가 비어 있습니다.");
                else if (_recipes != null && !_recipes.Contains(row.RecipeKey))
                    errors.Add($"{context}의 레시피 '{row.RecipeKey}'가 레시피 테이블에 없습니다.");
                else if (drinkByRecipe.TryGetValue(row.RecipeKey, out string otherId))
                    errors.Add($"{context}와 '{otherId}'가 같은 레시피 '{row.RecipeKey}'를 가리킵니다.");
                else
                    drinkByRecipe.Add(row.RecipeKey, row.Id);

                // 시드를 넣으면 이 빌런이 된다. 비어 있으면 시드 투입 결과를 정할 수 없다.
                if (string.IsNullOrEmpty(row.VillainKey))
                    errors.Add($"{context}의 villainKey가 비어 있습니다.");
                else if (_villains != null && !_villains.Contains(row.VillainKey))
                    errors.Add($"{context}의 빌런 '{row.VillainKey}'가 빌런 테이블에 없습니다.");

                if (_strings != null)
                {
                    _strings.CheckKey(row.NameStrKey, $"{context} nameStrKey", errors);
                    _strings.CheckKey(row.DescStrKey, $"{context} descStrKey", errors);
                }
            }
        }
    }

    public enum DrinkGroup
    {
        None
    }

    [Serializable]
    public class DrinkData : ISheetRow
    {
        // 시트 변수명 'id'는 필드 'id' 또는 '_id'에 들어간다.
        [SerializeField] private string _id;
        [SerializeField] private int _price;
        [SerializeField] private DrinkGroup _type;
        [SerializeField] private string _nameStrKey;
        [SerializeField] private string _descStrKey;
        [SerializeField] private string _recipeKey;
        [SerializeField] private string _villainKey;
        

        public string Id => _id;
        public int Price => _price;
        public DrinkGroup Type => _type;
        public string NameStrKey => _nameStrKey;
        public string DescStrKey => _descStrKey;
        public string RecipeKey => _recipeKey;
        public string VillainKey => _villainKey;

    }
}
