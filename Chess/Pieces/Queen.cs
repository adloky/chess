using System;
using System.Collections.Generic;

namespace Chess.Pieces
{
    public class Queen : Piece
    {
        protected override IEnumerable<PieceMove> GetRawMoves()
        {
            foreach (MoveDirection dir in Enum.GetValues(typeof(MoveDirection)))
                foreach (var square in MoveUntilObstruction(this.Square, dir))
                    yield return new PieceMove(this, square);
        }

        public override bool IsAttack(Square square) {
            if (Square == square) {
                throw new Exception();
            }
            var dx = square.GetColumn() - Square.GetColumn();
            var dy = square.GetRank() - Square.GetRank();

            if (Math.Abs(dx) != Math.Abs(dy) && dx != 0 && dy != 0) {
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