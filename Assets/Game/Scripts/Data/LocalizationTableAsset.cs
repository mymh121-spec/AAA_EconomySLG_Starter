using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Localization;

namespace Game.Data
{
    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key;
        public string korean;
        public string english;
    }

    [CreateAssetMenu(
        fileName = "KoreanLocalization",
        menuName = "게임/로컬라이징/문자열 테이블")]
    public sealed class LocalizationTableAsset : ScriptableObject
    {
        [SerializeField] private List<LocalizationEntry> entries =
            new List<LocalizationEntry>();

        public string GetKorean(string key)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.key == key)
                    return string.IsNullOrEmpty(entry.korean)
                        ? entry.english
                        : entry.korean;
            }

            return key;
        }

        public LocalizationTable ToDomain()
        {
            var table = new LocalizationTable();

            foreach (var entry in entries)
            {
                if (entry != null)
                    table.Register(entry.key, entry.korean);
            }

            return table;
        }
    }
}
