using System;
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

    public class SfZobristInt {
        private int val;
        private SfZobrist[] zs;

        public SfZobrist Zobrist { get; set; }

        public SfZobristInt(SfZobrist[] zs) {
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
                    Zobrist = Zobrist.Xor(zs[val-1]);
                }

                if (value != 0) {
                    Zobrist = Zobrist.Xor(zs[value-1]);
                }

                val = value;
            }
        }
    }
}
