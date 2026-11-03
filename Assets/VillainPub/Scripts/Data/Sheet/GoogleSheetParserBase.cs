using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    /// <summary>
    /// 시트 테이블의 비제네릭 베이스. 에디터(CustomEditor, 일괄 동기화)가 T를 몰라도 다룰 수 있게 한다.
    /// 런타임 코드는 GSTU에 의존하지 않는다. 시트 읽기·변환은 에디터 쪽(SheetSync)에서만 한다.
    /// </summary>
    public abstract class GoogleSheetParserBase : ScriptableObject
    {
        [Header("시트 ID (URL의 /d/ 와 /edit 사이)")]
        [SerializeField] private string _sheetId;
        [Header("워크시트(탭) 이름")]
        [SerializeField] private string _worksheet;

        [Header("행 번호 (1부터 시작, 타입 행 0 = 사용 안 함)")]
        [SerializeField, Min(1)] private int _headerRow = 1;
        [SerializeField, Min(0)] private int _typeRow = 2;
        [SerializeField, Min(1)] private int _dataStartRow = 3;

        public string SheetId => _sheetId;
        public string Worksheet => _worksheet;
        public int TypeRow => _typeRow;
        public int HeaderRow => _headerRow;
        public int DataStartRow => _dataStartRow;

        public abstract Type RowType { get; }
        public abstract int Count { get; }

        /// <summary>
        /// 에디터 동기화 전용. 런타임에서 호출하지 않는다.
        /// </summary>
        public abstract void ReplaceRows(IEnumerable<object> rows);

        /// <summary>
        /// 데이터 검증. 문제를 errors에 추가한다. 다른 테이블 참조 검증은 하위 클래스에서 한다.
        /// </summary>
        public abstract void Validate(List<string> errors);
    }
}
