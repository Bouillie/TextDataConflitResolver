using System.Collections;
using System.Collections.Generic;

namespace ConsoleApplication1.Utils
{

    public class SortedListDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly List<KeyValuePair<TKey, TValue>?> m_list = new List<KeyValuePair<TKey, TValue>?>();
        private readonly Dictionary<TKey, int> m_indexes = new Dictionary<TKey, int>();

        public void Add(TKey key, TValue value)
        {
            m_list.Add(new KeyValuePair<TKey, TValue>(key, value));
            m_indexes.Add(key, m_list.Count - 1);
        }

        public void Remove(TKey key)
        {
            if (m_indexes.TryGetValue(key, out int index))
            {
                m_indexes.Remove(key);
                m_list[index] = null;
            }
        }
        
        public TValue this[TKey key]
        {
            set
            {
                if (m_indexes.TryGetValue(key, out int index))
                {
                    m_list[index] = new KeyValuePair<TKey, TValue>(key, value);
                }
            }
        }

        public bool Contains(TKey key)
        {
            return m_indexes.ContainsKey(key);
        }

        public int Count => m_indexes.Count;
        
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (m_indexes.TryGetValue(key, out int index))
            {
                KeyValuePair<TKey,TValue>? pair = m_list[index];
                if (pair.HasValue)
                {
                    value = pair.Value.Value;
                    return true;
                }
            }

            value = default(TValue);
            return false;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0, size = m_list.Count; i < size; ++i)
            {
                KeyValuePair<TKey, TValue>? pair = m_list[i];
                if (pair.HasValue)
                    yield return pair.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}