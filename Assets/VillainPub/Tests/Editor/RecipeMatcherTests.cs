using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VillainPub.Bar;
using VillainPub.Data;

namespace VillainPub.Tests
{
    // 실제 시트(Recipe 탭)와 같은 레시피로 판정 규칙을 확인한다.
    public class RecipeMatcherTests
    {
        private DrinkTable _drinks;
        private RecipeTable _recipes;

        [SetUp]
        public void SetUp()
        {
            _drinks = ScriptableObject.CreateInstance<DrinkTable>();
            _recipes = ScriptableObject.CreateInstance<RecipeTable>();

            SetRecipes(
                ("Recipe_GinTonic", "Id_Gin:1|Id_Tonic:2|Id_Lime"),
                ("Recipe_Daiquiri", "Id_Rum:2|Id_Lime|Id_Syrup"),
                ("Recipe_Gimlet", "Id_Gin:2|Id_Lime|Id_Syrup"),
                ("Recipe_WhiskeySour", "Id_Whiskey:2|Id_Lemon|Id_Syrup"),
                ("Recipe_GinFizz", "Id_Gin:2|Id_Lemon|Id_Syrup|Id_Soda:2"),
                ("Recipe_Martini", "Id_Gin:2|Id_DryVermouth"));

            SetDrinks(
                ("Id_GinTonic", "Recipe_GinTonic"),
                ("Id_Daiquiri", "Recipe_Daiquiri"),
                ("Id_Gimlet", "Recipe_Gimlet"),
                ("Id_WhiskeySour", "Recipe_WhiskeySour"),
                ("Id_GinFizz", "Recipe_GinFizz"),
                ("Id_Martini", "Recipe_Martini"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_drinks);
            Object.DestroyImmediate(_recipes);
        }

        [Test]
        public void 모든_음료가_판정_대상에_들어간다()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);

            Assert.AreEqual(6, matcher.DrinkCount);
            CollectionAssert.IsEmpty(matcher.Problems);
        }

        [Test]
        public void 재료_순서가_달라도_같은_음료다()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);

            AssertDrink(matcher, "Id_GinTonic", "Id_Gin", "Id_Tonic", "Id_Tonic", "Id_Lime");
            AssertDrink(matcher, "Id_GinTonic", "Id_Lime", "Id_Tonic", "Id_Gin", "Id_Tonic");
        }

        [Test]
        public void 베이스만_다른_조합을_구분한다()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);

            AssertDrink(matcher, "Id_Daiquiri", "Id_Rum", "Id_Rum", "Id_Lime", "Id_Syrup");
            AssertDrink(matcher, "Id_Gimlet", "Id_Gin", "Id_Gin", "Id_Lime", "Id_Syrup");
        }

        [Test]
        public void 재료가_더_많거나_적으면_실패한다()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);

            AssertNoDrink(matcher, "Id_Whiskey", "Id_Whiskey", "Id_Lemon", "Id_Syrup", "Id_Soda");
            AssertNoDrink(matcher, "Id_Gin", "Id_Gin");
        }

        [Test]
        public void 개수가_다르면_실패한다()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);

            AssertNoDrink(matcher, "Id_Gin", "Id_DryVermouth");
            AssertNoDrink(matcher, "Id_Gin", "Id_Tonic", "Id_Lime");
        }

        [Test]
        public void 아무것도_넣지_않으면_시드와_상관없이_실패한다()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);

            Assert.AreEqual(BrewOutcome.Failed, matcher.Brew(new string[0], false).Outcome);
            Assert.AreEqual(BrewOutcome.Failed, matcher.Brew(new string[0], true).Outcome);
            Assert.AreEqual(BrewOutcome.Failed, matcher.Brew(null, true).Outcome);
        }

        [Test]
        public void 레시피_음료에_시드가_없으면_판매_있으면_해당_빌런()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);
            string[] martini = { "Id_Gin", "Id_Gin", "Id_DryVermouth" };

            BrewResult sold = matcher.Brew(martini, hasVillainSeed: false);
            BrewResult villain = matcher.Brew(martini, hasVillainSeed: true);

            Assert.AreEqual(BrewOutcome.Sold, sold.Outcome);
            Assert.IsFalse(sold.IsVillain);
            Assert.AreEqual("Id_Martini", sold.Drink.Id);

            Assert.AreEqual(BrewOutcome.Villain, villain.Outcome);
            Assert.IsTrue(villain.IsVillain);
            Assert.AreEqual("Id_Martini", villain.Drink.Id);
        }

        [Test]
        public void 레시피에_없는_조합에_시드가_없으면_실패_있으면_기본_빌런()
        {
            var matcher = new RecipeMatcher(_drinks, _recipes);
            string[] unknown = { "Id_Gin" };

            BrewResult failed = matcher.Brew(unknown, hasVillainSeed: false);
            BrewResult defaultVillain = matcher.Brew(unknown, hasVillainSeed: true);

            Assert.AreEqual(BrewOutcome.Failed, failed.Outcome);
            Assert.IsFalse(failed.HasDrink);
            Assert.IsFalse(failed.IsVillain);

            Assert.AreEqual(BrewOutcome.DefaultVillain, defaultVillain.Outcome);
            Assert.IsFalse(defaultVillain.HasDrink);
            Assert.IsTrue(defaultVillain.IsVillain);
        }

        [Test]
        public void 레시피를_찾을_수_없는_음료는_제외하고_나머지는_판정한다()
        {
            SetDrinks(
                ("Id_GinTonic", "Recipe_GinTonic"),
                ("Id_Broken", "Recipe_Missing"));

            var matcher = new RecipeMatcher(_drinks, _recipes);

            Assert.AreEqual(1, matcher.DrinkCount);
            Assert.AreEqual(1, matcher.Problems.Count);
            AssertDrink(matcher, "Id_GinTonic", "Id_Gin", "Id_Tonic", "Id_Tonic", "Id_Lime");
        }

        [Test]
        public void 같은_재료_조합의_음료는_먼저_나온_것만_판정한다()
        {
            SetRecipes(
                ("Recipe_A", "Id_Gin:1|Id_Tonic:2"),
                ("Recipe_B", "Id_Tonic:2|Id_Gin"));
            SetDrinks(
                ("Id_A", "Recipe_A"),
                ("Id_B", "Recipe_B"));

            var matcher = new RecipeMatcher(_drinks, _recipes);

            Assert.AreEqual(1, matcher.DrinkCount);
            Assert.AreEqual(1, matcher.Problems.Count);
            AssertDrink(matcher, "Id_A", "Id_Gin", "Id_Tonic", "Id_Tonic");
        }

        private static void AssertDrink(RecipeMatcher matcher, string expectedDrinkId, params string[] ingredients)
        {
            BrewResult result = matcher.Brew(ingredients, false);
            Assert.AreEqual(BrewOutcome.Sold, result.Outcome, $"[{string.Join(", ", ingredients)}] → 음료를 찾지 못함");
            Assert.AreEqual(expectedDrinkId, result.Drink.Id);
        }

        private static void AssertNoDrink(RecipeMatcher matcher, params string[] ingredients)
        {
            BrewResult result = matcher.Brew(ingredients, false);
            Assert.AreEqual(BrewOutcome.Failed, result.Outcome, $"[{string.Join(", ", ingredients)}] → '{result.Drink?.Id}'로 판정됨");
        }

        // 행 클래스 필드는 private이라 시트 동기화와 같은 직렬화 경로(JsonUtility)로 채운다.
        private void SetRecipes(params (string id, string ingredientKey)[] rows)
        {
            _recipes.ReplaceRows(rows.Select(r =>
                (object)JsonUtility.FromJson<RecipeData>(JsonUtility.ToJson(new RecipeRowJson { _id = r.id, _ingredientKey = r.ingredientKey }))));
        }

        private void SetDrinks(params (string id, string recipeKey)[] rows)
        {
            _drinks.ReplaceRows(rows.Select(r =>
                (object)JsonUtility.FromJson<DrinkData>(JsonUtility.ToJson(new DrinkRowJson { _id = r.id, _recipeKey = r.recipeKey }))));
        }

        // JsonUtility용 필드 이름은 행 클래스의 직렬화 필드 이름과 같아야 한다.
        [System.Serializable]
        private class RecipeRowJson
        {
            public string _id;
            public string _ingredientKey;
        }

        [System.Serializable]
        private class DrinkRowJson
        {
            public string _id;
            public string _recipeKey;
        }
    }
}
