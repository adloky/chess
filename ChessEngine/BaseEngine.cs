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
    public abstract class BaseEngine : IDisposable {

        public abstract IEnumerable<IList<EngineCalcResult>> CalcScores(string fen, int? nodes = null, int? depth = null);

        public abstract void Stop();

        public abstract void Close();

        public void Dispose() {
            Close();
        }
    }
}
