﻿using Chess.Pieces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chess
{
    public class Board : IDisposable
    {
        #region Properties

        public const string DEFAULT_STARTING_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public int fenHistoryLenDiff { get; private set; }

        public int fenHistory50LenDiff { get; private set; }

        public Square? fenEnPassedTarget { get; set; }

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
            this.fenHistoryLenDiff = config.historyLenDiff;
            this.fenHistory50LenDiff = config.history50LenDiff;
            this.Turn = config.Turn;
            this.fenEnPassedTarget = config.enPassedTarget;
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

            this.CorrectCastleAvailability();
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

        public Square CorrectCastleTarget(Square source, Square target) {
            var piece = this[source];

            if (piece is King) {
                if (source == Square.E1) {
                    if (target == Square.H1) {
                        target = Square.G1;
                    }
                    else if ((target == Square.A1)) {
                        target = Square.C1;
                    }
                }
                else if (source == Square.E8) {
                    if (target == Square.H8) {
                        target = Square.G8;
                    }
                    else if ((target == Square.A8)) {
                        target = Square.C8;
                    }
                }
            }

            return target;
        }

        public bool Move(Square source, Square target, Type promotePawnTo)
        {
            var piece = this[source];
            if (this.Turn != piece.Player || !this.IsActive)
                return false;

            target = CorrectCastleTarget(source, target);

            var move = piece.GetValidMove(target);

            if (move == null)
                return false;

            if (!IsValid(move))
                return false;

            CastleAvailabityChangeTest(move.Piece);
            MoveCore(move, true);

            this.CurrentPlayer.OnMove(move);
            //The move is valid ready to proceed
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

            CorrectCastleAvailability();

            return true;
        }

        public int? GetMateState() {
            bool hasValidMove = this[this.Turn].SelectMany(p => p.GetValidMoves()).Where(m => IsValid(m)).Any();
            bool isInCheck = this.IsInCheck();

            if (isInCheck && !hasValidMove)
                return (1 - ((int)this.Turn - 1) * 2) * (-1);
            else if (!isInCheck && !hasValidMove)
                return 0;

            return null;
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
            if (!(piece is King || piece is Rook)) return;

            CastleType disabled = CastleType.None;

            if (piece is King) {
                disabled = CastleType.QueenOrKingSide;
            } else if (piece.Player == PlayerColor.White && (piece.Square == Square.A1 || piece.Square == Square.H1)
             || piece.Player == PlayerColor.Black && (piece.Square == Square.A8 || piece.Square == Square.H8))
            {
                disabled = piece.Square.GetColumn() == 1 ? CastleType.QueenSide : CastleType.KingSide;
            }

            this.castleAvailability[piece.Player] = this.castleAvailability[piece.Player] & ~disabled;
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
            this.Turn = (PlayerColor)(-(int)this.Turn);
        }

        public void OnStalemate(StalemateReason reason)
        {
            if (!this.IsActive)
                return;

            this.IsActive = false;

            if (this.Stalemate != null)
                this.Stalemate(reason);
            this.Turn = (PlayerColor)(-(int)this.Turn);
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

        public void CorrectCastleAvailability() {
            if (this[Square.E1] == null || !(this[Square.E1] is King) || this[Square.E1].Player != PlayerColor.White) {
                this.castleAvailability[PlayerColor.White] &= ~CastleType.QueenOrKingSide;
            }

            if (this[Square.E8] == null || !(this[Square.E8] is King) || this[Square.E8].Player != PlayerColor.Black) {
                this.castleAvailability[PlayerColor.Black] &= ~CastleType.QueenOrKingSide;
            }

            if (this[Square.A1] == null || !(this[Square.A1] is Rook) || this[Square.A1].Player != PlayerColor.White) {
                this.castleAvailability[PlayerColor.White] &= ~CastleType.QueenSide;
            }

            if (this[Square.H1] == null || !(this[Square.H1] is Rook) || this[Square.H1].Player != PlayerColor.White) {
                this.castleAvailability[PlayerColor.White] &= ~CastleType.KingSide;
            }

            if (this[Square.A8] == null || !(this[Square.A8] is Rook) || this[Square.A8].Player != PlayerColor.Black) {
                this.castleAvailability[PlayerColor.Black] &= ~CastleType.QueenSide;
            }

            if (this[Square.H8] == null || !(this[Square.H8] is Rook) || this[Square.H8].Player != PlayerColor.Black) {
                this.castleAvailability[PlayerColor.Black] &= ~CastleType.KingSide;
            }
        }

        #region ParseSanMove

        private static readonly Regex SanTailRegex = new Regex("[+#]?[?!]{0,2}$", RegexOptions.Compiled);

        private static readonly Regex SanPromotionRegex = new Regex("=?([NBRQ])$", RegexOptions.Compiled);

        private static readonly Regex SanCastlingRegex = new Regex("^O-O(-O)?$", RegexOptions.Compiled);

        private static readonly Regex SanMainRegex = new Regex("^(?<piece>[NBRQK])?(?<srcCol>[a-h])?(?<srcRow>[1-8])?(?<capture>x)?(?<target>[a-h][1-8])$", RegexOptions.Compiled);

        public PieceMove ParseSanMove(string san) {
            san = SanTailRegex.Replace(san, "");

            Square target;
            Piece piece;

            var match = SanCastlingRegex.Match(san);
            if (match.Success) {
                var source = (Turn == PlayerColor.White) ? Square.E1 : Square.E8;
                target = (match.Length == 3)
                    ? source.Move(MoveDirection.Right).Move(MoveDirection.Right).Value
                    : source.Move(MoveDirection.Left).Move(MoveDirection.Left).Value;

                piece = this[source];
                if (!(piece is King)) return null;

                var move = piece.GetValidMove(target);

                return move;
            }

            Type promotion = null;
            match = SanPromotionRegex.Match(san);
            if (match.Success) {
                promotion = Piece.GetPieceType($"{san.Last()}");
                san = san.Substring(0, san.Length - match.Length);
            }

            #region main

            match = SanMainRegex.Match(san);
            if (!match.Success) return null;

            var pieceG = match.Groups["piece"].Value;
            var srcColG = match.Groups["srcCol"].Value;
            var srcRowG = match.Groups["srcRow"].Value;
            var captureG = match.Groups["capture"].Value;
            var targetG = match.Groups["target"].Value;

            target = (Square)Enum.Parse(typeof(Square), targetG.ToUpper());
            var pieceType = (pieceG == "") ? typeof(Pawn) : Piece.GetPieceType(pieceG);

            var pieces = this.pieces.Where(x => x != null && x.GetType() == pieceType && x.Player == Turn).ToArray();

            if (pieceType == typeof(Pawn) && captureG == "") {
                pieces = pieces.Where(x => x.Square.GetColumn() == target.GetColumn()).ToArray();
            }

            if (srcColG != "") {
                var col = srcColG[0] - 'a' + 1;
                pieces = pieces.Where(x => x.Square.GetColumn() == col).ToArray();
            }

            if (srcRowG != "") {
                var row = srcRowG[0] - '1' + 1;
                pieces = pieces.Where(x => x.Square.GetRank() == row).ToArray();
            }

            var moves = pieces.Select(x => x.GetValidMove(target)).Where(x => x != null).ToArray();

            if (moves.Length > 1) {
                moves = moves.Where(x => IsValid(x)).ToArray();
            }

            if (moves.Length != 1) {
                return null;
            }

            moves[0].PawnPromotedTo = promotion;

            return moves[0];

            #endregion main
        }

        #endregion ParseSanMove

        private static readonly Regex UciRegex = new Regex("^([a-h][1-8])([a-h][1-8])([nbrq])?$", RegexOptions.Compiled);

        public PieceMove ParseUciMove(string uci) {
            var match = UciRegex.Match(uci);

            if (!match.Success) { return null; }

            var piece = this[(Square)Enum.Parse(typeof(Square), match.Groups[1].Value.ToUpper())];
            var move = piece.GetValidMove((Square)Enum.Parse(typeof(Square), match.Groups[2].Value.ToUpper()));

            if (match.Groups[3].Value != "") {
                move.PawnPromotedTo = Piece.GetPieceType(match.Groups[3].Value);
            }

            return move;
        }

        public bool Move(string s) {
            var move = ParseUciMove(s);
            if (move == null) {
                move = ParseSanMove(s);
                if (move == null) return false;
            }

            return Move(move.Source, move.Target, move.PawnPromotedTo);
        }

        public string Uci2San(string uci) {
            var move = ParseUciMove(uci);

            move.Target = CorrectCastleTarget(move.Source, move.Target);

            var san = (string)null;

            if (move.Piece is Pawn) {
                san = (move.CapturedPiece == null)
                    ? move.Target.ToString().ToLower()
                    : (char)('a' + move.Source.GetColumn() - 1) + "x" + move.Target.ToString().ToLower();

                if (move.PawnPromotedTo != null) {
                    san += "=" + Piece.GetNotation(move.PawnPromotedTo).ToUpper();
                }
            }
            else if (move is KingCastleMove) {
                san = (((KingCastleMove)move).Castle == CastleType.KingSide) ? "O-O" : "O-O-O";
            }
            else {
                san = Piece.GetNotation(move.Piece.GetType()).ToUpper();
                var otherMoves = pieces.Where(x => x != null && x != move.Piece && x.GetType() == move.Piece.GetType() && x.Player == move.Piece.Player)
                    .Select(x => x.GetValidMove(move.Target)).Where(x => x != null).ToArray();

                if (otherMoves.Length > 0) {
                    otherMoves = otherMoves.Where(x => IsValid(x)).ToArray();
                }

                if (otherMoves.Length > 0) {
                    if (!otherMoves.Where(x => x.Source.GetColumn() == move.Source.GetColumn()).Any()) {
                        san += (char)('a' + move.Source.GetColumn() - 1);
                    }
                    else if (!otherMoves.Where(x => x.Source.GetRank() == move.Source.GetRank()).Any()) {
                        san += (char)('1' + move.Source.GetRank() - 1);
                    }
                    else {
                        san += move.Source.ToString().ToLower();
                    }
                }

                if (move.CapturedPiece != null) { san += "x"; }

                san += move.Target.ToString().ToLower();
            }

            #region check and checkmate

            MoveCore(move, false);

            var castleRookSource = (Square)0;
            var castleRookTarget = (Square)0;
            if (move is KingCastleMove) {
                castleRookSource = (((KingCastleMove)move).Castle == CastleType.KingSide) ? move.Target.Move(MoveDirection.Right).Value : move.Target.Move(MoveDirection.Left).Move(MoveDirection.Left).Value;
                castleRookTarget = (((KingCastleMove)move).Castle == CastleType.KingSide) ? move.Target.Move(MoveDirection.Left).Value : move.Target.Move(MoveDirection.Right).Value;
                this[castleRookTarget] = this[castleRookSource];
            }

            var king = King(this.Turn.Opponent());
            var isInCheck = GetAttackers(king.Square, king.Player.Opponent()).Any();

            if (isInCheck) {
                var validMoves = this[this.Turn.Opponent()].SelectMany(p => p.GetValidMoves()).Where(m => IsValid(m)).ToArray();

                // fix bug
                this[king.Square] = null;
                validMoves = validMoves.Where(x => !(x.Piece is King) || !GetAttackers(x.Target, king.Player.Opponent()).Any()).ToArray();
                this[king.Square] = king;

                var hasValidMove = validMoves.Any();
                san += (hasValidMove) ? "+" : "#";
            }

            // undo move
            this[move.Source] = this[move.Target];
            if (move.CapturedPiece != null) {
                this[move.CapturedPiece.Square] = move.CapturedPiece;
            }
            if (move is KingCastleMove) {
                this[castleRookSource] = this[castleRookTarget];
            }

            #endregion

            return san;
        }

        #endregion Methods
    }
}