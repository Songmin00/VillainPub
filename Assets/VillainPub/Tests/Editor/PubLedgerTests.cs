using NUnit.Framework;
using VillainPub.Core;

namespace VillainPub.Tests
{
    public class PubLedgerTests
    {
        [Test]
        public void 하루를_마감하면_매출과_보상이_쌓이고_다음_날이_된다()
        {
            var ledger = new PubLedger(startingMoney: 10);

            DaySettlement s = ledger.CloseDay(barRevenue: 40, battleReward: 15);

            Assert.AreEqual(1, s.Day);
            Assert.AreEqual(55, s.Earned);
            Assert.AreEqual(65, s.MoneyAfter);
            Assert.AreEqual(65, ledger.Money);
            Assert.AreEqual(2, ledger.Day);
        }

        [Test]
        public void 한_푼도_못_벌어도_날은_넘어간다()
        {
            var ledger = new PubLedger();

            ledger.CloseDay(0, 0);

            Assert.AreEqual(0, ledger.Money);
            Assert.AreEqual(2, ledger.Day);
        }
    }
}
