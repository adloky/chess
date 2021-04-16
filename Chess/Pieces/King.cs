using System;
using System.Collections.Generic;

namespace Chess.Pieces
{
    public class King : Piece
    {
        protected override IEnumerable<PieceMove> GetRawMoves()
        {
            foreach (MoveDirection dir in Enum.GetValues(typeof(MoveDirection)))
            {
                var square = this.Square.Move(dir);
                if (square.HasValue)
                    yield return new PieceMove(this, square.Value);
            }

            if (this.CanCastle(CastleType.KingSide))
                yield return new KingCastleMove(this, this.Square.Move(MoveDirection.Right).Move(MoveDirection.Right).Value, CastleType.KingSide);

            if (this.CanCastle(CastleType.QueenSide))
                yield return new KingCastleMove(this, this.Square.Move(MoveDirection.Left).Move(MoveDirection.Left).Value, CastleType.QueenSide);
        }

        protected override bool CanMove(PieceMove move)
        {
            return base.CanMove(move) && !this.Board.IsUnderAttack(move.Target, move.Piece.Player.Opponent());
        }

        private bool CanCastle(CastleType castleType)
        {
            var castle = this.Board.GetCastleAvailabity(this.Player);

            if (!castle.HasFlag(castleType))
                return false;

            if (this.Board.IsInCheck())
                return false;

            var testSquare = castleType.GetRookSquare(this.Player);
            for (int i = 0; i < castleType.GetRookDistance(); i++)
            {
                testSquare = testSquare.Move(castleType.GetRookMoveDirection()).Value;
                if (this.Board[testSquare] != null || this.Board.IsUnderAttack(testSquare, this.Player.Opponent()))
                    return false;
            }

            return true;
        }
    }

    internal class KingCastleMove : PieceMove
    {
        public CastleType Castle { get; private set; }

        public KingCastleMove(Piece piece, Square target, CastleType castle)
            : base(piece, target)
        {
            this.Castle = castle;
        }
    }
}