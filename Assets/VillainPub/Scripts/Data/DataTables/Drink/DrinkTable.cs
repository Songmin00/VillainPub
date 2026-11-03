using System;
using System.Collections.Generic;
using UnityEngine;

namespace VillainPub.Data
{
    [CreateAssetMenu(fileName = "DrinkTable", menuName = "VillainPub/Data/Drink Table")]
    public class DrinkTable : GoogleSheetParser<DrinkData> { }

    public enum DrinkGroup
    {
        None
    }

    [Serializable]
    public class DrinkData : ISheetRow
    {
        // 시트 변수명 'id'는 필드 'id' 또는 '_id'에 들어간다.
        [SerializeField] private string _id;
        [SerializeField] private int _price;
        [SerializeField] private DrinkGroup _type;
        [SerializeField] private string _nameStrKey;
        [SerializeField] private string _descStrKey;
        [SerializeField] private string _recipeKey;
        [SerializeField] private string _villainKey;
        

        public string Id => _id;
        public int Price => _price;
        public DrinkGroup Type => _type;
        public string NameStrKey => _nameStrKey;
        public string DescStrKey => _descStrKey;
        public string RecipeKey => _recipeKey;
        public string VillainKey => _villainKey;

    }
}
