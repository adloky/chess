using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Chess;
using Markdig;

namespace ChessMd {

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

    public class MoveInfoList : IList<MoveInfo> {
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
        public void Add(MoveInfo item) { throw new NotImplementedException(); }
        public void Clear() { throw new NotImplementedException(); }
        public bool Contains(MoveInfo item) { throw new NotImplementedException(); }
        public void CopyTo(MoveInfo[] array, int arrayIndex) { throw new NotImplementedException(); }
        public IEnumerator<MoveInfo> GetEnumerator() { throw new NotImplementedException(); }
        public int IndexOf(MoveInfo item) { throw new NotImplementedException(); }
        public void Insert(int index, MoveInfo item) { throw new NotImplementedException(); }
        public bool Remove(MoveInfo item) { throw new NotImplementedException(); }
        public void RemoveAt(int index) { throw new NotImplementedException(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
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
            if (move != "--") {
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

    class Program {
        private static string bookPath = "d:/Projects/smalls/book.html";

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

        private static string moveReS = "[NBRQK]?[a-h]?[1-8]?x?[a-h][1-8](=[NBRQ])?|O-O(-O)?|--";
        private static string evalLongReS = $"(?<ccm>\\+)−\\+|(?<ccm>\\+)\\+−|\\+−|−\\+|(?<ccm>\\+)|(?<ccm>#)";
        private static string evalLong2ReS = evalLongReS.Replace("?<ccm>", "");
        private static string evalShortReS = "[!?⩱⩲∓±↻=∞⇄↑→N@⨀/]";
        private static string evalReS = $"({evalLongReS})?({evalLong2ReS}|{evalShortReS})*";
        private static string moveNEvalReS = $"(?<move>{moveReS})(?<eval>{evalReS})";
        private static string moveSeq1stReS = $"\\d+\\.{moveNEvalReS}( {moveNEvalReS} \\d+\\.{moveNEvalReS})*( {moveNEvalReS})?";
        private static string moveSeq2ndReS = $"\\d+\\.\\.\\.{moveNEvalReS}( \\d+\\.{moveNEvalReS} {moveNEvalReS})*( \\d+\\.{moveNEvalReS})?";
        private static string moveSeqFullReS = $"{moveSeq1stReS}|{moveSeq2ndReS}";
        private static Regex moveRe = new Regex($"(?<num>\\d+\\.(\\.\\.)?)?{moveNEvalReS}");
        private static Regex moveSeqRe = new Regex(moveSeqFullReS, RegexOptions.Compiled);

        private static Regex prevSkipMoveRe = new Regex("\\d+\\.+", RegexOptions.Compiled);

        private static string prevSkipMove(string s) {
            var sn = prevSkipMoveRe.Match(s).Value;
            var n = int.Parse(sn.Replace(".", ""));

            return (sn.Contains("...")) ? $"{n}.--" : $"{n - 1}...--";
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

        private static void compileMd(string path = null) {
            var srcPath = path ?? ChessConfig.current.mdPath;
            var name = Path.GetFileNameWithoutExtension(srcPath);
            var dstPath = Path.Combine(ChessConfig.current.mdDstDir, $"{name}.html");

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
                    .Where(x => x.name == "add" && x.attr.ContainsKey("start"))
                    .Select(x => new Tuple<string, string>(x.attr["start"], x.attr.ContainsKey("value") ? x.attr["value"] : null))
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
                    if (s2.StartsWith("7.e7")) {
                    }

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
                    if (addTuple != null) {
                        if (add != null) {
                            hub.Push(level, add, true);
                        }
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
                       
                        compileMd(e.FullPath);
                        Console.WriteLine(e.FullPath);
                    });
                };
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
            }
        }

        static void Main(string[] args) {
            //compileMd(@"d:\Projects\smalls\caro-kann-shima.md");
            mdMonitor();
        }
    }
}
