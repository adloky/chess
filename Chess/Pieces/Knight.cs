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
    }
}