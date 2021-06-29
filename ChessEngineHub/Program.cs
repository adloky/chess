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
        private static Engine engine = Engine.Open("d:/Distribs/lc0/lc0.exe");
        private static object calcSyncRoot = new object();
        private static volatile bool calcStopped;

        public string move(string fen, string move) {
            string rFen = null;
            try {
                rFen = FEN.Move(fen, move);
            } catch { }

            return rFen;
        }

        public void calcScores(string fen) {
            engine.Stop();
            calcStopped = true;
            Task.Run(() => {
                lock (calcSyncRoot) {
                    calcStopped = false;
                    var sw = new Stopwatch();
                    sw.Start();
                    foreach (var crs in engine.CalcScores(fen, 5000)) {
                        if (calcStopped || sw.ElapsedMilliseconds < 300) continue;
                        Clients.Caller.applyScores(crs);
                    }
                }
            });
        }
    }
}
