using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Structure
{
    public partial class RnRoad : RnRoadBase
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------
        // 接続先(nullの場合は接続なし)
        public RnRoadBase Next { get; private set; }

        // 接続元(nullの場合は接続なし)
        public RnRoadBase Prev { get; private set; }

        // レーンリスト
        // 車線レーンリスト(参照のみ)
        // 必ず左車線 -> 右車線の順番になっている( そうなるように追加する必要がある)
        private List<RnLane> mainLanes = new List<RnLane>();

        // 中央分離帯
        private RnLane medianLane;

        //----------------------------------
        // end: フィールド
        //----------------------------------

        // 車線レーンリスト(参照のみ)
        // 必ず左車線 -> 右車線の順番になっている( そうなるように追加する必要がある)
        // 追加/削除はAddMainLane/RemoveMainLaneを使うこと
        public IReadOnlyList<RnLane> MainLanes => mainLanes;

        // 全レーン
        public IEnumerable<RnLane> AllLanes => MainLanes;

        /// <summary>
        /// 中央分離帯を含めた全てのレーン。
        /// 左車線 -> 中央分離帯 -> 右車線の順番になっている
        /// </summary>
        public IEnumerable<RnLane> AllLanesWithMedian
        {
            get
            {
                // このイテレータを回している途中でlane.IsReversedが変わると困るので
                // 最初にカウントを取ってからにする
                // mainLanesの前半がIsLeftLane, 後半がIsRightLaneである前提の挙動
                var leftLaneCount = GetLeftLaneCount();
                for (var i = 0; i < leftLaneCount; ++i)
                {
                    yield return mainLanes[i];
                }

                if (MedianLane != null)
                    yield return MedianLane;

                for (var i = leftLaneCount; i < mainLanes.Count; ++i)
                {
                    yield return mainLanes[i];
                }
            }
        }

        // 有効なRoadかどうか
        public bool IsValid => MainLanes.Any() && MainLanes.All(l => l.HasBothBorder);

        // 全てのレーンに両方向に接続先がある
        public bool IsAllBothConnectedLane => MainLanes.Any() && MainLanes.All(l => l.IsBothConnectedLane);

        /// <summary>
        /// 全レーンがIsValidかどうかチェックする
        /// </summary>
        public bool IsAllLaneValid => AllLanesWithMedian.All(l => l.IsValidWay);


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

        public RnRoad() { }

        public RnRoad(PLATEAUCityObjectGroup targetTran)
        {
            AddTargetTran(targetTran);
        }

        public RnRoad(IEnumerable<PLATEAUCityObjectGroup> targetTrans)
        {
            foreach (var targetTran in targetTrans)
                AddTargetTran(targetTran);
        }

        /// <summary>
        /// 交差点間に挿入された空Roadかどうか
        /// </summary>
        /// <returns></returns>
        public bool IsEmptyRoad
        {
            get
            {
                return Prev is RnIntersection && Next is RnIntersection && AllLanes.All(l => l.IsEmptyLane);
            }
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
        /// laneがこのRoadの左車線かどうか(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <param name="lane"></param>
        /// <returns></returns>
        public bool IsLeftLane(RnLane lane)
        {
            return lane.IsReverse == false;
        }

        /// <summary>
        /// laneがこのRoadの右車線かどうか(Prev/NextとBorderが共通である前提)
        /// </summary>
        /// <param name="lane"></param>
        /// <returns></returns>
        public bool IsRightLane(RnLane lane)
        {
            return lane.IsReverse == true;
        }

        /// <summary>
        /// レーンの方向を見る
        /// </summary>
        /// <param name="lane"></param>
        /// <returns></returns>
        private RnDir GetLaneDir(RnLane lane)
        {
            return IsLeftLane(lane) ? RnDir.Left : RnDir.Right;
        }

        // 境界線情報を取得
        public override IEnumerable<NeighborBorder> GetBorders()
        {
            foreach (var borderType in new[] { RnLaneBorderType.Prev, RnLaneBorderType.Next })
            {
                var neighbor = this.GetNeighborRoad(borderType);

                foreach (var b in GetBorderWays(borderType))
                {
                    yield return new NeighborBorder
                    {
                        NeighborRoad = neighbor,
                        BorderWay = b
                    };
                }
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
        /// 直接呼ぶの禁止. RnRoadGroupから呼ばれる
        /// </summary>
        /// <param name="lane"></param>
        public void SetMedianLane(RnLane lane)
        {
            // 以前の中央分離帯から親情報を削除
            OnRemoveLane(medianLane);
            medianLane = lane;
            OnAddLane(medianLane);
        }

        /// <summary>
        /// dirで指定した側の全レーンのBorderを統合した一つの大きなBorderを返す
        /// WayはLeft -> Right方向になっている
        /// dir == nullの時は中央分離帯含む全レーン
        /// </summary>
        /// <param name="type"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public RnWay GetMergedBorder(RnLaneBorderType type, RnDir? dir = null)
        {
            var ret = new RnLineString();
            foreach (var l in dir == null ? AllLanesWithMedian : MainLanes.Where(l => GetLaneDir(l) == dir))
            {
                RnWay way = GetBorderWay(l, type, RnLaneBorderDir.Left2Right);
                if (way == null)
                    continue;
                foreach (var p in way.Points)
                    ret.AddPointOrSkip(p);
            }
            return new RnWay(ret);
        }

        /// <summary>
        /// laneの車線に対して, RnRoad基準におけるborderTypeで指定した境界線を取ってくる
        /// (laneのPrev/Nextをそのまま持ってくるわけではないことに注意)
        /// この時境界線の方向はborderDirで指定する
        /// </summary>
        /// <param name="lane"></param>
        /// <param name="borderType"></param>
        /// <param name="borderDir"></param>
        /// <returns></returns>
        public RnWay GetBorderWay(RnLane lane, RnLaneBorderType borderType, RnLaneBorderDir borderDir)
        {
            if (lane.Parent != this)
            {
                DebugEx.LogWarning("自身の子レーンじゃないレーンに対してGetBorderWayが呼ばれました");
                return null;
            }

            if (IsLeftLane(lane) == false)
            {
                borderType = borderType.GetOpposite();
                borderDir = borderDir.GetOpposite();
            }

            var way = lane.GetBorder(borderType);
            if (way == null)
                return null;
            if (lane.GetBorderDir(borderType) != borderDir)
                way = way.ReversedWay();
            return way;
        }

        /// <summary>
        /// 境界線の一覧を取得する. left->rightの順番
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public IEnumerable<RnWay> GetBorderWays(RnLaneBorderType type)
        {
            // 左 -> 中央分離帯 -> 右の順番
            foreach (var l in AllLanesWithMedian)
            {
                var laneBorderType = type;
                var laneBorderDir = RnLaneBorderDir.Left2Right;
                if (IsLeftLane(l) == false)
                {
                    laneBorderType = laneBorderType.GetOpposite();
                    laneBorderDir = laneBorderDir.GetOpposite();
                }
                var way = l.GetBorder(laneBorderType);
                if (way == null)
                    continue;
                if (l.GetBorderDir(laneBorderType) != laneBorderDir)
                    way = way.ReversedWay();
                yield return way;
            }
        }

        /// <summary>
        /// dirで指定した側の全レーンの左右のWayを返す
        /// dir==nullの時は全レーン共通で返す
        /// 例) 左２車線でdir==RnDir.Leftの場合, 一番左の車線の左側のWayと左から２番目の車線の右側のWayを返す
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="leftWay"></param>
        /// <param name="rightWay"></param>
        /// <returns></returns>
        public bool TryGetMergedSideWay(RnDir? dir, out RnWay leftWay, out RnWay rightWay)
        {
            leftWay = rightWay = null;
            if (IsValid == false)
                return false;
            var targetLanes = MainLanes.Where(l => dir == null || GetLaneDir(l) == dir).ToList();
            if (targetLanes.Any() == false)
                return false;
            var leftLane = targetLanes[0];
            leftWay = IsLeftLane(leftLane) ? leftLane?.LeftWay : leftLane?.RightWay?.ReversedWay();
            var rightLane = targetLanes[^1];
            rightWay = IsLeftLane(rightLane) ? rightLane?.RightWay : rightLane?.LeftWay?.ReversedWay();
            return true;
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
        /// Next,Prevを逆転する.
        /// その結果, レーンのIsReverseも逆転/mainLanesの配列順も逆転する
        /// keepOneLaneIsLeftがtrueの場合, 1車線しか無い道路だとその1車線がRoadのPrev/Nextを同じ方向になるように(左車線扱い)する
        /// </summary>
        /// <param name="keepOneLaneIsLeft"></param>
        public void Reverse(bool keepOneLaneIsLeft = true)
        {
            (Next, Prev) = (Prev, Next);

            // 親の向きが変わるので,レーンの親に対する向きも変わる
            // 各レーンのWayの向きは変えずにIsRevereだけ変える
            // 左車線/右車線の関係が変わるので配列の並びも逆にする
            foreach (var lane in AllLanesWithMedian)
                lane.IsReverse = !lane.IsReverse;
            mainLanes.Reverse();

            // 歩道の設定も逆にする(左車線/右車線の関係が変わるので)
            foreach (var sw in SideWalks)
            {
                sw.ReverseLaneType();
            }

            // １車線道路でかつその道路が右車線扱いになったら, 左車線になるようにレーンを反転させる
            if (keepOneLaneIsLeft)
            {
                if (AllLanesWithMedian.Count() == 1 && GetLeftLaneCount() == 0)
                {
                    foreach (var lane in AllLanesWithMedian)
                        lane.Reverse();

                    foreach (var sw in SideWalks)
                        sw.ReverseLaneType();
                }
            }
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
        /// laneを追加する. ParentRoad情報も更新する
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
        /// laneを追加. indexで指定した位置に追加する
        /// </summary>
        /// <param name="lane"></param>
        /// <param name="index"></param>
        public void AddMainLane(RnLane lane, int index)
        {
            if (mainLanes.Contains(lane))
                return;
            OnAddLane(lane);
            mainLanes.Insert(index, lane);
        }

        /// <summary>
        /// laneを削除するParentRoad情報も更新する
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

        public void ReplaceLanes(IEnumerable<RnLane> newLanes, RnDir dir)
        {
            foreach (var l in GetLanes(dir).ToList())
            {
                RemoveLane(l);
            }
            // Leftは先頭に追加
            if (dir == RnDir.Left)
            {
                var i = 0;
                foreach (var l in newLanes)
                {
                    AddMainLane(l, i);
                    i++;
                }
            }
            // Rightは末尾に追加
            else
            {
                foreach (var l in newLanes)
                {
                    AddMainLane(l);
                }
            }
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

        /// <summary>
        /// 必ず 境界線の間に輪郭線が来るように少し移動させて間に微小なEdgeを追加する
        /// </summary>
        public void SeparateContinuousBorder()
        {
            // aとbが接続しているかどうか
            static bool IsConnected(RnWay a, RnWay b, out RnPoint newA, out RnPoint jointPoint, out RnPoint newB)
            {
                newA = newB = jointPoint = null;
                var d = new[] { 0, -1 };
                foreach (var d1 in d)
                {
                    foreach (var d2 in d)
                    {
                        // 端点が繋がっているかどうかチェックする
                        if (a.GetPoint(d1) == b.GetPoint(d2))
                        {
                            var offset = 0.01f;
                            newA = new RnPoint(a.GetAdvancedPoint(offset, d1 == -1));
                            newB = new RnPoint(b.GetAdvancedPoint(offset, d2 == -1));
                            jointPoint = a.GetPoint(d1);
                            return true;
                        }
                    }
                }
                return false;
            }

            static void Check(RnLane lane)
            {
                // 境界線がない場合は何もしない
                if (lane.PrevBorder == null || lane.NextBorder == null)
                    return;

                if (IsConnected(lane.PrevBorder, lane.NextBorder, out var newA, out var jointPoint, out var newB) == false)
                    return;

                var newLs = new RnLineString(new[] { newA, jointPoint, newB });
                var prevRoad = lane.GetPrevRoad();
                var nextRoad = lane.GetNextRoad();
                lane.PrevBorder.LineString.ReplacePoint(jointPoint, newA);
                lane.NextBorder.LineString.ReplacePoint(jointPoint, newB);
                foreach (var ls in prevRoad.GetAllLineStringsDistinct())
                    ls.ReplacePoint(jointPoint, newA);
                foreach (var ls in nextRoad.GetAllLineStringsDistinct())
                    ls.ReplacePoint(jointPoint, newB);

                if (lane.LeftWay == null)
                    lane.SetSideWay(RnDir.Left, new RnWay(newLs, false, false));
                else if (lane.RightWay == null)
                    lane.SetSideWay(RnDir.Right, new RnWay(newLs, false, true));
            }

            foreach (var lane in AllLanesWithMedian)
                Check(lane);
        }
        /// <summary>
        /// 接続を解除する
        /// </summary>
        public override void DisConnect(bool removeFromModel)
        {
            base.DisConnect(removeFromModel);
            Prev?.UnLink(this);
            Next?.UnLink(this);
            SetPrevNext(null, null);
            if (removeFromModel)
            {
                ParentModel?.RemoveRoad(this);
            }

            foreach (var lane in mainLanes)
            {
                lane.DisConnectBorder();
            }
        }

        /// <summary>
        /// 情報を直接書き換えるので呼び出し注意(相互に隣接情報を維持するように書き換える必要がある)
        /// 隣接情報をfrom -> toに変更する.
        /// (from/to側の隣接情報は変更しない)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public override void ReplaceNeighbor(RnRoadBase from, RnRoadBase to)
        {
            if (from == null)
                return;
            if (Prev == from)
                Prev = to;
            if (Next == from)
                Next = to;
        }

        /// <summary>
        /// デバッグ用) その道路の中心を表す代表頂点を返す
        /// </summary>
        /// <returns></returns>
        public override Vector3 GetCentralVertex()
        {
            return Vector3Ex.Centroid(this.GetMergedSideWays()
                .Select(w => w.GetLerpPoint(0.5f))
                );
        }


        /// <summary>
        /// 所属するすべてのWayを取得
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<RnWay> AllWays()
        {
            foreach (var s in AllLanesWithMedian)
            {
                foreach (var w in s.AllWays)
                {
                    yield return w;
                }
            }

            foreach (var w in base.AllWays())
            {
                yield return w;
            }
        }


        /// <summary>
        /// Lane内のWayの内、最も左のWayを取得
        /// </summary>
        /// <returns></returns>
        public RnWay GetLeftWayOfLanes()
        {
            var lane = MainLanes.First();
            return IsLeftLane(lane) ? lane?.LeftWay : lane?.RightWay;
        }

        /// <summary>
        /// Lane内のWayの内、最も右のWayを取得
        /// </summary>
        /// <returns></returns>
        public RnWay GetRightWayOfLanes()
        {
            var lane = MainLanes.Last();
            return IsLeftLane(lane) ? lane?.RightWay : lane?.LeftWay;
        }


        public override bool Check()
        {

            foreach (var BorderType in new[] { RnLaneBorderType.Prev, RnLaneBorderType.Next })
            {
                if (!this.IsValidBorderAdjacentNeighbor(BorderType, true))
                    return false;
            }

            return true;
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
        public static RnRoad CreateIsolatedRoad(PLATEAUCityObjectGroup targetTran, RnWay way)
        {
            var lane = RnLane.CreateOneWayLane(way);
            var ret = new RnRoad(targetTran);
            ret.AddMainLane(lane);
            return ret;
        }

        public static RnRoad CreateOneLaneRoad(PLATEAUCityObjectGroup targetTran, RnLane lane)
        {
            var ret = new RnRoad(targetTran);
            ret.AddMainLane(lane);
            return ret;
        }
    }

    public static class RnRoadEx
    {
        /// <summary>
        /// selfのPrev/Nextのうち, otherじゃない方を返す.
        /// 両方ともotherじゃない場合は例外を投げる
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        public static RnRoadBase GetOppositeRoad(this RnRoad self, RnRoadBase other)
        {
            if (self.Prev == other)
            {
                return self.Next == other ? null : self.Next;
            }
            if (self.Next == other)
            {
                return self.Prev == other ? null : self.Prev;
            }

            throw new InvalidDataException($"{self.DebugMyId}/{self.GetTargetTransName()} is not neighbor road {other.DebugMyId}/{other.GetTargetTransName()}");
        }

        /// <summary>
        /// selfと隣接しているRoadをすべてまとめたRoadGroupを返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RnRoadGroup CreateRoadGroup(this RnRoad self)
        {
            var roads = new List<RnRoad> { self };
            RnIntersection Search(RnRoadBase src, RnRoadBase target, bool isPrev)
            {
                while (target is RnRoad road)
                {
                    // ループしていたら終了
                    if (roads.Contains(road))
                        break;
                    if (isPrev)
                        roads.Insert(0, road);
                    else
                        roads.Add(road);
                    // roadの接続先でselfじゃない方
                    target = road.GetOppositeRoad(src);

                    src = road;
                }
                return target as RnIntersection;
            }
            var prevIntersection = Search(self, self.Prev, true);
            var nextIntersection = Search(self, self.Next, false);
            return new RnRoadGroup(prevIntersection, nextIntersection, roads);
        }

        /// <summary>
        /// selfと隣接しているRoadをすべてまとめたRoadGroupを返す.
        /// 返せない場合はnullを返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RnRoadGroup CreateRoadGroupOrDefault(this RnRoad self)
        {
            try
            {
                return CreateRoadGroup(self);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// 道路の両端のWayを取得する. dirが指定されている場合はその方向の車線だけで絞って返す.
        /// dir = null道路全体の両端のWayを返す
        /// dir = Left(Right). 左(右)車線だけみた両端のwayを返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static IEnumerable<RnWay> GetMergedSideWays(this RnRoad self, RnDir? dir = null)
        {
            if (self.TryGetMergedSideWay(dir, out var leftWay, out var rightWay))
            {
                if (leftWay != null)
                    yield return leftWay;
                if (rightWay != null)
                    yield return rightWay;
            }
        }

        /// <summary>
        /// この境界とつながっているレーンリスト
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<RnLane> GetConnectedLanes(this RnRoad self, RnWay border)
        {
            if (self == null || border == null)
                yield break;
            foreach (var lane in self.MainLanes)
            {
                // Borderと同じ線上にあるレーンを返す
                if (lane.AllBorders.Any(b => b.IsSameLineReference(border)))
                    yield return lane;
            }
        }

        /// <summary>
        /// selfの全LinestringとlineSegmentの交点を取得する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="lineSegment"></param>
        /// <returns></returns>
        public static LineCrossPointResult GetLaneCrossPoints(this RnRoad self, LineSegment3D lineSegment)
        {
            var targetLines = self.AllLanesWithMedian
                .SelectMany(l => l.BothWays)
                .Concat(self.SideWalks.SelectMany(s => s.SideWays));
            return RnEx.GetLineIntersections(lineSegment, targetLines);
        }

        /// <summary>
        /// selfの両端のWayとlineの最も近い点との距離をdistanceに格納する
        /// その結果, lineがselfの内部を通っているときは, lineに幅を持たせてもselfのLane内をはみ出さないような最大の幅となる
        /// </summary>
        /// <param name="self"></param>
        /// <param name="line"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static bool TryGetNearestDistanceToSideWays(this RnRoad self, RnLineString line, out float distance)
        {
            if (self.TryGetMergedSideWay(null, out var leftWay, out var rightWay) == false)
            {
                distance = 0f;
                return false;
            }

            var prevBorder = self.GetMergedBorder(RnLaneBorderType.Prev, null);
            var nextBorder = self.GetMergedBorder(RnLaneBorderType.Next, null);

            distance = Mathf.Min(prevBorder.CalcLength(), nextBorder.CalcLength());

            HashSet<float> indices = new();
            foreach (var p in leftWay.Points)
            {
                line.GetNearestPoint(p.Vertex, out var v, out var index, out var _);
                indices.Add(index);
            }

            foreach (var p in rightWay.Points)
            {
                line.GetNearestPoint(p.Vertex, out var v, out var index, out var _);
                indices.Add(index);
            }

            foreach (var i in Enumerable.Range(0, line.Count))
                indices.Add(i);

            // 左右それぞれで最も小さい幅の２倍にする
            // 各点に置けるwl+wrの最小値だと、wl << wrの場合があったりすると中心線をずらす必要があるので苦肉の策
            var leftWidth = float.MaxValue;
            var rightWidth = float.MaxValue;
            foreach (var i in indices)
            {
                var v = line.GetVertexByFloatIndex(i);
                leftWay.LineString.GetNearestPoint(v, out var nl, out var il, out var wl);
                leftWidth = Mathf.Min(leftWidth, wl);
                rightWay.LineString.GetNearestPoint(v, out var nr, out var ir, out var wr);
                rightWidth = Mathf.Min(rightWidth, wr);
            }
            distance = Mathf.Min(Mathf.Min(rightWidth, leftWidth), distance);
            return true;
        }

        /// <summary>
        /// roadのMergedSideWayから計算された進行方向に垂直な境界線を取得する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="borderType"></param>
        /// <param name="adjustedBorderLeft2Right">垂直な境界線(左車線->右車線の方向)</param>
        /// <returns></returns>
        public static bool TryGetAdjustBorderSegment(this RnRoad self, RnLaneBorderType borderType, out LineSegment3D adjustedBorderLeft2Right)
        {
            adjustedBorderLeft2Right = new LineSegment3D();
            var leftWay = self.GetMergedSideWay(RnDir.Left);
            var rightWay = self.GetMergedSideWay(RnDir.Right);
            if (!leftWay.IsValidOrDefault() || !rightWay.IsValidOrDefault())
                return false;

            // Xz平面で交点を求める
            LineSegment3D leftSeg, rightSeg;
            if (borderType == RnLaneBorderType.Next)
            {
                leftSeg = new LineSegment3D(leftWay[^2], leftWay[^1]);
                rightSeg = new LineSegment3D(rightWay[^2], rightWay[^1]);
            }
            else if (borderType == RnLaneBorderType.Prev)
            {
                leftSeg = new LineSegment3D(leftWay[1], leftWay[0]);
                rightSeg = new LineSegment3D(rightWay[1], rightWay[0]);
            }
            else
            {
                throw new ArgumentException($"TryGetAdjustBorderSegment. Invalid border type ${borderType}");
            }
            var plane = RnModel.Plane;
            var leftSeg2D = leftSeg.To2D(plane);
            var rightSeg2D = rightSeg.To2D(plane);
            // leftWay/rightWayの最後の直線の2等分線に対して直角な線
            var ray2D = GeoGraph2D.LerpRay(leftSeg2D.Ray, rightSeg2D.Ray, 0.5f);
            var left2RightDir = ray2D.direction.Rotate(90f);
            if (Vector2.Dot(rightSeg2D.End - leftSeg2D.End, left2RightDir) < 0)
                left2RightDir = -left2RightDir;

            // leftWayの方が手前にある
            if (rightSeg2D.TryHalfLineIntersection(leftSeg2D.End, left2RightDir, out var rInter,
                    out var lT1, out var rSegT1))
            {
                adjustedBorderLeft2Right
                    = new LineSegment3D(leftSeg.End, rightSeg.Lerp(rSegT1));
                return true;
            }
            // rightWayの方が手前にある
            if (leftSeg2D.TryHalfLineIntersection(rightSeg2D.End, -left2RightDir, out var lInter,
                         out var rT2, out var lSegT2))
            {
                adjustedBorderLeft2Right
                    = new LineSegment3D(leftSeg.Lerp(lSegT2), rightSeg.End);
                return true;
            }
            return false;
        }

        /// <summary>
        /// borderTypeで指定した隣の道路(交差点)を取得する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="borderType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static RnRoadBase GetNeighborRoad(this RnRoad self, RnLaneBorderType borderType)
        {
            return borderType switch
            {
                RnLaneBorderType.Prev => self.Prev,
                RnLaneBorderType.Next => self.Next,
                _ => throw new ArgumentOutOfRangeException(nameof(borderType), borderType, null),
            };
        }

        /// <summary>
        /// 道路を隣の交差点にマージする
        /// </summary>
        /// <param name="self"></param>
        /// <param name="borderType"></param>
        /// <returns></returns>
        public static bool TryMerge2NeighborIntersection(this RnRoad self, RnLaneBorderType borderType)
        {
            var neighbor = self.GetNeighborRoad(borderType);
            if (neighbor is RnIntersection intersection == false)
            {
                DebugEx.LogWarning($"TryMerge2NeighborIntersection. neighbor is not intersection. {neighbor.DebugMyId}");
                return false;
            }

            self.TryGetMergedSideWay(null, out var leftWay, out var rightWay);
            var edgeGroup = intersection.CreateEdgeGroup().FirstOrDefault(e => e.Key == self);
            if (edgeGroup == null)
                return false;

            var visited = new HashSet<RnLineString>();

            void Merge(RnWay dst, RnWay src, Action<RnWay, RnWay> merger)
            {
                if (dst == null || src == null)
                    return;
                if (visited.Contains(dst.LineString))
                    return;
                visited.Add(dst.LineString);
                merger(dst, src);
            }

            var oppositeBorders = self.GetBorderWays(borderType.GetOpposite()).ToList();
            var oppositeRoadBase = self.GetNeighborRoad(borderType.GetOpposite());
            if (borderType == RnLaneBorderType.Prev)
            {
                // 前方の交差点を取得
                var rightEdge = edgeGroup.LeftSide.Edges[0];
                var leftEdge = edgeGroup.RightSide.Edges[^1];
                Merge(rightEdge?.Border, rightWay?.ReversedWay(), RnWayEx.AppendFront2LineString);
                Merge(leftEdge?.Border, leftWay, RnWayEx.AppendBack2LineString);
            }
            else if (borderType == RnLaneBorderType.Next)
            {
                // 後方の交差点を取得
                var rightEdge = edgeGroup.RightSide.Edges[^1];
                var leftEdge = edgeGroup.LeftSide.Edges[0];
                Merge(rightEdge?.Border, rightWay?.ReversedWay(), RnWayEx.AppendBack2LineString);
                Merge(leftEdge?.Border, leftWay, RnWayEx.AppendFront2LineString);
            }

            // intersectionの輪郭情報を差し替える
            intersection.ReplaceEdges(self, oppositeBorders, false);

            // selfの隣接道路に対して, selfに対する接続情報を置き換える
            intersection.ReplaceNeighbor(self, oppositeRoadBase);
            oppositeRoadBase?.ReplaceNeighbor(self, intersection);

            // トラックを生成しなおす
            intersection.BuildTracks(BuildTrackOption.WithBorder(oppositeBorders.Select(x => x.LineString).ToHashSet()));

            var dstSideWalks = intersection.SideWalks.ToList();
            var srcSideWalks = self.SideWalks.ToList();

            // SideWalksと共通のLineStringがあるとき, レーン側は統合されるけど
            // SideWalksは統合されない場合もある. その時はLineStringを分離する必要があるので
            // 元のLineStringをコピーして持っておく
            var originalDstSideWalks = dstSideWalks.ToList();
            var original = dstSideWalks
                .SelectMany(sw => sw.SideWays)
                .ToHashSet()
                .ToDictionary(x => x, x => x.Clone(false));

            HashSet<RnSideWalk> mergedDstSideWalks = new();
            foreach (var srcSw in srcSideWalks)
            {
                var found = false;
                foreach (var dstSw in dstSideWalks)
                {
                    void Merge2(RnWay dst, RnWay src, Action<RnWay, RnWay> merger)
                    {
                        if (dst == null || src == null)
                            return;
                        if (visited.Contains(dst.LineString))
                            return;
                        visited.Add(dst.LineString);

                        var tolerance = 0f;
                        if (dst.TryMergePointsToLineString(src, tolerance) == false)
                        {
                            // #NOTE : もともとの歩道がきれいにつながっていない場合は仮で直接つなぐようにする
                            DebugEx.LogWarning($"共通頂点を持たないWayをマージしようとしました. {dst.GetDebugIdLabelOrDefault()} {src.GetDebugIdLabelOrDefault()}");
                            //DebugEx.DrawArrows(dst, false, color: Color.red, duration: 100);
                            //DebugEx.DrawArrows(src, false, color: Color.blue, duration: 100);
                            merger(dst, src);
                        }
                    }

                    void MergeSideWalk(bool reverse, Action<RnWay, RnWay> merger)
                    {
                        var insideWay = reverse ? srcSw.InsideWay?.ReversedWay() : srcSw.InsideWay;
                        var outsideWay = reverse ? srcSw.OutsideWay?.ReversedWay() : srcSw.OutsideWay;

                        Merge2(dstSw.InsideWay, insideWay, merger);
                        Merge2(dstSw.OutsideWay, outsideWay, merger);
                        // もともとnullだった場合は置き換える
                        dstSw.SetSideWays(dstSw.OutsideWay ?? outsideWay, dstSw.InsideWay ?? insideWay);
                        mergedDstSideWalks.Add(dstSw);
                        found = true;
                    }

                    // start - startで重なっている場合
                    if (dstSw.StartEdgeWay?.IsSameLineReference(srcSw.StartEdgeWay) ?? false)
                    {
                        MergeSideWalk(true, RnWayEx.AppendFront2LineString);
                        dstSw.SetStartEdgeWay(srcSw.EndEdgeWay);
                    }
                    // start - endで重なっている場合
                    else if (dstSw.StartEdgeWay?.IsSameLineReference(srcSw.EndEdgeWay) ?? false)
                    {
                        MergeSideWalk(false, RnWayEx.AppendFront2LineString);
                        dstSw.SetStartEdgeWay(srcSw.StartEdgeWay);
                    }
                    // end - endで重なっている場合
                    else if (dstSw.EndEdgeWay?.IsSameLineReference(srcSw.EndEdgeWay) ?? false)
                    {
                        MergeSideWalk(true, RnWayEx.AppendBack2LineString);
                        dstSw.SetEndEdgeWay(srcSw.StartEdgeWay);
                    }
                    // end - startで重なっている場合
                    else if (dstSw.EndEdgeWay?.IsSameLineReference(srcSw.StartEdgeWay) ?? false)
                    {
                        MergeSideWalk(false, RnWayEx.AppendBack2LineString);
                        dstSw.SetEndEdgeWay(srcSw.EndEdgeWay);
                    }

                    if (found)
                        break;
                }

                // マージできなかった歩道は直接追加
                if (found == false)
                {
                    intersection.AddSideWalk(srcSw);
                    dstSideWalks.Add(srcSw);
                }
            }

            // dstSideWalksの中でマージされなかった(元の形状から変更されない)ものは
            // レーンと共通のLineStringを持っている場合に勝手に形状変わっているかもしれないので明示的に元に戻す
            foreach (var sw in originalDstSideWalks
                         .Where(d => mergedDstSideWalks.Contains(d) == false))
            {
                sw.SetSideWays(
                    sw.OutsideWay == null ? null : original[sw.OutsideWay]
                    , sw.InsideWay == null ? null : original[sw.InsideWay]);
            }

            intersection.AddTargetTrans(self.TargetTrans);
            self.DisConnect(true);
            return true;
        }

        /// <summary>
        /// selfのborderSide側からborderOffsetMeterだけ離れた位置に道路を垂直に分割する線分を計算する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="borderSide"></param>
        /// <param name="borderOffsetMeter"></param>
        /// <param name="segment"></param>
        /// <returns></returns>
        public static bool TryGetVerticalSliceSegment(this RnRoad self, RnLaneBorderType borderSide,
            float borderOffsetMeter, out LineSegment3D segment)
        {
            segment = new LineSegment3D();
            if (self.TryGetMergedSideWay(null, out var leftWay, out var rightWay) == false)
                return false;

            var prevBorder = self.GetMergedBorder(RnLaneBorderType.Prev);
            var nextBorder = self.GetMergedBorder(RnLaneBorderType.Next);
            var st = prevBorder.GetLerpPoint(0.5f);
            var en = nextBorder.GetLerpPoint(0.5f);
            var vertices = RnEx.CreateInnerLerpLineString(
                leftWay.Vertices.ToList()
                , rightWay.Vertices.ToList()
                , new RnPoint(st)
                , new RnPoint(en)
                , prevBorder
                , nextBorder
                , 0.5f);

            var centerWay = new RnWay(RnLineString.Create(vertices));
            var startIndex = 0;
            var endIndex = 0;
            var v = Vector3.zero;

            var border = borderSide == RnLaneBorderType.Prev ? prevBorder : nextBorder;

            // 道路の両隣
            // 境界線の斜めがきつい時のため
            // 垂直線と境界線が交わらないように多めにborderOffsetMeterを取る
            List<Vector3> checkBorderPoints = new() { border[0], border[^1] };
            var neighborRoad = self.GetNeighborRoad(borderSide);
            if (neighborRoad != null)
            {
                foreach (var sw in self.SideWalks)
                {
                    foreach (var w in sw.AllWays)
                    {
                        if (neighborRoad.SideWalks.Any(x => x.AllWays.Any(y => y.IsSameLineReference(w))))
                        {
                            checkBorderPoints.Add(w[0]);
                            checkBorderPoints.Add(w[^1]);
                        }
                    }
                }
            }

            foreach (var p in checkBorderPoints)
            {
                centerWay.GetNearestPoint(p, out var nearest0, out float index, out float _);

                var len = borderSide == RnLaneBorderType.Prev
                    ? centerWay.CalcLength(0, index)
                    : centerWay.CalcLength(index, centerWay.Count - 1);

                // 0.5mは余白分として入れる
                borderOffsetMeter = Mathf.Max(borderOffsetMeter, len + 2.5f);
            }

            if (borderSide == RnLaneBorderType.Next)
            {
                v = centerWay.GetAdvancedPointFromBack(borderOffsetMeter, out startIndex, out endIndex);
            }
            else if (borderSide == RnLaneBorderType.Prev)
            {
                v = centerWay.GetAdvancedPointFromFront(borderOffsetMeter, out startIndex, out endIndex);
            }
            else
            {
                return false;
            }
            var dir = (centerWay[endIndex] - centerWay[startIndex]).normalized;
            dir = Quaternion.AngleAxis(90, Vector3.up) * dir;
            var ray = new Ray(v, dir);

            if (leftWay.LineString.TryGetNearestIntersectionBy2D(ray, out var leftRes) == false)
                return false;
            if (rightWay.LineString.TryGetNearestIntersectionBy2D(ray, out var rightRes) == false)
                return false;

            var d = (leftRes.v - ray.origin).normalized;
            segment = new LineSegment3D(leftRes.v + d * 20, rightRes.v - d * 20);
            return true;
        }

        /// <summary>
        /// 境界線情報が
        /// </summary>
        /// <param name="self"></param>
        /// <param name="borderType"></param>
        /// <param name="noBorderIsTrue"></param>
        /// <returns></returns>
        public static bool IsValidBorderAdjacentNeighbor(this RnRoad self, RnLaneBorderType borderType, bool noBorderIsTrue)
        {
            if (self == null)
                return false;
            var neighbor = self.GetNeighborRoad(borderType);
            if (neighbor == null)
                return noBorderIsTrue;
            var ways = neighbor.GetBorders()?.Select(x => x.BorderWay)?.ToList() ?? new List<RnWay>();

            var oppositeBorderType = borderType.GetOpposite();
            foreach (var lane in self.MainLanes)
            {
                var laneBorder = self.GetBorderWay(lane, borderType, RnLaneBorderDir.Left2Right);
                if (laneBorder == null)
                {
                    if (noBorderIsTrue)
                        continue;
                    return false;
                }

                var oppositeLaneBorder = self.GetBorderWay(lane, oppositeBorderType, RnLaneBorderDir.Left2Right);
                // 隣接する道路に共通の境界線を持たない場合はfalse
                if (!ways.Any(w => w.IsSameLineReference(laneBorder)))
                {
                    // 反対側のボーダーが境界線だったらアウト
                    var isRev = ways.Any(w => w.IsSameLineReference(oppositeLaneBorder));
                    if (!noBorderIsTrue || isRev)
                        return false;
                }
            }
            return true;
        }
    }

}