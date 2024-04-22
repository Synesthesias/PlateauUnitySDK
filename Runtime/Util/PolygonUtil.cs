﻿using PLATEAU.CityGML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace PLATEAU.Util
{
    public static class PolygonUtil
    {
        /// <summary>
        /// 頂点verticesで構成される多角形の辺を返す. isLoop=trueの時は最後の用途と最初の要素を繋ぐ辺も返す
        /// Item1 : 始点, Item2 : 終点
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="isLoop"></param>
        /// <returns></returns>
        private static IEnumerable<Tuple<T, T>> GetEdges<T>(IEnumerable<T> vertices, bool isLoop) where T : struct
        {
            T? first = null;
            T? current = null;
            foreach (var v in vertices)
            {
                if (current == null)
                {
                    first = current = v;
                    continue;
                }
                yield return new Tuple<T, T>(current.Value, v);
                current = v;
            }

            if (isLoop && first.HasValue)
                yield return new Tuple<T, T>(current.Value, first.Value);
        }

        /// <summary>
        /// ポリゴンを構成する頂点配列を渡すと, そのポリゴンが時計回りなのか反時計回りなのかを返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static bool IsClockwise(IEnumerable<Vector2> vertices)
        {
            var total = GetEdges(vertices, true).Sum(item => Vector2Util.Cross(item.Item1, item.Item2));
            return total >= 0;
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

            var cnt = GetEdges(vertices, true).Count(item => Check(item.Item1, item.Item2));
            return (cnt % 2) == 1;
        }

        /// <summary>
        /// verticesで構成された線分の長さを求める
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static float GetLineSegmentLength(IEnumerable<Vector3> vertices)
        {
            return GetEdges(vertices, false).Sum(item => (item.Item2 - item.Item1).magnitude);
        }

        /// <summary>
        /// verticesで表される線分の中央地点を返す
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="midPoint"></param>
        /// <returns></returns>
        public static bool TryGetLineSegmentMidPoint(IList<Vector3> vertices, out Vector3 midPoint)
        {
            var halfLength = GetLineSegmentLength(vertices) * 0.5f;

            var len = 0f;
            for (var i = 0; i < vertices.Count - 1; ++i)
            {
                var p0 = vertices[i];
                var p1 = vertices[i + 1];
                var l = (p1 - p0).magnitude;
                len += l;
                if (len >= halfLength && l > float.Epsilon)
                {
                    var f = (len - halfLength) / l;
                    midPoint = Vector3.Lerp(p0, p1, f);
                    return true;
                }
            }

            midPoint = Vector3.zero;
            return false;
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
            var ret = GetEdges(vertices, isLoop)
                .Select(p =>
                {
                    var success = LineUtil.HalfLineSegmentIntersection(ray, p.Item1, p.Item2, out Vector2 intersection,
                        out float f1,
                        out float f2);
                    return new { success, intersection, f1, f2 };
                })
                .Where(p => p.success)
                .TryFindMin(p => p.f1, out var o);

            intersection = o.intersection;
            t = o.f1;
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
            var ret = GetEdges(vertices, isLoop)
                .Select(p =>
                {
                    var success = LineUtil.SegmentIntersection(st, en, p.Item1, p.Item2, out Vector2 intersection,
                        out float f1,
                        out float f2);
                    return new { success, intersection, f1, f2 };
                })
                .Where(p => p.success)
                .TryFindMin(p => p.f1, out var o);

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
    }
}