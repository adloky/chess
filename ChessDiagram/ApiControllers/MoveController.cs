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
        [HttpGet]
        public IEnumerable<MoveInfo> Info(string pgn = "")
        {
            var moves = Pgn.Load(pgn).Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            var moveInfoList = new List<MoveInfo>();
            var board = Board.Load();

            foreach (var move in moves) {
                var mi = new MoveInfo();
                mi.move = move;
                var clearMove = move.Split('~')[0];
                mi.uci = board.ParseSanMove(clearMove).ToString();
                if (clearMove != move) {
                    mi.uci += move.Substring(clearMove.Length);
                }
                board.Move(clearMove);
                mi.fen = board.GetFEN();
                moveInfoList.Add(mi);
            }

            return moveInfoList;
        }
    }
}
