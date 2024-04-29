using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess {
    public class ChessConfig {
        public string mdPath { get; set; }
        public string mdDstDir { get; set; }
        public string evalPath { get; set; }

        public string enginePath { get; set; }

        public int evalDepth { get; set; }

        public string fn { get; set; }

        private static ChessConfig _current;

        private static ChessConfig getConfig() {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".chess");
            if (!File.Exists(path)) {
                path = "d:/.chess";
            }
            return JsonConvert.DeserializeObject<ChessConfig>(File.ReadAllText(path));
        }

        public static ChessConfig current {
            get {
                return _current ?? (_current = getConfig());
            }
        }
    }
}
