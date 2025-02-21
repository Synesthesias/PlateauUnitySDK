using PLATEAU.Native;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLATEAU.Editor.RoadNetwork.Exporter
{
    /// <summary>
    /// 信号制御器を表すクラス
    /// </summary>
    public class RoadNetworkElementSignalController : RoadNetworkElement
    {
        /// <summary>
        /// 信号制御器識別子のプレフィックス
        /// </summary>
        public static readonly string IDPrefix = "SignalController";

        /// <summary>
        /// 設置ノード
        /// </summary>
        public RoadNetworkElementNode Node;

        /// <summary>
        /// 信号制御器の座標
        /// </summary>
        public Vector3 Coord;

        /// <summary>
        /// 制御対象の信号灯器
        /// </summary>
        public List<RoadNetworkElementSignalLight> SignalLights = new List<RoadNetworkElementSignalLight>();

        /// <summary>
        /// オフセット基準信号制御器
        /// </summary>
        public RoadNetworkElementSignalController OffsetController;

        /// <summary>
        /// オフセットタイプ
        /// </summary>
        public int OffsetType;

        /// <summary>
        /// オフセット値
        /// </summary>
        public int OffsetValue;

        /// <summary>
        /// 信号パターンのリスト
        /// </summary>
        public Dictionary<string, List<RoadNetworkElementSignalStep>> SignalPatterns = new Dictionary<string, List<RoadNetworkElementSignalStep>>();

        /// <summary>
        /// パターンインデックス
        /// </summary>
        public int PatternIndex;

        /// <summary>
        /// 道路ネットワーク上のインデックス
        /// </summary>
        public int RoadNetworkIndex { get; private set; } = -1;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">道路ネットワークのコンテキスト</param>
        /// <param name="id">ID</param>
        /// <param name="index">インデックス</param>
        public RoadNetworkElementSignalController(RoadNetworkContext context, string id, int index) : base(context, CreateID(id))
        {
        }

        /// <summary>
        /// 信号制御器の識別子を生成します
        /// </summary>
        /// <param name="id">元のID</param>
        /// <returns>新しいID</returns>
        private static string CreateID(string id)
        {
            var newID = IDPrefix + id;

            return newID;
        }

        /// <summary>
        /// 設置ノードを取得します
        /// </summary>
        /// <returns>設置ノード</returns>
        public string GetNode()
        {
            return Node.ID;
        }

        /// <summary>
        /// 制御対象の制御信号灯器を取得します
        /// </summary>
        /// <returns>制御対象の制御信号灯器の文字列</returns>
        public string GetSignalLights()
        {
            if (SignalLights.Count == 0)
            {
                return "";
            }

            var ret = SignalLights[0].ID;

            for (int i = 1; i < SignalLights.Count; i++)
            {
                ret += ":" + SignalLights[i].ID;
            }

            return ret;
        }

        /// <summary>
        /// 制御パターン数を取得します
        /// </summary>
        /// <returns>パターンの数</returns>
        public int GetPatternNum()
        {
            return SignalPatterns.Count;
        }

        /// <summary>
        /// 制御パターンを取得します
        /// </summary>
        /// <returns>パターンIDの文字列</returns>
        public string GetPatternID()
        {
            string ret = "";

            foreach (var pattern in SignalPatterns.Select((kvp, index) => new { kvp, index }))
            {
                if (pattern.kvp.Value.Count == 0) continue;

                ret += pattern.index == 0 ? pattern.kvp.Value[0].PatternID : ":" + pattern.kvp.Value[0].PatternID;
            }

            return ret;
        }

        /// <summary>
        /// サイクル長を取得します
        /// </summary>
        /// <returns>サイクルの長さ</returns>
        public string GetCycleLen()
        {
            string ret = "";

            foreach (var pattern in SignalPatterns.Select((kvp, index) => new { kvp, index }))
            {
                int cycle = 0;

                pattern.kvp.Value.ForEach(x => cycle += x.Duration);

                ret += pattern.index == 0 ? cycle.ToString() : ":" + cycle.ToString();
            }
            return ret;
        }

        /// <summary>
        /// 現示数を取得します
        /// </summary>
        /// <returns>フェーズの数</returns>
        public string GetPhaseNum()
        {
            string ret = "";

            foreach (var pattern in SignalPatterns.Select((kvp, index) => new { kvp, index }))
            {
                int phase = pattern.kvp.Value.Count;

                ret += pattern.index == 0 ? phase.ToString() : ":" + phase.ToString();
            }

            return ret;
        }

        /// <summary>
        /// 制御パターン開始時間を取得します
        /// </summary>
        /// <returns>開始時間の文字列</returns>
        public string GetStartTime()
        {
            string ret = "";

            foreach (var pattern in SignalPatterns.Select((kvp, index) => new { kvp, index }))
            {
                ret += pattern.index == 0 ? pattern.kvp.Key : ":" + pattern.kvp.Key;
            }

            return ret;
        }

        /// <summary>
        /// ジオメトリ情報を取得します
        /// </summary>
        /// <returns>ジオメトリの位置</returns>
        public GeoJSON.Net.Geometry.Position GetGeometory()
        {
            Vector3 coord = Coord;

            var geoCoord = roadNetworkContext.GeoReference.Unproject(new PlateauVector3d(coord.x, coord.y, coord.z));

            return new GeoJSON.Net.Geometry.Position(geoCoord.Latitude, geoCoord.Longitude);
        }
    }
}