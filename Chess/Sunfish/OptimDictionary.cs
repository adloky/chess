using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chess.Sunfish {
    public class OptimDictionary<T> : IDictionary<char, T> {
        private int[] codes = Enumerable.Repeat(-1, 128).ToArray();
        private List<T> vals = new List<T>();

        public void Add(char key, T value) {
            codes[key] = vals.Count;
            vals.Add(value);
        }

        public T this[char key] { get => vals[codes[key]]; set => throw new NotImplementedException(); }

        public ICollection<char> Keys => throw new NotImplementedException();

        public ICollection<T> Values => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(KeyValuePair<char, T> item) { throw new NotImplementedException(); }

        public void Clear() { throw new NotImplementedException(); }

        public bool Contains(KeyValuePair<char, T> item) { throw new NotImplementedException(); }

        public bool ContainsKey(char key) { throw new NotImplementedException(); }

        public void CopyTo(KeyValuePair<char, T>[] array, int arrayIndex) { throw new NotImplementedException(); }

        public IEnumerator<KeyValuePair<char, T>> GetEnumerator() { throw new NotImplementedException(); }

        public bool Remove(char key) { throw new NotImplementedException(); }

        public bool Remove(KeyValuePair<char, T> item) { throw new NotImplementedException(); }

        public bool TryGetValue(char key, out T value) { throw new NotImplementedException(); }

        IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
    }

}
