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
    }
}