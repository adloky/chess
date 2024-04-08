using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess.Sunfish
{
    internal class Sf {
        static public readonly int TABLE_SIZE = 10000000;
        static public readonly int NODES_SEARCHED = 10000;
        static public readonly int MATE_VALUE = 30000;

        static public readonly int A1 = 91;
        static public readonly int H1 = 98;
        static public readonly int A8 = 21;
        static public readonly int H8 = 28;

        static public readonly char[] initilal = "                     rnbqkbnr  pppppppp  ........  ........  ........  ........  PPPPPPPP  RNBQKBNR                    ".ToCharArray();

        static public readonly int N = -10;
        static public readonly int E = 1;
        static public readonly int S = 10;
        static public readonly int W = -1;

        static public readonly Dictionary<char, int[]> directions = new Dictionary<char, int[]>() {
            { 'P', new[] { N, 2 * N, N + W, N + E } },
            { 'B', new[] { N + E, S + E, S + W, N + W } },
            { 'N', new[] { 2 * N + E, N + 2 * E, S + 2 * E, 2 * S + E, 2 * S + W, S + 2 * W, N + 2 * W, 2 * N + W } },
            { 'R', new[] { N, E, S, W } },
            { 'Q', new[] { N, E, S, W, N + E, S + E, S + W, N + W } },
            { 'K', new[] { N, E, S, W, N + E, S + E, S + W, N + W } }
        };

        static public readonly Dictionary<char, int[]> pst = new Dictionary<char, int[]>() {
            { 'P', new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 198, 198, 198, 198, 198, 198, 198, 198, 0, 0, 178, 198, 198, 198, 198, 198, 198, 178, 0, 0, 178, 198, 198, 198, 198, 198, 198, 178, 0, 0, 178, 198, 208, 218, 218, 208, 198, 178, 0, 0, 178, 198, 218, 238, 238, 218, 198, 178, 0, 0, 178, 198, 208, 218, 218, 208, 198, 178, 0, 0, 178, 198, 198, 198, 198, 198, 198, 178, 0, 0, 198, 198, 198, 198, 198, 198, 198, 198, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
            { 'B', new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 797, 824, 817, 808, 808, 817, 824, 797, 0, 0, 814, 841, 834, 825, 825, 834, 841, 814, 0, 0, 818, 845, 838, 829, 829, 838, 845, 818, 0, 0, 824, 851, 844, 835, 835, 844, 851, 824, 0, 0, 827, 854, 847, 838, 838, 847, 854, 827, 0, 0, 826, 853, 846, 837, 837, 846, 853, 826, 0, 0, 817, 844, 837, 828, 828, 837, 844, 817, 0, 0, 792, 819, 812, 803, 803, 812, 819, 792, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
            { 'N', new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 627, 762, 786, 798, 798, 786, 762, 627, 0, 0, 763, 798, 822, 834, 834, 822, 798, 763, 0, 0, 817, 852, 876, 888, 888, 876, 852, 817, 0, 0, 797, 832, 856, 868, 868, 856, 832, 797, 0, 0, 799, 834, 858, 870, 870, 858, 834, 799, 0, 0, 758, 793, 817, 829, 829, 817, 793, 758, 0, 0, 739, 774, 798, 810, 810, 798, 774, 739, 0, 0, 683, 718, 742, 754, 754, 742, 718, 683, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
            { 'R', new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 1258, 1263, 1268, 1272, 1272, 1268, 1263, 1258, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
            { 'Q', new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 2529, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
            { 'K', new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 60098, 60132, 60073, 60025, 60025, 60073, 60132, 60098, 0, 0, 60119, 60153, 60094, 60046, 60046, 60094, 60153, 60119, 0, 0, 60146, 60180, 60121, 60073, 60073, 60121, 60180, 60146, 0, 0, 60173, 60207, 60148, 60100, 60100, 60148, 60207, 60173, 0, 0, 60196, 60230, 60171, 60123, 60123, 60171, 60230, 60196, 0, 0, 60224, 60258, 60199, 60151, 60151, 60199, 60258, 60224, 0, 0, 60287, 60321, 60262, 60214, 60214, 60262, 60321, 60287, 0, 0, 60298, 60332, 60273, 60225, 60225, 60273, 60332, 60298, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } }
        };

        public static readonly Dictionary<SfPosition, SfEntry> tp = new Dictionary<SfPosition, SfEntry>();

        private static void simplePst(char piece, int val) {
            var a = pst[piece];
            for (var i = 0; i < a.Length; i++) {
                if (a[i] == 0) continue;
                a[i] = val;
            }
        }

        public static void SimplePst() {
            simplePst('P', 100);
            simplePst('B', 300);
            simplePst('N', 300);
            simplePst('R', 500);
            simplePst('Q', 900);
            simplePst('K', 30000);
        }
    }

    internal class SfEntry {
        public int depth = -1;
        public int score = -1;
        public int gamma = -1;
        public Tuple<int, int> bmove = new Tuple<int, int>(-1,-1);

        public SfEntry() {}

        public SfEntry(int sDepth, int sScore, int sGamma, Tuple<int, int> sBmove) {
            depth = sDepth; score = sScore; gamma = sGamma; bmove = sBmove;
        }
    }

    public class SfPosition {
        public char[] mBoard;
        public int mScore;
        public bool[] mWc;
        public bool[] mBc;
        int mEp;
        int mKp;

        public static void SimplePst() {
            Sf.SimplePst();
        }

        public SfPosition(char[] board, int score, bool[] wc, bool[] bc, int ep, int kp ) {
            mBoard = (char[])board.Clone();
            mScore = score;
            mWc = (bool[])wc.Clone();
            mBc = (bool[])bc.Clone();
            mEp = ep;
            mKp = kp;
        }

        static public SfPosition Load(string fen) {
            var split = fen.Split(' ');
            var f0 = split[0];
            for (var i = 8; i > 0; i--) {
                f0 = f0.Replace(i.ToString(), new string('.', i));
            }

            var board = "                    "
                + string.Join("", f0.Split('/').Select(x => " " + x + " "))
                + "                    ";

            var fc = split[2];
            var wc = new bool[] { fc.Contains("Q"), fc.Contains("K") };
            var bc = new bool[] { fc.Contains("q"), fc.Contains("k") };

            var ep = parse(split[3]).Item1;

            return new SfPosition(board.ToCharArray(), 0, wc, bc, ep, 0);
        }

        public List<Tuple<int, int>> genMoves() {
            List<Tuple<int, int>> moves = new List<Tuple<int, int>>();
            for (int i = 0; i < 120; i++) {
                if (char.IsUpper(mBoard[i])) {
                    int[] dirs = Sf.directions[mBoard[i]];
                    for (int d = 0; d < dirs.Length; d++) {
                        int di = dirs[d];
                        for (int j = di + i; ; j += di) {
                            char q = mBoard[j];

                            // stay inside playable board
                            if (q == ' ') {
                                break;
                            }

                            // Check castling
                            if (i == Sf.A1 && q == 'K' && mWc[0]) {
                                moves.Add(new Tuple<int,int>(j, j - 2));
                            }
                            if (i == Sf.H1 && q == 'K' && mWc[1]) {
                                moves.Add(new Tuple<int, int>(j, j + 2));
                            }

                            // No friendly captures
                            if (char.IsUpper(q)) {
                                break;
                            }

                            // Special pawn movement
                            if ( mBoard[i] == 'P' && ( di == Sf.N + Sf.W || di == Sf.N + Sf.E ) && q == '.' && ( j != mEp && j != mKp ) ) {
						        break;
					        }
					        if( mBoard[i] == 'P' && ( di == Sf.N || di == 2 * Sf.N ) && q != '.' ) {
						        break;
					        }
					        if( mBoard[i] == 'P' && di == 2 * Sf.N && ( i < Sf.A1 + Sf.N || mBoard[i + Sf.N] != '.' ) ) {
						        break;
					        }

                            // Move the piece
                            moves.Add(new Tuple<int, int>(i, j));

                            // Stop crawlers from sliding
                            if (mBoard[i] == 'P' || mBoard[i] == 'N' || mBoard[i] == 'K') {
                                break;
                            }

                            // No sliding after captures
                            if (char.IsLower(q)) {
                                break;
                            }
                        }
                    }
                }
            }

            return moves;
        }

        public SfPosition rotate() {
            // Reverse order of board array then swap the case of each character
            // then return Position( swappedboard, -mScore, mBc, mWc, 119 - mEp, 119 - mKp );
            //												^-----^---These two need to be swapped like they are here
            char[] swapBoard = (char[])mBoard.Clone();
            // Reverse alg taken from http://stackoverflow.com/questions/1128985/c-reverse-array
            int len = swapBoard.Length;
            for (int i = 0; i < len / 2; i++) {
                swapBoard[i] ^= swapBoard[len - i - 1];
                swapBoard[len - i - 1] ^= swapBoard[i];
                swapBoard[i] ^= swapBoard[len - i - 1];
            }
            // Now need swap case of character.
            for (int i = 0; i < len; i++) {
                if (char.IsLetter(swapBoard[i])) {
                    if (char.IsLower(swapBoard[i])) {
                        swapBoard[i] = char.ToUpper(swapBoard[i]);
                    }
                    else if (char.IsUpper(swapBoard[i])) {
                        swapBoard[i] = char.ToLower(swapBoard[i]);
                    }
                }
            }
            return new SfPosition(swapBoard, -mScore, mBc, mWc, 119 - mEp, 119 - mKp);
        }

        public SfPosition move(Tuple<int, int> sMove)
        {
            int i = sMove.Item1;
            int j = sMove.Item2;
            char p = mBoard[i];
            char q = mBoard[j];
            // put = lambda board, i, p: board[:i] + p + board[i+1:]
            // creates put functions that places a letter p at a certain point in the board i.
            // This doesn't need to be done in c++, as we are using a char array, instead of pythons immutable string.

            // Copy variables and reset ep and kp
            char[] board = (char[])mBoard.Clone();
            int ep = 0, kp = 0;
            bool[] wc = (bool[])mWc.Clone();
            bool[] bc = (bool[])mBc.Clone();
            int score = mScore + value(sMove);
            // Actual move
            board[j] = board[i];
            board[i] = '.';

            // Castling rights
            if (i == Sf.A1) {
                wc[0] = false;
            }
            if (i == Sf.H1) {
                wc[1] = false;
            }
            if (j == Sf.A8) {
                bc[1] = false;
            }
            if (j == Sf.H8) {
                bc[0] = false;
            }
            if (p == 'K') {
                wc[0] = false; wc[1] = false;
                if (Math.Abs(j - i) == 2)
                {
                    kp = (i + j) / 2; // floor
                    board[(j < i) ? Sf.A1 : Sf.H1] = '.';
                    board[kp] = 'R';
                }
            }
            if (p == 'P') {
                if (Sf.A8 <= j && j <= Sf.H8) {
                    board[j] = 'Q';
                }
                if (j - i == 2 * Sf.N) {
                    ep = i + Sf.N;
                }
                if ((j - i == Sf.N + Sf.W || j - i == Sf.N + Sf.E) && q == '.') {
                    board[j + Sf.S] = '.';
                }
            }

            return new SfPosition(board, score, wc, bc, ep, kp).rotate();
        }

        public int value(Tuple<int, int> sMove) {
            int i = sMove.Item1;
            int j = sMove.Item2;
            char p = mBoard[i];
            char q = mBoard[j];

            // Actual move
            int score = Sf.pst[char.ToUpper(p)][j] - Sf.pst[char.ToUpper(p)][i];

            // Capture
            if (char.IsLower(q)) {
                score += Sf.pst[char.ToUpper(q)][j];
            }
            if (Math.Abs(j - mKp) < 2) {
                score += Sf.pst['K'][j];
            }

            //Castling
            if (p == 'K' && Math.Abs(i - j) == 2)
            {
                score += Sf.pst['R'][(i + j) / 2];
                score -= Sf.pst['R'][(j < i) ? Sf.A1 : Sf.H1];
            }

            // More special pawn movement
            if (p == 'P') {
                if (Sf.A8 <= j && j <= Sf.H8) {
                    score += Sf.pst['Q'][j] - Sf.pst['P'][j];
                }
                if (j == mEp) {
                    score += Sf.pst['P'][j + Sf.S];
                }
            }

            return score;
        }

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

            if (Math.Abs(pos.mScore) >= Sf.MATE_VALUE) {
                return pos.mScore;
            }

            int nullscore = (depth > 0) ? -bound(pos.rotate(), 1 - gamma, depth - 3) : pos.mScore;

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
    }
}
