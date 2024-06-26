﻿using System;
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
using System.IO;

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
        private static BaseEngine engine { get { return engines[engineNum].engine; } }
        private static int engineNum = 0;
        private static (BaseEngine engine, int nodes, int playNodes)[] engines = {
            (new LichessEngine(), 0, 0),
            (Engine.Open(@"d:\Distribs\lc0\lc0.exe"), 20000, 2000),
            (Engine.Open(@"d:\Distribs\stockfish_16\stockfish-windows-x86-64-modern.exe"), 50000000, 1000000),
            (Engine.Open(@"d:\Distribs\komodo-dragon-3\dragon-3-64bit.exe"), 50000000, 1000000),
            (Engine.Open(@"d:\Projects\stockfish-simpleEval\bin\Release\x64\Stockfish.exe"), 50000000, 1000000)
        };
        private static object calcSyncRoot = new object();
        private static AutoResetEvent startCalcWaiter = new AutoResetEvent(true);
        private static volatile bool calcStopped;

        public MoveFen move(string fen, string move) {
            fen = fen.Split(',')[0];
            var mf = new MoveFen();
            try {
                var board = Board.Load(fen);
                mf.move = board.Uci2San(move);
                if (board.Move(move)) {
                    mf.fen = board.GetFEN() + "," + move;
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
                var uci = FEN.San2Uci(fen, move);
                fen = FEN.Move(fen, move);
                pgnDto.moveFens.Add(new MoveFen { fen = fen + "," + uci, move = move });
            }

            return pgnDto;
        }

        private static void Stop() {
            startCalcWaiter.WaitOne();
            engine.Stop();
            startCalcWaiter.Set();
        }

        public void calcScores(string fen, bool isPlayMode) {
            fen = fen.Split(',')[0];
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

                        var nodeCount = isPlayMode ? engines[engineNum].playNodes : engines[engineNum].nodes;
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
                                var goodMoves = lastSkipped.Where(x => Math.Abs(lastSkipped[0].score - x.score) <= 20).ToArray();
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

        public GameDto[] getGames(string moves, int color) {
            var exceptResult = color == 1 ? "0-1" : "1-0";
            var rnd = new Random();
            using (var stream = File.OpenRead("d:/chess/pgns/_adv.pgn")) {
                return Pgn.LoadMany(stream, moves)
                .Where(p => p.Params["Color"] == color.ToString() && p.Params["Result"] != exceptResult)
                .Take(10000).Select(p => new GameDto {
                    wElo = int.Parse(p.Params["WhiteElo"])
                  , bElo = int.Parse(p.Params["BlackElo"])
                  , result = p.Params["Result"].Replace("1/2", "½")
                  , moves = p.Moves
                }).OrderByDescending(x => Math.Abs(x.wElo - x.bElo) * 1000 + rnd.Next(1000))
                .Take(100).ToArray();
            }
        }

        public class GameDto {
            public int wElo { get; set; }
            public int bElo { get; set; }
            public string moves { get; set; }
            public string result { get; set; }
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
