using System;
using System.Collections.Generic;
using System.Linq;

namespace Chess.Pieces
{
    public class Pawn : Piece
    {
        protected override IEnumerable<PieceMove> GetRawMoves()
        {
            return this.AllowedMovesCore().Where(i => i != null);
        }

        private IEnumerable<PieceMove> AllowedMovesCore()
        {
            MoveDirection dir = this.Player == PlayerColor.White ? MoveDirection.Up : MoveDirection.Down;

            yield return GetMoveResult(this.Square.Move(dir));
            if (this.IsInFirstPosition())
                yield return GetMoveResult(this.Square.Move(dir).Move(dir));

            yield return GetMoveResult(this.Square.Move(dir | MoveDirection.Left));
            yield return GetMoveResult(this.Square.Move(dir | MoveDirection.Right));
        }

        protected override bool CanMove(PieceMove move)
        {
            if (!base.CanMove(move))
                return false;

            if (IsCaptureMove(move.Target))
                return this.Board[move.Target] != null || this.IsEnPassantCapture(move.Target);

            return this.Board[move.Target] == null;
        }

        private PieceMove GetMoveResult(Square? destination)
        {
            if (!destination.HasValue)
                return null;

            int rank = destination.Value.GetRank();
            if (rank == 1 || rank == 8)
                return new PieceMove(this, destination.Value) { CanCapture = IsCaptureMove(destination.Value), HasPromotion = true };

            if (IsEnPassantCapture(destination.Value))
                return new PieceMove(this, destination.Value, this.Board.LastMove.Piece);

            return new PieceMove(this, destination.Value) { CanCapture = IsCaptureMove(destination.Value) };
        }

        private bool IsCaptureMove(Square target)
        {
            var dir = this.Square.GetDirection(target);
            return (dir.HasFlag(MoveDirection.Left) || dir.HasFlag(MoveDirection.Right));
        }

        private bool IsInFirstPosition()
        {
            int expectedRank = this.Player == PlayerColor.White ? 2 : 7;
            return this.Square.GetRank() == expectedRank;
        }

        private bool IsEnPassantCapture(Square target)
        {
            //Is the last moved piece capturable by "En Passant"?
            //There is a previous move? Was it a pawn? Moved two squares?
            if (this.Board.LastMove == null || !(this.Board.LastMove.Piece is Pawn) || Math.Abs(this.Board.LastMove.Source.GetRank() - this.Board.LastMove.Target.GetRank()) == 1)
                return false;

            MoveDirection dir = this.Player == PlayerColor.White ? MoveDirection.Down : MoveDirection.Up;
            //The "En Passant" candidate is imediatelly ahead of target? Is current move a capture move?
            if (target.Move(dir) == this.Board.LastMove.Target && this.Square.GetColumn() != target.GetColumn())
                return true;

            return false;
        }

        public static MoveDirection GetAttackingDirection(PlayerColor attacker)
        {
            return attacker == PlayerColor.White ? MoveDirection.Down : MoveDirection.Up;
        }
    }
}