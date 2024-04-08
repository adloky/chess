using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chess;
using Chess.Sunfish;


namespace SunfishEngine {
    class Program {
        static void Main(string[] args) {
            var fen = Board.DEFAULT_STARTING_FEN;
            SfPosition.SimplePst();
            while (true) {
                var s = Console.ReadLine();
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
                    int depth = -1;
                    for (var i = 1; i < ps.Length; i++) {
                        if (ps[i] == "depth") {
                            depth = int.Parse(ps[i + 1]) + 2;
                            break;
                        }
                    }

                    var color = fen.Contains(" w ") ? 1 : -1;
                    var pos = SfPosition.Load(fen);
                    if (color == -1) {
                        pos = pos.rotate();
                    }

                    var best = (string)null;
                    Tuple<int, int> m = null;
                    for (var i = 0; i < 2; i++) {
                        var sr = SfPosition.search(pos, maxd: depth, exMove: m);
                        m = sr.Item1;
                        var uci = SfPosition.tuple2move(m, color == -1);
                        var isValid = true;
                        try {
                            FEN.Uci2San(fen, uci);
                        }
                        catch {
                            isValid = false;
                        }

                        if (m.Item1 == 0 || sr.Item2 == -30000 || !isValid) {
                            if (i == 0) best = uci;
                            break;
                        } 
                        
                        Console.WriteLine($"info score cp {sr.Item2 * color} multipv {i+1} pv {uci}");
                        if (i == 0) best = uci;
                    }
                    if (best != null) {
                        Console.WriteLine($"bestmove {best}");
                    }
                }
            }
        }
    }
}
