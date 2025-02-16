using PLATEAU.CityInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Structure
{
    /// <summary>
    /// Serialize時にnewする必要があるのでabstractにはできない
    /// </summary>
    [Serializable]
    public partial class RnRoadBase : ARnParts<RnRoadBase>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------

        // 自分が所属するRoadNetworkModel
        public RnModel ParentModel { get; set; }

        /// <summary>
        ///  これに紐づくtranオブジェクトリスト(統合なので複数存在する場合がある)
        /// </summary>
        public List<PLATEAUCityObjectGroup> TargetTrans { get; set; } = new List<PLATEAUCityObjectGroup>();

        /// <summary>
        /// 歩道情報
        /// </summary>
        protected List<RnSideWalk> sideWalks = new List<RnSideWalk>();


        //----------------------------------
        // end: フィールド
        //----------------------------------

        public IReadOnlyList<RnSideWalk> SideWalks => sideWalks;

        /// <summary>
        /// 歩道sideWalkを追加する.
        /// sideWalkの親情報も書き換える
        /// </summary>
        /// <param name="sideWalk"></param>
        public void AddSideWalk(RnSideWalk sideWalk)
        {
            if (sideWalk == null)
                return;
            if (sideWalks.Contains(sideWalk))
                return;
            // 以前の親からは削除
            sideWalk?.ParentRoad?.RemoveSideWalk(sideWalk);
            sideWalk.SetParent(this);
            sideWalks.Add(sideWalk);
        }

        /// <summary>
        /// 歩道sideWalkを削除する.
        /// sideWalkの親情報は変更しない
        /// </summary>
        /// <param name="sideWalk"></param>
        public void RemoveSideWalk(RnSideWalk sideWalk)
        {
            if (sideWalk == null)
                return;
            sideWalk.SetParent(null);
            sideWalks.Remove(sideWalk);
        }

        /// <summary>
        /// 隣接道路とその境界線情報
        /// </summary>
        public class NeighborBorder
        {
            // 隣接するRoad
            public RnRoadBase NeighborRoad { get; set; }

            // 境界線の線分
            public RnWay BorderWay { get; set; }
        }

        /// <summary>
        /// 隣接するRoadとその境界線情報を取得. 同じRoadに対して複数の境界線がある場合がある.
        /// また、隣接するRoadはnullの場合もある
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<NeighborBorder> GetBorders() { yield break; }


        // 隣接するRoadを取得
        public virtual IEnumerable<RnRoadBase> GetNeighborRoads() { yield break; }


        /// <summary>
        /// 対象のTargetTranを追加
        /// </summary>
        /// <param name="targetTran"></param>
        public void AddTargetTran(PLATEAUCityObjectGroup targetTran)
        {
            if (TargetTrans.Contains(targetTran) == false)
                TargetTrans.Add(targetTran);
        }

        public void AddTargetTrans(IEnumerable<PLATEAUCityObjectGroup> targetTrans)
        {
            foreach (var t in targetTrans)
                AddTargetTran(t);
        }

        /// <summary>
        /// 所属するすべてのWayを取得(重複の可能性あり)
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<RnWay> AllWays()
        {
            foreach (var sw in sideWalks)
            {
                foreach (var way in sw.AllWays)
                    yield return way;
            }
        }

        /// <summary>
        /// 自身の接続を切断する.
        /// removeFromModel=trueの場合、RnModelからも削除する
        /// </summary>
        public virtual void DisConnect(bool removeFromModel)
        {
            if (removeFromModel)
            {
                foreach (var sw in sideWalks)
                    ParentModel?.RemoveSideWalk(sw);
            }
        }

        /// <summary>
        /// デバッグ用) その道路の中心を表す代表頂点を返す
        /// </summary>
        /// <returns></returns>
        public virtual Vector3 GetCentralVertex()
        {
            return Vector3.zero;
        }


        /// <summary>
        /// 隣接情報otherをつながりから削除する. other側の接続は消えない
        /// </summary>
        /// <param name="other"></param>
        public void UnLink(RnRoadBase other)
        {
            ReplaceNeighbor(other, null);
        }

        /// <summary>
        /// 情報を直接書き換えるので呼び出し注意(相互に隣接情報を維持するように書き換える必要がある)
        /// 隣接情報をfrom -> toに変更する.
        /// (from/to側の隣接情報は変更しない)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public virtual void ReplaceNeighbor(RnRoadBase from, RnRoadBase to) { }

        /// <summary>
        /// 不正チェック処理を行う
        /// </summary>
        /// <returns></returns>
        public virtual bool Check() { return true; }

    }

    public static class RnRoadBaseEx
    {
        /// <summary>
        /// 相互に接続を解除する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        public static void UnLinkEachOther(this RnRoadBase self, RnRoadBase other)
        {
            self?.UnLink(other);
            other?.UnLink(self);
        }

        /// <summary>
        /// デバッグ表示用. TargetTransの名前を取得
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string GetTargetTransName(this RnRoadBase self)
        {
            if (self == null || self.TargetTrans == null)
                return "null";

            return string.Join(",", self.TargetTrans.Select(t => !t ? "null" : t.name));

        }

        /// <summary>
        /// selfのすべてのLineStringを取得
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static HashSet<RnLineString> GetAllLineStringsDistinct(this RnRoadBase self)
        {
            if (self == null)
                return new HashSet<RnLineString>();
            return self.AllWays().Select(w => w.LineString).Where(ls => ls != null).ToHashSet();
        }
    }
}