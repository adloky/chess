using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chess;
using Chess.Pieces;
using Newtonsoft.Json;
using Markdig;

namespace ChessAnalCon {

    public class BString : IComparable {
        private byte[] bytes;

        public BString(string s) {
            bytes = Encoding.ASCII.GetBytes(s);
        }

        public int CompareTo(object obj) {
            var yBytes = ((BString)obj).bytes;

            var l = Math.Min(bytes.Length, yBytes.Length);

            for (int i = 0; i < l; i++) {
                int result = bytes[i].CompareTo(yBytes[i]);
                if (result != 0) return result;
            }

            return bytes.Length == yBytes.Length ? 0
                : bytes.Length < yBytes.Length ? -1 : 1;
        }

        public override string ToString() {
            return Encoding.ASCII.GetString(bytes);
        }
    }

    public static class StreamReaderExtenstions {
        readonly static FieldInfo charPosField = typeof(StreamReader).GetField("charPos", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        readonly static FieldInfo charLenField = typeof(StreamReader).GetField("charLen", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        readonly static FieldInfo charBufferField = typeof(StreamReader).GetField("charBuffer", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        public static long GetVirtualPosition(this StreamReader reader) {
            var charBuffer = (char[])charBufferField.GetValue(reader);
            var charLen = (int)charLenField.GetValue(reader);
            var charPos = (int)charPosField.GetValue(reader);

            return reader.BaseStream.Position - reader.CurrentEncoding.GetByteCount(charBuffer, charPos, charLen - charPos);
        }
    }

    public class OpeningNode {
        public string fen { get; set; }

        public int count { get; set; }

        public int midCount { get; set; }

        public string last { get; set; }

        public int? score { get; set; }

        public string moves { get; set; }

        public int status { get; set; }

        [JsonIgnore]
        public int turn {
            get {
                return fen.IndexOf(" w ") > -1 ? 1 : -1;
            }
        }

        [JsonIgnore]
        public string key {
            get {
                return FenLast.GetKey(fen,last);
            }
        }
    }

    public class PosInfo {
        public Guid Hash { get; set; }
        public string Fen { get; set; }
        public string Last { get; set; }
    }

    public class StatStorage {
        private Dictionary<Guid, OpeningNode> dic;
        private Dictionary<Guid, OpeningNode> locDic;
        private string posPath;
        private string dicPath;
        private string locDicPath;

        public int Count { get; private set; } = 0;

        private Dictionary<Guid, OpeningNode> loadDic(string path) {
            return File.ReadAllLines(path).Select(x => {
                var split = x.Split(';');
                var guid = Guid.Parse(split[0]);
                var oNode = JsonConvert.DeserializeObject<OpeningNode>(split[1]);
                var kv = new KeyValuePair<Guid, OpeningNode>(guid, oNode);
                return kv;
            }).ToDictionary(x => x.Key, x => x.Value);
        }

        public void Load(StreamReader reader, string posPath, string dicPath, string locDicPath) {
            this.posPath = posPath;
            this.dicPath = dicPath;
            this.locDicPath = locDicPath;

            reader.BaseStream.Position = long.Parse(File.ReadAllText(posPath));

            dic = loadDic(dicPath);
            locDic = loadDic(locDicPath);
            Count = dic.Count();
        }

        public void Save(StreamReader reader) {
            File.WriteAllText(posPath, reader.GetVirtualPosition().ToString());
            File.WriteAllLines(dicPath, dic.Select(x => $"{x.Key.ToString("N")};{JsonConvert.SerializeObject(x.Value)}"));
            File.WriteAllLines(locDicPath, locDic.Select(x => $"{x.Key.ToString("N")};{JsonConvert.SerializeObject(x.Value)}"));
        }

        public void Handle(IEnumerable<PosInfo> pis, int limit, int midCount) {
            var nextLocDic = new Dictionary<Guid, OpeningNode>();
            foreach (var pi in pis) {
                OpeningNode oNode;
                if (locDic.TryGetValue(pi.Hash, out oNode)) {
                    if (oNode.count == limit - 1) {
                        dic.Add(pi.Hash, oNode);
                        Count++;
                    }
                    else {
                        nextLocDic.Add(pi.Hash, oNode);
                    }
                }
                else if (dic.TryGetValue(pi.Hash, out oNode)) { }
                else {
                    oNode = new OpeningNode { fen = pi.Fen, last = pi.Last };
                    nextLocDic.Add(pi.Hash, oNode);
                };

                oNode.count++;
                oNode.midCount += midCount;
            }
            locDic = nextLocDic;
        }
    }

    public class FenLast {

        public string Fen { get; set; }

        public string Last { get; set; }

        public static string GetKey(string fen, string last) {
            if (last == null) {
                return fen;
            }

            return $"{fen} {last}";
        }

        public string Key { get { return GetKey(Fen, Last); } }
    }

    public class MoveInfo {
        public string id { get; set; }

        public string moveSan { get; set; }

        public string moveUci { get; set; }

        public string fen { get; set; }
    }

    public class MoveInfoList : IList<MoveInfo>
    {
        private int delta;

        private List<MoveInfo> moveInfos = new List<MoveInfo>();

        public MoveInfo this[int index] {
            get {
                index = index - delta;
                if (index < 0 || index >= moveInfos.Count) {
                    return null;
                }

                return moveInfos[index];
            }
            set {
                if (moveInfos.Count == 0) {
                    delta = index;
                }
                else if (index - delta < 0) {
                    moveInfos.Clear();
                    delta = index;
                }
                else {
                    index = index - delta;
                    moveInfos.RemoveRange(index, moveInfos.Count - index);
                }

                moveInfos.Add(value);
            }
        }

        #region Not Implemented
        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(MoveInfo item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(MoveInfo item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(MoveInfo[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<MoveInfo> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(MoveInfo item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, MoveInfo item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(MoveInfo item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class MoveInfoHub {

        private int id = 0;

        private Regex re;

        private static MoveInfo start = new MoveInfo() { fen = Board.DEFAULT_STARTING_FEN };

        private List<MoveInfoList> list { get; } = new List<MoveInfoList>();

        private MoveInfo getPrev(int level, int index) {
            if (index == 0) return start;

            for (; list.Count - 1 < level || list[level][index - 1] == null; level--) ;

            return list[level][index - 1];
        }

        private string fenMove(string fen, string move) {
            if (move == "XX") {
                var split = fen.Split(' ');
                var turn = (split[1] == "b") ? "w" : "b";
                var num = int.Parse(split[5]) + (turn == "w" ? 1 : 0);
                var num50 = int.Parse(split[4]) + 1;
                return $"{split[0]} {turn} {split[2]} - {num50} {num}";
            }

            return FEN.Move(fen, move);
        }

        public MoveInfo Push(int level, int index, string move) {
            var prev = getPrev(level, index);
            var mi = new MoveInfo() { fen = fenMove(prev.fen, move), moveSan = move, moveUci = move == "XX" ? null : FEN.San2Uci(prev.fen, move) };
            this[level, index] = mi;

            return mi;
        }

        public string Push(int level, string moves, bool skipInc = false) {
            var index = 0;
            var isEx = false;
            return Program.handleString(moves, re, (x, m) => {
                if (isEx) {
                    return x;
                }

                var move = m.Groups["move"].Value + m.Groups["ccm"].Value;
                var num = m.Groups["num"].Value;
                if (num != "") {
                    index = (int.Parse(num.Replace(".", "")) - 1) * 2 + (num.IndexOf("...") > -1 ? 1 : 0);
                }
                else {
                    index++;
                }

                var mi = (MoveInfo)null;
                try {
                    mi = Push(level, index, move);
                }
                catch {}

                if (mi == null) {
                    isEx = true;
                    return $"=>{x}";
                }

                if (!skipInc) id++;
                return $"<span id='move{id}' class='move' fen='{mi.fen}' uci='{mi.moveUci}'>{x}</span>";
            });
        }

        public MoveInfo this[int level, int index] {
            set {
                while (level > list.Count - 1) {
                    list.Add(new MoveInfoList());
                }

                if (level < list.Count - 1) {
                    list.RemoveRange(level + 1, list.Count - level - 1);
                }

                list[level][index] = value;
            }
        }

        public MoveInfoHub(Regex re) {
            this.re = re;
        }
    }

    public class Tag {
        private static Regex tagRe = new Regex("<(?<name>[^/ ]+)[^/]*/>");
        private static Regex attrRe = new Regex(" +(?<attr>[^=]+)=\"(?<value>[^\"]*)\"");

        public string name { get; set; }

        public Dictionary<string, string> attr { get; } = new Dictionary<string, string>();

        public static Tag[] Parse(string s) {
            var r = new List<Tag>();
            var m = tagRe.Match(s);
            while (m.Success) {
                var tag = new Tag();
                tag.name = m.Groups["name"].Value;
                var m2 = attrRe.Match(m.Value);
                while (m2.Success) {
                    tag.attr.Add(m2.Groups["attr"].Value, m2.Groups["value"].Value);
                    m2 = m2.NextMatch();
                }
                r.Add(tag);
                m = m.NextMatch();
            }
            return r.ToArray();
        }

        public static string Clear(string s) {
            return tagRe.Replace(s, "");
        }
    }

    class Program {
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

        public static string ShrinkMovesNSalt(string s, int count) {
            var i = 0;
            var j = 0;
            for (; s[i] != ','; i++) {
                if (s[i] == ' ') {
                    j++;
                    if (j == count) {
                        break;
                    }
                }
            }

            if (s[i] != ',') {
                s = s.Substring(0, i) + s.Substring(s.IndexOf(','));
            }

            s = $"{s},{Guid.NewGuid().ToString("N")}";

            return s;
        }

        public static string RemoveSalt(string s) {
            return s.Substring(0, s.Length - 33);
        }

        public static IEnumerable<Tuple<int, int>> EnumMoves(string s) {
            var i = 0;
            var j = 0;

            for (; i < s.Length; i++) {
                if (s[i] == ' ') {
                    yield return new Tuple<int, int>(j, i - j);
                    j = i + 1; 
                }
            }
            yield return new Tuple<int, int>(j, i - j);
        }

        private static SHA256 sha;

        public static Guid BytesToGuid(byte[] bytes, int len)
        {
            if (sha == null) {
                sha = SHA256.Create();
            }
            
            var hash = sha.ComputeHash(bytes,0,len);
            var guid = new Guid(hash.Take(16).ToArray());
            return guid;
        }

        public static IEnumerable<PieceMove> PromoteProcessed(PieceMove move) {
            if (!move.HasPromotion) {
                 return Enumerable.Repeat(move, 1);
            }

            return (new Type[] { typeof(Knight), typeof(Bishop), typeof(Rook), typeof(Queen) })
               .Select(x => new PieceMove(move.Source, move.Target, x));
        }

        public static IEnumerable<string> GetPieceMoves(string fen) {
            var board = Board.Load(fen);
 
            return board[board.Turn]
                .SelectMany(x => x.GetValidMoves())
                .SelectMany(x => PromoteProcessed(x))
                .Select(x => x.ToUciString())
                .Select(x => board.Uci2San(x));
        }

        public static IEnumerable<FenLast> GetFenLasts(string fen) {
            foreach (var move in GetPieceMoves(fen)) {
                FenLast fenLast = null;

                try {
                    fenLast = new FenLast() { Fen = FEN.Move(fen, move), Last = move };
                }
                catch { }

                if (fenLast != null) {
                    yield return fenLast;
                }
            }
        }

        public static string pushMove(string moves, string move) {
            if (moves == null) {
                return move;
            }

            return $"{moves} {move}";
        }

        private static volatile bool ctrlC = false;

        private static string nodesPath = "d:/lichess-big.json";

        private static void renameFiles() {
            var prefix = "Aspose.Words.87de280c-dfc0-448b-960a-33bcf0583fb7.";
            var files = Directory.EnumerateFiles("d:/nimzo-lysyy").Where(x => x.IndexOf(prefix) >= 0);
            
            foreach (var file in files) {
                File.Move(file, file.Replace(prefix, ""));
//                Console.WriteLine(file);
            }
        }

        public static string handleString(string s, Regex re, Func<string, Match, string> handler) {
            var m = re.Match(s);
            var i = 0;
            var sb = new StringBuilder();

            while (m.Success) {
                var ls = s.Substring(i, m.Index - i);
                sb.Append(ls);
                ls = s.Substring(m.Index, m.Length);
                ls = handler(ls, m);
                sb.Append(ls);
                i = m.Index + m.Length;
                m = m.NextMatch();
            }
            var ls2 = s.Substring(i, s.Length - i);
            sb.Append(ls2);

            return sb.ToString();
        }

        private static string norm(string s) {
            s = s.Replace("х", "x");
            s = s.Replace("а", "a");
            s = s.Replace("с", "c");
            s = s.Replace("е", "e");
            return s;
        }

        private static int countBrackets(string s) {
            var x = 0;
            foreach (var c in s) {
                if (c == '(') x++;
                if (c == ')' && x > 0) x--;
            }
            return x;
        }

        private static List<Tuple<int, int>> getLevels(string s, int level = 1) {
            var r = new List<Tuple<int, int>>();
            r.Add(new Tuple<int, int>(-1, level));
            var bold = false;
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c == '*' && i > 0 && s[i - 1] == '*') {
                    bold = !bold;
                    level = bold ? 0 : 1;
                    r.Add(new Tuple<int, int>(i, level));
                }

                if (c == '(') { level++; r.Add(new Tuple<int, int>(i, level)); }
                else if (c == ')' && level > 1) { level--; r.Add(new Tuple<int, int>(i, level)); }
            }

            return r;
        }

        private static Regex moveRuRe = new Regex("[a-fасе]?[хx]?[a-fасе][1-8]", RegexOptions.Compiled);

        private static string mdPath = "d:/Projects/smalls/nimzo-lysyy.md";
        private static string md2Path = "d:/Projects/smalls/nimzo-lysyy-2.md";
        private static string htmlPath = "d:/nimzo-lysyy.html";
        private static string bookPath = "d:/Projects/smalls/book.html";

        private static string moveReS = "[NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?|XX";
        //private static Regex moveRe = new Regex(moveReS);
        private static string moveEvalReS = $"({moveReS})(?<eval>[^ ,\\.\\*;:)]*)";
        //private static Regex moveEvalRe = new Regex(moveEvalReS);
        private static string evalLongReS = $"(?<ccm>\\+)−\\+|(?<ccm>\\+)\\+−|\\+−|−\\+|(?<ccm>\\+)|(?<ccm>#)";
        private static string evalReS = $"({evalLongReS}|[!?⩱⩲∓±↻=∞⇄↑→N])*";
        private static string moveNEvalReS = $"(?<move>{moveReS})(?<eval>{evalReS})";
        private static string moveSeq1stReS = $"\\d+\\.{moveNEvalReS}( {moveNEvalReS} \\d+\\.{moveNEvalReS})*( {moveNEvalReS})?";
        private static string moveSeq2ndReS = $"\\d+\\.\\.\\.{moveNEvalReS}( \\d+\\.{moveNEvalReS} {moveNEvalReS})*( \\d+\\.{moveNEvalReS})?";
        private static string moveSeqFullReS = $"{moveSeq1stReS}|{moveSeq2ndReS}";
        private static Regex moveSeqRe = new Regex(moveSeqFullReS);
        private static Regex moveRe = new Regex($"(?<num>\\d+\\.(\\.\\.)?)?{moveNEvalReS}");

        static void Main(string[] args) {
            Console.CancelKeyPress += (o, e) => { ctrlC = true; e.Cancel = true; };
            var book = File.ReadAllLines(bookPath);
            var hub = new MoveInfoHub(moveRe);
            //Console.WriteLine(hub.Push(0, "1.e4 e5"));

            var rss = new List<string>();
            var ss = File.ReadAllLines(mdPath);
            var lastLevel = 1;
            foreach (var s in ss) {
                if (s.Length == 0 || s.IndexOf("![](") == 0) {
                    rss.Add(s);
                    continue;
                }

                if (s.Length > 0 && s[0] == '#') {
                    rss.Add(s);
                    continue;
                }

                var tags = Tag.Parse(s);
                var s2 = Tag.Clear(s);
                var skipStarts = tags
                    .Where(x => x.name == "skip" && x.attr.ContainsKey("start"))
                    .Select(x => x.attr["start"])
                    .ToArray();

                var adds = tags
                    .Where(x => x.name == "add" && x.attr.ContainsKey("start") && x.attr.ContainsKey("value"))
                    .Select(x => new Tuple<string, string>(x.attr["start"], x.attr["value"]))
                    .ToList();

                var levelTags = tags
                    .Where(x => x.name == "level" && x.attr.ContainsKey("start") && x.attr.ContainsKey("value"))
                    .Select(x => new Tuple<string, int>(x.attr["start"], int.Parse(x.attr["value"])))
                    .ToList();

                var levelAll = tags
                    .Where(x => x.name == "level" && !x.attr.ContainsKey("start") && x.attr.ContainsKey("value"))
                    .Select(x => int.Parse(x.attr["value"]))
                    .FirstOrDefault();

                var levels = getLevels(s2, lastLevel);
                lastLevel = tags.Any(x => x.name == "continue") ? levels.Last().Item2 : 1;

                var breakHandle = false;
                var rs = handleString(s2, moveSeqRe, (x, m) => {
                    if (skipStarts.Any(y => x.IndexOf(y) == 0) || breakHandle) {
                        return x;
                    }

                    var level = levels.TakeWhile(y => y.Item1 < m.Index).Last().Item2;
                    level += levelAll;
                    var levelTagTuple = levelTags.Where(y => x.IndexOf(y.Item1) == 0).FirstOrDefault();
                    var levelTag = levelTagTuple?.Item2;
                    if (levelTag != null) {
                        level += levelTag.Value;
                        levelTags.Remove(levelTagTuple);
                    }

                    var addTuple = adds.Where(y => x.IndexOf(y.Item1) == 0).FirstOrDefault();
                    var add = addTuple?.Item2;
                    if (add != null) {
                        hub.Push(level, add, false);
                        adds.Remove(addTuple);
                    }

                    var r = hub.Push(level, x);
                    breakHandle = r.IndexOf("=>") >= 0;
                    return r;
                });

                rss.Add(rs);

                if (rs.IndexOf("=>") >= 0) {
                    break;
                }
            }

            var html = rss.Where(x => x != "").Select(x => Markdown.ToHtml(x)).ToArray();
            
            File.WriteAllLines(htmlPath, book.Concat(html));
            
            // renameFiles();
            //var dic = File.ReadAllLines(nodesPath).Select(x => JsonConvert.DeserializeObject<OpeningNode>(x)).ToDictionary(x => x.key, x => x);
            //foreach (var node in dic.Values) { node.status = 0; }
            /*
            var ss = File.ReadAllLines("d:/lichess-unreach.json");
            foreach (var s in ss) {
                var split = s.Split(',');
                var move = split[0];
                var key = split[1];
                var node = dic[key];
                node.moves = pushMove(node.moves, move);
            }
            */
            /*
            var count = dic.Values.Count();
            foreach (var node in dic.Values) {
                if (ctrlC) {
                    break;
                }

                count--;
                if (count % 1000 == 0) {
                    Console.WriteLine(count);
                }

                if (node.moves == null) {
                    continue;
                }

                var moves = node.moves.Split(' ');

                foreach (var move in moves) {
                    var nextFen = FEN.Move(node.fen, move);
                    var key = FenLast.GetKey(nextFen, move);
                    dic[key].status = 1;
                }
            }
            */
            Console.WriteLine("Save? (y/n)");
            //if (Console.ReadLine() == "y") {
                // File.WriteAllLines(nodesPath, dic.Select(x => JsonConvert.SerializeObject(x.Value)));
            //}
        }
    }
}
/*
    // Eval notes
    var set = new HashSet<string>();
    var ss = File.ReadAllLines(mdPath);
    ss = ss.Select(s => handleString(s, moveEvalRe, (x,m) => {
        var eval = m.Groups["eval"].Value;
                
        //x = x.Replace("", "");
        if (eval != "" && !set.Contains(eval) && !moveEvalRe.Match(eval).Success) {
            set.Add(eval);
        }
                
        return x;
    })).ToArray();

    foreach (var s in set) { Console.WriteLine(s); }
            
    File.WriteAllLines(mdPath, ss);
 */

/*
            var ss = File.ReadAllLines(mdPath);
            ss = ss.Select(s => handleString(s, moveRe, x => norm(x))).ToArray();
            File.WriteAllLines(mdPath, ss);

 */

/*
            using (var readStream = File.OpenRead("d:/lichess_2023-04.csv"))
            using (var reader = new StreamReader(readStream))
            //using (var writeStream = File.Open("e:/lichess_2023-04.csv", FileMode.Create))
            //using (var writer = new StreamWriter(writeStream))
            {
                var count = 0;
                while (!reader.EndOfStream) {
                    var s = reader.ReadLine();
                    var split = s.Split(',');
                    var moves = split[0];
                    var moveCount = moves.Count(x => x == ' ') + 1;
                    var isBlitz = split[1] == "blitz";
                    var elos = split[2].Split(' ').Select(x => int.Parse(x)).ToArray();
                    var is10 = split[3] == "1-0";
                    var isRl = moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 d3") == 0
                            || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 Nf6 d3") == 0
                            || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 d6 c3 Nf6 d3") == 0
                            || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 d6 c3 a6 Ba4 Nf6 d3") == 0
                            || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Bc5 d3") == 0
                            || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 Bc5 c3 Nf6 d3") == 0
                            || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Bc5 c3 Nf6 d3") == 0;

                    if (!isBlitz || moveCount > 32 * 2 || !is10 || !isRl || elos[0] < 2200 || elos[1] > 1900) {
                        continue;
                    }

                    count++;
                    Console.WriteLine(PrettyPgn(moves));
                }
            }
            Console.WriteLine("Finish");
            Console.ReadLine();

 */