using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chess;
using Chess.Sunfish;


namespace SunfishEngine {
    class Program {
        static string[] startLines = new string[] {
            /*"setoption name EnableDiag value 0",
            "position fen r1bq1rk1/pp2bppp/2n2B2/8/3pN3/5NP1/PP2PPBP/R2Q1RK1 b - - 0 12",
            "go depth 7"*/
        };

        static Queue<string> startLinesQue = new Queue<string>(startLines);

        static void Main(string[] args) {
            var fen = Board.DEFAULT_STARTING_FEN;
            //Sunfish.SimplePst();
            
            while (true) {
                var s = startLinesQue.Count > 0 ? startLinesQue.Dequeue() : Console.ReadLine();
                var ps = s.Split(' ').Skip(1).ToArray();

                if (s == "uci") {
                    Console.WriteLine("uciok");
                }
                else if (s == "isready") {
                    Console.WriteLine("readyok");
                }
                else if (s == "ucinewgame") {
                    fen = Board.DEFAULT_STARTING_FEN;
                }
                else if (s.StartsWith("position fen ")) {
                    fen = s.Substring(13);
                }
                else if (s == "quit") {
                    return;
                }
                else if (s.StartsWith("setoption ")) {
                    var name = ps[1];
                    var val = ps[3];
                    if (name == "EnableDiag") {
                        Sunfish.EnableDiag = val != "0";
                    }
                }
                else if (s == "go" || s.StartsWith("go ")) {
                    int maxdepth = -1;
                    for (var i = 0; i < ps.Length; i++) {
                        if (ps[i] == "depth") {
                            maxdepth = int.Parse(ps[i + 1]);
                            break;
                        }
                    }
                    var best = (string)null;
                    var startMs = DateTime.Now.Ticks / 10000;
                    foreach (var r in Sunfish.search(fen, maxdepth)) {
                        var elapsedMs = Math.Max(100, DateTime.Now.Ticks / 10000 - startMs);
                        var nps = (long)r.nodes * 1000 / elapsedMs;
                        Console.WriteLine($"info depth {r.depth} nodes {r.nodes} nps {nps} score cp {r.score}" + (r.pv == null ? "" : $" pv {r.pv}"));
                        if (r.pv != null) {
                            best = r.pv.Split(' ')[0];
                        }
                    }

                    Console.WriteLine($"bestmove {(best == null ? "(none)" : best)}");
                }
            }
        }
    }
}

/*
var ss = File.ReadAllLines("d:/lichess.json").ToList();
var rs = new List<string>();
ss.ForEach(_s => {
    var s = Regex.Replace(_s, "[{}\"]", "");
    var fs = s.Split(',').Where(x => Regex.IsMatch(x, "^(fen|score):")).Select(x => x.Split(':')[1]).ToArray();
    var _fen = fs[0];
    var score = int.Parse(fs[1]);
    var count = int.Parse(_fen.Split(' ').Last());
    if (count < 12 || score < -20 || score > 60)
        return;

    rs.Add(_fen);
});

var rnd = new Random();
var hs = new HashSet<string>();
Enumerable.Range(0, 100).ToList().ForEach(x => {
    var s = rs[rnd.Next(rs.Count-1)];
    if (!hs.Contains(s))
        hs.Add(s);
});

File.WriteAllLines("d:/fens.txt", hs);

return;
 */

/*
private static SfPosition[] poss = (new string [] { "rnbqkb1r/pp1ppp1p/5np1/2pP4/2P2B2/8/PP2PPPP/RN1QKBNR b KQkq - 1 4",
    "r2qkbnr/pp3ppp/2n1p3/3p4/3P4/3QBN2/PPP2PPP/RN3RK1 b kq - 3 8",
    "r1bq1rk1/pppp1ppp/1bn5/4P3/2B1n3/2P2N2/PP3PPP/RNBQ1RK1 w - - 2 8",
    "rnb1kbnr/1p2qppp/p1p5/8/2BPp3/1QN5/PP3PPP/R1B1K1NR w KQkq - 2 8",
    "r2qkb1r/pp2pppp/2n2n2/3p4/3P2b1/2NB1N2/PPP2PPP/R1BQK2R w KQkq - 5 7",
    "rnb1k2r/ppp2ppp/4pn2/3p4/1qPP4/4PN2/PP3PPP/RN1QKB1R w KQkq - 1 7",
    "r1bqkb1r/pp1p2pp/2n1pP2/3n4/2B1Q3/2P5/PP3PPP/RNB1K1NR b KQkq - 0 8",
    "r1b1kbnr/ppppqppp/8/4n3/8/5NP1/PPPNPP1P/R1BQKB1R b KQkq - 1 5",
    "N2k1b1r/pp1bpppp/2n2n2/2q5/2B5/3P4/PPPB1PPP/R2QK1NR b KQ - 0 9",
    "rn1qk2r/pbpp2pp/1p2pn2/5p2/2PP4/2PBP3/P3NPPP/R1BQK2R w KQkq - 2 8"
}).Select(x => SfPosition.FromFen(x)).ToArray();
*/
/*
var dic = new Dictionary<SfZobrist, int>();
var dic2 = new Dictionary<SfZobrist,int>();
long count = 0;
var _startMs = DateTime.Now.Ticks / 10000;
for (var i = 0; i < 10000; i++) {
    var pos = poss[i % 10].Clone();
    foreach (var m in pos.gen_moves()) {
        count++;
        pos.value(m);
        var zch = pos.move(m);
        dic[pos.Zobrist] = 0;
        dic2[pos.Zobrist] = 0;
        pos.Rollback(zch);
    }
    dic.Clear();
    dic2.Clear();
}
var _elapsedMs = Math.Max(100, DateTime.Now.Ticks / 10000 - _startMs);
var ips = (long)count * 1000 / _elapsedMs;
Console.WriteLine(ips);
*/