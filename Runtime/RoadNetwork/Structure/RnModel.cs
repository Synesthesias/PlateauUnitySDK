using PLATEAU.RoadNetwork.Data;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Structure
{
    // #NOTE : Editorが重いのでSerialize対象にしない
    public partial class RnModel
    {
        public const float Epsilon = float.Epsilon;

        internal const AxisPlane Plane = RnDef.Plane;

        //----------------------------------
        // start: フィールド
        //----------------------------------

        /// <summary>
        /// 自動生成で作成されたときのバージョン. これが現在のバージョンよりも古い場合はデータが古い可能性がある.
        /// RoadNetworkFactory.FactoryVersion参照
        /// </summary>
        public string FactoryVersion { get; set; } = "";

        // #NOTE : Editorが重いのでSerialize対象にしない
        private List<RnRoad> roads = new List<RnRoad>();

        private List<RnIntersection> intersections = new List<RnIntersection>();

        private List<RnSideWalk> sideWalks = new List<RnSideWalk>();

        //----------------------------------
        // end: フィールド
        //----------------------------------

        public IReadOnlyList<RnRoad> Roads => roads;

        public IReadOnlyList<RnIntersection> Intersections => intersections;

        public IReadOnlyList<RnSideWalk> SideWalks => sideWalks;

        public void AddRoad(RnRoad link)
        {
            if (roads.Contains(link))
                return;

            link.ParentModel = this;
            roads.Add(link);
        }

        public void RemoveRoad(RnRoad link)
        {
            if (roads.Remove(link))
                link.ParentModel = null;
        }

        public void AddIntersection(RnIntersection intersection)
        {
            if (Intersections.Contains(intersection))
                return;

            intersection.ParentModel = this;
            intersections.Add(intersection);
        }

        public void RemoveIntersection(RnIntersection intersection)
        {
            if (intersections.Remove(intersection))
                intersection.ParentModel = null;
        }

        /// <summary>
        /// レーンの削除
        /// </summary>
        /// <param name="lane"></param>
        public void RemoveLane(RnLane lane)
        {
            foreach (var l in roads)
            {
                l.RemoveLane(lane);
            }
        }

        /// <summary>
        /// レーンの入れ替え
        /// </summary>
        /// <param name="before"></param>
        /// <param name="after"></param>
        public void ReplaceLane(RnLane before, RnLane after)
        {
            foreach (var l in roads)
                l.ReplaceLane(before, after);
        }

        /// <summary>
        /// Roadのレーンを全て取得
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RnLane> CollectAllLanes()
        {
            // Laneは重複しないはず
            return Roads.SelectMany(l => l.AllLanesWithMedian);
        }

        public IEnumerable<RnNeighbor> CollectAllEdges()
        {
            return Intersections.SelectMany(i => i.Edges);
        }

        /// <summary>
        /// Intersection/RoadのWayを全て取得
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RnWay> CollectAllWays()
        {
            // #TODO : 実際はもっとある
            return CollectAllLanes()
                .SelectMany(l => l.AllWays)
                .Concat(CollectAllEdges().Select(e => e.Border))
                .Concat(SideWalks.SelectMany(sw => sw.AllWays))
                .Distinct();
        }

        /// <summary>
        /// Modelが所持する全てのLineStringを取得
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RnLineString> CollectAllLineStrings()
        {
            return CollectAllWays().Select(w => w.LineString).Distinct();
        }

        /// <summary>
        /// roadをIntersectionに変換
        /// </summary>
        /// <param name="road"></param>
        /// <param name="buildTracks">変換後にTrackの生成を行う</param>
        public void Convert2Intersection(RnRoad road, bool buildTracks = true)
        {
            var intersection = new RnIntersection(road.TargetTrans);

            // 左右のWayとBorderを使って交差点とする
            road.TryGetMergedSideWay(null, out var leftWay, out var rightWay);
            if (leftWay != null)
                intersection.AddEdge(null, leftWay);
            foreach (var lane in road.AllLanesWithMedian)
            {
                lane.AlignBorder();
                var border = lane.NextBorder.Clone(false);
                intersection.AddEdge(lane.GetNextRoad(), border);
            }

            if (rightWay != null)
                intersection.AddEdge(null, rightWay.ReversedWay());
            foreach (var lane in road.AllLanesWithMedian)
            {
                var border = lane.PrevBorder.ReversedWay();
                intersection.AddEdge(lane.GetPrevRoad(), border);
            }

            // 歩道情報を移す
            foreach (var sw in road.SideWalks.ToList())
            {
                intersection.AddSideWalk(sw);
            }

            AddIntersection(intersection);
            if (buildTracks)
                intersection.BuildTracks();
            // 旧Roadの削除
            road.DisConnect(true);
        }

        public void Convert2Road(RnIntersection intersection, RnRoadBase prev, RnRoadBase next)
        {
            var neighbors = intersection
                .Neighbors
                .Where(n => n.Road == prev || n.Road == next)
                .ToList();

            var road = new RnRoad(intersection.TargetTrans);
            road.SetPrevNext(prev, next);

            // #TODO : 

            AddRoad(road);
            intersection.DisConnect(true);
        }

        /// <summary>
        /// 歩道情報追加
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="sideWalk"></param>
        public void AddSideWalk(RnSideWalk sideWalk)
        {
            if (sideWalks.Contains(sideWalk))
                return;
            sideWalks.Add(sideWalk);
        }

        public void RemoveSideWalk(RnSideWalk sideWalk)
        {
            sideWalks.Remove(sideWalk);
        }

        public void AddRoadBase(RnRoadBase roadBase)
        {
            if (roadBase is RnRoad road)
                AddRoad(road);
            else if (roadBase is RnIntersection intersection)
                AddIntersection(intersection);
        }

        /// <summary>
        /// prev/next間に道路を作成
        /// </summary>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <param name="lane"></param>
        public void CreateRoadBetweenIntersection(RnIntersection prev, RnIntersection next, RnLane lane)
        {
            var road = RnRoad.CreateOneLaneRoad(null, lane);
            road.SetPrevNext(prev, next);

            // intersectionに隣接情報追加
            prev.AddEdge(road, lane.PrevBorder);
            next.AddEdge(road, lane.NextBorder);
            AddRoad(road);
        }

        public RoadNetworkStorage Serialize(bool createEmptyCheck = true)
        {
            // シリアライズ前に一度全レーンに対して中央線を作成する


            using (var _ = new DebugTimer("Create Center Way"))
            {
                foreach (var road in Roads)
                {
                    foreach (var l in road.MainLanes)
                        l.CreateCenterWay();
                }
            }
            if (createEmptyCheck)
            {
                using var _ = new DebugTimer("Create Empty Roads");
                CreateEmptyRoadBetweenInteraction();
                CreateEmptyIntersectionBetweenRoad();
            }


            var serializer = new RoadNetworkSerializer();
            RoadNetworkStorage ret;
            using (var _ = new DebugTimer("Serialize"))
            {
                ret = serializer.Serialize(this, false);
            }

            // 自分は元に戻す
            if (createEmptyCheck)
            {
                using var _ = new DebugTimer("Remove Empty Roads");
                RemoveEmptyRoadBetweenIntersection();
                RemoveEmptyIntersectionBetweenRoad();
            }

            return ret;
        }

        public void Deserialize(RoadNetworkStorage storage, bool removeEmptyCheck = true)
        {
            FactoryVersion = storage.FactoryVersion;
            var serializer = new RoadNetworkSerializer();
            var model = serializer.Deserialize(storage);
            CopyFrom(model);
            if (removeEmptyCheck)
            {
                RemoveEmptyRoadBetweenIntersection();
                RemoveEmptyIntersectionBetweenRoad();
            }
        }

        /// <summary>
        /// 道路同士が直接つながっている状況において、空の交差点を作成する
        /// </summary>
        public void CreateEmptyIntersectionBetweenRoad()
        {
            HashSet<RnRoad> visited = new();
            var visitedRoads = new HashSet<RnRoad>();
            foreach (var link in Roads)
            {
                if (visitedRoads.Contains(link))
                    continue;

                try
                {
                    var roadGroup = link.CreateRoadGroup();
                    foreach (var r in roadGroup.Roads)
                        visitedRoads.Add(r);

                    if (roadGroup.Roads.Count <= 1)
                        continue;

                    // 整列させる
                    roadGroup.Align();
                    for (var i = 0; i < roadGroup.Roads.Count - 1; i++)
                    {
                        var prev = roadGroup.Roads[i];
                        var next = roadGroup.Roads[i + 1];
                        var borders = prev.GetBorderWays(RnLaneBorderType.Next).ToList();
                        var intersection = RnIntersection.CreateEmptyIntersection(borders, prev, next);

                        prev?.SetPrevNext(prev.Prev, intersection);
                        next?.SetPrevNext(intersection, next.Next);
                        AddIntersection(intersection);
                    }

                }
                catch (Exception e)
                {
                    DebugEx.LogException(e);
                }
            }
        }

        public void RemoveEmptyIntersectionBetweenRoad()
        {
            HashSet<RnIntersection> remove = new();
            foreach (var intersection in Intersections)
            {
                if (intersection.IsEmptyIntersection == false)
                    continue;
                var roads = intersection.Edges
                    .Select(e => e.Road as RnRoad)
                    .Where(n => n != null)
                    .ToHashSet();

                foreach (var r in roads)
                {
                    var other = roads.FirstOrDefault(n => n != r);
                    if (r.Prev == intersection)
                        r.SetPrevNext(other, r.Next);
                    if (r.Next == intersection)
                        r.SetPrevNext(r.Prev, other);
                }

                remove.Add(intersection);
            }

            foreach (var r in remove)
                r.DisConnect(true);
        }

        /// <summary>
        /// 交差点同士が直接つながっている状況において、交差点間のリンクを作成する
        /// </summary>
        public void CreateEmptyRoadBetweenInteraction()
        {
            HashSet<RnIntersection> visited = new();
            foreach (var inter in Intersections)
            {
                if (visited.Contains(inter))
                    continue;
                visited.Add(inter);

                foreach (var neighbor in inter.Neighbors)
                {
                    if (neighbor.Border == null)
                        continue;

                    if ((neighbor.Road is RnIntersection other) == false)
                        continue;

                    if (visited.Contains(other))
                        continue;

                    var otherNeighbor =
                        other.Neighbors.FirstOrDefault(n => n.Border.IsSameLineReference(neighbor.Border) && n.Road == inter);
                    if (otherNeighbor == null)
                        continue;

                    neighbor.Border.GetLerpPoint(0.5f, out var pos);
                    var p = new RnPoint(pos);
                    var emptyWay = new RnWay(RnLineString.Create(Enumerable.Repeat(p, 2), false));
                    var emptyLane = RnLane.CreateEmptyLane(neighbor.Border, emptyWay);
                    var emptyRoad = RnRoad.CreateOneLaneRoad(null, emptyLane);
                    emptyRoad.SetPrevNext(inter, other);
                    neighbor.Road = emptyRoad;
                    otherNeighbor.Road = emptyRoad;
                    AddRoad(emptyRoad);
                }
            }
        }

        /// <summary>
        /// 直接つながった交差点間に挿入された空リンクを削除する
        /// </summary>
        public void RemoveEmptyRoadBetweenIntersection()
        {
            HashSet<RnRoad> remove = new();
            foreach (var road in Roads)
            {
                if (road.IsEmptyRoad == false)
                    continue;

                var inter1 = road.Next as RnIntersection;
                var inter2 = road.Prev as RnIntersection;
                foreach (var n in inter1.Neighbors.Where(n => n.Road == road))
                    n.Road = inter2;
                foreach (var n in inter2.Neighbors.Where(n => n.Road == road))
                    n.Road = inter1;
                road.SetPrevNext(null, null);
                remove.Add(road);
            }

            foreach (var r in remove)
                RemoveRoad(r);
        }


        public void ReBuildIntersectionTracks()
        {
            foreach (var intersection in Intersections)
            {
                intersection.BuildTracks();
            }
        }

        public void AddDefaultTrafficSignalLights()
        {
            foreach (var intersection in intersections)
            {
                TrafficSignalLightController.CreateDefault(intersection);
            }
        }

        public void Clear()
        {
            sideWalks.Clear();
            roads.Clear();
            intersections.Clear();
        }

        /// <summary>
        /// modelからコピーしてくる
        /// </summary>
        /// <param name="model"></param>
        public void CopyFrom(RnModel model)
        {
            // 自身は削除
            Clear();

            // 道路/交差点/歩道をコピー
            foreach (var l in model.Roads)
                AddRoad(l);

            foreach (var n in model.Intersections)
                AddIntersection(n);

            foreach (var n in model.SideWalks)
                AddSideWalk(n);
        }

        /// <summary>
        /// 連続した道路を一つのRoadにまとめる
        /// </summary>
        public void MergeRoadGroup()
        {
            var visitedRoads = new HashSet<RnRoad>();
            var roads = Roads.ToList();
            foreach (var road in roads)
            {
                if (visitedRoads.Contains(road))
                    continue;

                try
                {
                    var roadGroup = road.CreateRoadGroup();
                    foreach (var l in roadGroup.Roads)
                        visitedRoads.Add(l);
                    roadGroup.MergeRoads();
                }
                catch (Exception e)
                {
                    DebugEx.LogException(e);
                }
            }
        }

        /// <summary>
        /// 交差点の形状調整
        /// </summary>
        public void SeparateContinuousBorder()
        {
            foreach (var inter in intersections)
            {
                // 連続した境界線を分離する
                inter.SeparateContinuousBorder();
            }

            foreach (var road in roads)
            {
                road.SeparateContinuousBorder();
            }
        }

        /// <summary>
        /// 道路グループの交差点との境界線の角度を調整する(垂直になるようにする)
        /// </summary>
        public void AdjustRoadGroupBorder()
        {
            var visitedRoads = new HashSet<RnRoad>();
            var roads = Roads.ToList();
            foreach (var road in roads)
            {
                if (visitedRoads.Contains(road))
                    continue;
                var roadGroup = road.CreateRoadGroup();
                foreach (var l in roadGroup.Roads)
                    visitedRoads.Add(l);
                roadGroup.AdjustBorder();
            }
        }

        /// <summary>
        /// roadWidthの道路幅を基準にレーンを分割する
        /// </summary>
        /// <param name="roadWidth"></param>
        /// <param name="rebuildTrack">レーン分割後に関連する交差点のトラックを再生成する</param>
        /// <param name="failedRoads"></param>
        public void SplitLaneByWidth(float roadWidth, bool rebuildTrack, out List<ulong> failedRoads, Func<RnRoadGroup, bool> isTargetLaneSplit)
        {
            failedRoads = new List<ulong>();
            var visitedRoads = new HashSet<RnRoad>();
            foreach (var link in Roads)
            {
                if (visitedRoads.Contains(link))
                    continue;

                try
                {
                    var roadGroup = link.CreateRoadGroup();
                    foreach (var l in roadGroup.Roads)
                        visitedRoads.Add(l);

                    roadGroup.Align();
                    if (roadGroup.IsValid == false)
                        continue;

                    if (roadGroup.Roads.Any(l => l.MainLanes[0].HasBothBorder == false))
                        continue;

                    if (isTargetLaneSplit(roadGroup) == false)
                        continue;

                    var leftCount = roadGroup.GetLeftLaneCount();
                    var rightCount = roadGroup.GetRightLaneCount();
                    // すでにレーンが分かれている場合、左右で独立して分割を行う
                    if (leftCount > 0 && rightCount > 0)
                    {
                        foreach (var dir in new[] { RnDir.Left, RnDir.Right })
                        {
                            var width = roadGroup.Roads.Select(r => r.GetLanes(dir).Sum(l => l.CalcWidth())).Min();
                            var num = (int)(width / roadWidth);
                            // 
                            roadGroup.SetLaneCount(dir, num, rebuildTrack);
                        }
                    }
                    // 
                    else
                    {
                        var width = roadGroup.Roads.Select(l => l.MainLanes.Sum(l => l.CalcWidth())).Min();
                        var num = (int)(width / roadWidth);
                        if (num <= 1)
                            continue;

                        var leftLaneCount = (num + 1) / 2;
                        var rightLaneCount = num - leftLaneCount;
                        roadGroup.SetLaneCount(leftLaneCount, rightLaneCount, rebuildTrack);
                    }

                }
                catch (Exception e)
                {
                    DebugEx.LogException(e);
                    failedRoads.Add(link.DebugMyId);
                }
            }
        }

        public void Check()
        {
            // 隣接情報のチェック
            Dictionary<RnRoadBase, Dictionary<RnRoadBase, List<RnLineString>>> borderTable = new();

            void AddBorderInfo(RnRoadBase road)
            {
                var table = new Dictionary<RnRoadBase, List<RnLineString>>();
                foreach (var a in road.GetBorders())
                {
                    if (a.NeighborRoad == null)
                        continue;
                    table.GetValueOrCreate(a.NeighborRoad).Add(a.BorderWay.LineString);
                }

                borderTable[road] = table;
            }

            foreach (var road in roads)
            {
                road.Check();
                AddBorderInfo(road);
            }

            foreach (var inter in intersections)
            {
                inter.Check();
                AddBorderInfo(inter);
            }

            foreach (var item in borderTable)
            {
                var aKey = item.Key;
                var aVal = item.Value;
                foreach (var aToB in aVal)
                {
                    var bKey = aToB.Key;
                    var bVal = borderTable.GetValueOrDefault(bKey);

                    void ShowErrorLog(string label)
                    {
                        DebugEx.LogError($"{label}[{aKey.GetTargetTransName()}]-[{bKey.GetTargetTransName()}]");
                    }

                    if (bVal == null)
                    {
                        ShowErrorLog("bKey not found");
                        continue;
                    }

                    var bToA = bVal.GetValueOrDefault(aKey) ?? new();

                    if (aToB.Value.Count != bToA.Count)
                    {
                        ShowErrorLog("Border Count Error");
                    }
                    else
                    {
                        foreach (var aBorder in aToB.Value)
                        {
                            if (bToA.Contains(aBorder) == false)
                            {
                                ShowErrorLog("Border Count Error");
                            }
                        }
                    }
                }
            }

            foreach (var sw in sideWalks)
            {
                sw.Check();
            }
        }
    }

    public static class RnModelEx
    {
        /// <summary>
        /// 切断時の端点判定の時の許容誤差
        /// </summary>
        private const float CutIndexTolerance = 1e-5f;

        public enum RoadCutResult
        {
            Success,
            // 道路自体が不正
            InvalidRoad,
            // 不正な歩道を持っている
            InvalidSideWalk,
            // 切断線が不正
            InvalidCutLine,
            // 分断できないレーンがあった
            UnSlicedLaneExist,
            // 一部だけ分断された歩道が存在
            PartiallySlicedSideWalkExist,
            // 切断線が端点と近すぎる(ほぼ分断しない状態)
            TerminateCutLine,
            // 切断線が交差している
            CrossCutLine,

            // 交差点自体が不正
            InvalidIntersection,
            // 交差点の境界線を切断
            IntersectionBorderSliced,
            // 交差点のEdgeが片側だけ切断された
            IntersectionPartiallySlicedEdge,
            // 交差点で複数の入口を切断するのはダメ
            IntersectionMultipleEdgeSliced,
            // 交差点で分断された入口が存在しなかった
            IntersectionNoEdgeSliced,
        }

        /// <summary>
        /// 道路を２か所で水平切断できるかチェックする
        /// </summary>
        /// <param name="self"></param>
        /// <param name="road"></param>
        /// <param name="lineSegment1"></param>
        /// <param name="lineSegment2"></param>
        public static RoadCutResult CanSliceRoadHorizontalAndConvert2Intersection(this RnModel self, RnRoad road,
            LineSegment3D lineSegment1, LineSegment3D lineSegment2)
        {
            if (lineSegment1.TrySegmentIntersectionBy2D(lineSegment2, RnModel.Plane, -1f, out var _))
                return RoadCutResult.CrossCutLine;

            var check1 = self.CanSliceRoadHorizontal(road, lineSegment1, out var inters1);
            if (check1 != RoadCutResult.Success)
                return check1;

            var check2 = self.CanSliceRoadHorizontal(road, lineSegment2, out var inters2);
            if (check2 != RoadCutResult.Success)
                return check2;

            return RoadCutResult.Success;
        }


        /// <summary>
        /// 道路を２か所で水平切断し、中央の道路を交差点にする.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="road"></param>
        /// <param name="lineSegment1"></param>
        /// <param name="lineSegment2"></param>
        public static void SliceRoadHorizontalAndConvert2Intersection(this RnModel self, RnRoad road, LineSegment3D lineSegment1, LineSegment3D lineSegment2)
        {
            var check1 = self.SliceRoadHorizontal(road, lineSegment1);
            if (check1.Result != RoadCutResult.Success)
                return;

            if (self.CanSliceRoadHorizontal(check1.PrevRoad, lineSegment2, out var _) == RoadCutResult.Success)
            {
                var check2 = self.SliceRoadHorizontal(check1.PrevRoad, lineSegment2);
                if (check2.Result == RoadCutResult.Success)
                {
                    self.Convert2Intersection(check2.NextRoad);
                }
            }
            else if (self.CanSliceRoadHorizontal(check1.NextRoad, lineSegment2, out var _) == RoadCutResult.Success)
            {
                var check2 = self.SliceRoadHorizontal(check1.NextRoad, lineSegment2);
                if (check2.Result == RoadCutResult.Success)
                {
                    self.Convert2Intersection(check2.PrevRoad);
                }
            }
            else
            {

            }
        }

        /// <summary>
        /// Roadの水平切断結果
        /// </summary>
        public class SliceRoadHorizontalResult
        {
            // 切断結果
            public RoadCutResult Result { get; set; }

            // 元のRoadのPrev側(元のRoadはこれになる)
            public RnRoad PrevRoad { get; set; }

            // 元のRoadのNext側
            public RnRoad NextRoad { get; set; }
        }

        /// <summary>
        /// roadの水平切断可能かチェックする
        /// </summary>
        /// <param name="self"></param>
        /// <param name="road"></param>
        /// <param name="lineSegment"></param>
        /// <param name="res"></param>
        /// <returns></returns>
        public static RoadCutResult CanSliceRoadHorizontal(this RnModel self, RnRoad road, LineSegment3D lineSegment, out LineCrossPointResult res)
        {
            res = road.GetLaneCrossPoints(lineSegment);
            if (res == null)
                return RoadCutResult.InvalidRoad;

            if (road.IsAllLaneValid == false)
                return RoadCutResult.InvalidRoad;

            // 同じLineStringを２回以上交わってはいけない
            if (res.TargetLines.All(i => i.Intersections.Count <= 1) == false)
                return RoadCutResult.InvalidCutLine;

            var targetLines = res.TargetLines;

            bool IsSliced(RnWay way)
            {
                return way != null && targetLines.Any(t => t.LineString == way.LineString && t.Intersections.Count > 0);
            }

            // 分断されないレーンが存在する
            foreach (var lane in road.AllLanesWithMedian)
            {
                if (lane.BothWays.Any(w => IsSliced(w) == false))
                    return RoadCutResult.UnSlicedLaneExist;
            }

            // 歩道チェック
            foreach (var sw in road.SideWalks)
            {
                if (sw.IsValid == false)
                    return RoadCutResult.InvalidSideWalk;

                // 歩道は角の道だったりすると前後で分かれていたりするので交わらない場合もある
                // ただし、inside/outsideがどっちも交わるかどっちも交わらないかしか許さない
                var slicedCount = sw.SideWays.Count(IsSliced);
                if (!(slicedCount == 0 || slicedCount == 2))
                {
                    DebugEx.DrawArrows(sw.OutsideWay.Vertices, color: Color.cyan, duration: 5);
                    DebugEx.DrawArrows(sw.InsideWay.Vertices, color: Color.magenta, duration: 5);
                    foreach (var x in sw.SideWays)
                    {
                        var l = targetLines.FirstOrDefault(a => a.LineString == x.LineString);
                        if (l == null)
                            continue;
                        foreach (var a in l.Intersections)
                            DebugEx.DrawSphere(a.v, 3f, Color.green, duration: 5);
                    }
                    return RoadCutResult.PartiallySlicedSideWalkExist;
                }
            }

            // LineStringの端点と交わってはいけない
            if (res.TargetLines
                .Where(l => l.Intersections.Any())
                .Any(l =>
                    l.Intersections[0].index <= CutIndexTolerance &&
                    l.Intersections[0].index >= l.LineString.Count - 1 - CutIndexTolerance))
                return RoadCutResult.TerminateCutLine;

            return RoadCutResult.Success;
        }

        /// <summary>
        /// wayのlineStringだけ差し替えて他同じ物を返す
        /// </summary>
        /// <param name="lineString"></param>
        /// <param name="way"></param>
        /// <returns></returns>
        private static RnWay CopyWay(RnLineString lineString, RnWay way)
        {
            if (way == null || lineString == null)
                return null;
            return new RnWay(lineString, way.IsReversed, way.IsReverseNormal);
        }

        /// <summary>
        /// 歩道を分割する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="sideWalks"></param>
        /// <param name="lineSegment2D"></param>
        /// <param name="lineTable"></param>
        /// <param name="newNextRoad"></param>
        /// <param name="isPrevSide"></param>
        private static void SliceSideWalks(
            this RnModel self
            , IEnumerable<RnSideWalk> sideWalks
            , LineSegment2D lineSegment2D
            , Dictionary<RnLineString, (RnLineString prev, RnLineString next, RnPoint midPoint)> lineTable
            , RnRoadBase newNextRoad
            , Func<RnSideWalk, bool> isPrevSide)
        {
            foreach (var sideWalk in sideWalks.ToList())
            {
                // 曲がり角だと歩道が3個所入っていたりすることがある.
                // その場合歩道に関しては交わらない場合もあり得るので

                // 今回交わらない歩道の場合はそのままだが、親が変わるかだけチェックする
                if (lineTable.ContainsKey(sideWalk.InsideWay.LineString) == false &&
                    lineTable.ContainsKey(sideWalk.OutsideWay.LineString) == false)
                {
                    if (isPrevSide(sideWalk) == false)
                    {
                        newNextRoad.AddSideWalk(sideWalk);
                    }

                    continue;
                }

                var inside = lineTable.GetValueOrDefault(sideWalk.InsideWay.LineString);
                var outside = lineTable.GetValueOrDefault(sideWalk.OutsideWay.LineString);

                var nextInsideWay = CopyWay(inside.next, sideWalk.InsideWay);
                var nextOutsideWay = CopyWay(outside.next, sideWalk.OutsideWay);

                var prevInsideWay = CopyWay(inside.prev, sideWalk.InsideWay);
                var prevOutsideWay = CopyWay(outside.prev, sideWalk.OutsideWay);

                var (startEdgeWay, endEdgeWay) = (sideWalk.StartEdgeWay, sideWalk.EndEdgeWay);

                // LineStringのfront側がlineSegmentのどっち側にあるか
                if (startEdgeWay != null)
                {
                    var prevSign = lineSegment2D.Sign(Vector3.Lerp(inside.prev[0], inside.prev[1], 0.5f).Xz());
                    // selfのprev側がlineSegmentのどっち側にあるか
                    var startSign = lineSegment2D.Sign(startEdgeWay[0].Xz());
                    // startがprevと逆なら入れ替える
                    if (prevSign != startSign)
                    {
                        (startEdgeWay, endEdgeWay) = (endEdgeWay, startEdgeWay);
                    }
                }
                else if (endEdgeWay != null)
                {
                    var prevSign = lineSegment2D.Sign(Vector3.Lerp(inside.next[0], inside.next[1], 0.5f).Xz());
                    // selfのprev側がlineSegmentのどっち側にあるか
                    var startSign = lineSegment2D.Sign(endEdgeWay[0].Xz());
                    // startがprevと逆なら入れ替える
                    if (prevSign != startSign)
                    {
                        (startEdgeWay, endEdgeWay) = (endEdgeWay, startEdgeWay);
                    }
                }

                // 切断線の境界
                var midEdgeWay = new RnWay(RnLineString.Create(new[] { inside.midPoint, outside.midPoint }));

                var newSideWalk = RnSideWalk.Create(newNextRoad, nextOutsideWay, nextInsideWay, midEdgeWay, endEdgeWay, sideWalk.LaneType);
                sideWalk.SetSideWays(prevOutsideWay, prevInsideWay);
                sideWalk.SetEdgeWays(startEdgeWay, midEdgeWay);
                self.AddSideWalk(newSideWalk);
            }
        }

        /// <summary>
        /// roadをlineSegmentで水平分割し、２つのRoadに分割する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="road"></param>
        /// <param name="lineSegment"></param>
        public static SliceRoadHorizontalResult SliceRoadHorizontal(this RnModel self, RnRoad road, LineSegment3D lineSegment)
        {
            var check = self.CanSliceRoadHorizontal(road, lineSegment, out var crossPointResult);
            if (check != RoadCutResult.Success)
                return new SliceRoadHorizontalResult { Result = check };

            var lineSegment2D = lineSegment.To2D(RnModel.Plane);

            // key   : 元のLineString
            // value : 分割後のselfのprev/next側のLineString
            Dictionary<RnLineString, (RnLineString prev, RnLineString next, RnPoint midPoint)> lineTable = new();

            // 分割後LineStringがprev/nextどっち側かの判定用
            // PrevBorder上にある全ポイントを持っておく
            var prevBorderPoints = road.GetBorderWays(RnLaneBorderType.Prev)
                .SelectMany(x => x.Points)
                .ToHashSet();
            var nextBorderPoints = road.GetBorderWays(RnLaneBorderType.Next)
                .SelectMany(x => x.Points)
                .ToHashSet();

            // 未決定のもの
            HashSet<LineCrossPointResult.TargetLineInfo> undesideds = new();

            // 片方のLineStringが別のLineStringの部分集合だったりすると
            // 交点が同じでも別のRnPointになる. それを防ぐために共通テーブルを用意する
            Dictionary<Vector3, RnPoint> sharePoints = new();

            void SplitByIndex(RnLineString ls, float index, out RnLineString front, out RnLineString back)
            {
                ls.SplitByIndex(index, out front, out back, v =>
                {
                    if (sharePoints.TryGetValue(v, out var p) == false)
                    {
                        p = new RnPoint(v);
                        sharePoints[v] = p;
                    }
                    return p;
                });
            }

            void AddTable(LineCrossPointResult.TargetLineInfo inter, bool isFrontPrev)
            {
                var item = inter.Intersections.First();
                SplitByIndex(inter.LineString, item.index, out var front, out var back);
                if (isFrontPrev)
                {
                    lineTable[inter.LineString] = (front, back, back.Points[0]);
                }
                else
                {
                    lineTable[inter.LineString] = (back, front, back.Points[0]);
                }
            }
            foreach (var inter in crossPointResult.TargetLines)
            {
                if (inter.Intersections.Any() == false)
                    continue;

                if (prevBorderPoints.Contains(inter.LineString.Points[0]))
                {
                    AddTable(inter, true);

                }
                else if (nextBorderPoints.Contains(inter.LineString.Points[0]))
                {
                    AddTable(inter, false);
                }
                else
                {
                    undesideds.Add(inter);
                }
            }

            foreach (var inter in undesideds)
            {
                var item = inter.Intersections.First();
                SplitByIndex(inter.LineString, item.index, out var front, out var back);
                var prevScore = lineTable
                    .Select(l => front.CalcProximityScore(l.Value.prev))
                    .Where(score => score != null)
                    .Select(s => s.Value)
                    .FindMinOr(a => a, float.MaxValue);

                var nextScore = lineTable
                    .Select(l => front.CalcProximityScore(l.Value.next))
                    .Where(score => score != null)
                    .Select(s => s.Value)
                    .FindMinOr(a => a, float.MaxValue);
                if (prevScore < nextScore)
                {
                    lineTable[inter.LineString] = (front, back, back.Points[0]);
                }
                else
                {
                    lineTable[inter.LineString] = (back, front, back.Points[0]);
                }
            }

            // 新しく生成されるRoad
            var newNextRoad = new RnRoad(road.TargetTrans);

            // roadをprev/next側で分断して, next側をnewRoadにする
            foreach (var lane in road.AllLanesWithMedian)
            {
                // 必ず存在する前提
                var left = lineTable[lane.LeftWay.LineString];
                var right = lineTable[lane.RightWay.LineString];

                var nextLeftWay = CopyWay(left.next, lane.LeftWay);
                var nextRightWay = CopyWay(right.next, lane.RightWay);

                var prevLeftWay = CopyWay(left.prev, lane.LeftWay);
                var prevRightWay = CopyWay(right.prev, lane.RightWay);

                var isReverseLane = lane.IsReverse;

                // 分割個所の境界線
                var midBorderWay = new RnWay(RnLineString.Create(new[] { left.midPoint, right.midPoint }));

                // 順方向ならNext/逆方向ならPrevが中間地点になる
                var laneMidBorderType = isReverseLane ? RnLaneBorderType.Prev : RnLaneBorderType.Next;

                // 以前のボーダーは新しいボーダーに設定する
                var nextBorder = lane.GetBorder(laneMidBorderType);
                lane.SetBorder(laneMidBorderType, midBorderWay);

                var newLane = new RnLane(nextLeftWay, nextRightWay, null, null) { IsReverse = isReverseLane };
                newLane.SetBorder(laneMidBorderType, nextBorder);
                newLane.SetBorder(laneMidBorderType.GetOpposite(), midBorderWay);
                if (lane.IsMedianLane)
                {
                    newNextRoad.SetMedianLane(newLane);
                }
                else
                {
                    newNextRoad.AddMainLane(newLane);
                }

                lane.SetSideWay(RnDir.Left, prevLeftWay);
                lane.SetSideWay(RnDir.Right, prevRightWay);
            }
            newNextRoad.SetPrevNext(road, road.Next);
            road.SetPrevNext(road.Prev, newNextRoad);

            newNextRoad.Prev?.ReplaceNeighbor(road, newNextRoad);
            newNextRoad.Next?.ReplaceNeighbor(road, newNextRoad);
            self.AddRoad(newNextRoad);

            // 歩道周りを処理する

            SliceSideWalks(self, road.SideWalks, lineSegment2D, lineTable, newNextRoad, sw => sw.CalcRoadProximityScore(road) < sw.CalcRoadProximityScore(newNextRoad));

            return new SliceRoadHorizontalResult { Result = RoadCutResult.Success, PrevRoad = road, NextRoad = newNextRoad, };
        }

        public class SliceIntersectionCheckResult
        {
            public LineCrossPointResult CrossPointResult { get; set; }

            // 対象のEdgeGroup
            public RnIntersectionEx.EdgeGroup TargetEdgeGroup { get; set; }

            // 切断されたLineString
            public List<RnLineString> SlicedEdgeLines { get; } = new();

            // 切断対象の歩道
            public List<RnSideWalk> SlicedSideWalks { get; } = new();
        }

        public class SliceIntersectionResult
        {
            // 切断結果
            public RoadCutResult Result { get; set; }

            // 切断で分離されたRoad
            public RnRoad PrevRoad { get; set; }

            // 元のIntersection側
            public RnIntersection NextIntersection { get; set; }
        }

        /// <summary>
        /// intersectionの水平切断可能かチェックする
        /// </summary>
        /// <param name="self"></param>
        /// <param name="inter"></param>
        /// <param name="lineSegment"></param>
        /// <param name="res"></param>
        /// <returns></returns>
        public static RoadCutResult CanSliceIntersectionHorizontal(this RnModel self, RnIntersection inter, LineSegment3D lineSegment, out SliceIntersectionCheckResult res)
        {
            res = new SliceIntersectionCheckResult();
            res.CrossPointResult = inter.GetEdgeCrossPoints(lineSegment);
            if (res.CrossPointResult == null)
                return RoadCutResult.InvalidRoad;

            // 同じLineStringを２回以上交わってはいけない
            if (res.CrossPointResult.TargetLines.All(i => i.Intersections.Count <= 1) == false)
                return RoadCutResult.InvalidCutLine;

            var targetLines = res.CrossPointResult.TargetLines;

            bool IsSliced(RnWay way)
            {
                return way != null && targetLines.Any(t => t.LineString == way.LineString && t.Intersections.Count > 0);
            }

            // 切断していいのは一つの境界線から延びる左右の線のみ
            var edgeGroups = inter.CreateEdgeGroup();

            // 境界線を分断するのは禁止
            if (inter.Neighbors.Any(n => IsSliced(n.Border)))
                return RoadCutResult.IntersectionBorderSliced;

            RnIntersectionEx.EdgeGroup targetEdgeGroup = null;
            foreach (var edgeGroup in edgeGroups.Where(e => e.Key != null))
            {
                // 左右が境界線であってはならない. 境界線ではない場合Edgeは一つのはず
                var left = edgeGroup.LeftSide;
                var right = edgeGroup.RightSide;
                if (left.Key != null || left.Edges.Count != 1)
                    continue;
                if (right.Key != null || right.Edges.Count != 1)
                    continue;

                // どっちも切断されている場所のみ有効
                var leftSliced = IsSliced(left.Edges[0].Border);
                var rightSliced = IsSliced(right.Edges[0].Border);
                if (leftSliced && rightSliced)
                {
                    if (targetEdgeGroup != null)
                        return RoadCutResult.IntersectionMultipleEdgeSliced;

                    targetEdgeGroup = edgeGroup;
                }
            }

            if (targetEdgeGroup == null)
                return RoadCutResult.IntersectionNoEdgeSliced;
            res.TargetEdgeGroup = targetEdgeGroup;
            var leftEdge = targetEdgeGroup.LeftSide;
            var rightEdge = targetEdgeGroup.RightSide;
            // 歩道チェック
            foreach (var sw in inter.SideWalks)
            {
                if (sw.IsValid == false)
                    return RoadCutResult.InvalidSideWalk;

                // 歩道は角の道だったりすると前後で分かれていたりするので交わらない場合もある
                // ただし、inside/outsideがどっちも交わるかどっちも交わらないかしか許さない
                var slicedCount = sw.SideWays.Count(IsSliced);
                if (!(slicedCount == 0 || slicedCount == 2))
                    return RoadCutResult.PartiallySlicedSideWalkExist;

                if (slicedCount > 0)
                    res.SlicedSideWalks.Add(sw);
            }

            // LineStringの端点と交わってはいけない
            if (targetLines
                .Where(l => l.Intersections.Any())
                .Any(l =>
                    l.Intersections[0].index <= CutIndexTolerance &&
                    l.Intersections[0].index >= l.LineString.Count - 1 - CutIndexTolerance))
                return RoadCutResult.TerminateCutLine;

            return RoadCutResult.Success;
        }

        /// <summary>
        /// interをlineSegmentで水平分割し、1つのRoadと1つのIntersectionに分割する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="inter"></param>
        /// <param name="lineSegment"></param>
        public static SliceIntersectionResult SliceIntersectionHorizontal(this RnModel self, RnIntersection inter, LineSegment3D lineSegment)
        {
            var check = self.CanSliceIntersectionHorizontal(inter, lineSegment, out var inters);
            if (check != RoadCutResult.Success)
                return new SliceIntersectionResult { Result = check };

            var lineSegment2D = lineSegment.To2D(RnModel.Plane);

            // key   : 元のLineString
            // value : 分割後のselfのprev/next側のLineString
            Dictionary<RnLineString, (RnLineString prev, RnLineString next, RnPoint midPoint)> lineTable = new();

            // 分割後LineStringがprev/nextどっち側かの判定用
            var prevBorder = inters.TargetEdgeGroup.Edges[0].Border;
            // selfのprev側がlineSegmentのどっち側にあるか
            var prevBorderSign = lineSegment2D.Sign(prevBorder[0].Xz());
            bool IsPrevSide(RnLineString ls)
            {
                // LineStringのfront側がlineSegmentのどっち側にあるか
                var sign = lineSegment2D.Sign(ls[0].Xz());
                return sign == prevBorderSign;
            }

            // LineStringの分割テーブルを作成
            foreach (var cross in inters.CrossPointResult.TargetLines)
            {
                if (cross.Intersections.Any() == false)
                    continue;
                var item = cross.Intersections.First();
                cross.LineString.SplitByIndex(item.index, out var front, out var back);
                var (prev, next) = (front, back);
                var isReversed = IsPrevSide(front) == false;
                if (isReversed)
                    (next, prev) = (prev, next);

                lineTable[cross.LineString] = (prev, next, back.Points[0]);
            }

            // 新しく生成されるRoad
            var newNextRoad = new RnRoad(inter.TargetTrans);



            var leftEdge = inters.TargetEdgeGroup.LeftSide.Edges[0];
            var rightEdge = inters.TargetEdgeGroup.RightSide.Edges[0];

            var leftRes = lineTable[leftEdge.Border.LineString];
            leftEdge.Border = CopyWay(leftRes.next, leftEdge.Border);
            var newRoadLeftWay = new RnWay(leftRes.prev);

            var rightRes = lineTable[rightEdge.Border.LineString];
            rightEdge.Border = CopyWay(rightRes.next, rightEdge.Border);
            //　時計回りなので右側は逆にする
            var newRoadRightWay = new RnWay(rightRes.prev).ReversedWay();

            // 新しい道路のprevBorders(左 -> 右)
            var prevBorders = inters.TargetEdgeGroup.Edges.Select(e => e.Border.ReversedWay()).Reverse().ToList();
            var prevBorderLength = prevBorders.Select(b => b.LineString.CalcLength()).ToList();
            var prevBorderTotalLength = prevBorderLength.Sum();
            var prevBorderRates = new List<float>();
            foreach (var x in prevBorderLength)
            {
                var a = prevBorderRates.LastOrDefault() + x / prevBorderTotalLength;
                prevBorderRates.Add(a);
            }

            var leftPoint = newRoadLeftWay.GetPoint(-1);
            var rightPoint = newRoadRightWay.GetPoint(-1);

            var nextBorderPoints = Enumerable.Range(0, prevBorders.Count + 1)
                .Select(i =>
            {
                if (i == 0)
                    return leftPoint;
                if (i == prevBorders.Count)
                    return rightPoint;

                return new RnPoint(Vector3.Lerp(leftPoint.Vertex, rightPoint.Vertex, prevBorderRates[i - 1]));
            }).ToList();

            var nextBorders = Enumerable.Range(0, nextBorderPoints.Count - 1)
                .Select(i =>
                    new RnWay(RnLineString.Create(new List<RnPoint> { nextBorderPoints[i], nextBorderPoints[i + 1] })))
                .ToList();

            var leftWay = newRoadLeftWay;
            for (var i = 0; i < prevBorders.Count; ++i)
            {
                var prev = prevBorders[i];
                var next = nextBorders[i];

                var rightWay = newRoadRightWay;

                if (i < prevBorders.Count - 1)
                {
                    rightWay = new RnWay(RnEx.CreateInnerLerpLineString(
                        newRoadLeftWay.Vertices.ToList()
                        , newRoadRightWay.Vertices.ToList()
                        , prev.GetPoint(-1)
                        , next.GetPoint(-1)
                        , prev
                        , next
                        , prevBorderRates[i]
                    ));
                }

                newNextRoad.AddMainLane(new RnLane(leftWay, rightWay, prev, next));
                leftWay = rightWay;
            }

            inter.RemoveEdges(n => n.Road == inters.TargetEdgeGroup.Key);
            foreach (var w in nextBorders)
                inter.AddEdge(newNextRoad, w);

            newNextRoad.SetPrevNext(inters.TargetEdgeGroup.Key, inter);

            // ひとつ前の道路の隣接情報を置き換える
            inters.TargetEdgeGroup.Key?.ReplaceNeighbor(inter, newNextRoad);

            self.AddRoad(newNextRoad);
            SliceSideWalks(self, inters.SlicedSideWalks
                , lineSegment2D
                , lineTable
                , newNextRoad
                , sw => sw.CalcRoadProximityScore(inter) < sw.CalcRoadProximityScore(newNextRoad));

            return new SliceIntersectionResult { Result = RoadCutResult.Success, PrevRoad = newNextRoad, NextIntersection = inter, };
        }

        [Serializable]
        public class CalibrateIntersectionBorderOption
        {
            // 交差点の停止線を道路側に移動させる量
            [field: SerializeField]
            public float MaxOffsetMeter { get; set; } = 5;

            // 道路の長さがこれ以下にならないように交差点の移動量を減らす
            [field: SerializeField]
            public float NeedRoadLengthMeter { get; set; } = 23;
        }

        /// <summary>
        /// 道路グループの交差点との境界線の角度を調整する(垂直になるようにする)
        /// </summary>
        public static void CalibrateIntersectionBorderForAllRoad(this RnModel self, CalibrateIntersectionBorderOption option)
        {
            HashSet<RnRoad> prevs = new();
            HashSet<RnRoad> nexts = new();
            var failNum = 0;
            foreach (var road in self.Roads.ToList())
            {
                //self.CalibrateIntersectionBorder(road, option);
                var success =
                    self.TrySliceRoadHorizontalNearByBorder(road, option, out var prev, out var center, out var next);
                if (prev != null)
                    prevs.Add(prev);
                if (next != null)
                    nexts.Add(next);

                if (success == false)
                    failNum++;
            }

            DebugEx.Log($"CalibrateIntersectionBorderForAllRoad: {failNum}/{self.Roads.Count}");

            foreach (var p in prevs)
                p.TryMerge2NeighborIntersection(RnLaneBorderType.Prev);
            foreach (var p in nexts)
                p.TryMerge2NeighborIntersection(RnLaneBorderType.Next);
        }

        /// <summary>
        /// roadが交差点と隣接するとき, 交差点からmeterだけ離れた位置で道路を分割する.
        /// prevSideRoad, centerSideRoad, nextSideRoadの3つの道路が返る
        /// ただし、片方としか分割されなかった場合などはその場所にはnullが入る
        /// </summary>
        /// <param name="self"></param>
        /// <param name="road"></param>
        /// <param name="option"></param>
        /// <param name="prevSideRoad"></param>
        /// <param name="centerSideRoad"></param>
        /// <param name="nextSideRoad"></param>
        /// <returns></returns>
        public static bool TrySliceRoadHorizontalNearByBorder(
            this RnModel self,
            RnRoad road
            , CalibrateIntersectionBorderOption option
            , out RnRoad prevSideRoad
            , out RnRoad centerSideRoad
            , out RnRoad nextSideRoad
            )
        {
            prevSideRoad = nextSideRoad = null;
            centerSideRoad = road;
            bool IsNeighbor(RnRoad r, RnIntersection neighbor)
            {
                return r.Next == neighbor || r.Prev == neighbor;
            }

            // farSide  : 交差点から遠い方の道路(元の道路)
            // nearSide : 交差点側の道路(新しくできた道路)
            (RnRoad farSide, RnRoad nearSide) Check(SliceRoadHorizontalResult res, RnIntersection neighbor)
            {
                // origRoad交差点から遠い方の道路
                var (nearRoad, farRoad) = (res.NextRoad, res.PrevRoad);
                //Assert.IsTrue(IsNeighbor());
                if (IsNeighbor(res.PrevRoad, neighbor))
                    (nearRoad, farRoad) = (farRoad, nearRoad);

                return (farRoad, nearRoad);
            }

            // 不正な道路は対象外
            if (road.IsValid == false)
                return false;

            road.TryGetMergedSideWay(null, out var leftWay, out var rightWay);

            var minLength = Mathf.Min(leftWay.CalcLength(), rightWay.CalcLength());
            // MaxOffsetMeter以下の長さの道路は何もしない
            if (minLength < option.MaxOffsetMeter)
                return false;


            var neighbors = EnumEx.GetValues<RnLaneBorderType>()
                .Select(x => new { n = road.GetNeighborRoad(x) as RnIntersection, t = x })
                .Where(x => x.n != null)
                .ToList();

            if (neighbors.Count == 0)
                return false;

            var offsetLength = Mathf.Max(1f,
                Mathf.Min(option.MaxOffsetMeter, (minLength - option.NeedRoadLengthMeter) / neighbors.Count));

            if (road.Next is RnIntersection nextIntersection)
            {
                if (road.TryGetVerticalSliceSegment(RnLaneBorderType.Next, offsetLength, out var seg))
                {
                    var res = self.SliceRoadHorizontal(road, seg);


                    if (res.Result == RnModelEx.RoadCutResult.Success)
                    {
                        var check = Check(res, nextIntersection);
                        centerSideRoad = check.farSide;
                        nextSideRoad = check.nearSide;
                        road = check.farSide;
                    }
                }
            }
            if (road.Prev is RnIntersection prevIntersection)
            {
                if (road.TryGetVerticalSliceSegment(RnLaneBorderType.Prev, offsetLength, out var seg))
                {
                    var res = self.SliceRoadHorizontal(road, seg);
                    if (res.Result == RnModelEx.RoadCutResult.Success)
                    {
                        var check = Check(res, prevIntersection);
                        centerSideRoad = check.farSide;
                        prevSideRoad = check.nearSide;
                    }
                }
            }

            return true;
        }


        /// <summary>
        /// roadが交差点と隣接するとき, 交差点からmeterだけ離れた位置で道路を分割する.
        /// </summary>
        /// <param name="road"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static void CalibrateIntersectionBorder(this RnModel self, RnRoad road, CalibrateIntersectionBorderOption option)
        {
            if (self.TrySliceRoadHorizontalNearByBorder(road, option, out var prevSideRoad, out var centerSideRoad,
                    out var nextSideRoad) == false)
                return;

            if (nextSideRoad?.Next is RnIntersection)
                nextSideRoad.TryMerge2NeighborIntersection(RnLaneBorderType.Next);
            if (prevSideRoad?.Prev is RnIntersection)
                prevSideRoad.TryMerge2NeighborIntersection(RnLaneBorderType.Prev);
        }

    }
}