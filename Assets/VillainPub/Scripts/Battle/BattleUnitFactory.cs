using System;
using System.Collections.Generic;
using VillainPub.Bar;
using VillainPub.Battle.Simulation;
using VillainPub.Data;

namespace VillainPub.Battle
{
    /// <summary>
    /// 시트 데이터(Villain, Hero, Building, Encounter) → 전투 입력(<see cref="BattleUnitSpec"/>) 변환.
    /// 시뮬레이션이 데이터 테이블(ScriptableObject)을 모르게 하려고 이 단계를 따로 둔다.
    /// </summary>
    public sealed class BattleUnitFactory
    {
        private readonly VillainTable _villains;
        private readonly HeroTable _heroes;
        private readonly BuildingTable _buildings;

        public BattleUnitFactory(VillainTable villains, HeroTable heroes, BuildingTable buildings)
        {
            _villains = villains ?? throw new ArgumentNullException(nameof(villains));
            _heroes = heroes ?? throw new ArgumentNullException(nameof(heroes));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        }

        /// <summary>
        /// 영업에서 만든 빌런 → 전투 유닛. 순서는 그대로 유지한다(배치는 플레이어가 나중에 바꾼다).
        /// 데이터에 없는 빌런은 건너뛰고 problems에 남긴다.
        /// </summary>
        public List<BattleUnitSpec> CreateVillains(IEnumerable<BarVillain> villains, List<string> problems = null)
        {
            var result = new List<BattleUnitSpec>();
            foreach (BarVillain villain in villains)
            {
                VillainData data = null;
                if (villain.IsDefault)
                    data = _villains.DefaultVillain;
                else
                    _villains.TryGet(villain.VillainKey, out data);

                if (data == null)
                {
                    problems?.Add(villain.IsDefault
                        ? "기본 빌런(isDefault)이 빌런 테이블에 없습니다."
                        : $"빌런 '{villain.VillainKey}'가 빌런 테이블에 없습니다.");
                    continue;
                }

                result.Add(FromVillain(data));
            }
            return result;
        }

        public static BattleUnitSpec FromVillain(VillainData data) =>
            new BattleUnitSpec(data.Id, UnitKind.Villain, data.BaseAtk, data.BaseHp);

        /// <summary>적 구성 → 전투 유닛(맨 앞부터). 히어로·건물 테이블 어디에도 없는 ID는 건너뛰고 problems에 남긴다.</summary>
        public List<BattleUnitSpec> CreateEnemies(EncounterData encounter, List<string> problems = null)
        {
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));

            var result = new List<BattleUnitSpec>(encounter.EnemyKey.Count);
            foreach (string id in encounter.EnemyKey)
            {
                if (_heroes.TryGet(id, out HeroData hero))
                    result.Add(new BattleUnitSpec(hero.Id, UnitKind.Hero, hero.BaseAtk, hero.BaseHp));
                else if (_buildings.TryGet(id, out BuildingData building))
                    result.Add(new BattleUnitSpec(building.Id, UnitKind.Building, 0, building.BaseHp));
                else
                    problems?.Add($"'{encounter.Id}'의 적 '{id}'가 히어로·건물 테이블에 없습니다.");
            }
            return result;
        }

        /// <summary>부순 건물 보상 합계. 승패와 무관하게 받는다.</summary>
        public int SumBuildingReward(IEnumerable<string> destroyedBuildingIds)
        {
            int total = 0;
            foreach (string id in destroyedBuildingIds)
            {
                if (_buildings.TryGet(id, out BuildingData building)) total += building.Reward;
            }
            return total;
        }

        /// <summary>표시용 이름 키. 유닛 종류에 맞는 테이블에서 찾고, 없으면 null.</summary>
        public string NameStrKey(BattleUnitSpec spec)
        {
            switch (spec.Kind)
            {
                case UnitKind.Villain:
                    return _villains.TryGet(spec.Id, out VillainData v) ? v.NameStrKey : null;
                case UnitKind.Hero:
                    return _heroes.TryGet(spec.Id, out HeroData h) ? h.NameStrKey : null;
                default:
                    return _buildings.TryGet(spec.Id, out BuildingData b) ? b.NameStrKey : null;
            }
        }
    }
}
