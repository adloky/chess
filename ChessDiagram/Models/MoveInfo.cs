using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ChessDiagram.Models
{
    public class MoveInfo
    {
        public string move { get; set; }

        public string uci { get; set; }

        public string fen { get; set; }
    }
}