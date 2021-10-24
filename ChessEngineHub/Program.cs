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
using System.Threading;
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
        private static Engine engine { get { return engines[engineNum]; } }
        private static int engineNum = 0;
        private static Engine[] engines = { Engine.Open(@"d:/Distribs/lc0/lc0.exe"), Engine.Open(@"d:/Distribs/stockfish_13_win_x64/stockfish_13_win_x64.exe") };
        private static int[] nodeCounts = { 20000, 20000000 };
        private static int nodeCount { get { return nodeCounts[engineNum]; } }
        private static object calcSyncRoot = new object();
        private static AutoResetEvent startCalcWaiter = new AutoResetEvent(true);
        private static volatile bool calcStopped;

        public MoveFen move(string fen, string move) {
            var mf = new MoveFen();
            try {
                var board = Board.Load(fen);
                mf.move = board.UciToSan(move);
                if (board.Move(move)) {
                    mf.fen = board.GetFEN();
                }
            } catch { }

            return mf;
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

            startCalcWaiter.WaitOne();
            engine.Stop();
            startCalcWaiter.Set();

            startCalcWaiter.WaitOne();
            var caller = Clients.Caller;
            Task.Run(() => {
                lock (calcSyncRoot) {
                    calcStopped = false;
                    var sw = new Stopwatch();
                    sw.Start();
                    IList<CalcResult> lastSkipped = null;
                    var isFirst = true;
                    foreach (var crs in engine.CalcScores(fen, nodeCount)) {
                        if (isFirst) {
                            startCalcWaiter.Set();
                            isFirst = false;
                        }

                        var board = Board.Load(fen);
                        foreach (var cr in crs.Where(x => x.move != null)) {
                            cr.san = board.UciToSan(cr.move);
                        }

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

        public int engineNumber(int? n = null) {
            if (n != null) {
                calcStopped = true;

                startCalcWaiter.WaitOne();
                engine.Stop();
                startCalcWaiter.Set();

                lock (calcSyncRoot) {
                    engineNum = n.Value;
                };
            }
            return engineNum;
        }

        public class MoveFen {
            public string move { get; set; }
            public string fen { get; set; }
        }
    }
}
