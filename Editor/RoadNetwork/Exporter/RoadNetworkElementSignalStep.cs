using System.Collections.Generic;

namespace PLATEAU.Editor.RoadNetwork.Exporter
{
    /// <summary>
    /// 信号現示階梯を表すクラス
    /// </summary>
    public class RoadNetworkElementSignalStep : RoadNetworkElement
    {
        /// <summary>
        /// 信号現示階梯識別子のプレフィックス
        /// </summary>
        public static readonly string IDPrefix = "SignalStep";

        /// <summary>
        /// 信号制御器への参照
        /// </summary>
        public RoadNetworkElementSignalController Controller;

        /// <summary>
        /// 信号灯火器への参照リスト
        /// </summary>
        public List<RoadNetworkElementSignalLight> SignalLights;

        /// <summary>
        /// パターンID
        /// </summary>
        public string PatternID;

        /// <summary>
        /// 現示順
        /// </summary>
        public int Order;

        /// <summary>
        /// 現示時間
        /// </summary>
        public int Duration;

        /// <summary>
        /// 緑信号のリンクペアのリスト
        /// </summary>
        public List<(RoadNetworkElementLink In, RoadNetworkElementLink Out)> LinkPairsGreen = new List<(RoadNetworkElementLink In, RoadNetworkElementLink Out)>();

        /// <summary>
        /// 黄信号のリンクペアのリスト
        /// </summary>
        public List<(RoadNetworkElementLink In, RoadNetworkElementLink Out)> LinkPairsYellow = new List<(RoadNetworkElementLink In, RoadNetworkElementLink Out)>();

        /// <summary>
        /// 赤信号のリンクペアのリスト
        /// </summary>
        public List<(RoadNetworkElementLink In, RoadNetworkElementLink Out)> LinkPairsRed = new List<(RoadNetworkElementLink In, RoadNetworkElementLink Out)>();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">道路ネットワークのコンテキスト</param>
        /// <param name="id">ID</param>
        /// <param name="index">インデックス</param>
        public RoadNetworkElementSignalStep(RoadNetworkContext context, string id, int index) : base(context, CreateID(id))
        {
        }

        /// <summary>
        /// 信号現示階梯の識別子を生成します
        /// </summary>
        /// <param name="id">元のID</param>
        /// <returns>生成されたID</returns>
        private static string CreateID(string id)
        {
            var newID = IDPrefix + id;

            return newID;
        }

        /// <summary>
        /// リンクペアのリストから色を取得します
        /// </summary>
        /// <param name="pair">リンクペアのリスト</param>
        /// <returns>色の文字列</returns>
        public string GetColor(List<(RoadNetworkElementLink In, RoadNetworkElementLink Out)> pair)
        {
            var ret = "";

            foreach (var p in pair)
            {
                if (ret != "")
                {
                    ret += ":";
                }

                if (p.In == null || p.Out == null)
                {
                    continue;
                }

                ret += p.In.ID + "->" + p.Out.ID;
            }

            return ret;
        }

        public string GetSignalController()
        {
            return Controller?.ID;
        }

        /// <summary>
        /// 信号灯火器のIDを取得します
        /// </summary>
        /// <returns>信号灯火器のID</returns>
        public string GetSignalLights()
        {
            var ret = string.Empty;
            
            if(SignalLights == null)
            {
                return ret;
            }

            for (int i = 0; i < SignalLights.Count; i++)
            {
                ret += (i == 0 ? "" : ":") + SignalLights[i].ID;
            }

            return ret;
        }

        /// <summary>
        /// 進行可能な車両の種類を取得します
        /// </summary>
        /// <returns></returns>
        public int GetTypeMask()
        {
            return -1;
        }

        /// <summary>
        /// ジオメトリ情報を取得します
        /// </summary>
        /// <returns>ジオメトリの位置</returns>
        public GeoJSON.Net.Geometry.Position GetGeometory()
        {
            return Controller.GetGeometory();
        }
    }
}