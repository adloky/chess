using System.ComponentModel;

namespace Chess
{
    public enum StalemateReason
    {
        [Description("50 moves")]
        FiftyMoves,

        [Description("Repetition")]
        Repetition,

        [Description("No move available")]
        NoMoveAvailable,

        [Description("Insufficient material")]
        InsufficientMaterial
    }
}