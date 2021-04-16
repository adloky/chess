using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Chess
{
    internal class Images
    {
        private static Dictionary<string, Bitmap> imageCache = new Dictionary<string, Bitmap>();

        private static Dictionary<string, SquareImage> boardImageCache = DrawBoardImages();

        private static Dictionary<string, SquareImage> DrawBoardImages()
        {
            var playerColors = new[] { PlayerColor.Black, PlayerColor.White };
            var ret = new Dictionary<string, SquareImage>();

            var emptySquare = new SquareImage();
            emptySquare.BlackSquare = DrawSquare(SquareColor.Black);
            emptySquare.WhiteSquare = DrawSquare(SquareColor.White);

            ret.Add("Empty", emptySquare);

            foreach (var type in typeof(Piece).Assembly.GetTypes().Where(i => i.IsSubclassOf(typeof(Piece))))
            {
                foreach (var playerColor in playerColors)
                {
                    var img = new SquareImage();
                    img.BlackSquare = DrawSquare(SquareColor.Black, playerColor, type);
                    img.WhiteSquare = DrawSquare(SquareColor.White, playerColor, type);
                    ret.Add(playerColor + type.Name, img);
                }
            }

            return ret;
        }

        private static Image DrawSquare(SquareColor color, PlayerColor player = 0, Type piece = null)
        {
            var image = new Bitmap(100, 100);
            using (var g = Graphics.FromImage(image))
            {
                g.FillRectangle(new SolidBrush(color.ToColor()), 0, 0, 100, 100);
                if (piece != null)
                    g.DrawImage(Images.GetPieceImage(player, piece), 0, 0, 100, 100);
            }
            return image;
        }

        private static Bitmap LoadImage(string objectName)
        {
            string file = "Chess.Images." + objectName + ".png";
            if (imageCache.ContainsKey(file))
                return imageCache[file];

            System.Reflection.Assembly myAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            Stream stream = myAssembly.GetManifestResourceStream(file);
            return imageCache[file] = new Bitmap(stream);
        }

        public static Image GetPieceImage(Piece piece)
        {
            return GetPieceImage(piece.Player, piece.GetType());
        }

        public static Image GetPieceImage(PlayerColor color, Type pieceType)
        {
            return LoadImage(color + pieceType.Name);
        }

        internal static Image GetSquareImage(Square square, Piece piece = null)
        {
            if (piece == null)
                return boardImageCache["Empty"][square.GetSquareColor()];

            var squareColor = square.GetSquareColor();
            var key = piece.Player + piece.GetType().Name;
            return boardImageCache[key][squareColor];
        }

        #region SquareImage

        private class SquareImage
        {
            public Image BlackSquare { get; set; }

            public Image WhiteSquare { get; set; }

            public Image this[SquareColor color]
            {
                get
                {
                    return color == SquareColor.White ? this.WhiteSquare : this.BlackSquare;
                }
            }
        }

        #endregion SquareImage
    }
}