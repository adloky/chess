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

namespace ChessCon {

    public class OpeningNode {
        public string fen { get; set; }
        public int count { get; set; }
        public int score { get; set; }
        public int status { get; set; }
        public Move[] moves { get; set; } = new Move[0];

        public static string GetShortFen(string fen) {
            return string.Join(" ", fen.Split(' ').Take(3));
        }

        public string GetShortFen() {
            return GetShortFen(this.fen);
        }

        public static OpeningNode Parse(XNode xml) {
            var node = new OpeningNode();
            node.count += int.Parse(xml.XPathSelectElement("white").Value);
            node.count += int.Parse(xml.XPathSelectElement("draws").Value);
            node.count += int.Parse(xml.XPathSelectElement("black").Value);
            var xMoves = xml.XPathSelectElements("moves");
            node.moves = xMoves.Select((x) => {
                var move = new Move();
                move.uci = x.XPathSelectElement("uci").Value;
                move.san = x.XPathSelectElement("san").Value;
                move.count += int.Parse(x.XPathSelectElement("white").Value);
                move.count += int.Parse(x.XPathSelectElement("draws").Value);
                move.count += int.Parse(x.XPathSelectElement("black").Value);
                return move;
            }).ToArray();
            return node;
        }

        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }

        public class Move {
            public string uci { get; set; }
            public string san { get; set; }
            public int count { get; set; }
        }
    }

    class Program {
        public static XNode Get(string fen) {
            var request = WebRequest.Create($"https://explorer.lichess.ovh/lichess?variant=standard&speeds[]=blitz&speeds[]=rapid&speeds[]=classical&ratings[]=1800&ratings[]=2000&fen={Uri.EscapeUriString(fen)}");
            var response = request.GetResponse();

            var result = "";

            using (Stream dataStream = response.GetResponseStream()) {
                var reader = new StreamReader(dataStream);
                result = reader.ReadToEnd();
            }
            response.Close();
            var xmlResult = JsonHelper.JsonToXml(result);
            return xmlResult;
        }

        public static List<OpeningNode> oNodeList { get; set; } = new List<OpeningNode>();

        public static Dictionary<string,OpeningNode> oNodeDic { get; set; } = new Dictionary<string,OpeningNode>();

        public static int nodesWriteInc = 0;

        public static void GetProcessNode(string fen, int count) {
            OpeningNode node;
            var shortFen = OpeningNode.GetShortFen(fen);
            if (oNodeDic.ContainsKey(shortFen)) {
                node = oNodeDic[shortFen];
            }
            else {
                var xml = Get(fen);
                node = OpeningNode.Parse(xml);
                node.fen = fen;
                node.moves = node.moves.Where(x => x.count >= count).ToArray();
                oNodeList.Add(node);
                oNodeDic.Add(node.GetShortFen(),node);
                Thread.Sleep(1500);

                nodesWriteInc = (nodesWriteInc + 1) % 10;
                if (nodesWriteInc == 0) {
                    WriteNodesFile();
                }
            }

            Console.WriteLine(node);

            if (node.status == 0) {
                node.status = 1;
                foreach (var move in node.moves) {
                    GetProcessNode(FEN.Move(fen, move.uci), count);
                }
            }

            node.status = 2;
        }

        public static void ReadNodesFile() {
            var lines = File.ReadAllLines(nodesPath);
            oNodeList = lines.Select(x => JsonConvert.DeserializeObject<OpeningNode>(x)).ToList();
            oNodeDic = oNodeList.ToDictionary(x => x.GetShortFen(), x => x);
        }

        public static void WriteNodesFile() {
            File.WriteAllLines(nodesPath, oNodeList.Select(x => x.ToString()).ToArray());
        }

        public struct SearchNode {
            public OpeningNode node { get; set; }
            public OpeningNode parentNode { get; set; }
            public string moves { get; set; }
        }

        public static IEnumerable<SearchNode> EnumerateNodeTree(string fen, string moves = "", OpeningNode parent = null, HashSet<string> procHash = null) {
            if (procHash == null) procHash = new HashSet<string>();
            if (parent == null) parent = new OpeningNode() { count = int.MaxValue, score = 0 };
            var shortFen = OpeningNode.GetShortFen(fen);
            if (oNodeDic.ContainsKey(shortFen) && !procHash.Contains(shortFen)) {
                var node = oNodeDic[shortFen];
                procHash.Add(shortFen);
                yield return new SearchNode() { parentNode = parent, node = node, moves = moves };
                
                foreach (var m in node.moves) {
                    var childs = EnumerateNodeTree(FEN.Move(fen,m.uci), $"{moves}{(moves == "" ? "" : " ")}{m.san}", node, procHash);
                    foreach (var child in childs) {
                        yield return child;
                    }
                }
            }
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

        public static string nodesPath = "d:/Docs/chess/lichess/london.json";

        #region ParsePgn

        public enum ParseState {
            Empty,
            Param,
            Moves
        }

        public class Game {
            public readonly static Regex ParamRegex = new Regex(@"^\[([^ ]+) ""([^""]*)""\]$", RegexOptions.Compiled);
            public readonly static Regex CommentRegex = new Regex(@" \{[^}]*\}", RegexOptions.Compiled);
            public readonly static Regex NumberRegex = new Regex(@"\d+\.+ ", RegexOptions.Compiled);
            public readonly static Regex ScoreRegex = new Regex(@"[!?]", RegexOptions.Compiled);
            public readonly static Regex SpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

            public long StreamPos { get; set; } = 0;

            public Dictionary<string,string> Params { get; private set; } = new Dictionary<string, string>();

            public List<string> Lines { get; private set; } = new List<string>();

            public List<string> MoveList { get; private set; } = new List<string>();

            public string Moves { get; set; } = "";
        }

        #endregion ParsePgn

        static void Main(string[] args) {
            if (false) {
                var moves = "e4 d6 Nc3 Nf6 d4 g6 Bg5 Bg7 e5 dxe5 dxe5 Qxd1+ Rxd1 Ng4 Nd5 Bxe5 Bxe7 c6 Nf6+ Nxf6 Rd8+ Kxe7 Rxh8 Na6 Bxa6 bxa6 c3 Bb7 Rxa8 Bxa8 Nf3 Bd6 O-O".Split(' ');
                var board = Board.Load(Board.DEFAULT_STARTING_FEN);
                board.Start();
                foreach (var move in moves) {
                    board.Move(move);
                    Console.WriteLine($"{move},{board.GetFEN()}");
                }

                Console.ReadLine();
                return;
            }


            using (var stream = File.Open("d:/lichess_db_standard_rated_2018-06.pgn ", FileMode.Open)) using (var reader = new StreamReader(stream)) {
                stream.Position = 478206495;
                var prevState = ParseState.Empty;
                var state = ParseState.Empty;
                var game = new Game();
                while (!reader.EndOfStream) {
                    prevState = state;
                    var pos = reader.GetVirtualPosition();
                    var s = reader.ReadLine();
                    game.Lines.Add(s);

                    state = (s == "") ? ParseState.Empty
                        : (s[0] == '[') ? ParseState.Param
                        : ParseState.Moves;

                    if (prevState == ParseState.Empty && state == ParseState.Param) {
                        game.StreamPos = pos;
                    }

                    try {
                        Match match = null;

                        if (state == ParseState.Param) {
                            match = Game.ParamRegex.Match(s);
                            if (!match.Success) throw new Exception($"Invalid param: {s}");
                            game.Params.Add(match.Groups[1].Value, match.Groups[2].Value);
                        }

                        if (state == ParseState.Moves) {
                            game.Moves = $"{game.Moves}{(game.Moves == "" ? "" : " ")}{s}";
                        }

                        if (state == ParseState.Empty && prevState == ParseState.Moves) {
                            game.Moves = Game.CommentRegex.Replace(game.Moves, "");
                            game.Moves = Game.NumberRegex.Replace(game.Moves, "");
                            game.Moves = Game.ScoreRegex.Replace(game.Moves, "");
                            game.Moves = Game.SpaceRegex.Replace(game.Moves, " ");
                            game.Moves = game.Moves.Replace($"{game.Params["Result"]}", "");
                            game.Moves = game.Moves.Trim();

                            var board = Board.Load(Board.DEFAULT_STARTING_FEN);
                            board.Start();
                            
                            var moveSplit = game.Moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var move in moveSplit) {
                                game.MoveList.Add(move);
                                if (!board.Move(move)) throw new Exception($"Invalid move: {board.GetFEN()},{move}");
                            }

                            Console.WriteLine($"{game.StreamPos},{game.Params["Site"].Split('/').Last()},{game.Params["Result"]},{game.Moves}");

                            game = new Game();
                        }
                    }
                    catch (Exception e) {
                        Console.WriteLine(game.StreamPos);

                        foreach (var l in game.Lines) {
                            Console.WriteLine(l);
                        }

                        Console.WriteLine(string.Join(" ", game.MoveList));

                        throw;
                    }
                }
            }

            Console.ReadLine();
        }
    }
}

/*
var fen = FEN.Move("r2q1rk1/pb2nppp/1p1bpn2/2ppN3/3P1P2/2PBP1B1/PP1N2PP/R2QK2R w KQ - 0 1", "e1h1");
Console.WriteLine(fen);
Console.ReadLine();
*/

/*
var engine = new Engine();
engine.Open("d:/Distribs/stockfish_13_win_x64/stockfish_13_win_x64.exe");
Console.WriteLine(engine.CalcScore("r1bqkb1r/ppp1pppp/2n2n2/3p4/3P1B2/4P3/PPP2PPP/RN1QKBNR w KQkq - 0 1", 1000));
Console.WriteLine(engine.CalcScore("r2q1rk1/pb3ppp/1pnbpn2/3pN3/2pP1P2/1NPBP1B1/PP4PP/R2QK2R b KQ - 0 1", 1000));

engine.Close();
Console.ReadLine();
 */
/*
var xml = Get("r2q1rk1/pb3ppp/1pnbpn2/2ppN3/3P4/2PBP1B1/PP1N1PPP/R2QK2R w KQ - 0 1");
Console.WriteLine(xml);
Console.ReadLine();
 */
/*
            ReadNodesFile();
            foreach (var node in oNodeList.Where(x => x.status == 1)) {
                node.status = 0;
            }
            GetProcessNode("rnbqkbnr/pppppppp/8/8/3P4/8/PPP1PPPP/RNBQKBNR b KQkq - 0 1", 1000);
            WriteNodesFile();
            Console.WriteLine(oNodeList.Count);
            Console.ReadLine();
 */

/*
            ReadNodesFile();
            // oNodeList.ForEach(x => x.status = 0); WriteNodesFile(); return;
            using (var engine = new Engine()) {
                engine.Open("d:/Distribs/lc0/lc0.exe");
                engine.SetOption("Threads", 4);
                foreach (var node in oNodeList.Where(x => x.status == 0)) {
                    Console.WriteLine($"{node.fen}");
                    node.score = engine.CalcScore(node.fen, 1000);
                    Console.WriteLine($"{node.score}");
                    node.status = 1;

                    nodesWriteInc = (nodesWriteInc + 1) % 10;
                    if (nodesWriteInc == 0) {
                        WriteNodesFile();
                    }
                }
                engine.Close();
            }

            WriteNodesFile();
            Console.ReadLine();
 */

/*
            ReadNodesFile();

            var sel = EnumerateNodeTree(oNodeList[0].fen, "d4 d5 Bf4").Where(x => x.node.score - x.parentNode.score > 40 && x.parentNode.score > -10 && x.node.count >= 5000);
            // var sel = EnumerateNodeTree(oNodeList[0].fen, "d4").Where(x => x.node.count >= max).Where(x => x.node.moves.Where(y => y.count >= max).Count() == 0);

            foreach (var x in sel) {
                Console.WriteLine($"{PrettyPgn(x.moves)}");
            }

            Console.ReadLine();
 */
 