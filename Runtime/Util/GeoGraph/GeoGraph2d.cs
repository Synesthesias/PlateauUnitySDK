using PLATEAU.RoadNetwork.CityObject;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace PLATEAU.Util.GeoGraph
{
    public static class GeoGraph2D
    {
        public const float Epsilon = 1e-5f;

        public class Vector2Equitable : IEqualityComparer<Vector2>
        {
            public float Tolerance { get; set; }

            public Vector2Equitable(float tolerance)
            {
                Tolerance = tolerance;
            }
            public bool Equals(Vector2 x, Vector2 y)
            {
                return (x - y).sqrMagnitude < Tolerance;
            }

            public int GetHashCode(Vector2 obj)
            {
                return obj.GetHashCode();
            }
        }
        /// <summary>
        /// 凸包を計算する. 戻り値の0番,^1番は同じものが入る
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices"></param>
        /// <param name="toVec3"></param>
        /// <param name="plane"></param>
        /// <param name="sameLineTolerance">同一直線の許容誤差. ２つのベクトルの内積が1-これ以上</param>
        /// <returns></returns>
        public static List<T> ComputeConvexVolume<T>(IEnumerable<T> vertices, Func<T, Vector3> toVec3, AxisPlane plane, float sameLineTolerance = 0f)
        {
            Vector2 ToVec2(T a) => toVec3(a).GetTangent(plane);
            // リストの最後の辺が時計回りになっているかを確認
            bool IsLastClockwise(List<T> list)
            {
                if (list.Count <= 2)
                    return true;
                var v1 = ToVec2(list[^1]);
                var v2 = ToVec2(list[^2]);
                var v3 = ToVec2(list[^3]);
                var d1 = v1 - v2;
                var d2 = v2 - v3;
                var v = Vector2Ex.Cross(d1, d2);
                if (v > 0)
                    return true;
                // 同一直線の場合は採用とする. sameLineTolerance = 0の場合は同一直線を無視する
                return Vector2.Dot(d1.normalized, d2.normalized) > 1f - sameLineTolerance;
            }
            var compare = new Vector2Equitable(Epsilon);
            var sortedVertices = vertices.OrderBy(v => ToVec2(v).x).ThenBy(v => ToVec2(v).y).ToList();
            for (var i = 0; i < sortedVertices.Count - 1;)
            {
                if (compare.Equals(ToVec2(sortedVertices[i]), ToVec2(sortedVertices[i + 1])))
                    sortedVertices.RemoveAt(i + 1);
                else
                    i++;
            }
            if (sortedVertices.Count <= 2)
                return new List<T>();

            // 上方の凸形状計算
            var ret = new List<T> { sortedVertices[0], sortedVertices[1] };

            for (var i = 2; i < sortedVertices.Count; i++)
            {
                ret.Add(sortedVertices[i]);
                while (IsLastClockwise(ret) == false)
                    ret.RemoveAt(ret.Count - 2);
            }

            // 下方の凸形状計算
            ret.Add(sortedVertices[^2]);
            for (var i = sortedVertices.Count - 3; i >= 0; --i)
            {
                ret.Add(sortedVertices[i]);
                while (IsLastClockwise(ret) == false)
                    ret.RemoveAt(ret.Count - 2);
            }

            return ret;
        }

        /// <summary>
        /// Vector3の頂点をtoVec2で2Dに射影したうえで凸包を構成する頂点を返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="toVec2"></param>
        /// <returns></returns>
        public static List<Vector3> ComputeConvexVolume(IEnumerable<Vector3> vertices, Func<Vector3, Vector2> toVec2)
        {
            // リストの最後の辺が時計回りになっているかを確認
            bool IsLastClockwise(List<Vector3> list)
            {
                if (list.Count <= 2)
                    return true;
                return Vector2Ex.Cross(toVec2(list[^1] - list[^2]), toVec2(list[^2] - list[^3])) > 0;
            }
            var compare = new Vector2Equitable(Epsilon);
            var sortedVertices = vertices.OrderBy(v => v.x).ThenBy(v => v.y).ToList();
            for (var i = 0; i < sortedVertices.Count - 1;)
            {
                if (compare.Equals(toVec2(sortedVertices[i]), toVec2(sortedVertices[i + 1])))
                    sortedVertices.RemoveAt(i + 1);
                else
                    i++;
            }
            if (sortedVertices.Count <= 2)
                return new List<Vector3>();

            // 上方の凸形状計算
            var ret = new List<Vector3> { sortedVertices[0], sortedVertices[1] };

            for (var i = 2; i < sortedVertices.Count; i++)
            {
                ret.Add(sortedVertices[i]);
                while (IsLastClockwise(ret) == false)
                    ret.RemoveAt(ret.Count - 2);
            }

            // 下方の凸形状計算
            ret.Add(sortedVertices[^2]);
            for (var i = sortedVertices.Count - 3; i >= 0; --i)
            {
                ret.Add(sortedVertices[i]);
                while (IsLastClockwise(ret) == false)
                    ret.RemoveAt(ret.Count - 2);
            }

            return ret;
        }

        /// <summary>
        /// 凸多角形を計算して返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static List<Vector2> ComputeConvexVolume(IEnumerable<Vector2> vertices)
        {
            // リストの最後の辺が時計回りになっているかを確認
            bool IsLastClockwise(List<Vector2> list)
            {
                if (list.Count <= 2)
                    return true;
                return Vector2Ex.Cross(list[^1] - list[^2], list[^2] - list[^3]) > 0;
            }

            var sortedVertices = vertices.OrderBy(v => v.x).ThenBy(v => v.y).Distinct().ToList();
            var compare = new Vector2Equitable(Epsilon);
            for (var i = 0; i < sortedVertices.Count - 1;)
            {
                if (compare.Equals(sortedVertices[i], sortedVertices[i + 1]))
                    sortedVertices.RemoveAt(i + 1);
                else
                    i++;
            }
            if (sortedVertices.Count <= 2)
                return new List<Vector2>();

            // 上方の凸形状計算
            var ret = new List<Vector2> { sortedVertices[0], sortedVertices[1] };
            for (var i = 2; i < sortedVertices.Count; i++)
            {
                ret.Add(sortedVertices[i]);
                while (IsLastClockwise(ret) == false)
                    ret.RemoveAt(ret.Count - 2);
            }

            // 下方の凸形状計算
            ret.Add(sortedVertices[^2]);
            for (var i = sortedVertices.Count - 3; i >= 0; --i)
            {
                ret.Add(sortedVertices[i]);
                while (IsLastClockwise(ret) == false)
                    ret.RemoveAt(ret.Count - 2);
            }

            return ret;
        }

        /// <summary>
        /// 点群vertices, 互いの距離がepsilon以下の頂点をリンクさせた辞書が返る
        /// 辞書にない頂点は近い頂点が無いもの
        /// key : 元の頂点, value : 近い頂点のうち最初に見つかったもの
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="calcDistance"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static int[] GetNearVertexTable(IList<Vector3> vertices, Func<Vector3, Vector3, float> calcDistance, float epsilon = 0.1f)
        {
            // 全頂点の最小値を基準値にする
            var o = vertices.Aggregate(Vector3.one * float.MaxValue, Vector3.Min);
            // すべての頂点に対して２重ループで回すと遅そうなのでソートして枝切りする
            // 原点からの距離でソートする
            // v[i]とv[j]の原点までの距離の差がepsilonを超えるとお互いの距離がepsilon以内ではない
            var orderedVertices = vertices
                .Select((v, i) => new { v, d = calcDistance(o, v), i })
                .ToList();

            //orderedVertices.Sort((a, b) => Comparer<float>.Default.Compare(a.d, b.d));
            var indices = Enumerable.Range(0, orderedVertices.Count).ToArray();
            for (var i = 0; i < orderedVertices.Count; ++i)
            {
                var v = orderedVertices[i];
                var index = indices[i];
                for (var j = i + 1; j < orderedVertices.Count; j++)
                {
                    // epsilonを超えた場合はそれ以上は見なくてよい
                    //if ((orderedVertices[j].d - v.d) > epsilon)
                    //    break;

                    if (calcDistance(v.v, orderedVertices[j].v) < epsilon)
                    {
                        if (indices[j] == j)
                        {
                            indices[j] = index;
                        }
                        else
                        {
                            var j2 = indices[j];
                            while (j2 != indices[j2]) j2 = indices[j2];

                            var i2 = indices[i];
                            while (i2 != indices[i2]) i2 = indices[i2];

                            if (j2 < i2)
                            {
                                indices[i2] = j2;
                            }
                            else
                            {
                                indices[j2] = i2;
                            }
                        }
                    }
                }
            }

            for (var i = 0; i < indices.Length; ++i)
            {
                var j = indices[i];
                while (j != indices[j]) j = indices[j];
                indices[i] = j;
            }

            return indices;
            //return indices.Select(i => orderedVertices[i].i).ToArray();
        }

        public static List<Vector3> ComputeMeshOutlineVertices(IReadOnlyList<Vector3> vert, IList<int> triangles, Func<Vector3, Vector2> toVec2, float epsilon = 0.1f)
        {
#if false
            var mergeTable = GetNearPointTable(vert, (a, b) => (a - b).Xz().magnitude, epsilon);
            Vector3 Convert(Vector3 v)
            {
                if (mergeTable.TryGetValue(v, out var ret))
                    return ret;
                return v;
            }
#else
            Vector3 Convert(Vector3 v)
            {

                var x = (int)(v.x / epsilon);
                var y = (int)(v.y / epsilon);
                var z = (int)(v.z / epsilon);
                return new Vector3(x * epsilon, y * epsilon, z * epsilon);
            }
#endif
            // key   : 頂点
            // value : その頂点とつながっている頂点のリスト
            var vertices = new Dictionary<Vector3, HashSet<Vector3>>();

            void Add(HashSet<Vector3> list, Vector3 v)
            {
                if (list.Contains(v) == false)
                    list.Add(v);
            }
            for (var i = 0; i < triangles.Count; i += 3)
            {
                var p0 = Convert(vert[triangles[i]]);
                var p1 = Convert(vert[triangles[i + 1]]);
                var p2 = Convert(vert[triangles[i + 2]]);

                var p0V = vertices.GetValueOrCreate(p0);
                var p1V = vertices.GetValueOrCreate(p1);
                var p2V = vertices.GetValueOrCreate(p2);

                Add(p0V, p1);
                Add(p0V, p2);
                Add(p1V, p2);
                Add(p1V, p0);
                Add(p2V, p0);
                Add(p2V, p1);
            }

            if (vertices.Count <= 2)
                return new List<Vector3>();

            return ComputeOutlineVertices(toVec2, vertices);
        }

        public class ComputeOutlineResult<T>
        {
            // アウトライン頂点
            public List<T> Outline { get; set; } = new List<T>();

            // 成功したかどうか
            public bool Success { get; set; }

            // 自己ループが存在する(同じ点が複数回出てくる)
            public bool HasSelfCrossing { get; set; }

            public ComputeOutlineResult()
            {
            }

            public ComputeOutlineResult(bool success, bool hasSelfCrossing, List<T> outline)
            {
                Outline = outline;
                Success = success;
                HasSelfCrossing = hasSelfCrossing;
            }
        }

        /// <summary>
        /// アウトライン頂点を返す
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices"></param>
        /// <param name="toVec3"></param>
        /// <param name="plane"></param>
        /// <param name="getNeighbor"></param>
        /// <returns></returns>
        public static ComputeOutlineResult<T> ComputeOutline2<T>(
            IEnumerable<T> vertices
            , Func<T, Vector3> toVec3
            , AxisPlane plane
            , Func<T, IEnumerable<T>> getNeighbor
            )
        {
            var res = new ComputeOutlineResult<T>();
            var comp = Comparer<float>.Default;
            var keys = vertices.ToList();
            if (keys.Count == 0)
                return res;

            if (keys.Count <= 2)
            {
                res.Outline = keys;
                res.Success = false;
                return res;
            }

            Vector2 ToVec2(T a) => toVec3(a).GetTangent(plane);


            keys.Sort((a, b) =>
            {
                var a2 = ToVec2(a);
                var b2 = ToVec2(b);
                var x = comp.Compare(a2.x, b2.x);
                var y = comp.Compare(a2.y, b2.y);
                if (x != 0)
                    return x;
                return y;
            });

            var convexVertices = ComputeConvexVolume(keys, toVec3, plane, 1e-3f);

            // success : outlineVerticesがきれいにループしている
            // hasSelfCrossing : 途中に同じ点が２回出てくる(直線１本でつながっている個所がある
            // outlineVertices : アウトライン頂点
            (bool success, bool hasSelfCrossing, List<T> outlineVertices)
                Search
                (
                    T start
                    , Vector2 dir
                    , Func<(float ang, float sqrLen), (float ang, float sqrLen), int> compare
                    )
            {
                var convexIndex = 0;
                var ret = new List<T> { start };
                var hasCrossing = false;
                (float ang, float sqrLen) Eval(Vector2 axis, Vector2 a)
                {
                    var ang = Vector2.SignedAngle(axis, a);
                    if (ang < 0f)
                        ang += 360f;
                    var sqrLen = a.sqrMagnitude;
                    return new(ang, sqrLen);
                }
                while (true)
                {
                    var last = ToVec2(ret[^1]);

                    var neighbors = getNeighbor(ret[^1]).ToList();
                    if (neighbors.Count == 0)
                        break;

                    bool TryCalcNext(out T next)
                    {
                        var nextConvexIndex = (convexIndex + 1) % convexVertices.Count;
                        if (neighbors.Contains(convexVertices[nextConvexIndex]))
                        {
                            next = convexVertices[nextConvexIndex];
                            convexIndex = nextConvexIndex;
                            return true;
                        }

                        // 途中につながるようなものは削除
                        var filtered = ret.Count >= 2 ? neighbors.Where(v => ret[^2].Equals(v) == false).ToList() : neighbors;
                        if (filtered.Count == 0)
                            filtered = neighbors;
                        if (filtered.Count == 0)
                        {
                            next = default(T);
                            return false;
                        }

                        next = filtered.First();

                        var eval0 = Eval(dir, ToVec2(next) - last);
                        foreach (var v in filtered.Skip(1))
                        {
                            // 最も外側に近い点を返す
                            var eval1 = Eval(dir, ToVec2(v) - last);
                            if (compare(eval0, eval1) < 0)
                            {
                                next = v;
                                eval0 = eval1;
                            }
                        }

                        return true;
                    }

                    if (TryCalcNext(out var next) == false)
                        break;

                    // 先頭に戻ってきたら終了
                    if (ret[0].Equals(next))
                        return new(true, hasCrossing, ret);

                    // 途中に戻ってくる場合
                    var index = ret.IndexOf(next);
                    if (index >= 0)
                    {
                        // ループを検出したら終了
                        if (index > 0 && ret[index - 1].Equals(ret[^1]))
                        {
                            return new(false, hasCrossing, ret);
                        }
                        hasCrossing = true;
                    }

                    ret.Add(next);
                    dir = last - ToVec2(next);

                }

                return new(false, hasCrossing, ret);
            }

            // 時計回りに探し出す
            var leftSearch = Search(
                    convexVertices[0]
                    , Vector2.down
                    , (a, b) =>
                    {
                        var x = -comp.Compare(b.ang, a.ang);
                        if (x == 0)
                            x = comp.Compare(b.sqrLen, a.sqrLen);
                        return x;
                    }
                );
            // 見つかったらそれでおしまい
            res.Success = leftSearch.success;
            res.Outline = leftSearch.outlineVertices;
            res.HasSelfCrossing = leftSearch.hasSelfCrossing;
            if (res.Success)
                return res;

            res.HasSelfCrossing = res.Outline.GroupBy(v => v).Any(g => g.Count() > 1);
            if (res.HasSelfCrossing)
                Debug.Log("アウトライン計算でループ検出");

            return res;
        }

        /// <summary>
        /// アウトライン頂点を返す
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices"></param>
        /// <param name="toVec3"></param>
        /// <param name="plane"></param>
        /// <param name="getNeighbor"></param>
        /// <returns></returns>
        public static ComputeOutlineResult<T> ComputeOutline<T>(
            IEnumerable<T> vertices
            , Func<T, Vector3> toVec3
            , AxisPlane plane
            , Func<T, IEnumerable<T>> getNeighbor
            )
        {
            var res = new ComputeOutlineResult<T>();
            var comp = Comparer<float>.Default;
            var keys = vertices.ToList();
            if (keys.Count == 0)
                return res;

            if (keys.Count <= 2)
            {
                res.Outline = keys;
                res.Success = false;
                return res;
            }

            Vector2 ToVec2(T a) => toVec3(a).GetTangent(plane);
            keys.Sort((a, b) =>
            {
                var a2 = ToVec2(a);
                var b2 = ToVec2(b);
                var x = comp.Compare(a2.x, b2.x);
                var y = comp.Compare(a2.y, b2.y);
                if (x != 0)
                    return x;
                return y;
            });

            // success : outlineVerticesがきれいにループしている
            // hasSelfCrossing : 途中に同じ点が２回出てくる(直線１本でつながっている個所がある
            // outlineVertices : アウトライン頂点
            ComputeOutlineResult<T>
                Search
                (
                    T start
                    , Vector2 dir
                    , Func<(float ang, float sqrLen), (float ang, float sqrLen), int> compare
                    )
            {
                var ret = new List<T> { start };
                var hasCrossing = false;
                (float ang, float sqrLen) Eval(Vector2 axis, Vector2 a)
                {
                    var ang = Vector2.SignedAngle(axis, a);
                    if (ang < 0f)
                        ang += 360f;
                    var sqrLen = a.sqrMagnitude;
                    return new(ang, sqrLen);
                }
                while (true)
                {
                    var last = ToVec2(ret[^1]);
                    var neighbors = getNeighbor(ret[^1]).ToList();
                    if (neighbors.Count == 0)
                        break;
                    // 途中につながるようなものは削除
                    var filtered = ret.Count >= 2 ? neighbors.Where(v => ret[^2].Equals(v) == false).ToList() : neighbors;
                    if (filtered.Count == 0)
                        filtered = neighbors;
                    if (filtered.Count == 0)
                        break;
                    var next = filtered.First();

                    var eval0 = Eval(dir, ToVec2(next) - last);
                    foreach (var v in filtered.Skip(1))
                    {
                        // 最も外側に近い点を返す
                        var eval1 = Eval(dir, ToVec2(v) - last);
                        if (compare(eval0, eval1) < 0)
                        {
                            next = v;
                            eval0 = eval1;
                        }
                    }

                    // 先頭に戻ってきたら終了
                    if (ret[0].Equals(next))
                        return new(true, hasCrossing, ret);

                    // 途中に戻ってくる場合
                    var index = ret.IndexOf(next);
                    if (index >= 0)
                    {
                        // ループを検出したら終了
                        if (index > 0 && ret[index - 1].Equals(ret[^1]))
                        {
                            return new(false, hasCrossing, ret);
                        }
                        hasCrossing = true;
                    }

                    ret.Add(next);
                    dir = last - ToVec2(next);
                }

                return new(false, hasCrossing, ret);
            }

            // 時計回りに探し出す
            var leftSearch = Search(
                    keys[0]
                    , Vector2.down
                    , (a, b) =>
                    {
                        var x = -comp.Compare(b.ang, a.ang);
                        if (x == 0)
                            x = comp.Compare(b.sqrLen, a.sqrLen);
                        return x;
                    }
                );
            // 見つかったらそれでおしまい
            if (leftSearch.Success)
                return leftSearch;

            // 見つからない場合(３次元的なねじれの位置がある場合等)
            // 反時計回りにも探す
            var rightSearch = Search(
                    keys[0]
                    , Vector2.up
                    , (a, b) =>
                    {
                        var x = comp.Compare(b.ang, a.ang);
                        if (x == 0)
                            x = comp.Compare(b.sqrLen, a.sqrLen);
                        return x;
                    }
                );
            // 右回りで見つかったらそれでおしまい
            if (rightSearch.Success)
                return rightSearch;

            // 両方の結果をマージする
            res.Outline = leftSearch.Outline.ToList();
            // 0番目は共通なので削除
            rightSearch.Outline.RemoveAt(0);
            while (rightSearch.Outline.Count > 0)
            {
                var v = rightSearch.Outline[0];
                rightSearch.Outline.RemoveAt(0);
                var index = res.Outline.IndexOf(v);
                if (index >= 0)
                {
                    res.Success = true;
                    res.Outline.RemoveRange(index, res.Outline.Count - index);
                    res.Outline.Add(v);
                    break;
                }
                res.Outline.Add(v);
            }

            res.HasSelfCrossing = res.Outline.GroupBy(v => v).Any(g => g.Count() > 1);
            if (res.HasSelfCrossing)
                Debug.Log("アウトライン計算でループ検出");

            return res;
        }

        private static List<Vector3> ComputeOutlineVertices(Func<Vector3, Vector2> toVec2, Dictionary<Vector3, HashSet<Vector3>> vertices, bool ignoreVisitedVertex = true)
        {
            var comp = Comparer<float>.Default;

            var keys = vertices.Keys.ToList();
            keys.Sort((a, b) =>
            {
                var a2 = toVec2(a);
                var b2 = toVec2(b);
                var x = comp.Compare(a2.x, b2.x);
                var y = comp.Compare(a2.y, b2.y);
                if (x != 0)
                    return x;
                return y;
            });

            void Eval(Vector2 axis, Vector2 a, out float ang, out float sqrLen)
            {
                ang = Vector2.SignedAngle(axis, a);
                if (ang < 0f)
                    ang += 360f;
                sqrLen = a.sqrMagnitude;
            }

            // 時計回りに探し出す
            var dir = Vector2.down;
            var ret = new List<Vector3> { keys[0] };
            while (ret.Count < vertices.Count)
            {
                var last = toVec2(ret[^1]);
                var neighbors = vertices[ret[^1]];
                if (neighbors.Count == 0)
                    break;
                // 途中につながるようなものは削除
                var filtered = ignoreVisitedVertex ? neighbors.Where(v => v == ret[0] || ret.Contains(v) == false).ToList() : neighbors.ToList();
                if (filtered.Count == 0)
                    break;
                Vector3 next = filtered.First();

                Eval(dir, toVec2(next) - last, out var ang, out var sqrLen);
                foreach (var v in filtered.Skip(1))
                {
                    // 最も外側に近い点を返す
                    Eval(dir, toVec2(v) - last, out var ang2, out var sqrLen2);
                    var x = -comp.Compare(ang2, ang);
                    if (x == 0)
                        x = comp.Compare(sqrLen2, sqrLen);
                    if (x < 0)
                    {
                        next = v;
                        ang = ang2;
                        sqrLen = sqrLen2;
                    }
                }

                if (ret.Contains(next))
                {
                    break;
                }

                ret.Add(next);
                dir = last - toVec2(next);
            }
            return ret;
        }

        /// <summary>
        /// meshのアウトラインを計算する
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="toVec2"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static List<Vector3> ComputeMeshOutlineVertices(UnityEngine.Mesh mesh, Func<Vector3, Vector2> toVec2, float epsilon = 0.1f)
        {
            return ComputeMeshOutlineVertices(mesh.vertices, mesh.triangles, toVec2, epsilon);
        }


        /// <summary>
        /// meshのアウトラインを計算する
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="subMesh"></param>
        /// <param name="toVec2"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static List<Vector3> ComputeMeshOutlineVertices(SubDividedCityObject.Mesh mesh, SubDividedCityObject.SubMesh subMesh, Func<Vector3, Vector2> toVec2, float epsilon = 0.1f)
        {
            // key   : 頂点
            // value : その頂点とつながっている頂点のリスト
            var vertices = new Dictionary<Vector3, HashSet<Vector3>>();

            void Add(HashSet<Vector3> list, Vector3 v)
            {
                //if (list.Contains(v) == false)
                list.Add(v);
            }
            for (var j = 0; j < subMesh.Triangles.Count; j += 3)
            {
                var p0 = mesh.Vertices[subMesh.Triangles[j]];
                var p1 = mesh.Vertices[subMesh.Triangles[j + 1]];
                var p2 = mesh.Vertices[subMesh.Triangles[j + 2]];

                var p0V = vertices.GetValueOrCreate(p0);
                var p1V = vertices.GetValueOrCreate(p1);
                var p2V = vertices.GetValueOrCreate(p2);

                Add(p0V, p1);
                Add(p0V, p2);
                Add(p1V, p2);
                Add(p1V, p0);
                Add(p2V, p0);
                Add(p2V, p1);
            }
            if (vertices.Count <= 2)
                return new List<Vector3>();
            return ComputeOutlineVertices(toVec2, vertices);
        }

        /// <summary>
        /// ポリゴンを構成する頂点配列を渡すと, そのポリゴンが時計回りなのか反時計回りなのかを返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static bool IsClockwise(IEnumerable<Vector2> vertices)
        {
            var total = GeoGraphEx.GetEdges(vertices, true).Sum(item => Vector2Ex.Cross(item.Item1, item.Item2));
            return total < 0;
        }

        /// <summary>
        /// verticesで表される多角形が点pを内包するかどうか
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static bool Contains(IEnumerable<Vector2> vertices, Vector2 p)
        {
            // https://www.nttpc.co.jp/technology/number_algorithm.html
            bool Check(Vector2 c, Vector2 v)
            {
                // 上向きの辺。点Pがy軸方向について、始点と終点の間にある。ただし、終点は含まない。(ルール1)
                // 下向きの辺。点Pがy軸方向について、始点と終点の間にある。ただし、始点は含まない。(ルール2)
                if (((c.y <= p.y) && (v.y > p.y)) || ((c.y > p.y) && (v.y <= p.y)))
                {
                    // ルール1,ルール2を確認することで、ルール3も確認できている。
                    // 辺は点pよりも右側にある。ただし、重ならない。(ルール4)
                    // 辺が点pと同じ高さになる位置を特定し、その時のxの値と点pのxの値を比較する。
                    var vt = (p.y - c.y) / (v.y - c.y);
                    if (p.x < (c.x + (vt * (v.x - c.x))))
                    {
                        return true;
                    }
                }

                return false;
            }

            var cnt = GeoGraphEx.GetEdges(vertices, true).Count(item => Check(item.Item1, item.Item2));
            return (cnt % 2) == 1;
        }

        /// <summary>
        /// 頂点verticesで構成されるポリゴン(isLoop = falseの時は開いている)と半直線rayとの交点を返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="ray"></param>
        /// <param name="intersection"></param>
        /// <param name="t"></param>
        /// <param name="isLoop"></param>
        /// <returns></returns>
        public static bool PolygonHalfLineIntersection(IEnumerable<Vector2> vertices, Ray2D ray, out Vector2 intersection, out float t, bool isLoop = true)
        {
            var ret = GeoGraphEx.GetEdges(vertices, isLoop)
                .Select(p =>
                {
                    var success = LineUtil.HalfLineSegmentIntersection(ray, p.Item1, p.Item2, out Vector2 intersection,
                        out float f1,
                        out float f2);
                    return new { success, intersection, f1, f2 };
                })
                .Where(p => p.success)
                .TryFindMinElement(p => p.f1, out var o);

            intersection = o?.intersection ?? Vector2.zero;
            t = o?.f1 ?? 0f;
            return ret;
        }

        /// <summary>
        /// 頂点verticesで構成されるポリゴン(isLoop = falseの時は開いている)と半直線rayとの交点を返す.
        /// ただし、y座標は無視してXz平面だけで当たり判定を行う
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="ray"></param>
        /// <param name="intersection"></param>
        /// <param name="t"></param>
        /// <param name="isLoop"></param>
        /// <returns></returns>
        public static bool PolygonHalfLineIntersectionXZ(IEnumerable<Vector3> vertices, Ray ray,
            out Vector3 intersection, out float t, bool isLoop = true)
        {
            var ret = PolygonHalfLineIntersection(vertices.Select(v => v.Xz()),
                new Ray2D(ray.origin.Xz(), ray.direction.Xz()), out Vector2 _, out float f1, isLoop);
            if (ret == false)
            {
                intersection = Vector3.zero;
                t = 0f;
            }
            else
            {
                intersection = ray.origin + ray.direction * f1;
                t = f1;
            }
            return ret;
        }


        /// <summary>
        /// 頂点verticesで構成されるポリゴン(isLoop = falseの時は開いている)と半直線rayとの交点を返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="ray"></param>
        /// <param name="intersection"></param>
        /// <param name="t"></param>
        /// <param name="isLoop"></param>
        /// <returns></returns>
        public static bool PolygonSegmentIntersection(IEnumerable<Vector2> vertices, Vector2 st, Vector2 en, out Vector2 intersection, out float t, bool isLoop = true)
        {
            var ret = GeoGraphEx.GetEdges(vertices, isLoop)
                .Select(p =>
                {
                    var success = LineUtil.SegmentIntersection(st, en, p.Item1, p.Item2, out Vector2 intersection,
                        out float f1,
                        out float f2);
                    return new { success, intersection, f1, f2 };
                })
                .Where(p => p.success)
                .TryFindMinElement(p => p.f1, out var o);

            intersection = o.intersection;
            t = o.f1;
            return ret;
        }

        /// <summary>
        /// 頂点verticesで構成されるポリゴン(isLoop = falseの時は開いている)と半直線rayとの交点を返す.
        /// ただし、y座標は無視してXz平面だけで当たり判定を行う
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="en"></param>
        /// <param name="intersection"></param>
        /// <param name="t"></param>
        /// <param name="isLoop"></param>
        /// <param name="st"></param>
        /// <returns></returns>
        public static bool PolygonSegmentIntersectionXZ(IEnumerable<Vector3> vertices, Vector3 st, Vector3 en,
            out Vector3 intersection, out float t, bool isLoop = true)
        {
            var ret = PolygonHalfLineIntersection(vertices.Select(v => v.Xz()),
                new Ray2D(st.Xz(), en.Xz()), out Vector2 _, out float f1, isLoop);
            if (ret == false)
            {
                intersection = Vector3.zero;
                t = 0f;
            }
            else
            {
                intersection = Vector3.Lerp(st, en, f1);
                t = f1;
            }
            return ret;
        }

        /// <summary>
        /// 自己交差している交点のポイントを作成する関数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seg1Start"></param>
        /// <param name="seg1End"></param>
        /// <param name="seg2Start"></param>
        /// <param name="seg2End"></param>
        /// <param name="intersection"></param>
        /// <param name="seg1T"></param>
        /// <param name="seg2T"></param>
        /// <returns></returns>
        public delegate T CreateSelfCrossingIntersectionPoint<T>(T seg1Start, T seg1End, T seg2Start, T seg2End, Vector2 intersection, float seg1T, float seg2T);

        /// <summary>
        /// pointsで表現された線分リストが自己交差している場合,その部分を削除する
        ///   4--5
        ///   |  |
        ///   3------2
        ///      |   |
        ///      6   1
        /// ↑の様な線分だと以下のようになる
        ///      3---2
        ///      |   |
        ///      4   1
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="points"></param>
        /// <param name="selector"></param>
        /// <param name="creator">交点に新しい点を作成する関数.　p1-p2の線分, p3-p4の線分と交点intersection,  (p1, p2, p3, p4, intersection, t1, t2) -> T</param>
        public static void RemoveSelfCrossing<T>(List<T> points, Func<T, Vector2> selector, CreateSelfCrossingIntersectionPoint<T> creator)
        {
            for (var i = 0; i < points.Count - 2; ++i)
            {
                var p1 = selector(points[i]);
                var p2 = selector(points[i + 1]);
                for (var j = i + 2; j < points.Count - 1;)
                {
                    var p3 = selector(points[j]);
                    var p4 = selector(points[j + 1]);

                    if (LineUtil.SegmentIntersection(p1, p2, p3, p4, out var intersection, out var f1, out var f2))
                    {
                        var newNode = creator(points[i], points[i + 1], points[j], points[j + 1], intersection, f1, f2);
                        points.RemoveRange(i + 1, j - i);
                        points.Insert(i + 1, newNode);
                        // もう一回最初から検索しなおす
                        j = i + 2;
                    }
                    else
                    {
                        ++j;
                    }
                }
            }
        }

        /// <summary>
        /// pointsで表現された線分リストが自己交差している場合,その部分を削除する
        ///   4--5
        ///   |  |
        ///   3------2
        ///      |   |
        ///      6   1
        /// ↑の様な線分だと以下のようになる
        ///      3---2
        ///      |   |
        ///      4   1
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="srcPoints"></param>
        /// <param name="selector"></param>
        /// <param name="creator">交点に新しい点を作成する関数.　p1-p2の線分, p3-p4の線分と交点intersection,  (p1, p2, p3, p4, intersection, t1, t2) -> T</param>
        /// <param name="isLoop"></param>
        public static List<List<T>> ExtractSelfCrossing<T>(
            IReadOnlyList<T> srcPoints
            , Func<T, Vector2> selector
            , CreateSelfCrossingIntersectionPoint<T> creator
            , bool isLoop = false
            )
        {
            var points = srcPoints.ToList();
            List<List<T>> ret = new List<List<T>>();
            for (var i = 0; i < points.Count - 2; ++i)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                var v1 = selector(p1);
                var v2 = selector(p2);
                for (var j = i + 2; j < points.Count;)
                {
                    // ループでない場合は最後の点はチェックしない
                    if (isLoop == false && j == points.Count - 1)
                        break;

                    // i==0とj==points.Count-1の時はループの最初と最後で
                    // 頂点共有だから必ず交差扱いになるので無視する
                    if (i == 0 && j == points.Count - 1)
                        break;
                    var p3 = points[j];
                    var p4 = points[(j + 1) % points.Count];
                    var v3 = selector(p3);
                    var v4 = selector(p4);

                    if (LineUtil.SegmentIntersection(v1, v2, v3, v4, out var intersection, out var f1, out var f2))
                    {
                        var newNode = creator(p1, p2, p3, p4, intersection, f1, f2);

                        var part = points.GetRange(i + 1, j - i);
                        part.Insert(0, newNode);

                        // partがさらに自己交差している場合もあるので再帰的に処理する
                        // partはisLoop=true(交差しているから)
                        ret.AddRange(ExtractSelfCrossing(part, selector, creator, true));
                        points.RemoveRange(i + 1, j - i);
                        points.Insert(i + 1, newNode);
                        // もう一回最初から検索しなおす
                        j = i + 2;
                    }
                    else
                    {
                        ++j;
                    }
                }
            }
            if (points.Any())
                ret.Add(points);
            return ret;
        }

        /// <summary>
        /// 直線a,bがあり. |a.origin-pos|とdistance(pos, b)の比率がp:1-pとなるようなA上点posを返す
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static bool CalcLerpPointInLine(Ray2D a, Ray2D b, float p, out Vector2 pos)
        {
            p = Mathf.Clamp01(p);
            var p2 = p * p;
            var dotDab = Vector2.Dot(a.direction, b.direction);
            var dOg = a.origin - b.origin;
            var dotDao = Vector2.Dot(a.direction, dOg);
            var dotDbo = Vector2.Dot(b.direction, dOg);

            // l(t)上の点
            // |t| : √( |d_a*t + o_a - o_b|^2 - |(d_a*t + o_a - o_b)・d_b|^2) = p : (1-p)
            // |t| : √(|d_a*t + dOg|^2 - |(d_a*t + dOg)・d_b|^2) = p : (1-p)
            // p^2 *  (|d_a*t + dOg|^2 - |(d_a*t + dOg)・d_b|^2  = (1-p)^2*t^2
            // p^2 *  (|d_a|^2*t^2 + 2*dotDao*t + |dOg|^2 - (dotDab*t + dotDbo)^2                         ) - (1-p)^2*t^2 = 0
            // p^2 *  (        t^2 + 2*dotDao*t + |dOg|^2 -  dotDab^2*t^2 - 2 * dotDab*dotDbo*t - dotDbo^2) - (1-p)^2*t^2 = 0
            // (p^2 - p^2*dotDab^2 - (1-p)^2) * t^2 + 2*p^2( dotDao - dotDab*dotDbo)*t + p^2*( dOg^2 - dotDbo^2) = 0
            // (2p-1 - p^2*dotDab^2         ) * t^2 + 2*p^2( dotDao - dotDab*dotDbo)*t + p^2*( dOg^2 - dotDbo^2) = 0

            var A = 2 * p - 1 - p2 * dotDab * dotDab;
            var B = 2 * p2 * (dotDao - dotDab * dotDbo);
            var C = p2 * (dOg.sqrMagnitude - dotDbo * dotDbo);
            var D = B * B - 4 * A * C;
            // D < 0の時は交点が無いが0の計算誤差を考慮する
            if (D < 0f && Mathf.Abs(D) < Epsilon)
                D = 0f;
            pos = Vector3.zero;

            bool Calc(out float t)
            {
                t = -1;
                // 1次方程式の場合
                if (Mathf.Abs(A) < Epsilon)
                {
                    if (B == 0)
                        return false;
                    t = -C / B;
                    return true;
                }
                // 2次方程式の場合
                if (D < 0)
                    return false;

                var t1 = (-B + Mathf.Sqrt(D)) / (2 * A);
                var t2 = (-B - Mathf.Sqrt(D)) / (2 * A);

                t = t1;
                if (Mathf.Abs(t1) > Mathf.Abs(t2))
                {
                    t = t2;
                }

                return true;
            }
            if (Calc(out var t) == false)
                return false;
            pos = a.direction * t + a.origin;
            return true;

        }

        /// <summary>
        /// 直線l上の点から直線a,bへの距離がp : 1-pとなるような直線lを返す
        /// 0.5だと中間の角度が返る
        /// \ p  |1-p /
        ///  \   |   /
        ///   \  |  / 
        ///  a \ | / b
        ///     \ /
        /// </summary>
        /// <param name="rayA"></param>
        /// <param name="rayB"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Ray2D LerpRay2(Ray2D rayA, Ray2D rayB, float p)
        {
            static Vector2 Get(Ray2D r1, Ray2D r2, Vector2 a, float p)
            {
                var b = r2.GetNearestPoint(a);
                if ((a - b).sqrMagnitude < Epsilon)
                {
                    return a;
                }
                var c = r1.GetNearestPoint(b);

                var ab = (b - a).magnitude;
                var bc = (c - b).magnitude;

                var x = ab * (1 - p);
                var t = x / (x + bc * p);
                return Vector2.Lerp(a, b, t);
            }

            p = 1 - Mathf.Clamp01(p);
            var p1 = Get(rayA, rayB, rayA.origin, p);
            var p2 = Get(rayA, rayB, rayA.origin + rayA.direction * 10, p);
            var dir = (p2 - p1).normalized;
            if (Vector2.Dot(dir, rayA.direction) < 0)
                dir = -dir;
            return new Ray2D(p1, dir);
        }

        /// <summary>
        /// 直線l上の点から直線a,bへの距離がp : 1-pとなるような直線lを返す
        /// 0.5だと中間の角度が返る
        /// \ p  |1-p /
        ///  \   |   /
        ///   \  |  / 
        ///  a \ | / b
        ///     \ /
        /// </summary>
        /// <param name="rayA"></param>
        /// <param name="rayB"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Ray2D LerpRay(Ray2D rayA, Ray2D rayB, float p)
        {
            // 2線が平行の時は交点が無いので特別処理
            if (LineUtil.LineIntersection(rayA, rayB, out var intersection, out var t1, out var t2) == false)
            {
                var aPos = Vector2.Dot(rayB.origin - rayA.origin, rayA.direction) * rayA.direction + rayA.origin;
                var origin = Vector2.Lerp(aPos, rayB.origin, p);
                return new Ray2D(origin, rayA.direction);
            }

            var dirA = rayA.direction;
            var dirB = rayB.direction;

            var radX = Mathf.Deg2Rad * Vector2.Angle(dirA, dirB);
            var siX = Mathf.Sin(radX);
            var coX = Mathf.Cos(radX);
            // a-b間の角度をx
            // a-l間の角度A
            // l-b間の角度(x - A)
            // sin(A) : sin(B) = p : (1-p)
            // B = X - A
            // sin(A) : sin(X - A) = p : (1-p)
            // Sin(A) : sin(X)cos(A) - cos(X)sin(A) = p : (1-p)
            // (1-p)Sin(A) = p ( sin(X)cos(A) - cos(X)sin(A))
            // ((1-p) + p * cos(X))sin(A) = p*sin(X)cos(A)
            // tan(A) = p*sin(X) / ((1-p) + p * cos(X))
            var radA = Mathf.Atan2(p * siX, 1 - p + p * coX);
            var dir = Vector2Ex.RotateTo(dirA, dirB, radA);

            // a,bが平行に近いとintersectionが遠点となりfloat誤差が発生するため, a,bのStartからdirへの射影をして見つかった位置をoriginにする
            var inters = new List<Vector2>(2);
            // rayAの法線上の点posにおいて, len(rayA.origin - pos) : distance(pos - rayB) = p : 1-pとなる点は, 答えのray上にある
            if (CalcLerpPointInLine(new Ray2D(rayA.origin, rayA.direction.Rotate(90)), rayB, p, out var pos))
            {
                inters.Add(pos);
            }
            if (CalcLerpPointInLine(new Ray2D(rayB.origin, rayB.direction.Rotate(90)), rayA, p, out var pos2))
            {
                inters.Add(pos2);
            }

            if (inters.Count == 0)
                return new Ray2D(intersection, dir);

            if (inters.Count == 1)
                return new Ray2D(inters[0], dir);

            if (Vector2.Dot(dir, inters[1] - inters[0]) > 0)
                return new Ray2D(inters[0], dir);
            return new Ray2D(inters[1], dir);
        }

        public class BorderParabola2D
        {
            public float A { get; set; }

            public float B { get; set; }

            public float C0 { get; set; }

            public float P { get; set; }

            public float Y0 { get; set; }

            // p=0.5以外の時は条件を満たすxには限界がある
            public float? RangeX { get; set; }
            public Vector2 Origin { get; set; }

            public Vector2 AxisX { get; set; }

            public Vector2 AxisY { get; set; }

            public BorderParabola2D(Vector2 origin, Vector2 axisX, Vector2 axisY, float y0, float p)
            {
                // 以下の方程式を解く
                // x^2 + (y0 - y)^2 : y^2 = (1-p)^2 : p^2
                Origin = origin;
                AxisX = axisX;
                AxisY = axisY;
                P = p;
                Y0 = y0;
                var p2 = p * p;
                A = 2 * p - 1;
                B = 2 * Y0 * p2;
                C0 = p2 * Y0 * Y0;
                // 以下の二次方程式になる
                // ay^2 + 2by + c0 + p^2x^2 = 0

                //var c0 = 2 * p2 * y0;
                //var c1 = 4 * p4 * y02 - 4 * d * p2 * y02;
                //var c2 = -4 * d * p2;
                // y = (c0 ± √(c1 + c2*x^2)) / 2d

                var c = C0 + P * P * 0 * 0;
                var d = B * B - 4 * A * C0;
                if (d > 0f && A > 0f)
                {
                    RangeX = Mathf.Sqrt(d / p2);
                }

            }

            float GetY(float x)
            {
                if (Mathf.Abs(A) < GeoGraph2D.Epsilon)
                    return x * x / (2 * Y0) + Y0 / 2f;
                var c = C0 + P * P * x * x;
                var d = B * B - 4 * A * c;
                return (B - Mathf.Sqrt(d)) / (2 * A);
            }

            public Vector2 GetPoint(float x)
            {
                var local = new Vector2(x, GetY(x));
                return local.x * AxisX + local.y * AxisY + Origin;
            }

        }


        /// <summary>
        /// vと直線rayをp : 1-pで分割する放物線を返す
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="v"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static BorderParabola2D GetBorderParabola(Ray2D ray, Vector2 v, float p)
        {
            p = Mathf.Clamp01(p);
            var origin = ray.GetNearestPoint(v, out var t);
            //var origin = Vector2.Lerp(near, v, p);
            //
            var dir = (v - origin);
            var rot = Vector2.SignedAngle(Vector2.up, dir);
            var y0 = (v - origin).magnitude;// * (Vector2Util.Cross(ray.direction, v - origin) < 0f ? -1f : 1f);
            return new BorderParabola2D(origin, ray.direction, dir.normalized, y0, p);
        }

        [Serializable]
        public class DebugOption
        {
            public int showLeftIndex = -1;
            public int showRightIndex = -1;
            public bool showParabolaStart = true;
            public bool showParabolaEnd = true;
            public bool showRemoveSegment = true;
            public bool showReturnSegments = true;
        }

        public class NearestPointInfo
        {
            public int Index { get; set; }

            public float T { get; set; }

            public float Distance { get; set; }

            public static int Compare(NearestPointInfo a, NearestPointInfo b)
            {
                var ret = Comparer<int>.Default.Compare(a.Index, b.Index);
                if (ret != 0)
                    return ret;
                return Comparer<float>.Default.Compare(a.T, b.T);
            }

            public override string ToString()
            {
                return $"{Index}/{T:F2}";
            }
        }

        public class InnerSegment
        {
            public LineSegment2D Segment { get; set; }

            public int LeftIndex { get; }

            public int RightIndex { get; }

            public NearestPointInfo LeftNearestStartInfo { get; set; }

            public NearestPointInfo LeftNearestEndInfo { get; set; }

            public NearestPointInfo RightNearestStartInfo { get; set; }

            public NearestPointInfo RightNearestEndInfo { get; set; }

            public bool IsStartLeft { get; set; }

            public bool IsEndLeft { get; set; }

            // Left:Right = P : (1-P)となるP
            public float P { get; set; }

            // 輪郭線と交差しているかどうか
            public bool IsCrossed { get; set; }

            public float GetDistEval(bool isStart)
            {
                var (left, right) = isStart ? (LeftNearestStartInfo, RightNearestStartInfo) : (LeftNearestEndInfo, RightNearestEndInfo);

                var leftDist = left.Distance / P;
                var rightDist = right.Distance / P;
                return Mathf.Min(leftDist, rightDist);
            }

            public NearestPointInfo GetNearestInfo(bool isStart)
            {
                return isStart ? LeftNearestStartInfo : LeftNearestEndInfo;
            }


            public InnerSegment(LineSegment2D segment, int leftIndex, int rightIndex, bool isStartLeft, bool isEndLeft, float p)
            {
                Segment = segment;
                LeftIndex = leftIndex;
                RightIndex = rightIndex;
                P = p;
                IsStartLeft = isStartLeft;
                IsEndLeft = isEndLeft;
            }

            public override string ToString()
            {
                //return $"{LeftIndex}-{RightIndex}(L={LeftNearestStartInfo}-{LeftNearestEndInfo},R={RightNearestStartInfo}-{RightNearestEndInfo}";
                return $"{LeftIndex}-{RightIndex}";
            }

            public static int Compare(InnerSegment a, InnerSegment b)
            {
                var ret = NearestPointInfo.Compare(a.LeftNearestStartInfo, b.LeftNearestStartInfo);
                if (ret != 0)
                    return ret;
                return NearestPointInfo.Compare(a.RightNearestStartInfo, b.RightNearestStartInfo);
            }
        }

        /// <summary>
        /// selfの左右の道を横幅p : (1-p)で分割した線分を返す. p=[0,1]
        /// 例) 0.5だと中央線が返る, 0だとLeftが返る, 1だとRightが返る. 
        /// </summary>
        /// <param name="seg1"></param>
        /// <param name="p"></param>
        /// <param name="op"></param>
        /// <param name="seg0"></param>
        /// <returns></returns>
        public static List<InnerSegment> GetInnerLerpSegments(IReadOnlyList<Vector2> seg0, IReadOnlyList<Vector2> seg1, float p, DebugOption op = null)
        {
            p = Mathf.Clamp01(p);
            var lefts = GeoGraphEx.GetEdges(seg0, false).Select(v => new LineSegment2D(v.Item1, v.Item2)).ToList();
            var rights = GeoGraphEx.GetEdges(seg1, false).Select(v => new LineSegment2D(v.Item1, v.Item2)).ToList();

            var floatComparer = Comparer<float>.Default;

            var innerSegments = new List<InnerSegment>();


            var q = from m in Enumerable.Range(0, lefts.Count)
                    from n in Enumerable.Range(0, rights.Count)
                    select new { leftIndex = m, rightIndex = n };

            void ChangeSegment(InnerSegment ev, LineSegment2D segment)
            {
                var isHit = false;
                NearestPointInfo FindNearestPoint(List<LineSegment2D> edges, Vector2 pos)
                {
                    Enumerable.Range(0, edges.Count)
                        .Select(i =>
                        {
                            var e = edges[i];
                            var near = e.GetNearestPoint(pos, out var t);
                            if (e.TrySegmentIntersection(segment, out var inter, out var t1, out var t2) && t2 > Epsilon && t2 < segment.Magnitude - Epsilon)
                            {
                                isHit = true;
                            }
                            return new { dist = (near - pos).magnitude, index = i, t = t, isHit = isHit };
                        }).TryFindMinElement(x => x.dist, out var x);
                    return new NearestPointInfo() { Index = x.index, T = x.t, Distance = x.dist, };
                }

                ev.Segment = segment;
                ev.LeftNearestStartInfo = FindNearestPoint(lefts, segment.Start);
                ev.LeftNearestEndInfo = FindNearestPoint(lefts, segment.End);
                ev.RightNearestStartInfo = FindNearestPoint(rights, segment.Start);
                ev.RightNearestEndInfo = FindNearestPoint(rights, segment.End);
                ev.IsCrossed = isHit;
            }

            {

                var leftIndex = 0;
                var rightIndex = 0;
                while (leftIndex < lefts.Count && rightIndex < rights.Count)
                {
                    var l = lefts[leftIndex];
                    var r = rights[rightIndex];
                    var centerRay = GeoGraph2D.LerpRay(l.Ray, r.Ray, p);
                    var dirL = new Vector2(l.Direction.y, -l.Direction.x);
                    var dirR = new Vector2(r.Direction.y, -r.Direction.x);
                    var points = new[]
                        {
                        new { ray = new Ray2D(l.Start, dirL), isLeft = true },
                        new { ray = new Ray2D(l.End, dirL), isLeft = true },
                        new { ray = new Ray2D(r.Start, dirR), isLeft = false },
                        new { ray = new Ray2D(r.End, dirR), isLeft = false }
                    }
                        .Select(x =>
                        {
                            var hit = LineUtil.LineIntersection(centerRay, x.ray, out var inter, out var t1, out var t2);
                            var other = x.isLeft ? r : l;
                            var nearPos = other.GetNearestPoint(x.ray.origin);
                            return new
                            {
                                isHit = hit,
                                inter = inter,
                                tCenterRay = t1,
                                tRay = t2,
                                isLeft = x.isLeft,
                                origin = x.ray.origin
                            };
                        })
                        //.Where(x => x.isHit)
                        .ToList();

                    points.Sort((a, b) => floatComparer.Compare(a.tCenterRay, b.tCenterRay));

                    // ベースラインの切り替えが走ったかどうか
                    if (points.Any() == false)
                        break;

                    // 重ならない時は無視
                    if (points.Count > 1 && points[0].isLeft != points[1].isLeft)
                    {
                        // left-rightが重ならない時は無視
                        //if (baseRef.HasValue && points.Count == 4 && points[0].Item3 != points[1].Item3)
                        void Add()
                        {
                            var begSeg = points[1];
                            var endSeg = points[2];
                            //if (begSeg.tCenterRay < -Epsilon && endSeg.tCenterRay < -Epsilon)
                            //    return;

                            var segment = new LineSegment2D(points[1].inter, points[2].inter);
                            var ev = new InnerSegment(segment, leftIndex, rightIndex, begSeg.isLeft, endSeg.isLeft, p);
                            ChangeSegment(ev, segment);
                            innerSegments.Add(ev);
                            if (op?.showReturnSegments ?? false)
                            {
                                DebugEx.DrawString(ev.ToString(), ev.Segment.Start);
                            }
                        }

                        Add();
                    }


                    // より遠いのが左の場合右の線分を進める
                    if (points.Last().isLeft)
                    {
                        rightIndex++;
                    }
                    else
                    {
                        leftIndex++;
                    }
                }
            }
            innerSegments.Sort(InnerSegment.Compare);
            return innerSegments;
        }

        /// <summary>
        /// verticesで表される線分を, toleranceAngleDeg/midPointToleranceを使った同一直線判定でグルーピングする
        /// 戻り値はグループの始点インデックス
        /// 例) (v0, v1, v2, v3, v4, v5)
        /// v0~v1, v1~v5が同一直線なら[0, 2]が返る
        /// v0~v5が同一直線なら[0]が返る
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="toleranceAngleDeg"></param>
        /// <param name="midPointTolerance"></param>
        /// <returns></returns>
        public static List<int> SplitByCollinearSegment(IReadOnlyList<Vector2> vertices, float toleranceAngleDeg = 0f, float midPointTolerance = 0f, bool checkAdjustSegmentAngleOnly = false)
        {
            return GeoGraphEx.SplitByCollinearSegment(vertices, v => (Vector3)v, toleranceAngleDeg, midPointTolerance);
        }

        [Serializable]
        public class DebugFindOppositeOption
        {
            public bool showCenterLine = false;
            public Color showCenterLineColor = Color.blue;
            public int pattern = 0;
        }

        /// <summary>
        /// edgesで定義された線分リストのedgeBaseIndex番の辺を基準に, 同一直線となる辺を探す.(許容誤差はtoleranceAngleDegForMidEdge)
        /// </summary>
        /// <param name="edgeBaseIndex"></param>
        /// <param name="edges"></param>
        /// <param name="toleranceAngleDegForMidEdge"></param>
        /// <returns></returns>
        public static List<int> FindCollinearRange(
            int edgeBaseIndex
            , IReadOnlyList<LineSegment2D> edges
            , float toleranceAngleDegForMidEdge = 20f
            , int? startIndex = null
            , int? endIndex = null
            )
        {
            startIndex ??= 0;
            endIndex ??= edges.Count - 1;

            var ret = new List<int> { edgeBaseIndex };
            var stop = new[] { false, false };
            while (stop.Contains(false) && ret.Count < edges.Count - 1)
            {
                // 0 : left用
                // 1 : right用
                var infos = new[]
                {
                    new {now=ret.First(), d = -1 },
                    new {now=ret.Last() , d = +1 }
                };
                // 差が小さいほうから見る
                var es = Enumerable.Range(0, 2)
                    // すでに停止している or 最後まで進んだら無視
                    .Where(i => stop[i] == false && startIndex <= infos[i].now + infos[i].d && infos[i].now + infos[i].d <= endIndex)
                    .Select(j =>
                    {
                        var info = infos[j];
                        var e0 = edges[edgeBaseIndex];
                        var e1 = edges[info.now + info.d];
                        return new { i = j, index = info.now + info.d, ang = Vector2.Angle(e0.Direction, e1.Direction) };
                    })
                    .OrderBy(x => x.ang)
                    .ToList();
                if (es.Count == 0)
                    break;
                foreach (var e in es)
                {
                    if (e.ang > toleranceAngleDegForMidEdge)
                    {
                        stop[e.i] = true;
                        continue;
                    }

                    if (e.i == 0)
                    {
                        ret.Insert(0, e.index);
                    }
                    else
                    {
                        ret.Add(e.index);
                    }
                }
            }

            return ret;
        }


        /// <summary>
        /// verticesを始点終点から見ていき,お互い中心線を使って比較しながら中心の辺を表すインデックス配列を返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="toleranceAngleDegForMidEdge">中心線と同一直線と判断する線分の角度[deg]</param>
        /// <param name="skipAngleDeg">開始線分との角度がこれ以下の間は絶対に中心線にならない</param>
        /// <param name="op"></param>
        /// <returns></returns>
        public static List<int> FindMidEdge(IReadOnlyList<Vector2> vertices, float toleranceAngleDegForMidEdge = 20f, float skipAngleDeg = 20f, DebugFindOppositeOption op = null)
        {
            var edges = GeoGraphEx.GetEdges(vertices, false).Select(v => new LineSegment2D(v.Item1, v.Item2)).ToList();

            // 開始線分が中心線扱いにならないようにskipAngleDegを使ってスキップする
            var (startIndex, endIndex) = (0, edges.Count - 1);
            if (skipAngleDeg > 0f)
            {
                while (startIndex < edges.Count - 1 && Mathf.Abs(Vector2.Angle(edges[startIndex].Direction, edges[startIndex + 1].Direction)) < skipAngleDeg)
                    startIndex++;
                while (endIndex > 0 && Mathf.Abs(Vector2.Angle(edges[endIndex].Direction, edges[endIndex - 1].Direction)) < skipAngleDeg)
                    endIndex--;
            }

            var leftIndex = startIndex;
            var rightIndex = endIndex;
            var fComp = Comparer<float>.Default;

            // left ~ rightの間にエッジが1つしかなくなるまで続ける
            //   -> その残った一つがエッジ
            while (leftIndex < rightIndex - 2)
            {
                var l = edges[leftIndex];
                // 0 ~ edge.Countまでつながっているような線なので逆順の線分の方向を逆にする
                var r = edges[rightIndex].Reversed();
                // 中心線を計算
                var centerRay = GeoGraph2D.LerpRay(l.Ray, r.Ray, 0.5f);
                var dirL = new Vector2(l.Direction.y, -l.Direction.x);
                var dirR = new Vector2(r.Direction.y, -r.Direction.x);
                var points = new[]
                    {
                        new { ray = new Ray2D(l.Start, dirL), isLeft = true },
                        new { ray = new Ray2D(l.End, dirL), isLeft = true },
                        new { ray = new Ray2D(r.Start, dirR), isLeft = false },
                        new { ray = new Ray2D(r.End, dirR), isLeft = false }
                    }
                    .Select(x =>
                    {
                        var hit = LineUtil.LineIntersection(centerRay, x.ray, out var inter, out var t1, out var t2);
                        var other = x.isLeft ? r : l;
                        return new
                        {
                            isHit = hit,
                            inter,
                            tCenterRay = t1,
                            tRay = t2,
                            x.isLeft,
                            x.ray.origin
                        };
                    }).Where(x => x.isHit)
                    .ToList();

                points.Sort((a, b) => fComp.Compare(a.tCenterRay, b.tCenterRay));

                // 重ならない時は無視
                if (points.Any() == false)
                {
                    leftIndex++;
                    continue;
                }
#if UNITY_EDITOR
                var isCross = points.Count == 4 && points[0].isLeft != points[1].isLeft;
                if (isCross)
                {
                    if (op?.showCenterLine ?? false)
                        DebugEx.DrawArrow(points[1].inter, points[2].inter, bodyColor: op.showCenterLineColor);
                }
#endif
                // より遠いのが左の場合右の線分を進める
                if (points.Last().isLeft)
                {
                    rightIndex--;
                }
                else
                {
                    leftIndex++;
                }
            }

            // ここに来る段階でleftIndex == rightIndex - 2のはず
            var edgeBaseIndex = (leftIndex + rightIndex) / 2;
            var ret = FindCollinearRange(edgeBaseIndex, edges, toleranceAngleDegForMidEdge, startIndex, endIndex);
            // edge -> 頂点の配列に戻すために最後のインデックスを足す
            ret.Add(ret.Last() + 1);
            return ret;
        }


        /// <summary>
        /// pointがverticesで構成される多角形の内部にあるかどうかを返す. 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static bool IsInsidePolygon(Vector2 point, IList<Vector2> vertices)
        {
            // Winding Number Algorithmで判定する
            // https://www.nttpc.co.jp/technology/number_algorithm.html
            var wn = 0;
            for (var i = 0; i < vertices.Count; i++)
            {
                var v1 = vertices[i];
                var v2 = vertices[(i + 1) % vertices.Count];

                // 上向きの辺、下向きの辺によって処理が分かれる。
                // 上向きの辺。点Pがy軸方向について、始点と終点の間にある。ただし、終点は含まない。
                if ((v1.y <= point.y) && (v2.y > point.y))
                {
                    // 辺は点pよりも右側にある。ただし、重ならない。
                    // 辺が点pと同じ高さになる位置を特定し、その時のxの値と点pのxの値を比較する。
                    var vt = (point.y - v1.y) / (v2.y - v1.y);
                    if (point.x < (v1.x + (vt * (v2.x - v1.x))))
                    {
                        ++wn;
                    }
                }
                // 下向きの辺。点Pがy軸方向について、始点と終点の間にある。ただし、始点は含まない。
                else if ((v1.y > point.y) && (v2.y <= point.y))
                {
                    // 辺は点pよりも右側にある。ただし、重ならない。
                    // 辺が点pと同じ高さになる位置を特定し、その時のxの値と点pのxの値を比較する。
                    var vt = (point.y - v1.y) / (v2.y - v1.y);
                    if (point.x < (v1.x + (vt * (v2.x - v1.x))))
                    {
                        --wn;
                    }
                }
            }

            return wn != 0;
        }

        /// <summary>
        /// 多角形の面積を計算する
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static float CalcPolygonArea(IReadOnlyList<Vector2> vertices)
        {
            return CalcPolygonArea(vertices, v => v);
        }

        /// <summary>
        /// 多角形の面積を計算する
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static float CalcPolygonArea<T>(IReadOnlyList<T> vertices, Func<T, Vector2> toVec2)
        {

            var area = 0f;
            for (var i = 0; i < vertices.Count; i++)
            {
                var v1 = toVec2(vertices[i]);
                var v2 = toVec2(vertices[(i + 1) % vertices.Count]);
                area += (v1.x * v2.y - v2.x * v1.y);
            }
            return Mathf.Abs(area) / 2f;
        }
    }
}