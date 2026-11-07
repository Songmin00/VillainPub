using System;
using System.Collections.Generic;
using VillainPub.Data;

namespace VillainPub.Bar
{
    /// <summary>
    /// 플레이어가 넣은 재료로 음료를 찾는다. 재료 순서는 상관없다(멀티셋).
    /// 영업을 시작할 때 한 번 만들어 영업 동안 쓴다. Domain Reload가 꺼져 있으므로 static 캐시로 두지 않는다.
    /// </summary>
    public sealed class RecipeMatcher
    {
        private readonly Dictionary<string, DrinkData> _drinkByRecipeKey = new Dictionary<string, DrinkData>();
        private readonly List<string> _problems = new List<string>();

        public RecipeMatcher(DrinkTable drinks, RecipeTable recipes)
        {
            if (drinks == null) throw new ArgumentNullException(nameof(drinks));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));

            foreach (DrinkData drink in drinks.Rows)
            {
                if (!recipes.TryGet(drink.RecipeKey, out RecipeData recipe) || recipe.Ingredients.Count == 0)
                {
                    _problems.Add($"'{drink.Id}'의 레시피 '{drink.RecipeKey}'를 찾을 수 없거나 비어 있어 판정에서 제외합니다.");
                    continue;
                }

                string key = Recipe.ToKey(recipe.Ingredients);
                if (_drinkByRecipeKey.TryGetValue(key, out DrinkData other))
                {
                    _problems.Add($"'{drink.Id}'의 재료 조합이 '{other.Id}'와 같아 판정에서 제외합니다. ({key})");
                    continue;
                }

                _drinkByRecipeKey.Add(key, drink);
            }
        }

        /// <summary>판정할 수 있는 음료 수.</summary>
        public int DrinkCount => _drinkByRecipeKey.Count;

        /// <summary>판정할 수 있는 음료(메뉴판). 손님 주문도 여기서 고른다.</summary>
        public IReadOnlyCollection<DrinkData> Drinks => _drinkByRecipeKey.Values;

        /// <summary>
        /// 판정에서 빠진 음료와 그 이유. 테이블 검증을 통과한 데이터라면 비어 있다.
        /// </summary>
        public IReadOnlyList<string> Problems => _problems;

        /// <summary>
        /// 음료를 만든다. 재료 버튼을 누른 순서대로 ID를 쌓아 넘기면 된다(같은 ID가 여러 번 들어가면 개수로 센다).
        /// 여기서 실패는 "손님 주문과 다름"이 아니라 "어느 레시피에도 없는 조합"이다. 주문 일치 여부는 호출하는 쪽이 판단한다.
        /// 재료를 하나도 넣지 않았으면 시드와 상관없이 Failed다(빈 잔은 내지 않는다).
        /// 기본 빌런이 어떤 유닛인지는 Villain 테이블이 정해지면 호출하는 쪽에서 정한다.
        /// </summary>
        public BrewResult Brew(IEnumerable<string> addedIngredientIds, bool hasVillainSeed)
        {
            List<RecipeEntry> entries = CountIngredients(addedIngredientIds);
            if (entries.Count == 0) return new BrewResult(BrewOutcome.Failed, null);

            if (TryFindDrink(entries, out DrinkData drink))
                return new BrewResult(hasVillainSeed ? BrewOutcome.Villain : BrewOutcome.Sold, drink);

            return new BrewResult(hasVillainSeed ? BrewOutcome.DefaultVillain : BrewOutcome.Failed, null);
        }

        public bool TryFindDrink(IEnumerable<string> addedIngredientIds, out DrinkData drink)
        {
            return TryFindDrink(CountIngredients(addedIngredientIds), out drink);
        }

        private bool TryFindDrink(List<RecipeEntry> entries, out DrinkData drink)
        {
            drink = null;
            return entries.Count > 0 && _drinkByRecipeKey.TryGetValue(Recipe.ToKey(entries), out drink);
        }

        private static List<RecipeEntry> CountIngredients(IEnumerable<string> ids)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (ids == null) return new List<RecipeEntry>();

            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                counts.TryGetValue(id, out int count);
                counts[id] = count + 1;
            }

            var entries = new List<RecipeEntry>(counts.Count);
            foreach (KeyValuePair<string, int> pair in counts)
                entries.Add(new RecipeEntry(pair.Key, pair.Value));
            return entries;
        }
    }

    public enum BrewOutcome
    {
        /// <summary>레시피에 없는 조합, 시드 없음. 만든 음료가 없다.</summary>
        Failed,
        /// <summary>레시피 음료, 시드 없음 → 판매(돈).</summary>
        Sold,
        /// <summary>레시피 음료 + 시드 → Drink.VillainKey의 빌런으로 개조(돈 없음).</summary>
        Villain,
        /// <summary>레시피에 없는 조합 + 시드 → 기본 빌런(잡몹 수준)으로 개조(돈 없음).</summary>
        DefaultVillain,
    }

    /// <summary>
    /// 음료 제조 결과. Drink는 레시피 음료를 만들었을 때(Sold, Villain)만 있다.
    /// </summary>
    public readonly struct BrewResult
    {
        public BrewOutcome Outcome { get; }
        public DrinkData Drink { get; }

        public bool HasDrink => Drink != null;
        public bool IsVillain => Outcome == BrewOutcome.Villain || Outcome == BrewOutcome.DefaultVillain;

        public BrewResult(BrewOutcome outcome, DrinkData drink)
        {
            Outcome = outcome;
            Drink = drink;
        }
    }
}
