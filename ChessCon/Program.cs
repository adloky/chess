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

    class Program {

        public class OpeningNode {
            public string fen { get; set; }

            public int count { get; set; }

            public int? score { get; set; }

            public string moves { get; set; }

            public int status { get; set; }

            [JsonIgnore]
            public int turn {
                get {
                    return fen.IndexOf(" w ") > -1 ? 1 : -1;
                }
            }
        }

        public struct WalkNode {
            public OpeningNode node { get; set; }
            public OpeningNode parentNode { get; set; }
            public string moves { get; set; }
        }

        public static IEnumerable<WalkNode> EnumerateNodesRecurse(string fen, string moves, int minCount = 0, OpeningNode parent = null, HashSet<string> hashs = null) {
            if (hashs == null) hashs = new HashSet<string>();
            if (nodeDic.ContainsKey(fen) && !hashs.Contains(fen) ) {
                var node = nodeDic[fen];
                if (node.count >= minCount) {
                    hashs.Add(fen);
                    if (parent != null) {
                        yield return new WalkNode() { parentNode = parent, node = node, moves = moves };
                    }
                
                    foreach (var move in (node.moves ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var childs = EnumerateNodesRecurse(FEN.Move(fen,move), $"{moves}{(moves == "" ? "" : " ")}{move}", minCount, node, hashs);
                        foreach (var child in childs) {
                            yield return child;
                        }
                    }
                }
            }
        }

        public static IEnumerable<WalkNode> EnumerateNodes(string moves = null, int minCount = 0) {
            moves = Regex.Replace(moves ?? "", @"\d+\.\s+", "");

            var fen = Board.DEFAULT_STARTING_FEN;
            foreach (var move in (moves ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                fen = FEN.Move(fen, move);
            }

            return EnumerateNodesRecurse(fen, moves, minCount);
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


        private static string nodesPath = "d:/lichess.json";

        private static Dictionary<string,OpeningNode> nodeDic;

        private static volatile bool ctrlC = false;

        static void Main(string[] args) {
            Console.CancelKeyPress += (o,e) => { ctrlC = true; e.Cancel = true; };
            nodeDic = File.ReadAllLines(nodesPath).Select(x => JsonConvert.DeserializeObject<OpeningNode>(x)).ToDictionary(x => x.fen, x => x);

            // foreach (var node in nodeDic.Values) { node.status = 0; }

            foreach (var wn in EnumerateNodes("1. e4 e6 2. d4 d5 3. e5 c5", 20).Where(x => (x.node.score - x.parentNode.score) * x.node.turn > 80 && x.parentNode.score * x.node.turn > -30 && x.node.turn == -1)) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}; {wn.node.score - wn.parentNode.score}");
            }

            /*
            var wns = EnumerateNodes("1. e4 e6 2. d4 d5 3. e5 c5", 200).ToList();

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
                File.WriteAllLines(nodesPath, nodeDic.Select(x => JsonConvert.SerializeObject(x.Value)).ToArray());
            }
            */
        }
    }
}

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
