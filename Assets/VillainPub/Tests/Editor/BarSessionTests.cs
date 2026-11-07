using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VillainPub.Bar;
using VillainPub.Data;

namespace VillainPub.Tests
{
    public class BarSessionTests
    {
        private const float Duration = 60f;
        private const int Seed = 1234;

        private DrinkTable _drinks;
        private RecipeTable _recipes;
        private RecipeMatcher _matcher;

        [SetUp]
        public void SetUp()
        {
            _drinks = ScriptableObject.CreateInstance<DrinkTable>();
            _recipes = ScriptableObject.CreateInstance<RecipeTable>();

            _recipes.ReplaceRows(new object[]
            {
                Row<RecipeData>("{\"_id\":\"Recipe_GinTonic\",\"_ingredientKey\":\"Id_Gin:1|Id_Tonic:2|Id_Lime\"}"),
                Row<RecipeData>("{\"_id\":\"Recipe_Martini\",\"_ingredientKey\":\"Id_Gin:2|Id_DryVermouth\"}"),
            });
            _drinks.ReplaceRows(new object[]
            {
                Row<DrinkData>("{\"_id\":\"Id_GinTonic\",\"_price\":10,\"_recipeKey\":\"Recipe_GinTonic\",\"_villainKey\":\"Villain_Sniper\"}"),
                Row<DrinkData>("{\"_id\":\"Id_Martini\",\"_price\":16,\"_recipeKey\":\"Recipe_Martini\",\"_villainKey\":\"Villain_Boss\"}"),
            });

            _matcher = new RecipeMatcher(_drinks, _recipes);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_drinks);
            Object.DestroyImmediate(_recipes);
        }

        [Test]
        public void 주문한_음료를_내면_돈을_받고_다음_손님이_온다()
        {
            var session = new BarSession(_matcher, Duration, Seed);
            DrinkData order = session.CurrentOrder;

            Pour(session, order);
            ServeResult result = session.Serve();

            Assert.AreEqual(ServeOutcome.Sold, result.Outcome);
            Assert.AreEqual(order.Price, session.Money);
            Assert.AreEqual(1, session.SoldCount);
            Assert.AreEqual(2, session.CustomerCount);
            CollectionAssert.IsEmpty(session.Glass);
        }

        [Test]
        public void 주문과_다른_레시피_음료는_돈_없이_손님이_떠난다()
        {
            var session = new BarSession(_matcher, Duration, Seed);
            DrinkData other = _matcher.Drinks.First(d => d != session.CurrentOrder);

            Pour(session, other);
            ServeResult result = session.Serve();

            Assert.AreEqual(ServeOutcome.WrongDrink, result.Outcome);
            Assert.AreEqual(0, session.Money);
            Assert.AreEqual(1, session.LostCount);
        }

        [Test]
        public void 레시피에_없는_조합은_손님이_떠난다()
        {
            var session = new BarSession(_matcher, Duration, Seed);

            session.AddIngredient("Id_Gin");
            ServeResult result = session.Serve();

            Assert.AreEqual(ServeOutcome.Failed, result.Outcome);
            Assert.AreEqual(1, session.LostCount);
        }

        [Test]
        public void 시드를_넣으면_만든_음료의_빌런이_된다_주문과_무관()
        {
            var session = new BarSession(_matcher, Duration, Seed);
            DrinkData other = _matcher.Drinks.First(d => d != session.CurrentOrder);

            Pour(session, other);
            session.SetSeed(true);
            ServeResult result = session.Serve();

            Assert.AreEqual(ServeOutcome.Villain, result.Outcome);
            Assert.AreEqual(0, session.Money);
            Assert.AreEqual(1, session.Villains.Count);
            Assert.AreEqual(other.VillainKey, session.Villains[0].VillainKey);
            Assert.IsFalse(session.HasSeed, "제공 후 시드는 초기화되어야 한다");
        }

        [Test]
        public void 주문한_음료에_시드를_넣으면_돈도_받고_빌런도_된다()
        {
            var session = new BarSession(_matcher, Duration, Seed);
            DrinkData order = session.CurrentOrder;

            Pour(session, order);
            session.SetSeed(true);
            ServeResult result = session.Serve();

            Assert.AreEqual(ServeOutcome.Villain, result.Outcome);
            Assert.AreEqual(order.Price, result.MoneyEarned);
            Assert.AreEqual(order.Price, session.Money);
            Assert.AreEqual(1, session.Villains.Count);
            Assert.AreEqual(order.VillainKey, session.Villains[0].VillainKey);
        }

        [Test]
        public void 레시피에_없는_조합에_시드를_넣으면_기본_빌런()
        {
            var session = new BarSession(_matcher, Duration, Seed);

            session.AddIngredient("Id_Lime");
            session.SetSeed(true);
            ServeResult result = session.Serve();

            Assert.AreEqual(ServeOutcome.DefaultVillain, result.Outcome);
            Assert.IsTrue(session.Villains[0].IsDefault);
            Assert.IsNull(session.Villains[0].VillainKey);
        }

        [Test]
        public void 빈_잔은_낼_수_없다()
        {
            var session = new BarSession(_matcher, Duration, Seed);
            session.SetSeed(true);

            ServeResult result = session.Serve();

            Assert.AreEqual(ServeOutcome.None, result.Outcome);
            Assert.AreEqual(1, session.CustomerCount, "손님이 바뀌면 안 된다");
        }

        [Test]
        public void 시간이_끝나면_더_이상_제공할_수_없다()
        {
            var session = new BarSession(_matcher, Duration, Seed);
            DrinkData order = session.CurrentOrder;

            session.Tick(Duration + 1f);
            Pour(session, order);

            Assert.IsTrue(session.IsOver);
            Assert.IsNull(session.CurrentOrder);
            Assert.AreEqual(ServeOutcome.None, session.Serve().Outcome);
            Assert.AreEqual(0, session.Money);
        }

        [Test]
        public void 같은_시드면_같은_주문_순서가_나온다()
        {
            var a = new BarSession(_matcher, Duration, Seed);
            var b = new BarSession(_matcher, Duration, Seed);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreSame(a.CurrentOrder, b.CurrentOrder, $"{i}번째 주문");
                a.AddIngredient("Id_Lime");
                b.AddIngredient("Id_Lime");
                a.Serve();
                b.Serve();
            }
        }

        private void Pour(BarSession session, DrinkData drink)
        {
            foreach (RecipeEntry entry in _recipes.Get(drink.RecipeKey).Ingredients)
            {
                for (int i = 0; i < entry.Count; i++)
                    session.AddIngredient(entry.IngredientId);
            }
        }

        private static T Row<T>(string json) => JsonUtility.FromJson<T>(json);
    }
}
