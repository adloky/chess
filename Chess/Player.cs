using System;

namespace Chess
{
    public class Player : IDisposable
    {
        public PlayerColor PlayerColor { get; internal set; }

        public Board Board { get; internal set; }

        public event PieceMoved PieceMoved;

        public event Action Turn;

        public virtual void OnPreInit()
        {
        }

        public virtual void OnInitialize()
        {
        }

        public void OnTurn()
        {
            this.OnTurnCore();
            if (this.Turn != null)
                this.Turn();
        }

        protected virtual void OnTurnCore()
        {
        }

        protected virtual void OnDispose()
        {
        }

        void IDisposable.Dispose()
        {
            this.OnDispose();
        }

        internal void OnMove(PieceMove move)
        {
            if (this.PieceMoved != null)
                this.PieceMoved(move);
        }

        public virtual bool IsReady { get { return true; } }
    }
}