using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Chess;

namespace ChessEngine {

    public class EngineCalcResult {
        public int score { get; set; }
        public string uci { get; set; }
        public string san { get; set; }

        public string uci1st {
            get {
                if (uci == null) {
                    return null;
                }

                var len = uci.IndexOf(" ");
                if (len < 0) {
                    return uci;
                }

                return uci.Substring(0,len);
            }
        }

        public string san1st {
            get {
                if (san == null) {
                    return null;
                }

                var len = san.IndexOf(" ");
                if (len < 0) {
                    return san;
                }

                return san.Substring(0,len);
            }
        }
    }
}
