using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PLATEAU.Native;
using PLATEAU.RoadNetwork.Data;

namespace PLATEAU.Editor.RoadNetwork.Exporter
{
    /// <summary>
    /// トラックを表すクラス
    /// </summary>
    public class RoadNetworkElementTrack : RoadNetworkElement
    {
        /// <summary>
        /// トラック識別子のプレフィックス
        /// </summary>
        public static readonly string IDPrefix = "Track";

        /// <summary>
        /// 元となる道路ネットワーク上のトラックデータ
        /// </summary>
        public RnDataTrack OriginTrack { get; private set; } = null;

        /// <summary>
        /// トラックの解像度
        /// </summary>
        private const int TrackResolution = 10;

        /// <summary>
        /// トラックの順序
        /// </summary>
        public int Order { get; private set; } = 0;

        /// <summary>
        /// 上り方向の距離
        /// </summary>
        public float UpDistance = 0.0f;

        /// <summary>
        /// 下り方向の距離
        /// </summary>
        public float DownDistance = 0.0f;

        /// <summary>
        /// 上り方向のリンク
        /// </summary>
        public RoadNetworkElementLink UpLink = null;

        /// <summary>
        /// 下り方向のリンク
        /// </summary>
        public RoadNetworkElementLink DownLink = null;

        /// <summary>
        /// 上り方向の接続レーン番号
        /// </summary>
        public int UpLane = 0;

        /// <summary>
        /// 下り方向の接続レーン番号
        /// </summary>
        public int DownLane = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="context">道路ネットワークのコンテキスト</param>
        /// <param name="id">トラックのID</param>
        /// <param name="track">元のトラックデータ</param>
        /// <param name="tracks">トラックのリスト</param>
        /// <param name="links">リンクのリスト</param>
        public RoadNetworkElementTrack(RoadNetworkContext context, string id, RnDataTrack track, List<RoadNetworkElementTrack> tracks, List<RoadNetworkElementLink> links) : base(context, IDPrefix + id)
        {
            ID = IDPrefix + id;

            OriginTrack = track;

            var allLanes = roadNetworkContext.RoadNetworkGetter.GetLanes();

            var allWays = roadNetworkContext.RoadNetworkGetter.GetWays();

            var allLineStrings = roadNetworkContext.RoadNetworkGetter.GetLineStrings();

            foreach (var link in links)
            {
                var lanes = link.GetOriginLanes();

                for (int i = 0; i < lanes.Count; i++)
                {
                    var lane = lanes[i];

                    var fromBorderID = OriginTrack.FromBorder.ID;
                    var fromLineID = allWays[fromBorderID].LineString.ID;
                    var toBorderID = OriginTrack.ToBorder.ID;
                    var toLineID = allWays[toBorderID].LineString.ID;
                    var nextBorderID = allLanes[lanes[i].ID].NextBorder.ID;
                    var prevBorderID = allLanes[lanes[i].ID].PrevBorder.ID;
                    if (nextBorderID >= allWays.Count || prevBorderID >= allWays.Count || nextBorderID < 0 || prevBorderID < 0) continue;
                    var nextWayID = allWays[nextBorderID].LineString.ID;
                    var prevWayID = allWays[prevBorderID].LineString.ID;
                    if (allLineStrings[fromLineID] == allLineStrings[nextWayID] ||
                        allLineStrings[fromLineID] == allLineStrings[prevWayID])
                    {
                        UpLink = link;

                        UpLane = i;
                    }

                    if (allLineStrings[toLineID] == allLineStrings[nextWayID] ||
                        allLineStrings[toLineID] == allLineStrings[prevWayID])
                    {
                        DownLink = link;

                        DownLane = i;
                    }
                }
            }

            if (UpLink == null || DownLink == null)
            {
                Debug.Log("Link not found. skipping. ID = " + ID);

                return;
            }

            var uplink = UpLink.ID.Replace(RoadNetworkElementLink.IDPrefix, "").Split('_');
            var downlink = DownLink.ID.Replace(RoadNetworkElementLink.IDPrefix, "").Split('_');

            Order = tracks.Where(t => t.UpLink == UpLink && t.DownLink == DownLink).ToList().Count;

            ID += "_" + uplink[0] + "_" + downlink[0] + "_" + Order;
        }

        /// <summary>
        /// ジオメトリ情報を取得します
        /// </summary>
        /// <returns>ジオメトリ情報のリスト</returns>
        public List<GeoJSON.Net.Geometry.Position> GetGeometory()
        {
            var coods = new List<GeoJSON.Net.Geometry.Position>();

            for (int i = 0; i < TrackResolution; i++)
            {
                var point = UnityEngine.Splines.SplineUtility.EvaluatePosition(OriginTrack.Spline, (float)i / TrackResolution);

                var cood = point;

                var geoCood = roadNetworkContext.GeoReference.Unproject(new PlateauVector3d(cood.x, cood.y, cood.z));

                coods.Add(new GeoJSON.Net.Geometry.Position(geoCood.Latitude, geoCood.Longitude));
            }

            return coods;
        }

        /// <summary>
        /// トラックの長さを取得します
        /// </summary>
        /// <returns>トラックの長さ</returns>
        public float GetLength()
        {
            return UnityEngine.Splines.SplineUtility.CalculateLength(OriginTrack.Spline, Matrix4x4.identity);
        }
    }
}