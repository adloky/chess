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

        static Random rnd = new Random();

        public static SfZobrist New() {
            var bytes = new byte[16];
            rnd.NextBytes(bytes);
            var r = new SfZobrist();
            r.a = BitConverter.ToUInt64(bytes, 0);
            r.b = BitConverter.ToUInt64(bytes, 0);

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
    }

    public abstract class SfZobristBase {
        private SfZobristBase parent;

        public SfZobristBase(SfZobristBase parent = null) {
            this.parent = parent;
        }

        public void Xor(SfZobrist z) {
            Zobrist = Zobrist.Xor(z);
            if (parent != null) {
                parent.Xor(z);
            }
        }

        public SfZobrist Zobrist { get; private set; }
    }

    public class SfZobristInt : SfZobristBase {
        private int val;
        private SfZobrist[] zs;

        public SfZobristInt(SfZobrist[] zs, SfZobristBase parent = null) : base(parent) {
            this.zs = zs;
        }

        public int Value {
            get => val;
            set {
                if (val == value)
                    return;

                if (value > zs.Length)
                    throw new ArgumentOutOfRangeException();

                if (val != 0) {
                    Xor(zs[val-1]);
                }

                if (value != 0) {
                    Xor(zs[value-1]);
                }

                val = value;
            }
        }
    }

    public abstract class SfZobristArray<T> : SfZobristBase, IList<T> {
        protected IList<T> a;

        public SfZobristArray(IList<T> a, SfZobristBase parent = null) : base(parent) {
            this.a = a;
        }

        public int Count => a.Count;

        public virtual void SetValue(int index, T value) {
            a[index] = value;
        }

        public T this[int index] {
            get => a[index];
            set => SetValue(index, value);
        }

        #region Not Implemented
        public bool IsReadOnly => throw new NotImplementedException();
        public void Add(T item) { throw new NotImplementedException(); }
        public void Clear() { throw new NotImplementedException(); }
        public bool Contains(T item) { throw new NotImplementedException(); }
        public void CopyTo(T[] array, int arrayIndex) { throw new NotImplementedException(); }
        public IEnumerator<T> GetEnumerator() { throw new NotImplementedException(); }
        public int IndexOf(T item) { throw new NotImplementedException(); }
        public void Insert(int index, T item) { throw new NotImplementedException(); }
        public bool Remove(T item) { throw new NotImplementedException(); }
        public void RemoveAt(int index) { throw new NotImplementedException(); }
        IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
        #endregion
    }

    public class SfZobristBoolArray : SfZobristArray<bool> {
        private SfZobrist[] zs;
        public SfZobristBoolArray(SfZobrist[] zs, SfZobristBase parent = null) : base(new bool[zs.Length], parent) {
            this.zs = zs;
        }

        public override void SetValue(int index, bool value) {
            Xor(zs[index]);
            base.SetValue(index, value);
        }
    }

    public class SfZobristCharArray : SfZobristArray<char> {
        private Dictionary<char, SfZobrist[]> zd;
        public SfZobristCharArray(Dictionary<char,SfZobrist[]> zd, SfZobristBase parent = null) : base(new char[zd.First().Value.Length], parent) {
            this.zd = zd;
        }

        public override void SetValue(int index, char value) {
            if (a[index] == value)
                return;

            SfZobrist[] zs;
            if (zd.TryGetValue(a[index], out zs)) {
                Xor(zs[index]);
            }
            if (zd.TryGetValue(value, out zs)) {
                Xor(zs[index]);
            }

            base.SetValue(index, value);
        }
    }

}
