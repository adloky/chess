﻿using Chess.Pieces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Chess
{
    public class FEN
    {
        #region Properties

        public ReadOnlyCollection<PiecePlacement> Pieces { get; private set; }

        public PlayerColor Turn { get; private set; }

        public Square? enPassedTarget { get; private set; }

        public int historyLenDiff { get; private set; }

        public int history50LenDiff { get; private set; }

        public CastleType BlackCastleAvailability { get; set; }

        public CastleType WhiteCastleAvailability { get; set; }

        private static Dictionary<char, Type> pieceNotation = new Dictionary<char, Type>
        {
            { 'k', typeof(King) } ,{ 'q', typeof(Queen)} ,{ 'b', typeof(Bishop)} ,{ 'n', typeof(Knight)} ,{ 'r', typeof(Rook)} ,{ 'p', typeof(Pawn)}
        };

        #endregion Properties

        #region Methods

        public static FEN Parse(string fenString)
        {
            var fen = new FEN();

            string[] config = fenString.Split(' ');
            fen.Pieces = new ReadOnlyCollection<PiecePlacement>(GetPieces(config[0]));
            fen.Turn = GetTurn(config[1]);
            fen.SetCastleAvailability(config[2]);
            fen.enPassedTarget = (config[3] == "-") ? (Square?)null : (Square)Enum.Parse(typeof(Square), config[3].ToUpper());
            fen.history50LenDiff = (int.Parse(config[4]));
            fen.historyLenDiff = (int.Parse(config[5]) - 1) * 2 + ((fen.Turn == PlayerColor.White) ? 0 : 1);
            return fen;
        }

        private static IList<PiecePlacement> GetPieces(string placement)
        {
            var pieces = new List<PiecePlacement>();

            int squareIdx = 0;
            foreach (var rank in placement.Split('/').Reverse())
            {
                if (squareIdx % 8 != 0)
                    throw new InvalidOperationException("Unexpected FEN format");

                foreach (char c in rank)
                {
                    if (c >= '1' && c <= '8')
                        squareIdx += ((int)c - (int)'1' + 1);
                    else
                        pieces.Add(new PiecePlacement { PieceType = pieceNotation[char.ToLower(c)], Player = char.IsLower(c) ? PlayerColor.Black : PlayerColor.White, Square = (Square)squareIdx++ });
                }
            }

            return pieces;
        }

        private static PlayerColor GetTurn(string turn)
        {
            if (turn == "w") return PlayerColor.White;
            if (turn == "b") return PlayerColor.Black;

            throw new InvalidOperationException();
        }

        private void SetCastleAvailability(string text)
        {
            CastleType black = (CastleType)0;
            CastleType white = (CastleType)0;
            foreach (char c in text)
            {
                switch (c)
                {
                    case 'K': white = white | CastleType.KingSide; break;
                    case 'Q': white = white | CastleType.QueenSide; break;
                    case 'k': black = black | CastleType.KingSide; break;
                    case 'q': black = black | CastleType.QueenSide; break;
                }
            }
            this.BlackCastleAvailability = black;
            this.WhiteCastleAvailability = white;
        }

        #endregion Methods

        public class PiecePlacement
        {
            public Type PieceType { get; set; }

            public PlayerColor Player { get; set; }

            public Square Square { get; set; }

            public Piece CreatePiece(Board board)
            {
                var piece = (Piece)Activator.CreateInstance(this.PieceType);
                piece.SetPlacement(board, this);
                return piece;
            }
        }

        internal static string FromBoard(Board board)
        {
            StringBuilder sb = new StringBuilder();

            int emptyCount = 0;
            for (int rank = 8; rank >= 1; rank--)
            {
                for (int column = 1; column <= 8; column++)
                {
                    var piece = board[rank, column];
                    if (piece == null)
                    {
                        emptyCount++;
                        continue;
                    }

                    if (emptyCount != 0)
                        sb.Append(emptyCount);
                    sb.Append(GetNotation(piece));
                    emptyCount = 0;
                }

                if (emptyCount != 0) sb.Append(emptyCount);
                emptyCount = 0;

                if (rank > 1)
                    sb.Append('/');
            }

            sb.Append(' ').Append(board.Turn == PlayerColor.White ? "w" : "b");

            sb.Append(' ');
            string castle = GetNotation(board.GetCastleAvailabity(PlayerColor.White), PlayerColor.White) + GetNotation(board.GetCastleAvailabity(PlayerColor.Black), PlayerColor.Black);
            if (castle.Length == 0)
                sb.Append('-');
            else
                sb.Append(castle);

            sb.Append(' ');
            if (board.LastMove != null && board.LastMove.Piece is Pawn && Math.Abs(board.LastMove.Source.GetRank() - board.LastMove.Target.GetRank()) == 2)
                sb.Append(board.LastMove.Target.ToggleEnPassed().ToString().ToLower());
            else if (board.LastMove == null && board.fenEnPassedTarget != null)
                sb.Append(board.fenEnPassedTarget.Value.ToString().ToLower());
            else
                sb.Append('-');

            var history50Len = board.History.Reverse().TakeWhile(i => !(i.Piece is Pawn) && i.CapturedPiece == null).Count();
            if (board.History.Count == history50Len) {
                history50Len += board.fenHistory50LenDiff;
            }

            sb.Append(' ')
              .Append(history50Len)
              .Append(' ')
              .Append((int)((board.History.Count + board.fenHistoryLenDiff) / 2 + 1));

            return sb.ToString();
        }

        private static string GetNotation(CastleType castleType, PlayerColor color)
        {
            string notation = "";
            switch (castleType)
            {
                case CastleType.KingSide: notation = "k"; break;
                case CastleType.QueenSide: notation = "q"; break;
                case CastleType.QueenOrKingSide: notation = "kq"; break;
            }

            return color == PlayerColor.White ? notation.ToUpper() : notation.ToLower();
        }

        private static char GetNotation(Piece piece)
        {
            char notation = piece is Knight ? 'N' : piece.GetType().Name[0];
            return piece.Player == PlayerColor.White ? char.ToUpper(notation) : char.ToLower(notation);
        }
        public static string Move(string fen, string move) {
            if (move == "--") {
                var fs = fen.Split(' ');
                if (fs[1] == "w") {
                    fs[1] = "b";
                }
                else {
                    fs[5] = (int.Parse(fs[5]) + 1).ToString();
                    fs[1] = "w";
                }
                fs[3] = "-";

                return string.Join(" ", fs);
            }

            var board = Board.Load(fen);
            var opt = new MoveOptions() { SkipTestMate = true };
            if (!board.Move(move, opt)) {
                throw new Exception($"Invalid move (FEN: {fen}; Move: {move})");
            };

            return board.GetFEN();
        }

        public static string Correct(string fen) {
            var board = Board.Load(fen);
            return board.GetFEN();
        }

        public static int? GetMateState(string fen) {
            var board = Board.Load(fen);
            return board.GetMateState();
        }

        public static IEnumerable<string> San2Uci(string fen, IEnumerable<string> sans) {
            var board = Board.Load(fen);
            foreach (var san in sans) {
                yield return board.ParseSanMove(san).ToUciString();
                board.Move(san);
            }
        }

        public static IEnumerable<string> Uci2San(string fen, IEnumerable<string> ucis) {
            var board = Board.Load(fen);
            foreach (var uci in ucis) {
                yield return board.Uci2San(uci);
                board.Move(uci);
            }
        }

        public static string San2Uci(string fen, string sans) {
            if (sans == null) {
                return null;
            }
            var board = Board.Load(fen);
            var sanSplit = sans.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var uciList = new List<string>();
            foreach (var san in sanSplit) {
                uciList.Add(board.ParseSanMove(san).ToUciString());
                board.Move(san);
            }

            return string.Join(" ", uciList);
        }

        public static string Uci2San(string fen, string ucis) {
            if (ucis == null) {
                return null;
            }
            var board = Board.Load(fen);
            var uciSplit = ucis.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sanList = new List<string>();
            foreach (var uci in uciSplit) {
                sanList.Add(board.Uci2San(uci));
                board.Move(uci);
            }

            return string.Join(" ", sanList);
        }

        public static string StrictEnPassed(string fen) {
            var board = Board.Load(fen);
            var fenSplit = fen.Split(' ');
            var targetStr = fenSplit[3].ToUpper();
            if (targetStr == "-") {
                return fen;
            }

            var target = (Square)Enum.Parse(typeof(Square), targetStr);
            var dy = target.GetRank() == 3 ? 1 : -1;
            var hasMove = (new int[] { 1, -1 })
                .Select(dx => target.Move(dx, dy)).Where(s => s != null)
                .Select(s => board[s.Value]).Where(p => p != null && p is Pawn && p.Player == board.Turn)
                .Select(p => p.GetValidMove(target)).Any(m => m != null && board.IsValid(m));

            if (!hasMove) {
                fenSplit[3] = "-";
            }

            return string.Join(" ", fenSplit);
        }

        public static bool Like(string fen, string pat) {
            var board = Board.Load(fen);
            var patBoard = Board.Load(pat);
            var patSquares = Enumerable.Range(0, 64).Select(x => (Square)x).Where(x => patBoard[x] != null).ToArray();
            return !patSquares.Where(s => board[s] == null || board[s].GetType() != patBoard[s].GetType() || board[s].Player != patBoard[s].Player).Any();
        }

        public static string Basic(string fen) {
            var fs = fen.Split(' ');
            fs[4] = "0";
            fs[5] = "1";
            return string.Join(" ", fs);
        }
    }
}