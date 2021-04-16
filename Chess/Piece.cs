using Chess.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chess
{
    public abstract class Piece
    {
        public Board Board { get; private set; }

        public PlayerColor Player { get; private set; }

        public Square Square { get; internal set; }

        protected Piece()
        {
        }

        public Piece(PlayerColor player, Square square)
        {
            this.Player = player;
            this.Square = square;
        }

        public PieceMove GetValidMove(Square target)
        {
            return this.GetValidMoves().FirstOrDefault(i => i.Target == target);
        }

        public IEnumerable<PieceMove> GetValidMoves()
        {
            return this.GetRawMoves().Where(CanMove);
        }

        protected abstract IEnumerable<PieceMove> GetRawMoves();

        protected virtual bool CanMove(PieceMove move)
        {
            var capturedPiece = move.CapturedPiece ?? this.Board[move.Target];
            return capturedPiece == null || (capturedPiece.Player != move.Piece.Player && move.CanCapture);
        }

        protected IEnumerable<Square> MoveUntilObstruction(Square from, MoveDirection dir)
        {
            var target = from.Move(dir);
            bool last = false;

            var player = this.Board[from].Player;

            while (target.HasValue && (this.Board[target.Value] == null || this.Board[target.Value].Player != player) && !last)
            {
                yield return target.Value;
                last = this.Board[target.Value] != null;
                target = target.Move(dir);
            }
        }

        internal Piece Promote(Type type)
        {
            var ret = (Piece)Activator.CreateInstance(type);
            ret.Square = this.Square;
            ret.Player = this.Player;
            ret.Board = this.Board;
            return ret;
        }

        #region Aux

        public static Type GetPieceType(string notation)
        {
            switch (char.ToLower(notation[0]))
            {
                case 'q': return typeof(Queen);
                case 'r': return typeof(Rook);
                case 'b': return typeof(Bishop);
                case 'n': return typeof(Knight);
                case 'k': return typeof(King);
                case 'p': return typeof(Pawn);
            }
            throw new InvalidOperationException("Notation not supported: " + notation);
        }

        public static string GetNotation(Type pieceType)
        {
            switch (pieceType.Name)
            {
                case "Queen": return "q";
                case "Rook": return "r";
                case "Bishop": return "b";
                case "Knight": return "n";
                case "King": return "k";
                case "Pawn": return "p";
            }
            throw new InvalidOperationException("Piece not supported: " + pieceType.Name);
        }

        public override string ToString()
        {
            return string.Format("{0}{1}({2})", this.Player, this.GetType().Name, this.Square);
        }

        internal void SetPlacement(Board board, FEN.PiecePlacement piecePlacement)
        {
            this.Player = piecePlacement.Player;
            this.Square = piecePlacement.Square;
            this.Board = board;
        }

        #endregion Aux
    }
}