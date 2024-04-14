using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chess.Sunfish {
    internal class SF {
        public const int MATE_LOWER = 60000 - 11 * 900;
        public const int MATE_UPPER = 60000 + 11 * 900;
        public const int QS = 40;
        public const int QS_A = 140;
        public const int EVAL_ROUGHNESS = 15;

        public const int A1 = 91;
        public const int H1 = 98;
        public const int A8 = 21;
        public const int H8 = 28;

        public const int N = -10;
        public const int E = 1;
        public const int S = 10;
        public const int W = -1;

        static public readonly Dictionary<char, int[]> directions = new Dictionary<char, int[]>() {
            { 'P', new[] { N, N+N, N+W, N+E } },
            { 'B', new[] { N+E, S+E, S+W, N+W } },
            { 'N', new[] { N+N+E, E+N+E, E+S+E, S+S+E, S+S+W, W+S+W, W+N+W, N+N+W } },
            { 'R', new[] { N, E, S, W } },
            { 'Q', new[] { N, E, S, W, N+E, S+E, S+W, N+W } },
            { 'K', new[] { N, E, S, W, N+E, S+E, S+W, N+W } }
        };

        public const string INITIAL = "          "
                                    + "          "
                                    + " rnbqkbnr "
                                    + " pppppppp "
                                    + " ........ "
                                    + " ........ "
                                    + " ........ "
                                    + " ........ "
                                    + " PPPPPPPP "
                                    + " RNBQKBNR "
                                    + "          "
                                    + "          ";

        public static readonly Dictionary<char, List<int>> PST_B = new Dictionary<char, List<int>>();
        public static readonly Dictionary<char, List<int>> PST = new Dictionary<char, List<int>>() {
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

        static SF() {
            var pss = new Dictionary<char, int>() { { 'P', 100 }, { 'N', 280 }, { 'B', 320 }, { 'R', 479 }, { 'Q', 929 }, { 'K', 60000 } };
            foreach (var ps in pss) {
                var p = ps.Key;
                var s = ps.Value;
                var xs = PST[p];
                for (var i = 0; i < xs.Count; i++) {
                    xs[i] += s;
                }

                for (var i = xs.Count; i > 0; i -= 8) {
                    xs.Insert(i, 0);
                    xs.Insert(i - 8, 0);
                }

                xs.AddRange(Enumerable.Repeat(0, 20));
                xs.InsertRange(0, Enumerable.Repeat(0, 20));

                // mirror for blacks
                xs = xs.ToList();
                PST_B.Add(p, xs);
                for (var i = 0; i < xs.Count; i += 10) {
                    for (var j = 0; j < 5; j++) {
                        var tmp = xs[i + j];
                        xs[i + j] = xs[i + (9 - j)];
                        xs[i + (9 - j)] = tmp;
                    }
                }
            }

            /*
            foreach (var c in pss.Keys.Concat(pss.Keys.Select(x =>char.ToLower(x)))) {
                Console.WriteLine(c);
                for (var i = A8 - 1; i <= H1 + 1; i ++) {
                    Console.Write(string.Format("{0,6}", PST[c][i].ToString()));
                    if (i % 10 == 9) {
                        Console.WriteLine();
                    }
                }
            }
            */
        }

        private static void simple_pst_4_piece(char piece, int val) {
            var w = PST[piece];
            var b = PST[char.ToLower(piece)];
            for (var i = 0; i < w.Count; i++) {
                if (w[i] == 0) continue;
                w[i] = val;
                b[i] = val;
            }
        }

        public static void simple_pst() {
            simple_pst_4_piece('P', 100);
            simple_pst_4_piece('B', 300);
            simple_pst_4_piece('N', 300);
            simple_pst_4_piece('R', 500);
            simple_pst_4_piece('Q', 900);
            simple_pst_4_piece('K', 60000);
        }

        public static char swap_case(char c) {
            return !char.IsLetter(c) ? c : char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c);
        }
    }

    public struct SfMove {
        private int a;

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

        public SfMove Rotate() {
            var r = new SfMove(a);
            if (i != 0) 
                r.i = 119 - i;
            if (j != 0)
                r.j = 119 - j;
            return r;
        }

        public static SfMove Parse(string s) {
            int r = 0;
            for (var i = 0; i + 1 < 4 && i + 1 < s.Length; i += 2) {
                int file = s[i + 0] - 'a';
                int rank = s[i + 1] - '1';
                if (file < 0 || file > 7 || rank < 0 || rank > 7)
                    throw new FormatException();

                r += (SF.A1 + file - 10 * rank) << (i << 2);
            }

            if (s.Length > 4) {
                r += (PROM.IndexOf(s[4]) + 1) << 16;
            }

            return new SfMove(r);
        }

        public override string ToString() {
            var cs = new char[5];
            var n = 0;
            if (i != 0) {
                cs[n++] = (char)('a' + (i % 10) - 1);
                cs[n++] = (char)('1' - (i / 10) + 9);
                if (j != 0) {
                    cs[n++] = (char)('a' + (j % 10) - 1);
                    cs[n++] = (char)('1' - (j / 10) + 9);
                    if (k > 0) {
                        if (k > PROM.Length) 
                            throw new ArgumentOutOfRangeException();
                        
                        cs[n++] = PROM[k - 1];
                    }
                }
            }

            return new string(cs, 0, n);
        }
    }

    public abstract class SfSimpleList<T> : IList<T> {
        protected IList<T> a;

        public int Count => a.Count;

        public virtual T this[int index] {
            get => a[index];
            set => a[index] = value;
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

    public class SfTxArray<T> : SfSimpleList<T> {
        private List<(int i, T v)> ch = new List<(int, T)>();

        public override T this[int index] { get => a[index];
            set {
                if (!ch.Any(x => x.i == index)) {
                    if (value.Equals(a[index])) {
                        return;
                    }
                    ch.Add((index, a[index]));
                }
                a[index] = value;
            }
        }

        public SfTxArray(IList<T> a) { this.a = a; }

        public void Rollback() {
            foreach (var x in ch) {
                a[x.i] = x.v;
            }
            ch.Clear();
        }
    }

    public class SfReversed : SfSimpleList<char> {
        private SfPosition pos;

        public SfReversed(SfPosition pos, char[] a) {
            this.pos = pos;
            this.a = a;
        }

        public override char this[int index] {
            get => !pos.btm ? a[index] : SF.swap_case(a[119-index]);
            set {
                if (!pos.btm) {
                    a[index] = value;
                }
                else {
                    a[119 - index] = SF.swap_case(value);
                }
            }
        }

        public IList<char> Original { get => a; }
    }

    public class SfPosition {
        private SfReversed board;
        // public char[] board;

        private int _score;
        private (bool q, bool k) _wc;
        private (bool q, bool k) _bc;
        private int _ep;
        private int _kp;

        public bool btm;
        public int score { get => !btm ? _score : -_score; set => _score = (!btm) ? value : -value; }
        public (bool q, bool k) wc {
            get => !btm ? _wc : _bc;
            set { if (!btm) _wc = value; else _bc = value; } }
        public (bool q, bool k) bc {
            get => !btm ? _bc : _wc;
            set { if (!btm) _bc = value; else _wc = value; } }
        public int ep {
            get => !btm ? _ep : _ep == 0 ? 0 : 119 - _ep;
            set { if (!btm || value == 0) _ep = value; else _ep = 119 - value; }
        }
        public int kp {
            get => !btm ? _kp : _ep == 0 ? 0 : 119 - _kp;
            set { if (!btm || value == 0) _kp = value; else _kp = 119 - value; }
        }

        public SfPosition(IList<char> board, bool btm, int score, (bool, bool) wc, (bool, bool) bc, int ep, int kp, bool rotate = false) {
            this.board = new SfReversed(this, board.ToArray());
            this.btm = btm;
            this.score = score;
            this.wc = wc;
            this.bc = bc;
            this.ep = ep;
            this.kp = kp;

            if (rotate)
                this.btm = !btm;

            /*
            if (!Regex.IsMatch(string.Concat(this.board), "^ {20}( .{8} ){8} {20}$")) {
                throw new Exception();
            }
            */
        }

        public static SfPosition FromFen(string fen) {
            var fs = fen.Split(' ');
            var rotate = fs[1] == "b";
            var f0 = fs[0];
            for (var i = 8; i > 0; i--) {
                f0 = f0.Replace(i.ToString(), new string('.', i));
            }
            var board = $"{new string(' ', 20)}{string.Concat(f0.Split('/').Select(x => $" {x} "))}{new string(' ', 20)}";

            var wc = (fs[2].Contains("Q"), fs[2].Contains("K"));
            var bc = (fs[2].Contains("q"), fs[2].Contains("k"));

            var ep = fs[3] == "-" ? 0 : SfMove.Parse(fs[3]);

            var pos = new SfPosition(board.ToCharArray(), false, 0, wc, bc, ep, 0, rotate);
            pos.renew_scores();

            return pos;
        }

        public void renew_scores() {
            var score = 0;
            var pst = !btm ? SF.PST : SF.PST_B;
            var pst_op = btm ? SF.PST : SF.PST_B;
            for (var j = SF.A8; j <= SF.H1; j++) {
                var p = board[j];
                if (!char.IsLetter(p))
                    continue;

                var c = 1;
                var i = j;
                var _pst = pst;
                if (char.IsLower(p)) {
                    i = 119 - i;
                    c = -1;
                    _pst = pst_op;
                }

                score += _pst[char.ToUpper(p)][i] * c;
            }

            this.score = score;
        }

        private static readonly string dig = "0123456789";

        public override string ToString() {
            var cs = new char[100];
            var k = 0;
            var sn = 0;
            for (var i = SF.A8; i < SF.H1; i += SF.S) {
                var il = i + 8;
                for (var j = i; j < il; j++) {
                    var _j = !btm ? j : 119 - j;
                    if (board[_j] == '.') {
                        sn++;
                    }
                    else {
                        if (sn > 0) {
                            cs[k++] = dig[sn];
                            sn = 0;
                        }
                        cs[k++] = !btm ? board[_j] : SF.swap_case(board[_j]);
                    }
                }
                if (sn > 0) {
                    cs[k++] = dig[sn];
                    sn = 0;
                }
                cs[k++] = '/';
            }

            cs[k-1] = ' ';
            cs[k++] = btm ? 'b' : 'w';
            cs[k++] = ' ';

            var wc = btm ? this.bc : this.wc;
            var bc = btm ? this.wc : this.bc;
            if (wc.k) cs[k++] = 'K';
            if (wc.q) cs[k++] = 'Q';
            if (bc.k) cs[k++] = 'k';
            if (bc.q) cs[k++] = 'q';
            if (cs[k - 1] == ' ') cs[k++] = '-';
            cs[k++] = ' ';

            if (ep == 0) {
                cs[k++] = '-';
            }
            else {
                var epStr = (new SfMove(!btm ? ep : 119 - ep)).ToString();
                cs[k++] = epStr[0];
                cs[k++] = epStr[1];
            }

            var fen = new string(cs, 0, k);
            var kpStr = kp == 0 ? "-" : (new SfMove(!btm ? kp : 119 - kp)).ToString();

            return $"{fen} 0 1,{kpStr} {score}";
        }

        public IEnumerable<SfMove> gen_moves() {
            for (int i = 0; i < board.Count; i++) {
                var p = board[i];
                if (!char.IsUpper(p))
                    continue;

                foreach (var d in SF.directions[p]) {
                    for (var j = i + d; ; j += d) {
                        var q = board[j];

                        if (char.IsWhiteSpace(q) || char.IsUpper(q))
                            break;

                        if (p == 'P') {
                            if ((d == SF.N || d == SF.N + SF.N) && q != '.')
                                break;

                            if (d == SF.N + SF.N && (i < SF.A1 + SF.N || board[i + SF.N] != '.'))
                                break;

                            if ((d == SF.N + SF.W || d == SF.N + SF.E)
                              && q == '.'
                              && j != ep && Math.Abs(kp - j) > 1)
                                break;

                            if (SF.A8 <= j && j <= SF.H8) {
                                for (var k = 1; k <= 4; k++) {
                                    yield return new SfMove(i, j, k);
                                }
                                break;
                            }
                        }

                        yield return new SfMove(i, j, 0);

                        if (p == 'P' || p == 'N' || p == 'K' || char.IsLower(q))
                            break;

                        if (i == SF.A1 && board[j + SF.E] == 'K' && wc.q) {
                            yield return new SfMove(j + SF.E, j + SF.W);
                        }

                        if (i == SF.H1 && board[j + SF.W] == 'K' && wc.k) {
                            yield return new SfMove(j + SF.W, j + SF.E);
                        }
                    }
                }
            }
        }

        public SfPosition rotate(bool nullmove = false) {
            return new SfPosition(board.Original, btm, score, wc, bc, nullmove ? 0 : ep, nullmove ? 0 : kp, rotate: true);
        }

        public SfPosition move(SfMove m) {
            var i = m.i;
            var j = m.j;
            var prom = char.ToUpper(m.k == 0 ? 'q' : m.prom.Value);
            char p = this.board[i];
            var board = new SfTxArray<char>(this.board);
            var wc = this.wc;
            var bc = this.bc;
            var ep = 0;
            var kp = 0;
            var btm = this.btm;
            var score = this.score + value(m);

            if (i == SF.A1) wc = (false, wc.k);
            if (i == SF.H1) wc = (wc.q, false);
            if (i == SF.A8) wc = (bc.q, false);
            if (i == SF.H8) wc = (false, bc.k);

            board[j] = board[i];
            board[i] = '.';

            if (p == 'K') {
                wc = (false, false);
                if (Math.Abs(j - i) == 2) {
                    kp = (i + j) >> 1;
                    board[j < i ? SF.A1 : SF.H1] = '.';
                    board[kp] = 'R';
                }
            }

            if (p == 'P') {
                if (SF.A8 <= j && j <= SF.H8) {
                    board[j] = prom;
                }
                if (j - i == SF.N + SF.N && (board[j + SF.W] == 'p' || board[j + SF.E] == 'p')) {
                    ep = i + SF.N;
                }
                if (j == this.ep) {
                    board[j + SF.S] = '.';
                }
            }

            var r = new SfPosition(this.board.Original, btm, score, wc, bc, ep, kp, rotate: true);
            board.Rollback();

            return r;
        }

        public int value(SfMove m) {
            int i = m.i;
            int j = m.j;
            char p = board[i];
            char q = board[j];
            var prom = m.k == 0 ? 'Q' : char.ToUpper(m.prom.Value);
            var pst = !btm ? SF.PST : SF.PST_B;
            var pst_op = btm ? SF.PST : SF.PST_B;

            var score = pst[p][j] - pst[p][i];

            if (char.IsLower(q)) {
                score += pst_op[char.ToUpper(q)][119 - j];
            }

            if (Math.Abs(j - kp) <= 1) {
                score += pst_op['K'][119 - j];
            }

            if (p == 'K' && Math.Abs(i - j) == 2) {
                score += pst['R'][(i + j) / 2];
                score -= pst['R'][j < i ? SF.A1 : SF.H1];
            }

            if (p == 'P') {
                if (SF.A8 <= j && j <= SF.H8) {
                    score += pst[prom][j] - pst['P'][j];
                }
                if (j == ep) {
                    score += pst_op['P'][119 - (j + SF.S)];
                }
            }

            return score;
        }
    }

    public struct SfEntry {
        public int lower { get; set; }
        public int upper { get; set; }

        public SfEntry(int lower, int upper) {
            this.lower = lower;
            this.upper = upper;
        }
    }

     public static class Sunfish {
        static int nodes = 0;
        // tp_score key  (SfPosition pos, int depth, bool can_null)
        public static Dictionary<string, SfEntry> tp_score = new Dictionary<string, SfEntry>();
        public static Dictionary<string, SfMove> tp_move = new Dictionary<string, SfMove>();

        public static void SimplePst() {
            SF.simple_pst();
        }

        #region Diag
        enum Diag {
            TP_SCORE_GET,
            TP_SCORE_SIZE,
            TP_MOVE_GET,
            TP_MOVE_SIZE,
            TP_MOVE_UPDATE
        }

        static long[] DD = new long[Enum.GetValues(typeof(Diag)).Length];
        #endregion

        public static IEnumerable<(SfMove move, int score)> boundMoves(SfPosition pos, int gamma, int depth, bool can_null) {
            if (depth == 0)
                yield return (new SfMove(0), pos.score);
            else if (depth > 2 && can_null && Math.Abs(pos.score) < 500)
                yield return (new SfMove(0), -bound(pos.rotate(nullmove: true), 1 - gamma, depth - 3));

            SfMove killer = new SfMove(0);
            if (tp_move.TryGetValue(pos.ToString(), out killer)) {
                DD[(int)Diag.TP_MOVE_GET]++;
            }
            if (killer == 0 && depth > 2) {
                bound(pos, gamma, depth - 3, can_null: false);

                if (tp_move.TryGetValue(pos.ToString(), out killer)) {
                    DD[(int)Diag.TP_MOVE_GET]++;
                }
            }

            var val_lower = SF.QS - depth * SF.QS_A;

            if (killer != 0 && pos.value(killer) >= val_lower) {
                yield return (killer, -bound(pos.move(killer), 1 - gamma, depth - 1));
            }

            foreach (var vm in pos.gen_moves().Select(m => (val: pos.value(m), move: m)).OrderByDescending(x => x.val)) {
                var val = vm.val;
                var move = vm.move;

                if (val < val_lower)
                    break;

                if (depth <= 1 && pos.score + val < gamma) {
                    yield return (move, (val < SF.MATE_LOWER) ? pos.score + val : SF.MATE_UPPER);
                    break;
                }

                yield return (move, -bound(pos.move(move), 1 - gamma, depth - 1));
            }
        }

        public static int bound(SfPosition pos, int gamma, int depth, bool can_null = true) {
            nodes++;
            if (nodes % 100000 == 0) {
                DD[(int)Diag.TP_SCORE_SIZE] = tp_score.Count;
                DD[(int)Diag.TP_MOVE_SIZE] = tp_move.Count;
                for (var ddi = 0; ddi < DD.Length; ddi++) {
                    var i = (Diag)ddi;
                    Console.WriteLine($"{i}: {DD[ddi] / (nodes / 100000)}");
                }
            }

            depth = Math.Max(depth, 0);
            if (pos.score <= -SF.MATE_LOWER) {
                return -SF.MATE_UPPER;
            }

            SfEntry entry;
            if (!tp_score.TryGetValue((pos, depth, can_null).ToString(), out entry)) {
                entry = new SfEntry(-SF.MATE_UPPER, SF.MATE_UPPER);
            }
            else {
                DD[(int)Diag.TP_SCORE_GET]++;
            }
            if (entry.lower >= gamma)
                return entry.lower;
            if (entry.upper < gamma)
                return entry.upper;

            var best = -SF.MATE_UPPER;
            foreach (var ms in boundMoves(pos, gamma, depth, can_null)) {
                var move = ms.move;
                var score = ms.score;

                best = Math.Max(best, score);
                if (best >= gamma) {
                    if (move != 0) {
                        SfMove ddMove;
                        var pos_key = pos.ToString();
                        if (tp_move.TryGetValue(pos_key, out ddMove) && ddMove != move) {
                            DD[(int)Diag.TP_MOVE_UPDATE]++;
                        }

                        tp_move[pos_key] = move; // depth > 2
                    }
                    break;
                }
            }

            if (depth > 2 && best == -SF.MATE_UPPER) {
                var flipped = pos.rotate(nullmove: true);
                var in_check = bound(flipped, SF.MATE_UPPER, 0) == SF.MATE_UPPER;
                best = in_check ? -SF.MATE_LOWER : 0;
            }

            var newEntry = best >= gamma ? new SfEntry(best, entry.upper) : new SfEntry(entry.lower, best);
            tp_score[(pos, depth, can_null).ToString()] = newEntry;

            return best;
        }

        public static IEnumerable<(int depth, int gamma, int score, SfMove move)> search(SfPosition pos, int maxdepth = -1) {
            nodes = 0;
            tp_score.Clear();
            var gamma = 0;
            if (maxdepth == -1) maxdepth = 1000;
            for (var depth = 1; depth <= maxdepth; depth++) {
                var lower = -SF.MATE_LOWER;
                var upper = SF.MATE_LOWER;
                while (lower < upper - SF.EVAL_ROUGHNESS) {
                    var score = bound(pos, gamma, depth, can_null: false);
                    if (score >= gamma) {
                        lower = score;
                    }
                    else {
                        upper = score;
                    }
                    SfMove move;
                    tp_move.TryGetValue(pos.ToString(), out move);
                    yield return (depth, gamma, score, move);
                    gamma = (lower + upper + 1) / 2;
                }
            }
        }

        public static IEnumerable<(int depth, int nodes, int score, string pv)> search(string fen, int maxdepth = -1) {
            var pos = SfPosition.FromFen(fen);
            var c = fen.Contains(" w ") ? 0 : 1;
            foreach (var r in search(pos, maxdepth)) {
                if (r.score < r.gamma)
                    continue;

                var pi = pos;
                var moves = new List<SfMove>();
                for (var i = 0; i < r.depth; i++) {
                    SfMove move;
                    tp_move.TryGetValue(pi.ToString(), out move);

                    if (move == 0)
                        break;

                    moves.Add(move);
                    pi = pi.move(move);
                }

                var pv = (string)null;
                if (moves.Count > 0) {
                    pv = string.Join(" ", moves.Take(r.depth).Select((m, i) => (i % 2 == c) ? m.ToString() : m.Rotate().ToString()));
                }

                yield return (r.depth, nodes, r.score, pv);
            }
        }
    }
}
