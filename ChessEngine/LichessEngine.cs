using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Chess;
using System.Net;
using System.Xml.XPath;
using Lichess;

namespace ChessEngine
{
    public class LichessEngine : BaseEngine {

        public override IEnumerable<IList<EngineCalcResult>> CalcScores(string fen, int? nodes = null, int? depth = null) {
            fen = FEN.StrictEnPassed(fen);

            var mateState = FEN.GetMateState(fen);
            if (mateState != null) {
                yield return new EngineCalcResult[] { new EngineCalcResult() { score = mateState.Value * 30000 } };
                yield break;
            }

            var request = WebRequest.Create($"https://lichess.org/api/cloud-eval?multiPv=5&fen={Uri.EscapeUriString(fen)}");
            WebResponse response = null;
            try {
                response = request.GetResponse();
            } catch (WebException e) {
                if (((HttpWebResponse)e.Response).StatusCode != HttpStatusCode.NotFound) {
                    throw;
                }
            }

            if (response == null) {
                yield return new EngineCalcResult[] { new EngineCalcResult() { san = "NotFound" } };
                yield break;
            }

            var result = "";
            
            using (Stream dataStream = response.GetResponseStream()) {
                var reader = new StreamReader(dataStream);
                result = reader.ReadToEnd();
            }
            response.Close();

            var xml = JsonHelper.JsonToXml(result);
            var _depth = int.Parse(xml.XPathSelectElement("depth").Value);

            /*
            if (_depth < 22) {
                yield return new EngineCalcResult[] { new EngineCalcResult() };
                yield break;
            }*/

            var calcResultList = new List<EngineCalcResult>();

            var pvs = xml.XPathSelectElements("pvs");
            foreach (var pv in pvs) {
                var cr = new EngineCalcResult();
                cr.uci = pv.XPathSelectElement("moves").Value;
                cr.san = FEN.Uci2San(fen, cr.uci);

                var xmlMate = xml.XPathSelectElement("mate");

                if (xmlMate != null) {
                    var mate = int.Parse(xmlMate.Value);
                    cr.score = (300 * Math.Sign(mate) - mate) * 100;
                }
                else {
                    cr.score = int.Parse(pv.XPathSelectElement("cp").Value);
                }

                calcResultList.Add(cr);
            }

            var turn = fen.IndexOf(" w ") > -1 ? 1 : -1;
            calcResultList = calcResultList.OrderByDescending(x => x.score * turn).ToList();

            yield return calcResultList;
        }

        public override void Stop() {}

        public override void Close() {}
    }
}
