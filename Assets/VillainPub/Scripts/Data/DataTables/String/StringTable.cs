using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "StringTable", menuName = "VillainPub/Data/String Table")]
    public class StringTable : GoogleSheetParser<StringData>
    {
        /// <summary>
        /// 키에 해당하는 문구. 해당 언어가 비어 있으면 한국어, 키가 없으면 키 자체를 돌려준다(누락이 화면에 보이도록).
        /// </summary>
        public string GetText(string key, Language language)
        {
            if (!TryGet(key, out StringData row)) return key;

            string text = row.Get(language);
            return string.IsNullOrEmpty(text) ? row.Kr : text;
        }

        /// <summary>
        /// 다른 테이블이 참조하는 문자열 키가 이 테이블에 있는지 검사한다.
        /// </summary>
        public void CheckKey(string key, string context, List<string> errors)
        {
            if (string.IsNullOrEmpty(key))
                errors.Add($"{context}: 문자열 키가 비어 있습니다.");
            else if (!Contains(key))
                errors.Add($"{context}: 문자열 키 '{key}'가 문자열 테이블에 없습니다.");
        }

        protected override void ValidateRows(List<string> errors)
        {
            foreach (StringData row in Rows)
            {
                if (string.IsNullOrEmpty(row.Kr))
                    errors.Add($"[{name}] '{row.Id}'의 kr(기본 언어) 문구가 비어 있습니다.");
            }
        }
    }

    public enum Language
    {
        Kr,
        En,
        Jp,
    }

    [Serializable]
    public class StringData : ISheetRow
    {
        [SerializeField] private string _id;
        [SerializeField] private string _kr;
        [SerializeField] private string _en;
        [SerializeField] private string _jp;

        public string Id => _id;
        public string Kr => _kr;
        public string En => _en;
        public string Jp => _jp;

        public string Get(Language language)
        {
            switch (language)
            {
                case Language.En: return _en;
                case Language.Jp: return _jp;
                default: return _kr;
            }
        }
    }
}
