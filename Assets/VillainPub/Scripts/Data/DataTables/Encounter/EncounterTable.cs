using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    /// <summary>
    /// 전투 한 번의 적 구성. 영업 전에 플레이어에게 공개한다.
    /// 구역·스테이지 구조가 정해지면 스테이지가 이 ID를 참조하게 될 예정이다.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterTable", menuName = "VillainPub/Data/Encounter Table")]
    public class EncounterTable : GoogleSheetParser<EncounterData>
    {
        [Header("참조 검증용")]
        [SerializeField] private HeroTable _heroes;
        [SerializeField] private BuildingTable _buildings;
        [SerializeField] private StringTable _strings;

        protected override void ValidateRows(List<string> errors)
        {
            if (_heroes == null) errors.Add($"[{name}] 히어로 테이블이 연결되지 않았습니다.");
            if (_buildings == null) errors.Add($"[{name}] 건물 테이블이 연결되지 않았습니다.");
            if (_strings == null) errors.Add($"[{name}] 문자열 테이블이 연결되지 않았습니다.");

            if (_heroes != null && _buildings != null)
            {
                // enemyKey는 히어로·건물 ID를 섞어 쓰므로 두 테이블에 같은 ID가 있으면 어느 쪽인지 정할 수 없다.
                foreach (HeroData hero in _heroes.Rows)
                {
                    if (_buildings.Contains(hero.Id))
                        errors.Add($"[{name}] ID '{hero.Id}'가 히어로·건물 테이블 양쪽에 있습니다.");
                }
            }

            foreach (EncounterData row in Rows)
            {
                string context = $"[{name}] '{row.Id}'";
                if (_strings != null) _strings.CheckKey(row.NameStrKey, $"{context} nameStrKey", errors);

                if (row.EnemyKey.Count == 0)
                {
                    errors.Add($"{context}의 enemyKey가 비어 있습니다.");
                    continue;
                }

                if (_heroes == null || _buildings == null) continue;
                foreach (string enemyId in row.EnemyKey)
                {
                    if (!_heroes.Contains(enemyId) && !_buildings.Contains(enemyId))
                        errors.Add($"{context}의 적 '{enemyId}'가 히어로·건물 테이블에 없습니다.");
                }
            }
        }
    }

    [Serializable]
    public class EncounterData : ISheetRow
    {
        [SerializeField] private string _id;
        [SerializeField] private string _nameStrKey;
        // 맨 앞(빌런과 맞닿는 칸)부터 순서대로. 히어로·건물 ID를 섞어 쓴다. 같은 ID를 여러 번 적어도 된다.
        [SerializeField] private string[] _enemyKey;

        public string Id => _id;
        public string NameStrKey => _nameStrKey;
        public IReadOnlyList<string> EnemyKey => _enemyKey ?? Array.Empty<string>();
    }
}
