using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.CityObject;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Graph
{
    public static class RGraphEx
    {
        public readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public RVertex V0 { get; }
            public RVertex V1 { get; }

            public EdgeKey(RVertex v0, RVertex v1)
            {
                V0 = v0;
                V1 = v1;
                if (V0.GetHashCode() > V1.GetHashCode())
                {
                    (V0, V1) = (V1, V0);
                }
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(V0, V1);
            }

            public bool Equals(EdgeKey other)
            {
                // V0/V1が逆でも同じとみなす
                if (Equals(V0, other.V0) && Equals(V1, other.V1))
                    return true;

                if (Equals(V0, other.V1) && Equals(V1, other.V0))
                    return true;

                return false;
            }
        }

        public static RGraph Create(List<(SubDividedCityObject cityObjects, Matrix4x4 mat)> cityObjects, bool useOutline)
        {
            var graph = new RGraph();
            Dictionary<Vector3, RVertex> vertexMap = new Dictionary<Vector3, RVertex>();
            Dictionary<EdgeKey, REdge> edgeMap = new Dictionary<EdgeKey, REdge>();
            foreach (var item in cityObjects)
            {
                var cityObject = item.cityObjects;
                if (!cityObject.CityObjectGroup)
                {
                    Debug.LogWarning($"[{cityObject.Name}] CityObjectGroupがない為. RFace生成はスキップされます.");
                    continue;
                }

                var lodLevel = cityObject.CityObjectGroup.GetLodLevel();
                var roadType = cityObject.GetRoadType(true);
                // transformを適用する
                var mat = item.mat;
                foreach (var mesh in cityObject.Meshes)
                {
                    var face = new RFace(graph, cityObject.CityObjectGroup, roadType, lodLevel);
                    var vertices = mesh.Vertices.Select(v =>
                    {
                        var v4 = mat * v.Xyza(1f);
                        return vertexMap.GetValueOrCreate(v4.Xyz(), k => new RVertex(k));
                    }).ToList();
                    foreach (var s in mesh.SubMeshes)
                    {
                        if (useOutline)
                        {
                            var indexTable = s.CreateOutlineIndices();
                            foreach (var indices in indexTable)
                            {
                                for (var i = 0; i < indices.Count; i++)
                                {
                                    var e0 = edgeMap.GetValueOrCreate(new EdgeKey(vertices[indices[i]], vertices[indices[(i + 1) % indices.Count]]), e => new REdge(e.V0, e.V1));
                                    face.AddEdge(e0);
                                }
                            }
                        }
                        else
                        {
                            for (var i = 0; i < s.Triangles.Count; i += 3)
                            {
                                var e0 = edgeMap.GetValueOrCreate(new EdgeKey(vertices[s.Triangles[i]], vertices[s.Triangles[i + 1]])
                                    , e => new REdge(e.V0, e.V1));
                                var e1 = edgeMap.GetValueOrCreate(new EdgeKey(vertices[s.Triangles[i + 1]], vertices[s.Triangles[i + 2]])
                                    , e => new REdge(e.V0, e.V1));
                                var e2 = edgeMap.GetValueOrCreate(new EdgeKey(vertices[s.Triangles[i + 2]], vertices[s.Triangles[i]])
                                    , e => new REdge(e.V0, e.V1));
                                var edges = new[] { e0, e1, e2 };
                                foreach (var e in edges)
                                {
                                    face.AddEdge(e);
                                }
                            }
                        }

                    }
                    graph.AddFace(face);
                }
            }
            return graph;
        }

        /// <summary>
        /// 輪郭以外の内部点を削除する
        /// </summary>
        /// <param name="self"></param>
        public static void RemoveInnerVertex(this RFace self)
        {
            var outlineVertices = self.ComputeOutlineVertices().ToHashSet();
            foreach (var v in self.CreateVertexSet())
            {
                if (outlineVertices.Contains(v))
                    continue;
                v.DisConnect(true);
            }
        }

        /// <summary>
        /// 輪郭以外の内部点を削除する
        /// </summary>
        /// <param name="self"></param>
        public static void RemoveInnerVertex(this RGraph self)
        {
            foreach (var face in self.Faces)
            {
                face.RemoveInnerVertex();
            }
        }

        /// <summary>
        /// Lod1の外形の頂点を隣接するLOD2以上のポリゴンの頂点に高さを考慮してマージする
        /// 戻り値は削除された頂点
        /// </summary>
        /// <param name="self"></param>
        /// <param name="mergeCellSize"></param>
        /// <param name="mergeCellLength"></param>
        /// <param name="heightTolerance"></param>
        public static HashSet<RVertex> AdjustSmallLodHeight(this RGraph self, float mergeCellSize, int mergeCellLength,
            float heightTolerance)
        {
            using var _ = new DebugTimer("AdjustSmallLodHeight");
            HashSet<RVertex> removed = new();
            // 変換対象の頂点
            HashSet<RVertex> targetVertices = new();
            var table = new Dictionary<Vector2Int, HashSet<RVertex>>();
            foreach (var f in self.Faces)
            {
                // LOD1/2には高さ情報が無いのでheightToleranceで吸着処理をかける
                var maxLod = 2;
                if (f.LodLevel <= maxLod)
                {
                    // 2重実行対策. すでにLOD3の他の頂点にマージされている場合はスキップ
                    targetVertices.UnionWith(f.ComputeConvexHullVertices().Where(v => v.GetMaxLodLevel() <= maxLod));
                }
                else
                {
                    foreach (var v in f.CreateVertexSet())
                    {
                        var key = (v.Position / mergeCellSize).FloorToInt().Xz();
                        table.GetValueOrCreate(key).Add(v);
                    }
                }
            }

            var delta = GeoGraphEx.GetNeighborDistance2D(mergeCellLength);
            var mergedCount = 0;
            foreach (var p in targetVertices)
            {
                var key = (p.Position / mergeCellSize).FloorToInt().Xz();

                RVertex nearest = null;
                float minDistance = float.MaxValue;
                foreach (var k in delta.Select(d => key + d))
                {
                    var t = table.GetValueOrDefault(k);
                    if (t == null)
                        continue;
                    foreach (var v in t)
                    {
                        if (Mathf.Abs(v.Position.y - p.Position.y) > heightTolerance)
                            continue;

                        var d = (v.Position - p.Position).sqrMagnitude;
                        if (d < minDistance)
                        {
                            minDistance = d;
                            nearest = v;
                        }
                    }
                }

                if (nearest != null && nearest != p)
                {
                    p.MergeTo(nearest);
                    mergedCount++;
                    removed.Add(p);
                }
            }
            Debug.Log($"MergeLodPoint: {mergedCount}");
            return removed;
        }

        /// <summary>
        /// 頂点をリダクション処理
        /// </summary>
        /// <param name="self"></param>
        /// <param name="mergeCellSize"></param>
        /// <param name="mergeCellLength"></param>
        /// <param name="midPointTolerance">aとcとしか接続していない点bに対して、a-cの直線との距離がこれ以下だとbをマージする</param>
        public static void VertexReduction(this RGraph self, float mergeCellSize, int mergeCellLength, float midPointTolerance)
        {
            using var _ = new DebugTimer("VertexReduction");
            while (true)
            {
                var vertices = self.GetAllVertices().ToList();

                var vertexTable = GeoGraphEx.MergeVertices(vertices.Select(v => v.Position), mergeCellSize, mergeCellLength);
                var vertex2RVertex = vertexTable.Values.Distinct().ToDictionary(v => v, v => new RVertex(v));

                var afterCount = vertex2RVertex.Count +
                                 vertices.Count(v => vertexTable.ContainsKey(v.Position) == false);
                Debug.Log($"MergeVertices: {vertices.Count} -> {afterCount}");
                foreach (var v in vertices)
                {
                    if (vertexTable.TryGetValue(v.Position, out var dst))
                    {
                        v.MergeTo(vertex2RVertex[dst]);
                    }
                }
                if (vertices.Count == afterCount)
                    break;
            }
            // a-b-cのような直線状の頂点を削除する
            while (true)
            {
                var vertices = self.Faces
                    .SelectMany(f => f.Edges)
                    .SelectMany(e => e.Vertices)
                    .Where(v => v.Edges.Count == 2)
                    .Distinct()
                    .ToList();

                var sqrLen = midPointTolerance * midPointTolerance;
                var count = 0;
                foreach (var v in vertices)
                {
                    var neighbor = v.GetNeighborVertices().ToList();
                    if (neighbor.Count != 2)
                        continue;

                    // 中間点があってもほぼ直線だった場合は中間点は削除する
                    var segment = new LineSegment3D(neighbor[0].Position, neighbor[1].Position);
                    var p = segment.GetNearestPoint(v.Position);
                    if ((p - v.Position).sqrMagnitude < sqrLen)
                    {
                        v.MergeTo(neighbor[0]);
                        count++;
                    }
                }
                Debug.Log($"RemoveMidPoint : {vertices.Count} -> {vertices.Count - count}");
                if (count == 0)
                    break;
            }
#if false
            while (true)
            {
                var vertices = self.GetAllVertices().ToList();
                Dictionary<REdge, HashSet<RVertex>> edgeInsertMap = new();
                foreach (var v in vertices)
                {
                    var edges = v.Edges.ToList();
                    for (var i = 0; i < v.Edges.Count - 1; i++)
                    {
                        var e0 = edges[i];
                        var v0 = e0.GetOppositeVertex(v);
                        var s0 = new LineSegment3D(v.Position, v0.Position);
                        for (var j = i + 1; j < edges.Count; j++)
                        {
                            var e1 = edges[j];
                            var v1 = e1.GetOppositeVertex(v);
                            if (v0 == v1)
                                continue;

                            var s1 = new LineSegment3D(v.Position, v1.Position);
                            if (s0.Magnitude < s1.Magnitude)
                            {
                                var n = s1.GetNearestPoint(v0.Position);
                                if ((n - v0.Position).magnitude < 0.2f)
                                {
                                    edgeInsertMap.GetValueOrCreate(e1).Add(v0);
                                }
                            }
                            else
                            {
                                var n = s0.GetNearestPoint(v1.Position);
                                if ((n - v1.Position).magnitude < 0.2f)
                                {
                                    edgeInsertMap.GetValueOrCreate(e0).Add(v1);
                                }
                            }
                        }
                    }
                }
                if (edgeInsertMap.Any() == false)
                    break;
                foreach (var e in edgeInsertMap)
                {
                    var sortedV = e.Value.OrderBy(p => (p.Position - e.Key.V0.Position).sqrMagnitude).ToList();

                    var edge = e.Key;
                    foreach (var v in sortedV)
                    {
                        edge = edge.SplitEdge(v);
                    }
                }

                self.EdgeReduction();
            }
#endif
        }

        /// <summary>
        /// 辺のリダクション処理（同じ頂点を持つ辺をマージする)
        /// </summary>
        /// <param name="self"></param>
        public static void EdgeReduction(this RGraph self)
        {
            using var _ = new DebugTimer("EdgeReduction");
            var edges = self.GetAllEdges().ToList();
            var edgeTable = new Dictionary<EdgeKey, HashSet<REdge>>();

            foreach (var e in edges)
            {
                var key = new EdgeKey(e.V0, e.V1);
                edgeTable.GetValueOrCreate(key).Add(e);
            }
            Debug.Log($"MergeEdges: {edges.Count} -> {edgeTable.Count}");
            foreach (var e in edgeTable.Where(e => e.Value.Count > 1))
            {
                var dst = e.Value.First();
                var remove = e.Value.Skip(1).ToList();

                foreach (var r in remove)
                {
                    r.MergeTo(dst);
                }
            }
        }

        /// <summary>
        /// 各Faceに対して以下のような飛び出た辺をFaceから取り除く
        /// o-o
        /// | |
        /// o-o---o ←の様な飛び出た頂点を削除する 
        /// </summary>
        /// <param name="self"></param>
        public static void RemoveIsolatedEdgeFromFace(this RGraph self)
        {
            using var _ = new DebugTimer("RemoveIsolatedEdgeFromFace");
            foreach (var f in self.Faces)
            {
                f.RemoveIsolatedEdge();
            }

            foreach (var f in self.Faces.Where(f => f.Edges.Count == 0).ToList())
            {
                self.RemoveFace(f);
            }
        }

        /// <summary>
        /// o-o
        /// | |
        /// o-o---o ←の様な飛び出た辺をFaceから削除する.
        /// 戻り値は削除された辺の数
        /// </summary>
        /// <param name="self"></param>
        public static HashSet<REdge> RemoveIsolatedEdge(this RFace self)
        {
            bool IsIsolatedEdge(REdge edge)
            {
                // edgeのどっちかの頂点がedgeでしかselfに繋がっていない場合は孤立している
                return edge.Vertices
                    .Any(v =>
                        v == null ||
                        !v.Edges.Where(e => e != edge).Any(e => e.Faces.Contains(self)));
            }

            Queue<REdge> removeEdgeQueue = new();
            foreach (var edge in self.Edges)
            {
                if (IsIsolatedEdge(edge))
                {
                    removeEdgeQueue.Enqueue(edge);
                }
            }

            HashSet<REdge> removeEdgeSet = new();
            HashSet<REdge> disconnectedEdges = new();
            while (removeEdgeQueue.Any())
            {
                var edge = removeEdgeQueue.Dequeue();
                // すでに削除済みの場合は無視
                if (removeEdgeSet.Add(edge) == false)
                    continue;
                self.RemoveEdge(edge);

                // edgeが削除されたことにより別の辺が孤立するかもしれないのでそれも削除キューに入れる
                // 以下のような場合, b-cの辺を削除するとa-bの辺も孤立する
                // o-o
                // | |
                // o-o-a-b-c ←の様な飛び出た辺をFaceから削除する.
                foreach (var e in edge.GetNeighborEdges())
                {
                    if (IsIsolatedEdge(e) == false)
                        continue;
                    removeEdgeQueue.Enqueue(e);
                }

                // どの面にも所属しない辺になったら削除する
                if (edge.Faces.Count == 0)
                {
                    edge.DisConnect();
                    disconnectedEdges.Add(edge);
                }
            }
            //if (removeEdgeSet.Any())
            //    DebugEx.Log($"[{self.GetDebugLabelOrDefault()}] Remove edges {removeEdgeSet.Count}");
            if (disconnectedEdges.Any())
                DebugEx.Log($"[{self.GetDebugLabelOrDefault()}] Disconnected edges {disconnectedEdges.Count}");
            return disconnectedEdges;
        }

        /// <summary>
        /// Outsideの無い歩道に対して微小な辺を追加する
        /// </summary>
        /// <param name="swFace"></param>
        /// <exception cref="InvalidDataException"></exception>
        public static void ModifySideWalkShape(RFace swFace)
        {
            // 微小な量だけ移動する
            float moveDeltaMeter = 1e-3f;

            if (TryGroupBySideWalkEdge(swFace, out var edgeGroups) == false)
                return;

            // 外/内/境界線x2あればOK
            if (edgeGroups.Count == 4)
                return;
            for (var i = 0; i < edgeGroups.Count; ++i)
            {
                var g0 = edgeGroups[i];
                var g1 = edgeGroups[(i + 1) % edgeGroups.Count];

                // 境界線が連続する場合(外側/内側の辺が存在しない)
                // 微小な辺を挿入する
                if (g0.Key.Type == g1.Key.Type && g0.Key.Type == SideWalkEdgeKeyType.Border)
                {
                    var e0 = g0.Edges[^1];
                    var e1 = g1.Edges[0];
                    if (e0.IsShareAnyVertex(e1, out var originVertex) == false)
                        throw new InvalidDataException($"[{swFace.CityObjectGroup.name}] Invalid EdgeGroup");

                    var v0 = e0.GetOppositeVertex(originVertex);
                    var v1 = e1.GetOppositeVertex(originVertex);

                    var ray0 = new Ray(originVertex.Position, v0.Position - originVertex.Position).To2D(RnDef.Plane);
                    var ray1 = new Ray(originVertex.Position, v1.Position - originVertex.Position).To2D(RnDef.Plane);
                    var ray = GeoGraph2D.LerpRay(ray0, ray1, 0.5f);

                    var d = RnDef.ToVec3(Vector2Ex.Rotate(ray.direction, 90)).normalized;

                    var newVertex = new RVertex(originVertex.Position + d * moveDeltaMeter);

                    var isNewVertexLeftSide = ray.IsPointOnLeftSide(RnDef.ToVec2(newVertex.Position));

                    foreach (var e in originVertex.Edges.ToList())
                    {
                        var v = e.GetOppositeVertex(originVertex);
                        // 新しい頂点と同じ側にある辺はそっちにつなぐようにする
                        if (ray.IsPointOnLeftSide(RnDef.ToVec2(v.Position)) == isNewVertexLeftSide)
                        {
                            e.ChangeVertex(originVertex, newVertex);
                        }
                    }

                    // 両方の頂点が所属するFaceには今回作成されたEdgeも追加する(分離しないように)
                    var commonFaces = newVertex.GetFaces().ToHashSet();
                    commonFaces.IntersectWith(originVertex.GetFaces().ToHashSet());
                    var newEdge = new REdge(newVertex, originVertex);
                    foreach (var face in commonFaces)
                    {
                        face.AddEdge(newEdge);
                    }
                    // デバッグ用
                    newVertex.DebugMemo = "SideWalkInsert";
                    break;
                }
            }
        }

        /// <summary>
        /// 歩道部分のうちOutSideWalk
        /// </summary>
        /// <param name="self"></param>
        public static void ModifySideWalkShape(this RGraph self)
        {
            var sideWalkFaces = self.Faces.Where(f => f.RoadTypes.IsSideWalk()).ToList();
            foreach (var swFace in sideWalkFaces)
            {
                ModifySideWalkShape(swFace);
            }
        }

        /// <summary>
        /// 同じPLATEAUCityObjectGroupのFaceをpredicateのルールに従って一つのRFaceGroupにする.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="isMatch"></param>
        public static List<RFaceGroup> GroupBy(this RGraph self, Func<RFace, RFace, bool> isMatch)
        {
            var ret = new List<RFaceGroup>(self.Faces.Count);
            foreach (var group in self.Faces.GroupBy(f => f.CityObjectGroup))
            {
                var faces = group.ToHashSet();
                while (faces.Any())
                {
                    var queue = new Queue<RFace>();
                    queue.Enqueue(faces.First());
                    faces.Remove(faces.First());
                    var g = new HashSet<RFace>();
                    while (queue.Any())
                    {
                        var f0 = queue.Dequeue();
                        g.Add(f0);
                        foreach (var f1 in faces)
                        {
                            // 繋がっていないFaceは無視する
                            if (IsShareEdge(f0, f1) && isMatch(f0, f1))
                            {
                                g.Add(f1);
                                queue.Enqueue(f1);
                                // ここではfacesから削除できない(イテレート中だから)
                            }
                        }

                        foreach (var f in queue)
                            faces.Remove(f);
                    }

                    ret.Add(new RFaceGroup(self, group.Key, g));
                }

            }
            return ret;
        }

        public static void Optimize(this RGraph self, float mergeCellSize, int mergeCellLength, float midPointTolerance, float lod1HeightTolerance)
        {
            self.AdjustSmallLodHeight(mergeCellSize, mergeCellLength, lod1HeightTolerance);
            self.EdgeReduction();
            self.VertexReduction(mergeCellSize, mergeCellLength, midPointTolerance);
            self.RemoveIsolatedEdgeFromFace();
            self.EdgeReduction();
            self.InsertVertexInNearEdge(midPointTolerance);
            self.EdgeReduction();
            self.SeparateFaces();
        }

        /// <summary>
        /// 各頂点と辺の当たり判定チェック. 距離誤差許容量はtolerance
        /// </summary>
        /// <param name="self"></param>
        /// <param name="tolerance"></param>
        public static void InsertVertexInNearEdge(this RGraph self, float tolerance)
        {
            using var _ = new DebugTimer("InsertVertexInNearEdge");
            var vertices = self.GetAllVertices().ToList();

            var comp = Comparer<float>.Default;
            int Compare(RVertex v0, RVertex v1)
            {
                var x = comp.Compare(v0.Position.x, v1.Position.x);
                if (x != 0)
                    return x;
                var z = comp.Compare(v0.Position.z, v1.Position.z);
                if (z != 0)
                    return z;
                return comp.Compare(v0.Position.y, v1.Position.y);
            }
            vertices.Sort(Compare);

            var queue = new HashSet<REdge>();

            Dictionary<REdge, HashSet<RVertex>> edgeInsertMap = new();
            var threshold = tolerance * tolerance;
            for (var i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                // 新規追加分
                var addEdges = new List<REdge>();
                var removeEdges = new HashSet<REdge>();
                foreach (var e in v.Edges)
                {
                    // vと反対側の点を見る
                    var o = e.V0 == v ? e.V1 : e.V0;
                    var d = Compare(v, o);
                    // vが開始点の辺を追加する
                    if (d < 0)
                        addEdges.Add(e);
                    // vが終了点の辺を取り出す
                    else if (d > 0)
                        removeEdges.Add(e);
                }
                foreach (var remove in removeEdges)
                    queue.Remove(remove);

                foreach (var e in queue)
                {
                    if (e.V0 == v || e.V1 == v)
                        continue;

                    var s = new LineSegment3D(e.V0.Position, e.V1.Position);
                    var near = s.GetNearestPoint(v.Position);
                    if ((near - v.Position).sqrMagnitude < threshold)
                    {
                        edgeInsertMap.GetValueOrCreate(e).Add(v);
                    }
                }

                foreach (var add in addEdges)
                    queue.Add(add);
            }

            foreach (var e in edgeInsertMap)
            {
                e.Key.InsertVertices(e.Value);
            }
        }

        /// <summary>
        /// 交差する辺の交点に頂点を追加する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="heightTolerance">交点が高さ方向にずれていた時の許容量</param>
        public static void InsertVerticesInEdgeIntersection(this RGraph self, float heightTolerance)
        {
            var vertices = self.GetAllVertices().ToList();

            var comp = Comparer<float>.Default;
            int Compare(RVertex v0, RVertex v1)
            {
                var x = comp.Compare(v0.Position.x, v1.Position.x);
                if (x != 0)
                    return x;
                var z = comp.Compare(v0.Position.z, v1.Position.z);
                if (z != 0)
                    return z;
                return comp.Compare(v0.Position.y, v1.Position.y);
            }
            vertices.Sort(Compare);

            var queue = new HashSet<REdge>();

            Dictionary<REdge, HashSet<RVertex>> edgeInsertMap = new();
            Dictionary<Vector3, RVertex> vertexMap = new();
            for (var i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                // 新規追加分
                var addEdges = new List<REdge>();
                var removeEdges = new HashSet<REdge>();
                foreach (var e in v.Edges)
                {
                    // vと反対側の点を見る
                    var o = e.V0 == v ? e.V1 : e.V0;
                    var d = Compare(v, o);
                    // vが開始点の辺を追加する
                    if (d < 0)
                        addEdges.Add(e);
                    // vが終了点の辺を取り出す
                    else if (d > 0)
                        removeEdges.Add(e);
                }
                bool NearlyEqual(float a, float b)
                {
                    return Mathf.Abs(a - b) < 1e-3f;
                }

                // 今回除かれる線分同士はチェックしない(vで交差しているから)
                var targets = queue.Where(e =>
                {
                    // vを端点に持つ辺は無視
                    if (e.V0 == v || e.V1 == v)
                        return false;
                    return removeEdges.Contains(e) == false;
                }).ToList();
                foreach (var e0 in removeEdges)
                {
                    var s0 = new LineSegment3D(e0.V0.Position, e0.V1.Position);
                    foreach (var e1 in targets)
                    {
                        var s1 = new LineSegment3D(e1.V0.Position, e1.V1.Position);
                        // e0とe1が共有している頂点がある場合は無視
                        if (e0.IsShareAnyVertex(e1, out var shareV))
                        {
                            //var (sv, s) = s0.Magnitude < s1.Magnitude
                            //    ? (e0.GetOppositeVertex(shareV), s1)
                            //    : (e1.GetOppositeVertex(shareV), s0);
                            //if ((s.GetNearestPoint(sv.Position) - sv.Position).sqrMagnitude < shareMid)
                            //{
                            //    edgeInsertMap.GetValueOrCreate(e0).Add(sv);
                            //}

                            continue;
                        }

                        if (s0.TrySegmentIntersectionBy2D(s1, RnDef.Plane, heightTolerance, out var intersection,
                                out var t1, out var t2))
                        {
                            // お互いの端点で交差している場合は無視
                            if ((NearlyEqual(t1, 0) || NearlyEqual(t1, 1)) && (NearlyEqual(t2, 0) || NearlyEqual(t2, 1)))
                                continue;
                            var p = vertexMap.GetValueOrCreate(intersection, k => new RVertex(k));
                            // #TODO : 0 or 1で交差した場合を考慮
                            edgeInsertMap.GetValueOrCreate(e0).Add(p);
                            edgeInsertMap.GetValueOrCreate(e1).Add(p);
                        }
                    }
                }

                foreach (var add in addEdges)
                    queue.Add(add);

                foreach (var remove in removeEdges)
                    queue.Remove(remove);
            }

            Debug.Log($"Add Vertex [{vertexMap.Count}]");
            foreach (var e in edgeInsertMap)
            {
                e.Key.InsertVertices(e.Value);
            }
        }

        /// <summary>
        /// 辺の間にverticesを追加して分割する. verticesはself.V0 ~ V1の間にある前提
        /// </summary>
        /// <param name="self"></param>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static List<REdge> InsertVertices(this REdge self, IEnumerable<RVertex> vertices)
        {
            var ret = new List<REdge>();
            var o = self.V0.Position;
            var edge = self;
            ret.Add(edge);
            // V0 -> V1の順に並ぶようにして分割する.
            foreach (var v in vertices.OrderBy(v => (v.Position - o).sqrMagnitude))
            {
                edge = edge.SplitEdge(v);
                //DebugEx.DrawSphere(v.Position, 2f, color: Color.green, 30f);
                ret.Add(edge);
            }

            return ret;
        }

        /// <summary>
        /// Face内で非連結な部分を分離する
        /// </summary>
        /// <param name="self"></param>
        public static void SeparateFaces(this RGraph self)
        {
            using var _ = new DebugTimer("SeparateFaces");
            foreach (var p in self.Faces.ToList())
            {
                p.Separate();
            }
        }

        /// <summary>
        /// facesのアウトライン頂点を計算する
        /// </summary>
        /// <param name="faces"></param>
        /// <returns></returns>
        private static List<RVertex> ComputeOutlineVertices(IList<RFace> faces)
        {
            var vertices = faces
                .SelectMany(f => f.Edges.SelectMany(e => e.Vertices))
                .Where(v => v != null)
                .ToHashSet();
            var edges = faces
                .SelectMany(f => f.Edges)
                .ToHashSet();
            var res = GeoGraph2D.ComputeOutline(
                vertices
                , v => v.Position
                , RnDef.Plane
                , v => v.Edges.Where(e => edges.Contains(e)).Select(e => e.GetOppositeVertex(v)).Where(n => n != null));
            return res.Outline ?? new List<RVertex>();
        }

        /// <summary>
        /// アウトライン頂点を計算する
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static List<RVertex> ComputeOutlineVertices(this RFace self)
        {
            return ComputeOutlineVertices(new RFace[] { self });
        }

        /// <summary>
        /// faceGroupの中のpredicateで指定されたRFaceのアウトライン頂点を計算する
        /// </summary>
        /// <param name="faceGroup"></param>
        /// <param name="predicate">対象RFace</param>
        /// <returns></returns>
        public static List<RVertex> ComputeOutlineVertices(this RFaceGroup faceGroup, Func<RFace, bool> predicate)
        {
            var faces = faceGroup.Faces.Where(predicate).ToList();
            return ComputeOutlineVertices(faces);
        }

        /// <summary>
        /// CityObjectGroupに属する面のアウトライン頂点を計算する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="cityObjectGroup"></param>
        /// <param name="roadTypes"></param>
        /// <param name="removeRoadTypes"></param>
        /// <returns></returns>
        public static List<RVertex> ComputeOutlineVerticesByCityObjectGroup(this RGraph self, PLATEAUCityObjectGroup cityObjectGroup, RRoadTypeMask roadTypes, RRoadTypeMask removeRoadTypes)
        {
            var faces = self
                .Faces
                .Where(f => f.CityObjectGroup == cityObjectGroup && f.RoadTypes.HasAnyFlag(roadTypes) && f.RoadTypes.HasAnyFlag(removeRoadTypes) == false)
                .ToList();
            return ComputeOutlineVertices(faces);
        }

        /// <summary>
        /// xz平明上での凸包頂点を計算する
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static List<RVertex> ComputeConvexHullVertices(this RFace self)
        {
            var vertices = self.CreateVertexSet();
            return GeoGraph2D.ComputeConvexVolume(vertices, v => v.Position, AxisPlane.Xz, 1e-3f);
        }

        /// <summary>
        /// aとbの間に共通の辺があるかどうか
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsShareEdge(RFace a, RFace b)
        {
            // #TODO : 共通の辺ではなく共通の頂点になっているが正しいのかチェック
            return a.Edges.SelectMany(e => e.Vertices).Any(v => b.Edges.SelectMany(e => e.Vertices).Contains(v));
        }

        /// <summary>
        /// selfの非連結な部分を分離する
        /// </summary>
        /// <param name="self"></param>
        public static void Separate(this RFace self)
        {
            var edges = self.Edges.ToHashSet();
            if (edges.Any() == false)
                return;
            List<HashSet<REdge>> separatedEdges = new();
            while (edges.Any())
            {
                var queue = new Queue<REdge>();
                queue.Enqueue(edges.First());
                edges.Remove(edges.First());
                var subFace = new HashSet<REdge>();
                while (queue.Any())
                {
                    var edge = queue.Dequeue();
                    subFace.Add(edge);
                    foreach (var e in edge.GetNeighborEdges())
                    {
                        // すでに分離済み or 別のFaceに属している辺は無視
                        if (edges.Contains(e))
                        {
                            edges.Remove(e);
                            queue.Enqueue(e);
                        }
                    }
                }
                separatedEdges.Add(subFace);
            }

            if (separatedEdges.Count <= 1)
                return;

            // #TODO : この処理はここではいらないはず
            //       : この次のfor文の中でRemoveすれば良いはず
            //       : (Faceの全てのEdgeはseparatedEdgesに含まれているはずなので)
            // selfは0番目のものだけ残す
            foreach (var e in self.Edges.Where(e => separatedEdges[0].Contains(e) == false).ToList())
                self.RemoveEdge(e);

            for (var i = 1; i < separatedEdges.Count; i++)
            {
                var face = new RFace(self.Graph, self.CityObjectGroup, self.RoadTypes, self.LodLevel);
                foreach (var e in separatedEdges[i])
                    face.AddEdge(e);
                self.Graph.AddFace(face);
            }
        }

        /// <summary>
        /// 頂点を参照する面のタイプマスクをorで取得
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RRoadTypeMask GetAnyFaceTypeMaskOrDefault(this RVertex self)
        {
            if (self == null)
                return RRoadTypeMask.Empty;
            return self.Edges.SelectMany(e => e.Faces).Aggregate(RRoadTypeMask.Empty, (current, f) => current | f.RoadTypes);
        }

        /// <summary>
        /// 頂点を参照する面のタイプマスクをandで取得
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RRoadTypeMask GetAllFaceTypeMaskOrDefault(this RVertex self)
        {
            if (self == null)
                return RRoadTypeMask.Empty;
            return self.Edges.SelectMany(e => e.Faces).Aggregate(RRoadTypeMask.All, (current, f) => current & f.RoadTypes);
        }

        /// <summary>
        /// 頂点のタイプマスクを取得する. anyOrAllがtrueの時は全ての面のタイプマスクを取得する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="useAnyFaceType"></param>
        /// <returns></returns>
        public static RRoadTypeMask GetTypeMaskOrDefault(this RVertex self, bool useAnyFaceType = false)
        {
            return useAnyFaceType ? self.GetAnyFaceTypeMaskOrDefault() : self.GetAllFaceTypeMaskOrDefault();
        }

        /// <summary>
        /// 辺を参照する面のタイプマスクをorで取得
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RRoadTypeMask GetAnyFaceTypeMaskOrDefault(this REdge self)
        {
            if (self == null)
                return RRoadTypeMask.Empty;
            return self.Faces.Aggregate(RRoadTypeMask.Empty, (current, f) => current | f.RoadTypes);
        }

        /// <summary>
        /// 辺を参照する面のタイプマスクをandで取得
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RRoadTypeMask GetAllFaceTypeMaskOrDefault(this REdge self)
        {
            if (self == null)
                return RRoadTypeMask.Empty;
            return self.Faces.Aggregate(RRoadTypeMask.All, (current, f) => current & f.RoadTypes);
        }

        /// <summary>
        /// 辺のタイプマスクを取得する. anyOrAllがtrueの時は全ての面のタイプマスクを取得する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="useAnyFaceType"></param>
        /// <returns></returns>
        public static RRoadTypeMask GetTypeMaskOrDefault(this REdge self, bool useAnyFaceType = false)
        {
            return useAnyFaceType ? self.GetAnyFaceTypeMaskOrDefault() : self.GetAllFaceTypeMaskOrDefault();
        }

        /// <summary>
        /// アウトライン頂点のリストを同じアウトラインを構成する辺のリストに変換する
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="outlineEdges"></param>
        /// <param name="keepEdgeOnFail">辺リストが作れない時にその場で打ち切るのではなくnullを入れて続きを行う</param>
        /// <returns></returns>
        public static bool OutlineVertex2Edge(IReadOnlyList<RVertex> vertices, out List<REdge> outlineEdges, bool keepEdgeOnFail = false)
        {
            outlineEdges = new List<REdge>(vertices.Count);
            var ret = true;
            for (var i = 0; i < vertices.Count; i++)
            {
                var v0 = vertices[i % vertices.Count];
                var v1 = vertices[(i + 1) % vertices.Count];
                var e = v0.Edges.FirstOrDefault(e => e.GetOppositeVertex(v0) == v1);
                if (e == null)
                {
                    if (keepEdgeOnFail == false)
                        return false;
                    ret = false;
                }

                outlineEdges.Add(e);
            }

            return ret;
        }

        /// <summary>
        /// 連結した辺リストを頂点リストに変換する.
        /// </summary>
        /// <param name="edges"></param>
        /// <param name="outlineVertices"></param>
        /// <param name="isLoop">edgesが閉路かどうか</param>
        /// <returns></returns>
        public static bool SegmentEdge2Vertex(IReadOnlyList<REdge> edges, out List<RVertex> outlineVertices, out bool isLoop)
        {
            isLoop = edges.Count > 1 && edges[0].IsShareAnyVertex(edges[^1]);
            outlineVertices = new List<RVertex>(edges.Count + 1);

            if (edges.Count == 0)
                return false;

            {
                var e0 = edges[0];
                var e1 = edges[1 % edges.Count];
                if (e0.IsShareAnyVertex(e1, out var shareVertex) == false)
                    return false;
                var v = e0.GetOppositeVertex(shareVertex);
                if (v == null)
                    return false;
                outlineVertices.Add(v);
            }

            foreach (var e0 in edges)
            {
                RVertex shareVertex = outlineVertices[^1];
                var v = e0.GetOppositeVertex(shareVertex);
                if (v == null)
                    return false;
                outlineVertices.Add(v);
            }

            return true;
        }

        /// <summary>
        /// 歩道の輪郭グルーピング用
        /// </summary>
        public enum SideWalkEdgeKeyType
        {
            OutSide,
            InSide,
            Border
        }

        public struct SideWalkEdgeKey
        {
            public SideWalkEdgeKeyType Type;
            public PLATEAUCityObjectGroup CityObjectGroup;
            public SideWalkEdgeKey(SideWalkEdgeKeyType type, PLATEAUCityObjectGroup cityObjectGroup)
            {
                Type = type;
                CityObjectGroup = cityObjectGroup;
            }
        }

        /// <summary>
        /// selfの輪郭線を歩道の内側, 外側, 境界線に分類する.
        /// NeighborCityObjectGroupsFilterが設定されている場合, 歩道の境界判定対象はこのリストに含まれるPLATEAUCityObjectGroupと繋がっているものに限定される.
        /// </summary>
        /// <param name="faceGroup"></param>
        /// <param name="edgeGroups"></param>
        /// <param name="neighborCityObjectGroupsFilter"></param>
        /// <returns></returns>
        public static bool TryGroupBySideWalkEdge(
            RFaceGroup faceGroup
            , out List<RnEx.KeyEdgeGroup<SideWalkEdgeKey, REdge>> edgeGroups
            , HashSet<PLATEAUCityObjectGroup> neighborCityObjectGroupsFilter = null
            )
        {
            edgeGroups = null;
            // 0 : 外側の辺, 1:内側の辺, 2:境界線
            SideWalkEdgeKey Edge2WayType(REdge e)
            {
                // 自身の歩道にしか所属しない場合は外側の辺
                if (e.Faces.Count == 1)
                    return new(SideWalkEdgeKeyType.OutSide, null);

                // 以下複数のFaceと所属する場合

                var t = e.GetAllFaceTypeMaskOrDefault();

                var neighborCityObjects = e.Faces
                    .Select(f => f.CityObjectGroup)
                    .Where(co => co != faceGroup.CityObjectGroup)
                    .ToHashSet();
                if (neighborCityObjectGroupsFilter != null)
                    neighborCityObjects.IntersectWith(neighborCityObjectGroupsFilter);
                // 複数の歩道に所属している場合は歩道との境界線
                if (t.HasAnyFlag(RRoadTypeMask.SideWalk) && neighborCityObjects.Any())
                {
                    var key = neighborCityObjects.First();
                    return new(SideWalkEdgeKeyType.Border, key);
                }

                // 歩道との境界線ではない and 他のtranメッシュとの境界線は外側の辺
                if (e.Faces.GroupBy(f => f.CityObjectGroup).Count() > 1)
                    return new(SideWalkEdgeKeyType.OutSide, null);

                return new(SideWalkEdgeKeyType.InSide, null);
            }

            // 面ができない場合は無視
            var vertices = faceGroup.ComputeOutlineVertices(f => f.RoadTypes.IsSideWalk());
            if (vertices.Count < 3)
                return false;

            // 頂点リスト -> 辺リストに変換
            if (OutlineVertex2Edge(vertices, out var outlineEdges) == false)
                return false;

            edgeGroups = RnEx.GroupByOutlineEdges(outlineEdges, Edge2WayType);
            return true;
        }

        public static bool TryGroupBySideWalkEdge(
            RFace self
            , out List<RnEx.KeyEdgeGroup<SideWalkEdgeKey, REdge>> edgeGroups
            , HashSet<PLATEAUCityObjectGroup> neighborCityObjectGroupsFilter = null
        )
        {
            edgeGroups = null;
            if (self == null)
                return false;

            if (self.RoadTypes.IsSideWalk() == false)
                return false;

            // 面ができない場合は無視
            if (self.Edges.Count < 3)
                return false;

            var vertices = self.ComputeOutlineVertices();
            // 面ができない場合は無視
            if (vertices.Count < 3)
                return false;
            var faceGroup = new RFaceGroup(self.Graph, self.CityObjectGroup, new[] { self });
            return TryGroupBySideWalkEdge(faceGroup, out edgeGroups, neighborCityObjectGroupsFilter);
        }



        /// <summary>
        /// 歩道を構築するREdgeを取得する.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="outsideEdges"></param>
        /// <param name="insideEdges"></param>
        /// <param name="startEdges"></param>
        /// <param name="endEdges"></param>
        /// <param name="neighborCityObjectGroupsFilter"></param>
        /// <returns></returns>
        public static bool CreateSideWalk(this RFace self
        , out List<REdge> outsideEdges
        , out List<REdge> insideEdges
        , out List<REdge> startEdges
        , out List<REdge> endEdges
        , HashSet<PLATEAUCityObjectGroup> neighborCityObjectGroupsFilter = null
        )
        {
            outsideEdges = new List<REdge>();
            insideEdges = new List<REdge>();
            startEdges = new List<REdge>();
            endEdges = new List<REdge>();

            if (TryGroupBySideWalkEdge(self, out var ways, neighborCityObjectGroupsFilter) == false)
                return false;

            return SplitSideWalkEdge(self.CityObjectGroup, ref outsideEdges, ref insideEdges, ref startEdges, ref endEdges, ways);
        }

        private static bool SplitSideWalkEdge(PLATEAUCityObjectGroup cityObjectGroup, ref List<REdge> outsideEdges, ref List<REdge> insideEdges, ref List<REdge> startEdges,
            ref List<REdge> endEdges, List<RnEx.KeyEdgeGroup<SideWalkEdgeKey, REdge>> ways)
        {
            if (RnDebugDef.ShowDetailLog)
            {
                foreach (var way in ways)
                {
                    RGraphEx.SegmentEdge2Vertex(way.Edges, out var outVertices, out var isLoop);
                    Dictionary<SideWalkEdgeKeyType, Color> colors = new Dictionary<SideWalkEdgeKeyType, Color>
                    {
                        [SideWalkEdgeKeyType.Border] = Color.green,
                        [SideWalkEdgeKeyType.InSide] = Color.blue,
                        [SideWalkEdgeKeyType.OutSide] = Color.red
                    };

                    DebugEx.DrawLines(outVertices.Select(x => x.Position), isLoop, color: colors[way.Key.Type], duration: 30);
                }
            }

            // #TODO : このチェックはいらないかも(OutSideが無い歩道もあるため)
            var outsideIndex = ways.FindIndex(w => w.Key.Type == SideWalkEdgeKeyType.OutSide);
            if (outsideIndex < 0)
            {
                Debug.LogWarning($"outside edge not found {(cityObjectGroup ? cityObjectGroup.name : "null")}");
                return false;
            }

            for (var i = 0; i < ways.Count; i++)
            {
                var index = (outsideIndex + i) % ways.Count;
                var w = ways[index];
                if (w.Key.Type == SideWalkEdgeKeyType.OutSide)
                {
                    outsideEdges = w.Edges;
                }
                else if (w.Key.Type == SideWalkEdgeKeyType.InSide)
                {
                    insideEdges = w.Edges;
                }
                else
                {
                    var lastIndex = (index + ways.Count - 1) % ways.Count;
                    if (ways[lastIndex].Key.Type == SideWalkEdgeKeyType.OutSide)
                        endEdges = w.Edges;
                    else
                        startEdges = w.Edges;
                }
            }

            return true;
        }

        /// <summary>
        /// 歩道を構築するREdgeを取得する.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="outsideEdges"></param>
        /// <param name="insideEdges"></param>
        /// <param name="startEdges"></param>
        /// <param name="endEdges"></param>
        /// <param name="neighborCityObjectGroupsFilter"></param>
        /// <returns></returns>
        public static bool CreateSideWalk(this RFaceGroup self
            , out List<REdge> outsideEdges
            , out List<REdge> insideEdges
            , out List<REdge> startEdges
            , out List<REdge> endEdges
            , HashSet<PLATEAUCityObjectGroup> neighborCityObjectGroupsFilter = null
        )
        {
            outsideEdges = new List<REdge>();
            insideEdges = new List<REdge>();
            startEdges = new List<REdge>();
            endEdges = new List<REdge>();

            if (TryGroupBySideWalkEdge(self, out var ways, neighborCityObjectGroupsFilter) == false)
                return false;

            return SplitSideWalkEdge(self.CityObjectGroup, ref outsideEdges, ref insideEdges, ref startEdges, ref endEdges, ways);
        }

        /// <summary>
        /// selfに所属する全頂点のHashSetを取得する
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static HashSet<RVertex> CreateVertexSet(this RFace self)
        {
            return self.Edges.SelectMany(e => e.Vertices).Where(v => v != null).ToHashSet();
        }

        public static List<RnEx.KeyEdgeGroup<T, REdge>> CreateOutlineBorderGroup<T>(
            List<RVertex> outlineVertices
            , Func<REdge, T> keySelector
            , IEqualityComparer<T> comparer = null)
        {
            OutlineVertex2Edge(outlineVertices, out var outlineEdges);
            return RnEx.GroupByOutlineEdges(outlineEdges, keySelector, comparer);
        }
    }

}