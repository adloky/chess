using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chess;
using Chess.Sunfish;


namespace SunfishEngine {
    class Program {
        static string[] startLines = new string[] {
            "position fen 5r1k/1Q5p/6p1/6r1/5q2/3PN3/PP1R1P1P/7K b - - 0 1",
            "go depth 10"
        };
        static Queue<string> startLinesQue = new Queue<string>(startLines);

        static void Main(string[] args) {
            var pos = SfPosition.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

            var zd = new Dictionary<char, SfZobrist[]>() { { 'P', SfZobrist.NewArray(1) }, { 'Q', SfZobrist.NewArray(1) } };
            var cs = new SfZobristCharArray(zd);
            cs[0] = 'P';
            Console.WriteLine(cs.Zobrist);
            cs[0] = 'Q';
            Console.WriteLine(cs.Zobrist);
            cs[0] = ' ';
            Console.WriteLine(cs.Zobrist);


            var fen = Board.DEFAULT_STARTING_FEN;
            /*
            var zs = SfZobrist.NewArray(10);
            SfZobristInt zi = new SfZobristInt(zs);
            //zi.Value = 5;
            zi.Value = 4;
            zi.Value = 5;
            zi.Value = 0;
            Console.WriteLine(zi.Zobrist);
            */

            //Sunfish.SimplePst();
            while (true) {
                var s = startLinesQue.Count > 0 ? startLinesQue.Dequeue() : Console.ReadLine();

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
                else if (s == "go" || s.StartsWith("go ")) {
                    var ps = s.Split(' ');
                    int maxdepth = -1;
                    for (var i = 1; i < ps.Length; i++) {
                        if (ps[i] == "depth") {
                            maxdepth = int.Parse(ps[i + 1]);
                            break;
                        }
                    }

                    var best = (string)null;
                    var startMs = DateTime.Now.Ticks / 10000;
                    foreach (var r in Sunfish.search(fen, maxdepth)) {
                        var elapsedMs = Math.Max(100, DateTime.Now.Ticks / 10000 - startMs);
                        var nps = r.nodes * 1000 / elapsedMs;
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
