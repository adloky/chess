using System.Collections.Generic;
using System;

namespace Chess.Pieces
{
    public class Bishop : Piece
    {
        protected override IEnumerable<PieceMove> GetRawMoves()
        {
            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.UpLeft))
                yield return new PieceMove(this, square);

            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.UpRight))
                yield return new PieceMove(this, square);

            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.DownLeft))
                yield return new PieceMove(this, square);

            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.DownRight))
                yield return new PieceMove(this, square);
        }
        public override bool IsAttack(Square square) {
            if (Square == square) {
                throw new Exception();
            }
            var dx = square.GetColumn() - Square.GetColumn();
            var dy = square.GetRank() - Square.GetRank();

            if (Math.Abs(dx) != Math.Abs(dy)) {
                return false;
            }

            var sx = Math.Sign(dx);
            var sy = Math.Sign(dy);

            var iSquare = Square.Move(sx,sy).Value;

            while (iSquare != square) {
                if (Board[iSquare] != null) {
                    return false;
                }

                iSquare = iSquare.Move(sx, sy).Value;
            }

            return true;
        }
    }
}