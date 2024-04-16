using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess.Sunfish {
    public struct SfZobrist {
        ulong a;
        ulong b;

        static Random rnd = new Random(0);

        public static SfZobrist New() {
            var bytes = new byte[16];
            rnd.NextBytes(bytes);
            var r = new SfZobrist();
            r.a = BitConverter.ToUInt64(bytes, 0);
            r.b = BitConverter.ToUInt64(bytes, 8);

            return r;
        }

        public static SfZobrist[] NewArray(int size) {
            var r = new SfZobrist[size];
            for (var i = 0; i < size; i++) {
                r[i] = New();
            }

            return r;
        }

        public SfZobrist Xor(SfZobrist z) {
            var r = this;
            r.a ^= z.a;
            r.b ^= z.b;

            return r;
        }

        public override string ToString() {
            return a.ToString("X") + b.ToString("X");
        }

        public override int GetHashCode() {
            return unchecked((int)a);
        }
    }

    public class SfZobristIntArray : IList<int> {
        private int[] vals;
        private int[] init;
        private List<SfZobrist[]> z;
        private int zSize;
        private List<(int i, int v)> chs = new List<(int, int)>(8);

        public int Count => vals.Length;

        public SfZobrist Zobrist { get; set; }

        private SfZobristIntArray() { }

        public SfZobristIntArray(List<SfZobrist[]> z, int[] vals, int[] init, int zSize) {
            this.z = z;
            this.vals = vals;
            this.init = init;
            this.zSize = zSize;
        }

        public int this[int index] {
            get => vals[index];
            set {
                var val = vals[index];
                if (val == value)
                    return;

                if (index < zSize) {
                    if (val != init[index]) {
                        Zobrist = Zobrist.Xor(z[index][val]);
                    }
                    if (value != init[index]) {
                        Zobrist = Zobrist.Xor(z[index][value]);
                    }
                }

                if (!chs.Any(x => x.i == index))
                    chs.Add((index, val));
                
                vals[index] = value;
            }
        }

        public Changes PopChanges() {
            var r = new Changes(chs);
            chs = new List<(int, int)>(8);

            return r;
        }

        public void Rollback(Changes zch) {
            if (this.chs.Count > 0)
                throw new Exception("Change list not empty.");

            foreach (var ch in zch.chs)
                this[ch.i] = ch.v;

            PopChanges();
        }

        public SfZobristIntArray Clone() {
            var r = new SfZobristIntArray();
            r.z = z;
            r.vals = vals.ToArray();
            r.init = init;
            r.zSize = zSize;
            r.Zobrist = Zobrist;

            return r;
        }

        public struct Changes {

            internal List<(int i, int v)> chs;

            public Changes(List<(int i, int v)> chs) {
                this.chs = chs;
            }
        }

        #region Not Implemented
        public bool IsReadOnly => throw new NotImplementedException();
        public void Add(int item) { throw new NotImplementedException(); }
        public void Clear() { throw new NotImplementedException(); }
        public bool Contains(int item) { throw new NotImplementedException(); }
        public void CopyTo(int[] array, int arrayIndex) { throw new NotImplementedException(); }
        public IEnumerator<int> GetEnumerator() { throw new NotImplementedException(); }
        public int IndexOf(int item) { throw new NotImplementedException(); }
        public void Insert(int index, int item) { throw new NotImplementedException(); }
        public bool Remove(int item) { throw new NotImplementedException(); }
        public void RemoveAt(int index) { throw new NotImplementedException(); }
        IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
        #endregion
    }
}
