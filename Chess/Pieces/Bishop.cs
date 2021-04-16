using System.Collections.Generic;

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
    }
}