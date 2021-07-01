using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chess {
    public class Pgn {

        #region GetMoves

        private enum ParseState {
             Empty,
             Param,
             Moves
         }

        private class Game
        {
             public readonly static Regex ParamRegex = new Regex(@"^\[([^ ]+) ""([^""]*)""\]$", RegexOptions.Compiled);
             public readonly static Regex CommentRegex = new Regex(@" \{[^}]*\}", RegexOptions.Compiled);
             public readonly static Regex NumberRegex = new Regex(@"\d+\.+ ", RegexOptions.Compiled);
             public readonly static Regex ScoreRegex = new Regex(@"[!?]", RegexOptions.Compiled);
             public readonly static Regex SpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
            public readonly static Regex ResultRegex = new Regex(@" ?(1-0|0-1|1/2-1/2|\*)$", RegexOptions.Compiled);
 
             public Dictionary<string, string> Params { get; private set; } = new Dictionary<string, string>();
 
             public string Moves { get; set; } = "";
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

        public static string GetMoves(string pgn) {
            var result = "";
            using (var memStream = new MemoryStream()) {
                var pgnBytes = Encoding.UTF8.GetBytes(pgn + "\n\n");
                memStream.Write(pgnBytes, 0, pgnBytes.Length);
                memStream.Position = 0;

                using (var reader = new StreamReader(memStream)) {
                    var prevState = ParseState.Empty;
                    var state = ParseState.Empty;
                    var game = new Game();
                    while (!reader.EndOfStream) {
                        prevState = state;
                        var s = reader.ReadLine();

                        state = (s == "") ? ParseState.Empty
                                          : (s[0] == '[') ? ParseState.Param
                                          : ParseState.Moves;

                        Match match = null;

                        if (state == ParseState.Param) {
                            match = Game.ParamRegex.Match(s);
                            if (!match.Success) throw new Exception($"Invalid param: {s}");
                            game.Params.Add(match.Groups[1].Value, match.Groups[2].Value);
                        }

                        if (state == ParseState.Moves) {
                            game.Moves = $"{game.Moves} {s}";
                        }

                        if (state == ParseState.Empty && prevState == ParseState.Moves) {
                            game.Moves = Game.CommentRegex.Replace(game.Moves, "");
                            game.Moves = Game.NumberRegex.Replace(game.Moves, "");
                            game.Moves = Game.ScoreRegex.Replace(game.Moves, "");
                            game.Moves = removeVariants(game.Moves);
                            game.Moves = Game.SpaceRegex.Replace(game.Moves, " ");
                            game.Moves = Game.ResultRegex.Replace(game.Moves, "");
                            game.Moves = game.Moves.Trim();

                            result = game.Moves;
                        }
                    }
                }
            }
            return result;
        }

        #endregion GetMoves
    }
}
