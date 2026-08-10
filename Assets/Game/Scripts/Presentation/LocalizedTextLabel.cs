using UnityEngine;
using UnityEngine.UI;
using Game.Data;

namespace Game.Presentation
{
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedTextLabel : MonoBehaviour
    {
        [SerializeField] private LocalizationTableAsset table;
        [SerializeField] private string key;

        private Text _text;

        private void Awake()
        {
            _text = GetComponent<Text>();
            Refresh();
        }

        public void Refresh()
        {
            if (_text == null)
                _text = GetComponent<Text>();

            if (_text == null || table == null || string.IsNullOrEmpty(key))
                return;

            _text.text = table.GetKorean(key);
        }
    }
}
