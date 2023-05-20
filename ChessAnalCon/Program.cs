﻿using System;
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
using Newtonsoft.Json;

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

        private static volatile bool ctrlC = false;

        static void Main(string[] args) {
            Console.CancelKeyPress += (o, e) => { ctrlC = true; e.Cancel = true; };
            using (var readStream = File.OpenRead("d:/lichess_2023-04-sorted.csv"))
            using (var reader = new StreamReader(readStream))
            //using (var writeStream = File.Open("d:/lichess_2023-04-sorted.csv", FileMode.Create))
            //using (var writer = new StreamWriter(writeStream))
            {
                var storage = new StatStorage();
                storage.Load(reader, "d:/lichess-pos.txt", "d:/lichess.txt", "d:/lichess-loc.txt");

                var count = 0;
                while (!reader.EndOfStream && !ctrlC) {
                    var s = reader.ReadLine();
                    var board = Board.Load();

                    var split = s.Split(',');
                    var moves = split[0];
                    var isBlitz = split[1] == "blitz";
                    var elos = split[2].Split(' ').Select(x => int.Parse(x)).ToArray();
                    var maxElo = Math.Max(elos[0], elos[1]);
                    var isMid = isBlitz ? maxElo <= 2000 : maxElo <= 2150;
                    var midCount = isMid ? 1 : 0;
                    var bytes = Encoding.ASCII.GetBytes(moves);

                    var pis = new List<PosInfo>();
                    pis.Add(new PosInfo() { Fen = Board.DEFAULT_STARTING_FEN, Last = null, Hash = BytesToGuid(bytes, 0) });
                    foreach (var m in EnumMoves(moves)) {
                        var move = moves.Substring(m.Item1, m.Item2);
                        if (!board.Move(move)) {
                            throw new Exception("Bad move");
                        }

                        pis.Add(new PosInfo() { Fen = board.GetFEN(), Last = move, Hash = BytesToGuid(bytes, m.Item1 + m.Item2) });
                    }

                    storage.Handle(pis, 5, midCount);

                    count++;
                    if (count % 1000 == 0) {
                        Console.WriteLine($"{reader.GetVirtualPosition() / 1024 / 1024}/{storage.Count}");
                    }
                }

                Console.WriteLine("Save? (y/n)");
                if (Console.ReadLine() == "y") {
                    storage.Save(reader);
                }
            }

//            Console.ReadLine();
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