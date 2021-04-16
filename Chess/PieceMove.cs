using System;

namespace Chess
{
    public class PieceMove
    {
        public Piece Piece { get; set; }

        public Piece CapturedPiece { get; set; }

        public bool CanCapture { get; internal set; }

        public bool HasPromotion { get; internal set; }

        public Type PawnPromotedTo { get; internal set; }

        public Square Source { get; private set; }

        public Square Target { get; private set; }

        protected Board Board
        {
            get
            {
                return this.Piece.Board;
            }
        }

        public PieceMove(Square source, Square target, Type promotePawnTo)
        {
            this.Source = source;
            this.Target = target;
            this.PawnPromotedTo = promotePawnTo;
        }

        public PieceMove(Piece piece, Square target, Piece capturedPiece = null)
        {
            this.Piece = piece;
            this.Source = piece.Square;
            this.Target = target;
            this.CanCapture = true;
            this.CapturedPiece = capturedPiece ?? this.Board[target];
        }

        protected PieceMove()
        {
        }

        public MoveDirection GetDirection()
        {
            return this.Source.GetDirection(this.Target);
        }

        internal bool HasEnPassantCapture()
        {
            return this.CapturedPiece != null && this.CapturedPiece.Square != this.Target;
        }
    }
}