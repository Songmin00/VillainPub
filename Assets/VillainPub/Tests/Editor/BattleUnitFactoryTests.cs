using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VillainPub.Bar;
using VillainPub.Battle;
using VillainPub.Battle.Simulation;
using VillainPub.Data;

namespace VillainPub.Tests
{
    public class BattleUnitFactoryTests
    {
        private VillainTable _villains;
        private HeroTable _heroes;
        private BuildingTable _buildings;
        private BattleUnitFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _villains = ScriptableObject.CreateInstance<VillainTable>();
            _heroes = ScriptableObject.CreateInstance<HeroTable>();
            _buildings = ScriptableObject.CreateInstance<BuildingTable>();

            _villains.ReplaceRows(new object[]
            {
                Row<VillainData>("{\"_id\":\"Id_Sniper\",\"_baseHp\":2,\"_baseAtk\":4}"),
                Row<VillainData>("{\"_id\":\"Id_Thug\",\"_baseHp\":2,\"_baseAtk\":1,\"_isDefault\":true}"),
            });
            _heroes.ReplaceRows(new object[]
            {
                Row<HeroData>("{\"_id\":\"Id_Rookie\",\"_baseHp\":3,\"_baseAtk\":2}"),
            });
            _buildings.ReplaceRows(new object[]
            {
                Row<BuildingData>("{\"_id\":\"Id_Shop\",\"_baseHp\":4,\"_reward\":15}"),
                Row<BuildingData>("{\"_id\":\"Id_Bank\",\"_baseHp\":8,\"_reward\":40}"),
            });

            _factory = new BattleUnitFactory(_villains, _heroes, _buildings);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_villains);
            Object.DestroyImmediate(_heroes);
            Object.DestroyImmediate(_buildings);
        }

        [Test]
        public void 영업_빌런은_순서대로_변환되고_기본_빌런은_isDefault_행을_쓴다()
        {
            var bar = new[]
            {
                new BarVillain("Id_Sniper", "Id_GinTonic", isDefault: false),
                new BarVillain(null, null, isDefault: true),
            };

            List<BattleUnitSpec> specs = _factory.CreateVillains(bar);

            CollectionAssert.AreEqual(new[] { "Id_Sniper", "Id_Thug" }, specs.Select(s => s.Id));
            Assert.AreEqual(4, specs[0].Attack);
            Assert.AreEqual(2, specs[0].Hp);
            Assert.IsTrue(specs.All(s => s.Kind == UnitKind.Villain));
        }

        [Test]
        public void 없는_빌런은_건너뛰고_문제로_남긴다()
        {
            var problems = new List<string>();
            List<BattleUnitSpec> specs = _factory.CreateVillains(
                new[] { new BarVillain("Id_Nope", "x", isDefault: false) }, problems);

            CollectionAssert.IsEmpty(specs);
            Assert.AreEqual(1, problems.Count);
        }

        [Test]
        public void 적_구성은_히어로와_건물을_섞어_앞에서부터_변환한다()
        {
            var encounter = Row<EncounterData>("{\"_id\":\"Encounter_T\",\"_enemyKey\":[\"Id_Shop\",\"Id_Rookie\",\"Id_Bank\"]}");

            List<BattleUnitSpec> specs = _factory.CreateEnemies(encounter);

            CollectionAssert.AreEqual(new[] { UnitKind.Building, UnitKind.Hero, UnitKind.Building }, specs.Select(s => s.Kind));
            Assert.AreEqual(0, specs[0].Attack, "건물은 공격력 0");
            Assert.AreEqual(2, specs[1].Attack);
        }

        [Test]
        public void 부순_건물_보상을_합산한다()
        {
            Assert.AreEqual(55, _factory.SumBuildingReward(new[] { "Id_Shop", "Id_Bank" }));
            Assert.AreEqual(0, _factory.SumBuildingReward(new string[0]));
        }

        [Test]
        public void 데이터로_만든_전투가_끝까지_돈다()
        {
            var encounter = Row<EncounterData>("{\"_id\":\"Encounter_T\",\"_enemyKey\":[\"Id_Rookie\",\"Id_Shop\"]}");
            var setup = new BattleSetup(
                _factory.CreateVillains(new[] { new BarVillain("Id_Sniper", "x", false), new BarVillain(null, null, true) }),
                _factory.CreateEnemies(encounter));

            BattleResult result = BattleSimulator.Run(setup);

            // 저격수(4/2)가 신참(2/3)과 맞교환, 잡졸(1/2)이 상점(4)을 4라운드에 부순다.
            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            CollectionAssert.AreEqual(new[] { "Id_Shop" }, result.DestroyedBuildings);
        }

        private static T Row<T>(string json) => JsonUtility.FromJson<T>(json);
    }
}
