using PLATEAU.CityInfo;
using PLATEAU.PolygonMesh;
using PLATEAU.RoadNetwork.Structure;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PLATEAU.RoadNetwork.Util
{
    public struct CombSet2<T>
    {
        public T A { get; }

        public T B { get; }

        public CombSet2(T a, T b)
        {
            A = a;
            B = b;
            if (A.GetHashCode() > B.GetHashCode())
                (A, B) = (B, A);
        }
    }

    public struct CombSet3<T>
    {
        public T A { get; }

        public T B { get; }

        public T C { get; }

        public CombSet3(T a, T b, T c)
        {
            A = a;
            B = b;
            C = c;
            if (A.GetHashCode() > B.GetHashCode())
                (A, B) = (B, A);
            if (B.GetHashCode() > C.GetHashCode())
                (B, C) = (C, B);
            if (A.GetHashCode() > B.GetHashCode())
                (A, B) = (B, A);
        }
    }


    /// <summary>
    /// LineSegmentとLineStringとの交点チェック結果
    /// </summary>
    public class LineCrossPointResult
    {
        public class TargetLineInfo
        {
            /// <summary>
            /// 対象線分
            /// </summary>
            public RnLineString LineString { get; set; }

            /// <summary>
            /// 交点情報.
            /// index : LineString上の配列インデックス位置. 線分の途中の点の場合は小数になる
            ///     v : 交点座標
            /// </summary>
            public List<(float index, Vector3 v)> Intersections { get; set; } = new();
        }

        /// <summary>
        /// 交点チェック対象のLineString情報
        /// </summary>
        public List<TargetLineInfo> TargetLines { get; set; } = new();

        /// <summary>
        /// TargetLinesのうち、交差しているもの
        /// </summary>
        public IEnumerable<TargetLineInfo> CrossingLines => TargetLines.Where(t => t.Intersections.Count > 0);

        /// <summary>
        /// 対象の線分
        /// </summary>
        public LineSegment3D LineSegment { get; set; }
    }


    public static class RnEx
    {
        /// <summary>
        /// EditorのScene上で選択されているPLATEAUCityObjectGroupを取得する(Editorのみ)
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<PLATEAUCityObjectGroup> GetSceneSelectedCityObjectGroups()
        {
#if UNITY_EDITOR
            return Selection.gameObjects.Select(go => go.GetComponent<PLATEAUCityObjectGroup>()).Where(cog => cog != null);
#else
            return Enumerable.Empty<PLATEAUCityObjectGroup>();
#endif
        }
        public static CombSet2<T> CombSet<T>(T a, T b)
        {
            return new CombSet2<T>(a, b);
        }

        public static CombSet3<T> CombSet<T>(T a, T b, T c)
        {
            return new CombSet3<T>(a, b, c);
        }

        public static void Replace<T>(IList<T> self, T before, T after) where T : class
        {
            for (var i = 0; i < self.Count; i++)
            {
                if (self[i] == before)
                    self[i] = after;
            }
        }

        public static void ReplaceLane(IList<RnLane> self, RnLane before, RnLane after)
        {
            Replace(self, before, after);
        }

        /// <summary>
        /// ModelのRootNodeを返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        private static IEnumerable<Node> GetRootNodes(this Model self)
        {
            for (var i = 0; i < self.RootNodesCount; ++i)
                yield return self.GetRootNodeAt(i);
        }

        /// <summary>
        /// Sceneエディタ上で選択されているかどうか
        /// </summary>
        /// <param name="cog"></param>
        /// <returns></returns>
        public static bool IsEditorSceneSelected(PLATEAUCityObjectGroup cog)
        {
#if UNITY_EDITOR
            if (!cog)
                return false;
            return UnityEditor.Selection.gameObjects.Contains(cog.gameObject);
#else
            return false;
#endif
        }

        /// <summary>
        /// leftVerticesとrightVerticesの間をtで補間した点列を生成する.
        /// start/endはそれぞれの端点.
        /// startBorder/endBorderはそれぞれの端点のボーダーでstartBorder/LeftVertices/endBorder/RightVerticesで囲まれた範囲の外に出ないようにチェックするためのもの
        /// </summary>
        /// <param name="leftVertices"></param>
        /// <param name="rightVertices"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="startBorder"></param>
        /// <param name="endBorder"></param>
        /// <param name="t"></param>
        /// <param name="pointSkipDistance"></param>
        /// <returns></returns>
        public static RnLineString CreateInnerLerpLineString(IReadOnlyList<Vector3> leftVertices, IReadOnlyList<Vector3> rightVertices, RnPoint start, RnPoint end, RnWay startBorder, RnWay endBorder, float t, float pointSkipDistance = 1e-3f)
        {
            // 左右がどちらも直線もしくは点以下の場合 -> start/endを直接つなぐ
            if (leftVertices.Count <= 2 && rightVertices.Count <= 2)
            {
                return new RnLineString(new List<RnPoint> { start, end });
            }

            var line = new RnLineString();
            void AddPoint(RnPoint p)
            {
                if (p == null)
                    return;
                line.AddPointOrSkip(p, pointSkipDistance);
            }

            AddPoint(start);
            var segments = GeoGraphEx.GetInnerLerpSegments(leftVertices, rightVertices, RnModel.Plane, t);
            // 1つ目の点はボーダーと重複するのでスキップ
            // #TODO : 実際はボーダーよりも外側にあるのはすべてスキップすべき
            foreach (var s in segments.Skip(1))
                AddPoint(new RnPoint(s));
            AddPoint(end);
            // 自己交差があれば削除する
            var plane = RnModel.Plane;
            GeoGraph2D.RemoveSelfCrossing(line.Points
                , t => t.Vertex.ToVector2(plane)
                , (p1, p2, p3, p4, inter, f1, f2) => new RnPoint(Vector3.Lerp(p1, p2, f1)));

            return line;
        }

        /// <summary>
        /// lineSegmentとwaysの交点を取得する. ただし、2Dでの交点
        /// </summary>
        /// <param name="lineSegment"></param>
        /// <param name="ways"></param>
        /// <returns></returns>
        public static LineCrossPointResult GetLineIntersections(LineSegment3D lineSegment, IEnumerable<RnWay> ways)
        {
            var ret = new LineCrossPointResult { LineSegment = lineSegment };

            // 全てのwayのLineStringを取得
            var targetLines = ways
                .Select(w => w.LineString)
                .ToHashSet();

            foreach (var way in targetLines)
            {
                var elem = new LineCrossPointResult.TargetLineInfo { LineString = way };

                foreach (var r in way.GetIntersectionBy2D(lineSegment, RnModel.Plane))
                {
                    elem.Intersections.Add((r.index, r.v));
                }
                ret.TargetLines.Add(elem);
            }

            return ret;
        }

        /// <summary>
        /// start -> endの線分の法線ベクトルを取得する(上(Vector3.up)から反時計回りを向いている)
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static Vector3 GetEdgeNormal(Vector3 start, Vector3 end)
        {
            var d = end - start;
            // Vector3.Crossは左手系なので逆
            return (-Vector3.Cross(Vector3.up, d)).normalized;
        }

        /// <summary>
        /// start -> endの線分の法線ベクトルを取得する(反時計回りを向いている)
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static Vector2 GetEdgeNormal(Vector2 start, Vector2 end)
        {
            var d = end - start;
            return new Vector2(-d.y, d.x).normalized;
        }

        public class FindBorderEdgesResult
        {
            public bool Success { get; set; }

            /// <summary>
            /// 頂点調整された頂点配列
            /// </summary>
            public List<Vector2> ReducedVertices { get; set; }

            public List<int> ReducedBorderVertexIndices { get; set; }

            public IEnumerable<Vector2> ReducedBorderVertices => ReducedBorderVertexIndices.Select(i => ReducedVertices[i]);
            /// <summary>
            /// 境界線を表すSrcVerticesのインデックス
            /// </summary>
            public List<int> BorderVertexIndices { get; set; }

            /// <summary>
            /// 元の頂点配列
            /// </summary>
            public List<Vector2> SrcVertices { get; set; }

            public IEnumerable<Vector2> BorderVertices => BorderVertexIndices.Select(i => SrcVertices[i]);
        }

        public static FindBorderEdgesResult FindBorderEdges(IReadOnlyList<Vector2> vertices, float toleranceAngleDegForMidEdge = 20f, float skipAngleDeg = 20f, AxisPlane plane = RnDef.Plane)
        {
            var verts = vertices.ToList();

            var isClockwise = GeoGraph2D.IsClockwise(verts);

            FindBorderEdgesResult ret = new() { SrcVertices = verts };
            var afterVerts = Reduction(verts, (list, i) =>
            {
                if (list.Count <= 4)
                    return true;
                var area1 = GeoGraph2D.CalcPolygonArea(list);
                var area2 = GeoGraph2D.CalcPolygonArea(verts);
                return area1 / area2 < 0.6f;
            });
            ret.ReducedVertices = afterVerts;

            var indices = GeoGraph2D.FindMidEdge(afterVerts, toleranceAngleDegForMidEdge, skipAngleDeg);

            ret.ReducedBorderVertexIndices = indices;
            var x = (indices.Count - 1) / 2;
            var ind0 = indices[x];
            var ind1 = indices[x + 1];
            var st = afterVerts[ind0];
            var en = afterVerts[ind1];

            var n = RnEx.GetEdgeNormal(st, en);
            if (isClockwise == false)
                n *= -1f;
            var mid = (st + en) * 0.5f;
            var ray = new Ray2D(mid, n);

            // midからray方向に最も近い位置にあるvertsの辺のインデックス
            var minLen = float.MaxValue;
            var minIndex = -1;
            var edges = GeoGraphEx.GetEdges(verts, false).Select(v => new LineSegment2D(v.Item1, v.Item2)).ToList();

            for (var i = 0; i < edges.Count; ++i)
            {
                var seg = edges[i];
                if (seg.TryHalfLineIntersection(ray.origin, ray.direction, out var inter, out var _, out var _))
                {
                    var len = (mid - inter).sqrMagnitude;
                    if (len < minLen)
                    {
                        minLen = len;
                        minIndex = i;
                    }
                }
            }

            if (minIndex < 0)
            {
                ret.Success = false;
                ret.BorderVertexIndices = GeoGraph2D.FindMidEdge(vertices, toleranceAngleDegForMidEdge, skipAngleDeg);
                return ret;
            }

            ret.Success = true;
            // 開始線分が中心線扱いにならないようにskipAngleDegを使ってスキップする
            var (startIndex, endIndex) = (0, edges.Count - 1);
            if (skipAngleDeg > 0f)
            {
                while (startIndex < edges.Count - 1 && Mathf.Abs(Vector2.Angle(edges[startIndex].Direction, edges[startIndex + 1].Direction)) < skipAngleDeg)
                    startIndex++;
                while (endIndex > 0 && Mathf.Abs(Vector2.Angle(edges[endIndex].Direction, edges[endIndex - 1].Direction)) < skipAngleDeg)
                    endIndex--;
            }
            var res = GeoGraph2D.FindCollinearRange(minIndex, edges, toleranceAngleDegForMidEdge, startIndex, endIndex);
            res.Add(res[^1] + 1);

            if (RnDebugDef.ShowDetailLog)
            {
                DebugEx.DrawLines(res.Select(i => verts[i].Xay(0)), color: Color.red, duration: 10);
            }
            ret.BorderVertexIndices = res;

            return ret;
        }

        public static List<Vector2> Reduction(IReadOnlyList<Vector2> src, Func<List<Vector2>, int, bool> checkStop)
        {
            return Reduction(src, x => x, x => x, checkStop, 0);
        }

        /// <summary>
        /// polygonVerticesで表される多角形を縮小しシンプルな形状にする
        /// toVec2はT型からVector2型への変換
        /// creatorは新規で頂点を作成する
        /// checkStopは縮小処理の打ち止めチェック
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="polygonVertices"></param>
        /// <param name="toVec2"></param>
        /// <param name="creator"></param>
        /// <param name="checkStop"></param>
        /// <param name="nest"></param>
        /// <returns></returns>
        private static List<T> Reduction<T>(
            IReadOnlyList<T> polygonVertices
            , Func<T, Vector2> toVec2
            , Func<Vector2, T> creator
            , Func<List<T>, int, bool> checkStop
            , int nest = 0)
        {
            var vertices = polygonVertices.ToList();

            var isClockwise = GeoGraph2D.IsClockwise(vertices.Select(toVec2));
            var normalSign = isClockwise ? -1 : 1;
            Vector2 GetPos(int x)
            {
                return toVec2(vertices[(x + vertices.Count) % vertices.Count]);
            }

            Vector2 GetEdgeNormal(int x)
            {
                x += vertices.Count;
                return normalSign * RnEx.GetEdgeNormal(GetPos(x), GetPos(x + 1));
            }

            Vector2 GetVertexNormal(int x)
            {
                return (GetEdgeNormal(x) + GetEdgeNormal(x - 1)).normalized;
            }

            List<T> Move(float delta)
            {
                var points = new List<T>();
                for (var i = 0; i < vertices.Count; ++i)
                {
                    var e0 = GetEdgeNormal(i);
                    var e1 = GetEdgeNormal(i - 1);
                    var dd = e0 + e1 * (1f - Vector2.Dot(e0, e1));
                    points.Add(creator(GetPos(i) + dd * delta));
                }

                return points;
            }
            Dictionary<int, (float minLen, int index, float offset, Vector2 inter)> minLenDic = new();

            void Check(int srcIndex, int dstIndex, Vector2 inter)
            {
                var srcV = GetPos(srcIndex);
                var dir = (inter - srcV);
                var len = dir.magnitude;

                var en = GetEdgeNormal(srcIndex);

                var offset = Vector2.Dot(en, dir);
                if (minLenDic.TryGetValue(srcIndex, out var minLen) == false)
                {
                    minLenDic[srcIndex] = (len, dstIndex, offset, inter);
                }
                else if (len < minLen.minLen)
                {
                    minLenDic[srcIndex] = (len, dstIndex, offset, inter);
                }
            }
            for (var i = 0; i < vertices.Count; ++i)
            {
                var vn1 = GetVertexNormal(i);
                var halfRay1 = new Ray2D(GetPos(i), vn1);
                for (var j = i + 1; j < vertices.Count; ++j)
                {
                    var vn2 = GetVertexNormal(j);

                    var halfRay2 = new Ray2D(GetPos(j), vn2);
                    if (halfRay2.CalcIntersection(halfRay1, out var inter, out var t1, out var t2) == false)
                        continue;

                    Check(i, j, inter);
                    Check(j, i, inter);
                }
            }


            if (minLenDic.Where(x =>
            {
                var key = x.Key;
                var val = x.Value.index;
                return minLenDic[val].index == key;
            }).TryFindMinElement(x => x.Value.offset, out var e))
            {
                var moved = Move(e.Value.offset);
                var (from, to) = (e.Key, e.Value.index);
                if (from > to)
                    (to, from) = (from, to);

                var range = moved.GetRange(from, to - from);
                moved.RemoveRange(from, to - from);

                if (GeoGraph2D.CalcPolygonArea(range.Select(toVec2).ToList()) > GeoGraph2D.CalcPolygonArea(moved.Select(toVec2).ToList()))
                {
                    moved = range;
                }

                if (checkStop(moved, nest + 1))
                    return vertices;

                return Reduction(moved, toVec2, creator, checkStop, nest + 1);
            }

            return vertices;
        }

        public class KeyEdgeGroup<TKey, TEdge>
        {
            public TKey Key { get; set; }
            public List<TEdge> Edges { get; } = new();

            public KeyEdgeGroup() { }
            public KeyEdgeGroup(TKey key)
            {
                Key = key;
            }
        }

        // OutlineEdgesで表現される多角形の各辺をkeySelectorをキーとした連続した辺でグループ化
        // 例: 各辺のキーが右のようになる場合 {A, A, B, B, A, A, C, C, A}
        //   => {B, (2,3)}, {A, (4,5)}, {C, (6,7)}, {A, (8,0, 1)}のようにグループ化される
        public static List<KeyEdgeGroup<TKey, TEdge>> GroupByOutlineEdges<TKey, TEdge>(
            IEnumerable<TEdge> edges
            , Func<TEdge, TKey> keySelector
            , IEqualityComparer<TKey> comparer = null
            , bool isLoop = true
            )
        {
            List<KeyEdgeGroup<TKey, TEdge>> ret = new();
            comparer ??= EqualityComparer<TKey>.Default;
            foreach (var e in edges)
            {
                var key = keySelector(e);
                if (!ret.Any() || comparer.Equals(ret[^1].Key, key) == false)
                {
                    ret.Add(new KeyEdgeGroup<TKey, TEdge>(key));
                }
                ret[^1].Edges.Add(e);
            }

            // 両端が同じキーの場合は結合する
            if (isLoop)
            {
                if (ret.Count > 1 && comparer.Equals(ret[0].Key, ret[^1].Key))
                {
                    ret[^1].Edges.AddRange(ret[0].Edges);
                    ret.RemoveAt(0);
                }
            }

            return ret;
        }
    }
}
