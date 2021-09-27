using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Hosting;
using Owin;
using Microsoft.Owin.Cors;
using Chess;
using ChessEngine;
using System.Diagnostics;
using static ChessEngine.Engine;

namespace ChessEngineHub {
    class Program {
        static void Main(string[] args) {
            using (WebApp.Start("http://192.168.0.2:8080")) {
                Console.WriteLine("ChessEngine started...");
                Console.ReadLine();
            }
        }
    }

    class Startup {
        public void Configuration(IAppBuilder app) {
            app.UseCors(CorsOptions.AllowAll);
            app.MapSignalR();
        }
    }

    public class EngineHub : Hub {
        private static Engine engine = Engine.Open(@"d:\Distribs\stockfish_13_win_x64\stockfish_13_win_x64.exe");
        private static object calcSyncRoot = new object();
        private static volatile bool calcStopped;

        public string move(string fen, string move) {
            string rFen = null;
            try {
                rFen = FEN.Move(fen, move);
            } catch { }

            return rFen;
        }

        public IList<MoveFen> getMoves(string pgn) {
            var moveStr = Pgn.GetMoves(pgn);
            var moves = moveStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var moveFenList = new List<MoveFen>();
            var fen = Board.DEFAULT_STARTING_FEN;
            foreach (var move in moves) {
                fen = FEN.Move(fen, move);
                moveFenList.Add(new MoveFen { fen = fen, move = move });
            }

            return moveFenList;
        }

        public void calcScores(string fen) {
            calcStopped = true;
            engine.Stop();
            var caller = Clients.Caller;
            Task.Run(() => {
                lock (calcSyncRoot) {
                    calcStopped = false;
                    var sw = new Stopwatch();
                    sw.Start();
                    IList<CalcResult> lastSkipped = null;
                    foreach (var crs in engine.CalcScores(fen, 10000000)) {
                        if (calcStopped) { continue; }
                        if (sw.ElapsedMilliseconds >= 500) {
                            sw.Restart();
                        } else {
                            lastSkipped = crs;
                            continue;
                        }

                        caller.applyScores(crs);
                        lastSkipped = null;
                    }

                    if (lastSkipped != null && !calcStopped) {
                        caller.applyScores(lastSkipped);
                    }
                }
            });
        }

        public class MoveFen {
            public string move { get; set; }
            public string fen { get; set; }
        }
    }
}
