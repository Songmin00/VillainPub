using System;

namespace VillainPub.Core
{
    /// <summary>
    /// 날을 넘어 이어지는 펍의 기록: 몇째 날인지, 모은 돈.
    /// 돈은 소프트한 자원이다. 고정 지출(임대료 등)이나 파산은 없고, 벌수록 강화(예정)가 빨라질 뿐이다.
    /// </summary>
    public sealed class PubLedger
    {
        public PubLedger(int startingMoney = 0)
        {
            if (startingMoney < 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            Money = startingMoney;
            Day = 1;
        }

        /// <summary>오늘이 몇째 날인지(1부터).</summary>
        public int Day { get; private set; }
        public int Money { get; private set; }

        /// <summary>하루 마감: 영업 매출과 전투 보상을 더하고 다음 날로 넘어간다.</summary>
        public DaySettlement CloseDay(int barRevenue, int battleReward)
        {
            if (barRevenue < 0) throw new ArgumentOutOfRangeException(nameof(barRevenue));
            if (battleReward < 0) throw new ArgumentOutOfRangeException(nameof(battleReward));

            var settlement = new DaySettlement(Day, barRevenue, battleReward, Money + barRevenue + battleReward);
            Money = settlement.MoneyAfter;
            Day++;
            return settlement;
        }
    }

    public readonly struct DaySettlement
    {
        public int Day { get; }
        public int BarRevenue { get; }
        public int BattleReward { get; }
        public int Earned => BarRevenue + BattleReward;
        public int MoneyAfter { get; }

        public DaySettlement(int day, int barRevenue, int battleReward, int moneyAfter)
        {
            Day = day;
            BarRevenue = barRevenue;
            BattleReward = battleReward;
            MoneyAfter = moneyAfter;
        }
    }
}
