using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChessAnalCon {

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


    public enum ParseState {
        Empty,
        Param,
        Moves
    }

    public enum GameType {
        None,
        Blitz,
        Rapid
    }


    public class Game {
        public readonly static Regex ParamRegex = new Regex(@"^\[([^ ]+) ""([^""]*)""\]$", RegexOptions.Compiled);
        public readonly static Regex CommentRegex = new Regex(@" \{[^}]*\}", RegexOptions.Compiled);
        public readonly static Regex NumberRegex = new Regex(@"\d+\.+ ", RegexOptions.Compiled);
        public readonly static Regex ScoreRegex = new Regex(@"[!?]", RegexOptions.Compiled);
        public readonly static Regex SpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        public readonly static Regex ResultRegex = new Regex(@" ?(1-0|0-1|1/2-1/2|\*)$", RegexOptions.Compiled);

        public Dictionary<string, string> Params { get; private set; } = new Dictionary<string, string>();

        public string Moves { get; set; } = "";
    }


    class Program {
        static void Main(string[] args) {
            using (var readStream = File.OpenRead("e:/lichess_db_standard_rated_2023-04.pgn"))
            using (var reader = new StreamReader(readStream))
            using (var writeStream = File.Open("e:/lichess_2023-04.csv", FileMode.Create))
            using (var writer = new StreamWriter(writeStream))
            {
                var prevState = ParseState.Empty;
                var state = ParseState.Empty;
                var game = new Game();
                var count = 0;
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
                        game.Moves = $"{game.Moves}{(game.Moves == "" ? "" : " ")}{s}";
                    }

                    if (state == ParseState.Empty && prevState == ParseState.Moves) {
                        game.Moves = Game.CommentRegex.Replace(game.Moves, "");
                        game.Moves = Game.NumberRegex.Replace(game.Moves, "");
                        game.Moves = Game.ScoreRegex.Replace(game.Moves, "");
                        game.Moves = Game.SpaceRegex.Replace(game.Moves, " ");
                        game.Moves = Game.ResultRegex.Replace(game.Moves, "");
                        game.Moves = game.Moves.Trim();

                        // handle

                        var moveCount = game.Moves.Count(x => x == ' ') + 1;
                        var evnt = game.Params["Event"];
                        var type = evnt.Contains("Blitz") ? GameType.Blitz
                            : evnt.Contains("Rapid") ? GameType.Rapid
                            : GameType.None;

                        var typeMinElo = (type == GameType.Blitz) ? 1700
                            : (type == GameType.Rapid) ? 1850
                            : int.MaxValue;

                        var result = game.Params["Result"];
                        var whiteElo = int.Parse(game.Params["WhiteElo"]);
                        var blackElo = int.Parse(game.Params["BlackElo"]);

                        var minElo = Math.Min(whiteElo, blackElo);

                        if (minElo >= typeMinElo && moveCount >= 10) {
                            writer.WriteLine($"{game.Moves},{type.ToString().ToLower()},{whiteElo} {blackElo},{result}");
                            count++;
                            if (count % 1000 == 0) {
                                Console.WriteLine(reader.GetVirtualPosition() / 1024 / 1024);
                            }
                        }

                        game = new Game();
                    }
                }
                Console.WriteLine($"games: {count}");
            }

            Console.ReadLine();
        }
    }
}
