using System;
using System.Collections.Generic;
using System.Linq;
using VillainPub.Data;

namespace VillainPub.Bar
{
    /// <summary>
    /// 하루 영업 한 번의 진행 상태. 화면과 무관한 순수 로직이며, 화면은 이 상태를 읽고 입력만 전달한다.
    /// 손님은 한 명씩 오고 인내심 타이머는 없다. 영업 제한 시간이 끝나면 더 이상 제공할 수 없다.
    /// </summary>
    public sealed class BarSession
    {
        private readonly RecipeMatcher _matcher;
        private readonly List<DrinkData> _menu;
        private readonly Random _random;

        private readonly List<string> _glass = new List<string>();
        private readonly List<BarVillain> _villains = new List<BarVillain>();

        public BarSession(RecipeMatcher matcher, float durationSeconds, int randomSeed)
        {
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            if (durationSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(durationSeconds));

            // 딕셔너리 순서에 의존하지 않도록 정렬해 두어, 같은 시드면 같은 주문 순서가 나오게 한다.
            _menu = matcher.Drinks.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (_menu.Count == 0) throw new ArgumentException("판정할 수 있는 음료가 없습니다.", nameof(matcher));

            _random = new Random(randomSeed);
            DurationSeconds = durationSeconds;
            RemainingSeconds = durationSeconds;
            NextCustomer();
        }

        public float DurationSeconds { get; }
        public float RemainingSeconds { get; private set; }
        public bool IsOver => RemainingSeconds <= 0f;

        /// <summary>지금 손님이 주문한 음료. 영업이 끝나면 null.</summary>
        public DrinkData CurrentOrder { get; private set; }

        /// <summary>잔에 넣은 재료 ID(넣은 순서대로).</summary>
        public IReadOnlyList<string> Glass => _glass;
        public bool HasSeed { get; private set; }

        public int Money { get; private set; }
        public IReadOnlyList<BarVillain> Villains => _villains;
        public int CustomerCount { get; private set; }
        public int SoldCount { get; private set; }
        public int LostCount { get; private set; }

        public void Tick(float deltaSeconds)
        {
            if (IsOver || deltaSeconds <= 0f) return;

            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (IsOver) CurrentOrder = null;
        }

        public void AddIngredient(string ingredientId)
        {
            if (IsOver || string.IsNullOrEmpty(ingredientId)) return;
            _glass.Add(ingredientId);
        }

        public void SetSeed(bool hasSeed)
        {
            if (IsOver) return;
            HasSeed = hasSeed;
        }

        public void ClearGlass()
        {
            _glass.Clear();
            HasSeed = false;
        }

        /// <summary>
        /// 잔을 손님에게 낸다. 결과와 상관없이 손님은 떠나고 다음 손님이 온다.
        /// 빈 잔은 내지 않는다(아무 일도 일어나지 않는다).
        /// </summary>
        public ServeResult Serve()
        {
            if (IsOver) return ServeResult.None;
            if (_glass.Count == 0) return ServeResult.None;

            DrinkData order = CurrentOrder;
            BrewResult brew = _matcher.Brew(_glass, HasSeed);
            ServeResult result;

            switch (brew.Outcome)
            {
                case BrewOutcome.Sold when ReferenceEquals(brew.Drink, order):
                    Money += brew.Drink.Price;
                    SoldCount++;
                    result = new ServeResult(ServeOutcome.Sold, order, brew.Drink, brew.Drink.Price);
                    break;

                case BrewOutcome.Sold:
                    LostCount++;
                    result = new ServeResult(ServeOutcome.WrongDrink, order, brew.Drink, 0);
                    break;

                // 빌런 종류는 주문이 아니라 실제로 만든 음료가 정한다.
                // 주문한 음료에 시드를 넣었으면 값도 받는다(손님은 주문대로 받았으니 계산은 한다).
                case BrewOutcome.Villain:
                {
                    int earned = ReferenceEquals(brew.Drink, order) ? brew.Drink.Price : 0;
                    Money += earned;
                    _villains.Add(new BarVillain(brew.Drink.VillainKey, brew.Drink.Id, isDefault: false));
                    result = new ServeResult(ServeOutcome.Villain, order, brew.Drink, earned);
                    break;
                }

                case BrewOutcome.DefaultVillain:
                    _villains.Add(new BarVillain(null, null, isDefault: true));
                    result = new ServeResult(ServeOutcome.DefaultVillain, order, null, 0);
                    break;

                default:
                    LostCount++;
                    result = new ServeResult(ServeOutcome.Failed, order, null, 0);
                    break;
            }

            ClearGlass();
            NextCustomer();
            return result;
        }

        private void NextCustomer()
        {
            CustomerCount++;
            CurrentOrder = _menu[_random.Next(_menu.Count)];
        }
    }

    public enum ServeOutcome
    {
        /// <summary>제공하지 않음(빈 잔이거나 영업 종료).</summary>
        None,
        /// <summary>주문한 음료를 냄 → 돈을 받음.</summary>
        Sold,
        /// <summary>레시피에는 있지만 주문과 다른 음료 → 돈 없이 손님이 떠남.</summary>
        WrongDrink,
        /// <summary>레시피에 없는 조합 → 돈 없이 손님이 떠남.</summary>
        Failed,
        /// <summary>레시피 음료 + 시드 → 그 음료의 빌런. 주문한 음료였으면 돈도 받는다.</summary>
        Villain,
        /// <summary>레시피에 없는 조합 + 시드 → 기본 빌런.</summary>
        DefaultVillain,
    }

    public readonly struct ServeResult
    {
        public static ServeResult None => default;

        public ServeOutcome Outcome { get; }
        public DrinkData Order { get; }
        /// <summary>실제로 만든 레시피 음료. 실패·기본 빌런이면 null.</summary>
        public DrinkData Made { get; }
        public int MoneyEarned { get; }

        public ServeResult(ServeOutcome outcome, DrinkData order, DrinkData made, int moneyEarned)
        {
            Outcome = outcome;
            Order = order;
            Made = made;
            MoneyEarned = moneyEarned;
        }
    }

    /// <summary>
    /// 오늘 만든 빌런 한 명. 기본 빌런이면 VillainKey가 null이다(어떤 유닛인지는 Villain 테이블 확정 후 정한다).
    /// </summary>
    public readonly struct BarVillain
    {
        public string VillainKey { get; }
        public string FromDrinkId { get; }
        public bool IsDefault { get; }

        public BarVillain(string villainKey, string fromDrinkId, bool isDefault)
        {
            VillainKey = villainKey;
            FromDrinkId = fromDrinkId;
            IsDefault = isDefault;
        }
    }
}
