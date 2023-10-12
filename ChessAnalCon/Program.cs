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
using System.Globalization;
using ChessEngine;
using System.Net;
using Lichess;
using System.Threading;

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
                return FenLast.GetKey(fen, last);
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

        public bool err { get; set; }
    }

    public class MoveInfoEval : MoveInfo {
        public int eval { get; set; }

        public int num { get; set; }

        public string numStr { get { return $"{num / 2 + 1}.{(num % 2 == 0 ? "" : "..")}"; } }

        public string evalStr { get { return ((float)eval / 100).ToString(CultureInfo.InvariantCulture); } }
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

        private MoveInfo start;

        private int startIndex;

        private List<MoveInfoList> list { get; } = new List<MoveInfoList>();

        public string Fen { get { return start.fen; } }

        MoveInfoHub() {
            SetFen(Board.DEFAULT_STARTING_FEN);
        }

        public void SetFen(string fenStr) {
            start = new MoveInfo() { fen = fenStr };
            var fen = FEN.Parse(fenStr);
            startIndex = fen.historyLenDiff;
            list.Clear();
        }

        private MoveInfo getPrev(int level, int index) {
            if (index == startIndex) return start;

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
            var err = false;
            if (move.IndexOf("+") >= 0) {
                var uci = FEN.San2Uci(prev.fen, move);
                var san = FEN.Uci2San(prev.fen, uci);
                err = move != san;
            }
            var mi = new MoveInfo() { fen = fenMove(prev.fen, move), moveSan = move, moveUci = move == "XX" ? null : FEN.San2Uci(prev.fen, move), err = err };
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
                catch { }

                if (mi == null) {
                    isEx = true;
                    return $"=>{x}";
                }

                if (!skipInc) id++;
                return $"<span id='move{id}' class='move' fen='{mi.fen}' uci='{mi.moveUci}'>{(mi.err ? "=>" : "")}{x}</span>";
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

        public MoveInfoHub(Regex re) : this() {
            this.re = re;
        }
    }

    public class Tag {
        public static Regex tagRe { get; } = new Regex(@"<(?<close>/)?(?<name>[^/> ]+)(""[^""]*""|[^/>])*/?>");
        private static Regex attrRe = new Regex(@" +(?<attr>[^=]+)=""(?<value>[^""]*)""");

        public string name { get; set; }

        public bool isOpen { get; private set; }

        public Dictionary<string, string> attr { get; } = new Dictionary<string, string>();

        public static Tag[] Parse(string s) {
            var r = new List<Tag>();
            var m = tagRe.Match(s);
            while (m.Success) {
                var tag = new Tag();
                tag.name = m.Groups["name"].Value;
                tag.isOpen = m.Groups["close"].Value != "/";
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

    public class FenScore {
        public string fen { get; set; }
        public int? score { get; set; }
    }

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
        public readonly static Regex ResultRegex = new Regex(@" ?(1-0|0-1|1/2-1/2|\*)$", RegexOptions.Compiled);
    }

    public class Config {
        public string mdPath { get; set; }
        public string mdDstDir { get; set; }
        public string evalPath { get; set; }

        public string enginePath { get; set; }

        public int evalDepth { get; set; }

        public string fn { get; set; }

        private static Config _current;

        private static Config getConfig() {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".chess-anal");
            if (!File.Exists(path)) {
                path = "d:/.chess-anal";
            }
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
        }

        public static Config current {
            get {
                return _current ?? (_current = getConfig());
            }
        }
    }

    class Program {
        public static string PrettyPgn(string pgn) {
            var result = "";
            var split = pgn.Split(' ');
            for (var i = 0; i < split.Length; i++) {
                if (i % 2 == 0) {
                    result += $"{i / 2 + 1}. ";
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

            var hash = sha.ComputeHash(bytes, 0, len);
            var guid = new Guid(hash.Take(16).ToArray());
            return guid;
        }

        public static Guid StringToGuid(string s) {
            var bytes = Encoding.ASCII.GetBytes(s);
            return BytesToGuid(bytes, bytes.Length);
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

        private static void findGames() {
            using (var readStream = File.OpenRead("d:/lichess_2023-04.csv"))
            using (var reader = new StreamReader(readStream))
            {
                var r = new List<string>();
                var count = 0;
                var rCount = 0;
                while (!reader.EndOfStream) {
                    var s = reader.ReadLine();
                    var split = s.Split(',');
                    var moves = split[0];
                    var moveCount = moves.Count(x => x == ' ') + 1;
                    var isBlitz = split[1] == "blitz";
                    var elos = split[2].Split(' ').Select(x => int.Parse(x)).ToArray();
                    var isWin = split[3] == "1-0";
                    var isOpen = moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 d3") == 0
                              || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 Nf6 d3") == 0
                              || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 d6 c3 Nf6 d3") == 0
                              || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 d6 c3 a6 Ba4 Nf6 d3") == 0
                              || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Bc5 d3") == 0
                              || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 Bc5 c3 Nf6 d3") == 0
                              || moves.IndexOf("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Bc5 c3 Nf6 d3") == 0;

                    count++;
                    
                    if (count % 10000 == 0) {
                        Console.WriteLine($"{rCount}/{count}");
                    }

                    if (!(isBlitz && moveCount <= 200 && isWin && isOpen && elos[1] < 10000 && elos[0] - elos[1] >= -1000 && elos.Min() > 2300)) {
                        continue;
                    }

                    rCount++;
                    r.Add($"{elos.Min()},{PrettyPgn(moves)}");
                    //r.Add("");
                }
                File.WriteAllLines("d:/spanish-top.txt", r.OrderByDescending(x => x));
            }
        }


        private static Regex prevSkipMoveRe = new Regex("\\d+\\.+", RegexOptions.Compiled);

        private static string prevSkipMove(string s) {
            var sn = prevSkipMoveRe.Match(s).Value;
            var n = int.Parse(sn.Replace(".", ""));

            return (sn.IndexOf("...") >= 0) ? $"{n}.XX" : $"{n - 1}...XX";
        }

        private static void processMd(string path = null) {
            var srcPath = path ?? Config.current.mdPath;
            var name = Path.GetFileNameWithoutExtension(srcPath);
            var dstPath = Path.Combine(Config.current.mdDstDir, $"{name}.html");

            var book = File.ReadAllLines(bookPath);
            var hub = new MoveInfoHub(moveRe);

            var rss = new List<string>();
            var ss = File.ReadAllLines(srcPath);

            var configStr = ss.Take(10).Where(x => x.IndexOf("<config") >= 0).FirstOrDefault() ?? "<config/>";
            var configTag = Tag.Parse(configStr).Where(x => x.name == "config").First();
            var configVal = "";
            if (configTag.attr.TryGetValue("color", out configVal) && configVal == "-1") {
                book = book.Select(x => x.Replace("flip = false", "flip = true")).ToArray();
            }
            if (configTag.attr.TryGetValue("hilight", out configVal) && configVal == "1") {
                book = book.Select(x => x.Replace(".move-hilight", ".move")).ToArray();
            }

            var lastLevel = 1;
            foreach (var s in ss) {
                if (s == "" || s.IndexOf("![](") == 0 || s.IndexOf("#") == 0) {
                    rss.Add(s);
                    continue;
                }

                var tags = Tag.Parse(s);
                var s2 = Tag.Clear(s);

                foreach (var tag in tags.Where(x => x.name == "addx" && x.attr.ContainsKey("start"))) {
                    tag.name = "add";
                    tag.attr.Add("value", prevSkipMove(tag.attr["start"]));
                }

                var skipAll = tags.Any(x => x.name == "skip" && !x.attr.ContainsKey("start"));
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

                var fen = tags
                    .Where(x => x.name == "fen")
                    .Select(x => x.attr.ContainsKey("value") ? x.attr["value"] : Board.DEFAULT_STARTING_FEN)
                    .FirstOrDefault();

                if (fen != null) {
                    hub.SetFen(fen);
                }

                var levels = getLevels(s2, lastLevel);
                lastLevel = tags.Any(x => x.name == "continue") ? levels.Last().Item2 : 1;

                var breakHandle = false;
                var rs = handleString(s2, moveSeqRe, (x, m) => {
                    if (skipAll || skipStarts.Any(y => x.IndexOf(y) == 0) || breakHandle) {
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
                        hub.Push(level, add, true);
                        adds.Remove(addTuple);
                    }

                    var r = hub.Push(level, x);
                    breakHandle = r.IndexOf("=>") >= 0;
                    return r;
                });

                rs = rs.Replace("(S)", $"<span class=\"move\" fen=\"{hub.Fen}\">(S)</span>");

                rss.Add(rs);

                if (rs.IndexOf("=>") >= 0) {
                    break;
                }
            }

            var html = rss.Where(x => x != "").Select(x => Markdown.ToHtml(x)).ToArray();
            
            File.WriteAllLines(dstPath, book.Concat(html));
        }

        private static IEnumerable<string> GetPgnBodies(StreamReader reader) {
            var prevState = ParseState.Empty;
            var state = ParseState.Empty;
            var body = "";
            while (!reader.EndOfStream) {
                prevState = state;
                var s = reader.ReadLine();

                state = (s == "") ? ParseState.Empty
                    : (s[0] == '[') ? ParseState.Param
                    : ParseState.Moves;

                if (state == ParseState.Moves) {
                    body = $"{body}{(body == "" ? "" : " ")}{s}";
                }

                if (state == ParseState.Empty && prevState == ParseState.Moves) {
                    yield return body;
                    body = "";
                }
            }
        }

        private static Regex evalRe = new Regex("%eval (?<eval>[^\\]]+)", RegexOptions.Compiled);

        private static string normMovesBody(string body) {
            body = handleString(body, Game.CommentRegex, (s,x) => {
                var m = evalRe.Match(s);
                var eval = m.Groups["eval"].Value;
                return eval == "" ? "" : " $" + eval;
            });
            //body = Game.CommentRegex.Replace(body, "");
            body = Game.NumberRegex.Replace(body, "");
            body = Game.ScoreRegex.Replace(body, "");
            body = Game.SpaceRegex.Replace(body, " ");
            body = Game.ResultRegex.Replace(body, "");
            body = body.Trim();
            return body;
        }

        private static HashSet<string> pgnParams = new HashSet<string>(new string[] { "Date", "White", "Black", "Result", "Date", "WhiteElo", "BlackElo", "TimeControl", "Link", "Color" });

        private static void handlePgn() {
            var path = "d:/chess/pgns/_top.pgn";
            var prefix = "panov-";
            var open = "1. e4 c6 2. d4 d5 3. exd5 cxd5 4. c4";
            var openSubs = " ";
            var findColor = 1;

            var fileName = Path.GetFileName(path);
            var dstPath = path.Replace(fileName, prefix + fileName);
            open = string.Join(" ", open.Split(' ').Where(x => x.IndexOf(".") < 0));

            using (var stream = File.OpenRead(path)) {
                var rs = new List<string>();
                var pgns = Pgn.LoadMany(stream).ToArray();

                foreach (var pgn in pgns) {
                    var color = int.Parse(pgn.Params["Color"]);
                    var isWin = color == 1 ? pgn.Params["Result"] == "1-0" : pgn.Params["Result"] == "0-1";
                    var isLoss = color == 1 ? pgn.Params["Result"] == "0-1" : pgn.Params["Result"] == "1-0";
                    var control = int.Parse(pgn.Params["TimeControl"].Split('/')[0].Split('+')[0]);
                    var isOpen = pgn.Moves.IndexOf(open) == 0 && pgn.Moves.IndexOf(openSubs) >= 0;

                    if (!((findColor == color || findColor == 0) && !isLoss && control >= 180 && isOpen)) {
                        continue;
                    }

                    var filtredParams = pgn.Params.Where(x => pgnParams.Contains(x.Key)).Select(x => $"[{x.Key} \"{x.Value}\"]").ToList();
                    filtredParams.ForEach(x => rs.Add(x));

                    rs.Add($"");
                    rs.Add(PrettyPgn(pgn.Moves));
                    rs.Add($"");
                }
                if (rs.Count > 0) {
                    File.WriteAllLines(dstPath, rs);
                }
            }
        }

        private static void evalPgn() {
            var path = Config.current.evalPath;
            var engine = Engine.Open(Config.current.enginePath);

            Func<string, bool> forHandle = x => x != "" && x[0] != '[' && x.IndexOf("{") < 0;
            var rs = File.ReadAllLines(path).ToArray();
            var count = rs.Where(x => forHandle(x)).Sum(x => x.Split(' ').Where(y => y.IndexOf(".") < 0).Count());

            for (var i = 0; i < rs.Length; i++) {
                var s = rs[i];

                if (!forHandle(s)) {
                    continue;
                }

                var moves = Pgn.Load(s).Moves.Split(' ');
                var fen = Board.DEFAULT_STARTING_FEN;
                var infos = new List<MoveInfoEval>();
                var num = 0;
                foreach (var move in moves) {
                    fen = FEN.Move(fen, move);
                    infos.Add(new MoveInfoEval() { fen = fen, moveSan = move, num = num });
                    num++;
                }

                foreach (var info in infos) {
                    if (ctrlC) {
                        break;
                    }

                    try {
                        info.eval = engine.CalcScore(info.fen, depth: Config.current.evalDepth);
                    } catch { ctrlC = true; }

                    count--;
                    Console.WriteLine(count);
                }

                if (ctrlC) {
                    break;
                }

                rs[i] = string.Join(" ", infos.Select(x => $"{x.numStr} {x.moveSan} {{{x.evalStr}}}"));
            }

            File.WriteAllLines(path, rs);
            engine.Dispose();
        }

        private static void handleChessable() {
            var src = "d:/chess/pgns/everyman.pgn";
            var dst = "d:/everyman-panov.pgn";
            var open = "1. e4 c6 2. d4 d5 3. exd5 cxd5 4. c4";
            var except = "Panov";

            open = string.Join(" ", open.Split(' ').Where(x => x.IndexOf(".") < 0));
            var rs = new List<string>();
            using (var stream = File.OpenRead(src)) {
                foreach (var pgn in Pgn.LoadMany(stream)) {
                    if (pgn.Moves.IndexOf(open) == 0 && pgn.Params["File"].IndexOf(except) == -1) {
                        rs.Add(pgn.ToString());
                    }
                }
            }
            File.WriteAllLines(dst, rs);
        }

        private static Queue<CancellationTokenSource> delayCtsQue = new Queue<CancellationTokenSource>();
        private static Task lastChangeTask = Task.Run(() => { });

        private static void mdMonitor() {
            using (var watcher = new FileSystemWatcher(@"d:/Projects/smalls")) {
                watcher.Filter = "*.md";
                watcher.EnableRaisingEvents = true;
                watcher.Changed += (object sender, FileSystemEventArgs e) => {
                    if (e.ChangeType != WatcherChangeTypes.Changed || e.Name[0] == '~') {
                        return;
                    }

                    while (delayCtsQue.Count > 0) {
                        var cts = delayCtsQue.Dequeue();
                        cts.Cancel();
                        cts.Dispose();
                    }

                    var delayCts = new CancellationTokenSource();
                    delayCtsQue.Enqueue(delayCts);

                    lastChangeTask = lastChangeTask.ContinueWith(async t => {
                        await Task.Delay(1000, delayCts.Token);
                        if (delayCts.IsCancellationRequested) return;
                       
                        processMd(e.FullPath);
                        Console.WriteLine(e.FullPath);
                    });
                };
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
            }
        }

        private static Regex svgTagRe = new Regex("<svg.*?</svg>", RegexOptions.Multiline);
        private static Dictionary<string, string> pieceAbbr = new Dictionary<string, string>() {
            { "knight", "N" }, { "bishop", "B" }, { "queen", "Q" }, { "rook", "R" }, { "king", "K" } };

        private static void simplifyChessable() {
            var src = "d:/catalan-b-ss.html";
            var dst = "d:/catalan-b-ss-2.html";
            var s = File.ReadAllText(src);

            s = svgTagRe.Replace(s, "");

            s = handleString(s, Tag.tagRe, (x, m) => {
                var tag = Tag.Parse(x)[0];
                var piece = (string)null;
                if (!tag.attr.TryGetValue("data-piece", out piece)) {
                    return x;
                }

                return x + pieceAbbr[piece];
            });

            s = handleString(s, Tag.tagRe, (x, m) => {
                var tag = Tag.Parse(x)[0];
                var @class = (string)null;
                var classes = new HashSet<string>();

                if (tag.attr.TryGetValue("class", out @class)) {
                    @class.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList().ForEach(y => classes.Add(y));
                }

                if ((new string[] { "blackMove", "openingNum", "commentMoveSmall" }).Any(y => classes.Contains(y))) {
                    x = " " + x;
                }

                if (classes.Contains("commentInMove")) {
                    x = "<p>" + x;
                }

                return x;
            });

            File.WriteAllText(dst, s);
        }

        private static Regex moveRuRe = new Regex("[a-fасе]?[хx]?[a-fасе][1-8]", RegexOptions.Compiled);
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

            mdMonitor();
            /*
            FEN.Move("3rr1k1/1bq2pb1/2p1nnpp/1p2p3/P1p1P3/5NNP/2QB1PP1/R3RBK1 w - - 0 25", "axb5");

            Console.WriteLine("END");
            Console.ReadLine();
            */
            // 
            // handlePgn();

            /*
            var path = @"d:\Projects\smalls\ruy-lopez-mbm.md";
            var rs = File.ReadAllLines(path);
            var re = new Regex($"\\.\\.\\. ({moveReS})");
            rs = rs.Select(x => handleString(x, re, (s,m) => s.Replace(" ", ""))).ToArray();
            File.WriteAllLines(path, rs);
            */

            /*
            var fn = (Config.current.fn.Split(' ').Where(x => x[0] == '*').FirstOrDefault() ?? "*").Substring(1);
            switch (fn) {
                case "md":
                    processMd();
                    break;
                case "eval":
                    evalPgn();
                    break;
            }
            */
            //findGames();
            //handlePgn();

            //Console.WriteLine("Save? (y/n)");
            //if (Console.ReadLine() == "y") {
            // File.WriteAllLines("d:/lichess.json", nodes.Select(x => JsonConvert.SerializeObject(x)));
            //}
        }
    }
}

/*
var dir = "d:/chess/chessable/_all";
var paths = Directory.GetFiles(dir, "*.pgn", SearchOption.AllDirectories);

var writer = new StreamWriter(File.OpenWrite("d:/chessable.pgn"));
foreach (var path in paths) {
    var fileName = path.Substring(dir.Length + 1).Replace("\\", "/").Replace(".pgn", "");
    fileName = string.Concat(fileName.Skip(Math.Max(0, fileName.Length - 120)));
    Console.WriteLine(fileName);
    using (var stream = File.OpenRead(path)) {
        foreach (var pgn in Pgn.LoadMany(stream)) {
            pgn.Params.Add("File", fileName);
            writer.Write(pgn.ToString());
         }
    }
}

Console.ReadLine();
*/

/*
            var nullCount = dic.Values.Count(x => x.score == null);
            Console.WriteLine($"left: {nullCount}");
            Console.WriteLine($"{((float)(dic.Count - nullCount) * 100 / dic.Count).ToString("0.00")}%");
*/

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