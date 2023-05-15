﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Chess;

namespace ChessEngine
{
    public class Engine : BaseEngine {

        private Process process;
        private StreamWriter input;
        private StreamReader output;

        private void open(string path) {
            process = new Process();
            process.StartInfo.FileName = path;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            input = process.StandardInput;
            output = process.StandardOutput;

            input.WriteLine("uci");
            var str = "";
            do { str = output.ReadLine(); } while (str != "uciok");
            input.WriteLine("isready");
            str = "";
            do { str = output.ReadLine(); } while (str != "readyok");

            SetOption("Threads", Math.Max(1, Environment.ProcessorCount-1));
            SetOption("MultiPV", 10);
        }

        public static Engine Open(string path) {
            var engine = new Engine();
            engine.open(path);
            return engine;
        }

        public void SetOption(string name, int value) {
            input.WriteLine($"setoption name {name} value {value}");
        }

        public override IEnumerable<IList<EngineCalcResult>> CalcScores(string fen, int? nodes = null, int? depth = null) {
            while (output.Peek() >= 0) { output.ReadLine(); }

            var mateState = FEN.GetMateState(fen);
            if (mateState != null) {
                yield return new EngineCalcResult[] { new EngineCalcResult() { score = mateState.Value * 30000 } };
                yield break;
            }

            var calcResultList = new List<EngineCalcResult>();
            var turn = (fen.Split(' ')[1] == "w") ? 1 : -1;

            Func<IList<EngineCalcResult>, IList<EngineCalcResult>> handleScores = sl => {
                sl = sl.OrderByDescending(x => x.score * turn).ToArray();

                foreach (var s in sl) {
                    s.san = FEN.Uci2San(fen,s.uci);
                }

                return sl;
            };


            input.WriteLine("ucinewgame");
            input.WriteLine($"position fen {fen}");
            var goStr = "go";
            goStr += nodes == null ? "" : $" nodes {nodes.Value}";
            goStr += depth == null ? "" : $" depth {depth.Value}";
            input.WriteLine(goStr);

            string str = "";
            var i = 0;
            var max = 0;
            do {
                str = output.ReadLine();
                var m = Regex.Match(str, "^info.* score (?<scoreName>cp|mate) (?<scoreValue>-?\\d+).* pv(?<uci>(\\s([a-h][1-8]){2,2}[qrbn]?)+)");
                if (m.Success) {
                    var r = new EngineCalcResult();
                    var scoreValue = int.Parse(m.Groups["scoreValue"].Value);
                    r.score = (m.Groups["scoreName"].Value == "cp") ? scoreValue * turn : (Math.Sign(scoreValue) * 30000 - scoreValue * 100) * turn;
                    r.uci = m.Groups["uci"].Value.Trim();

                    var multipvMatch = Regex.Match(str, " multipv (\\d+)");
                    var mulipv = 1;
                    if (multipvMatch.Success) {
                        mulipv = int.Parse(multipvMatch.Groups[1].Value);
                    }
                    
                    if (mulipv == 1) i++;

                    if (i == 2 && mulipv == 1) {
                        yield return handleScores(calcResultList);
                        max = calcResultList.Count;
                        calcResultList.Clear();
                    }

                    calcResultList.Add(r);

                    if (mulipv == max) {
                        yield return handleScores(calcResultList);
                        calcResultList.Clear();
                    }
                }
                else if (Regex.IsMatch(str, "^error")) {
                    throw new Exception(str);
                }
            } while (!Regex.IsMatch(str, "^bestmove"));

            if (i == 1) {
                yield return handleScores(calcResultList);
            }
        }

        public int CalcScore(string fen, int? nodes = null, int? depth = null) {
            return CalcScores(fen, nodes, depth).Last()[0].score;
        }

        public int Eval(string fen, string param) {
            input.WriteLine("ucinewgame");
            input.WriteLine($"position fen {fen}");
            input.WriteLine($"eval");
            var result = 0;
            var s = (string)null;
            do {
                s = output.ReadLine();
                if (s.IndexOf("Final evaluation: none") > -1) return 0;
                if (s.Length == 0 || s[0] != '|' || s.IndexOf(param) < 0) continue;

                var split = s.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                var floatStr = split[3].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                result = int.Parse(floatStr.Replace(".",""));
            } while (s.IndexOf("|      Total |") < 0);

            do {
                s = output.ReadLine();
            } while (s.IndexOf("Final evaluation") < 0);

            return result;
        }

        public override void Stop() {
            input.WriteLine("stop");
        }

        public override void Close() {
            input.WriteLine("quit");
            process.Dispose();
        }
    }
}
