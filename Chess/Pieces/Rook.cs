using System;
using System.Collections.Generic;

namespace Chess.Pieces
{
    public class Rook : Piece
    {
        protected override IEnumerable<PieceMove> GetRawMoves()
        {
            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.Up))
                yield return new PieceMove(this, square);

            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.Down))
                yield return new PieceMove(this, square);

            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.Left))
                yield return new PieceMove(this, square);

            foreach (var square in MoveUntilObstruction(this.Square, MoveDirection.Right))
                yield return new PieceMove(this, square);
        }

        public override bool IsAttack(Square square) {
            if (Square == square) {
                throw new Exception();
            }
            var dx = square.GetColumn() - Square.GetColumn();
            var dy = square.GetRank() - Square.GetRank();

            if (dx != 0 && dy != 0) {
                return false;
            }

            var sx = Math.Sign(dx);
            var sy = Math.Sign(dy);

            var iSquare = Square.Move(sx,sy);

            while (iSquare != square) {
                if (Board[iSquare] != null) {
                    return false;
                }

                iSquare = iSquare.Move(sx, sy);
            }

            return true;
        }

    }
}