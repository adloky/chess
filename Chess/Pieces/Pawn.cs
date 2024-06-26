﻿using System;
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

            if (IsInFirstPosition() && Math.Abs(move.Source.GetRank() - move.Target.GetRank()) == 2) {
                MoveDirection dir = this.Player == PlayerColor.White ? MoveDirection.Up : MoveDirection.Down;
                if (this.Board[move.Source.Move(dir)] != null) {
                    return false;
                }
            }

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
                return new PieceMove(this, destination.Value, (this.Board.LastMove != null) ? this.Board.LastMove.Piece : this.Board[this.Board.fenEnPassedTarget.Value.ToggleEnPassed()]);

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

            Square lastTarget;

            if (this.Board.LastMove != null) {
                if (!(this.Board.LastMove.Piece is Pawn) || Math.Abs(this.Board.LastMove.Source.GetRank() - this.Board.LastMove.Target.GetRank()) == 1) {
                    return false;
                }
                else lastTarget = this.Board.LastMove.Target;
            }
            else if (this.Board.fenEnPassedTarget != null) {
                lastTarget = this.Board.fenEnPassedTarget.Value.ToggleEnPassed();
            }
            else {
                return false;
            }

            MoveDirection dir = this.Player == PlayerColor.White ? MoveDirection.Down : MoveDirection.Up;
            //The "En Passant" candidate is imediatelly ahead of target? Is current move a capture move?
            if (target.Move(dir) == lastTarget && this.Square.GetColumn() != target.GetColumn())
                return true;

            return false;
        }

        public static MoveDirection GetAttackingDirection(PlayerColor attacker)
        {
            return attacker == PlayerColor.White ? MoveDirection.Down : MoveDirection.Up;
        }

        public override bool IsAttack(Square square) {
            if (Square == square) {
                throw new Exception();
            }

            var dx = Math.Abs(square.GetColumn() - Square.GetColumn());
            var dy = square.GetRank() - Square.GetRank();
            var dc = (this.Player == PlayerColor.White) ? 1 : -1;

            if (dx != 1 || dy != dc) {
                return false;
            }

            return true;
        }
    }
}