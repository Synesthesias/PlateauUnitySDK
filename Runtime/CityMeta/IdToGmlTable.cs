using System;
using System.Collections.Generic;
using PLATEAU.CityGML;
using PLATEAU.Util;
using UnityEngine;

namespace PLATEAU.CityMeta
{
    /// <summary>
    /// <see cref="CityObject"/> のIDから、対応するGMLのファイル名を検索できる辞書データです。
    /// <see cref="CityMetaData"/> によって保持されます。
    /// </summary>
    [Serializable]
    internal class IdToGmlTable :  ISerializationCallbackReceiver//, IDictionary<string, string> // TODO IDictionary に対応したほうが便利だけどまだやってない
    {
        private Dictionary<string, string> dictionary = new Dictionary<string, string>();
        // Unityの仕様上、シリアライズするときは List 形式で行い、 デシリアライズするときは Dictionary 形式に直します。
        [SerializeField] private List<string> keys = new List<string>();
        [SerializeField] private List<string> values = new List<string>();

        /// <summary>
        /// シリアライズするときに List形式に直します。
        /// </summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            DictionarySerializer.OnBeforeSerialize(this.dictionary, this.keys, this.values);
        }

        /// <summary>
        /// デシリアライズするときに List から Dictionary 形式に直します。
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            this.dictionary = DictionarySerializer.OnAfterSerialize(this.keys, this.values);
        }

        public void Add(string id, string gmlFileName)
        {
            Add(new KeyValuePair<string, string>(id, gmlFileName));    
        }

        public void Add(KeyValuePair<string, string> item)
        {
            this.dictionary.Add(item.Key, item.Value);
        }

        public bool ContainsKey(string key) => this.dictionary.ContainsKey(key);
        public bool TryGetValue(string key, out string value)
        {
            return this.dictionary.TryGetValue(key, out value);
        }

        public string this[string key]
        {
            get => this.dictionary[key];
            set => this.dictionary[key] = value;
        }

        public ICollection<string> Keys => this.dictionary.Keys;

        public void Clear()
        {
            this.dictionary.Clear();
        }

        public int Count => this.dictionary.Count;
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return this.dictionary.GetEnumerator();
        }
    }
}
