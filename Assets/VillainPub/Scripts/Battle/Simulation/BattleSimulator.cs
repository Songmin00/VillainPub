using System;
using System.Collections.Generic;

namespace VillainPub.Battle.Simulation
{
    /// <summary>
    /// 스탯만 쓰는 일렬 자동 전투(SAP 방식). 입력 → 이벤트 목록이며 상태를 남기지 않는다.
    ///
    /// 규칙:
    /// - 매 라운드 양 줄의 맨 앞 유닛이 동시에 서로를 공격한다(공격력 0이면 공격하지 않음).
    /// - 체력이 0 이하가 된 유닛은 쓰러지고, 뒤 유닛이 앞으로 당겨진다.
    /// - 한 줄이 전멸하면 끝. 히어로가 하나도 남지 않았으면 승리.
    ///   히어로를 다 쓰러뜨려도 건물이 남아 있으면 계속 싸운다(건물 파괴 = 보상).
    /// - 맨 앞끼리 둘 다 공격력 0이면 더 진행되지 않으므로 그 자리에서 끝낸다.
    /// </summary>
    public static class BattleSimulator
    {
        public static BattleResult Run(BattleSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));

            var units = new List<BattleUnitInfo>();
            var hp = new List<int>();
            List<int> villainLine = BuildLine(setup.Villains, BattleSide.Villain, units, hp);
            List<int> enemyLine = BuildLine(setup.Enemies, BattleSide.Enemy, units, hp);

            var events = new List<BattleEvent>();
            var destroyed = new List<string>();
            int round = 0;
            bool stalemate = false;

            while (villainLine.Count > 0 && enemyLine.Count > 0)
            {
                int v = villainLine[0];
                int e = enemyLine[0];
                int vAttack = units[v].Spec.Attack;
                int eAttack = units[e].Spec.Attack;

                if ((vAttack == 0 && eAttack == 0) || round >= setup.MaxRounds)
                {
                    stalemate = true;
                    break;
                }

                round++;
                events.Add(new BattleEvent(BattleEventType.RoundStarted, round, value: round));

                // 동시 공격: 피해를 둘 다 먼저 적용한 뒤 쓰러짐을 처리한다.
                Hit(v, e, vAttack, round, hp, events);
                Hit(e, v, eAttack, round, hp, events);

                RemoveFainted(villainLine, round, units, hp, events, destroyed);
                RemoveFainted(enemyLine, round, units, hp, events, destroyed);
            }

            events.Add(new BattleEvent(BattleEventType.Ended, round));

            bool heroesLeft = false;
            foreach (int uid in enemyLine)
            {
                if (units[uid].Spec.Kind == UnitKind.Hero)
                {
                    heroesLeft = true;
                    break;
                }
            }

            var survivors = new List<int>(villainLine.Count + enemyLine.Count);
            survivors.AddRange(villainLine);
            survivors.AddRange(enemyLine);

            return new BattleResult(
                heroesLeft ? BattleOutcome.Defeat : BattleOutcome.Victory,
                round, stalemate, units, events, destroyed, survivors);
        }

        private static List<int> BuildLine(IReadOnlyList<BattleUnitSpec> specs, BattleSide side,
            List<BattleUnitInfo> units, List<int> hp)
        {
            var line = new List<int>(specs.Count);
            for (int slot = 0; slot < specs.Count; slot++)
            {
                int uid = units.Count;
                units.Add(new BattleUnitInfo(uid, side, slot, specs[slot]));
                hp.Add(specs[slot].Hp);
                line.Add(uid);
            }
            return line;
        }

        private static void Hit(int source, int target, int damage, int round, List<int> hp, List<BattleEvent> events)
        {
            if (damage <= 0) return;

            hp[target] -= damage;
            events.Add(new BattleEvent(BattleEventType.Damaged, round, source, target, damage, hp[target]));
        }

        private static void RemoveFainted(List<int> line, int round, List<BattleUnitInfo> units, List<int> hp,
            List<BattleEvent> events, List<string> destroyed)
        {
            for (int i = 0; i < line.Count; i++)
            {
                int uid = line[i];
                if (hp[uid] > 0) continue;

                events.Add(new BattleEvent(BattleEventType.Fainted, round, targetUid: uid));
                if (units[uid].Spec.Kind == UnitKind.Building) destroyed.Add(units[uid].Spec.Id);

                line.RemoveAt(i);
                i--;
            }
        }
    }
}
