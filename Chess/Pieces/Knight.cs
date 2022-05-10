using System;
using System.Collections.Generic;
using System.Linq;

namespace Chess.Pieces
{
    public class Knight : Piece
    {
        protected override IEnumerable<PieceMove> GetRawMoves()
        {
            return Move(MoveDirection.UpLeft, MoveDirection.UpRight, MoveDirection.DownLeft, MoveDirection.DownRight)
                .Where(i => i.HasValue)
                .Select(i => new PieceMove(this, i.Value));
        }

        private IEnumerable<Square?> Move(params MoveDirection[] directions)
        {
            foreach (var dir in directions)
            {
                if (dir.HasFlag(MoveDirection.Up)) yield return this.Square.Move(dir).Move(MoveDirection.Up);
                if (dir.HasFlag(MoveDirection.Left)) yield return this.Square.Move(dir).Move(MoveDirection.Left);
                if (dir.HasFlag(MoveDirection.Down)) yield return this.Square.Move(dir).Move(MoveDirection.Down);
                if (dir.HasFlag(MoveDirection.Right)) yield return this.Square.Move(dir).Move(MoveDirection.Right);
            }
        }

        public override bool IsAttack(Square square) {
            if (Square == square) {
                throw new Exception();
            }
            var d1 = Math.Abs(square.GetColumn() - Square.GetColumn());
            var d2 = Math.Abs(square.GetRank() - Square.GetRank());

            if (d1 > d2) {
                var dt = d1; d1 = d2; d2 = dt;
            }

            if (d1 != 1 || d2 != 2) {
                return false;
            }

            return true;
        }
    }
}