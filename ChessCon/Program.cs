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

        public static IEnumerable<WalkNode> EnumerateNodes(string moves = "", int minCount = 0) {
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

        private static string nodesPath = "d:/lichess.json";

        private static Dictionary<string,OpeningNode> nodeDic = File.ReadAllLines(nodesPath).Select(x => JsonConvert.DeserializeObject<OpeningNode>(x)).ToDictionary(x => x.fen, x => x);

        private static volatile bool ctrlC = false;

        static void Main(string[] args) {
            Console.CancelKeyPress += (o,e) => { ctrlC = true; e.Cancel = true; };

            foreach (var wn in EnumerateNodes("e4 e6 d4 d5")) {
                Console.WriteLine(wn.moves);
            }

            Console.WriteLine("Save? (y/n)");
            if (Console.ReadLine() == "y") {
                File.WriteAllLines(nodesPath, nodeDic.Select(x => JsonConvert.SerializeObject(x)).ToArray());
            }
        }
    }
}
