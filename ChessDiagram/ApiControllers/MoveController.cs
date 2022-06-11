using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Text.RegularExpressions;
using Chess;
using ChessDiagram.Models;

namespace ChessDiagram.ApiControllers
{
    public class MoveController : ApiController
    {
        private string GetSkipFen(string fen) {
            return fen.IndexOf(" w ") > -1 ? fen.Replace(" w ", " b ") : fen.Replace(" b ", " w ");
        }

        [HttpGet]
        public IEnumerable<MoveInfo> Info(string pgn = "")
        {
            var moves = Pgn.Load(pgn).Moves.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            var moveInfoList = new List<MoveInfo>();
            var fen = Board.DEFAULT_STARTING_FEN;

            foreach (var move in moves) {
                var mi = new MoveInfo();
                mi.move = move;
                var cleanMove = move.Split('~')[0];
                mi.uci = cleanMove == "-" ? "a1a1" : FEN.San2Uci(fen, cleanMove);
                if (cleanMove != move) {
                    mi.uci += move.Substring(cleanMove.Length);
                }
                fen = cleanMove == "-" ? GetSkipFen(fen) : FEN.Move(fen, cleanMove);
                mi.fen = fen;
                moveInfoList.Add(mi);
            }

            return moveInfoList;
        }
    }
}
