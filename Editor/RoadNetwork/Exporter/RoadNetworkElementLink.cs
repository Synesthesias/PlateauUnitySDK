using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PLATEAU.Native;
using PLATEAU.RoadNetwork.Data;

namespace PLATEAU.Editor.RoadNetwork.Exporter
{
    /// <summary>
    /// リンクを表すクラス
    /// </summary>
    public class RoadNetworkElementLink : RoadNetworkElement
    {
        /// <summary>
        /// リンク識別子のプレフィックス
        /// </summary>
        public static readonly string IDPrefix = "Link";

        /// <summary>
        /// 上流ノード
        /// </summary>
        public RoadNetworkElementNode UpNode { get; set; }

        /// <summary>
        /// 下流ノード
        /// </summary>
        public RoadNetworkElementNode DownNode { get; set; }

        /// <summary>
        /// 対になるリンク
        /// </summary>
        public RoadNetworkElementLink Pair { get; set; }

        /// <summary>
        /// リンクが逆方向かどうかを示す
        /// </summary>
        public bool IsReverse { get; set; }

        /// <summary>
        /// 道路ネットワーク上のインデックス
        /// </summary>
        public int RoadNetworkIndex { get; private set; } = -1;

        /// <summary>
        /// リンク内のレーンのリスト
        /// </summary>
        public List<RoadNetworkElementLane> Lanes = new List<RoadNetworkElementLane>();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">道路ネットワークのコンテキスト</param>
        /// <param name="id">リンクのID</param>
        /// <param name="index">道路ネットワークのインデックス</param>
        public RoadNetworkElementLink(RoadNetworkContext context, string id, int index) : base(context, CreateID(id))
        {
            RoadNetworkIndex = index;

            // Note: Reverse判定がまだ行われていないためここではレーン生成は行わない
        }

        /// <summary>
        /// リンクの識別子を生成します
        /// </summary>
        /// <param name="id">元のID</param>
        /// <returns>生成されたID</returns>
        private static string CreateID(string id)
        {
            var newID = IDPrefix + id;

            return newID;
        }

        /// <summary>
        /// 元となる道路ネットワーク上の道路
        /// </summary>
        public RnDataRoad OriginLink
        {
            get
            {
                if (RoadNetworkIndex < 0)
                {
                    return null;
                }

                return roadNetworkContext.RoadNetworkGetter.GetRoadBases()[RoadNetworkIndex] as RnDataRoad;
            }
        }

        /// <summary>
        /// リンク内のレーンを生成します
        /// </summary>
        public void GenerateLane()
        {
            Lanes = new List<RoadNetworkElementLane>();

            if (OriginLink != null)
            {
                var lanesall = roadNetworkContext.RoadNetworkGetter.GetLanes();

                var lanes = GetOriginLanes();

                for (int i = 0; i < lanes.Count; i++)
                {
                    var lane = lanesall[lanes[i].ID];

                    Lanes.Add(new RoadNetworkElementLane(roadNetworkContext, ID.Replace(IDPrefix, ""), lane, i));
                }
            }
        }

        /// <summary>
        /// ジオメトリ情報を取得します
        /// </summary>
        /// <returns>ジオメトリ情報のリスト</returns>
        public List<GeoJSON.Net.Geometry.Position> GetGeometory()
        {
            // Note: そもそも空リンクの場合はジオメトリ取得されることないかも
            if (Lanes.Count == 0)
            {
                Vector3 upCoord;
                Vector3 downCoord;

                if (UpNode.IsVirtual)
                {
                    upCoord = UpNode.Coord;
                }
                else
                {
                    upCoord = UpNode.GetPosition();
                }

                if (DownNode.IsVirtual)
                {
                    downCoord = DownNode.Coord;
                }
                else
                {
                    downCoord = DownNode.GetPosition();
                }

                var upGeoCood = roadNetworkContext.GeoReference.Unproject(new PlateauVector3d(upCoord.x, upCoord.y, upCoord.z));
                var downGeoCood = roadNetworkContext.GeoReference.Unproject(new PlateauVector3d(downCoord.x, downCoord.y, downCoord.z));

                return new List<GeoJSON.Net.Geometry.Position>() { new GeoJSON.Net.Geometry.Position(upGeoCood.Latitude, upGeoCood.Longitude), new GeoJSON.Net.Geometry.Position(downGeoCood.Latitude, downGeoCood.Longitude) };
            }

            return Lanes[IsReverse ? 0 : Lanes.Count - 1].GetGeometory(false);
        }

        /// <summary>
        /// レーンの長さを取得します
        /// </summary>
        /// <returns>レーンの長さ</returns>
        public double GetLinkLength()
        {
            if (Lanes.Count == 0)
            {
                return 0;
            }

            return Lanes[IsReverse ? 0 : Lanes.Count - 1].GetLength();
        }

        /// <summary>
        /// リンクに所属する元のレーンを取得します
        /// </summary>
        /// <returns>元のレーンのリスト</returns>
        public List<RnID<RnDataLane>> GetOriginLanes()
        {
            List<RnID<RnDataLane>> lanes = new List<RnID<RnDataLane>>();

            var laneAll = roadNetworkContext.RoadNetworkGetter.GetLanes();

            if (OriginLink == null) return lanes;
            if (OriginLink.MainLanes == null) return lanes;
            foreach (var lane in OriginLink.MainLanes)
            {
                if (!lane.IsValid) continue;

                if (IsReverse == laneAll[lane.ID].IsReverse)
                {
                    lanes.Add(lane);
                }
            }

            return lanes;
        }

        /// <summary>
        /// レーンの数を取得します
        /// </summary>
        /// <returns>レーンの数</returns>
        public int GetLaneNum()
        {
            return GetOriginLanes().Count;
        }
    }
}