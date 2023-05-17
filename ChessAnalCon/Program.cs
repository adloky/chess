using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        static void Main(string[] args) {
            var dic = new SortedDictionary<BString, object>();
            using (var readStream = File.OpenRead("d:/lichess_2023-04.csv"))
            using (var reader = new StreamReader(readStream))
            using (var writeStream = File.Open("d:/lichess_2023-04-sorted.csv", FileMode.Create))
            using (var writer = new StreamWriter(writeStream))
            {
                var count = 0;
                while (!reader.EndOfStream) {
                    var s = reader.ReadLine();
                    var bs = new BString(ShrinkMovesNSalt(s,40));
                    dic.Add(bs,null);
                    count++;
                    if (count % 1000 == 0) {
                        Console.WriteLine(count);
                    }
                }

                foreach (var s in dic.Keys.Select(x => RemoveSalt(x.ToString()))) {
                    writer.WriteLine(s);
                }
            }
            Console.WriteLine("Finish");
            Console.ReadLine();
        }
    }
}
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