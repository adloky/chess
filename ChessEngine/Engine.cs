using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ChessEngine
{
    public class Engine : IDisposable {

        private Process process;
        private StreamWriter input;
        private StreamReader output;

        public void Open(string path) {
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
        }

        public void SetOption(string name, int value) {
            input.WriteLine($"setoption name {name} value {value}");
        }

        public int CalcScore(string fen, int nodes) {
            var turn = (fen.Split(' ')[1] == "w") ? 1 : -1;
            input.WriteLine("ucinewgame");
            input.WriteLine($"position fen {fen}");
            input.WriteLine($"go nodes {nodes}");
            var str = "";
            var prevStr = "";
            do { prevStr = str; str = output.ReadLine(); } while (str.Split(' ')[0] != "bestmove");
            var info = prevStr.Split(' ');
            var score = 0;
            var isScore = false;
            for (var i = 0; i < info.Length; i++) {
                if (info[i] == "cp") {
                    score = int.Parse(info[i+1]);
                    isScore = true;
                    break;
                }
            }

            if (!isScore) {
                throw new Exception("not find info");
            }

            return score * turn;
        }

        public void Close() {
            input.WriteLine("quit");
            process.Dispose();
        }

        public void Dispose() {
            Close();
        }
    }
}
