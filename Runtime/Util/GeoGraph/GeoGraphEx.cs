using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace PLATEAU.Util.GeoGraph
{
    /// <summary>
    /// 3D空間上の平面タイプ
    /// </summary>
    public enum AxisPlane
    {
        // XY平面
        Xy,

        // XZ平面
        Xz,

        // YZ平面
        Yz,
    }

    public static class AxisPlaneEx
    {
        /// <summary>
        /// planeの平面成分を取り出す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="plane"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Vector2 GetTangent(this Vector3 self, AxisPlane plane)
        {
            return plane switch
            {
                AxisPlane.Xy => self.Xy(),
                AxisPlane.Xz => self.Xz(),
                AxisPlane.Yz => self.Yz(),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        /// <summary>
        /// planeの平面成分を取り出す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="plane"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Vector2 ToVector2(this Vector3 self, AxisPlane plane)
        {
            return self.GetTangent(plane);
        }

        /// <summary>
        /// planeに指定した成分にselfを入れて、残りにaを入れたvector3を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="plane"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Vector3 ToVector3(this Vector2 self, AxisPlane plane, float a = 0f)
        {
            return plane switch
            {
                AxisPlane.Xy => new Vector3(self.x, self.y, a),
                AxisPlane.Xz => new Vector3(self.x, a, self.y),
                AxisPlane.Yz => new Vector3(a, self.x, self.y),
                _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null)
            };
        }

        /// <summary>
        /// planeに対する直交成分を取り出す
        /// </summary>
        /// <param name="plane"></param>
        /// <param name="self"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static float GetNormal(this Vector3 self, AxisPlane plane)
        {
            return plane switch
            {
                AxisPlane.Xy => self.z,
                AxisPlane.Xz => self.y,
                AxisPlane.Yz => self.x,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        /// <summary>
        /// planeに対する直交成分をnに変更する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="plane"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Vector3 PutNormal(this Vector3 self, AxisPlane plane, float n)
        {
            return plane switch
            {
                AxisPlane.Xy => self.PutZ(n),
                AxisPlane.Xz => self.PutY(n),
                AxisPlane.Yz => self.PutX(n),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        /// <summary>
        /// selfのplane成分をaに変更したVector3を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="plane"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Vector3 Put(this Vector3 self, AxisPlane plane, Vector2 a)
        {
            return plane switch
            {
                AxisPlane.Xy => self.PutXy(a),
                AxisPlane.Xz => self.PutXz(a.x, a.y),
                AxisPlane.Yz => self.PutYz(a.x, a.y),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        /// <summary>
        /// planeの平行成分をv, 直交成分をnormalに変更したVector3を返す
        /// </summary>
        /// <param name="plane"></param>
        /// <param name="v"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static Vector3 Make(this AxisPlane plane, Vector2 v, float normal)
        {
            return Vector3.zero.Put(plane, v).PutNormal(plane, normal);
        }

        /// <summary>
        /// planeに直交する軸の単位ベクトルを返す
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        public static Vector3 NormalVector(this AxisPlane plane)
        {
            return plane.Make(Vector2.zero, 1f);
        }
    }

    public static class GeoGraphEx
    {


        /// <summary>
        /// 頂点verticesで構成される多角形の辺を返す. isLoop=trueの時は最後の用途と最初の要素を繋ぐ辺も返す
        /// Item1 : 始点, Item2 : 終点
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="isLoop"></param>
        /// <returns></returns>
        public static IEnumerable<Tuple<T, T>> GetEdges<T>(IEnumerable<T> vertices, bool isLoop)
        {
            bool hasValue = false;
            T first = default(T);
            T current = default(T);
            foreach (var v in vertices)
            {
                if (hasValue == false)
                {
                    first = current = v;
                    hasValue = true;
                    continue;
                }

                yield return new Tuple<T, T>(current, v);
                current = v;
            }

            if (isLoop && hasValue)
                yield return new Tuple<T, T>(current, first);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="mergeMap"></param>
        /// <param name="newVertices"></param>
        /// <param name="newVertexIndexMap"></param>
        /// <returns>頂点のマージ処理が走ったかどうか</returns>
        public static bool MergeMeshVertex(
            IList<Vector3> vertices,
            Dictionary<Vector3, Vector3> mergeMap,
            out List<Vector3> newVertices,
            // verticesインデックス -> newVerticesのインデックス
            out List<int> newVertexIndexMap)
        {
            newVertexIndexMap = Enumerable.Range(0, vertices.Count).ToList();
            newVertices = new List<Vector3>(vertices.Count);
            // 頂点 -> インデックス変換
            var indexMap = new Dictionary<Vector3, int>();
            bool found = false;
            for (var i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                // 頂点の変換があるか確認する
                if (mergeMap.TryGetValue(v, out var afterVertex))
                {
                    v = afterVertex;
                    found = true;
                }

                // #NOTE : 重複頂点の削除
                var idx = 0;
                if (indexMap.TryGetValue(v, out idx) == false)
                {
                    // 新規の場合はnewVerticesに追加しインデックスマップに登録
                    idx = newVertices.Count;
                    newVertices.Add(v);
                    indexMap[v] = idx;
                    found = true;
                }

                newVertexIndexMap[i] = idx;
            }


            return found;
        }

        /// <summary>
        /// 3次元のセル空間における, d近傍の距離を返す.
        /// GetNeighborDistance3D(1)の場合は, 3*3*3の立方体の距離を返す( x,y,zがそれぞれ -1~1の範囲)
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static Vector3Int[] GetNeighborDistance3D(int d)
        {
            // +d, -dの範囲で2d+1の立方体を返す
            var w = 2 * d + 1;
            var size = w * w * w;
            var half = d;
            var w2 = w * w;
            var ret = new Vector3Int[size];
            for (var i = 0; i < size; i++)
            {
                var dx = i % w - half;
                var dy = (i / w) % w - half;
                var dz = (i / w2) - half;
                ret[i] = new Vector3Int(dx, dy, dz);
            }

            return ret;
        }

        /// <summary>
        /// 2次元のセル空間における, d近傍の距離を返す.
        /// GetNeighborDistance2D(1)の場合は, 3*3の正方形の距離を返す( x,yがそれぞれ -1~1の範囲)
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static Vector2Int[] GetNeighborDistance2D(int d)
        {
            // +d, -dの範囲で2d+1の立方体を返す
            var w = 2 * d + 1;
            var size = w * w;
            var half = d;
            var ret = new Vector2Int[size];
            for (var i = 0; i < size; i++)
            {
                var dx = i % w - half;
                var dy = (i / w) % w - half;
                ret[i] = new Vector2Int(dx, dy);
            }

            return ret;
        }

        /// <summary>
        /// a,b,cが同一直線上にあるかどうかを返す
        /// 角度/距離の誤差を許容する
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="degEpsilon">角度誤差(baとbcのなす角度が180±この値以内になるとき同一直線判定</param>
        /// <param name="midPointTolerance">線分abとbの距離がこれ以下なら同一直線判定</param>
        /// <returns></returns>
        public static bool IsCollinear(Vector3 a, Vector3 b, Vector3 c, float degEpsilon = 0f, float midPointTolerance = 0f)
        {
            // 角度による同一直線チェック
            if (degEpsilon >= 0f)
            {
                var deg = Vector3.Angle(a - b, c - b);
                if (Mathf.Abs(180f - deg) <= degEpsilon)
                    return true;
            }

            // 中間点による同一チェック
            if (midPointTolerance >= 0f)
            {
                var segment = new LineSegment3D(a, c);
                var pos = segment.GetNearestPoint(b);
                return (b - pos).sqrMagnitude <= midPointTolerance * midPointTolerance;
            }

            return false;
        }

        /// <summary>
        /// 点群verticesをセルサイズcellSizeでグリッド化し、頂点をまとめた結果を返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="cellSize"></param>
        /// <param name="mergeCellLength"></param>
        /// <returns></returns>
        public static Dictionary<Vector3, Vector3> MergeVertices(IEnumerable<Vector3> vertices, float cellSize = 0.1f,
            int mergeCellLength = 2)
        {
            var len = cellSize;
            var mergeLen = mergeCellLength;

            // HashSetだと全く同じ値が来たときに消えないのでListにする
            var cells = new Dictionary<Vector3Int, List<Vector3>>();
            var min = Vector3Int.one * int.MaxValue;
            foreach (var v in vertices)
            {
                var c = (v / len).ToVector3Int();
                cells.GetValueOrCreate(c).Add(v);
                min = Vector3Int.Min(min, c);
            }

            // z,y,xの順でソート
            var keys = cells.Keys.ToList();
            keys.Sort((a, b) =>
            {
                var d = a.z - b.z;
                if (d != 0)
                    return d;
                d = a.y - b.y;
                if (d != 0)
                    return d;
                return a.x - b.x;
            });

            var del1 = GetNeighborDistance3D(1);
            foreach (var k in keys)
            {
                if (cells.ContainsKey(k) == false)
                    continue;

                var queue = new Queue<Vector3Int>();
                queue.Enqueue(k);

                while (queue.Any())
                {
                    var c = queue.Dequeue();
                    foreach (var d in del1)
                    {
                        var n = c + d;
                        if (cells.ContainsKey(n) == false)
                            continue;
                        if (n == k)
                            continue;

                        // 指定した距離以上は無視
                        if ((k - n).Abs().Sum() > mergeLen)
                            continue;

                        // 重複があった場合はそっちに近づけるため単純にAddRangeする
                        cells[k].AddRange(cells[n]);
                        cells.Remove(n);
                        queue.Enqueue(n);
                    }
                }
            }

            var ret = new Dictionary<Vector3, Vector3>();

            foreach (var c in cells)
            {
                // #NOTE : 1セルに1つの頂点しかない場合は無視でよい(メモリ最適化)
                if (c.Value.Count == 1)
                    continue;

                var center = c.Value.Aggregate(Vector3.zero, (v, a) => v + a) / c.Value.Count;
                foreach (var v in c.Value)
                    ret[v] = center;
            }

            return ret;
        }

        /// <summary>
        /// selfの左右の道を横幅p : (1-p)で分割した線分を返す. p=[0,1]
        /// 例) 0.5だと中央線が返る, 0だとLeftが返る, 1だとRightが返る. 
        /// </summary>
        /// <param name="leftVertices"></param>
        /// <param name="rightVertices"></param>
        /// <param name="plane"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static List<Vector3> GetInnerLerpSegments(
            IReadOnlyList<Vector3> leftVertices
            , IReadOnlyList<Vector3> rightVertices
            , AxisPlane plane
            , float p
            , float checkMeter = 3f
            )
        {
            p = Mathf.Clamp01(p);

            // 線分になっていない場合は無視する
            if (leftVertices.Count < 2 || rightVertices.Count < 2)
                return new List<Vector3>();

            // それぞれ直線の場合は高速化の特別処理入れる
            if (leftVertices.Count == 2 && rightVertices.Count == 2)
            {
                return new List<Vector3>()
                {
                    Vector3.Lerp(leftVertices[0], rightVertices[0], p),
                    Vector3.Lerp(leftVertices[1], rightVertices[1], p)
                };
            }

            var leftEdges = GetEdges(leftVertices, false).Select(v => new LineSegment3D(v.Item1, v.Item2)).ToList();
            var rightEdges = GetEdges(rightVertices, false).Select(v => new LineSegment3D(v.Item1, v.Item2)).ToList();

            var indices = new List<float>();

            checkMeter = Mathf.Max(checkMeter, 1f);

            // 左の線分の頂点をイベントポイントとして登録
            // ただし、線分がcheckMeter以上の場合はcheckMeter間隔でイベントポイントを追加する
            for (var i = 0; i < leftVertices.Count; i++)
            {
                indices.Add(i);
                // 最後の頂点はチェックしない
                if (i == leftVertices.Count - 1)
                    break;
                var p0 = leftVertices[i];
                var p1 = leftVertices[i + 1];
                var sqrLen = (p1 - p0).sqrMagnitude;
                if (sqrLen > checkMeter * checkMeter)
                {
                    var len = Mathf.Sqrt(sqrLen);
                    var num = len / checkMeter;
                    for (var j = 0; j < num - 1; ++j)
                        indices.Add(i + (j + 1f) / num);
                }
            }

            bool IsInInnerSide(LineSegment3D? e, Vector3 d, bool reverse, bool isPrev)
            {
                if (e.HasValue == false)
                    return true;
                var ed2 = e.Value.Direction.ToVector2(plane);
                var d2 = d.ToVector2(plane);
                var cross = Vector2Ex.Cross(ed2, d2);
                if (reverse == false)
                    cross = -cross;
                if (cross > 0)
                    return false;
                if (cross == 0f)
                {
                    var dot = Vector2.Dot(ed2, d2);
                    if (isPrev)
                        dot = -dot;
                    return dot < 0f;
                }

                return true;
            }

            bool CheckCollision(Vector3 a, Vector3 b, IList<LineSegment3D> edges, float indexF)
            {
                var a2 = a.ToVector2(plane);
                var b2 = b.ToVector2(plane);
                var index = (int)indexF;
                var f = indexF - index;
                var prevIndex = f > 0 ? index : index - 1;
                for (var i = 0; i < edges.Count; ++i)
                {
                    if (i == index || i == prevIndex)
                        continue;
                    var e = edges[i];
                    var e2 = e.To2D(plane);
                    if (e2.TrySegmentIntersection(a2, b2, out var _, out var _, out var _))
                        return true;
                }

                return false;
            }

            for (var i = 0; i < rightVertices.Count; ++i)
            {
                var pos = rightVertices[i];

                var prevEdge = i > 0 ? rightEdges[i - 1] : (LineSegment3D?)null;
                var nextEdge = i < rightEdges.Count ? rightEdges[i] : (LineSegment3D?)null;

                float minIndexF = -1;
                float minDist = float.MaxValue;
                for (var edgeIndex = 0; edgeIndex < leftEdges.Count; ++edgeIndex)
                {
                    var e = leftEdges[edgeIndex];
                    var nearPos = e.GetNearestPoint(pos, out var distanceFromStart);
                    var d = nearPos - pos;

                    var dist = d.magnitude;
                    if (dist >= minDist)
                        continue;
                    if (IsInInnerSide(prevEdge, d, false, true) == false)
                        continue;
                    if (IsInInnerSide(nextEdge, d, false, false) == false)
                        continue;
                    if (CheckCollision(pos, nearPos, rightEdges, i))
                        continue;
                    minDist = dist;
                    minIndexF = edgeIndex + distanceFromStart / e.Magnitude;
                }

                if (minIndexF < 0)
                    continue;
                indices.Add(minIndexF);
            }

            indices.Sort();

            var searchRightIndex = 0;
            var ret = new List<Vector3>();
            foreach (var indexF in indices)
            {
                var i = Mathf.Clamp((int)indexF, 0, leftEdges.Count - 1);
                var e1 = leftEdges[i];
                var f = Mathf.Clamp01(indexF - i);
                var pos = Vector3.Lerp(e1.Start, e1.End, f);

                LineSegment3D? prevEdge = null;
                LineSegment3D? nextEdge = null;
                if (f is > 0f and < 1f)
                {
                    prevEdge = new LineSegment3D(e1.Start, pos);
                    nextEdge = new LineSegment3D(pos, e1.End);
                }
                else
                {
                    if (i > 0)
                        prevEdge = leftEdges[i - 1];
                    if (i < leftEdges.Count)
                        nextEdge = leftEdges[i];
                }

                float minIndexF = -1;
                float minDist = float.MaxValue;
                Vector3 minPos = Vector3.zero;
                for (var edgeIndex = searchRightIndex; edgeIndex < rightEdges.Count; ++edgeIndex)
                {
                    var e2 = rightEdges[edgeIndex];
                    var nearPos = e2.GetNearestPoint(pos, out var t);
                    var d = nearPos - pos;
                    var dist = d.magnitude;

                    if (dist >= minDist)
                        continue;
                    if (IsInInnerSide(prevEdge, d, true, true) == false)
                        continue;
                    if (IsInInnerSide(nextEdge, d, true, false) == false)
                        continue;

                    if (CheckCollision(pos, nearPos, leftEdges, indexF))
                        continue;
                    minDist = dist;
                    minIndexF = edgeIndex + t;
                    minPos = nearPos;
                }

                if (minIndexF < 0)
                    continue;
                // #TODO : やってみたらおかしくなったので毎回最初から探す
                // 高速化のため. 戻ることは無いはずなので見つかったindexから探索でよいはず
                //searchRightIndex = (int)minIndexF;

                ret.Add(Vector3.Lerp(pos, minPos, p));
            }

            return ret;
        }

        /// <summary>
        /// verticesで表される線分を, toleranceAngleDeg/midPointToleranceを使った同一直線判定でグルーピングする
        /// 戻り値はグループの始点インデックス
        /// 例) (v0, v1, v2, v3, v4, v5)
        /// v0~v1, v1~v5が同一直線なら[0, 2, 5]が返る
        /// v0~v5が同一直線なら[0, 5]が返る
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="isCollinear">頂点v0, v1, v2が同一直線となるか</param>
        /// <returns></returns>
        public static List<int> SplitByCollinearSegment<T>(IReadOnlyList<T> vertices, Func<T, T, T, List<int>, bool> isCollinear)
        {
            if (vertices.Count <= 2)
            {
                return new List<int> { 0 };
            }

            var indices = new List<int> { 0, 1 };
            var ret = new List<int> { };
            for (var i = 2; i < vertices.Count; ++i)
            {
                indices.Add(i);
                // 中間地点の削除扱い(Vector3でもz成分が0なら同じなのでキャストして使う)
                if (isCollinear(vertices[indices[0]], vertices[indices[1]], vertices[indices[2]], indices))
                {
                    indices.RemoveAt(1);
                    continue;
                }

                ret.Add(indices[0]);
                indices = new List<int> { i - 1, i };
            }

            // 最後の要素は必ず追加(最後の要素は削除されないため
            ret.Add(indices[0]);
            ret.Add(vertices.Count - 1);
            return ret;
        }

        public static List<int> SplitByCollinearSegment<T>(IReadOnlyList<T> vertices, Func<T, Vector3> toVec3,
            float toleranceAngleDeg, float midPointTolerance, bool checkAdjustSegmentAngleOnly = false)
        {
            return SplitByCollinearSegment(vertices,
                (v0, v1, v2, indices) =>
                {
                    if (checkAdjustSegmentAngleOnly == false)
                        return IsCollinear(toVec3(v0), toVec3(v1), toVec3(v2), toleranceAngleDeg, midPointTolerance);

                    var i2 = indices[2];
                    var a = toVec3(vertices[i2]);
                    var b = toVec3(vertices[i2 - 1]);
                    var c = toVec3(vertices[i2 - 2]);
                    // 角度による同一直線チェック
                    if (toleranceAngleDeg >= 0f)
                    {
                        var deg = Vector3.Angle(a - b, c - b);
                        if (Mathf.Abs(180f - deg) <= toleranceAngleDeg)
                            return true;
                    }

                    return false;
                });
        }

    }
}