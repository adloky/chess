using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Chess {
    public class Pgn {
        private readonly static Regex ParamRegex = new Regex(@"^\[(?<name>[^ ]+) ""(?<value>(\\""|""[^\]]|[^""])*)""\] *$", RegexOptions.Compiled);
        private readonly static Regex CommentRegex = new Regex(@" \{[^}]*\}", RegexOptions.Compiled);
        private readonly static Regex NumberRegex = new Regex(@"\d+\.+ ", RegexOptions.Compiled);
        private readonly static Regex ScoreRegex = new Regex(@"[!?]", RegexOptions.Compiled);
        private readonly static Regex SpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private readonly static Regex ResultRegex = new Regex(@" ?(1-0|0-1|1/2-1/2|\*)$", RegexOptions.Compiled);

        public string Fen { get; private set; } = Board.DEFAULT_STARTING_FEN;
        public string Moves { get; private set; } = "";

        public List<string> MovesSource { get; } = new List<string>();
        public string Site { get; private set; }
        public Dictionary<string, string> Params { get; private set; } = new Dictionary<string, string>();


        #region GetMoves

        private enum ParseState {
             Empty,
             Param,
             Moves
         }

        private static string removeVariants(string s) {
            var level = 0;
            var sb = new StringBuilder();
            foreach (var c in s) {
                switch (c) {
                    case '(':
                        level++;
                        break;
                    case ')':
                        level--;
                        break;
                    default:
                        if (level == 0) {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        public static IEnumerable<Pgn> LoadMany(StreamReader reader) {
            var prevState = ParseState.Empty;
            var state = ParseState.Empty;
            var pgn = new Pgn();
            while (!reader.EndOfStream) {
                prevState = state;
                var s = reader.ReadLine();

                state = (s == "") ? ParseState.Empty
                                    : s[0] == '[' && prevState != ParseState.Moves ? ParseState.Param
                                    : ParseState.Moves;

                if (state == ParseState.Param) {
                    var match = ParamRegex.Match(s);
                    if (!match.Success) throw new Exception($"Invalid param: {s}");
                    var name = match.Groups["name"].Value;
                    if (!pgn.Params.ContainsKey(name)) {
                        pgn.Params.Add(match.Groups["name"].Value, match.Groups["value"].Value);
                    }
                }

                if (state == ParseState.Moves) {
                    pgn.MovesSource.Add(s);
                    pgn.Moves = $"{pgn.Moves} {s}";
                }

                if (state == ParseState.Empty && prevState == ParseState.Moves) {
                    pgn.Moves = CommentRegex.Replace(pgn.Moves, "");
                    pgn.Moves = NumberRegex.Replace(pgn.Moves, "");
                    pgn.Moves = ScoreRegex.Replace(pgn.Moves, "");
                    pgn.Moves = removeVariants(pgn.Moves);
                    pgn.Moves = SpaceRegex.Replace(pgn.Moves, " ");
                    pgn.Moves = ResultRegex.Replace(pgn.Moves, "");
                    pgn.Moves = pgn.Moves.Trim();

                    if (pgn.Params.ContainsKey("FEN")) {
                        pgn.Fen = pgn.Params["FEN"];
                    }

                    if (pgn.Params.ContainsKey("Site")) {
                        pgn.Site = pgn.Params["Site"];
                    }

                    yield return pgn;
                    pgn = new Pgn();
                }
            }
        }

        public static IEnumerable<Pgn> LoadMany(Stream stream) {
            using (var reader = new StreamReader(stream)) {
                foreach (var pgn in LoadMany(reader)) {
                    yield return pgn;
                }
            }
        }

        public static Pgn Load(string pgn) {
            using (var memStream = new MemoryStream()) {
                pgn = pgn.Replace("\r\n", "\n");
                var hasMoves = pgn.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Any(x => x[0] != '[');
                if (!hasMoves) {
                    pgn = pgn + "\n\n*";
                }
                var pgnBytes = Encoding.UTF8.GetBytes(pgn + "\n\n");
                memStream.Write(pgnBytes, 0, pgnBytes.Length);
                memStream.Position = 0;
                var pgnResult = LoadMany(memStream).FirstOrDefault();
                return pgnResult;
            }
        }

        #endregion GetMoves

        public override string ToString() {
            var sb = new StringBuilder();
            foreach (var param in Params) {
                sb.Append($"[{param.Key} \"{param.Value}\"]{Environment.NewLine}");
            }
            sb.Append(Environment.NewLine);
            foreach (var s in MovesSource) {
                sb.Append($"{s}{Environment.NewLine}");
            }
            sb.Append(Environment.NewLine);

            return sb.ToString();
        }
    }
}
