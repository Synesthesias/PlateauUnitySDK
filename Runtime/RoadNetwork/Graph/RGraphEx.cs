using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.CityObject;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
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

        public static RGraph Create(List<SubDividedCityObject> cityObjects)
        {
            var graph = new RGraph();
            Dictionary<Vector3, RVertex> vertexMap = new Dictionary<Vector3, RVertex>();
            Dictionary<EdgeKey, REdge> edgeMap = new Dictionary<EdgeKey, REdge>();
            foreach (var cityObject in cityObjects)
            {
                if (!cityObject.CityObjectGroup)
                {
                    Debug.LogWarning($"[{cityObject.Name}] CityObjectGroupがない為. RFace生成はスキップされます.");
                    continue;
                }

                var lodLevel = cityObject.CityObjectGroup.GetLodLevel();
                var roadType = cityObject.GetRoadType(true);
                // transformを適用する
                var mat = cityObject.CityObjectGroup.transform.localToWorldMatrix;
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
                        var separated = s.Separate();
                        foreach (var m in separated)
                        {
                            for (var i = 0; i < m.Triangles.Count; i += 3)
                            {
                                var e0 = edgeMap.GetValueOrCreate(new EdgeKey(vertices[m.Triangles[i]], vertices[m.Triangles[i + 1]])
                                    , e => new REdge(e.V0, e.V1));
                                var e1 = edgeMap.GetValueOrCreate(new EdgeKey(vertices[m.Triangles[i + 1]], vertices[m.Triangles[i + 2]])
                                    , e => new REdge(e.V0, e.V1));
                                var e2 = edgeMap.GetValueOrCreate(new EdgeKey(vertices[m.Triangles[i + 2]], vertices[m.Triangles[i]])
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
        /// 頂点をリダクション処理
        /// </summary>
        /// <param name="self"></param>
        /// <param name="mergeCellSize"></param>
        /// <param name="mergeCellLength"></param>
        /// <param name="midPointTolerance">aとcとしか接続していない点bに対して、a-cの直線との距離がこれ以下だとbをマージする</param>
        public static void VertexReduction(this RGraph self, float mergeCellSize, int mergeCellLength, float midPointTolerance)
        {
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
        /// 同じPLATEAUCityObjectGroupのFaceをpredicateのルールに従って一つのRFaceにする
        /// </summary>
        /// <param name="self"></param>
        /// <param name="isMatch"></param>
        public static List<RFaceGroup> GroupBy(this RGraph self, Func<RFace, RFace, bool> isMatch)
        {
            var ret = new List<RFaceGroup>(self.Faces.Count);
            foreach (var group in self.Faces.GroupBy(f => f.CityObjectGroup))
            {
                var faces = group.ToHashSet();
                var faceGroups = new List<RFaceGroup>();
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
                            if (IsShareEdge(f0, f1) && isMatch(f0, f1))
                            {
                                g.Add(f1);
                                queue.Enqueue(f1);
                            }
                        }
                        foreach (var f in queue)
                            faces.Remove(f);
                    }

                    faceGroups.Add(new RFaceGroup(self, group.Key, g));
                }

                foreach (var f in faceGroups)
                    ret.Add(f);

            }
            return ret;
        }

        public static void Optimize(this RGraph self, float mergeCellSize, int mergeCellLength, float midPointTolerance)
        {
            self.VertexReduction(mergeCellSize, mergeCellLength, midPointTolerance);
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
                var shareMid = 0.3f * 0.3f;
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

                        if (s0.TrySegmentIntersectionBy2D(s1, AxisPlane.Xz, heightTolerance, out var intersection,
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
            foreach (var p in self.Faces.ToList())
            {
                p.Separate();
            }
        }

        private static List<RVertex> ComputeOutlineVertices(IList<RFace> faces)
        {
            var vertices = faces
                .SelectMany(f => f.Edges.SelectMany(e => e.Vertices))
                .Where(v => v != null)
                .ToHashSet();
            var edges = faces
                .SelectMany(f => f.Edges)
                .ToHashSet();
            var res = GeoGraph2D.ComputeOutline2(
                vertices
                , v => v.Position, AxisPlane.Xz
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
        /// <returns></returns>
        public static bool OutlineVertex2Edge(IReadOnlyList<RVertex> vertices, out List<REdge> outlineEdges)
        {
            outlineEdges = new List<REdge>(vertices.Count);
            for (var i = 0; i < vertices.Count; i++)
            {
                var v0 = vertices[i % vertices.Count];
                var v1 = vertices[(i + 1) % vertices.Count];
                var e = v0.Edges.FirstOrDefault(e => e.GetOppositeVertex(v0) == v1);
                if (e == null)
                    return false;
                outlineEdges.Add(e);
            }

            return true;
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
        /// 歩道を構築するREdgeを取得する.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="outsideEdges"></param>
        /// <param name="insideEdges"></param>
        /// <param name="startEdges"></param>
        /// <param name="endEdges"></param>
        /// <returns></returns>
        public static bool CreateSideWalk(this RFace self, out List<REdge> outsideEdges, out List<REdge> insideEdges, out List<REdge> startEdges, out List<REdge> endEdges)
        {
            outsideEdges = new List<REdge>();
            insideEdges = new List<REdge>();
            startEdges = new List<REdge>();
            endEdges = new List<REdge>();
            if (self == null)
                return false;

            if (self.RoadTypes.IsSideWalk() == false)
                return false;

            // 面ができない場合は無視
            if (self.Edges.Count < 3)
                return false;

            // 0 : 外側の辺, 1:内側の辺, 2:境界線
            static int Edge2WayType(REdge e)
            {
                // 歩道にしか所属しない場合は外側の辺
                if (e.Faces.Count == 1)
                    return 0;

                var t = e.GetAllFaceTypeMaskOrDefault();
                // 複数の歩道に所属している場合は境界線
                if (t.HasAnyFlag(RRoadTypeMask.SideWalk))
                    return 2;
                // 歩道以外に所属している場合は内側の辺
                return 1;
            }
            var vertices = self.ComputeOutlineVertices();
            // 面ができない場合は無視
            if (vertices.Count < 3)
                return false;

            // 頂点リスト -> 辺リストに変換
            if (OutlineVertex2Edge(vertices, out var outlineEdges) == false)
                return false;

            var lastType = -1;
            List<REdge> way = new();
            List<(int type, List<REdge> edges)> ways = new();
            foreach (var e in outlineEdges)
            {
                var type = Edge2WayType(e);
                if (type != lastType)
                {
                    if (way.Any())
                        ways.Add((lastType, way));

                    way = new();
                    lastType = type;
                }
                way.Add(e);
            }

            if (way.Any())
                ways.Add((lastType, way));

            if (ways.Count > 1 && ways[0].type == ways[^1].type)
            {
                ways[^1].edges.AddRange(ways[0].edges);
                ways.RemoveAt(0);
            }

            var outsideIndex = ways.FindIndex(w => w.type == 0);
            if (outsideIndex < 0)
            {
                Debug.LogWarning("outside edge not found");
                return false;
            }

            for (var i = 0; i < ways.Count; i++)
            {
                var index = (outsideIndex + i) % ways.Count;
                var w = ways[index];
                if (w.type == 0)
                {
                    outsideEdges = w.edges;
                }
                else if (w.type == 1)
                {
                    insideEdges = w.edges;
                }
                else
                {
                    var lastIndex = (index + ways.Count - 1) % ways.Count;
                    if (ways[lastIndex].type == 0)
                        endEdges = w.edges;
                    else
                        startEdges = w.edges;
                }
            }

            return true;
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
    }

}