using Chess.Pieces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Chess
{
    public class Board : IDisposable
    {
        #region Properties

        public const string DEFAULT_STARTING_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public Dictionary<PlayerColor, Player> Players { get; private set; }

        public Player CurrentPlayer { get { return this.Players[this.Turn]; } }

        public IList<PieceMove> History { get; private set; }

        private Piece[] pieces = new Piece[64];

        public PlayerColor Turn { get; set; }

        public PieceMove LastMove { get { return this.History.LastOrDefault(); } }

        private Dictionary<PlayerColor, CastleType> castleAvailability = new Dictionary<PlayerColor, CastleType>();

        public bool IsActive { get; private set; }

        public string StartFen { get; private set; }

        public IEnumerable<Piece> this[PlayerColor player]
        {
            get
            {
                return this.pieces.Where(i => i != null && i.Player == player);
            }
        }

        public IEnumerable<Piece> this[PlayerColor player, Type pieceType]
        {
            get
            {
                return this.pieces.Where(i => i != null && i.Player == player && i.GetType() == pieceType);
            }
        }

        public Piece this[int rank, int column]
        {
            get
            {
                var square = (Square)Enum.Parse(typeof(Square), ((char)('A' + (column - 1))).ToString() + rank);
                return this[square];
            }
        }

        public Piece this[Square square]
        {
            get { return pieces[(int)square]; }
            private set
            {
                pieces[(int)square] = value;
                if (value == null)
                {
                    this.OnSquareChanged(square);
                    return;
                }

                var source = value.Square;
                if (source != square)
                {
                    pieces[(int)value.Square] = null;
                    value.Square = square;
                }
            }
        }

        public Piece this[Square? square]
        {
            get { return square.HasValue ? pieces[(int)square.Value] : null; }
        }

        #endregion Properties

        #region Events

        public event SquareChangedHandler SquareChanged;

        public event PieceMoved PieceMoved;

        public event CheckHandler Check;

        public event CheckHandler Checkmate;

        public event StalemateHandler Stalemate;

        #endregion Events

        #region Constructor

        public Board(Player whitePlayer, Player blackPlayer, string fenString = DEFAULT_STARTING_FEN)
        {
            this.StartFen = fenString;
            var config = FEN.Parse(fenString);

            this.Turn = config.Turn;
            foreach (var piece in config.Pieces)
                this[piece.Square] = piece.CreatePiece(this);

            this.castleAvailability.Add(PlayerColor.Black, config.BlackCastleAvailability);
            this.castleAvailability.Add(PlayerColor.White, config.WhiteCastleAvailability);

            this.History = new List<PieceMove>();
            whitePlayer.Board = this;
            whitePlayer.PlayerColor = PlayerColor.White;

            blackPlayer.Board = this;
            blackPlayer.PlayerColor = PlayerColor.Black;
            this.Players = new Dictionary<PlayerColor, Player> { { PlayerColor.White, whitePlayer }, { PlayerColor.Black, blackPlayer } };

            this.Players[PlayerColor.White].OnPreInit();
            this.Players[PlayerColor.Black].OnPreInit();
        }

        public void Start()
        {
            this.Players[PlayerColor.White].OnInitialize();
            this.Players[PlayerColor.Black].OnInitialize();

            this.IsActive = true;
            this.Players[PlayerColor.White].OnTurn();
        }

        public void Stop()
        {
            this.IsActive = false;
        }

        #endregion Constructor

        #region Methods

        public bool Move(Square source, Square target, Type promotePawnTo)
        {
            var piece = this[source];
            if (this.Turn != piece.Player || !this.IsActive)
                return false;

            var move = piece.GetValidMove(target);

            if (move == null)
                return false;

            if (!IsValid(move))
                return false;

            MoveCore(move, true);

            this.CurrentPlayer.OnMove(move);
            //The move is valid ready to proceed
            CastleAvailabityChangeTest(move.Piece);
            this.History.Add(move);
            this.Turn = this.Turn.Opponent();

            if (move is KingCastleMove)
                Castle(move as KingCastleMove);

            if (move.HasPromotion)
            {
                move.PawnPromotedTo = promotePawnTo;
                Promote(piece, promotePawnTo);
            }

            if (this.PieceMoved != null)
                this.PieceMoved(move);

            bool hasValidMove = this[this.Turn].SelectMany(p => p.GetValidMoves()).Where(m => IsValid(m)).Any();
            bool isInCheck = this.IsInCheck();

            if (isInCheck && hasValidMove)
                this.OnCheck();

            this.CurrentPlayer.OnTurn();
            if (isInCheck && !hasValidMove)
                this.OnCheckmate();
            else if (!isInCheck && !hasValidMove)
                this.OnStalemate(StalemateReason.NoMoveAvailable);

            return true;
        }

        public Bitmap Draw()
        {
            var img = new Bitmap(850, 850);
            Draw(img);
            return img;
        }
        public Bitmap Draw(int width, int height)
        {
            if (width < 850) throw new ArgumentException("", "width");
            if (height < 850) throw new ArgumentException("", "height");

            var img = new Bitmap(width, height);
            Draw(img);
            return img;
        }
        public void Draw(Image image)
        {
            using (var g = Graphics.FromImage(image))
            {
                g.FillRectangle(Brushes.Black, 0, 0, image.Width, image.Height);

                foreach (var square in Enum.GetValues(typeof(Square)).Cast<Square>())
                {
                    var rect = square.GetRectangle();
                    g.DrawImageUnscaled(Images.GetSquareImage(square, this[square]), rect);

                    if (square.GetColumn() == 1)
                    {
                        var loc = rect.Location;
                        loc.Offset(-15, 40);
                        g.DrawString(square.GetRank().ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.White, loc);
                    }

                    if (square.GetRank() == 1)
                    {
                        var loc = rect.Location;
                        loc.Offset(40, 105);
                        g.DrawString(((char)('a' + (square.GetColumn() - 1))).ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.White, loc);
                    }
                }
            }
        }

        public void Draw(Image image, Square square, bool monochrome = false)
        {
            using (var g = Graphics.FromImage(image))
                Draw(g, square, monochrome);
        }
        public void Draw(Graphics graphics, Square square, bool monochrome = false)
        {
            var rect = square.GetRectangle();
            if (monochrome)
                graphics.DrawImageUnscaled(Grayscale(Images.GetSquareImage(square, this[square])), rect);
            else
                graphics.DrawImageUnscaled(Images.GetSquareImage(square, this[square]), rect);
        }

        public Image Grayscale(Image img)
        {
            Bitmap temp = (Bitmap)img;
            Bitmap bmap = (Bitmap)temp.Clone();
            Color c;
            for (int i = 0; i < bmap.Width; i++)
            {
                for (int j = 0; j < bmap.Height; j++)
                {
                    c = bmap.GetPixel(i, j);
                    byte gray = (byte)(.299 * c.R + .587 * c.G + .114 * c.B);

                    bmap.SetPixel(i, j, Color.FromArgb(gray, gray, gray));
                }
            }
            return (Bitmap)bmap.Clone();
        }

        private void MoveCore(PieceMove move, bool raiseEvent)
        {
            this[move.Target] = this[move.Source];

            if (raiseEvent)
            {
                this.OnSquareChanged(move.Source);
                this.OnSquareChanged(move.Target);
            }
            if (move.HasEnPassantCapture())
            {
                this[move.CapturedPiece.Square] = null;
                if (raiseEvent)
                    this.OnSquareChanged(move.CapturedPiece.Square);
            }
        }

        private bool IsValid(PieceMove move)
        {
            MoveCore(move, false);
            bool ret = !this.IsInCheck();

            //Undo move
            this[move.Source] = this[move.Target];
            if (move.CapturedPiece != null)
                this[move.CapturedPiece.Square] = move.CapturedPiece;

            return ret;
        }

        private void Castle(KingCastleMove move)
        {
            Square rookSquare = move.Castle.GetRookSquare(move.Piece.Player);
            Square target = rookSquare;

            for (int i = 0; i < move.Castle.GetRookDistance(); i++)
                target = target.Move(move.Castle.GetRookMoveDirection()).Value;

            var rook = this[rookSquare];
            this[target] = this[rookSquare];
            this.OnSquareChanged(rookSquare);
            this.OnSquareChanged(target);
        }

        internal void Promote(Piece piece, Type type)
        {
            var newPiece = piece.Promote(type);
            this[piece.Square] = newPiece;
            this.OnSquareChanged(newPiece.Square);
        }

        public CastleType GetCastleAvailabity(PlayerColor player)
        {
            return this.castleAvailability[player];
        }

        private void CastleAvailabityChangeTest(Piece piece)
        {
            if (piece is King || piece is Rook)
            {
                CastleType disabled = CastleType.None;

                if (piece is King)
                    disabled = CastleType.QueenOrKingSide;

                if (piece.Square == Square.A1 || piece.Square == Square.H1 || piece.Square == Square.A8 || piece.Square == Square.H8)
                    disabled = piece.Square.GetColumn() == 1 ? CastleType.QueenSide : CastleType.KingSide;

                this.castleAvailability[piece.Player] = this.castleAvailability[piece.Player] & ~disabled;
            }
        }

        public bool IsInCheck()
        {
            var king = this.King(this.Turn);
            return this.GetAttackers(king.Square, king.Player.Opponent()).Any();
        }

        public IList<Piece> GetAttackers(Square square, PlayerColor attacker)
        {
            var ret = new List<Piece>();

            //Find adjacent king
            if (this[attacker].First(i => i is King).Square.IsAdjacent(square))
                ret.Add(this[attacker].First(i => i is King));

            //Find attacking Pawn
            foreach (var dir in Pawn.GetAttackingDirection(attacker).GetDiagonals())
            {
                var pawn = this[square.Move(dir)] as Pawn;
                if (pawn != null && pawn.Player == attacker)
                    ret.Add(pawn);
            }

            var opponents = this[attacker].Where(i => !(i is King));
            var moves = opponents.Where(i => !(i is King || i is Pawn)).SelectMany(o => o.GetValidMoves().Where(i => i.Target == square && i.CanCapture));

            ret.AddRange(moves.Select(i => i.Piece));
            return ret;
        }

        public bool IsUnderAttack(Square square, PlayerColor attacker)
        {
            return GetAttackers(square, attacker).Any();
        }

        private King King(PlayerColor player)
        {
            return (King)this[player].First(i => i is King);
        }

        #region Events

        private void OnCheck()
        {
            if (this.Check != null)
                this.Check(this.Turn);
        }

        private void OnCheckmate()
        {
            this.IsActive = false;

            if (this.Checkmate != null)
                this.Checkmate(this.Turn);
            this.Turn = (PlayerColor)(-1);
        }

        public void OnStalemate(StalemateReason reason)
        {
            if (!this.IsActive)
                return;

            this.IsActive = false;

            if (this.Stalemate != null)
                this.Stalemate(reason);
            this.Turn = (PlayerColor)(-1);
        }

        private void OnSquareChanged(Square square)
        {
            if (this.SquareChanged != null)
                this.SquareChanged(square);
        }

        #endregion Events

        void IDisposable.Dispose()
        {
            foreach (IDisposable player in this.Players.Values)
                player.Dispose();
        }

        public void Resign()
        {
            this.OnCheckmate();
        }

        public string GetFEN()
        {
            return FEN.FromBoard(this);
        }

        public static Board Load(string fen, Player white = null, Player black = null)
        {
            return new Board(white ?? new Player(), black ?? new Player(), fen);
        }
        #endregion Methods


    }
}