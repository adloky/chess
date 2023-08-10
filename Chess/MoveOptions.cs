using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess {
    public class MoveOptions {
        public bool? SkipTestMate { get; set; }

        public bool? SkipCheckValidate { get; set; }

        public static MoveOptions Default = new MoveOptions {
            SkipTestMate = false
          , SkipCheckValidate = false
        };

        public MoveOptions Merge(MoveOptions x) {
            var r = new MoveOptions();

            if (x.SkipTestMate != null) {
                r.SkipTestMate = x.SkipTestMate;
            }

            if (x.SkipCheckValidate != null) {
                r.SkipCheckValidate = x.SkipCheckValidate;
            }

            return r;
        }
    }
}
