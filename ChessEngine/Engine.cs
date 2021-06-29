using System;
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
    public class Engine : IDisposable {

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

        public IEnumerable<IList<CalcResult>> CalcScores(string fen, int nodes) {
            var mateState = FEN.GetMateState(fen);
            if (mateState != null) {
                yield return new CalcResult[] { new CalcResult() { score = mateState.Value * 300 } };
                yield break;
            }

            var scoreList = new List<CalcResult>();
            var turn = (fen.Split(' ')[1] == "w") ? 1 : -1;
            input.WriteLine("ucinewgame");
            input.WriteLine($"position fen {fen}");
            input.WriteLine($"go nodes {nodes}");

            string str = "";
            var i = 0;
            var max = 0;
            do {
                str = output.ReadLine();
                var m = Regex.Match(str, "^info .*score (?<scoreName>cp|mate) (?<scoreValue>-?\\d+) .*multipv (?<multipv>\\d+) .*pv (?<move>([a-h][1-8]){2,2}[qrbn]?)");
                if (m.Success) {
                    var r = new CalcResult();
                    var scoreValue = int.Parse(m.Groups["scoreValue"].Value);
                    r.score = turn * ((m.Groups["scoreName"].Value == "cp") ? scoreValue : scoreValue * 30000);
                    r.move = m.Groups["move"].Value;
                    var mulipv = int.Parse(m.Groups["multipv"].Value);
                    if (mulipv == 1) i++;

                    if (i == 2 && mulipv == 1) {
                        yield return scoreList.OrderByDescending(x => x.score * turn).ToArray();
                        max = scoreList.Count;
                        scoreList.Clear();
                    }

                    scoreList.Add(r);

                    if (mulipv == max) {
                        yield return scoreList.OrderByDescending(x => x.score * turn).ToArray();
                        scoreList.Clear();
                    }
                }
                else if (Regex.IsMatch(str, "^error")) {
                    throw new Exception(str);
                }
            } while (!Regex.IsMatch(str, "^bestmove"));

            if (i == 1) {
                yield return scoreList.OrderByDescending(x => x.score * turn).ToArray();
            }
        }

        public int CalcScore(string fen, int nodes) {
            return CalcScores(fen, nodes).Last()[0].score;
        }

        public void Stop() {
            input.WriteLine("stop");
        }

        public void Close() {
            input.WriteLine("quit");
            process.Dispose();
        }

        public void Dispose() {
            Close();
        }

        public class CalcResult {
            public int score { get; set; }
            public string move { get; set; }
        }
    }
}
