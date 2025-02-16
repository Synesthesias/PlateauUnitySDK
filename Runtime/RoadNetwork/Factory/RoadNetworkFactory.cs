using PLATEAU.CityGML;
using PLATEAU.CityInfo;
using PLATEAU.RoadAdjust.RoadNetworkToMesh;
using PLATEAU.RoadNetwork.CityObject;
using PLATEAU.RoadNetwork.Graph;
using PLATEAU.RoadNetwork.Structure;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PLATEAU.RoadNetwork.Factory
{
    [Serializable]
    public partial class RoadNetworkFactory
    {
        /// <summary>
        /// 自動生成バージョン. 作成時に番号埋め込んでおいてどのバージョンで作られたかを見れるようにする.
        /// メジャー/マイナー分けたいので文字列
        /// </summary>
        public static readonly string FactoryVersion = "1.1";

        /// <summary>
        /// 一つの道路の内部に存在するRoadTypeMask.
        /// </summary>
        public static readonly RRoadTypeMask RoadPackTypes = RRoadTypeMask.Road | RRoadTypeMask.Median | RRoadTypeMask.Lane | RRoadTypeMask.Undefined;

        // --------------------
        // start:フィールド
        // --------------------
        // 道路サイズ
        [field: SerializeField]
        public float RoadSize { get; set; } = 3f;

        // 行き止まり検出判定時に同一直線と判断する角度の総和
        [field: SerializeField]
        public float TerminateAllowEdgeAngle { get; set; } = 20f;

        // 行き止まり検出用. 開始線分との角度がこれ以下の間は絶対に中心線にならない
        [field: SerializeField]
        public float TerminateSkipAngle { get; set; } = 5f;

        // Lod1の道の歩道サイズ
        [field: SerializeField]
        public float Lod1SideWalkSize { get; set; } = 3f;

        // Lod1のの道に歩道を追加するときに対象の道路の幅がこれ以下ならしない
        [field: SerializeField]
        public float Lod1SideWalkThresholdRoadWidth { get; set; } = 2f;

        // Lod3の歩道を追加するかどうか
        [field: SerializeField]
        public bool AddSideWalk { get; set; } = true;

        // 中央分離帯をチェックする
        [field: SerializeField]
        public bool CheckMedian { get; set; } = true;

        // LOD3.1以上のLane情報を見る
        [field: SerializeField]
        public bool CheckLane { get; set; } = true;

        // 高速道路を無視するかのフラグ
        [field: SerializeField]
        public bool IgnoreHighway { get; set; } = true;

        // 信号制御器をデフォ値で生成するか
        [field: SerializeField]
        public bool AddTrafficSignalLights { get; set; } = true;

        // 道路のレーン分割を行うかどうか
        [field: SerializeField]
        public bool SplitByRoadSize { get; set; } = true;

        // RGraph作るときのファクトリパラメータ
        [field: SerializeField]
        public RGraphFactory GraphFactory { get; set; } = new();

        // 中間データを保存する
        [field: SerializeField]
        public bool SaveTmpData { get; set; } = false;

        // 平滑化されたtranオブジェクトに対してContourMeshを使用するかどうか(基本true)
        [field: SerializeField]
        public bool UseContourMesh { get; set; } = true;

        // 連続した道路を統合するかどうか
        [field: SerializeField]
        public bool MergeRoadGroup { get; set; } = true;

        [field: SerializeField]
        public bool CalibrateIntersection { get; set; } = true;

        [field: SerializeField]
        public RnModelEx.CalibrateIntersectionBorderOption CalibrateIntersectionOption { get; set; } = new();

        // 道路や交差点で別の道路との境界線が繋がっている場合(間に輪郭線が入っていない)場合に少しずらして挿入する
        [field: SerializeField]
        public bool SeparateContinuousBorder { get; set; } = true;

        // --------------------
        // end:フィールド
        // --------------------

        // 中間データ
        public Work FactoryWork { get; private set; }



        public enum RoadType
        {
            // 通常の道
            Road,
            // 交差点
            Intersection,
            // 行き止まり
            Terminate,
            // 孤島
            Isolated,
        }

        [Serializable]
        public class Work
        {
            public Dictionary<RFaceGroup, Tran> TranMap { get; } = new();

            public Dictionary<RVertex, RnPoint> PointMap { get; } = new();

            // key   : RnLineString.PointsのDebugIdのxor(高速化用)
            // value : RnLineString
            public Dictionary<ulong, List<RnLineString>> RnPointList2LineStringMap { get; } = new();

            // 行き止まり検出用. 角度がこの値以下の場合は同一直線と判断
            public float terminateAllowEdgeAngle = 20f;

            // 行き止まり検出用.開始線分との角度がこれ以下の間は絶対に中心線にならない
            public float terminateSkipAngleDeg = 30f;

            public Tran FindTranOrDefault(RFace face)
            {
                return TranMap.FirstOrDefault(x => x.Key.Faces.Contains(face)).Value;
            }

            public Tran FindTranOrDefault(RFaceGroup faceGroup)
            {
                return TranMap.GetValueOrDefault(faceGroup);
            }

            public RnPoint CreatePoint(RVertex v)
            {
                return PointMap.GetValueOrCreate(v, x => new RnPoint(x.Position));
            }

            private static bool IsEqual(List<RVertex> a, List<RVertex> b, out bool isReverse)
            {
                isReverse = false;
                if (a.Count != b.Count)
                    return false;
                if (a[0] == b[0])
                {
                    isReverse = false;
                    return a.SequenceEqual(b);
                }

                isReverse = true;
                return a.SequenceEqual(Enumerable.Range(0, a.Count).Select(i => b[b.Count - 1 - i]));
            }

            private static bool IsEqual(List<RnPoint> a, List<RnPoint> b, out bool isReverse)
            {
                isReverse = false;
                if (a.Count != b.Count)
                    return false;
                if (a[0] == b[0])
                {
                    isReverse = false;
                    return a.SequenceEqual(b);
                }

                isReverse = true;
                return a.SequenceEqual(Enumerable.Range(0, a.Count).Select(i => b[b.Count - 1 - i]));
            }

            public RnWay CreateWay(List<RnPoint> points)
            {
                return CreateWay(points, out var _);
            }

            public RnWay CreateWay(List<RnPoint> points, out bool isCached, bool useCache = true)
            {
                isCached = false;
                if (points == null)
                    return null;

                // キャッシュ使わない設定
                if (useCache == false)
                {
                    var ls = RnLineString.Create(points, true);
                    return new RnWay(ls, false);
                }
                var key = points.Aggregate(0Lu, (a, p) => a ^ p.DebugMyId);
                var lines = RnPointList2LineStringMap.GetValueOrCreate(key);
                foreach (var line in lines)
                {
                    if (IsEqual(line.Points, points, out var isReverse))
                    {
                        isCached = true;
                        return new RnWay(line, false);
                    }
                }
                var newLine = RnLineString.Create(points, true);
                lines.Add(newLine);
                return new RnWay(newLine, false);
            }

            public RnWay CreateWay(List<RVertex> vertices)
            {
                return CreateWay(vertices, out var _);
            }

            public RnWay CreateWay(List<RVertex> vertices, out bool isCached, bool useCache = true)
            {
                isCached = false;
                if (vertices == null)
                    return null;
                // 頂点無い場合はnull
                if (vertices.Any() == false)
                    return null;

                var points = vertices.Select(CreatePoint).ToList();
                return CreateWay(points, out isCached, useCache);
            }

            public List<RnSideWalk> CreateSideWalk(float minRoadSize, float lod1SideWalkSize)
            {
                if (lod1SideWalkSize < 0)
                    return new List<RnSideWalk>();
                // pos : 移動前の座標, normal : 移動に使った法線ベクトル
                var visited = new Dictionary<RnPoint, (RnPoint pos, List<Vector3> normal)>();

                var ret = new List<RnSideWalk>();
                // LOD1の場合は周りにlod1RoadSize分歩道があると仮定して動かす
                bool MoveWay(RnWay way, RnRoadBase parent, RnSideWalkLaneType laneType, float sideWalkSize)
                {
                    if (way == null)
                        return false;

                    if (sideWalkSize <= 0f)
                        return false;

                    var normals = Enumerable.Range(0, way.Count)
                        .Select(i => way.GetVertexNormal(i).normalized)
                        .ToList();

                    // 始点/終点は除いて法線と逆方向に動かす
                    var outsidePoints = new List<RnPoint> { };
                    foreach (var i in Enumerable.Range(0, way.Count))
                    {
                        var p = way.GetPoint(i);
                        var n = normals[i];
                        if (visited.ContainsKey(p) == false)
                        {
                            var before = new RnPoint(p.Vertex);
                            p.Vertex -= n * sideWalkSize;
                            visited[p] = new(before, new List<Vector3> { n }); //new VertexInfo { Normal = n, Position = last };
                            outsidePoints.Add(before);
                        }
                        else
                        {
                            var last = visited[p];
                            // すでに移動済みの法線成分は除いて移動する
                            foreach (var nn in last.normal)
                            {
                                n -= Vector3.Dot(n, nn) * nn;
                            }
                            p.Vertex -= n * sideWalkSize;
                            last.normal.Add(n);
                            outsidePoints.Add(last.pos);
                        }
                    }
                    var startWay = CreateWay(new List<RnPoint> { outsidePoints[0], way.GetPoint(0) });
                    var endWay = CreateWay(new List<RnPoint> { outsidePoints[^1], way.GetPoint(-1) });
                    var sideWalk = RnSideWalk.Create(parent, CreateWay(outsidePoints), way, startWay, endWay, laneType);
                    ret.Add(sideWalk);
                    return true;
                }


                // 道路の幅によってはLOD1でも歩道を作らない場合もある

                // そのため先に道路に対して歩道を作成する
                // そのあと交差点に対して歩道を作成する
                // その際, 各輪郭線に対してそれに接続する道路が
                // 　1. 歩道を作成した(十分な道幅があった)
                //   2. LOD2以上だった
                // のどれかの場合のみ作成するようにする.
                // 交差点は広く見えても接続する道路が狭い場合.

                Dictionary<RnRoad, float> addedRoads = new();
                foreach (var tran in TranMap.Values)
                {
                    // 移動を適用するのはLODLevel1のみ
                    if (tran.LodLevel != 1)
                    {
                        if (tran.Node is RnRoad r)
                            addedRoads.Add(r, lod1SideWalkSize);
                        continue;
                    }

                    if (tran.Node is RnRoad road)
                    {
                        // 歩道を作ることで道路がminRoadSize以下になる場合は作らない
                        var leftLane = road.MainLanes.FirstOrDefault();
                        var rightLane = road.MainLanes.LastOrDefault();
                        float sideWalkSize;
                        if (leftLane == rightLane)
                        {
                            var leftWidth = Mathf.Min(leftLane.CalcMinWidth(), rightLane.CalcMinWidth());
                            sideWalkSize = Mathf.Min(leftWidth - minRoadSize * 2, lod1SideWalkSize);
                        }
                        else
                        {
                            var leftWidth = leftLane.CalcMinWidth();
                            var rightWidth = rightLane.CalcMinWidth();
                            sideWalkSize = Mathf.Min(leftWidth - minRoadSize, rightWidth - minRoadSize, lod1SideWalkSize);
                        }

                        // 歩道を作ることで道路がminRoadSize以下になる場合は作らない
                        // lod1SizeWalkSize以下になる場合は作らない
                        if (sideWalkSize < lod1SideWalkSize)
                            continue;
                        // ここで適用すると頂点を共有する道路が道が狭くなった後の道路で判定されるので, ここでは幅のみを保存し, 後で適用する
                        //MoveWay(leftLane?.LeftWay, road, RnSideWalkLaneType.LeftLane, sideWalkSize);
                        //MoveWay(rightLane?.RightWay, road, RnSideWalkLaneType.RightLane, sideWalkSize);
                        addedRoads.Add(road, sideWalkSize);
                    }
                }

                // 道路の幅によってはLOD1でも歩道を作らない場合もあるた
                foreach (var tran in TranMap.Values)
                {
                    // 移動を適用するのはLODLevel1のみ
                    if (tran.LodLevel != 1)
                        continue;
                    if (tran.Node is RnRoad road)
                    {
                        if (addedRoads.TryGetValue(road, out var sideWalkSize) == false)
                            continue;
                        var leftLane = road.MainLanes.FirstOrDefault();
                        var rightLane = road.MainLanes.LastOrDefault();
                        MoveWay(leftLane?.LeftWay, road, RnSideWalkLaneType.LeftLane, sideWalkSize);
                        MoveWay(rightLane?.RightWay, road, RnSideWalkLaneType.RightLane, sideWalkSize);
                    }
                    else if (tran.Node is RnIntersection intersection)
                    {
                        foreach (var eg in intersection
                                     .CreateEdgeGroup()
                                     .Where(e => e.IsBorder == false && e.IsValid))
                        {
                            bool IsTarget(RnIntersectionEx.EdgeGroup e, out float swSize)
                            {
                                swSize = 0f;
                                if (e == eg)
                                    return false;

                                if (e.Key is RnRoad r)
                                {
                                    return addedRoads.TryGetValue(r, out swSize);
                                }

                                // 隣がいない or 交差点の場合はOK
                                swSize = lod1SideWalkSize;
                                return true;
                            }

                            // このEdgeGroupと隣接する道路のどっちかが歩道対象じゃない場合は無視する
                            if (!IsTarget(eg.LeftSide, out var leftSwSize) || !IsTarget(eg.RightSide, out var rightSwSize))
                                continue;

                            var sideWalkSize = Mathf.Min(leftSwSize, rightSwSize, lod1SideWalkSize);
                            // lod1SizeWalkSize以下になる場合は作らない
                            if (sideWalkSize < lod1SideWalkSize)
                                continue;

                            foreach (var edge in eg.Edges)
                                MoveWay(edge.Border, intersection, RnSideWalkLaneType.Undefined, sideWalkSize);
                        }
                    }
                }
                return ret;
            }
        }

        public class Tran
        {
            // 親グラフ
            public RGraph Graph { get; }

            // 構成Face
            public HashSet<RFace> Faces => FaceGroup.Faces;

            public RFaceGroup FaceGroup { get; }

            // 構成頂点(Up方向から見て時計回り)
            public List<RVertex> Vertices { get; }

            /// <summary>
            /// 線分情報
            /// </summary>
            public class Line
            {
                public Tran Neighbor { get; set; }

                // 構成Edge
                public List<REdge> Edges { get; } = new List<REdge>();

                // 構成頂点
                public List<RVertex> Vertices { get; } = new List<RVertex>();

                public RnWay Way { get; set; }

                // 他との境界線かどうか
                public bool IsBorder => Neighbor != null;

                public Line Next { get; set; }

                public Line Prev { get; set; }
            }

            // 構成線分(頂点は時計回りになっている)
            public List<Line> Lines { get; } = new List<Line>();

            // 隣接オブジェクトの数
            public int NeighborCount => Lines.Count(l => l.IsBorder);

            // LodLevel
            public int LodLevel => Faces.Any() ? Faces.Max(f => f.LodLevel) : 0;

            /// <summary>
            /// 道路形状タイプ(道路/交差点/行き止まり/孤島)
            /// </summary>
            public RoadType RoadType
            {
                get
                {
                    switch (NeighborCount)
                    {
                        case 0: return RoadType.Isolated;
                        case 1: return RoadType.Terminate;
                        case 2: return RoadType.Road;
                        // 3つ以上の隣接オブジェクトを持つかどうか
                        default: return RoadType.Intersection;
                    };
                }
            }

            public bool IsValid { get; private set; } = true;

            public Work Work { get; }

            // RnIntersection or RnRoad
            public RnRoadBase Node { get; private set; }


            private float GetMedianLength(Line line)
            {
                var ret = line.Vertices
                    .SkipWhile(v => v.GetRoadType(f => Faces.Contains(f)).IsMedian() == false)
                    .TakeWhile(v => v.GetRoadType(f => Faces.Contains(f)).IsMedian())
                    .Aggregate(new { last = (RVertex)null, len = 0f }, (a, e) =>
                    {
                        var len = a.len;
                        if (a.last != null)
                        {
                            len += (e.Position - a.last.Position).magnitude;
                        }

                        return new { last = e, len = len };
                    });
                return ret.len;
            }

            // 中央分離帯の幅を求める
            public float GetMedianWidth()
            {
                var lines = Lines.Where(l => l.IsBorder && GetMedianLength(l) > 0).ToList();
                if (lines.Any() == false)
                    return 0f;

                return lines.Min(GetMedianLength);
            }

            public bool TryGetLaneNum(out int leftLaneNum, out int rightLaneNum)
            {
                leftLaneNum = 0;
                rightLaneNum = 0;
                if (Node is RnRoad road)
                {
                    var prevBorder = road.GetMergedBorder(RnLaneBorderType.Prev);
                    var nextBorder = road.GetMergedBorder(RnLaneBorderType.Next);

                    bool IsPrevSide(REdge e)
                    {
                        if (prevBorder == null)
                            return false;

                        if (nextBorder == null)
                            return true;

                        prevBorder.GetNearestPoint(e.V0.Position, out var _, out float _, out float prevDistance);
                        nextBorder.GetNearestPoint(e.V0.Position, out var _, out float _, out float nextDistance);
                        return prevDistance < nextDistance;
                    }

                    foreach (var line in Lines)
                    {
                        if (line.IsBorder == false)
                            continue;

                        // 同じLaneのFaceかどうか判定
                        bool IsSame(HashSet<RFace> a, HashSet<RFace> b)
                        {
                            if (a == b)
                                return true;
                            if (a.Count != b.Count)
                                return false;
                            return a.All(b.Contains);
                        }
                        var lastLaneFaces = new HashSet<RFace>();
                        List<int> laneCounts = new() { 0 };

                        if (line.Edges.Any() == false)
                            continue;

                        foreach (var edge in line.Edges)
                        {
                            var roadType = edge.GetAnyFaceTypeMaskOrDefault();
                            if (roadType.IsMedian())
                            {
                                if (laneCounts.Count == 1)
                                    laneCounts.Add(0);
                                continue;
                            }


                            var faces = edge.Faces.Where(f => f.RoadTypes.IsLane()).ToHashSet();
                            // 車線じゃない場合はスキップ
                            if (faces.Any() == false)
                                continue;
                            // 同じ車線が複数Edge持っている場合に無駄にカウントしないようにする
                            if (IsSame(lastLaneFaces, faces))
                            {
                                continue;
                            }

                            laneCounts[^1]++;
                            lastLaneFaces = faces;
                        }

                        leftLaneNum = laneCounts[0];
                        rightLaneNum = laneCounts.Count > 1 ? laneCounts[^1] : 0;
                        // Prev側のEdgeで見ている場合
                        // Edgeは時計回りになっているので最初が右車線側
                        if (IsPrevSide(line.Edges[0]))
                            (leftLaneNum, rightLaneNum) = (rightLaneNum, leftLaneNum);
                    }

                    return true;
                }

                return false;
            }


            public Tran(Work work, RGraph graph, RFaceGroup faceGroup)
            {
                Work = work;
                Graph = graph;

                FaceGroup = faceGroup;
                Vertices = faceGroup.ComputeOutlineVertices(f => f.RoadTypes.HasAnyFlag(RRoadTypeMask.SideWalk) == false);

                // 時計回りになるように順番チェック
                if (GeoGraph2D.IsClockwise(Vertices.Select(x => x.Position.Xz())) == false)
                    Vertices.Reverse();
            }

            public void Build()
            {
                Node = CreateRoad();
            }

            public void BuildLine()
            {
                IsValid = BuildLines();
            }

            private RnRoadBase CreateRoad()
            {
                var cityObjectGroup = FaceGroup.CityObjectGroup;
                // 孤立
                if (RoadType == RoadType.Isolated)
                {
                    // 孤立した中央分離帯は道路構造の生成対象から外す
                    // (道路に完全内包する中央分離帯だったりと無意味なので)
                    if (FaceGroup.RoadTypes.IsMedian())
                    {
                        DebugEx.LogWarning($"skip : {cityObjectGroup} is isolated median ");
                        return null;
                    }

                    var way = Work.CreateWay(Vertices);
                    var road = RnRoad.CreateIsolatedRoad(cityObjectGroup, way);
                    return road;
                }

                // 行き止まり
                if (RoadType == RoadType.Terminate)
                {
                    var border = Lines.FirstOrDefault(l => l.IsBorder);
                    var line = Lines.FirstOrDefault(l => l.IsBorder == false);
                    if (line == null)
                    {
                        Debug.LogError($"不正なレーン構成[Terminate] : {cityObjectGroup.name}");
                        return null;
                    }

                    var vertices = line.Vertices.Select(v => RnDef.ToVec2(v.Position)).ToList();
                    var borderResult = RnEx.FindBorderEdges(vertices, Work.terminateAllowEdgeAngle, Work.terminateSkipAngleDeg);
                    var edgeIndices = borderResult.BorderVertexIndices;
                    RnWay AsWay(IEnumerable<int> ind, bool isReverse, bool isRightSide)
                    {
                        var ls = Work.CreateWay(ind.Select(x => line.Vertices[x]).ToList());
                        if (ls == null)
                            return null;
                        return new RnWay(ls.LineString, ls.IsReversed ? !isReverse : isReverse, isRightSide);
                    }

                    var rWay = AsWay(Enumerable.Range(0, edgeIndices[0] + 1), true, true);
                    var lWay = AsWay(Enumerable.Range(edgeIndices.Last(), line.Vertices.Count - edgeIndices.Last()), false, false);
                    var prevBorderWay = AsWay(edgeIndices, true, false);
                    var nextBorderWay = Work.CreateWay(border?.Vertices);
                    var lane = new RnLane(lWay, rWay, prevBorderWay, nextBorderWay);
                    var road = RnRoad.CreateOneLaneRoad(cityObjectGroup, lane);
                    road.SetPrevNext(null, border.Neighbor?.Node);
                    return road;
                }

                // 通常の道
                if (RoadType == RoadType.Road)
                {
                    var lanes = Lines.Where(l => l.IsBorder == false).ToList();
                    var road = new RnRoad(cityObjectGroup);

                    // 同じPrev/Nextの組み合わせでグループ化(順序逆は問わない)
                    var pairs = lanes.GroupBy(l => RnEx.CombSet(l.Prev, l.Next))
                        .Select(g =>
                        {
                            var laneLines = g.ToList();
                            var (left, right) = (laneLines.ElementAtOrDefault(0), laneLines.ElementAtOrDefault(1));
                            if (left?.Way?.IsReversed ?? false)
                                (left, right) = (right, left);
                            return new { leftLine = left, rightLine = right };
                        }).ToList();
                    var pair = pairs.FirstOrDefault(p => p.leftLine != null && p.rightLine != null);
                    if (pair == null)
                        pair = pairs.FirstOrDefault();

                    //Assert.IsTrue(pair != null, $"不正なレーン構成 {cityObjectGroup.name}");
                    if (pair == null)
                        return road;
                    var (leftLine, rightLine) = (pair.leftLine, pair.rightLine);

                    if (leftLine == null && rightLine == null)
                    {
                        Debug.LogError($"不正なレーン構成(Wayの存在しないLane) {cityObjectGroup.name}");
                        return road;
                    }

                    // ログだけ残しておく
                    if (leftLine == null || rightLine == null)
                        Debug.LogWarning($"不正なレーン構成(片側Wayのみ存在) {cityObjectGroup.name}");

                    var line = leftLine ?? rightLine;
                    var prevBorderLine = line?.Prev; ;
                    var nextBorderLine = line?.Next;
                    var prevBorderWay = prevBorderLine?.Way;
                    var nextBorderWay = nextBorderLine?.Way;

                    var leftWay = leftLine?.Way;
                    var rightWay = rightLine?.Way;

                    // 方向そろえる
                    if (leftLine != null && leftLine.Prev != prevBorderLine)
                        leftWay = leftWay.ReversedWay();
                    if (rightLine != null && rightLine.Prev != prevBorderLine)
                        rightWay = rightWay.ReversedWay();
                    if (rightWay != null)
                        rightWay.IsReverseNormal = true;

                    var lane = new RnLane(leftWay, rightWay, prevBorderWay, nextBorderWay);
                    road.AddMainLane(lane);
                    return road;
                }

                // 交差点
                if (RoadType == RoadType.Intersection)
                {
                    var intersection = new RnIntersection(cityObjectGroup);
                    return intersection;
                }
                Debug.LogError($"不正なレーン構成 : {cityObjectGroup.name}");
                return null;
            }

            /// <summary>
            /// 接続情報作成
            /// </summary>
            public void BuildConnection()
            {
                if (Node is RnRoad road)
                {
                    var lane = road.MainLanes.FirstOrDefault();
                    if (lane == null)
                        return;
                    var prev = Lines.Where(l => l.IsBorder && l.Way != null)
                        .FirstOrDefault(l => lane.PrevBorder?.IsSameLineReference(l.Way) ?? false);
                    var next = Lines.Where(l => l.IsBorder && l.Way != null)
                        .FirstOrDefault(l => lane.NextBorder?.IsSameLineReference(l.Way) ?? false);

                    var prevRoad = prev?.Neighbor?.Node;
                    var nextRoad = next?.Neighbor?.Node;
                    road.SetPrevNext(prevRoad, nextRoad);
                }
                else if (Node is RnIntersection intersection)
                {
                    // 境界線情報
                    foreach (var b in Lines)
                    {
                        intersection.AddEdge(b.Neighbor?.Node, b.Way);
                    }
                }
            }

            private bool BuildLines()
            {
                var edges = new REdge[Vertices.Count];
                // index : アウトラインの辺に隣接するTran
                var neighbors = new Tran[edges.Length];
                var success = true;

                for (var i = 0; i < Vertices.Count; i++)
                {
                    var v0 = Vertices[i];
                    var v1 = Vertices[(i + 1) % Vertices.Count];

                    var e = v0.Edges.FirstOrDefault(e => e.IsSameVertex(v0, v1));
                    if (e == null)
                    {
                        Debug.LogError($"ループしていないメッシュ. {FaceGroup.CityObjectGroup.name}");
                        success = false;
                        continue;
                    }

                    edges[i] = e;
                    neighbors[i] = e.Faces.Select(f => Work.FindTranOrDefault(f)).FirstOrDefault(t => t != null && t != this);
                }

                var startIndex = 0;
                for (var i = 1; i < edges.Length; i++)
                {
                    if (neighbors[i] != neighbors[0])
                    {
                        startIndex = i;
                        break;
                    }
                }

                Line line = null;
                foreach (var i in Enumerable.Range(0, edges.Length).Select(i => (i + startIndex) % edges.Length))
                {
                    var v = Vertices[i];
                    var e = edges[i];
                    var n = neighbors[i];
                    // 切り替わり発生したら新しいLineを作成
                    if (line == null || line.Neighbor != n)
                    {
                        if (line != null)
                            line.Vertices.Add(v);
                        var next = new Line { Neighbor = n, Prev = line };
                        if (line != null)
                            line.Next = next;
                        line = next;
                        Lines.Add(line);
                    }

                    line.Edges.Add(e);
                    line.Vertices.Add(v);
                }

                if (line != null && Lines.Count > 1)
                {
                    line.Vertices.Add(Vertices[startIndex]);
                    line.Next = Lines[0];
                    Lines[0].Prev = line;
                }

                // Wayを先に作っておく
                foreach (var l in Lines)
                    l.Way = Work.CreateWay(l.Vertices);
                return success;
            }
        }

        public Task<RnModel> CreateRnModelAsync(RGraph graph)
        {
            try
            {
                // 道路/中央分離帯は一つのfaceGroupとしてまとめる
                var mask = ~RoadPackTypes;
                var faceGroups = graph.GroupBy((f0, f1) =>
                {
                    var m0 = f0.RoadTypes & mask;
                    var m1 = f1.RoadTypes & mask;
                    return m0 == m1;
                });

                var ret = new RnModel { FactoryVersion = FactoryVersion, };
                var work = new Work { terminateAllowEdgeAngle = TerminateAllowEdgeAngle, terminateSkipAngleDeg = TerminateSkipAngle };
                FactoryWork = work;

                foreach (var faceGroup in faceGroups)
                {
                    var roadType = faceGroup.RoadTypes;

                    // 道路を全く含まない時は無視
                    if (roadType.IsRoad() == false)
                        continue;
                    if (roadType.IsSideWalk())
                        continue;
                    // ignoreHighway=trueの時は高速道路も無視
                    if (roadType.IsHighWay() && IgnoreHighway)
                        continue;
                    work.TranMap[faceGroup] = new Tran(work, graph, faceGroup);
                }

                // 作成したTranを元にRoadを作成
                foreach (var tran in work.TranMap.Values)
                    tran.BuildLine();
                foreach (var tran in work.TranMap.Values)
                {
                    tran.Build();
                    if (tran.Node != null)
                        ret.AddRoadBase(tran.Node);
                }

                foreach (var tran in work.TranMap.Values)
                {
                    tran.BuildConnection();
                }

                // 歩道を作成する
                var sideWalks = work.CreateSideWalk(Lod1SideWalkThresholdRoadWidth, Lod1SideWalkSize);
                foreach (var sideWalk in sideWalks)
                    ret.AddSideWalk(sideWalk);
                if (AddSideWalk)
                {
                    foreach (var fg in faceGroups)
                    {
                        if (fg.RoadTypes.IsSideWalk() == false)
                            continue;

                        var parent = work.TranMap.Values.FirstOrDefault(t =>
                            t.FaceGroup.CityObjectGroup == fg.CityObjectGroup && t.Node != null);

                        var neighborCityObjects
                            = parent?.Node?.GetNeighborRoads()?.SelectMany(r => r.TargetTrans)?.ToHashSet();

                        if (fg.CreateSideWalk(out var outsideEdges, out var insideEdges,
                                out var startEdges, out var endEdges, neighborCityObjects) == false)
                            continue;

                        RnWay AsWay(IReadOnlyList<REdge> edges, bool useCache = true)
                        {
                            if (edges.Any() == false)
                                return null;
                            if (RGraphEx.SegmentEdge2Vertex(edges, out var vertices, out var isLoop))
                                return work.CreateWay(vertices, out var _, useCache: useCache);
                            return null;
                        }
                        // 歩道のアウトサイドはこの道路の完全なる外側なので, 他の道路とLineString共通にして影響が出ると困るので独立させる
                        var outsideWay = AsWay(outsideEdges, false);
                        var insideWay = AsWay(insideEdges);
                        var startWay = AsWay(startEdges);
                        var endWay = AsWay(endEdges);


                        RnSideWalkLaneType laneType = RnSideWalkLaneType.Undefined;
                        if (parent?.Node is RnRoad road)
                        {
                            var way = road.GetMergedSideWay(RnDir.Left);
                            if (insideWay != null)
                            {
                                // #NOTE : 自動生成の段階だと線分共通なので同一判定でチェックする
                                // #TODO : 自動生成の段階で分かれているケースが存在するならは点や法線方向で判定するように変える
                                if (way == null)
                                    laneType = RnSideWalkLaneType.Undefined;
                                else if (insideWay.IsSameLineReference(way))
                                    laneType = RnSideWalkLaneType.LeftLane;
                                else
                                    laneType = RnSideWalkLaneType.RightLane;
                            }
                        }

                        var sideWalk = RnSideWalk.Create(parent?.Node, outsideWay, insideWay, startWay, endWay, laneType);
                        ret.AddSideWalk(sideWalk);
                    }
                }

                // 交差点の
                if (SeparateContinuousBorder)
                    ret.SeparateContinuousBorder();

                // 中央分離帯の幅で道路を分割する

                var isLaneSplitRoads = new HashSet<RnRoad>();
                {
                    var visited = new HashSet<RnRoad>();
                    foreach (var n in work.TranMap.Values)
                    {
                        if (n.Node is RnRoad road)
                        {
                            if (visited.Contains(road))
                                continue;

                            var roadGroup = road.CreateRoadGroupOrDefault();
                            if (roadGroup == null)
                                continue;

                            n.TryGetLaneNum(out var leftLaneNum, out var rightLaneNum);
                            if (CheckLane == false)
                                leftLaneNum = rightLaneNum = 0;

                            foreach (var r in roadGroup.Roads)
                                visited.Add(r);

                            if (road.MainLanes.Count == 0)
                                continue;

                            // 中央分離帯の幅が取得できるときは分割
                            var medianWidth = n.GetMedianWidth();
                            var borderWidth = road.MainLanes[0].CalcWidth();
                            var hasLaneInfo = (leftLaneNum + rightLaneNum) >= 1;

                            if (hasLaneInfo)
                            {
                                DebugEx.Log($"LaneNum : {leftLaneNum}/{rightLaneNum} / {road.GetTargetTransName()}");
                                foreach (var r in roadGroup.Roads)
                                {
                                    isLaneSplitRoads.Add(r);
                                }
                            }

                            if (medianWidth > 0f && CheckMedian && borderWidth > medianWidth)
                            {
                                leftLaneNum = Mathf.Max(1, leftLaneNum);
                                rightLaneNum = Mathf.Max(1, rightLaneNum);
                                roadGroup.SetLaneCountWithMedian(leftLaneNum, rightLaneNum, medianWidth / borderWidth);
                            }
                            // レーン数が2以上になる場合だけ分割する
                            else if ((leftLaneNum + rightLaneNum) > 1)
                            {
                                roadGroup.SetLaneCount(leftLaneNum, rightLaneNum);
                            }
                        }
                    }
                }

                // 連続した道路を一つにまとめる
                if (MergeRoadGroup)
                    ret.MergeRoadGroup();


                // 交差点との境界線が垂直になるようにする
                if (CalibrateIntersection && CalibrateIntersectionOption != null)
                {
                    ret.CalibrateIntersectionBorderForAllRoad(CalibrateIntersectionOption);
                }

                // 道路を分割する
                if (SplitByRoadSize)
                {
                    ret.SplitLaneByWidth(RoadSize, false, out var failedLinks, rg =>
                    {
                        return rg.Roads.Any(r => isLaneSplitRoads.Contains(r)) == false;
                    });
                }

                ret.ReBuildIntersectionTracks();

                // 信号制御器をデフォ値で配置する
                if (AddTrafficSignalLights)
                    ret.AddDefaultTrafficSignalLights();

                ret.Check();

                return Task.FromResult(ret);
            }
            catch (Exception e)
            {
                DebugEx.LogException(e);
                throw;
            }
        }

        public class CreateRnModelRequest
        {
            public List<PLATEAUCityObjectGroup> CityObjectGroups { get; set; }
            public GameObject Target { get; set; }

            public PLATEAURnStructureModel Model { get; set; }

            public PLATEAURGraph Graph { get; set; }

            public PLATEAUSubDividedCityObjectGroup SubDividedCityObjectGroup { get; set; }


        }

        public CreateRnModelRequest CreateRequest(List<PLATEAUCityObjectGroup> cityObjectGroups, GameObject target)
        {
            if (!target)
                target = new GameObject("RoadNetworkStructure");
            var ret = new CreateRnModelRequest
            {
                CityObjectGroups = cityObjectGroups,
                Model = target.GetOrAddComponent<PLATEAURnStructureModel>(),
                Target = target,
            };

            if (target)
            {
                if (SaveTmpData)
                {
                    ret.Graph = target.GetOrAddComponent<PLATEAURGraph>();
                    ret.SubDividedCityObjectGroup = target.GetOrAddComponent<PLATEAUSubDividedCityObjectGroup>();
                }
                else
                {
                    // 中間データは削除する
                    foreach (var comp in target.GetComponents<PLATEAURGraph>().ToList())
                    {
                        UnityEngine.Object.DestroyImmediate(comp);
                    }

                    foreach (var comp in target.GetComponents<PLATEAUSubDividedCityObjectGroup>().ToList())
                    {
                        UnityEngine.Object.DestroyImmediate(comp);
                    }
                }
            }
            return ret;
        }


        public async Task<RnModel> CreateRnModelAsync(CreateRnModelRequest req)
        {
            var subDividedRes =
                SubDividedCityObjectFactory.ConvertCityObjects(req.CityObjectGroups, useContourMesh: UseContourMesh);
            var subDividedCityObjects = subDividedRes.ConvertedCityObjects;
            var graph = GraphFactory.CreateGraph(subDividedCityObjects);
            var model = await CreateRnModelAsync(graph);

            if (req.Graph)
                req.Graph.Graph = graph;

            if (req.SubDividedCityObjectGroup)
                req.SubDividedCityObjectGroup.CityObjects = subDividedCityObjects;

            req.Model.RoadNetwork = model;
#if UNITY_EDITOR
            EditorUtility.SetDirty(req.Model);
#endif
            return model;
        }

        /// <summary>
        /// 変換対象かどうか
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static bool IsConvertTarget(PLATEAUCityObjectGroup target)
        {
            return target.transform.childCount == 0
                   && target.CityObjects.rootCityObjects.Any(a => a.CityObjectType == CityObjectType.COT_Road);
        }

        /// <summary>
        /// 変換された道路かどうか
        /// </summary>
        /// <param name="tran"></param>
        /// <returns></returns>
        public static bool IsGeneratedRoad(Transform tran)
        {
            return tran.GetComponent<PLATEAUReproducedRoad>() != null;
        }
    }
}