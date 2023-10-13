﻿using Chess;
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
using Newtonsoft.Json.Linq;

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
        public int topCount { get { return count - midCount; } }

        [JsonIgnore]
        public int turn {
            get {
                return fen.Contains(" w ") ? 1 : -1;
            }
        }

        [JsonIgnore]
        public string key {
            get {
                return GetKey(fen, last);
            }
        }

        [JsonIgnore]
        public int relCount { get { return relCountFunc(this); } }

        [JsonIgnore]
        public int relScore { get { return relScoreFunc(this); } }

        public static Func<OpeningNode, int> relCountFunc { get; set; } = x => x.count;

        public static Func<OpeningNode, int> relScoreFunc { get; set; } = x => x.score.Value;

        public static string GetKey(string fen, string last) {
            if (last == null) {
                return fen;
            }

            return $"{fen} {last}";
        }
    }


    class Program {

        public struct WalkNode {
            public OpeningNode node { get { return nodes[0]; } }

            public OpeningNode parent { get { return nodes[1]; } }
            
            public OpeningNode[] nodes { get; private set; }

            public string moves { get { return string.Join(" ", nodes.Reverse().Skip(1).Select(x => x.last)); } } 

            public int? scoreDiff { get { return node.relScore - parent.relScore; } }

            public WalkNode(OpeningNode node, OpeningNode[] parents) {
                nodes = new OpeningNode[parents.Length + 1];
                Array.Copy(parents, 0, nodes, 1, parents.Length);
                nodes[0] = node;
            }
        }

        public static IEnumerable<WalkNode> EnumerateNodesRecurse(OpeningNode[] parents, Func<OpeningNode,bool> takeWhile, HashSet<string> hashs = null) {
            if (hashs == null) hashs = new HashSet<string>();

            var ms = (parents[0].moves ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var keys = ms.Select(m => OpeningNode.GetKey(FEN.Move(parents[0].fen, m), m))
                .Where(k => nodeDic.ContainsKey(k) && !hashs.Contains(k))
                .ToList();

            keys.ForEach(k => hashs.Add(k));

            var childs = keys.Select(k => nodeDic[k])
                .Where(n => takeWhile(n));

            var wns = childs.Select(n => new WalkNode(n, parents))
                .SelectMany(wn => Enumerable.Repeat(wn, 1).Concat(EnumerateNodesRecurse(wn.nodes, takeWhile, hashs)));

            return wns;
        }

        public static IEnumerable<WalkNode> EnumerateNodes(string moves = null, Func<OpeningNode, bool> takeWhile = null) {
            if (takeWhile == null) takeWhile = (n) => true;
            moves = Regex.Replace(moves ?? "", @"\d+\.\s+", "");

            var fen = Board.DEFAULT_STARTING_FEN;
            var ms = moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            var parents = new List<OpeningNode>();
            Func<OpeningNode[]> parentsReverse = () => ((IEnumerable<OpeningNode>)parents).Reverse().ToArray();
            var wns = new List<WalkNode>();
            parents.Add(nodeDic[fen]);

            foreach (var move in ms) {
                fen = FEN.Move(fen, move);
                var key = OpeningNode.GetKey(fen, move);
                var node = nodeDic[key];
                wns.Add(new WalkNode(node, parentsReverse()));
                parents.Add(node);
            }

            return wns.Concat(EnumerateNodesRecurse(parentsReverse(), takeWhile));
        }

        private static OpeningNode getNodeByMoves(string moves = null) {
            moves = Regex.Replace(moves ?? "", @"\d+\.\s+", "");
            var fen = Board.DEFAULT_STARTING_FEN;
            var ms = moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var last = ms.LastOrDefault();
            foreach (var move in ms) {
                fen = FEN.Move(fen, move);
            }
            return nodeDic[OpeningNode.GetKey(fen, last)];
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
                    foreach (var node in new OpeningNode[] { wn.parent, wn.node }) {
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
            return s.Contains($" {tag} ") ? tags : $"{tags} {tag}";
        }

        private static string nodesPath = "d:/lichess.json";

        private static Dictionary<string,OpeningNode> nodeDic;

        private static volatile bool ctrlC = false;

        static void Main(string[] args) {
            Console.CancelKeyPress += (o,e) => { ctrlC = true; e.Cancel = true; };
            OpeningNode.relCountFunc = x => x.midCount;
            OpeningNode.relScoreFunc = x => x.score.Value * 1;

            nodeDic = File.ReadAllLines(nodesPath).Where(x => x.Contains("#spanish")).Select(x => JsonConvert.DeserializeObject<OpeningNode>(x)).ToDictionary(x => x.key, x => x);
            Console.WriteLine("nodeDic loaded.");

            var wns = EnumerateNodes("1. e4 e5 2. Nf3 Nc6 3. Bb5", n => n.relCount >= 10000).ToList();

            ShrinkSubMoves(wns);

            foreach (var wn in wns) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}");
            }

            /*
            var wns = EnumerateNodes("1. e4 c6 2. d4 d5 3. exd5 cxd5 4. c4 Nf6 5. Nc3 Nc6 6. Bg5", n => n.relCount >= 3)
                .Where(x => x.scoreDiff >= 30 && x.parent.score >= -10)
                .OrderByDescending(wn => wn.node.relCount);
                
            foreach (var wn in wns) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}; scoreDiff: {wn.scoreDiff}; count: {wn.node.relCount};");
            }
            */



            // foreach (var node in nodeDic.Values) { node.status = 0; }

            /*
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

            /*
            var wns = EnumerateNodes("1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 5. d3", n => n.count >= 5)
                .Where(x => (x.node.score - x.parentNode.score) * x.node.turn >= 50 && x.parentNode.score * x.node.turn >= -20 && x.node.turn == 1)
                .OrderByDescending(wn => wn.node.count);
                
            foreach (var wn in wns) {
                Console.WriteLine($"{PrettyPgn(wn.moves)}; score: {wn.node.score - wn.parentNode.score}; count: {wn.node.count};");
            }
            */

            /*
            var wns = EnumerateNodes("1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 5. d3 b5 6. Bb3 Bc5", n => n.topCount >= 5 && n.score < 50 && n.score > -20).ToList();

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

// XX: #english #zukertort
// d4: #queens #indian #london
// e4: #french #caro-kann #sicilian #modern #pirc #scandinavian
// e4 e5: #bishops #kings
// e4 e5 Nf3: #russian #philidor
// e4 e5 Nf3 Nc6: #spanish #italian #scotch



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
