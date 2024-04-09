using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess.Sunfish {
    internal class Sf {
        //static public readonly int TABLE_SIZE = 10000000;
        //static public readonly int NODES_SEARCHED = 10000;
        //static public readonly int MATE_VALUE = 30000;
        static public readonly int MATE_LOWER;
        static public readonly int MATE_UPPER;
        static public readonly int QS = 40;
        static public readonly int QS_A = 140;
        static public readonly int EVAL_ROUGHNESS = 15;

        static public readonly int A1 = 91;
        static public readonly int H1 = 98;
        static public readonly int A8 = 21;
        static public readonly int H8 = 28;

        static public readonly char[] initilal = SfPosition.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1").board;

        static public readonly int N = -10;
        static public readonly int E = 1;
        static public readonly int S = 10;
        static public readonly int W = -1;

        static public readonly Dictionary<char, int[]> directions = new Dictionary<char, int[]>() {
            { 'P', new[] { N, N+N, N+W, N+E } },
            { 'B', new[] { N+E, S+E, S+W, N+W } },
            { 'N', new[] { N+N+E, E+N+E, E+S+E, S+S+E, S+S+W, W+S+W, W+N+W, N+N+W } },
            { 'R', new[] { N, E, S, W } },
            { 'Q', new[] { N, E, S, W, N+E, S+E, S+W, N+W } },
            { 'K', new[] { N, E, S, W, N+E, S+E, S+W, N+W } }
        };

        static public readonly Dictionary<char, List<int>> pst = new Dictionary<char, List<int>>() {
            { 'P', new List<int>() {
                0,   0,   0,   0,   0,   0,   0,   0,
               78,  83,  86,  73, 102,  82,  85,  90,
                7,  29,  21,  44,  40,  31,  44,   7,
              -17,  16,  -2,  15,  14,   0,  15, -13,
              -26,   3,  10,   9,   6,   1,   0, -23,
              -22,   9,   5, -11, -10,  -2,   3, -19,
              -31,   8,  -7, -37, -36, -14,   3, -31,
                0,   0,   0,   0,   0,   0,   0,   0
            } },
            { 'N', new List<int>() {
              -66, -53, -75, -75, -10, -55, -58, -70,
               -3,  -6, 100, -36,   4,  62,  -4, -14,
               10,  67,   1,  74,  73,  27,  62,  -2,
               24,  24,  45,  37,  33,  41,  25,  17,
               -1,   5,  31,  21,  22,  35,   2,   0,
              -18,  10,  13,  22,  18,  15,  11, -14,
              -23, -15,   2,   0,   2,   0, -23, -20,
              -74, -23, -26, -24, -19, -35, -22, -69
            } },
            { 'B', new List<int>() {
              -59, -78, -82, -76, -23,-107, -37, -50,
              -11,  20,  35, -42, -39,  31,   2, -22,
               -9,  39, -32,  41,  52, -10,  28, -14,
               25,  17,  20,  34,  26,  25,  15,  10,
               13,  10,  17,  23,  17,  16,   0,   7,
               14,  25,  24,  15,   8,  25,  20,  15,
               19,  20,  11,   6,   7,   6,  20,  16,
               -7,   2, -15, -12, -14, -15, -10, -10
            } },
            { 'R', new List<int>() {
               35,  29,  33,   4,  37,  33,  56,  50,
               55,  29,  56,  67,  55,  62,  34,  60,
               19,  35,  28,  33,  45,  27,  25,  15,
                0,   5,  16,  13,  18,  -4,  -9,  -6,
              -28, -35, -16, -21, -13, -29, -46, -30,
              -42, -28, -42, -25, -25, -35, -26, -46,
              -53, -38, -31, -26, -29, -43, -44, -53,
              -30, -24, -18,   5,  -2, -18, -31, -32
            } },
            { 'Q', new List<int>() {
                6,   1,  -8,-104,  69,  24,  88,  26,
               14,  32,  60, -10,  20,  76,  57,  24,
               -2,  43,  32,  60,  72,  63,  43,   2,
                1, -16,  22,  17,  25,  20, -13,  -6,
              -14, -15,  -2,  -5,  -1, -10, -20, -22,
              -30,  -6, -13, -11, -16, -11, -16, -27,
              -36, -18,   0, -19, -15, -15, -21, -38,
              -39, -30, -31, -13, -31, -36, -34, -42
            } },
            { 'K', new List<int>() {
                4,  54,  47, -99, -99,  60,  83, -62,
              -32,  10,  55,  56,  56,  55,  10,   3,
              -62,  12, -57,  44, -67,  28,  37, -31,
              -55,  50,  11,  -4, -19,  13,   0, -49,
              -55, -43, -52, -28, -51, -47,  -8, -50,
              -47, -42, -43, -79, -64, -32, -29, -32,
               -4,   3, -14, -50, -57, -18,  13,   4,
               17,  30,  -3, -14,   6,  -1,  40,  18
            } }
        };

        //public static readonly Dictionary<SfPosition, SfEntry> tp = new Dictionary<SfPosition, SfEntry>();

        static Sf() {
            var pss = new Dictionary<char, int>() { { 'P', 100 }, { 'N', 280 }, { 'B', 320 }, { 'R', 479 }, { 'Q', 929 }, { 'K', 60000 } };
            foreach (var ps in pss) {
                var p = ps.Key;
                var s = ps.Value;
                var xs = pst[p];
                for (var i = 0; i < xs.Count; i++) {
                    xs[i] += s;
                }

                for (var i = xs.Count; i > 0; i -= 8) {
                    xs.Insert(i, 0);
                    xs.Insert(i - 8, 0);
                }

                xs.InsertRange(0, Enumerable.Repeat(0, 20));
                xs.AddRange(Enumerable.Repeat(0, 20));
            }

            MATE_LOWER = pss['K'] - 10 * pss['Q'];
            MATE_UPPER = pss['K'] + 10 * pss['Q'];
        }

        private static void simplePst4Piece(char piece, int val) {
            var a = pst[piece];
            for (var i = 0; i < a.Count; i++) {
                if (a[i] == 0) continue;
                a[i] = val;
            }
        }

        public static void SimplePst() {
            simplePst4Piece('P', 100);
            simplePst4Piece('B', 300);
            simplePst4Piece('N', 300);
            simplePst4Piece('R', 500);
            simplePst4Piece('Q', 900);
            simplePst4Piece('K', 60000);
        }
    }

    /*
    internal class SfEntry {
        public int depth = -1;
        public int score = -1;
        public int gamma = -1;
        public Tuple<int, int> bmove = new Tuple<int, int>(-1,-1);

        public SfEntry() {}

        public SfEntry(int depth, int score, int gamma, Tuple<int, int> bmove) {
            this.depth = depth; this.score = score; this.gamma = gamma; this.bmove = bmove;
        }
    }
    */

    public struct SfMove {
        int a;

        private static readonly string PROM = "qrbn";

        public int i {
            get => (a >> 0) & 0xFF;
            set => a = (a & 0xFFFF00) | ((value & 0xFF) << 0);
        }

        public int j {
            get => (a >> 8) & 0xFF;
            set => a = (a & 0xFF00FF) | ((value & 0xFF) << 8);
        }

        public int k {
            get => (a >> 16) & 0xFF;
            set => a = (a & 0x00FFFF) | ((value & 0xFF) << 16);
        }

        public char? prom { get => (k == 0) ? (char?)null : PROM[k - 1]; }

        public static implicit operator int(SfMove m) => m.a;

        public SfMove(int a) {
            this.a = a;
        }

        public SfMove(int i, int j, int k) {
            a = 0;
            this.i = i;
            this.j = j;
            this.k = k;
        }

        public SfMove(int i, int j) : this(i, j, 0) { }

        public static SfMove Parse(string s) {
            int r = 0;
            for (var i = 0; i + 1 < 4 && i + 1 < s.Length; i += 2) {
                int file = s[i + 0] - 'a';
                int rank = s[i + 1] - '1';
                if (file < 0 || file > 7 || rank < 0 || rank > 7)
                    throw new FormatException();

                r += (Sf.A1 + file - 10 * rank) << (i << 2);
            }

            if (s.Length > 4) {
                r += (PROM.IndexOf(s[4]) + 1) << 16;
            }

            return new SfMove(r);
        }

        public override string ToString() {
            var cs = new char[5];
            var k = 0;
            if (i != 0) {
                cs[k++] = (char)('a' + (i % 10) - 1);
                cs[k++] = (char)('1' - (i / 10) + 9);
                if (j != 0) {
                    cs[k++] = (char)('a' + (j % 10) - 1);
                    cs[k++] = (char)('1' - (j / 10) + 9);
                    if (k != 0 && k <= PROM.Length) {
                        cs[k++] = PROM[k - 1];
                    }
                }
            }

            return new string(cs, 0, k);
        }
    }

    public class SfPartList<T> : IEnumerable<T> {
        bool sorted = true;
        T[] a;
        List<(int i, T v)> ch = new List<(int, T)>(4);

        public SfPartList(T[] a) {
            this.a = a;
        }

        public T this[int index] {
            get {
                var ci = ch.Where(x => x.i == index).Select((x,i) => (int?)i).FirstOrDefault();
                return ci == null ? a[index] : ch[ci.Value].v;
            }
            set {
                var ci = ch.Where(x => x.i == index).Select(x => (int?)x.i).FirstOrDefault();
                if (ci == null) {
                    ch.Add((index, value));
                    sorted = false;
                }
                else {
                    ch[ci.Value] = (index,value);
                }
            }
        }

        private IEnumerable<T> enumerate() {
            if (!sorted) { ch.Sort((a, b) => a.i.CompareTo(b.i)); sorted = true; }
            if (ch.Count == 0) {
                foreach (var x in a) {
                    yield return x;
                }
                yield break;
            }

            var j = 0;
            for (var i = 0; i < ch.Count; i++) {
                for (; j < ch[i].i; j++) {
                    yield return a[j];
                }
                yield return ch[i].v;
                j++;
            }
            
            for (; j < a.Length; j++) {
                yield return a[j];
            }
        }

        public IEnumerator<T> GetEnumerator() {
            return enumerate().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public class SfPosition {
        public char[] board;
        public int score;
        public (bool q, bool k) wc;
        public (bool q, bool k) bc;
        int ep;
        int kp;

        public SfPosition(IEnumerable<char> board, int score, (bool, bool) wc, (bool, bool) bc, int ep, int kp, bool rotate = false, bool nullmove = false) {
            var bs = new char[120];
            var i = !rotate ? 0 : 119;
            if (!rotate) {
                foreach (var b in board) { bs[i] = b; i++; }
                this.score = score;
                this.wc = wc;
                this.bc = bc;
                this.ep = ep;
                this.kp = kp;
            }
            else {
                foreach (var b in board) { bs[i] = b < 'A' ? b : b <= 'Z' ? char.ToLower(b) : char.ToUpper(b); i--; }
                this.score = -score;
                this.wc = bc;
                this.bc = wc;
                this.ep = (ep != 0 && !nullmove) ? 119 - ep : 0;
                this.kp = (kp != 0 && !nullmove) ? 119 - kp : 0;
            }

            this.board = bs;
        }

        private static readonly string space20 = new string(' ', 20);
        public static SfPosition FromFen(string fen, bool rotate = false) {
            var fs = fen.Split(' ');
            var f0 = fs[0];
            for (var i = 8; i > 0; i--) {
                f0 = f0.Replace(i.ToString(), new string('.', i));
            }
            var board = $"{space20}{string.Concat(f0.Split('/').Select(x => $" {x} "))}{space20}";

            var wc = (fs[2].Contains("Q"), fs[2].Contains("K"));
            var bc = (fs[2].Contains("q"), fs[2].Contains("k"));

            var ep = SfMove.Parse(fs[3]);

            return new SfPosition(board.ToCharArray(), 0, wc, bc, ep, 0, rotate);
        }


        private static readonly string dig = "0123456789";

        public override string ToString() {
            var cs = new char[100];
            var k = 0;
            var sn = 0;
            for (var i = Sf.A8; i < Sf.H1; i += 10) {
                var il = i + 8;
                for (var j = i; j < il; j++) {
                    if (board[j] == '.') {
                        sn++;
                    }
                    else {
                        if (sn > 0) {
                            cs[k++] = dig[sn];
                            sn = 0;
                        }
                        cs[k++] = board[j];
                    }
                }
                if (sn > 0) {
                    cs[k++] = dig[sn];
                    sn = 0;
                }
                cs[k++] = '/';
            }

            var bStr = new string(cs, 0, k-1);

            k = 0;
            if (wc.k) cs[k++] = 'K';
            if (wc.q) cs[k++] = 'Q';
            if (bc.k) cs[k++] = 'k';
            if (bc.q) cs[k++] = 'q';

            var cStr = k == 0 ? "-" : new string(cs, 0, k);
            var epStr = ep == 0 ? "-" : (new SfMove(ep)).ToString();
            var kpStr = kp == 0 ? "-" : (new SfMove(kp)).ToString();

            return $"{bStr} {cStr} {epStr} {kpStr} {score}";
        }

        public IEnumerable<int> gen_moves() {
            for (int i = 0; i < board.Length; i++) {
                var p = board[i];
                if (char.IsUpper(p))
                    continue;

                foreach (var d in Sf.directions[p]) {
                    for (var j = i + d; ; j += d) {
                        var q = board[j];

                        if (char.IsWhiteSpace(q) || char.IsUpper(q))
                            break;

                        if (p == 'P') {
                            if ((d == Sf.N || d == Sf.N + Sf.N) && q != '.')
                                break;

                            if (d == Sf.N + Sf.N && (i < Sf.A1 + Sf.N || board[i + Sf.N] != '.'))
                                break;

                            if ((d == Sf.N + Sf.W || d == Sf.N + Sf.E)
                              && q == '.'
                              && j != ep && Math.Abs(kp - j) > 1)
                                break;

                            if (Sf.A8 <= j && j <= Sf.H8) {
                                for (var k = 1; k <= 4; k++) {
                                    yield return new SfMove(i, j, k);
                                }
                                break;
                            }
                        }

                        yield return new SfMove(i, j, 0);

                        if (p =='P' || p =='N' || p == 'K' || char.IsLower(q))
                            break;

                        if (i == Sf.A1 && board[j + Sf.E] == 'K' && wc.q) {
                            yield return new SfMove(j + Sf.E, j + Sf.W);
                        }

                        if (i == Sf.H1 && board[j + Sf.W] == 'K' && wc.k) {
                            yield return new SfMove(j + Sf.W, j + Sf.E);
                        }
                    }
                }
            }
        }

        public SfPosition rotate(bool nullmove = false) {
            return new SfPosition(board, score, wc, bc, ep, kp, rotate: true, nullmove);
        }

        public SfPosition move(SfMove m) {
            var i = m.i;
            var j = m.j;
            var prom = char.ToUpper(m.k == 0 ? 'q' : m.prom.Value);
            char p = this.board[i];
            char q = this.board[j];
            var board = new SfPartList<char>(this.board);
            var wc = this.wc;
            var bc = this.bc;
            var ep = 0;
            var kp = 0;
            var score = this.score + value(m);

            if (i == Sf.A1) wc = (false, wc.k);
            if (i == Sf.H1) wc = (wc.q, false);
            if (i == Sf.A8) wc = (bc.q, false);
            if (i == Sf.H8) wc = (false, bc.k);

            board[j] = board[i];
            board[i] = '.';

            if (p == 'K') {
                wc = (false, false);
                if (Math.Abs(j - i) == 2) {
                    kp = (i + j) >> 1;
                    board[j < i ? Sf.A1 : Sf.H1] = '.';
                    board[kp] = 'R';
                }
            }

            if (p == 'P') {
                if (Sf.A8 <= j && j <= Sf.H8) {
                    board[j] = prom;
                }
                if (j - i == Sf.N + Sf.N && (board[j + Sf.W] == 'p' || board[j + Sf.E] == 'p')) {
                    ep = i + Sf.N;
                }
                if (j == this.ep) {
                    board[j + Sf.S] = '.';
                }
            }

            return new SfPosition(board, score, wc, bc, ep, kp, rotate: true);
        }

        public int value(SfMove m) {
            int i = m.i;
            int j = m.j;
            char p = board[i];
            char q = board[j];
            var prom = m.k == 0 ? 'Q' : char.ToUpper(m.prom.Value);

            var score = Sf.pst[p][j] - Sf.pst[char.ToUpper(p)][i];

            if (char.IsLower(q)) {
                score += Sf.pst[char.ToUpper(q)][119 - j];
            }

            if (Math.Abs(j - kp) <= 1) {
                score += Sf.pst['K'][119 - j];
            }

            if (p == 'K' && Math.Abs(i - j) == 2) {
                score += Sf.pst['R'][(i + j)/2];
                score -= Sf.pst['R'][j < i ? Sf.A1 : Sf.H1];
            }

            if (p == 'P') {
                if (Sf.A8 <= j && j <= Sf.H8) {
                    score += Sf.pst[prom][j] - Sf.pst['P'][j];
                }
                if (j == ep) {
                    score += Sf.pst['P'][119 - (j + Sf.S)];
                }
            }

            return score;
        }

        /*
        static int nodes = 0;
        static int bound(SfPosition pos, int gamma, int depth, Tuple<int,int> exMove = null) {
            nodes += 1;

            if (!Sf.tp.ContainsKey(pos)) {
                Sf.tp.Add(pos, new SfEntry());
            }
            SfEntry initEntry = Sf.tp[pos];
            if (initEntry.depth != -1 && initEntry.depth >= depth && (initEntry.score < initEntry.gamma && initEntry.score < gamma || initEntry.score >= initEntry.gamma && initEntry.score >= gamma)) {
                return initEntry.score;
            }

            if (Math.Abs(pos.score) >= Sf.MATE_VALUE) {
                return pos.score;
            }

            int nullscore = (depth > 0) ? -bound(pos.rotate(), 1 - gamma, depth - 3) : pos.score;

            if (nullscore >= gamma) {
                return nullscore;
            }

            int best = -3 * Sf.MATE_VALUE;
            Tuple<int, int> bmove = new Tuple<int, int>(0,0); // ??
            // not able to do the sorting by value thing here yet. TODO
            List<Tuple<int, int>> moves = pos.genMoves();
            for (int i = 0; i < moves.Count; i++) {
                var move = moves[i];
                if (exMove != null && exMove.Item1 == move.Item1 && exMove.Item2 == move.Item2) {
                    continue;
                }
                if (depth <= 0 && pos.value(move) < 150) {
                    break;
                }

                int score = -bound(pos.move(move), 1 - gamma, depth - 1);
                if (score > best) {
                    best = score;
                    bmove = move;
                }

                if (score >= gamma) {
                    break;
                }
            }

            if (depth <= 0 && best < nullscore) {
                return nullscore;
            }

            // Look at this again. No idea what is happening here with the is None.
            if (depth > 0 && best <= -Sf.MATE_VALUE && nullscore > -Sf.MATE_VALUE) {
                best = 0;
            }

            if (initEntry.depth == -1 || depth >= initEntry.depth && best >= gamma) {
                Sf.tp[pos] = new SfEntry(depth, best, gamma, bmove);
                if (Sf.tp.Count > Sf.TABLE_SIZE) {
                    Sf.tp.Remove(Sf.tp.Last().Key); // TODO change since not sorted
                }
            }
            return best; // returns integer score
        }

        static public Tuple<Tuple<int, int>, int> search(SfPosition pos, int maxn = -1, int maxd = -1, Tuple<int, int> exMove = null) {
            if (maxn == -1) {
                maxn = Sf.NODES_SEARCHED;
            }
            if (maxd == -1) {
                maxd = 99;
            }

            nodes = 0;
            int score = 0;
            Sf.tp.Clear();

            // Limit depth of search to a constant 99 so stack overflow isn't achieved.
            for (int depth = 1; depth <= maxd; depth++)
            {
                // Inner loop is a binary search on the score of the position
                // Inv: lower <= score <= upper
                // This can be broken by values from the transposition table since they don't have the same concept of p(score).
                // As a result lower < upper - margin is used as the loop condition.
                int lower = -3 * Sf.MATE_VALUE;
                int upper = 3 * Sf.MATE_VALUE;
                while (lower < (upper - 3))
                {
                    int gamma = (lower + upper + 1) / 2;
                    score = bound(pos, gamma, depth, exMove);
                    if (score >= gamma) {
                        lower = score;
                    }
                    if (score < gamma) {
                        upper = score;
                    }
                }

                // Stop increasing depth of search if global counter indicates too much time spent or if game is won
                if (nodes >= maxn || Math.Abs(score) >= Sf.MATE_VALUE) {
                    break;
                }
            }

            // If game hasn't finished retrieve move from transposition table
            SfEntry entry = Sf.tp[pos];
            if (entry.depth != -1) {
                return new Tuple<Tuple<int, int>, int>(entry.bmove, score);
            }

            return new Tuple<Tuple<int, int>, int>(new Tuple<int,int>(0, 0), score);
        }
        
        static public Tuple<int, int> parse(string sMove) {
            // calculate starting position
            int[] pos = { 0, 0 };
            for (var i = 0; i + 1 < 4 && i + 1 < sMove.Length; i+=2) {
                int file = sMove[i + 0] - 'a';
                int rank = sMove[i + 1] - '1';
                pos[i/2] = Sf.A1 + file - 10 * rank;
            }

            return new Tuple<int,int>(pos[0], pos[1]);
        }

        static public string tuple2move(Tuple<int,int> move, bool rotate = false) {
            int[] p = new int[] { !rotate ? move.Item1 : 119 - move.Item1, !rotate ? move.Item2 : 119 - move.Item2 };
            char[] r = { ' ', ' ', ' ', ' ' };
            for (var i = 0; i < 4; i += 2) {
                r[i]     = (char)('a' + (p[i / 2] % 10) - 1);
                r[i + 1] = (char)('1' - (p[i / 2] / 10) + 9);
            }
            return string.Join("", r);
        }
        */
    }

    public static class Sunfish {
        public static void SimplePst()
        {
            Sf.SimplePst();
        }
    }
}
