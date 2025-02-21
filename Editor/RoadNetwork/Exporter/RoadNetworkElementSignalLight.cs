using PLATEAU.Native;
using UnityEngine;

namespace PLATEAU.Editor.RoadNetwork.Exporter
{
    /// <summary>
    /// 信号灯火機を表すクラス
    /// </summary>
    public class RoadNetworkElementSignalLight : RoadNetworkElement
    {
        /// <summary>
        /// 信号灯火器識別子のプレフィックス
        /// </summary>
        public static readonly string IDPrefix = "SignalLight";

        /// <summary>
        /// 信号制御器への参照
        /// </summary>
        public RoadNetworkElementSignalController Controller;

        /// <summary>
        /// 設置されているリンク
        /// </summary>
        public RoadNetworkElementLink Link;

        /// <summary>
        /// 信号灯火器の座標
        /// </summary>
        public Vector3 Coord;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">道路ネットワークのコンテキスト</param>
        /// <param name="id">ID</param>
        /// <param name="index">インデックス</param>
        public RoadNetworkElementSignalLight(RoadNetworkContext context, string id, int index) : base(context, CreateID(id))
        {
        }

        /// <summary>
        /// 信号灯火器の識別子を生成します
        /// </summary>
        /// <param name="id">元のID</param>
        /// <returns>新しいID</returns>
        private static string CreateID(string id)
        {
            var newID = IDPrefix + id;

            return newID;
        }

        /// <summary>
        /// 規制対象レーン種別を取得します
        /// </summary>
        /// <returns>レーンのタイプ</returns>
        public string GetLaneType()
        {
            return "Lane";
        }

        /// <summary>
        /// 規制対象レーン番号を取得します
        /// </summary>
        /// <returns>レーンの位置</returns>
        public string GetLanePos()
        {
            return "-1";
        }

        /// <summary>
        /// 設置位置を取得します
        /// </summary>
        /// <returns>距離</returns>
        public string GetDistance()
        {
            return "0";
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