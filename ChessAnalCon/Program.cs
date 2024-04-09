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
using Chess.Sunfish;
using Newtonsoft.Json;
using Markdig;
using System.Globalization;
using ChessEngine;
using System.Net;
using Lichess;
using System.Threading;
using System.CodeDom;

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
                return fen.Contains(" w ") ? 1 : -1;
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

        public string prev { get; set; }

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

        public string GetLastFen() {
            if (list.Count == 0 || list.Last().Count == 0) return Fen;

            return list.Last().Last().fen;
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

        public MoveInfo Push(int level, int index, string move, bool skipId = false) {
            var prev = getPrev(level, index);
            var err = false;
            if (move.Contains("+")) {
                var uci = FEN.San2Uci(prev.fen, move);
                var san = FEN.Uci2San(prev.fen, uci);
                err = move != san;
            }
            if (!skipId) id++;
            var mi = new MoveInfo() { id = skipId ? null : $"move{id}", prev = prev.id, fen = FEN.Move(prev.fen, move), moveSan = move, moveUci = move == "--" ? null : FEN.San2Uci(prev.fen, move), err = err };
            this[level, index] = mi;

            return mi;
        }

        public string Push(int level, string moves, bool skipId = false) {
            var index = 0;
            var isEx = false;
            return Program.handleString(moves, re, (x, m) => {
                if (isEx) {
                    return x;
                }

                var move = m.Groups["move"].Value + m.Groups["ccm"].Value;
                var num = m.Groups["num"].Value;
                if (num != "") {
                    index = (int.Parse(num.Replace(".", "")) - 1) * 2 + (num.Contains("...") ? 1 : 0);
                }
                else {
                    index++;
                }

                var mi = (MoveInfo)null;
                try {
                    mi = Push(level, index, move, skipId);
                }
                catch { }

                if (mi == null) {
                    isEx = true;
                    return $"=>{x}";
                }

                return $"<span id='{mi.id}' prev='{mi.prev}' class='move' fen='{FEN.StrictEnPassed(mi.fen)}' uci='{mi.moveUci}' san='{mi.moveSan}'>{(mi.err ? "=>" : "")}{x}</span>";
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

        public static string Clear(string s, HashSet<string> names = null) {
            return Program.handleString(s, tagRe, (x, m) => (names == null || names.Contains(m.Groups["name"].Value)) ? "" : x);
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
            var files = Directory.EnumerateFiles("d:/nimzo-lysyy").Where(x => x.Contains(prefix));

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

        public static IEnumerable<string> enumReString(string s, Regex re) {
            var m = re.Match(s);
            var i = 0;

            while (m.Success) {
                var ls = s.Substring(i, m.Index - i);
                yield return ls;
                ls = s.Substring(m.Index, m.Length);
                yield return ls;
                i = m.Index + m.Length;
                m = m.NextMatch();
            }
            var ls2 = s.Substring(i, s.Length - i);
            yield return ls2;
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
                var shield = i > 0 && s[i-1] == '\\';
                if (c == '*' && i > 0 && s[i - 1] == '*') {
                    bold = !bold;
                    level = bold ? 0 : 1;
                    r.Add(new Tuple<int, int>(i, level));
                }

                if (c == '(' && !shield) { level++; r.Add(new Tuple<int, int>(i, level)); }
                else if (c == ')' && level > 1 && !shield) { level--; r.Add(new Tuple<int, int>(i, level)); }
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
                    var isOpen = moves.StartsWith("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 d3")
                              || moves.StartsWith("e4 e5 Nf3 Nc6 Bb5 Nf6 d3")
                              || moves.StartsWith("e4 e5 Nf3 Nc6 Bb5 d6 c3 Nf6 d3")
                              || moves.StartsWith("e4 e5 Nf3 Nc6 Bb5 d6 c3 a6 Ba4 Nf6 d3")
                              || moves.StartsWith("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Bc5 d3")
                              || moves.StartsWith("e4 e5 Nf3 Nc6 Bb5 Bc5 c3 Nf6 d3")
                              || moves.StartsWith("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Bc5 c3 Nf6 d3");

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

            return (sn.Contains("...")) ? $"{n}.--" : $"{n - 1}...--";
        }

        private static Dictionary<char, char> meridaChars = new Dictionary<char, char> {
            { 'P', 'p' }, { 'N', 'n' }, { 'B', 'b' }, { 'R', 'r' }, { 'Q', 'q' }, { 'K', 'k' },
            { 'p', 'o' }, { 'n', 'm' }, { 'b', 'v' }, { 'r', 't' }, { 'q', 'w' }, { 'k', 'l' }, { '.', '*' }, { '/', '/' }
        };

        private static string fen2diagram(string fen, int color = 0) {
            if (color == 0) {
                color = fen.Contains(" w ") ? 1 : -1;
            }
            var f = fen.Split(' ')[0];
            f = color == 1 ? f : new string(f.Reverse().ToArray());
            for (var i = 1; i <= 8; i++) {
                f = f.Replace(i.ToString(), new string('.', i));
            }

            var fc = f.Select(c => meridaChars[c]).ToArray();
            for (var i = 0; i < fc.Length; i++) {
                if (i % 2 == 0 || fc[i] == '/') continue;
                fc[i] = char.IsLetter(fc[i]) ? char.ToUpper(fc[i]) : '+';
            }

            return new string(fc);
        }

        private static HashSet<string> clearTags = new HashSet<string>() { "add", "addz", "level", "skip", "config", "fen", "fend", "confinue", "diagram" };
        private static Regex headerRe = new Regex("^#+ ", RegexOptions.Compiled);

        private static void processMd(string path = null) {
            var srcPath = path ?? Config.current.mdPath;
            var name = Path.GetFileNameWithoutExtension(srcPath);
            var dstPath = Path.Combine(Config.current.mdDstDir, $"{name}.html");

            var book = File.ReadAllLines(bookPath);
            var hub = new MoveInfoHub(moveRe);

            var rss = new List<string>();
            var ss = File.ReadAllLines(srcPath).Where(x => x != "").ToArray();

            var configStr = ss.Take(10).Where(x => x.Contains("<config")).FirstOrDefault() ?? "<config/>";
            var configTag = Tag.Parse(configStr).Where(x => x.name == "config").First();
            var configVal = "";
            if (configTag.attr.TryGetValue("color", out configVal) && configVal == "-1") {
                book = book.Select(x => x.Replace("flip = false", "flip = true")).ToArray();
            }
            if (configTag.attr.TryGetValue("top", out configVal) && configVal == "1") {
                book = book.Select(x => x.Replace("topPanel = false", "topPanel = true")).ToArray();
            }
            if (configTag.attr.TryGetValue("dnd", out configVal) && configVal == "1") {
                book = book.Select(x => x.Replace("dnd = false", "dnd = true")).ToArray();
            }
            if (configTag.attr.TryGetValue("hilight", out configVal) && configVal == "1") {
                book = book.Select(x => x.Replace(".move-hilight", ".move")).ToArray();
            }
            if (configTag.attr.TryGetValue("eval", out configVal) && configVal == "0") {
                book = book.Select(x => x.Replace("evalFen = true", "evalFen = false")).ToArray();
            }

            if (configTag.attr.TryGetValue("offline", out configVal) && configVal == "1") {
                var jqeury = "<script>\r\n" + File.ReadAllText("d:/Projects/smalls/jquery-2.2.4.min.js") + "</script>\r\n";
                for (var i = 0; i < book.Length; i++) {
                    if (book[i].StartsWith("<script src=\"https://code.jquery.com")) {
                        book[i] = jqeury;
                        break;
                    }
                }
            }

            var content = new List<string>();
            var lastLevel = 1;
            foreach (var s in ss) {
                var headerMatch = headerRe.Match(s);
                if (headerMatch.Success) {
                    var n = headerMatch.Value.Length - 1;
                    var header = s.Substring(n + 1);
                    content.Add($"<p class=\"indent{n}\"><a href=\"#ref{content.Count}\"><b>{header}</b></a></p>\r\n");
                    rss.Add($"{headerMatch.Value}<a name=\"ref{content.Count-1}\"></a>{header}");
                    continue;
                }

                var tags = Tag.Parse(s).ToList();
                var s2 = Tag.Clear(s, clearTags);
                
                foreach (var tag in tags.Where(x => x.name == "level" && !x.attr.ContainsKey("value"))) {
                    tag.attr.Add("value", "+1");
                }

                foreach (var tag in tags.Where(x => x.name == "addz" && x.attr.ContainsKey("start"))) {
                    tag.name = "add";
                    tag.attr.Add("value", prevSkipMove(tag.attr["start"]));
                }

                var diagrams = tags.Where(x => x.name == "diagram").ToArray();
                diagrams.Where(d => d.attr.ContainsKey("apply") && d.attr.ContainsKey("fen")).ToList()
                    .ForEach(d => tags.Add(Tag.Parse($"<fen value=\"{d.attr["fen"]}\">")[0]));

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
                    if (skipAll || skipStarts.Any(y => x.StartsWith(y)) || breakHandle) {
                        return x;
                    }

                    var level = levels.TakeWhile(y => y.Item1 < m.Index).Last().Item2;
                    level += levelAll;
                    var levelTagTuple = levelTags.Where(y => x.StartsWith(y.Item1)).FirstOrDefault();
                    var levelTag = levelTagTuple?.Item2;
                    if (levelTag != null) {
                        level += levelTag.Value;
                        levelTags.Remove(levelTagTuple);
                    }

                    var addTuple = adds.Where(y => x.StartsWith(y.Item1)).FirstOrDefault();
                    var add = addTuple?.Item2;
                    if (add != null) {
                        hub.Push(level, add, true);
                        adds.Remove(addTuple);
                    }

                    var r = hub.Push(level, x);
                    breakHandle = r.Contains("=>");
                    return r;
                });

                foreach (var d in diagrams) {
                    var val = (string)null;
                    var _fen = d.attr.TryGetValue("fen", out val) ? val : hub.GetLastFen();
                    var color = d.attr.TryGetValue("color", out val) ? int.Parse(val) : 1;

                    rs += $"<div class=\"diagram\" fen=\"{_fen}\">" + fen2diagram(_fen, color).Replace("/", "<br/>") + "</div>";
                }

                var fend = tags
                    .Where(x => x.name == "fend")
                    .Select(x => x.attr.ContainsKey("value") ? x.attr["value"] : Board.DEFAULT_STARTING_FEN)
                    .FirstOrDefault();

                if (fend != null) {
                    hub.SetFen(fend);
                }

                rss.Add(rs);

                if (rs.Contains("=>")) {
                    break;
                }
            }

            string[] ts1 = null;
            string[] ts2 = null;
            if (configTag.attr.TryGetValue("translate", out configVal)) {
                var paths = configVal.Split(';');
                ts1 = File.ReadAllLines(paths[0]);
                if (paths.Length > 1) {
                    ts2 = File.ReadAllLines(paths[1]);
                }
            }

            var html = new List<string>();
            for (var i = 0; i < rss.Count; i++) {
                var s = rss[i];
                if (s == "") continue;

                if (ts1 == null) {
                    html.Add(Markdown.ToHtml(s));
                    continue;
                }

                var t1 = (i < ts1.Length) ? ts1[i] : "";
                var t2 = (ts2 != null && i < ts2.Length) ? ts2[i] : "";
                html.Add($"<table class=\"colums{(ts2 == null ? "2" : "3" )}\"><tr><td>{Markdown.ToHtml(s)}</td><td>{Markdown.ToHtml(t1)}</td>{(ts2 == null ? "" : $"<td>{Markdown.ToHtml(t2)}</td>")}</tr></table>");
            }

            for (var i = 0; i < Math.Min(100, html.Count); i++) {
                if (html[i].Contains("<content/>")) {
                    html[i] = string.Join("", content);
                }
            }
            
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
            var path = "d:/esserman-li.pgn";
            var login = "MassterofMayhem";
            var prefix = "morra-";
            var open = "1. e4 c5 2. d4";
            var openSubs = " ";
            var fullMoves = true;
            var findColor = 1;

            var fileName = Path.GetFileName(path);
            var dstPath = path.Replace(fileName, prefix + fileName);
            open = string.Join(" ", open.Split(' ').Where(x => !x.Contains(".")));

            using (var stream = File.OpenRead(path)) {
                var rs = new List<string>();
                var pgns = Pgn.LoadMany(stream).ToArray();

                foreach (var pgn in pgns) {
                    //var color = int.Parse(pgn.Params["Color"]);
                    var color = pgn.Params["White"] == login ? 1 : -1;
                    pgn.Params.Add("Color", color.ToString());
                    var isWin = color == 1 ? pgn.Params["Result"] == "1-0" : pgn.Params["Result"] == "0-1";
                    var isLoss = color == 1 ? pgn.Params["Result"] == "0-1" : pgn.Params["Result"] == "1-0";
                    var control = int.Parse(pgn.Params["TimeControl"].Split('/')[0].Split('+')[0]);
                    var isOpen = pgn.Moves.StartsWith(open) && pgn.Moves.Contains(openSubs);

                    if (!((findColor == color || findColor == 0) && !isLoss && control >= 180 && isOpen)) {
                        continue;
                    }

                    var site = (string)null;
                    pgn.Params.TryGetValue("Site", out site);
                    if (site != null && site.Contains("lichess.org")) {
                        pgn.Params.Add("Link", site);
                    }

                    var filtredParams = pgn.Params.Where(x => pgnParams.Contains(x.Key)).Select(x => $"[{x.Key} \"{x.Value}\"]").ToList();
                    filtredParams.ForEach(x => rs.Add(x));

                    rs.Add($"");
                    if (fullMoves) {
                        rs.Add(string.Join(" ", pgn.MovesSource).Replace("  ", " "));
                    }
                    else {
                        rs.Add(PrettyPgn(pgn.Moves));
                    }
                    
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

            Func<string, bool> forHandle = x => x != "" && !x.StartsWith("[") && !x.Contains("{");
            var rs = File.ReadAllLines(path).ToArray();
            var count = rs.Where(x => forHandle(x)).Sum(x => x.Split(' ').Where(y => !y.Contains(".")).Count());

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

        private static void handleCbHtml(string path) {
            var s = File.ReadAllText(path);
            s = s.Replace("0-0-0", "O-O-O").Replace("0-0", "O-O");
            s = handleString(s, new Regex(@"<b>\w\d*\) </b>"), (x,m) => x.Replace("<b>", "var_").Replace("</b>", ""));
            s = s.Replace("<i>", "").Replace("</i>", "");
            s = s.Replace("<b></b> [", "<p>").Replace("</b> [", "</b>").Replace("] <b>", "<b>");
            s = Regex.Replace(s, @" *\*</b>", "</b>");
            s = s.Replace("<b></b>", "");
            s = s.Replace("<b>", "<p><b>").Replace("</b>", "</b><p>");
            s = s.Replace("<p>. ", "<p>");

            s = s.Replace("~~", "∞").Replace("+/=", "⩲").Replace("=/+", "⩱").Replace("+/-", "±").Replace("-/+", "∓")
                .Replace("+-", "+−").Replace("-+", "−+").Replace("(.)", "⨀").Replace("|^", "↑")
                .Replace("->", "→").Replace("~/=", "=/∞").Replace("<=>", "⇆");

            s = handleString(s, new Regex(moveNEvalReS), (x, m) => x.Replace("@", "⟳"));

            s = Regex.Replace(s, "..StartBracket..", "(");
            s = Regex.Replace(s, "..EndBracket..", ")");
            s = Regex.Replace(s, "..(StartFEN|EndFEN)..", " ");

            //s = s.Replace("(semicolon)", ";");

            var ext = Path.GetExtension(path);
            var newPath = path.Replace(ext, "-2" + ext);
            File.WriteAllText(newPath, s);
        }

        private static int getMoveCount(string s) {
            var ms = moveSeqRe.Matches(s);
            var sb = new StringBuilder();
            foreach (var m in ms) {
                sb.Append(((Match)m).Value);
                sb.Append(" ");
            }
            var r = sb.ToString().Replace(".", ". ")
                .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(x => !x.Contains("."));
            return r;
        }

        private static int[] get2dLevels(string s) {
            var levels = getLevels(s).ToArray();
            var pl = levels.Last();
            var ns = new List<int>();
            foreach (var l in levels.Reverse().Skip(1)) {
                if (pl.Item2 == 1 || l.Item2 == 1) {
                    ns.Add(pl.Item1 + l.Item2 - 1);
                }
                pl = l;
            }

            var rs = new List<int>();
            for (var i = 0; i < ns.Count() - 1; i += 2) {
                if (getMoveCount(s.Substring(ns[i + 1], ns[i] - ns[i + 1])) > 0) {
                    rs.Add(ns[i]);
                    rs.Add(ns[i+1]);
                }
            }

            return rs.Reverse<int>().ToArray();
        }

        private static void handleCbMd(string path) {
            var nlRe = new Regex(@" ?--- | ---|;? ?var_[a-z]\d*\) |; ?", RegexOptions.Compiled);

            var ss = File.ReadAllLines(path).Where(x => x != "").ToArray();

            for (var i = 1; i <= 3; i++) {
                ss = ss.SelectMany(s => {
                    if (s.StartsWith("**") && !s.StartsWith("### ")) return Enumerable.Repeat(s, 1);

                    var ls = get2dLevels(s).Reverse().ToArray();
                    switch (i) {
                        case 1:
                            if (ls.Length == 0) break;

                            var tail = s.Substring(ls[0], s.Length - ls[0]);

                            if (getMoveCount(tail) > 0) break;

                            s = s.Remove(ls[0] - 1, 1);
                            s = s.Insert(ls[0] - 1, (tail.Length <= 2) ? "\n" : "{warning: close bracket}\n");
                            s = s.Remove(ls[1], 1);
                            s = s.Insert(ls[1], (tail.Length <= 2) ? "\n" : "\n{warning: open bracket}");

                            break;
                        case 2:
                            var levels = getLevels(s);
                            s = handleString(s, nlRe, (x, m) => {
                                var level = levels.TakeWhile(y => y.Item1 < m.Index).Last().Item2;
                                return level == 1 ? "\n" : x;
                            });

                            break;
                        case 3:
                            for (var j = 0; j < ls.Length; j+=2) {
                                var bs = s.Substring(ls[j + 1], ls[j] - ls[j + 1]);
                                var mc = getMoveCount(bs);
                                if (!(bs.Length > 35 || mc > 2)) continue;
                                s = s.Insert(ls[j], "\n");
                                s = s.Insert(ls[j+1], "\n");
                            }

                            break;
                    }

                    return s.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());

                }).ToArray();
            }

            var ext = Path.GetExtension(path);
            var newPath = path.Replace(ext, "-2" + ext);
            File.WriteAllLines(newPath, ss.Select(s => s.Replace("(semicolon)", ";") + "\r\n")); ;
        }

        private static Regex scidAltRe = new Regex(@"alt=""([^""]*)""");

        private static string getFenFromScidHtml(string html) {
            var m = scidAltRe.Match(html);
            var ss = new List<string>();
            while (m.Success) {
                ss.Add(m.Groups[1].Value);
                m = m.NextMatch();
            }

            if (ss.Count != 64) return "NON64";

            var s = string.Join("", ss.Select(x => x == "" || x[0] == ':' ? "1" : x[0] == 'W' ? x[1].ToString() : x[1].ToString().ToLower()));
            for (var i = 8; i < 70; i += 9) {
                s = s.Insert(i, "/");
            }
            s = s.Replace("11111111", "8").Replace("1111111", "7").Replace("111111", "6")
                .Replace("11111", "5").Replace("1111", "4").Replace("111", "3").Replace("11", "2");
            
            return "FEN: " + s + " w KQkq - 0 1";
        }

        private static void handleScidHtml(string path) {
            var s = File.ReadAllText(path);

            s = Regex.Replace(s, @"<body[^>]*>", "<body><hr>");
            s = Regex.Replace(s, "</?center>", "");
            s = Regex.Replace(s, "<table ", "</b></p><p><b><table ");
            s = Regex.Replace(s, "<dl><dd>", "<p>");
            s = Regex.Replace(s, "</dl>", "</p>");
            s = Regex.Replace(s, "<hr></p>", "</p><hr>");
            s = Regex.Replace(s, @"<b>\*</b>", "");
            s = Regex.Replace(s, @"<br>", " ");

            s = handleString(s, new Regex(@"<table(.|\n)*?</table>", RegexOptions.Multiline), (x,m) => {
                return getFenFromScidHtml(x);
            });

            s = handleString(s, new Regex(@"<hr>(.|\n)*?</p>", RegexOptions.Multiline), (x, m) => {
                return x.Replace("<p>", "<h3>").Replace("</p>", "</h3>").Replace("<hr>", "").Replace("<b>","").Replace("</b>", "");
            });

            s = Regex.Replace(s, @"<hr>", "");

            var ext = Path.GetExtension(path);
            var newPath = path.Replace(ext, "-2" + ext);
            File.WriteAllText(newPath, s);
        }

        private static void handleScidMd(string path) {
            var ss = File.ReadAllLines(path).Where(x => x != "").ToArray();

            var reBraces = new Regex(@"^\( .*? \)$", RegexOptions.Compiled);
            var reBold = new Regex(@"^\*\*.*?\*\*$", RegexOptions.Compiled);
            var reBoldMoves = new Regex(@"^\*\*\d+\..*?\*\*$", RegexOptions.Compiled);
            var reHeader = new Regex(@"^### ", RegexOptions.Compiled);
            var reHeaderNum = new Regex(@"^### \(\d+\)", RegexOptions.Compiled);
            var moveRe = new Regex($"\\d+\\.+ +({moveReS.Replace("|--", "")})");
            var nlRe = new Regex(" --- ");
            var headerN = 1;
            for (var i = 0; i < ss.Length; i++) {
                var s = ss[i];

                s = handleString(s, moveRe, (x,m) => x.Replace(" ", ""));
                
                if (reBraces.IsMatch(s)) {
                    s = s.Substring(2, s.Length - 4);
                }

                if (reBold.IsMatch(s) && !reBoldMoves.IsMatch(s)) {
                    s = s.Substring(2, s.Length - 4);
                }

                if (reHeader.IsMatch(s) && !reHeaderNum.IsMatch(s)) {
                    s = s.Replace("### ", $"### ({headerN}) ");
                    headerN++;
                }

                if (!s.StartsWith("**") && !s.StartsWith("### ")) {
                    var levels = getLevels(s);
                    
                    s = handleString(s, nlRe, (x,m) => {
                        var level = levels.TakeWhile(y => y.Item1 < m.Index).Last().Item2;
                        return level == 1 ? "\r\n\r\n" : x;
                    });
                }

                ss[i] = s;
            }

            var ext = Path.GetExtension(path);
            var newPath = path.Replace(ext, "-2" + ext);
            File.WriteAllLines(newPath, ss.Select(x => x + "\r\n"));
        }

        private static void pgnSearch() {
            var onlyFiles = true;
            var src = "d:/chess/pgns/_all.pgn";
            var dst = "d:/sicil-alapin-files.txt";
            var open = "1. e4 c5 2. Nf3 e6 3. c3";
            var except = "asdfasdfasdf";
            open = string.Join(" ", open.Split(' ').Where(x => !x.Contains(".")));
            var rs = new List<string>();
            var files = new List<string>();
            using (var stream = File.OpenRead(src)) {
                foreach (var pgn in Pgn.LoadMany(stream)) {
                    if (pgn.Moves.StartsWith(open) && !pgn.Params["File"].Contains(except)) {
                        if (onlyFiles) {
                            rs.Add(pgn.Params["File"]);
                        }
                        else {
                            rs.Add(pgn.ToString());
                        }
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
            var src = "d:/e4-gambits.html";
            var dst = "d:/e4-gambits-2.html";
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

            s = "<style> div { display: inline; } </style>\r\n" + s;

            File.WriteAllText(dst, s);
        }

        private static void removeChessableDublicates(string path) {
            var ss = File.ReadAllLines(path).Where(x => x.Trim() != "").ToArray();
            var ext = Path.GetExtension(path);
            path = path.Replace(ext, "-2" + ext);
            var nRe = new Regex(@"\.\s*", RegexOptions.Compiled);
            var startRe = new Regex(@"^\d+\.", RegexOptions.Compiled);
            var rs = new List<string>();
            var hs = new HashSet<string>();
            var last = "";
            foreach (var s in ss) {
                if (s.StartsWith("#")) {
                    rs.Add(s);
                    continue;
                }

                if (s.StartsWith("**")) {
                    rs.Add(s);
                    var _s = s.Replace("**", "").Trim();

                    if (!startRe.IsMatch(_s)) continue;

                    var xs = nRe.Replace(_s, ". ").Split(' ');
                    var n = Pgn.ParseNum(xs[0]);
                    xs = xs.Where(x => !x.Contains(".")).ToArray();
                    n += xs.Length - 1;
                    last = Pgn.NumToString(n) + xs.Last();
                    continue;
                }

                var k = last + s + " ";
                if (hs.Contains(k)) {
                    continue;
                }

                rs.Add(s);
                hs.Add(k);
            }

            File.WriteAllLines(path, rs.Select(x => x + "\r\n"));
        }

        private static void splitDeepl(string path) {
            var qs = new Queue<string>(File.ReadAllLines(path));
            var ext = Path.GetExtension(path);
            path = path.Replace(ext, "-d" + ext);

            var rs = new List<string>();
            while (qs.Count > 0) {
                var ms = new List<string>();
                var len = 0;
                while (qs.Count > 0 && qs.Peek().Length + len < 4900) {
                    var _s = qs.Dequeue();
                    len += _s.Length + 2;
                    ms.Add(_s);
                }

                rs.AddRange(ms);
                rs.Add("=============================");
            }

            File.WriteAllLines(path, rs);
        }

        private static void prepareTranslate(string path) {
            var ext = Path.GetExtension(path);
            var ss = File.ReadAllLines(path).Where(x => x != "").ToArray();
            path = path.Replace(ext, ".txt");
            for (var i = 0; i < ss.Length; i++) {
                var s = ss[i];
                //s = $"nmbr{i.ToString("000000")} {Tag.Clear(s)}";
                s = $"{Tag.Clear(s)}";
                ss[i] = s;
            }
            File.WriteAllLines(path, ss);
        }

        private static void fixGoogleTranslate(string path) {
            var ext = Path.GetExtension(path);
            var ss = File.ReadAllLines(path);
            path = path.Replace(ext, "-2" + ext);

            ss = ss.Select(s => handleString(s, new Regex("(Кр|[КСЛФ])?[a-h]?[1-8]?x?[a-h][1-8]|OOO?"), (x,m) => {
                return x.Replace("Кр", "K").Replace("К", "N").Replace("С", "B").Replace("Л", "R").Replace("Ф", "Q")
                    .Replace("OOO", "O-O-O").Replace("OO", "O-O");
            })).ToArray();

            File.WriteAllLines(path, ss);
        }

        private static IList<EngineCalcResult> puzzleCalc(Engine engine, string fen, int depth, int mateDepthDeff = -2) {
            var unmateScores = new ConcurrentDictionary<string, EngineCalcResult>();
            var mateAfterScores = new ConcurrentDictionary<string, EngineCalcResult>();
            var scores = (IList<EngineCalcResult>)null;
            var color = fen.Contains(" w ") ? 1 : -1;

            foreach (var _scores in engine.CalcScores(fen, depth: depth)) {
                foreach (var score in _scores.Where(s => Math.Abs(s.score) <= 20000 && s.san1st != null)) {
                    unmateScores.AddOrUpdate(score.san1st, score, (k,s) => score);
                }
                foreach (var score in _scores.Where(s => Math.Abs(s.score) > 20000 && s.san1st != null)) {
                    var existScore = (EngineCalcResult)null;
                    if (unmateScores.TryGetValue(score.san1st, out existScore)) {
                        mateAfterScores.TryAdd(score.san1st, existScore);
                    }
                }
                scores = _scores;
            }

            if (scores == null) return new EngineCalcResult[] { };

            scores = scores.Select(s => {
                var score = (EngineCalcResult)null;
                var sAbs = Math.Abs(s.score);
                if (Math.Abs(s.score) <= 20000 || (sAbs <= 30000 && 30000 - sAbs <= (depth + mateDepthDeff) * 100))
                    return s;
                else if (s.san1st != null && mateAfterScores.TryGetValue(s.san1st, out score))
                    return score;
                else return null;
            }).Where(s => s != null).OrderByDescending(s => s.score * color).ToArray();

            return scores;
        }

        private static void solvePuzzles(string path, int limit, bool mateOnly = false, string enginePath = null) {
            if (enginePath == null) {
                enginePath = Config.current.enginePath;
            }

            limit = limit * 2 - 1 + 2;

            var pgns = (Pgn[]) null;
            using (var stream = File.OpenRead(path)) {
                pgns = Pgn.LoadMany(stream).ToArray();
            }

            var engine = Engine.Open(enginePath);

            Func<IList<string>, int, string> pretty = (ms,c) => c == 1 ? Pgn.PrettyMoves(string.Join(" ", ms)) : Pgn.PrettyMoves("-- " + string.Join(" ", ms), 1);

            foreach (var pgn in pgns.Where(x => x.Moves == "" && !x.Params.ContainsKey("Error"))) {
                var fen = pgn.Params["FEN"];
                var color = fen.Contains(" w ") ? 1 : -1;
                var ms = new List<string>();
                Console.WriteLine(fen);

                var kings = string.Concat(fen.Split(' ')[0].Where(x => char.ToUpper(x) == 'K').OrderBy(x => x));
                var fenSplit = fen.Split(' ')[0].Split('/');
                var pawn0 = (fenSplit[0] + fenSplit[7]).ToUpper().Contains("P");

                if (kings != "Kk" || pawn0) {
                    pgn.Params.Add("Error", "fen");
                    Console.WriteLine("Error: fen");
                    continue;
                }

                for (var depth = limit; depth > 0; depth-=2) {
                    if (FEN.GetMateState(fen) != null) {
                        if (depth == limit) {
                            pgn.Params.Add("Error", "none");
                        }
                        break;
                    }

                    var scores = (IList<EngineCalcResult>)(new EngineCalcResult[] { });
                    try {
                        scores = puzzleCalc(engine, fen, depth);
                    } catch {
                        ctrlC = true;
                        break;
                    }

                    if (scores.Count == 0) {
                        if (depth == limit) {
                            pgn.Params.Add("Error", "none");
                        }
                        break;
                    } 
                    
                    var _ms = (scores.First().san ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (depth == limit) {
                        if (_ms.Count == 0) {
                            pgn.Params.Add("Error", "none");
                            break;
                        }

                        if (Math.Abs(scores.First().score) > 20000 && _ms.Count <= limit - 2) {
                            ms = _ms;
                            break;
                        }
                        else if (mateOnly) {
                            pgn.Params.Add("Error", "continue");
                            break;
                        }
                    }

                    var scoreDiff = (scores.Count > 1) ? Math.Abs(scores[0].score - scores[1].score) : 30000; 
                    if (scoreDiff < 150) {
                        if (depth == limit) {
                            pgn.Params.Add("Error", "dirty");
                            pgn.MovesSource = new List<string>() { "{ " + pretty(_ms, color) + $"; ({scoreDiff / 100})" + pretty(scores[1].san.Split(' '), color) + "}" };
                        }
                        break;
                    }

                    if (depth == 1) {
                        pgn.Params.Add("Error", "limit");
                        ms.Clear();
                        break;
                    }

                    ms.Add(_ms[0]);
                    try {
                        fen = FEN.Move(fen, _ms[0]);
                    } catch {
                        pgn.Params.Add("Error", "parse");
                        ms.Clear();
                        break;
                    }
                    if (FEN.GetMateState(fen) != null) break;

                    if (_ms.Count < 2) {
                        if (FEN.GetMateState(fen) != null) {
                            break;
                        }
                        try {
                            scores = puzzleCalc(engine, fen, depth: depth - 1);
                        }
                        catch {
                            ctrlC = true;
                            break;
                        }

                        if (scores.Count == 0) { break; }

                        var move = scores.First().san1st;
                        _ms.Add(move);
                    }
                    ms.Add(_ms[1]);
                    try {
                        fen = FEN.Move(fen, _ms[1]);
                    } catch {
                        pgn.Params.Add("Error", "parse");
                        ms.Clear();
                        break;
                    }
                }

                if (ctrlC) break;

                if (ms.Count == 0) {
                    Console.WriteLine("Error: " + pgn.Params["Error"]);
                    continue;
                }

                ms = ms.Take(ms.Count - ((ms.Count + 1) % 2)).ToList();

                pgn.MovesSource = new List<string>() { pretty(ms,color) };
                Console.WriteLine(pgn.MovesSource[0]);
            }

            File.WriteAllLines(path, pgns.Select(x => x.ToString()));
        }

        private static Regex moveRuRe = new Regex("[a-fасе]?[хx]?[a-fасе][1-8]", RegexOptions.Compiled);
        private static string bookPath = "d:/Projects/smalls/book.html";
        private static string moveReS = "[NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?|--";
        //private static Regex moveRe = new Regex(moveReS);
        //private static Regex moveEvalRe = new Regex(moveEvalReS);
        private static string evalLongReS = $"(?<ccm>\\+)−\\+|(?<ccm>\\+)\\+−|\\+−|−\\+|(?<ccm>\\+)|(?<ccm>#)";
        private static string evalLong2ReS = evalLongReS.Replace("?<ccm>", "");
        private static string evalShortReS = "[!?⩱⩲∓±↻=∞⇄↑→N@⨀/]";
        private static string evalReS = $"({evalLongReS})?({evalLong2ReS}|{evalShortReS})*";
        private static string moveNEvalReS = $"(?<move>{moveReS})(?<eval>{evalReS})";
        private static string moveSeq1stReS = $"\\d+\\.{moveNEvalReS}( {moveNEvalReS} \\d+\\.{moveNEvalReS})*( {moveNEvalReS})?";
        private static string moveSeq2ndReS = $"\\d+\\.\\.\\.{moveNEvalReS}( \\d+\\.{moveNEvalReS} {moveNEvalReS})*( \\d+\\.{moveNEvalReS})?";
        private static string moveSeqFullReS = $"{moveSeq1stReS}|{moveSeq2ndReS}";
        private static Regex moveSeqRe = new Regex(moveSeqFullReS, RegexOptions.Compiled);
        private static Regex moveRe = new Regex($"(?<num>\\d+\\.(\\.\\.)?)?{moveNEvalReS}");

        static void Main(string[] args) {
            Console.CancelKeyPress += (o, e) => { ctrlC = true; e.Cancel = true; };

            //Sunfish.SimplePst();
            var pos = SfPosition.FromFen("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w - - 0 2");
            Console.WriteLine(pos);
            Console.ReadLine();

            //solvePuzzles("d:/Konotop4-1.pgn", 3, enginePath: @"d:\Distribs\Sunfish\sunfish.exe"); // @"d:\Distribs\Sunfish\sunfish.exe"
            //handlePgn();

            // processMd("d:/Projects/smalls/ideas-my.md");
            //mdMonitor();

            // fixGoogleTranslate(@"d:/french-classic-b-mbm-g.txt");
            // prepareTranslate(@"d:\Projects\smalls\french-classic-b-mbm.md");
            //splitDeepl("d:/french-b-fs.txt");

            //handleScidHtml("d:/french-classic-mbm.html");
            //handleScidMd(@"d:\Projects\smalls\french-classic-b-mbm.md");
            //Console.ReadLine();
            //pgnSearch();
            //simplifyChessable();
            //handleCbHtml("D:/sicilian-alapin.html");
            //handleCbMd(@"d:\Projects\smalls\panov-mbm.md");

            //removeChessableDublicates(@"d:\Projects\smalls\sicilian-alapin.md");

            /*
            var pgns = Pgn.LoadMany(File.OpenText("d:/Konotop4-1.pgn")).ToArray();
            var i = 0;
            var ss = new List<string>();
            foreach (var pgn in pgns) {
                i++;
                ss.Add(i.ToString());
                var fen = pgn.Params["FEN"];
                ss.Add($"<diagram fen=\"{fen}\" color=\"0\" apply=\"1\"/>");
                if (pgn.Params.ContainsKey("Error")) {
                    ss.Add($"Error: {pgn.Params["Error"]}");
                }
                ss.Add(pgn.MovesSource[0].Replace(". ", "."));
            }

            File.WriteAllLines("d:/konotop4.md", ss.Select(s => s + "\r\n"));
            */
            /*
            var path = "d:/morra-esserman-li.pgn";
            var ss = new List<string>();
            ss.Add("var games = [");
            foreach (var pgn in Pgn.LoadMany(File.OpenText(path))) {
                var moves = pgn.MovesSource.FirstOrDefault() ?? "";
                if (!pgn.MovesSource.First().Contains("{")) continue;

                ss.Add($"{{date:'{pgn.Params["Date"]}',link:'{pgn.Params["Link"]}',moves:'{moves}'}},");
            }
            ss[ss.Count - 1] = ss[ss.Count - 1].Replace("},", "}");
            ss.Add("];");
            path = path.Replace(".pgn", ".js");
            File.WriteAllLines(path, ss);
            */

            /*
            var path = "d:/morra-esserman-li.pgn";
            var ss = File.ReadAllLines(path);
            path = path.Replace(".pgn", "-2.pgn");

            for (var i = 0; i < ss.Length; i++) {
                var s = ss[i];
                if (s.StartsWith("[") || s == "") continue;

                s = s.Replace("{ [%eval ", "{").Replace("] }", "}");
                s = Regex.Replace(s, @" (1-0|0-1|\*|1/2-1/2)$", "");

                s = handleString(s, new Regex(@"\{#-?(\d+)\}"), (x,m) => {
                    var n = int.Parse(m.Groups[1].Value);
                    n = 300 - n;
                    x = Regex.Replace(x, @"\d+", n.ToString()).Replace("#", "");
                    return x;
                });

                ss[i] = s;
            }

            File.WriteAllLines(path,ss);
            */


            /*
            Directory.GetFiles(@"d:\Projects\smalls\", "*.md").ToList().ForEach(path => {
                var s = File.ReadAllText(path);
                s = s.Replace("<addx", "<addz");
                File.WriteAllText(path, s);
            });
            Console.ReadLine();
            */

            /*
            var path = @"d:\sicilian-alapin.pgn";
            using (var readStream = File.OpenRead(path)) {
                var ss = Pgn.LoadMany(readStream).Select(x => Pgn.PrettyMoves(x.Moves)).ToArray();
                File.WriteAllLines(path.Replace(".pgn", ".txt"), ss);
            }
            */

            /*
            var path = @"d:\Projects\smalls\sicilian-alapin.md";
            var ss = File.ReadAllLines(path);
            var re = new Regex(@"^(\*\*|##)");
            ss = ss.Select(s => {
                return re.IsMatch(s) || s == "" ? s : s + "</skip>";
            }).ToArray();
            File.WriteAllLines(path, ss);
            */

            /*
            var path = @"d:\Projects\smalls\french-4-gambits.md";
            var ss = File.ReadAllLines(path);
            //var re = new Regex(@"[CEIGK][a-h]?[1-8]?x?[a-h][1-8]");
            //var re = new Regex(@"([NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?)\.([NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?)");
            //var re = new Regex(@"[^a-h]\d ([NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?)");
            //var re = new Regex(@"\d+ ?\.\.\. ?([NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?)");
            var re = new Regex(@"([NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?)ch");
            //var re = new Regex(@"^\\\d+\) ");
            ss = ss.Select(s => handleString(" " + s, re, (x,m) => {
                // CEIGK
                // NBQRK
                return x.Substring(0, x.Length - 2) + "+"; // x.Remove(2, 1).Insert(2, ".");
            }).Substring(1)).ToArray();
            File.WriteAllLines(path, ss);
            */

            //Console.ReadLine();
            //FEN.Move("4r1k1/3P1pp1/5n1p/2P5/1Q2p3/4NbPq/PP3P1P/R4RK1 b - - 0 25", "Rf8");


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