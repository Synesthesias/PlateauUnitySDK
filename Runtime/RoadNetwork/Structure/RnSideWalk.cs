using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Structure
{
    [System.Flags]
    public enum RnSideWalkWayTypeMask
    {
        None = 0,
        Outside = 1 << 0,
        Inside = 1 << 1,
        StartEdge = 1 << 2,
        EndEdge = 1 << 3,
    }

    public class RnSideWalk : ARnParts<RnSideWalk>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------
        // 自分が所属するRoadNetworkModel
        private RnRoadBase parentRoad;

        // 道路と反対側のWay
        // シリアライズ化の為にフィールドに
        private RnWay outsideWay;

        // 道路と同じ側のWay
        // シリアライズ化の為にフィールドに
        private RnWay insideWay;

        // outsideWayとinsideWayの始点を繋ぐWay
        // シリアライズ化の為にフィールドに
        private RnWay startEdgeWay;

        // outsideWayとinsideWayの終点を繋ぐWay
        // シリアライズ化の為にフィールドに
        private RnWay endEdgeWay;

        //----------------------------------
        // end: フィールド
        //----------------------------------

        public RnRoadBase ParentRoad => parentRoad;

        public RnWay OutsideWay => outsideWay;

        public RnWay InsideWay => insideWay;

        public RnWay StartEdgeWay => startEdgeWay;

        public RnWay EndEdgeWay => endEdgeWay;

        /// <summary>
        /// 左右のWay(OutsideWay, InsideWay)を列挙
        /// </summary>
        public IEnumerable<RnWay> SideWays
        {
            get
            {
                if (OutsideWay != null)
                    yield return OutsideWay;
                if (InsideWay != null)
                    yield return insideWay;
            }
        }

        /// <summary>
        /// 開始/終了の境界線のWay(StartEdgeWay, EndEdgeWay)を列挙
        /// </summary>
        public IEnumerable<RnWay> EdgeWays
        {
            get
            {
                if (StartEdgeWay != null)
                    yield return startEdgeWay;
                if (EndEdgeWay != null)
                    yield return endEdgeWay;
            }
        }

        /// <summary>
        /// Inside/OutsideのWayが両方ともValidかどうか. (Edgeは角の道だとnullの場合もあり得るのでチェックしない)
        /// </summary>
        public bool IsValid => InsideWay.IsValidOrDefault() && OutsideWay.IsValidOrDefault();

        public RnSideWalk() { }

        private RnSideWalk(RnRoadBase parent, RnWay outsideWay, RnWay insideWay, RnWay startEdgeWay, RnWay endEdgeWay)
        {
            this.parentRoad = parent;
            this.outsideWay = outsideWay;
            this.insideWay = insideWay;
            this.startEdgeWay = startEdgeWay;
            this.endEdgeWay = endEdgeWay;
        }

        public RnSideWalkWayTypeMask GetValidWayTypeMask()
        {
            var mask = RnSideWalkWayTypeMask.None;
            if (outsideWay != null)
                mask |= RnSideWalkWayTypeMask.Outside;
            if (insideWay != null)
                mask |= RnSideWalkWayTypeMask.Inside;
            if (startEdgeWay != null)
                mask |= RnSideWalkWayTypeMask.StartEdge;
            if (endEdgeWay != null)
                mask |= RnSideWalkWayTypeMask.EndEdge;
            return mask;
        }

        /// <summary>
        /// 親情報の再設定
        /// </summary>
        /// <param name="parent"></param>
        public void ChangeParent(RnRoadBase parent)
        {
            // 以前の親からは削除する
            parent?.RemoveSideWalk(this);
            this.parentRoad = parent;
        }

        /// <summary>
        /// 左右のWayを再設定(使い方によっては構造壊れるので注意)
        /// </summary>
        /// <param name="outsideWay"></param>
        /// <param name="insideWay"></param>
        public void SetSideWays(RnWay outsideWay, RnWay insideWay)
        {
            this.outsideWay = outsideWay;
            this.insideWay = insideWay;
        }

        /// <summary>
        /// 境界のWayを再設定(使い方によっては構造壊れるので注意)
        /// </summary>
        /// <param name="startEdgeWay"></param>
        /// <param name="endEdgeWay"></param>
        public void SetEdgeWays(RnWay startEdgeWay, RnWay endEdgeWay)
        {
            this.startEdgeWay = startEdgeWay;
            this.endEdgeWay = endEdgeWay;
        }

        /// <summary>
        /// 歩道作成
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="outsideWay"></param>
        /// <param name="insideWay"></param>
        /// <param name="startEdgeWay"></param>
        /// <param name="endEdgeWay"></param>
        /// <returns></returns>
        public static RnSideWalk Create(RnRoadBase parent, RnWay outsideWay, RnWay insideWay, RnWay startEdgeWay, RnWay endEdgeWay)
        {
            var sideWalk = new RnSideWalk(parent, outsideWay, insideWay, startEdgeWay, endEdgeWay);
            parent.AddSideWalk(sideWalk);
            return sideWalk;
        }
    }

    public static class RnSideWalkEx
    {
        public static Vector3 GetCenter(this RnSideWalk self)
        {
            if (self == null)
                return Vector3.zero;
            var num = (self.OutsideWay?.Count ?? 0) + (self.InsideWay?.Count ?? 0);
            if (num == 0)
                return Vector3.zero;
            var sum = Enumerable.Repeat(self.OutsideWay, 1)
                .Concat(Enumerable.Repeat(self.InsideWay, 1))
                .Where(w => w != null)
                .SelectMany(w => w)
                .Aggregate(Vector3.zero, (sum, way) => sum + way);
            return sum / num;
        }
    }
}