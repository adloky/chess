using Chess;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace System
{
    public static class ExtensionMethods
    {
        #region CastleType

        public static Square GetRookSquare(this CastleType castleType, PlayerColor player)
        {
            if (player == PlayerColor.White)
                return castleType == CastleType.KingSide ? Square.H1 : Square.A1;
            else
                return castleType == CastleType.KingSide ? Square.H8 : Square.A8;
        }

        public static int GetRookDistance(this CastleType castleType)
        {
            return castleType == CastleType.KingSide ? 2 : 3;
        }

        public static MoveDirection GetRookMoveDirection(this CastleType castleType)
        {
            return castleType == CastleType.KingSide ? MoveDirection.Left : MoveDirection.Right;
        }

        #endregion CastleType

        #region Enum

        public static T GetAttribute<T>(this Enum enumeration)
        where T : Attribute
        {
            T attribute = enumeration.GetType().GetMember(enumeration.ToString())[0].GetCustomAttributes(typeof(T), false).Cast<T>().SingleOrDefault();

            if (attribute == null)
                return null;

            return attribute;
        }

        public static string GetDescription(this Enum enumeration)
        {
            var attr = enumeration.GetAttribute<DescriptionAttribute>();

            if (attr == null)
                return null;

            return attr.Description;
        }

        #endregion Enum

        #region Enumerator
        public static bool SkipWhile<T>(this IEnumerator<T> enumerator, Predicate<T> predicate)
        {
            bool found = true;
            while (predicate(enumerator.Current) )
            {
                found = enumerator.MoveNext();
                if (!found)
                    break;
            }

            return found;
        }

        public static IEnumerable<T> TakeWhile<T>(this IEnumerator<T> enumerator, Predicate<T> predicate)
        {
            while (predicate(enumerator.Current))
            {
                yield return enumerator.Current;
                if (!enumerator.MoveNext())
                    break;
            }

        }
        #endregion Enumerator

        #region IDisposable
        public static TOutput Using<TInput, TOutput>(this TInput disposable, Func<TInput, TOutput> action)
            where TInput : IDisposable
        {
            using (disposable)
                return action(disposable);
        }
        public static void Using<TInput>(this TInput disposable, Action<TInput> action)
           where TInput : IDisposable
        {
            using (disposable)
                action(disposable);
        }
        #endregion IDisposable

        #region MoveDirection

        public static IEnumerable<MoveDirection> GetDiagonals(this MoveDirection direction)
        {
            yield return direction | MoveDirection.Left;
            yield return direction | MoveDirection.Right;
        }

        #endregion MoveDirection

        #region PlayerColor

        public static PlayerColor Opponent(this PlayerColor player)
        {
            if (player == PlayerColor.White)
                return PlayerColor.Black;
            else if (player == PlayerColor.Black)
                return PlayerColor.White;

            throw new InvalidOperationException();
        }

        #endregion PlayerColor

        #region Point
        public static bool IsPositive(this Point point)
        {
            return point.X > 0 && point.Y > 0;
        }
        public static Square ToSquare(this Point point)
        {
            int rank = 7 - ((int)(point.Y) / 100);
            int column = (int)(point.X - 15) / 100;

            string square = ((char)('A' + column)).ToString() + (char)('1' + rank);
            if (!Enum.IsDefined(typeof(Square), square))
                return (Square)(-1);

            return (Square)Enum.Parse(typeof(Square), square);
        }

        #endregion Point

        #region Square

        public static Rectangle GetRectangle(this Square square)
        {
            int rank = 1 + (int)square / 8;
            int column = (int)square % 8;

            int y = ((8 - rank) * 100);
            int x = (column * 100);
            return new Rectangle(x + 15, y, 100, 100);
        }

        public static int GetRank(this Square square)
        {
            return 1 + (int)square / 8;
        }

        public static int GetColumn(this Square square)
        {
            return 1 + (int)square % 8;
        }

        public static SquareColor GetSquareColor(this Square square)
        {
            bool white = ((GetColumn(square) % 2) ^ (GetRank(square) % 2)) == 1;
            return white ? SquareColor.White : SquareColor.Black;
        }

        public static Square? Move(this Square? square, MoveDirection direction)
        {
            if (square.HasValue)
                return square.Value.Move(direction);

            return null;
        }

        public static Square? Move(this Square square, MoveDirection direction)
        {
            int rank = square.GetRank();
            int column = square.GetColumn();

            if (direction.HasFlag(MoveDirection.Up)) rank++;
            if (direction.HasFlag(MoveDirection.Down)) rank--;
            if (direction.HasFlag(MoveDirection.Left)) column--;
            if (direction.HasFlag(MoveDirection.Right)) column++;

            string ret = ((char)('A' + (column - 1))).ToString() + (char)('1' + (rank - 1));
            if (!Enum.IsDefined(typeof(Square), ret))
                return null;

            return (Square)Enum.Parse(typeof(Square), ret);
        }

        public static bool IsAdjacent(this Square source, Square target)
        {
            return Math.Abs(source.GetColumn() - target.GetColumn()) <= 1 && Math.Abs(source.GetRank() - target.GetRank()) <= 1;
        }

        public static IEnumerable<Square> GetAdjacencies(this Square square)
        {
            foreach (MoveDirection dir in Enum.GetValues(typeof(MoveDirection)))
            {
                var move = square.Move(dir);
                if (move.HasValue)
                    yield return move.Value;
            }
        }

        public static bool IsPromotionSquare(this Square square)
        {
            int rank = square.GetRank();
            return rank == 1 || rank == 8;
        }

        public static MoveDirection GetDirection(this Square square, Square target)
        {
            int rank1 = square.GetRank();
            int column1 = square.GetColumn();

            int rank2 = target.GetRank();
            int column2 = target.GetColumn();

            if (rank1 == rank2)
            {
                if (column1 > column2)
                    return MoveDirection.Left;
                else if (column1 < column2)
                    return MoveDirection.Right;
            }
            else if (column1 == column2)
            {
                if (rank1 > rank2)
                    return MoveDirection.Down;
                else if (rank1 < rank2)
                    return MoveDirection.Up;
            }
            else if (Math.Abs(column1 - column2) == Math.Abs(rank1 - rank2)) //Diagonal test
            {
                MoveDirection ret;

                if (column1 > column2)
                    ret = MoveDirection.Left;
                else
                    ret = MoveDirection.Right;

                if (rank1 > rank2)
                    return ret | MoveDirection.Down;
                else if (rank1 < rank2)
                    return ret | MoveDirection.Up;
            }

            return (MoveDirection)0;
        }

        #endregion Square

        #region SquareColor

        public static Color ToColor(this SquareColor squareColor)
        {
            if (squareColor == SquareColor.Black)
                return "#b58863".ToRgbColor();

            return "#faeac7".ToRgbColor();
        }

        #endregion SquareColor

        #region String

        public static Color ToRgbColor(this string text)
        {
            if (text[0] == '#')
                text = text.Substring(1);

            var rgb = long.Parse(text, System.Globalization.NumberStyles.HexNumber);
            return Color.FromArgb((int)(rgb | 0xFF000000));
        }

        #endregion String
    }
}