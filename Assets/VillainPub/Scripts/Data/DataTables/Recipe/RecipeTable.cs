using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "RecipeTable", menuName = "VillainPub/Data/Recipe Table")]
    public class RecipeTable : GoogleSheetParser<RecipeData>
    {
        [Header("참조 검증용")]
        [SerializeField] private IngredientTable _ingredients;

        protected override void ValidateRows(List<string> errors)
        {
            if (_ingredients == null) errors.Add($"[{name}] 재료 테이블이 연결되지 않았습니다.");

            // 같은 재료 조합이 두 레시피에 있으면 레시피 판정이 어느 쪽인지 정할 수 없다.
            var recipeByKey = new Dictionary<string, string>();

            foreach (RecipeData row in Rows)
            {
                string context = $"[{name}] '{row.Id}'";
                if (!Recipe.TryParse(row.IngredientKey, out List<RecipeEntry> entries, out string parseError))
                {
                    errors.Add($"{context} ingredientKey: {parseError}");
                    continue;
                }

                if (_ingredients != null)
                {
                    foreach (RecipeEntry entry in entries)
                    {
                        if (!_ingredients.Contains(entry.IngredientId))
                            errors.Add($"{context}의 재료 '{entry.IngredientId}'가 재료 테이블에 없습니다.");
                    }
                }

                string key = Recipe.ToKey(entries);
                if (recipeByKey.TryGetValue(key, out string otherId))
                    errors.Add($"{context}와 '{otherId}'의 재료 조합이 같습니다. ({key})");
                else
                    recipeByKey.Add(key, row.Id);
            }
        }
    }

    /// <summary>
    /// 음료 하나의 제조 정보. Drink와 1:1이며, 추후 잔·제조 기법 등 제조 관련 열을 여기에 추가한다.
    /// </summary>
    [Serializable]
    public class RecipeData : ISheetRow
    {
        [SerializeField] private string _id;
        [SerializeField] private string _ingredientKey;

        // 재료 문자열을 처음 쓸 때 한 번만 해석한다. 동기화하면 행 객체가 새로 만들어지므로 캐시도 함께 바뀐다.
        [NonSerialized] private List<RecipeEntry> _ingredients;

        public string Id => _id;
        public string IngredientKey => _ingredientKey;

        /// <summary>
        /// 해석된 재료 목록. 형식이 잘못됐으면 빈 목록이다(테이블 검증에서 에러로 잡힌다).
        /// </summary>
        public IReadOnlyList<RecipeEntry> Ingredients
        {
            get
            {
                if (_ingredients == null)
                    _ingredients = Recipe.TryParse(_ingredientKey, out List<RecipeEntry> entries, out _) ? entries : new List<RecipeEntry>();
                return _ingredients;
            }
        }
    }

    /// <summary>
    /// 레시피의 재료 한 종류와 개수.
    /// </summary>
    public readonly struct RecipeEntry
    {
        public string IngredientId { get; }
        public int Count { get; }

        public RecipeEntry(string ingredientId, int count)
        {
            IngredientId = ingredientId;
            Count = count;
        }

        public override string ToString() => $"{IngredientId}:{Count}";
    }

    /// <summary>
    /// 레시피 재료 문자열("Id_Gin:2|Id_Tonic|Id_Lime")을 해석한다. 재료 순서는 상관없다(멀티셋).
    /// </summary>
    public static class Recipe
    {
        private const char EntrySeparator = '|';
        private const char CountSeparator = ':';
        private const int DefaultCount = 1;

        public static bool TryParse(string text, out List<RecipeEntry> entries, out string error)
        {
            entries = new List<RecipeEntry>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "레시피가 비어 있습니다.";
                return false;
            }

            var seen = new HashSet<string>();
            foreach (string rawPart in text.Split(EntrySeparator))
            {
                string part = rawPart.Trim();
                if (part.Length == 0) continue;

                string[] pieces = part.Split(CountSeparator);
                string id = pieces[0].Trim();
                int count = DefaultCount;

                if (id.Length == 0 || pieces.Length > 2)
                {
                    error = $"'{part}'의 형식이 잘못됐습니다. (재료ID 또는 재료ID:개수)";
                    return false;
                }
                if (pieces.Length == 2 && (!int.TryParse(pieces[1].Trim(), out count) || count < 1))
                {
                    error = $"'{part}'의 개수는 1 이상의 정수여야 합니다.";
                    return false;
                }
                // 같은 재료를 두 번 적는 건 대개 오타이므로 합치지 않고 에러로 알린다.
                if (!seen.Add(id))
                {
                    error = $"재료 '{id}'가 두 번 적혀 있습니다. 개수로 합쳐 주세요. (예: {id}:2)";
                    return false;
                }

                entries.Add(new RecipeEntry(id, count));
            }

            if (entries.Count == 0)
            {
                error = "레시피가 비어 있습니다.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 재료 순서와 무관하게 같은 조합이면 같은 문자열을 돌려준다. 레시피 판정과 중복 검사에 쓴다.
        /// </summary>
        public static string ToKey(IEnumerable<RecipeEntry> entries)
        {
            return string.Join(EntrySeparator.ToString(),
                entries.OrderBy(e => e.IngredientId, StringComparer.Ordinal).Select(e => e.ToString()));
        }
    }
}
