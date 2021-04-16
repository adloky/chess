using Chess.Pieces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Chess
{
    internal class FEN
    {
        #region Properties

        public ReadOnlyCollection<PiecePlacement> Pieces { get; private set; }

        public PlayerColor Turn { get; private set; }

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

        internal class PiecePlacement
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

            sb.Append(' ').Append(char.ToLower(board.CurrentPlayer.PlayerColor.ToString()[0]));

            sb.Append(' ');
            string castle = GetNotation(board.GetCastleAvailabity(PlayerColor.White), PlayerColor.White) + GetNotation(board.GetCastleAvailabity(PlayerColor.Black), PlayerColor.Black);
            if (castle.Length == 0)
                sb.Append('-');
            else
                sb.Append(castle);

            sb.Append(' ');
            if (board.LastMove != null && board.LastMove.Piece is Pawn && Math.Abs(board.LastMove.Source.GetRank() - board.LastMove.Target.GetRank()) == 2)
                sb.Append(board.LastMove.Target.ToString().ToLower());
            else
                sb.Append('-');

            sb.Append(' ')
              .Append(board.History.Reverse().TakeWhile(i => !(i.Piece is Pawn) && i.CapturedPiece == null).Count())
              .Append(' ')
              .Append((int)(board.History.Count / 2));

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
    }
}