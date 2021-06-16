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

        public class PositionNode {
            public int Count { get; set; }

            public string Fen { get; set; }

            public string Moves { get; set; }

            public void Write(BinaryWriter writer) {
                writer.WriteVarint(2);
                writer.WriteVarint(Count);

                if (Fen != null) {
                    writer.WriteVarint(3);
                    writer.Write(Fen);
                }

                if (Moves != null) {
                    writer.WriteVarint(4);
                    writer.Write(Moves);
                }

                writer.WriteVarint(0);
            }

            public static PositionNode Read(BinaryReader reader) {
                var node = new PositionNode();

                long fieldNum;
                do {
                    fieldNum = reader.ReadVarint();
                    switch (fieldNum) {
                        case 0:
                            break;
                        case 2:
                            node.Count = (int)reader.ReadVarint();
                            break;
                        case 3:
                            node.Fen = reader.ReadString();
                            break;
                        case 4:
                            node.Moves = reader.ReadString();
                            break;
                        default:
                            throw new Exception($"Unknown fieldNum: {fieldNum}");
                    }

                } while (fieldNum != 0);

                return node;
            }

            public void PushMove(string move) {
                if (Moves == null) {
                    Moves = move;
                    return;
                }

                var moves = Moves.Split(' ');
                if (!moves.Contains(move)) {
                    Moves += $" {move}";
                }
            }
        }

        private static SHA256 sha;

        public static Guid StringToGuid(string s) {
            if (sha == null) {
                sha = SHA256.Create();
            }

            var b = Encoding.UTF8.GetBytes(s);
            var hash = sha.ComputeHash(b);
            var guid = new Guid(hash.Take(16).ToArray());
            return guid;
        }

        private static ConcurrentDictionary<Guid, PositionNode>  dic = new ConcurrentDictionary<Guid, PositionNode>();

        private static long pos = 0;

        private static Random rnd = new Random();

        private static volatile bool ctrlC = false;

        private static readonly Guid rootGuid = StringToGuid(Board.DEFAULT_STARTING_FEN);

        static void Main(string[] args) {
            Console.CancelKeyPress += (o,e) => { ctrlC = true; e.Cancel = true; };

            if (File.Exists("d:/lichess.dat")) {
                Console.WriteLine("Begin read file ...");
                using (var stream = File.Open("d:/lichess.dat", FileMode.Open)) using (var reader = new BinaryReader(stream)) {
                    while (reader.BaseStream.Position < reader.BaseStream.Length) {
                        var guid = reader.ReadGuid();
                        var node = PositionNode.Read(reader);
                        dic.TryAdd(guid,node);
                    }
                }
                Console.WriteLine("End read file");
                pos = long.Parse(File.ReadAllLines("d:/lichess.pos")[0]);
            }

            var rootNode = dic.GetOrAdd(rootGuid, x => new PositionNode() { Fen = Board.DEFAULT_STARTING_FEN });

            using (var reader = File.OpenText("d:/lichess.csv")) {
                reader.BaseStream.Position = pos;
                var sw = new Stopwatch();
                sw.Start();
                while (!reader.EndOfStream && !ctrlC) {
                    if (sw.ElapsedMilliseconds >= 1000) {
                        Console.WriteLine(reader.GetVirtualPosition());
                        sw.Restart();
                    }

                    var s = reader.ReadLine();

                    var mfs = new List<Tuple<string, string>>();
                    try {
                        var moves = s.Split(',')[3].Split(' ').Take(20).ToArray();
                        var board = Board.Load(Board.DEFAULT_STARTING_FEN);
                        board.Start();
                        foreach (var move in moves) {
                            if (!board.Move(move)) {
                                throw new Exception("Invalid move");
                            }
                            mfs.Add(new Tuple<string, string>(move,board.GetFEN()));
                        }
                    }
                    catch (Exception e) {
                        mfs = null;
                        File.AppendAllLines("d:/lichess.err", new string[] { s });
                        Console.WriteLine($"Exception: {s}");
                    }

                    if (mfs != null) {
                        var parentNode = rootNode;
                        parentNode.Count++;
                        foreach (var mp in mfs) {
                            var move = mp.Item1;
                            var fen = mp.Item2;
                            var guid = StringToGuid(fen);
                            var node = dic.GetOrAdd(guid, x => new PositionNode());
                            node.Count++;
                            if (node.Count == 10) {
                                node.Fen = fen;
                                parentNode.PushMove(move);
                            }
                            parentNode = node;
                        }
                    }
                }
                pos = reader.GetVirtualPosition();
            }

            Console.WriteLine("Save? (y/n)");
            if (Console.ReadLine() == "y") {
                Console.WriteLine("Begin write file...");

                using (var stream = File.Open("d:/lichess2.dat", FileMode.Create)) using (var writer = new BinaryWriter(stream)) {
                    foreach (var kv in dic) {
                        writer.WriteGuid(kv.Key);
                        kv.Value.Write(writer);
                    }
                    writer.Close();
                }

                if (File.Exists("d:/lichess.dat")) File.Delete("d:/lichess.dat");
                File.Move("d:/lichess2.dat", "d:/lichess.dat");
                File.WriteAllText("d:/lichess.pos", pos.ToString());

                Console.WriteLine("End write file");
            }

            Console.ReadLine();

            /*
            using (var stream = File.Open("d:/test.bin", FileMode.Create)) using (var writer = new BinaryWriter(stream)) {
                writer.Write("Hello world");
                writer.Close();
            }

            using (var stream = File.Open("d:/test.bin", FileMode.Open)) using (var reader = new BinaryReader(stream)) {
                var s = reader.ReadString();
                Console.WriteLine(s);
                reader.Close();
            }
            */
            /*
            var rnd = new Random();

            using (var stream = File.Open("d:/test.bin", FileMode.Create)) using (var writer = new BinaryWriter(stream)) {
                for (var i = 0; i < 50000000; i++) {
                    var fen = Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                    var hash = StringToGuid(fen);
                    var node = new PositionNode { };
                    if (rnd.Next(5) == 0) {
                        node.Fen = fen;
                        node.Moves = Guid.NewGuid().ToString();
                    }

                    //dic.TryAdd(hash, node);
                    writer.WriteGuid(hash);
                    node.Write(writer);
                }
            }

            Console.WriteLine("Finish");
            GC.Collect();
            Console.ReadLine();
            */
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

/*
            var dic = new ConcurrentDictionary<ulong, PositionNode>();
            var rnd = new Random();

            for (var i = 0; i < 50000000; i++) {
                var key = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
                var node = new PositionNode { Hash = key };
                if (rnd.Next(5) == 0) {
                    node.Fen = Guid.NewGuid().ToString();
                    node.Moves = Guid.NewGuid().ToString();
                }
                dic.TryAdd(key, node);
            }
            Console.WriteLine("Finish");
            Console.ReadLine();

 */