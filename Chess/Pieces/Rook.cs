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
    }
}