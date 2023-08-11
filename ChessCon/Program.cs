using Chess;
using Chess.Pieces;
using ChessEngine;
using Lichess;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json;
using System.Threading;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

namespace ChessCon {

    public class OpeningNode {

        public string fen { get; set; }

        public int count { get; set; }

        public int midCount { get; set; }

        public string last { get; set; }

        public int? score { get; set; }

        public string moves { get; set; }

        public int status { get; set; }

        public string tags { get; set; }

        [JsonIgnore]
        public int turn {
            get {
                return fen.IndexOf(" w ") > -1 ? 1 : -1;
            }
        }

        [JsonIgnore]
        public string key {
            get {
                return GetKey(fen, last);
            }
        }

        public static string GetKey(string fen, string last) {
            if (last == null) {
                return fen;
            }

            return $"{fen} {last}";
        }
    }


    class Program {

        public struct WalkNode {
            public OpeningNode node { get; set; }
            public OpeningNode parentNode { get; set; }
            public string moves { get; set; }
        }

        public static IEnumerable<WalkNode> EnumerateNodesRecurse(OpeningNode node, string moves, int minCount = 0, HashSet<string> hashs = null) {
            if (hashs == null) hashs = new HashSet<string>();

            var ms = (node.moves ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var keys = ms.Select(m => OpeningNode.GetKey(FEN.Move(node.fen, m), m))
                .Where(k => nodeDic.ContainsKey(k) && !hashs.Contains(k))
                .ToArray();

            foreach (var key in keys) hashs.Add(key);

            var childs = keys.Select(k => nodeDic[k])
                .Where(n => n.count >= minCount);

            var wns = childs.Select(c => new WalkNode() { parentNode = node, node = c, moves = $"{moves}{(moves == "" ? "" : " ")}{c.last}" })
                .SelectMany(wn => Enumerable.Repeat(wn, 1).Concat(EnumerateNodesRecurse(wn.node, wn.moves, minCount, hashs)));

            return wns;
        }

        public static IEnumerable<WalkNode> EnumerateNodes(string moves = null, int minCount = 0) {
            moves = Regex.Replace(moves ?? "", @"\d+\.\s+", "");

            var fen = Board.DEFAULT_STARTING_FEN;
            var ms = moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var wns = new List<WalkNode>();
            var _moves = "";
            var parentNode = nodeDic[fen];
            foreach (var move in ms) {
                fen = FEN.Move(fen, move);
                _moves = $"{_moves}{(_moves == "" ? "" : " ")}{move}";
                var node = nodeDic[OpeningNode.GetKey(fen, move)];
                wns.Add(new WalkNode() { node = node, parentNode = parentNode, moves = _moves });
                parentNode = node;
            }

            return wns.Concat(EnumerateNodesRecurse(parentNode, moves, minCount));
        }

        public static string PrettyPgn(string pgn) {
            var result = "";
            var split = pgn.Split(' ');
            for (var i = 0; i < split.Length; i++) {
                if (i % 2 == 0) {
                    result += $"{i/2+1}. ";
                }
                result += $"{split[i]} ";
            }
            if (result != "") { result = result.Substring(0, result.Length - 1); }

            return result;
        }

        public static void ShrinkSubMoves(IList<WalkNode> wns) {
            if (wns.Count < 2) return;
            var i = 1;
            do {
                if (wns[i].moves.Contains(wns[i-1].moves)) { wns.RemoveAt(i-1); } else { i++; }
            } while (i < wns.Count); 
        }

        public static void Calc(string moves = "") {
           using (var engine = Engine.Open(@"d:\Distribs\stockfish_14.1_win_x64_popcnt\stockfish_14.1_win_x64_popcnt.exe")) {
                var nodes = EnumerateNodes(moves).ToArray();
                var nullCount = nodes.Where(x => x.node.score == null).Count();
                foreach (var wn in nodes) {
                    if (ctrlC) break;
                    foreach (var node in new OpeningNode[] { wn.parentNode, wn.node }) {
                        if (node.score == null) {
                            try {
                                node.score = engine.CalcScore(node.fen, 2000000);
                            } catch {}
                            nullCount--;
                            Console.WriteLine($"{node.fen}, Score: {node.score}, {nullCount}");
                        }
                    }
                }
            }
        }

        public static Dictionary<char, int> pieceValue = new Dictionary<char, int> { { 'p', -1 }, { 'b', -3 }, { 'n', -3 }, { 'r', -5 }, { 'q', -9 },
            { 'P', 1 }, { 'B', 3 }, { 'N', 3 }, { 'R', 5 }, { 'Q', 9 } };

        public static int balance(string fen) {
            var fen0 = fen.Split(' ')[0];
            var result = 0;
            foreach (var p in fen0) {
                if (!pieceValue.ContainsKey(p)) continue;

                result += pieceValue[p];
            }

            return result;
        }

        private static string pushTag(string tags, string tag) {
            if (tags == null) {
                return tag;
            }

            var s = $" {tags} ";
            return s.IndexOf($" {tag} ") > -1 ? tags : $"{tags} {tag}";
        }

        private static string nodesPath = "d:/lichess.json";

        private static Dictionary<string,OpeningNode> nodeDic;

        private static volatile bool ctrlC = false;

        static void Main(string[] args) {
            Console.CancelKeyPress += (o,e) => { ctrlC = true; e.Cancel = true; };
            nodeDic = File.ReadAllLines(nodesPath).Where(x => x.IndexOf("#caro-kann") > -1).Select(x => JsonConvert.DeserializeObject<OpeningNode>(x)).ToDictionary(x => x.key, x => x);

            // foreach (var node in nodeDic.Values) { node.status = 0; }

            /*
            var moves = new List<string>() {
                "1. e4 c5 2. d4 e6 3. d5 d6 4. c4",
                "1. e4 e5 2. Nf3 Nc6 3. d4 d6 4. d5 Nce7 5. c4",
                "1. e4 e5 2. d4 Nc6 3. d5 Nce7 4. c4 d6",
                "1. e4 c6 2. d4 d6 3. c4 Qc7 4. Nc3 e5 5. d5",
                "1. e4 d6 2. d4 Nd7 3. c4 e5 4. d5",
                "1. d4 Nf6 2. c4 e6 3. Nc3 c5 4. d5 d6 5. e4",
                "1. d4 Nf6 2. c4 c5 3. d5 e5 4. Nc3 d6 5. e4",
                "1. d4 Nf6 2. c4 d6 3. Nc3 Nbd7 4. e4 e5 5. d5",
                "1. d4 e6 2. c4 c5 3. d5 d6 4. e4",
                "1. d4 c5 2. d5 d6 3. c4 e5 4. Nc3 f5 5. e4",
                "1. d4 c5 2. d5 d6 3. c4 e5 4. e4",
                "1. d4 c5 2. d5 d6 3. e4 e5 4. c4"
            };
            


            foreach (var m in moves) {
                foreach (var wn in EnumerateNodes(m, 20).Where(x => (x.node.score - x.parentNode.score) * x.node.turn >= 80 && x.parentNode.score * x.node.turn >= -30 && x.node.turn == 1)) {
                    Console.WriteLine($"{PrettyPgn(wn.moves)}; {wn.node.score - wn.parentNode.score}");
                }
            }
            */

            /*
            var pat = "8/8/3p4/3P4/2P1P3/8/8/8 w - - 0 1";
            var moves = new List<string>();

            foreach (var wn in EnumerateNodes(minCount:200).Where(x => FEN.Like(x.node.fen, pat))) {
                if (!moves.Any(m => wn.moves.IndexOf(m) == 0)) {
                    moves.Add(wn.moves);
                    Console.WriteLine(PrettyPgn(wn.moves));
                }
            }
            */

            // var r = EnumerateNodes("1. e4 c6", 0).ToArray(); Console.WriteLine(r.Length);

            /*
            var tag = "#sicilian";
            foreach (var wn in EnumerateNodes("1. e4 c5")) {
                if (wn.parentNode.last == null) {
                    wn.parentNode.tags = pushTag(wn.parentNode.tags, tag);
                }

                wn.node.tags = pushTag(wn.node.tags, tag);
            }
            */
            
            foreach (var wn in EnumerateNodes("1. e4 c6 2. d4 d5 3. exd5 cxd5 4. c4 Nf6 5. Nc3 Nc6 6. Nf3 Bg4 7. Be3", 0).Where(x => (x.node.score - x.parentNode.score) * x.node.turn >= 80 && x.parentNode.score * x.node.turn >= -30 && x.node.turn == 1)) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}; {wn.node.score - wn.parentNode.score}");
            }
            
            /*
            var wns = EnumerateNodes2("1. e4", 100000).ToList();

            ShrinkSubMoves(wns);

            foreach (var wn in wns) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}; {wn.node.score}");
            }
            */

            Console.Write("Press ENTER");
            Console.ReadLine();
            
            /*
            Console.WriteLine("Save? (y/n)");
            if (Console.ReadLine() == "y") {
                File.WriteAllLines(nodesPath, nodeDic.Values.Select(x => JsonConvert.SerializeObject(x)));
            }
            */
        }
    }
}

/*
            using (var engine = Engine.Open(@"d:\Distribs\stockfish_14.1_win_x64_popcnt\stockfish_14.1_win_x64_popcnt.exe")) {
                foreach (var node in nodeDic.Values) {
                    var fen = node.fen;
                    var split = fen.Split(' ');
                    var mn = int.Parse(split[5]);
                    if (mn < 13 || Math.Abs(node.score.Value) > 1000) continue;
                    // if (balance(fen) != 0) continue;

                    var r = engine.Eval(fen, "Winnable");
                    if (r < 10) continue;

                    Console.WriteLine(fen + ", " + r.ToString());
                }
            }

 */

/*
            var wns = EnumerateNodes("e4 c5 d4 cxd4 c3 dxc3 Nxc3 Nc6 Nf3 e6").ToList();

            ShrinkSubMoves(wns);

            foreach (var wn in wns) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}; {wn.node.score}");
            }
 
 */


/*
            foreach (var wn in EnumerateNodes("1. e4 d5 2. exd5 Qxd5 3. Nc3 Qa5 4. d4", 0).Where(x => (x.node.score - x.parentNode.score) * x.node.turn > 80 && x.parentNode.score * x.node.turn > -30 && x.node.turn == 1)) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}; {wn.node.score - wn.parentNode.score}");
            }
 
 */

/*
           using (var engine = Engine.Open(@"d:\Distribs\stockfish_14.1_win_x64_popcnt\stockfish_14.1_win_x64_popcnt.exe")) {
                var nodes = EnumerateNodes("d4 d5 Bf4").ToArray();
                var nullCount = nodes.Where(x => x.node.score == null).Count();
                foreach (var wn in nodes) {
                    if (ctrlC) break;
                    foreach (var node in new OpeningNode[] { wn.parentNode, wn.node }) {
                        if (node.score == null) {
                            try {
                                node.score = engine.CalcScore(node.fen, 2000000);
                            } catch {}
                            nullCount--;
                            Console.WriteLine($"{node.fen}, Score: {node.score}, {nullCount}");
                        }
                    }
                }
            }
 */


/*
        public static string GetArrow(int x1, int y1, int x2, int y2, int width) {
            var dx = x1 - x2;
            var dy = y1 - y2;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var sin = dx / length;
            var cos = dy / length;

            var ps = new List<Tuple<double, double>>();

            ps.Add(new Tuple<double, double>(0, 0));
            ps.Add(new Tuple<double, double>(width * 1.5f, width * 3));
            ps.Add(new Tuple<double, double>(width / 2, width * 3));
            ps.Add(new Tuple<double, double>(width / 2, length));
            ps.Add(new Tuple<double, double>(-width / 2, length));
            ps.Add(new Tuple<double, double>(-width / 2, width * 3));
            ps.Add(new Tuple<double, double>(-width * 1.5f, width * 3));

            for (var i = 0; i < ps.Count; i++) {
                ps[i] = new Tuple<double, double>(Math.Round(ps[i].Item1 * cos + ps[i].Item2 * sin) + x2, Math.Round(-ps[i].Item1 * sin + ps[i].Item2 * cos) + y2);
            }

            return string.Join(" ", ps.Select(p => $"{p.Item1},{p.Item2}"));
        }
*/

/*
            var i = 0;
            foreach (var node in nodeDic.Values) {
                var board = Board.Load(node.fen);
                foreach (var move in GetPieceMoves(node.fen)) {
                    string fen = null;
                    try {
                        fen = FEN.Move(node.fen, move);
                    } catch { }

                    if (fen == null || !nodeDic.ContainsKey(fen)) {
                        continue;
                    }

                    node.moves = pushMove(node.moves, board.UciToSan(move));
                }

                i++;
                if (i % 100 == 0) {
                    Console.WriteLine(i);
                }
            }

 */
