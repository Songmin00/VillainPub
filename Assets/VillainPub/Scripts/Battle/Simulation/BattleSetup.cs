using System;
using System.Collections.Generic;

namespace VillainPub.Battle.Simulation
{
    public enum BattleSide
    {
        Villain,
        Enemy,
    }

    public enum UnitKind
    {
        Villain,
        Hero,
        Building,
    }

    /// <summary>
    /// 전투에 들어가는 유닛 한 명의 초기 정보. 시트 데이터(Villain/Hero/Building)에서 만들어 넘긴다.
    /// 건물도 같은 유닛으로 다룬다. 보통 공격력 0이지만, 스탯으로만 구분하고 규칙을 따로 두지 않는다.
    /// </summary>
    public readonly struct BattleUnitSpec
    {
        /// <summary>데이터 ID. 연출·결과 표시용이며 시뮬레이션 규칙에는 쓰지 않는다.</summary>
        public string Id { get; }
        public UnitKind Kind { get; }
        public int Attack { get; }
        public int Hp { get; }

        public BattleUnitSpec(string id, UnitKind kind, int attack, int hp)
        {
            Id = id;
            Kind = kind;
            Attack = attack;
            Hp = hp;
        }

        public override string ToString() => $"{Id}({Kind} {Attack}/{Hp})";
    }

    /// <summary>
    /// 전투 입력. 두 줄 모두 index 0이 맨 앞(상대와 맞닿는 칸)이다.
    /// 같은 입력이면 항상 같은 결과가 나온다.
    /// </summary>
    public sealed class BattleSetup
    {
        /// <summary>무한 루프 방지용 상한. 정상적인 전투는 이보다 훨씬 빨리 끝난다.</summary>
        public const int DefaultMaxRounds = 1000;

        public IReadOnlyList<BattleUnitSpec> Villains { get; }
        public IReadOnlyList<BattleUnitSpec> Enemies { get; }

        /// <summary>
        /// 난수 시드. 스탯만 쓰는 지금 규칙에는 난수가 없지만, 능력(무작위 대상 등)이 들어오면 이 시드로 자체 RNG를 만든다.
        /// </summary>
        public int Seed { get; }
        public int MaxRounds { get; }

        public BattleSetup(IEnumerable<BattleUnitSpec> villains, IEnumerable<BattleUnitSpec> enemies,
            int seed = 0, int maxRounds = DefaultMaxRounds)
        {
            if (villains == null) throw new ArgumentNullException(nameof(villains));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));
            if (maxRounds <= 0) throw new ArgumentOutOfRangeException(nameof(maxRounds));

            // 호출한 쪽이 목록을 나중에 바꿔도 결과가 달라지지 않도록 복사해 둔다.
            var villainList = new List<BattleUnitSpec>(villains);
            var enemyList = new List<BattleUnitSpec>(enemies);

            foreach (BattleUnitSpec unit in villainList)
            {
                Validate(unit);
                if (unit.Kind != UnitKind.Villain)
                    throw new ArgumentException($"빌런 줄에는 빌런만 설 수 있습니다: {unit}", nameof(villains));
            }

            foreach (BattleUnitSpec unit in enemyList)
            {
                Validate(unit);
                if (unit.Kind == UnitKind.Villain)
                    throw new ArgumentException($"적 줄에는 히어로·건물만 설 수 있습니다: {unit}", nameof(enemies));
            }

            Villains = villainList;
            Enemies = enemyList;
            Seed = seed;
            MaxRounds = maxRounds;
        }

        private static void Validate(BattleUnitSpec unit)
        {
            if (unit.Hp <= 0) throw new ArgumentException($"체력은 1 이상이어야 합니다: {unit}");
            if (unit.Attack < 0) throw new ArgumentException($"공격력은 0 이상이어야 합니다: {unit}");
        }
    }
}
