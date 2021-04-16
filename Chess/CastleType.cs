using System;

namespace Chess
{
    [Flags]
    public enum CastleType
    {
        None = 0,
        QueenSide = 1,
        KingSide = 2,

        QueenOrKingSide = QueenSide | KingSide,
    }
}