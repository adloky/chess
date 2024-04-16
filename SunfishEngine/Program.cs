﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chess;
using Chess.Sunfish;


namespace SunfishEngine {
    class Program {

        static string[] startLines = new string[] {
            "position fen rnbqkb1r/pp1ppp1p/5np1/2pP4/2P2B2/8/PP2PPPP/RN1QKBNR b KQkq - 1 7",
            "go depth 9"
        };
        static Queue<string> startLinesQue = new Queue<string>(startLines);

        static void Main(string[] args) {
            var fen = Board.DEFAULT_STARTING_FEN;

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
