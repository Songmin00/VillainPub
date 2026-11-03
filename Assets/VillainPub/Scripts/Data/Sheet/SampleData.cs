using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    // 새 시트 테이블을 만들 때 참고용 예시. 실제 테이블이 생기면 지워도 된다.
    //
    // 시트 구성 예 (A열이 ID, 2행 타입은 참고용 검사에만 쓰임):
    //   1행 | id     | name   | level | value | isMan | nation       | tags
    //   2행 | string | string | int   | float | bool  | SampleNation | string[]
    //   3행 | s001   | 홍길동 | 3     | 1.5   | y     | Korean       | a|b|c
    //   4행 | #s002  | (A열이 #로 시작하면 주석 행, 건너뜀)
    // 변수명이 비었거나 #로 시작하는 열은 메모 열로 무시한다.

    [CreateAssetMenu(fileName = "SampleTable", menuName = "VillainPub/Data/Sample Table")]
    public class SampleTable : GoogleSheetParser<SampleData> { }

    public enum SampleNation
    {
        None, Korean, Chinese, American
    }

    [Serializable]
    public class SampleData : ISheetRow
    {
        // 시트 변수명 'id'는 필드 'id' 또는 '_id'에 들어간다.
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _level;
        [SerializeField] private float _value;
        [SerializeField] private bool _isMan;
        [SerializeField] private SampleNation _nation;
        [SerializeField] private List<string> _tags;

        public string Id => _id;
        public string Name => _name;
        public int Level => _level;
        public float Value => _value;
        public bool IsMan => _isMan;
        public SampleNation Nation => _nation;
        public IReadOnlyList<string> Tags => _tags;
    }
}
