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
using Newtonsoft.Json.Linq;
using System.Globalization;
using Chess.Sunfish;

namespace ChessCon {

    public abstract class BaseNode {
        public int count { get; set; }

        public int midCount { get; set; }

        public int? score { get; set; }

        public abstract string last { get; set; }

        [JsonIgnore]
        public int topCount { get { return count - midCount; } }

        [JsonIgnore]
        public int relCount { get { return relCountFunc(this); } }

        [JsonIgnore]
        public int relScore { get { return relScoreFunc(this); } }

        public static int color { get; set; } = 1;

        public static Func<BaseNode, int> relCountFunc { get; set; } = x => x.count;

        public static Func<BaseNode, int> relScoreFunc { get; set; } = x => x.score.Value * color;

        public abstract IEnumerable<BaseNode> getChilds(string move = null);

        public static BaseNode root { get; set; }
    }

    public class JsonNode : BaseNode {

        public string fen { get; set; }

        public override string last { get; set; }

        public string moves { get; set; }

        public int status { get; set; }

        public string tags { get; set; }

        public static Dictionary<string, JsonNode> nodes { get; set; } = new Dictionary<string, JsonNode>();

        [JsonIgnore]
        public int id { get; set; }

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

        private static string mergeStr(string a, string b) {
            if (b == null) return a;
            if (a == null) return b;

            return string.Join(" ", a.Split(' ').Concat(b.Split(' ')).Distinct());
        }

        public void Merge(JsonNode node) {
            count += node.count;
            midCount += node.midCount;
            moves = mergeStr(moves, node.moves);
            tags = mergeStr(tags, node.tags);
        }

        public FastNode GetFastNode() {
            var r = new FastNode();
            r.count = count;
            r.midCount = midCount;
            r.score = score;
            if (moves == null)
                return r;

            var sans = moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var ms = new List<(SfMove, int)>();
            foreach (var san in sans) {
                var m = SfMove.Parse(FEN.San2Uci(fen, san));
                var cFen = FEN.Move(fen, san);
                //cFen = FEN.Basic(f);
                var key = GetKey(FEN.Move(cFen, san), san);
                ms.Add((m, nodes[key].id));
            }

            r.moves = ms.ToArray();

            return r;
        }

        public static void Load(string tags) {
            var ts = tags.Split(' ');
            var nodes = File.ReadAllLines("d:/lichess.json").Where(x => ts.Any(t => x.Contains(t))).Select(x => JsonConvert.DeserializeObject<JsonNode>(x)).ToList();
            var id = 1;
            var j = 0;
            nodes.ForEach(n => {
                // n.fen = FEN.Basic(n.fen);
                if (n.last == null) {
                    n.id = 0;
                    JsonNode.nodes.Add(n.key, n);
                    return;
                }

                if (JsonNode.nodes.TryGetValue(n.key, out var en)) {
                    en.Merge(n);
                    j++;
                }
                else {
                    n.id = id;
                    JsonNode.nodes.Add(n.key, n);
                    id++;
                }
            });
        }

        public override IEnumerable<BaseNode> getChilds(string move = null) {
            var ms = (moves ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (move != null)
                ms = ms.Where(m => m == move).ToArray();

            foreach (var m in ms) {
                var cFen = FEN.Move(fen, m);
                if (nodes.TryGetValue(GetKey(cFen, m), out var en)) {
                    yield return en;
                }
            }
        }
    }

    public class FastNode : BaseNode {
        public (SfMove move, int id)[] moves;

        private SfMove _last;

        public override string last { get => _last.ToString(); set { throw new NotImplementedException(); } }

        public static List<FastNode> nodes { get; set; } = new List<FastNode>();

        public void Write(BinaryWriter writer) {
            writer.WriteVarint(1);
            writer.WriteVarint(count);
            writer.WriteVarint(2);
            writer.WriteVarint(midCount);
            writer.WriteVarint(3);
            writer.WriteVarint(score.Value);
            writer.WriteVarint(4);
            if (moves == null) {
                writer.WriteVarint(0);
            }
            else {
                writer.WriteVarint(moves.Length);
                foreach (var m in moves) {
                    writer.WriteVarint(1);
                    writer.Write(m.move.pack());
                    writer.WriteVarint(2);
                    writer.WriteVarint(m.id);
                    writer.WriteVarint(0);
                }
            }
            writer.WriteVarint(0);
        }

        public static FastNode Read(BinaryReader reader) {
            var r = new FastNode();
            long fieldNum;
            do {
                fieldNum = reader.ReadVarint();
                switch (fieldNum) {
                    case 0:
                        break;
                    case 1:
                        r.count = (int)reader.ReadVarint();
                        break;
                    case 2:
                        r.midCount = (int)reader.ReadVarint();
                        break;
                    case 3:
                        r.score = (int)reader.ReadVarint();
                        break;
                    case 4:
                        var len = (int)reader.ReadVarint();
                        if (len == 0) break;

                        r.moves = new (SfMove move, int id)[len];
                        for (var i = 0; i < len; i++) {
                            long fn2;
                            do {
                                fn2 = reader.ReadVarint();
                                switch (fn2) {
                                    case 0:
                                        break;
                                    case 1:
                                        r.moves[i].move = SfMove.unpack(reader.ReadUInt16());
                                        break;
                                    case 2:
                                        r.moves[i].id = (int)reader.ReadVarint();
                                        break;
                                    default:
                                        throw new Exception($"Unknown fieldNum: {fieldNum}");
                                }
                            } while (fn2 != 0);
                        }
                        break;
                    default:
                        throw new Exception($"Unknown fieldNum: {fieldNum}");
                }

            } while (fieldNum != 0);

            return r;
        }

        public static void Load() {
            using (var reader = new BinaryReader(File.Open("d:/lichess.dat", FileMode.Open))) {
                var len = reader.BaseStream.Length;
                while (reader.BaseStream.Position < len) {
                    nodes.Add(Read(reader));
                }
            }
            var empty = new (SfMove move, int id)[] { };
            foreach (var m in nodes.SelectMany(x => x.moves ?? empty)) {
                nodes[m.id]._last = m.move;
            }
        }

        public override IEnumerable<BaseNode> getChilds(string move = null) {
            if (moves == null)
                return Enumerable.Empty<BaseNode>();

            var ns = moves.Select(m => nodes[m.id]);
            return move == null ? ns : ns.Where(n => n.last == move);
        }
    }

    public enum WalkState {
        None,
        Break,
        Continue
    }

    public class WalkNode {
        public BaseNode node { get { return nodes[0]; } }

        public BaseNode parent { get { return nodes[1]; } }

        public float freq { get; private set; }

        public int freqPc { get { return (int)(freq * 100); } }

        public WalkState state { get; set; }

        public BaseNode[] nodes { get; private set; }

        public string moves { get { return string.Join(" ", nodes.Reverse().Skip(1).Select(x => x.last)); } } 

        public int? scoreDiff { get { return node.relScore - parent.relScore; } }

        public string info { get; set; }

        public int lastColor { get => nodes.Length % 2 == 0 ? 1 : -1; }

        public WalkNode(BaseNode node, BaseNode[] parents, float freq = 1) {
            nodes = new BaseNode[parents.Length + 1];
            Array.Copy(parents, 0, nodes, 1, parents.Length);
            nodes[0] = node;
            this.freq = freq;
        }
    }

    class Program {

        private static float getFreq(float freq, BaseNode node, BaseNode[] parents) {
            var lastColor = (parents.Length + 1) % 2 == 0 ? 1 : -1;
            return lastColor == BaseNode.color ? freq : freq * ((float)Math.Min(node.relCount, parents[0].relCount) / Math.Max(1, parents[0].relCount));
        }

        public static IEnumerable<WalkNode> EnumerateNodesRecurse(BaseNode[] parents, float freq, Func<WalkNode, WalkState> getState, HashSet<string> hashs = null) {
            if (hashs == null) hashs = new HashSet<string>();

            var nodes = parents[0].getChilds().OrderByDescending(x => x.relCount).ToArray();
            if (nodes.Any(n => hintSet.Contains(n))) {
                nodes = nodes.Where(n => hintSet.Contains(n)).ToArray();
            }
            var wns = nodes.Where(n => !exceptSet.Contains(n))
                .Select(n => new WalkNode(n, parents, getFreq(freq, n, parents)))
                .ToArray();

            foreach (var wn in wns) {
                wn.state = getState(wn);
            }

            var r = wns.Where(wn => wn.state != WalkState.None)
                .SelectMany(wn => Enumerable.Repeat(wn, 1)
                    .Concat(wn.state == WalkState.Break
                        ? Enumerable.Empty<WalkNode>()
                        : EnumerateNodesRecurse(wn.nodes, wn.freq, getState, hashs)
                    )
                );

            return r;
        }

        private static string[] getMoves(string moves = null) {
            moves = Regex.Replace(moves ?? "", @"\d+\.\s*", "");
            return moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static IEnumerable<BaseNode> enumNodesByMoves(string moves = null) {
            var node = BaseNode.root;
            yield return node;
            var ms = (BaseNode.root is JsonNode) ? getMoves(moves) : FEN.San2Uci(Board.DEFAULT_STARTING_FEN, getMoves(moves));
            //return ms.SelectMany();
            foreach (var move in ms) {
                node = node.getChilds(move).FirstOrDefault();
                if (node == null) yield break;

                yield return node;
            }
        }

        public static IEnumerable<WalkNode> EnumerateNodes(string moves = null, Func<WalkNode, WalkState> getState = null) {
            if (getState == null) getState = x => WalkState.Continue;

            var parents = new List<BaseNode>();
            Func<BaseNode[]> parentsReverse = () => ((IEnumerable<BaseNode>)parents).Reverse().ToArray();
            var wns = new List<WalkNode>();
            var nodes = enumNodesByMoves(moves).ToArray();
            parents.Add(nodes[0]);

            foreach (var node in nodes.Skip(1)) {
                wns.Add(new WalkNode(node, parentsReverse()));
                parents.Add(node);
            }

            return wns.Concat(EnumerateNodesRecurse(parentsReverse(), 1, getState));
        }

        private static BaseNode getNodeByMoves(string moves = null) {
            return enumNodesByMoves(moves).Last();
        }

        private static void addHints(string moves = null) {
            var nodes = enumNodesByMoves(moves).Skip(1).ToArray();
            for (var i = 0; i < nodes.Length; i++) {
                var color = 1 - (i % 2) * 2;
                var node = nodes[i];
                if (color == BaseNode.color && !hintSet.Contains(node)) {
                    hintSet.Add(node);
                }
            }
        }

        private static void addExcept(string moves = null) {
            var nodes = enumNodesByMoves(moves).ToList();
            if ((nodes.Count % 2) * (-2) + 1 == JsonNode.color) {
                exceptSet.Add(nodes.Last());
            }
        }

        public static void ShrinkSubMoves(IList<WalkNode> wns, string start) {
            start = Regex.Replace(start ?? "", @"\d+\.\s+", "");
            if (wns.Count < 2) return;
            var i = 1;
            do {
                if (wns[i].moves.Contains(wns[i-1].moves) && wns[i-1].moves != start) { wns.RemoveAt(i-1); } else { i++; }
            } while (i < wns.Count); 
        }

        public static void Prettify(IList<WalkNode> wns) {
            foreach (var wn in wns) {
                wn.info = (BaseNode.root is JsonNode) ? wn.moves : FEN.Uci2San(Board.DEFAULT_STARTING_FEN, wn.moves);
            }

            if (wns.Count > 0) {
                wns[0].info = Pgn.PrettyMoves(wns[0].info);
            }

            var prev = wns.FirstOrDefault()?.moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var wn in wns.Skip(1)) {
                var cur  = wn.moves.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var l = Math.Min(prev.Length, cur.Length);
                var skip = 0;
                for (skip = 0; skip < l && prev[skip] == cur[skip]; skip++);
                wn.info = Pgn.PrettyMoves(wn.info, skip);
                prev = cur;
            }

            foreach (var wn in wns) {
                wn.info = wn.info.Replace(". ", ".");
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
            return tags == null ? null : $" {tags} ".Contains($" {tag} ") ? tags : $"{tags} {tag}";
        }

        private static string align(object o, int s) {
            var r = o.ToString();
            return (new string(' ', Math.Max(0, s - r.Length))) + r;
        }

        private static void compile() {
            var nodes = JsonNode.nodes.Values.OrderBy(n => n.id).ToList();

            using (var writer = new BinaryWriter(File.Open("d:/lichess.dat", FileMode.Create))) {
                var i = 0;
                nodes.ForEach(n => { i++; if (i % 1000 == 0) { Console.WriteLine(i); }; n.GetFastNode().Write(writer); });
            }
        }

        private static volatile bool ctrlC = false;

        private static HashSet<BaseNode> exceptSet = new HashSet<BaseNode>();
        private static HashSet<BaseNode> hintSet = new HashSet<BaseNode>();

        static void Main(string[] args) {
            Console.CancelKeyPress += (o,e) => { ctrlC = true; e.Cancel = true; };

            BaseNode.color = 1;
            BaseNode.relCountFunc = x => x.count;
            FastNode.Load();
            BaseNode.root = FastNode.nodes[0]; // JsonNode.nodes[Board.DEFAULT_STARTING_FEN];

            var ss = new List<string>();
            foreach (var pgn in Pgn.LoadMany(File.OpenText("d:/french-msb2.pgn"))) {
                // if (!pgn.MovesSource.First().Contains("{")) continue;
                try {
                    var moves = getMoves(pgn.Moves);
                    var count = enumNodesByMoves(pgn.Moves).Skip(1).Count();
                    count += (count % 2 == 0) ? 1 : 0;
                    ss.Add(string.Join(" ", moves.Take(count)));
                }
                catch { }
            }
            ss = ss.Distinct().OrderBy(x => x).ToList();

            var prev = new string[] { "--" };
            for (var i = 0; i < ss.Count; i++) {
                var s = ss[i];
                var cur = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var l = Math.Min(prev.Length, cur.Length);
                var skip = 0;
                for (skip = 0; skip < l && prev[skip] == cur[skip]; skip++) ;
                ss[i] = Pgn.PrettyMoves(s, skip).Replace(". ", ".");
                prev = cur;
            }

            File.WriteAllLines("d:/french-msb2-pretty.txt", ss);
            return;




            var start = "1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3";

            //Console.WriteLine(EnumerateNodes(start).Count());

            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 d6 5. c4 Nc6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 d6 5. c4 Nf6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 e6 5. c4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 g6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 Nc6 5. c4 d6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 Nc6 5. c4 e6 6. Nc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 Nc6 5. c4 g6 6. Ne2 Bg7 7. Nbc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d3 4. Bxd3 Nc6 5. c4 Nf6 6. Nc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d5 4. exd5 Nf6 5. Bb5+ Bd7 6. Bc4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d5 4. exd5 Qxd5 5. cxd4 e5 6. Nf3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d5 4. exd5 Qxd5 5. cxd4 Nc6 6. Nf3 Bg4 7. Nc3 Bxf3 8. gxf3 Qxd4 9. Qxd4 Nxd4 10. Nb5 Nc2+ 11. Kd1 Nxa1 12. Nc7+");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d5 4. exd5 Qxd5 5. cxd4 Nc6 6. Nf3 e5 7. Nc3 Bb4 8. Bd2 Bxc3 9. Bxc3 e4 10. Ne5 Nxe5 11. dxe5 Ne7 12. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d5 4. exd5 Qxd5 5. cxd4 Nc6 6. Nf3 e6 7. Nc3 Bb4 8. Bd3 Nf6 9. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d5 4. exd5 Qxd5 5. cxd4 Nf6 6. Nc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 d6 4. cxd4 Nf6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 a6 5. Bc4 e6 6. Nf3 b5 7. Bb3 Bb7 8. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 a6 5. Bc4 e6 6. Nf3 Nc6 7. O-O b5 8. Bb3 Bb7 9. a4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 a6 5. Bc4 e6 6. Nf3 Nc6 7. O-O d6 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 a6 5. Bc4 e6 6. Nf3 Qc7 7. Bb3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 a6 6. Bc4 e6 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 e6 6. Bc4 a6 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 e6 6. Bc4 Be7 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 e6 6. Bc4 Nc6 7. O-O a6 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 e6 6. Bc4 Nf6 7. O-O Be7 8. Qe2 O-O 9. Rd1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 g6 6. Bc4 Bg7 7. Qb3 e6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 Nc6 6. Bc4 a6 7. O-O Nf6 8. Bf4 Bg4 9. h3 Bxf3 10. Qxf3 e6 11. Rfd1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 Nc6 6. Bc4 e6 7. O-O Be7 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 Nc6 6. Bc4 e6 7. O-O Nf6 8. Qe2 Be7 9. Rd1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 Nc6 6. Bc4 g6 7. Qb3 e6 8. Bf4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 Nc6 6. Bc4 Nf6 7. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 Nf6 6. Bc4 e6 7. O-O Be7 8. Qe2 O-O 9. Rd1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 d6 5. Nf3 Nf6 6. Bc4 g6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e5 5. Nf3 d6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e5 5. Nf3 Nc6 6. Bc4 d6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e5 5. Nf3 Nc6 6. Bc4 h6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 a6 6. Bc4 b5 7. Bb3 Bb7 8. O-O b4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 a6 6. Bc4 b5 7. Bb3 Bb7 8. O-O Nc6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 a6 6. Bc4 Nc6 7. O-O b5 8. Bb3 Bb7 9. a4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 a6 6. Bc4 Nc6 7. O-O d6 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 a6 6. Bc4 Ne7 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 a6 6. Bc4 Qc7 7. Bb3 Nc6 8. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Bb4 6. Qd4 Bxc3+");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 d6 6. Bc4 a6 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nc6 6. Bc4 a6 7. O-O b5 8. Bb3 Bb7 9. a4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nc6 6. Bc4 a6 7. O-O Nge7 8. Bg5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nc6 6. Bc4 Bb4 7. O-O Bxc3 8. bxc3 Nge7 9. Qd6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nc6 6. Bc4 Bb4 7. O-O Nge7 8. Bg5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nc6 6. Bc4 d6 7. O-O Be7 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nc6 6. Bc4 d6 7. O-O Nf6 8. Qe2 Be7 9. Rd1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nc6 6. Bc4 Nf6 7. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Ne7 6. Bc4 Ng6 7. h4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Nf6 6. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 g6 5. Nf3 Bg7 6. Bc4 d6 7. Qb3 e6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 g6 5. Nf3 Bg7 6. Bc4 e6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 g6 5. Nf3 Bg7 6. Bc4 Nc6 7. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 g6 5. Nf3 Bg7 6. Bc4 Nf6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 a6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 a6 7. O-O Nf6 8. Bf4 Bg4 9. h3 Bxf3 10. Qxf3 e6 11. Rfd1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 Bg4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 e6 7. O-O a6 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 e6 7. O-O Be7 8. Qe2 Nf6 9. Rd1 e5 10. Be3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 e6 7. O-O Nf6 8. Qe2 Be7 9. Rd1 e5 10. Be3 O-O 11. Rac1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 e6 7. O-O Nf6 8. Qe2 Be7 9. Rd1 Qc7 10. Nb5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 g6 7. Qb3 e6 8. Bf4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 Nf6 7. e5 dxe5 8. Qxd8+ Kxd8 9. Ng5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 d6 6. Bc4 Nf6 7. e5 dxe5 8. Qxd8+ Nxd8");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e5 6. Bc4 Bb4 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e5 6. Bc4 d6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e5 6. Bc4 h6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e5 6. Bc4 Nf6 7. Ng5 d5 8. exd5 Na5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 a6 7. O-O b5 8. Bb3 Bb7 9. a4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 a6 7. O-O d6 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 a6 7. O-O Nge7 8. Bg5 h6 9. Be3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 a6 7. O-O Qc7 8. Nd5 exd5 9. exd5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Bb4 7. O-O Bxc3 8. bxc3 Nge7 9. Qd6 O-O 10. Ba3 Re8 11. Rad1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Bb4 7. O-O Nf6 8. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Bb4 7. O-O Nge7 8. Qe2 Bxc3 9. bxc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Bb4 7. O-O Nge7 8. Qe2 O-O 9. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Bc5 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Be7 7. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 d6 7. O-O Be7 8. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 d6 7. O-O Nf6 8. Qe2 Be7 9. Rd1");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Nf6 7. O-O Be7 8. e5 Ng4 9. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Nge7 7. O-O Ng6 8. h4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 e6 6. Bc4 Qc7 7. O-O Nf6 8. Nb5 Qb8 9. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 g6 6. Bc4 Bg7 7. e5 e6 8. Nb5 Nxe5 9. Qd6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 g6 6. Bc4 Bg7 7. e5 Nh6 8. O-O O-O 9. Bf4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 g6 6. Bc4 Bg7 7. e5 Nxe5 8. Nxe5 Bxe5 9. Bxf7+ Kxf7 10. Qd5+ e6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 Nf6 6. Bc4 d6 7. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 Nf6 6. Bc4 e6 7. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nf6 5. e5 Ng8 6. Nf3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 e5 4. Nf3 dxc3 5. Nxc3 d6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 e5 4. Nf3 dxc3 5. Nxc3 Nc6 6. Bc4 d6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 e5 4. Nf3 dxc3 5. Nxc3 Nc6 6. Bc4 h6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 e5 4. Nf3 Nc6 5. cxd4 exd4 6. Nxd4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 e6 4. cxd4 d5 5. e5 Nc6 6. Nc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 g6 4. Nf3 Bg7 5. cxd4 d5 6. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 g6 4. Nf3 d3 5. Bxd3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 g6 4. Nf3 dxc3 5. Nxc3 Bg7 6. Bc4 d6 7. Qb3 e6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 g6 4. Nf3 dxc3 5. Nxc3 Bg7 6. Bc4 e6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 g6 4. Nf3 dxc3 5. Nxc3 Bg7 6. Bc4 Nc6 7. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nc6 4. cxd4 d5 5. exd5 Qxd5 6. Nf3 Bg4 7. Nc3 Bxf3 8. gxf3 Qxd4 9. Qxd4 Nxd4 10. Nb5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nc6 4. cxd4 d5 5. exd5 Qxd5 6. Nf3 e5 7. Nc3 Bb4 8. Bd2 Bxc3 9. Bxc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nc6 4. cxd4 d5 5. exd5 Qxd5 6. Nf3 e6 7. Nc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nc6 4. cxd4 d6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nc6 4. cxd4 e6 5. d5 exd5 6. exd5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 d6 6. Bc4 dxe5 7. Nxe5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 d6 6. Bc4 e6 7. cxd4 dxe5 8. dxe5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 d6 6. Bc4 Nb6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 e6 6. cxd4 b6 7. Nc3 Nxc3 8. bxc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 e6 6. cxd4 d6 7. Bc4 Be7 8. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 e6 6. cxd4 d6 7. Bc4 Nb6 8. Bd3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 e6 6. cxd4 d6 7. Bc4 Nc6 8. O-O Be7 9. Qe2 O-O 10. Nc3 Nxc3 11. bxc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 e6 6. cxd4 Nc6 7. Bc4 d6 8. O-O Be7 9. Qe2 O-O 10. Nc3 Nxc3 11. bxc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 e6 6. cxd4 Nc6 7. Bc4 Nb6 8. Bb3 d6 9. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 Nc6 6. Bc4 e6 7. cxd4 d6 8. O-O Be7 9. Qe2 O-O 10. Nc3 Nxc3 11. bxc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 Nc6 6. Bc4 Nb6 7. Bb3 d5 8. exd6 Qxd6 9. O-O Be6 10. Na3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 Nc6 6. Bc4 Nb6 7. Bb3 d6 8. exd6 Qxd6 9. O-O Be6 10. Na3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 Nc6 6. Bc4 Nb6 7. Bb3 dxc3 8. Nxc3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 Nc6 6. Bc4 Nb6 7. Bb3 e6 8. cxd4 d6 9. Qe2");
            addHints("1. e4 c5 2. d4 d6");
            addHints("1. e4 c5 2. d4 e6");
            addHints("1. e4 c5 2. d4 g6 3. Nf3 cxd4 4. c3");
            addHints("1. e4 c5 2. d4 Nc6");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 a6 6. Bf4 e6 7. Bc4");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 Nc6 5. Nf3 a6 6. Bf4 e6 7. Bc4 d6 8. O-O");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 a6 6. Bc4 b5 7. Bb3 Bb7 8. O-O Nc6 9. Qe2");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Bb4 6. Qd4 Bxc3+ 7. bxc3 Nf6 8. e5");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 dxc3 4. Nxc3 e6 5. Nf3 Bb4 6. Qd4 Bxc3+ 7. bxc3 Nf6 8. e5 Nc6 9. Qf4 Nd5 10. Qg3");
            addHints("1. e4 c5 2. d4 cxd4 3. c3 Nf6 4. e5 Nd5 5. Nf3 d6 6. Bc4 Nb6 7. Bb3");


            var startNode = getNodeByMoves(start);
            var startScore = startNode.relScore;
            Func<WalkNode, string> scoreDiff = wn => align(((float)(wn.node.relScore - startScore) / 100).ToString("0.00", CultureInfo.InvariantCulture), 6);

            Func<WalkNode, WalkState> getState = wn => {
                if (wn.freq < 0.10) return WalkState.None;

                return wn.lastColor == BaseNode.color
                    ? (wn.node.relScore >= startScore - 50 ? WalkState.Continue : WalkState.None)
                    : (wn.node.relScore <= startScore + 100 ? WalkState.Continue : WalkState.Break);
            };

            var wns = EnumerateNodes(start, getState).ToList();

            ShrinkSubMoves(wns, start);

            //wns = wns.OrderBy(x => x.moves).ToList();
            //wns.ForEach(x => Console.WriteLine("addHints(\"" + Pgn.PrettyMoves(x.moves) + "\");")); Console.ReadLine();

            Prettify(wns);

            foreach (var wn in wns) {
                Console.WriteLine($"{scoreDiff(wn)} {align(wn.freqPc, 3)}% {wn.info}");
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
                if (wn.parent.last == null) {
                    wn.parent.tags = pushTag(wn.parent.tags, tag);
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
            var ss = new List<string>();
            foreach (var pgn in Pgn.LoadMany(File.OpenText("d:/morra-esserman-li.pgn"))) {
                if (!pgn.MovesSource.First().Contains("{")) continue;
                try {
                    var moves = getMoves(pgn.Moves);
                    var count = enumNodesByMoves(pgn.Moves).Skip(1).Count();
                    count += (count % 2 == 0) ? 1 : 0;
                    ss.Add(string.Join(" ", moves.Take(count)));
                }
                catch { }
            }
            ss = ss.Distinct().OrderBy(x => x).ToList();

            var prev = new string[] { "--" };
            for (var i = 0; i < ss.Count; i++) {
                var s = ss[i];
                var cur  = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var l = Math.Min(prev.Length, cur.Length);
                var skip = 0;
                for (skip = 0; skip < l && prev[skip] == cur[skip]; skip++);
                ss[i] = Pgn.PrettyMoves(s, skip).Replace(". ", ".");
                prev = cur;
            }

            File.WriteAllLines("d:/morra-esserman-pretty.txt", ss);
            return;
 */


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
