﻿using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Data;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.PlayerLoop;
using static Codice.Client.Commands.WkTree.WorkspaceTreeNode;
using static UnityEngine.GraphicsBuffer;

namespace PLATEAU.RoadNetwork
{

    [Flags]
    public enum RnLinkAttribute
    {
        // 1レーンしかない時にそのレーンが両方向かどうか
        BothSide = 1 << 0,
    }
    //[Serializable]
    public class RnLink : RnRoadBase
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------
        // 自分が所属するRoadNetworkModel
        public RnModel ParentModel { get; set; }

        // 対象のtranオブジェクト
        public PLATEAUCityObjectGroup TargetTran { get; set; }

        // 接続先
        public RnRoadBase Next { get; private set; }

        // 接続元
        public RnRoadBase Prev { get; private set; }

        // レーンリスト
        private List<RnLane> mainLanes = new List<RnLane>();

        // 中央分離帯
        private RnLane medianLane;

        // 即性情報
        public RnLinkAttribute RnLinkAttribute { get; set; }

        //----------------------------------
        // end: フィールド
        //----------------------------------

        // 本線レーン(参照のみ)
        // 追加/削除はAddMainLane/RemoveMainLaneを使うこと
        public IReadOnlyList<RnLane> MainLanes => mainLanes;

        // 全レーン
        public override IEnumerable<RnLane> AllLanes => MainLanes;

        // 有効なLinkかどうか
        public bool IsValid => MainLanes.Any();

        /// <summary>
        /// 左車線/右車線両方あるかどうか
        /// </summary>
        public bool HasBothLane
        {
            get
            {
                var hasLeft = false;
                var hasRight = false;
                foreach (var lane in MainLanes)
                {
                    if (IsLeftLane(lane))
                        hasLeft = true;
                    else if (IsRightLane(lane))
                        hasRight = true;
                    if (hasLeft && hasRight)
                        return true;
                }

                return false;
            }
        }

        public RnLane MedianLane => medianLane;

        public RnLink() { }

        public RnLink(PLATEAUCityObjectGroup targetTran)
        {
            TargetTran = targetTran;
        }

        public IEnumerable<RnLane> GetLanes(RnDir dir)
        {
            return dir switch
            {
                RnDir.Left => GetLeftLanes(),
                RnDir.Right => GetRightLanes(),
                _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null),
            };
        }

        /// <summary>
        /// laneがこのLinkの左車線かどうか(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <param name="lane"></param>
        /// <returns></returns>
        private bool IsLeftLane(RnLane lane)
        {
            return lane.GetNextRoad() == Next;
        }

        /// <summary>
        /// laneがこのLinkの右車線かどうか(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <param name="lane"></param>
        /// <returns></returns>
        private bool IsRightLane(RnLane lane)
        {
            return lane.GetNextRoad() == Prev;
        }

        // 境界線情報を取得
        public override IEnumerable<RnBorder> GetBorders()
        {
            foreach (var lane in MainLanes)
            {
                if (lane.PrevBorder != null)
                    yield return new RnBorder(lane.PrevBorder, lane);
                if (lane.NextBorder != null)
                    yield return new RnBorder(lane.NextBorder, lane);
            }
        }

        /// <summary>
        /// 左側のレーン(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RnLane> GetLeftLanes()
        {
            return MainLanes.Where(IsLeftLane);
        }

        /// <summary>
        /// 右側のレーン(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RnLane> GetRightLanes()
        {
            return MainLanes.Where(IsRightLane);
        }

        /// <summary>
        /// 左側レーン数(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <returns></returns>
        public int GetLeftLaneCount()
        {
            return GetLeftLanes().Count();
        }

        /// <summary>
        /// 右側レーン数(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <returns></returns>
        public int GetRightLaneCount()
        {
            return GetRightLanes().Count();
        }

        /// <summary>
        /// 中央分離帯の幅を取得する
        /// </summary>
        /// <returns></returns>
        public float GetMedianWidth()
        {
            if (MedianLane == null)
                return 0f;

            return MedianLane.AllBorders.Select(b => b.CalcLength()).Min();
        }

        /// <summary>
        /// 直接呼ぶの禁止. RnLinkGroupから呼ばれる
        /// </summary>
        /// <param name="lane"></param>
        public void SetMedianLane(RnLane lane)
        {
            medianLane = lane;
        }

        /// <summary>
        /// 中央分離帯の幅を設定する. 中央分離帯が作られていない時(左右どっちかしかレーンが無い時)は無視される
        /// </summary>
        /// <param name="width"></param>
        /// <param name="changeAllLaneWidth"></param>
        public bool SetMedianWidth(float width, bool changeAllLaneWidth = false)
        {
            var nowWidth = GetMedianWidth();
            if (MedianLane == null)
                return false;

            var deltaWidth = width - nowWidth;
            medianLane.LeftWay.MoveAlongNormal(deltaWidth * 0.5f);
            medianLane.RightWay.MoveAlongNormal(deltaWidth * 0.5f);
            return true;
        }

        /// <summary>
        /// 全てのレーンのBorderを統合した一つの大きなBorderを返す
        /// WayはLeft -> Right方向になっている
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public RnWay GetMergedBorder(RnLaneBorderType type)
        {
            var ret = new RnLineString();
            foreach (var l in MainLanes)
            {
                RnWay way = null;
                var t = type;
                var dir = RnLaneBorderDir.Left2Right;
                if (IsLeftLane(l) == false)
                {
                    t = t.GetOpposite();
                    dir = dir.GetOpposite();
                }

                way = l.GetBorder(t);
                if (l.GetBorderDir(t) != dir)
                    way = way.ReversedWay();

                foreach (var p in way.Points)
                    ret.AddPointOrSkip(p);
            }
            return new RnWay(ret);
        }

        /// <summary>
        /// Lanes全体を一つのLaneとしたときのdir側のWayを返す
        /// WayはPrev -> Nextの方向になっている
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public RnWay GetMergedSideWay(RnDir dir)
        {
            if (IsValid == false)
                return null;
            switch (dir)
            {
                case RnDir.Left:
                    {
                        var lane = MainLanes[0];
                        return IsLeftLane(lane) ? lane?.LeftWay : lane?.RightWay?.ReversedWay();
                    }
                case RnDir.Right:
                    {
                        var lane = MainLanes[^1];
                        return IsLeftLane(lane) ? lane?.RightWay : lane?.LeftWay?.ReversedWay();
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        /// <summary>
        /// dir方向の一番左のWayと右のWayを取得.
        /// 向きは調整されていない
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="leftWay"></param>
        /// <param name="rightWay"></param>
        /// <returns></returns>
        public bool TryGetSideBorderWay(RnDir dir, out RnWay leftWay, out RnWay rightWay)
        {
            leftWay = rightWay = null;
            if (IsValid == false)
                return false;

            var lanes = GetLanes(dir).ToList();
            if (lanes.Any() == false)
                return false;

            leftWay = lanes[0].LeftWay;
            rightWay = lanes[^1].RightWay;
            return true;
        }

        /// <summary>
        /// dir方向のレーンの最も左のWayと最も右のWayを取得する
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public RnLane GetBorderLane(RnDir dir)
        {
            var prevBorder = new RnLineString();
            var nextBorder = new RnLineString();

            void Merge(RnLineString points, RnWay way)
            {
                foreach (var p in way.Points)
                {
                    points.AddPointOrSkip(p);
                }
            }

            RnLane firstLane = null;
            RnLane lastLane = null;
            foreach (var lane in GetLanes(dir))
            {
                if (lane == null)
                    continue;
                Merge(prevBorder, lane.PrevBorder);
                Merge(nextBorder, lane.NextBorder);
                firstLane ??= lane;
                lastLane = lane;
            }

            return new RnLane(firstLane?.LeftWay, lastLane?.RightWay, new RnWay(prevBorder), new RnWay(nextBorder));
        }

        /// <summary>
        /// 逆転する
        /// </summary>
        public void Reverse()
        {
            (Next, Prev) = (Prev, Next);
            foreach (var lane in mainLanes)
                lane.Reverse();
            mainLanes.Reverse();
        }

        /// <summary>
        /// レーンの境界線の向きをそろえる
        /// </summary>
        /// <param name="borderDir"></param>
        public void AlignLaneBorder(RnLaneBorderDir borderDir = RnLaneBorderDir.Left2Right)
        {
            foreach (var lane in mainLanes)
                lane.AlignBorder(borderDir);
        }

        public override IEnumerable<RnRoadBase> GetNeighborRoads()
        {
            if (Next != null)
                yield return Next;
            if (Prev != null)
                yield return Prev;
        }

        /// <summary>
        /// #TODO : 左右の隣接情報がないので要修正
        /// laneを追加する. ParentLink情報も更新する
        /// </summary>
        /// <param name="lane"></param>
        public void AddMainLane(RnLane lane)
        {
            if (mainLanes.Contains(lane))
                return;
            OnAddLane(lane);
            mainLanes.Add(lane);
        }

        /// <summary>
        /// laneを削除するParentLink情報も更新する
        /// </summary>
        /// <param name="lane"></param>
        public void RemoveLane(RnLane lane)
        {
            if (mainLanes.Remove(lane))
            {
                OnRemoveLane(lane);
            }
        }

        public void ReplaceLane(RnLane before, RnLane after)
        {
            RnEx.ReplaceLane(mainLanes, before, after);
        }

        public void ReplaceLanes(IEnumerable<RnLane> newLanes)
        {
            while (mainLanes.Count > 0)
                RemoveLane(mainLanes[0]);

            foreach (var lane in newLanes)
                AddMainLane(lane);
        }

        /// <summary>
        /// 中央分離帯を入れ替える
        /// </summary>
        /// <param name="lane"></param>
        public void ReplaceMedianLane(RnLane lane)
        {
            RemoveLane(lane);
            medianLane = lane;
            OnAddLane(lane);
        }

        public void ReplaceLane(RnLane before, IEnumerable<RnLane> newLanes)
        {
            var index = mainLanes.IndexOf(before);
            if (index < 0)
                return;
            var lanes = newLanes.ToList();
            mainLanes.InsertRange(index, lanes);
            foreach (var lane in lanes)
                OnRemoveLane(lane);
            RemoveLane(before);
        }

        private void OnAddLane(RnLane lane)
        {
            if (lane == null)
                return;
            lane.Parent = this;
        }

        private void OnRemoveLane(RnLane lane)
        {
            if (lane == null)
                return;
            if (lane.Parent == this)
                lane.Parent = null;
        }

        /// <summary>
        /// Factoryからのみ呼ぶ. Prev/Nextの更新
        /// </summary>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        public void SetPrevNext(RnRoadBase prev, RnRoadBase next)
        {
            Prev = prev;
            Next = next;
        }

        // ---------------
        // Static Methods
        // ---------------
        /// <summary>
        /// 完全に孤立したリンクを作成
        /// </summary>
        /// <param name="targetTran"></param>
        /// <param name="way"></param>
        /// <returns></returns>
        public static RnLink CreateIsolatedLink(PLATEAUCityObjectGroup targetTran, RnWay way)
        {
            var lane = RnLane.CreateOneWayLane(way);
            var ret = new RnLink(targetTran);
            ret.AddMainLane(lane);
            return ret;
        }

        public static RnLink CreateOneLaneLink(PLATEAUCityObjectGroup targetTran, RnLane lane)
        {
            var ret = new RnLink(targetTran);
            ret.AddMainLane(lane);
            return ret;
        }
    }

    public static class RnLinkEx
    {
        /// <summary>
        /// laneの向きがLinkの進行方向と逆かどうか(左車線/右車線の判断に使う)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="lane"></param>
        /// <returns></returns>
        public static bool IsReverseLane(this RnLink self, RnLane lane)
        {
            if (lane.Parent != self)
                return false;

            return lane.GetNextLanes().Any(a => a.Parent == self.Next);
        }

        /// <summary>
        /// selfのPrev/Nextのうち, otherじゃない方を返す.
        /// 両方ともotherじゃない場合は例外を投げる
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        public static RnRoadBase GetOppositeLink(this RnLink self, RnRoadBase other)
        {
            if (self.Prev == other)
            {
                return self.Next == other ? null : self.Next;
            }
            if (self.Next == other)
            {
                return self.Prev == other ? null : self.Prev;
            }

            throw new InvalidDataException($"{self.DebugMyId} is not linked {other.DebugMyId}");
        }

        /// <summary>
        /// selfと隣接しているLinkをすべてまとめたLinkGroupを返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RnLinkGroup CreateLinkGroup(this RnLink self)
        {
            var links = new List<RnLink> { self };
            RnNode Search(RnRoadBase src, RnRoadBase target, bool isPrev)
            {
                while (target is RnLink link)
                {
                    // ループしていたら終了
                    if (links.Contains(link))
                        break;
                    if (isPrev)
                        links.Insert(0, link);
                    else
                        links.Add(link);
                    // linkの接続先でselfじゃない方
                    target = link.GetOppositeLink(src);

                    src = link;
                }
                return target as RnNode;
            }
            var prevNode = Search(self, self.Prev, true);
            var nextNode = Search(self, self.Next, false);
            return new RnLinkGroup(prevNode, nextNode, links);
        }

        /// <summary>
        /// selfと隣接しているLinkをすべてまとめたLinkGroupを返す.
        /// 返せない場合はnullを返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RnLinkGroup CreateLinkGroupOrDefault(this RnLink self)
        {
            try
            {
                return CreateLinkGroup(self);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// selfの全頂点の重心を返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector3 GetCenter(this RnRoadBase self)
        {
            var a = self.AllLanes.Select(l => l.GetCenter()).Aggregate(new { sum = Vector3.zero, i = 0 }, (a, p) => new { sum = a.sum + p, i = a.i + 1 });
            if (a.i == 0)
                return Vector3.zero;
            return a.sum / a.i;
        }
    }
}