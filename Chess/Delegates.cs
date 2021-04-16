namespace Chess
{
    public delegate void SquareChangedHandler(Square square);

    public delegate void PieceMoved(PieceMove move);

    public delegate void CheckHandler(PlayerColor checkedPlayer);

    public delegate void StalemateHandler(StalemateReason reason);
}