using System;
using System.Linq;
using NUnit.Framework;
using VillainPub.Battle.Simulation;

namespace VillainPub.Tests
{
    public class BattleSimulatorTests
    {
        private static BattleUnitSpec V(string id, int atk, int hp) => new BattleUnitSpec(id, UnitKind.Villain, atk, hp);
        private static BattleUnitSpec H(string id, int atk, int hp) => new BattleUnitSpec(id, UnitKind.Hero, atk, hp);
        private static BattleUnitSpec B(string id, int hp, int atk = 0) => new BattleUnitSpec(id, UnitKind.Building, atk, hp);

        private static BattleResult Run(BattleUnitSpec[] villains, BattleUnitSpec[] enemies) =>
            BattleSimulator.Run(new BattleSetup(villains, enemies));

        [Test]
        public void 맨_앞끼리_동시에_공격하고_체력이_0이면_쓰러진다()
        {
            BattleResult result = Run(new[] { V("v", 3, 5) }, new[] { H("h", 2, 3) });

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(1, result.Rounds);
            var damaged = result.Events.Where(e => e.Type == BattleEventType.Damaged).ToList();
            Assert.AreEqual(2, damaged.Count);
            Assert.AreEqual(0, damaged.Single(e => e.TargetUid == 1).TargetHpAfter);
            Assert.AreEqual(3, damaged.Single(e => e.TargetUid == 0).TargetHpAfter);
            CollectionAssert.AreEqual(new[] { 0 }, result.Survivors);
        }

        [Test]
        public void 동시에_쓰러지면_둘_다_사라지고_히어로가_없으면_승리()
        {
            BattleResult result = Run(new[] { V("v", 5, 5) }, new[] { H("h", 5, 5) });

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            CollectionAssert.IsEmpty(result.Survivors);
            Assert.AreEqual(2, result.Events.Count(e => e.Type == BattleEventType.Fainted));
        }

        [Test]
        public void 앞_유닛이_쓰러지면_뒤_유닛이_이어서_싸운다()
        {
            // v1(2/1)은 1라운드에 쓰러지고, v2(4/10)가 h(1/5)를 상대한다. h는 1라운드에 2, 2라운드에 4 → 쓰러짐.
            BattleResult result = Run(new[] { V("v1", 2, 1), V("v2", 4, 10) }, new[] { H("h", 1, 5) });

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(2, result.Rounds);
            CollectionAssert.AreEqual(new[] { 1 }, result.Survivors);
        }

        [Test]
        public void 빌런이_전멸하고_히어로가_남으면_패배()
        {
            BattleResult result = Run(new[] { V("v", 1, 2) }, new[] { H("h", 3, 10) });

            Assert.AreEqual(BattleOutcome.Defeat, result.Outcome);
            CollectionAssert.AreEqual(new[] { 1 }, result.Survivors);
        }

        [Test]
        public void 앞에_선_건물은_반격하지_않고_뒤_히어로를_막는다()
        {
            // 건물이 맞는 동안 뒤의 히어로는 공격하지 못한다.
            BattleResult result = Run(new[] { V("v", 2, 3) }, new[] { B("wall", 4), H("h", 1, 2) });

            Assert.IsFalse(result.Events.Any(e => e.Type == BattleEventType.Damaged && e.SourceUid == 1),
                "건물(공격력 0)은 피해를 주지 않는다");
            CollectionAssert.AreEqual(new[] { "wall" }, result.DestroyedBuildings);
            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            // 빌런은 건물에 2라운드, 히어로에게 1라운드 → 히어로에게만 1 피해.
            Assert.AreEqual(3, result.Rounds);
            CollectionAssert.AreEqual(new[] { 0 }, result.Survivors);
        }

        [Test]
        public void 히어로를_다_쓰러뜨려도_건물을_부수러_계속_싸운다()
        {
            BattleResult result = Run(new[] { V("v", 3, 10) }, new[] { H("h", 1, 3), B("shop", 6), B("bank", 3) });

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            CollectionAssert.AreEqual(new[] { "shop", "bank" }, result.DestroyedBuildings);
        }

        [Test]
        public void 져도_부순_건물은_결과에_남는다()
        {
            BattleResult result = Run(new[] { V("v", 5, 2) }, new[] { B("shop", 5), H("h", 3, 10) });

            Assert.AreEqual(BattleOutcome.Defeat, result.Outcome);
            CollectionAssert.AreEqual(new[] { "shop" }, result.DestroyedBuildings);
        }

        [Test]
        public void 서로_피해를_줄_수_없으면_교착으로_끝난다()
        {
            BattleResult result = Run(new[] { V("v", 0, 5) }, new[] { B("wall", 5), H("h", 3, 3) });

            Assert.IsTrue(result.Stalemate);
            Assert.AreEqual(0, result.Rounds);
            Assert.AreEqual(BattleOutcome.Defeat, result.Outcome);
        }

        [Test]
        public void 라운드_상한에서_멈춘다()
        {
            var setup = new BattleSetup(new[] { V("v", 1, 100) }, new[] { H("h", 1, 100) }, maxRounds: 10);
            BattleResult result = BattleSimulator.Run(setup);

            Assert.IsTrue(result.Stalemate);
            Assert.AreEqual(10, result.Rounds);
        }

        [Test]
        public void 같은_입력이면_같은_이벤트가_나온다()
        {
            var villains = new[] { V("a", 2, 4), V("b", 3, 2), V("c", 1, 6) };
            var enemies = new[] { H("h1", 2, 5), B("shop", 4), H("h2", 3, 3) };

            BattleResult first = Run(villains, enemies);
            BattleResult second = Run(villains, enemies);

            CollectionAssert.AreEqual(first.Events, second.Events);
        }

        [Test]
        public void Uid는_빌런_줄_다음_적_줄_순서()
        {
            BattleResult result = Run(new[] { V("a", 1, 1), V("b", 1, 1) }, new[] { H("h", 1, 1) });

            Assert.AreEqual(BattleSide.Villain, result.Units[1].Side);
            Assert.AreEqual(1, result.Units[1].StartSlot);
            Assert.AreEqual(BattleSide.Enemy, result.Units[2].Side);
            Assert.AreEqual(0, result.Units[2].StartSlot);
            Assert.AreEqual(BattleEventType.Ended, result.Events.Last().Type);
        }

        [Test]
        public void 잘못된_입력은_거부한다()
        {
            Assert.Throws<ArgumentException>(() => new BattleSetup(new[] { V("v", 1, 0) }, new BattleUnitSpec[0]));
            Assert.Throws<ArgumentException>(() => new BattleSetup(new[] { V("v", -1, 1) }, new BattleUnitSpec[0]));
            Assert.Throws<ArgumentException>(() => new BattleSetup(new[] { H("h", 1, 1) }, new BattleUnitSpec[0]));
            Assert.Throws<ArgumentException>(() => new BattleSetup(new BattleUnitSpec[0], new[] { V("v", 1, 1) }));
        }
    }
}
