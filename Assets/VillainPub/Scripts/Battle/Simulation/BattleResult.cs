using System.Collections.Generic;

namespace VillainPub.Battle.Simulation
{
    public enum BattleEventType
    {
        /// <summary>라운드 시작. Value = 라운드 번호(1부터).</summary>
        RoundStarted,
        /// <summary>Source가 Target에게 피해. Value = 피해량, TargetHpAfter = 맞은 뒤 체력(음수 가능).</summary>
        Damaged,
        /// <summary>Target이 쓰러짐(건물이면 파괴). 같은 줄의 뒤 유닛들이 한 칸씩 앞으로 당겨진다.</summary>
        Fainted,
        /// <summary>전투 종료. 이후 이벤트 없음.</summary>
        Ended,
    }

    /// <summary>
    /// 연출이 재생할 전투 이벤트 한 건. 유닛은 Uid(<see cref="BattleResult.Units"/>의 index)로 가리킨다.
    /// 해당 없는 Uid는 -1.
    /// </summary>
    public readonly struct BattleEvent
    {
        public BattleEventType Type { get; }
        public int Round { get; }
        public int SourceUid { get; }
        public int TargetUid { get; }
        public int Value { get; }
        public int TargetHpAfter { get; }

        public BattleEvent(BattleEventType type, int round, int sourceUid = -1, int targetUid = -1,
            int value = 0, int targetHpAfter = 0)
        {
            Type = type;
            Round = round;
            SourceUid = sourceUid;
            TargetUid = targetUid;
            Value = value;
            TargetHpAfter = targetHpAfter;
        }

        public override string ToString() =>
            $"[R{Round}] {Type} src={SourceUid} tgt={TargetUid} value={Value} hpAfter={TargetHpAfter}";
    }

    /// <summary>전투 시작 시점의 유닛 정보. Uid = 이 유닛이 <see cref="BattleResult.Units"/>에 있는 index.</summary>
    public readonly struct BattleUnitInfo
    {
        public int Uid { get; }
        public BattleSide Side { get; }
        /// <summary>시작 시 자기 줄에서의 칸(0 = 맨 앞).</summary>
        public int StartSlot { get; }
        public BattleUnitSpec Spec { get; }

        public BattleUnitInfo(int uid, BattleSide side, int startSlot, BattleUnitSpec spec)
        {
            Uid = uid;
            Side = side;
            StartSlot = startSlot;
            Spec = spec;
        }
    }

    public enum BattleOutcome
    {
        /// <summary>히어로를 모두 쓰러뜨림 → 스테이지 클리어. 히어로가 없는 전투(건물만)도 여기에 해당.</summary>
        Victory,
        /// <summary>히어로가 남음 → 스테이지 진행 없음. 그래도 부순 건물의 보상은 받는다.</summary>
        Defeat,
    }

    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; }
        public int Rounds { get; }
        /// <summary>서로 피해를 줄 수 없는 상태(공격력 0끼리 마주침)나 라운드 상한으로 끝났는지.</summary>
        public bool Stalemate { get; }

        public IReadOnlyList<BattleUnitInfo> Units { get; }
        public IReadOnlyList<BattleEvent> Events { get; }

        /// <summary>파괴된 건물의 데이터 ID(파괴된 순서). 보상 계산용.</summary>
        public IReadOnlyList<string> DestroyedBuildings { get; }
        /// <summary>살아남은 유닛의 Uid(줄 앞에서부터, 빌런 → 적 순).</summary>
        public IReadOnlyList<int> Survivors { get; }

        public BattleResult(BattleOutcome outcome, int rounds, bool stalemate,
            IReadOnlyList<BattleUnitInfo> units, IReadOnlyList<BattleEvent> events,
            IReadOnlyList<string> destroyedBuildings, IReadOnlyList<int> survivors)
        {
            Outcome = outcome;
            Rounds = rounds;
            Stalemate = stalemate;
            Units = units;
            Events = events;
            DestroyedBuildings = destroyedBuildings;
            Survivors = survivors;
        }
    }
}
