using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace PLATEAU.RoadNetwork.Structure
{
    /// <summary>
    /// 実際にデータ化されるものではない
    /// Intersection -> Intersectionを繋ぐ複数のRoadをまとめるクラス
    /// </summary>
    [Serializable]
    public class RnRoadGroup
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------
        /// <summary>
        /// 開始ノード
        /// </summary>
        [field: SerializeField]
        public RnIntersection PrevIntersection { get; private set; }

        /// <summary>
        /// 終了ノード
        /// </summary>
        [field: SerializeField]
        public RnIntersection NextIntersection { get; private set; }

        [SerializeField]
        private List<RnRoad> roads;

        //----------------------------------
        // end: フィールド
        //----------------------------------

        public IReadOnlyList<RnRoad> Roads => roads;

        /// <summary>
        /// 有効なRoadGroupかどうか
        /// </summary>
        public bool IsValid => Roads.All(l => l.IsValid);

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="prevIntersection"></param>
        /// <param name="nextIntersection"></param>
        /// <param name="roads"></param>
        public RnRoadGroup(RnIntersection prevIntersection, RnIntersection nextIntersection, IEnumerable<RnRoad> roads)
        {
            PrevIntersection = prevIntersection;
            NextIntersection = nextIntersection;
            this.roads = roads.ToList();
            // Roadの向きをそろえる
            Align();
        }

        /// <summary>
        /// 左側のレーン数
        /// </summary>
        /// <returns></returns>
        public int GetLeftLaneCount()
        {
            // 向きをそろえる
            Align();
            if (Roads.Any() == false)
                return 0;

            return roads.Select(l => l.GetLeftLaneCount()).Min();
        }

        /// <summary>
        /// 右側レーン数
        /// </summary>
        /// <returns></returns>
        public int GetRightLaneCount()
        {
            // 向きをそろえる
            Align();
            if (Roads.Any() == false)
                return 0;
            return roads.Select(l => l.GetRightLaneCount()).Min();
        }

        public IEnumerable<RnLane> GetRightLanes()
        {
            // 向きをそろえる
            Align();
            if (Roads.Any() == false)
                return null;
            return roads.SelectMany(l => l.GetRightLanes());
        }

        public IEnumerable<RnLane> GetLeftLanes()
        {
            // 向きをそろえる
            Align();
            if (Roads.Any() == false)
                return null;
            return roads.SelectMany(l => l.GetLeftLanes());
        }

        /// <summary>
        /// RnDirで指定した側のレーンを取得する
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public IEnumerable<RnLane> GetLanes(RnDir dir)
        {
            return dir switch
            {
                RnDir.Left => GetLeftLanes(),
                RnDir.Right => GetRightLanes(),
                _ => throw new ArgumentOutOfRangeException($"GetLanes ({dir})")
            };
        }

        private RnWay ConnectWays(IEnumerable<RnWay> ways)
        {
            var points = new List<RnPoint>();
            foreach (var way in ways)
            {
                foreach (var p in way.Points)
                {
                    if (points.Any() && points.Last() == p)
                        continue;
                    points.Add(p);
                }
            }

            return new RnWay(RnLineString.Create(points), false, false);
        }

        /// <summary>
        /// dirで指定した側のレーンをnum個に分割する.
        /// </summary>
        /// <param name="num"></param>
        /// <param name="dir"></param>
        /// <param name="getSplitRate"></param>
        /// <returns></returns>
        private Dictionary<RnRoad, List<RnLane>> SplitLane(int num, RnDir? dir, Func<int, float> getSplitRate = null)
        {
            if (num <= 0)
                return null;

            var mergedBorders = Roads.Select(l => l.GetMergedBorder(RnLaneBorderType.Prev, dir)).ToList();
            mergedBorders.Add(Roads[^1].GetMergedBorder(RnLaneBorderType.Next, dir));

            var borderWays = new List<List<RnWay>>(Roads.Count + 1);

            foreach (var b in mergedBorders)
            {
                var split = b.Split(num, false, getSplitRate);
                borderWays.Add(split);
            }

            var afterLanes = new Dictionary<RnRoad, List<RnLane>>(Roads.Count);
            for (var i = 0; i < Roads.Count; ++i)
            {
                var road = Roads[i];
                var prevBorders = borderWays[i];
                var nextBorders = borderWays[i + 1];

                road.TryGetMergedSideWay(dir, out var leftWay, out var rightWay);

                var leftVertices = leftWay.Vertices.ToList();
                var rightVertices = rightWay.Vertices.ToList();
                var left = leftWay;
                var lanes = new List<RnLane>(num);
                var rate = 0f;
                for (var n = 0; n < num; ++n)
                {
                    var right = rightWay;
                    if (n < num - 1)
                    {
                        rate += getSplitRate?.Invoke(n) ?? (1f / num);
                        var prevBorder = prevBorders[n];
                        var nextBorder = nextBorders[n];
                        var line = RnEx.CreateInnerLerpLineString(
                            leftVertices
                            , rightVertices
                            , prevBorder.GetPoint(-1)
                            , nextBorder.GetPoint(-1)
                            , prevBorder
                            , nextBorder,
                            rate);

                        right = new RnWay(line, false, true);
                    }

                    var l = new RnWay(left.LineString, left.IsReversed, false);
                    var r = new RnWay(right.LineString, right.IsReversed, true);
                    var newLane = new RnLane(l, r, prevBorders[n], nextBorders[n]);
                    lanes.Add(newLane);
                    left = right;
                }

                afterLanes[road] = lanes;
            }

            return afterLanes;
        }

        /// <summary>
        /// レーン分割する
        /// </summary>
        /// <param name="count"></param>
        /// <param name="dir"></param>
        /// <param name="rebuildTrack">分割後に対象のレーンに紐づくトラックをリビルドする</param>
        private void SetLaneCountImpl(int count, RnDir dir, bool rebuildTrack)
        {
            if (IsValid == false)
                return;

            // 既に指定の数になっている場合は何もしない
            if (Roads.All(l => l.GetLanes(dir).Count() == count))
                return;
            // 向きをそろえる
            Align();

            var afterLanes = SplitLane(count, dir);
            if (afterLanes == null)
                return;

            // 最後に隣接する交差点のトラックの再生成を行うためのキャッシュ
            // 隣接情報が確定した後でトラック生成を行う必要があるので、ここでキャッシュしておく
            HashSet<RnLineString> newNextBorders = new();
            HashSet<RnLineString> newPrevBorders = new();
            // Roadsに変更を加えるのは最後にまとめて必要がある
            // (RnRoads.IsLeftLane等が隣のRoadに依存するため. 途中で変更すると、後続の処理が破綻する可能性がある)
            for (var i = 0; i < Roads.Count; ++i)
            {
                var road = Roads[i];
                var lanes = afterLanes[road];

                var beforeLanes = road.GetLanes(dir).ToList();
                if (i == Roads.Count - 1 && NextIntersection != null)
                {
                    foreach (var l in beforeLanes)
                        NextIntersection.RemoveEdge(road, l);
                    foreach (var l in lanes)
                    {
                        NextIntersection.AddEdge(road, l.NextBorder);
                        newNextBorders.Add(l.NextBorder.LineString);
                    }

                }
                if (i == 0 && PrevIntersection != null)
                {
                    foreach (var l in beforeLanes)
                        PrevIntersection.RemoveEdge(road, l);
                    foreach (var l in lanes)
                    {
                        PrevIntersection.AddEdge(road, l.PrevBorder);
                        newPrevBorders.Add(l.PrevBorder.LineString);
                    }
                }

                // 右車線の場合は反対にする
                // #NOTE : 隣接情報変更後に反転させる
                if (dir == RnDir.Right)
                {
                    foreach (var l in lanes)
                        l.Reverse();
                }

                Roads[i].ReplaceLanes(lanes, dir);
            }

            // トラックのリビルドするかチェックする
            if (rebuildTrack)
            {
                if (NextIntersection != null && newNextBorders.Any())
                    NextIntersection.BuildTracks(BuildTrackOption.WithBorder(newNextBorders));

                if (PrevIntersection != null && newPrevBorders.Any())
                    PrevIntersection.BuildTracks(BuildTrackOption.WithBorder(newPrevBorders));
            }
        }

        /// <summary>
        /// 中央分離帯を考慮したレーン分割
        /// </summary>
        /// <param name="leftCount"></param>
        /// <param name="rightCount"></param>
        /// <param name="medianWidthRate"></param>
        public void SetLaneCountWithMedian(int leftCount, int rightCount, float medianWidthRate)
        {
            if (IsValid == false)
                return;

            // 両方とも0はダメ
            if (leftCount <= 0 && rightCount <= 0)
            {
                DebugEx.LogWarning($"両方の車線数に0を入れることはできません");
                return;
            }


            // 向きをそろえる
            Align();

            var num = leftCount + rightCount + 1;
            var laneRate = (1f - medianWidthRate) / (num - 1);

            var afterLanes = SplitLane(num, null, i =>
            {
                if (i == leftCount)
                    return medianWidthRate;
                return laneRate;
            });
            //var afterLanes = SplitLane(num, null);
            if (afterLanes == null)
                return;

            // 最後に隣接する交差点のトラックの再生成を行うためのキャッシュ
            // 隣接情報が確定した後でトラック生成を行う必要があるので、ここでキャッシュしておく
            List<RnLineString> newNextBorders = new();
            List<RnLineString> newPrevBorders = new();

            // Roadsに変更を加えるのは最後にまとめて必要がある
            // (RnRoads.IsLeftLane等が隣のRoadに依存するため. 途中で変更すると、後続の処理が破綻する可能性がある)
            for (var i = 0; i < Roads.Count; ++i)
            {
                var road = Roads[i];
                var lanes = afterLanes[road];

                if (i == Roads.Count - 1 && NextIntersection != null)
                {
                    NextIntersection.ReplaceEdges(Roads[^1], lanes.Select(l => l.NextBorder).ToList());
                    newNextBorders.AddRange(lanes.Select(l => l.NextBorder.LineString));
                }

                if (i == 0 && PrevIntersection != null)
                {
                    PrevIntersection.ReplaceEdges(Roads[0], lanes.Select(l => l.PrevBorder).ToList());
                    newPrevBorders.AddRange(lanes.Select(l => l.PrevBorder.LineString));
                }

                // 右車線の場合は反対にする
                for (var j = leftCount + 1; j < lanes.Count; ++j)
                    lanes[j].Reverse();

                var median = lanes[leftCount];
                lanes.RemoveAt(leftCount);
                road.ReplaceLanes(lanes);
                road.SetMedianLane(median);
            }

            if (NextIntersection != null && newNextBorders.Any())
                NextIntersection.BuildTracks(BuildTrackOption.WithBorder(newNextBorders));

            if (PrevIntersection != null && newPrevBorders.Any())
                PrevIntersection.BuildTracks(BuildTrackOption.WithBorder(newPrevBorders));
        }

        /// <summary>
        /// レーン数を変更する. 中央分離帯は削除する
        /// </summary>
        /// <param name="leftCount"></param>
        /// <param name="rightCount"></param>
        /// <param name="rebuildTrack"></param>
        private void SetLaneCountWithoutMedian(int leftCount, int rightCount, bool rebuildTrack)
        {
            if (IsValid == false)
                return;
            // 既に指定の数になっている場合は何もしない
            if (Roads.All(l => l.GetLeftLaneCount() == leftCount && l.GetRightLaneCount() == rightCount))
                return;

            // 向きをそろえる
            Align();

            var num = leftCount + rightCount;
            var afterLanes = SplitLane(num, null);
            if (afterLanes == null)
                return;

            // 最後に隣接する交差点のトラックの再生成を行うためのキャッシュ
            // 隣接情報が確定した後でトラック生成を行う必要があるので、ここでキャッシュしておく
            List<RnLineString> newNextBorders = new();
            List<RnLineString> newPrevBorders = new();

            // Roadsに変更を加えるのは最後にまとめて必要がある
            // (RnRoads.IsLeftLane等が隣のRoadに依存するため. 途中で変更すると、後続の処理が破綻する可能性がある)
            for (var i = 0; i < Roads.Count; ++i)
            {
                var road = Roads[i];
                var lanes = afterLanes[road];

                if (i == Roads.Count - 1)
                {
                    NextIntersection?.ReplaceEdges(road, lanes.Select(l => l.NextBorder).ToList());
                    newNextBorders.AddRange(lanes.Select(l => l.NextBorder.LineString));
                }

                if (i == 0 && PrevIntersection != null)
                {
                    PrevIntersection?.ReplaceEdges(road, lanes.Select(l => l.PrevBorder).ToList());
                    newPrevBorders.AddRange(lanes.Select(l => l.PrevBorder.LineString));
                }

                // 右車線の分
                for (var j = leftCount; j < lanes.Count; ++j)
                    lanes[j].Reverse();

                road.ReplaceLanes(lanes);
            }

            // 中央分離帯を削除する
            foreach (var l in Roads)
                l.SetMedianLane(null);

            if (rebuildTrack)
            {
                if (NextIntersection != null && newNextBorders.Any())
                    NextIntersection.BuildTracks(BuildTrackOption.WithBorder(newNextBorders));

                if (PrevIntersection != null && newPrevBorders.Any())
                    PrevIntersection.BuildTracks(BuildTrackOption.WithBorder(newPrevBorders));
            }
        }

        /// <summary>
        /// レーン数を変更する
        /// </summary>
        /// <param name="leftCount"></param>
        /// <param name="rightCount"></param>
        /// <param name="rebuildTrack"></param>
        public void SetLaneCount(int leftCount, int rightCount, bool rebuildTrack = true)
        {
            if (IsValid == false)
                return;

            // 両方とも0はダメ
            if (leftCount <= 0 && rightCount <= 0)
            {
                DebugEx.LogWarning($"両方の車線数に0を入れることはできません");
                return;
            }

            // 向きをそろえる
            Align();

            var nowLeft = GetLeftLaneCount();
            var nowRight = GetRightLaneCount();

            // すでにどっちのレーンもある場合
            // or 0個の車線の道路の指定が0のままの場合は独立して分割する
            if ((nowLeft > 0 || leftCount == 0) &&
                (nowRight > 0 || rightCount == 0))
            {
                SetLeftLaneCount(leftCount, rebuildTrack);
                SetRightLaneCount(rightCount, rebuildTrack);
                return;
            }

            SetLaneCountWithoutMedian(leftCount, rightCount, rebuildTrack);
        }

        /// <summary>
        /// 左側レーン数を変更する
        /// </summary>
        /// <param name="count"></param>
        /// <param name="rebuildTrack"></param>
        public void SetLeftLaneCount(int count, bool rebuildTrack = true)
        {
            // 既に指定の数になっている場合は何もしない
            if (GetLeftLaneCount() == count)
                return;

            // 左車線が無い場合は全車線含めて変更する
            if (GetLeftLaneCount() == 0 || count == 0)
            {
                SetLaneCountWithoutMedian(count, GetRightLaneCount(), rebuildTrack);
            }
            // すでに左車線がある場合はそれだけで変更する
            else
            {
                SetLaneCountImpl(count, RnDir.Left, rebuildTrack);
            }
        }

        /// <summary>
        /// 右側レーン数を変更する
        /// </summary>
        /// <param name="count"></param>
        /// <param name="rebuildTrack"></param>
        public void SetRightLaneCount(int count, bool rebuildTrack = true)
        {
            // 既に指定の数になっている場合は何もしない
            if (GetRightLaneCount() == count)
                return;

            // 右車線が無い場合は全車線含めて変更する
            if (GetRightLaneCount() == 0 || count == 0)
            {
                SetLaneCountWithoutMedian(GetLeftLaneCount(), count, rebuildTrack);
            }
            // すでに右車線がある場合はそれだけで変更する
            else
            {
                SetLaneCountImpl(count, RnDir.Right, rebuildTrack);
            }
        }

        /// <summary>
        /// RnDirで指定した側のレーン数を設定する
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="count"></param>
        /// <param name="rebuildTrack">レーン分割後に関係する交差点のトラックを再生成する</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void SetLaneCount(RnDir dir, int count, bool rebuildTrack = true)
        {
            switch (dir)
            {
                case RnDir.Left:
                    SetLeftLaneCount(count, rebuildTrack);
                    break;
                case RnDir.Right:
                    SetRightLaneCount(count, rebuildTrack);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, $"SetLaneCount({dir})");
            }
        }

        /// <summary>
        /// 中央分離帯を取得する
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        public void GetMedians(
            out IReadOnlyCollection<RnWay> left,
            out IReadOnlyCollection<RnWay> right)
        {
            var lWays = new List<RnWay>(Roads.Count);
            var rWays = new List<RnWay>(Roads.Count);

            foreach (var road in Roads)
            {
                if (road.GetLeftLaneCount() > 0)
                {
                    var centerLeft = road.GetLeftLanes()?.Last();
                    lWays.Add(centerLeft.RightWay);
                }
                if (road.GetRightLaneCount() > 0)
                {
                    var centerRight = road.GetRightLanes()?.First();
                    rWays.Add(centerRight.RightWay);
                }
            }

            left = lWays;
            right = rWays;
        }

        public bool HasMedians()
        {
            return Roads.Any(l => l.MedianLane != null);
        }

        /// <summary>
        /// 中央分離帯の幅を拡縮する. deltaWidthが正なら幅が増加する
        /// </summary>
        /// <param name="deltaWidth"></param>
        /// <param name="moveOption"></param>
        /// <returns></returns>
        public bool ExpandMedianWidth(float deltaWidth, LaneWayMoveOption moveOption)
        {
            if (HasMedian() == false)
                return false;
            Align();
            foreach (var road in Roads)
            {
                //// サイズ0の中央分離帯だと法線方向が定まらないので、すでに幅の存在するレーンを動かすことで
                //// 中央分離帯の幅を設定する
                var centerLeft = road.GetLeftLanes().Last();
                var centerRight = road.GetRightLanes().First();
                switch (moveOption)
                {
                    case LaneWayMoveOption.MoveBothWay:
                        centerLeft.RightWay?.MoveAlongNormal(-deltaWidth * 0.5f);
                        centerRight.RightWay?.MoveAlongNormal(-deltaWidth * 0.5f);
                        break;
                    case LaneWayMoveOption.MoveLeftWay:
                        centerLeft.RightWay?.MoveAlongNormal(-deltaWidth);
                        break;
                    case LaneWayMoveOption.MoveRightWay:
                        centerRight.RightWay?.MoveAlongNormal(-deltaWidth);
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// 中央分離帯を削除する
        /// </summary>
        /// <param name="moveOption"></param>
        public void RemoveMedian(LaneWayMoveOption moveOption = LaneWayMoveOption.MoveBothWay)
        {
            if (IsValid == false)
            {
                DebugEx.LogError($"不正な道路に対してRemoveMedianが呼ばれました");
                return;
            }

            foreach (var road in Roads)
            {
                if (road.MedianLane == null)
                    continue;

                // 中央線との境界の左車線を取得
                var leftLane = road.GetLeftLanes().LastOrDefault();
                var rightLane = road.GetRightLanes().FirstOrDefault();
                if (leftLane == null || rightLane == null)
                {
                    DebugEx.LogError($"中央分離帯があるのに, 片側車線しかない道路です");
                    continue;
                }

                var borderTypes = new[] { RnLaneBorderType.Prev, RnLaneBorderType.Next };

                // laneの境界線に中央分離帯の境界線を追加する
                // dirは境界線を取得するときの方向
                // 左車線の場合は左->右の方向でとってきて後ろに追加する
                // 右車線の場合は右->左の方向でとってきて後ろに追加する
                void MergeBorder(RnLane lane, RnLaneBorderDir dir)
                {
                    var median = road.MedianLane;
                    foreach (var borderType in borderTypes)
                    {
                        // 左車線に追加するときも右車線に追加するときも
                        // 必ず中央分離帯は右側にあるのでそれ前提
                        // レーンの境界線をLeft->Right方向で求める
                        var way = road.GetBorderWay(lane, borderType, dir);
                        var medianWay = road.GetBorderWay(median, borderType, dir);
                        way.AppendBack2LineString(medianWay);
                    }
                }

                switch (moveOption)
                {
                    case LaneWayMoveOption.MoveLeftWay:
                        {
                            leftLane.SetSideWay(RnDir.Right, rightLane.RightWay.ReversedWay());
                            MergeBorder(leftLane, RnLaneBorderDir.Left2Right);

                            break;
                        }
                    case LaneWayMoveOption.MoveRightWay:
                        {
                            rightLane.SetSideWay(RnDir.Right, leftLane.RightWay.ReversedWay());
                            MergeBorder(rightLane, RnLaneBorderDir.Right2Left);
                            break;
                        }
                    case LaneWayMoveOption.MoveBothWay:
                        {
                            var median = road.MedianLane;
                            foreach (var borderType in borderTypes)
                            {
                                var mWay = road.GetBorderWay(median, borderType, RnLaneBorderDir.Left2Right);
                                var mWays = mWay.Split(2, false);
                                var leftBorder = road.GetBorderWay(leftLane, borderType, RnLaneBorderDir.Left2Right);
                                leftBorder.AppendBack2LineString(mWays[0]);

                                var rightBorder = road.GetBorderWay(rightLane, borderType, RnLaneBorderDir.Right2Left);
                                rightBorder.AppendBack2LineString(mWays[1].ReversedWay());
                            }
                            var left = leftLane.RightWay;
                            var right = rightLane.RightWay.ReversedWay();

                            // leftLaneから中央分離帯側の端点を取得
                            RnPoint GetEdgePoint(RnLaneBorderType type)
                            {
                                if (leftLane.GetBorderDir(type) == RnLaneBorderDir.Left2Right)
                                {
                                    // 左 -> 右の場合は最後の点
                                    return leftLane.GetBorder(type).GetPoint(-1);
                                }
                                else
                                {
                                    // 右 -> 左の場合は最初の点
                                    return leftLane.GetBorder(type).GetPoint(0);
                                }
                            }

                            var centerLine = RnEx.CreateInnerLerpLineString(
                                left.Vertices.ToList()
                                , right.Vertices.ToList()
                                , GetEdgePoint(RnLaneBorderType.Prev)
                                , GetEdgePoint(RnLaneBorderType.Next)
                                , RnWayEx.CreateMergedWay(leftLane.PrevBorder, rightLane.PrevBorder)
                                , RnWayEx.CreateMergedWay(leftLane.NextBorder, rightLane.NextBorder)
                                , 0.5f);

                            leftLane.SetSideWay(RnDir.Right, new RnWay(centerLine, false, true));
                            rightLane.SetSideWay(RnDir.Right, new RnWay(centerLine, true, true));
                            break;
                        }

                }
                road.SetMedianLane(null);
            }
        }

        public RnWayGroup GetEdgeWays(RnDir dir)
        {
            var cap = Roads.Count * 2;
            List<RnWay> ways = new List<RnWay>(cap);
            foreach (var road in Roads)
            {
                var mainLanes = road.MainLanes;
                var lane = RnDir.Left == dir ? mainLanes.FirstOrDefault() : mainLanes.LastOrDefault();
                var wayDir = RnDir.Left;
                wayDir = !lane.IsReverse ? wayDir : wayDir.GetOpposite();
                wayDir = dir == RnDir.Left ? wayDir : wayDir.GetOpposite();
                var way = lane.GetSideWay(wayDir);
                if (way != null)
                    ways.Add(way);
            }
            return new RnWayGroup(ways);
        }

        public IReadOnlyCollection<RnSideWalkGroup> GetSideWalkGroups()
        {
            var sideWalks = new List<RnSideWalk>();
            foreach (var road in Roads)
            {
                sideWalks.AddRange(road.SideWalks);
            }

            var groups = new List<RnSideWalkGroup>();
            foreach (var sideWalk in sideWalks)
            {
                var group = groups.FirstOrDefault(g => g.SideWalks.Contains(sideWalk));
                if (group == null)
                {
                    group = new RnSideWalkGroup(new List<RnSideWalk> { sideWalk });
                    groups.Add(group);
                }
                else
                {
                    ((List<RnSideWalk>)group.SideWalks).Add(sideWalk);
                }
            }

            return groups;
        }

        public void GetSideWalkGroups(out IReadOnlyCollection<RnSideWalkGroup> left, out IReadOnlyCollection<RnSideWalkGroup> right)
        {
            var cap = Roads.Count * 2;
            var leftSideWalks = new List<RnSideWalk>(cap);
            var rightSideWalks = new List<RnSideWalk>(cap);
            foreach (var road in Roads)
            {
                foreach (var sideWalk in road.SideWalks)
                {
                    if (sideWalk.LaneType == RnSideWalkLaneType.LeftLane)
                        leftSideWalks.Add(sideWalk);
                    else if (sideWalk.LaneType == RnSideWalkLaneType.RightLane)
                        rightSideWalks.Add(sideWalk);
                    else
                    {
                        // 仮　未定義のものは右側に割り当てる　後ほど位置関係から識別しても良い
                        rightSideWalks.Add(sideWalk);
                    }
                }
            }

            var leftGroup = new List<RnSideWalkGroup>
            {
                new RnSideWalkGroup(leftSideWalks)
            };
            var rightGroup = new List<RnSideWalkGroup>
            {
                new RnSideWalkGroup(rightSideWalks)
            };

            left = leftGroup;
            right = rightGroup;

        }

        public void AddSideWalks(IReadOnlyCollection<RnSideWalkGroup> sideWalkGroup)
        {
            // todo 重複チェック必要

            foreach (var road in Roads)
            {
                foreach (var sideWalk in sideWalkGroup)
                {
                    foreach (var item in sideWalk.SideWalks)
                    {
                        road.AddSideWalk(item);
                    }
                }
            }
        }

        public void RemoveSideWalks(IReadOnlyCollection<RnSideWalkGroup> sideWalks)
        {
            // todo 重複チェック必要

            foreach (var road in Roads)
            {
                foreach (var sideWalk in sideWalks)
                {
                    foreach (var item in sideWalk.SideWalks)
                    {
                        road.RemoveSideWalk(item);
                    }
                }
            }
        }

        /// <summary>
        /// まだ中央分離帯がない場合は作成する.
        /// medianWidthは作成時の中央分離帯の幅. 最低1m. ただし目安であり、実際の幅は最小のレーン幅に合わせる
        /// </summary>
        /// <param name="medianWidth"></param>
        /// <param name="maxMedianLaneRate">中央分離帯の割合が全体の道のこれを超えないようにする</param>
        public bool CreateMedianOrSkip(float medianWidth = 1f, float maxMedianLaneRate = 0.5f)
        {
            // 全ての道路に中央分離帯がある場合は何もしない
            if (HasMedian())
                return false;
            // 両方にレーンが無いと作成しない
            if (GetLeftLaneCount() == 0 || GetRightLaneCount() == 0)
            {
                DebugEx.LogWarning($"中央分離帯を作成するには左右の車線が必要");
                return false;
            }

            medianWidth = Mathf.Max(1, medianWidth);
            // 1番小さい幅レーンの道幅が1[m]になるように中央分離帯を作成する
            float MinLaneWidth(RnRoad r)
            {
                return r.AllLanesWithMedian.Sum(l => l.CalcWidth());
            }
            var width = Roads.Min(MinLaneWidth);
            var medianRate = Mathf.Min(maxMedianLaneRate, medianWidth / width);
            SetLaneCountWithMedian(GetLeftLaneCount(), GetRightLaneCount(), medianRate);
            return true;
        }

        /// <summary>
        /// 中央分離帯があるかどうか.
        /// 構成するすべての道路に中央分離帯がある場合にtrueを返す
        /// </summary>
        /// <returns></returns>
        public bool HasMedian()
        {
            return Roads.All(r => r.MedianLane != null);
        }

        /// <summary>
        /// 中央を通るスプラインとその幅を取得する
        /// 幅は中間点から取得するので完璧な値ではない
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="width"></param>
        /// <param name="tangentLength"></param>
        /// <param name="pointSkipDistance"></param>
        /// <returns></returns>
        public bool TryCreateSpline(out Spline spline, out float width, float tangentLength = 1f, float pointSkipDistance = 1e-3f)
        {
            Align();
            spline = new Spline();
            width = float.MaxValue;
            var points = new List<Vector3>();
            foreach (var r in Roads)
            {
                if (r.TryGetMergedSideWay(null, out var leftWay, out var rightWay) == false)
                    return false;
                var prevBorder = r.GetMergedBorder(RnLaneBorderType.Prev, null);
                var nextBorder = r.GetMergedBorder(RnLaneBorderType.Next, null);
                var start = new RnPoint(prevBorder.GetLerpPoint(0.5f));
                var end = new RnPoint(nextBorder.GetLerpPoint(0.5f));
                var line = RnEx.CreateInnerLerpLineString(leftWay.Vertices.ToList(), rightWay.Vertices.ToList(), start, end, prevBorder, nextBorder, 0.5f, pointSkipDistance);

                if (r.TryGetNearestDistanceToSideWays(line, out var distance) == false)
                    return false;
                // 左右の線との近い方の倍を幅とする
                var w = distance * 2;
                points.AddRange(line.Points.Select(p => p.Vertex));

                width = Mathf.Min(w, width);
            }

            for (var i = 0; i < points.Count; i++)
            {
                var dirIn = (i == 0 ? (points[i + 1] - points[i]) : (points[i] - points[i - 1])).normalized;
                var dirOut = i == (points.Count - 1) ? dirIn : (points[i + 1] - points[i]).normalized;
                spline.Add(new BezierKnot(points[i], dirIn * tangentLength, dirOut * tangentLength));
            }


            return true;
        }

        /// <summary>
        /// 中央を通るスプラインとその幅を取得する
        /// 幅は中間点から取得するので完璧な値ではない
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="width"></param>
        /// <param name="tangentLength"></param>
        /// <param name="pointSkipDistance"></param>
        /// <returns></returns>
        public bool TryCreateSimpleSpline(out Spline spline, out float width, float tangentLength = 1f, float pointSkipDistance = 1e-3f)
        {
            spline = new Spline();
            width = float.MaxValue;
            if (Roads.Count != 1)
            {
                DebugEx.LogError($"This operation is only supported when Roads.Count == 1. Current count = {Roads.Count}.");
                return false;
            }
            Align();

            var road = Roads[0];
            var minAngle = float.MaxValue;
            RnWay way = null;
            foreach (var l in road.AllLanesWithMedian)
            {
                foreach (var w in l.BothWays)
                {
                    var ang = w.LineString.CalcTotalAngle2D();
                    if (ang < minAngle)
                    {
                        minAngle = ang;
                        way = road.IsLeftLane(l) ? w : w.ReversedWay();
                    }
                }
            }

            if (way == null)
                return false;


            var prevBorder = road.GetMergedBorder(RnLaneBorderType.Prev, null);
            var nextBorder = road.GetMergedBorder(RnLaneBorderType.Next, null);
            var prevOffset = prevBorder.GetLerpPoint(0.5f) - way[0];
            var nextOffset = nextBorder.GetLerpPoint(0.5f) - way[^1];

            way = way.Clone(true);
            way.MoveLerpAlongNormal(prevOffset, nextOffset);
            for (var i = 0; i < way.Count; i++)
            {
                var dirIn = (i == 0 ? (way[i + 1] - way[i]) : (way[i] - way[i - 1])).normalized;
                var dirOut = i == (way.Count - 1) ? dirIn : (way[i + 1] - way[i]).normalized;
                spline.Add(new BezierKnot(way[i], dirIn * tangentLength, dirOut * tangentLength));
            }
            var ret = road.TryGetNearestDistanceToSideWays(way.LineString, out var distance);
            // 左右の線との近い方の倍を幅とする
            width = distance * 2;
            return ret;
        }

        /// <summary>
        /// 向きがそろっているかどうか
        /// </summary>
        public bool IsAligned
        {
            get
            {
                if (Roads.Count <= 1)
                    return true;

                for (var i = 0; i < Roads.Count; ++i)
                {
                    // 自分のNextがi+1番目じゃない場合は向きが逆
                    if (i < Roads.Count - 1 && Roads[i].Next != Roads[i + 1])
                        return false;

                    // 自分のPrevがi-1番目のRoadsじゃない場合は向きが逆
                    if (i > 0 && Roads[i].Prev != Roads[i - 1])
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// 複数道路がきれいに整列されているかどうか
        /// 道路の方向が一致, かつレーンの方向も含めて向きが一致している
        /// </summary>
        /// <returns></returns>
        public bool IsDeepAligned()
        {
            if (IsAligned == false)
                return false;

            // 道路の数が1以下の場合はOK
            if (Roads.Count <= 1)
                return true;

            // 次にLaneのPrev/Nextの向きをそろえる
            // 0番目を基準にして, 1番目以降の道路をひとつ前の道路に合わせていく
            for (var i = 1; i < Roads.Count; ++i)
            {
                var prevRoad = Roads[i - 1];
                var nowRoad = Roads[i];
                var nowLanes = nowRoad.AllLanesWithMedian.ToList();
                var prevLanes = prevRoad.AllLanesWithMedian.ToList();

                // レーン数が異なる場合はエラー
                if (nowLanes.Count != prevLanes.Count)
                {
                    DebugEx.LogError("Lane counts is mismatch");
                    return false;
                }

                for (var j = 0; j < Mathf.Min(nowLanes.Count, prevLanes.Count); ++j)
                {
                    var nowLane = nowLanes[j];
                    var prevLane = prevLanes[j];
                    // 親の方向と一致している場合はprevLane -> nextLaneの方向になっているかチェックする
                    if (prevLane.IsReverse == false)
                    {
                        if (nowLane.PrevBorder.IsSameLineReference(prevLane.NextBorder) == false)
                        {
                            DebugEx.LogError($"invalid Direction Lane[{j}] ${prevRoad.GetTargetTransName()} -> ${nowRoad.GetTargetTransName()}");
                            return false;
                        }
                    }
                    // 親の方向と逆の場合はnextLane -> prevLaneの方向になっているかチェックする
                    else
                    {
                        if (prevLane.PrevBorder.IsSameLineReference(nowLane.NextBorder) == false)
                        {
                            DebugEx.LogError($"invalid Direction Lane[{j}] ${prevRoad.GetTargetTransName()} -> ${nowRoad.GetTargetTransName()}");
                            return false;
                        }
                    }

                }
            }

            return true;
        }

        /// <summary>
        /// LaneのPrev/Nextの向きをそろえる
        /// </summary>
        public bool Align()
        {
            if (IsAligned)
                return true;

            // まずはRoadsのPrev/Nextの向きをそろえる
            // Roads.Count <= 1の場合はIsAligned=trueなのでここでは
            // インデックス範囲外チェックはしなくてよい
            // 0番目が逆かどうかチェック
            if (Roads[0].Next != Roads[1])
                Roads[0].Reverse();

            // 1番目以降が逆かどうかチェック
            for (var i = 1; i < Roads.Count; ++i)
            {
                // 自分のPrevがi-1番目のRoadsじゃない場合は向きが逆
                if (Roads[i].Prev != Roads[i - 1])
                    Roads[i].Reverse();
            }

            // 境界線の向きもそろえる
            foreach (var l in Roads)
                l.AlignLaneBorder();

            if (Roads[0].Prev != PrevIntersection)
                (PrevIntersection, NextIntersection) = (NextIntersection, PrevIntersection);

            return IsDeepAligned();
        }

        /// <summary>
        /// 複数のRoadsを1つのRoadにまとめる
        /// </summary>
        public bool MergeRoads()
        {
            if (Align() == false)
                return false;

            if (Roads.Count <= 1)
                return true;

            // マージ先の道路
            var dstRoad = Roads[0];
            var dstLanes = dstRoad.AllLanesWithMedian.ToList();
            var dstSideWalks = dstRoad.SideWalks.ToList();


            // SideWalksと共通のLineStringがあるとき, レーン側は統合されるけど
            // SideWalksは統合されない場合もあるので一旦、SideWalksのLineStringはコピーを保持するようにする
            // 最後にLane側のLineStringと比較して同じものがあれば統合する
            foreach (var sw in dstRoad.SideWalks)
            {
                foreach (var way in sw.SideWays)
                {
                    way.LineString = way.LineString.Clone(false);
                }
            }

            for (var i = 1; i < Roads.Count; i++)
            {
                // マージ元の道路. dstRoadに統合されて消える
                var srcRoad = Roads[i];
                var srcLanes = srcRoad.AllLanesWithMedian.ToList();

                for (var j = 0; j < srcLanes.Count; ++j)
                {
                    var srcLane = srcLanes[j];
                    var dstLane = dstLanes[j];

                    // 順方向(左車線)
                    if (srcRoad.IsLeftLane(srcLane))
                    {
                        dstLane.LeftWay?.AppendBack2LineString(srcLane.LeftWay);
                        // 中間のレーンはつながっているので右車線に追加するのは最後だけで良い
                        if (j == srcLanes.Count - 1)
                        {
                            dstLane.RightWay?.AppendBack2LineString(srcLane.RightWay);
                        }

                        dstLane.SetBorder(RnLaneBorderType.Next, srcLane.NextBorder);
                    }
                    // 逆方向(右車線)
                    else
                    {
                        dstLane.RightWay?.AppendFront2LineString(srcLane.RightWay);
                        // 中間のレーンはつながっているので右車線に追加するのは最後だけで良い
                        if (j == srcLanes.Count - 1)
                        {
                            dstLane.LeftWay?.AppendFront2LineString(srcLane.LeftWay);
                        }

                        dstLane.SetBorder(RnLaneBorderType.Prev, srcLane.PrevBorder);
                    }
                }

                var srcSideWalks = srcRoad.SideWalks.ToList();
                HashSet<RnSideWalk> mergedDstSideWalks = new();
                foreach (var srcSw in srcSideWalks)
                {
                    var found = false;
                    foreach (var dstSw in dstSideWalks)
                    {
                        void Merge(bool reverse, Action<RnWay, RnWay> merger)
                        {
                            var insideWay = reverse ? srcSw.InsideWay?.ReversedWay() : srcSw.InsideWay;
                            var outsideWay = reverse ? srcSw.OutsideWay?.ReversedWay() : srcSw.OutsideWay;
                            if (dstSw.InsideWay != null)
                            {
                                merger(dstSw.InsideWay, insideWay);

                                insideWay = dstSw.InsideWay;
                            }

                            if (dstSw.OutsideWay != null)
                            {
                                merger(dstSw.OutsideWay, outsideWay);
                                outsideWay = dstSw.OutsideWay;
                            }

                            dstSw.SetSideWays(outsideWay, insideWay);
                            mergedDstSideWalks.Add(dstSw);
                            found = true;
                        }

                        // start - startで重なっている場合
                        if (dstSw.StartEdgeWay?.IsSameLineReference(srcSw.StartEdgeWay) ?? false)
                        {
                            Merge(true, RnWayEx.AppendFront2LineString);
                            dstSw.SetStartEdgeWay(srcSw.EndEdgeWay);
                        }
                        // start - endで重なっている場合
                        else if (dstSw.StartEdgeWay?.IsSameLineReference(srcSw.EndEdgeWay) ?? false)
                        {
                            Merge(false, RnWayEx.AppendFront2LineString);
                            dstSw.SetStartEdgeWay(srcSw.StartEdgeWay);
                        }
                        // end - endで重なっている場合
                        else if (dstSw.EndEdgeWay?.IsSameLineReference(srcSw.EndEdgeWay) ?? false)
                        {
                            Merge(true, RnWayEx.AppendBack2LineString);
                            dstSw.SetEndEdgeWay(srcSw.StartEdgeWay);
                        }
                        // end - startで重なっている場合
                        else if (dstSw.EndEdgeWay?.IsSameLineReference(srcSw.StartEdgeWay) ?? false)
                        {
                            Merge(false, RnWayEx.AppendBack2LineString);
                            dstSw.SetEndEdgeWay(srcSw.EndEdgeWay);
                        }

                        if (found)
                            break;
                    }

                    // マージできなかった歩道は直接追加
                    if (found == false)
                    {
                        srcSw.SetSideWays(srcSw.OutsideWay, srcSw.InsideWay);
                        dstRoad.AddSideWalk(srcSw);
                        dstSideWalks.Add(srcSw);
                    }
                }
                dstRoad.AddTargetTrans(srcRoad.TargetTrans);
                srcRoad.DisConnect(true);
            }

            if (NextIntersection != null)
            {
                // ↑のDisConnectですでに参照情報は消えているので, 再度dstRoadを再設定する
                foreach (var l in dstLanes)
                {
                    var border = dstRoad.GetBorderWay(l, RnLaneBorderType.Next, RnLaneBorderDir.Left2Right);
                    var replaceCount = NextIntersection.ReplaceEdgeLink(border, dstRoad);
                    if (replaceCount == 0)
                        DebugEx.LogError($"共通辺情報が更新されませんでした. {NextIntersection.GetTargetTransName()}/{l.GetDebugLabelOrDefault()}");
                }
            }

            // 同じLineStringを持つSideWalkを統合する
            HashSet<RnLineString> laneLineStrings = new();
            foreach (var lane in dstRoad.AllLanesWithMedian)
            {
                if (lane.LeftWay?.LineString != null)
                    laneLineStrings.Add(lane.LeftWay?.LineString);
                if (lane.RightWay?.LineString != null)
                    laneLineStrings.Add(lane.RightWay?.LineString);
            }

            foreach (var sw in dstSideWalks)
            {
                foreach (var way in sw.SideWays)
                {
                    bool found = false;
                    foreach (var ls in laneLineStrings)
                    {
                        if (RnLineStringEx.IsSequenceEqual(way.LineString, ls, out var isReverseSequence))
                        {
                            way.LineString = ls;
                            if (isReverseSequence)
                                way.Reverse(false);
                            found = true;
                            break;
                        }
                    }

                    if (found == false)
                    {
                        DebugEx.Log($"道路の統合により, 歩道と車道のLineStringが分離された");
                    }
                }
            }

            dstRoad.SetPrevNext(PrevIntersection, NextIntersection);

            roads = new[] { dstRoad }.ToList();
            return true;
        }


        /// <summary>
        /// 交差点との境界線の角度を調整する(進行方向に垂直になるようにする)
        /// </summary>
        public void AdjustBorder()
        {
            Align();
            static void Adjust(RnRoad road, RnLaneBorderType borderType, RnIntersection inter)
            {
                if (inter == null)
                    return;

                if (road.TryGetAdjustBorderSegment(borderType, out var borderLeft2Right))
                {
                    var leftWay = road.GetMergedSideWay(RnDir.Left);
                    var rightWay = road.GetMergedSideWay(RnDir.Right);

                    // 交差点がわの輪郭を崩さないように元の位置に点を追加しておく
                    void DuplicatePoints(RnPoint p, Vector3 checkVertex)
                    {
                        // pとcheckVertexが同一点の場合は点を増やすことはしない
                        if ((p.Vertex - checkVertex).sqrMagnitude < 1e-6f)
                            return;
                        foreach (var e in inter.Edges.Where(e =>
                                 e.Road != road && e.Border.LineString.Contains(p)))
                        {
                            var i = e.Border.LineString.Points.IndexOf(p);
                            if (i == 0)
                            {
                                e.Border.LineString.Points.Insert(1, new RnPoint(p.Vertex));
                            }
                            else if (i == e.Border.LineString.Points.Count - 1)
                            {
                                e.Border.LineString.Points.Insert(e.Border.LineString.Points.Count - 1, new RnPoint(p.Vertex));
                            }
                        }
                    }
                    var lastPointIndex = borderType == RnLaneBorderType.Prev ? 0 : -1;
                    DuplicatePoints(leftWay.GetPoint(lastPointIndex), borderLeft2Right.Start);
                    DuplicatePoints(rightWay.GetPoint(lastPointIndex), borderLeft2Right.End);

                    void AdjustWay(RnLane lane, RnWay way)
                    {
                        if (lane.IsReverse)
                            way = way.ReversedWay();
                        var p = way.GetPoint(lastPointIndex);
                        var n = borderLeft2Right.GetNearestPoint(p.Vertex);
                        p.Vertex = n;
                    }
                    var lanes = road.AllLanesWithMedian.ToList();
                    var newBorders = new List<RnWay>(lanes.Count);
                    for (var i = 0; i < lanes.Count; ++i)
                    {
                        var lane = lanes[i];
                        AdjustWay(lane, lane.LeftWay);
                        if (i == lanes.Count - 1)
                            AdjustWay(lane, lane.RightWay);

                        var l = lane.LeftWay;
                        var r = lane.RightWay;
                        if (road.IsLeftLane(lane) == false)
                        {
                            l = l.ReversedWay();
                            r = r.ReversedWay();
                        }
                        var ls = RnLineString.Create(new List<RnPoint>
                    {
                        l.GetPoint(lastPointIndex), r.GetPoint(lastPointIndex)
                    });
                        var way = new RnWay(ls);
                        newBorders.Add(way);
                    }

                    inter.ReplaceEdges(road, newBorders);
                    for (var i = 0; i < lanes.Count; ++i)
                    {
                        var b = newBorders[i];
                        var lane = lanes[i];
                        if (road.IsLeftLane(lane))
                        {
                            lane.SetBorder(borderType, b);
                        }
                        else
                        {
                            lane.SetBorder(borderType.GetOpposite(), b.ReversedWay());
                        }
                    }

                }
            }
            Adjust(Roads[^1], RnLaneBorderType.Next, NextIntersection);
            Adjust(Roads[0], RnLaneBorderType.Prev, PrevIntersection);
        }

        // ---------------
        // Static Methods
        // ---------------
        /// <summary>
        /// 2つのノードを繋ぐRoadGroupを作成する
        /// 2つのノードが直接繋がっていない場合はnullを返す
        /// </summary>
        /// <param name="prevIntersection"></param>
        /// <param name="nextIntersection"></param>
        /// <returns></returns>
        public static RnRoadGroup CreateRoadGroupOrDefault(RnIntersection prevIntersection, RnIntersection nextIntersection)
        {
            if (prevIntersection == null || nextIntersection == null)
                return null;

            foreach (var n in prevIntersection.GetNeighborRoads())
            {
                if (n is RnRoad road)
                {
                    var ret = new RnRoadGroup(prevIntersection, nextIntersection, new[] { road });
                    var hasPrev = ret.PrevIntersection == prevIntersection || ret.NextIntersection == prevIntersection;
                    var hasNex = ret.PrevIntersection == nextIntersection || ret.NextIntersection == nextIntersection;
                    if (hasPrev && hasNex)
                        return ret;
                }
            }

            return null;
        }

        /// <summary>
        /// 2つのRoadGroupが同じかどうかを識別する
        /// 両方nullである場合はfalseを返す
        /// 
        /// 判定は同じ交差点、同じ道路を含むかで行う。
        /// この時、各要素の順序は問わない
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsSameRoadGroup(RnRoadGroup a, RnRoadGroup b)
        {
            if (a == null && b == null)
                return false;

            // 同じ交差点を含むか（Next,Prevは問わない）
            var isSameIntersection =
                (a.PrevIntersection == b.PrevIntersection && a.NextIntersection == b.NextIntersection) ||
                (a.PrevIntersection == b.NextIntersection && a.NextIntersection == b.PrevIntersection);

            // 同じ道路を含むか
            var isContainSameRoads = true;
            foreach (var road in a.roads)
            {
                if (b.roads.Contains(road))
                {
                    continue;
                }

                isContainSameRoads = false;
                break;
            }

            return isSameIntersection && isContainSameRoads;
        }

        public class RnSideWalkGroup
        {
            public RnSideWalkGroup(List<RnSideWalk> sideWalks)
            {
                this.sideWalks = sideWalks;
            }

            public IReadOnlyCollection<RnSideWalk> SideWalks => sideWalks;

            List<RnSideWalk> sideWalks;
        }


        public class RnWayGroup
        {
            public RnWayGroup(List<RnWay> ways)
            {
                Ways = ways;
            }

            public IReadOnlyCollection<RnWay> Ways { get; private set; }

        }
    }
}