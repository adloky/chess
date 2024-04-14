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

    public interface ISfZobristContainer {
        SfZobrist Zobrist { get; set; }
    }

    public abstract class SfZobristBase {
        private ISfZobristContainer iz;
        private ISfZobristContainer parent;

        public SfZobristBase(ISfZobristContainer parent = null) {
            this.parent = parent;
            this.iz = this as ISfZobristContainer;
        }

        public void Xor(SfZobrist z) {
            if (iz != null) {
                iz.Zobrist = iz.Zobrist.Xor(z);
            }

            if (parent != null) {
                parent.Zobrist = parent.Zobrist.Xor(z);
            }
        }
    }

    public class SfZobristInt : SfZobristBase {
        private int val;
        private SfZobrist[] zs;

        public SfZobristInt(SfZobrist[] zs, ISfZobristContainer parent = null) : base(parent) {
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

        public SfZobristArray(IList<T> a, ISfZobristContainer parent = null) : base(parent) {
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
        public SfZobristBoolArray(SfZobrist[] zs, ISfZobristContainer parent = null) : base(new bool[zs.Length], parent) {
            this.zs = zs;
        }

        public override void SetValue(int index, bool value) {
            Xor(zs[index]);
            base.SetValue(index, value);
        }
    }

    public class SfZobristCharArray : SfZobristArray<char> {
        private Dictionary<char, SfZobrist[]> zd;
        public SfZobristCharArray(Dictionary<char,SfZobrist[]> zd, int size, ISfZobristContainer parent = null) : base(new char[size], parent) {
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

    public class SfZobristContainerNCharArray : SfZobristCharArray, ISfZobristContainer {

        public SfZobrist Zobrist { get; set; }

        public SfZobristContainerNCharArray(Dictionary<char, SfZobrist[]> zd, int size, ISfZobristContainer parent = null) : base(zd, size, parent) {}
    }
}
