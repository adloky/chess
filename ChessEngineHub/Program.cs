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
        private static Random rnd = new Random((int)DateTime.Now.Ticks);
        private static BaseEngine engine { get { return engines[engineNum]; } }
        private static int engineNum = 0;
        private static BaseEngine[] engines = {
            new LichessEngine(),
            Engine.Open(@"d:\Distribs\lc0\lc0.exe"),
            Engine.Open(@"d:\Distribs\stockfish_14.1_win_x64_popcnt\stockfish_14.1_win_x64_popcnt.exe"),
            Engine.Open(@"d:\Distribs\komodo-dragon-3\dragon-3-64bit.exe")
        };
        private static int[] nodeCounts = { 0, 20000, 50000000, 50000000 };
        private static int[] nodePlayCounts = { 0, 2000, 2000000, 2000000 };
        private static object calcSyncRoot = new object();
        private static AutoResetEvent startCalcWaiter = new AutoResetEvent(true);
        private static volatile bool calcStopped;

        public MoveFen move(string fen, string move) {
            var mf = new MoveFen();
            try {
                var board = Board.Load(fen);
                mf.move = board.Uci2San(move);
                if (board.Move(move)) {
                    mf.fen = board.GetFEN();
                }
            } catch { }

            return mf;
        }

        public PgnDto getMoves(string pgnStr) {
            var pgnDto = new PgnDto();
            var pgn = Pgn.Load(pgnStr);
            var moves = pgn.Moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            pgnDto.fen = pgn.Fen;
            var fen = pgn.Fen;
            foreach (var move in moves) {
                fen = FEN.Move(fen, move);
                pgnDto.moveFens.Add(new MoveFen { fen = fen, move = move });
            }

            return pgnDto;
        }

        private static void Stop() {
            startCalcWaiter.WaitOne();
            engine.Stop();
            startCalcWaiter.Set();
        }

        public void calcScores(string fen, bool isPlayMode) {
            var caller = Clients.Caller;
            calcStopped = true;
            Stop();
            startCalcWaiter.WaitOne();
            Task.Run(() => {
                var isLock = true;
                try {
                    lock (calcSyncRoot) {
                        calcStopped = false;
                        var sw = new Stopwatch();
                        sw.Start();
                        IList<EngineCalcResult> lastSkipped = null;

                        var nodeCount = isPlayMode ? nodePlayCounts[engineNum] : nodeCounts[engineNum];
                        var interval = isPlayMode ? 60000 : 500;

                        foreach (var crs in engine.CalcScores(fen, nodeCount)) {
                            if (isLock) {
                                startCalcWaiter.Set();
                                isLock = false;
                            }

                            if (calcStopped) { continue; }
                            if (sw.ElapsedMilliseconds >= interval) {
                                sw.Restart();
                            } else {
                                lastSkipped = crs;
                                continue;
                            }

                            caller.applyScores(crs);
                            lastSkipped = null;
                        }

                        if (lastSkipped != null && !calcStopped) {
                            if (!isPlayMode) {
                                caller.applyScores(lastSkipped);
                            }
                            else {
                                var goodMoves = lastSkipped.Where(x => Math.Abs(lastSkipped[0].score - x.score) <= 30).ToArray();
                                var goodRndMove = goodMoves[rnd.Next(goodMoves.Length)];
                                caller.applyMove(goodRndMove);
                            }
                        }
                    }
                }
                finally {
                    if (isLock) {
                        startCalcWaiter.Set();
                    }
                }
            });
        }

        public int engineNumber(int? n = null) {
            if (n != null) {
                calcStopped = true;
                Stop();
                lock (calcSyncRoot) {
                    engineNum = n.Value;
                };
            }
            return engineNum;
        }

        public class PgnDto {
            public string fen { get; set; }
            public List<MoveFen> moveFens { get; set; } = new List<MoveFen>();
        }

        public class MoveFen {
            public string move { get; set; }
            public string fen { get; set; }
        }
    }
}
