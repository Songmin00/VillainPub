using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VillainPub.Data
{
    /// <summary>
    /// 구글 시트 한 장을 담는 읽기 전용 테이블.
    /// 사용법: public class IngredientTable : GoogleSheetParser&lt;IngredientDefinition&gt; { }
    /// </summary>
    public abstract class GoogleSheetParser<T> : GoogleSheetParserBase where T : class, ISheetRow, new()
    {
        [Header("결과 데이터 (시트에서 동기화, 직접 수정 금지)")]
        [SerializeField] private List<T> _rows = new List<T>();

        // 직렬화되지 않는 캐시. Domain Reload가 꺼져 있어도 SO 인스턴스 필드이므로 static 문제는 없지만,
        // 데이터가 바뀌는 시점(동기화, 인스펙터 수정)에는 반드시 비운다.
        [NonSerialized] private Dictionary<string, T> _lookup;

        public IReadOnlyList<T> Rows => _rows;
        public override Type RowType => typeof(T);
        public override int Count => _rows.Count;

        public bool TryGet(string id, out T row)
        {
            if (string.IsNullOrEmpty(id))
            {
                row = null;
                return false;
            }
            return Lookup.TryGetValue(id, out row);
        }

        public T Get(string id)
        {
            if (TryGet(id, out T row)) return row;

            Debug.LogError($"[{name}] ID '{id}'를 찾을 수 없습니다.", this);
            return null;
        }

        public bool Contains(string id) => !string.IsNullOrEmpty(id) && Lookup.ContainsKey(id);

        private Dictionary<string, T> Lookup
        {
            get
            {
                if (_lookup != null) return _lookup;

                _lookup = new Dictionary<string, T>(_rows.Count);
                foreach (T row in _rows)
                {
                    // 중복 ID는 Validate에서 에러로 잡는다. 여기서는 첫 행을 쓴다.
                    if (row != null && !string.IsNullOrEmpty(row.Id) && !_lookup.ContainsKey(row.Id))
                        _lookup.Add(row.Id, row);
                }
                return _lookup;
            }
        }

        public sealed override void ReplaceRows(IEnumerable<object> rows)
        {
            _rows = rows.Cast<T>().ToList();
            _lookup = null;
        }

        public sealed override void Validate(List<string> errors)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < _rows.Count; i++)
            {
                string id = _rows[i]?.Id;
                if (string.IsNullOrEmpty(id))
                    errors.Add($"[{name}] {i}번째 행의 ID가 비어 있습니다.");
                else if (!seen.Add(id))
                    errors.Add($"[{name}] ID '{id}'가 중복됩니다.");
            }

            ValidateRows(errors);
        }

        /// <summary>
        /// 테이블별 추가 검증(레시피 재료 ID 존재 여부 등). 필요한 테이블만 override한다.
        /// </summary>
        protected virtual void ValidateRows(List<string> errors) { }

        protected virtual void OnValidate()
        {
            _lookup = null;
        }
    }
}
