using PLATEAU.RoadNetwork.Util;
using UnityEngine;

namespace PLATEAU.Util.GeoGraph
{
    /// <summary>
    /// 線分を表すクラス
    /// </summary>
    public struct LineSegment2D
    {
        // --------------
        // start:フィールド
        // --------------
        // 始点
        private Vector2 start;
        // 終戦
        private Vector2 end;
        // 方向
        private Vector2 direction;
        // 長さ(キャッシュ)
        private float magnitude;
        // --------------
        // end:フィールド
        // --------------

        /// <summary>
        /// 始点
        /// </summary>
        public Vector2 Start
        {
            get => start;
            set
            {
                start = value;
                magnitude = (end - start).magnitude;
                direction = (end - start) / magnitude;
            }
        }

        /// <summary>
        /// 終点
        /// </summary>
        public Vector2 End
        {
            get => end;
            set
            {
                end = value;
                magnitude = (end - start).magnitude;
                direction = (end - start) / magnitude;
            }
        }

        /// <summary>
        /// 方向
        /// </summary>
        public Vector2 Direction => direction;

        /// <summary>
        /// 長さ
        /// </summary>
        public float Magnitude => magnitude;

        /// <summary>
        /// Ray2Dにして返す
        /// </summary>
        public Ray2D Ray => new(start, direction);

        public LineSegment2D(Vector2 start, Vector2 end)
        {
            this.start = start;
            this.end = end;
            magnitude = (end - start).magnitude;
            direction = (end - start) / magnitude;
        }

    }

    public static class LineSegment2DEx
    {
        /// <summary>
        /// 引数の線分(v0,v1)との交点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="v0"></param>
        /// <param name="v1"></param>
        /// <param name="intersection"></param>
        /// <param name="selfT">intersection = Mathf.Lerp(self.Start, self.End, selfT)となる値  </param>
        /// <param name="otherT">intersection = Mathf.Lerp(v0, v1, otherT)となる値 </param>
        /// <returns></returns>
        public static bool TrySegmentIntersection(this LineSegment2D self, Vector2 v0, Vector2 v1, out Vector2 intersection, out float selfT, out float otherT)
        {
            return LineUtil.SegmentIntersection(self.Start, self.End, v0, v1, out intersection, out selfT, out otherT);
        }
        /// <summary>
        /// 引数の線分otherとの交点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="intersection">交点</param>
        /// <param name="selfT"> </param>
        /// <param name="otherT"></param>
        public static bool TrySegmentIntersection(this LineSegment2D self, LineSegment2D other,
            out Vector2 intersection, out float selfT, out float otherT)
        {
            return self.TrySegmentIntersection(other.Start, other.End, out intersection, out selfT, out otherT);
        }

        /// <summary>
        /// 引数の線分otherとの交点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="intersection">交点</param>
        public static bool TrySegmentIntersection(this LineSegment2D self, LineSegment2D other,
            out Vector2 intersection)
        {
            return self.TrySegmentIntersection(other.Start, other.End, out intersection, out var t1, out var t2);
        }

        /// <summary>
        /// 引数の線分otherとの交点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        public static bool TrySegmentIntersection(this LineSegment2D self, LineSegment2D other)
        {
            return self.TrySegmentIntersection(other.Start, other.End, out var intersection, out var t1, out var t2);
        }

        /// <summary>
        /// 引数の半直線(origin,dir)との交点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="origin"></param>
        /// <param name="dir"></param>
        /// <param name="intersection"></param>
        /// <param name="selfT">intersection = self.Lerp(t1)となるt1が入る</param>
        /// <param name="halfLineOffset">intersection = origin + dir * t2となるt2が入る</param>
        /// <returns></returns>
        public static bool TryHalfLineIntersection(this LineSegment2D self, Vector2 origin, Vector2 dir, out Vector2 intersection
            , out float halfLineOffset, out float selfT)
        {
            return LineUtil.HalfLineSegmentIntersection(new Ray2D(origin, dir), self.Start, self.End, out intersection, out halfLineOffset,
                out selfT);
        }

        /// <summary>
        /// 直線(origin,dir)との交点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="origin"></param>
        /// <param name="dir"></param>
        /// <param name="intersection"></param>
        /// <param name="lineLength">intersection = line.origin + line.direction * lineLengthとなる値</param>
        /// <param name="segmentT">intersection = Vector2.LerpUnClamped(p1, p2, segmentT)となる値</param>
        /// <returns></returns>
        public static bool TryLineIntersection(this LineSegment2D self, Vector2 origin, Vector2 dir, out Vector2 intersection, out float lineLength,
            out float segmentT)
        {
            return LineUtil.LineSegmentIntersection(new Ray2D(origin, dir), self.Start, self.End, out intersection, out lineLength, out segmentT);
        }

        /// <summary>
        /// 点vから線分に対して最も近い点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static Vector2 GetNearestPoint(this LineSegment2D self, Vector2 v)
        {
            return self.GetNearestPoint(v, out var _);
        }

        /// <summary>
        /// 点vから線分に対して最も近い点を返す. distanceFromSegmentStartにはStartからの距離(符号あり)が入る
        /// </summary>
        /// <param name="self"></param>
        /// <param name="v"></param>
        /// <param name="distanceFromSegmentStart"></param>
        /// <returns></returns>
        public static Vector2 GetNearestPoint(this LineSegment2D self, Vector2 v, out float distanceFromSegmentStart)
        {
            distanceFromSegmentStart = Vector2.Dot(self.Direction, v - self.Start);
            distanceFromSegmentStart = Mathf.Clamp(distanceFromSegmentStart, 0f, self.Magnitude);
            return self.Start + distanceFromSegmentStart * self.Direction;
        }

        /// <summary>
        /// 点vから線分に対して最も近い点を返す. distanceFromSegmentStartにはStartからの距離(符号あり)が入る.
        /// tはClampする前の距離をself.Magnitudeで割ったもの t > 1 or t < 0の場合は線分の外側
        /// </summary>
        /// <param name="self"></param>
        /// <param name="v"></param>
        /// <param name="distanceFromSegmentStart"></param>
        /// <returns></returns>
        public static Vector2 GetNearestPoint(this LineSegment2D self, Vector2 v, out float distanceFromSegmentStart, out float t)
        {
            distanceFromSegmentStart = Vector2.Dot(self.Direction, v - self.Start);
            t = distanceFromSegmentStart / self.Magnitude;
            distanceFromSegmentStart = Mathf.Clamp(distanceFromSegmentStart, 0f, self.Magnitude);
            return self.Start + distanceFromSegmentStart * self.Direction;
        }

        /// <summary>
        /// self.Start + distance * self.Direction
        /// </summary>
        /// <param name="self"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static Vector2 GetPoint(this LineSegment2D self, float distance)
        {
            return self.Start + self.Direction * distance;
        }

        /// <summary>
        /// Start/End反転させたものを返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static LineSegment2D Reversed(this LineSegment2D self)
        {
            return new LineSegment2D(self.End, self.Start);
        }


        /// <summary>
        /// selfに対して, vが右側にあるか左側にあるかを返す(z軸上から見たとき)
        /// 右側 : 1, 左側 : -1, 線上 : 0
        /// </summary>
        /// <param name="self"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static int Sign(this LineSegment2D self, RnExplicit<Vector2> v)
        {
            var ret = Vector2Ex.Cross(self.Direction, v.V - self.Start);
            return ret.CompareTo(0f);
        }

        /// <summary>
        /// 線分間の距離を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static float GetDistance(this LineSegment2D self, LineSegment2D other)
        {
            // 交差る場合は0
            if (self.TrySegmentIntersection(other, out var inter, out var t1, out var t2))
            {
                return 0f;
            }

            var d1 = self.GetNearestPoint(other.Start) - other.Start;
            var d2 = self.GetNearestPoint(other.End) - other.End;
            var d3 = other.GetNearestPoint(self.Start) - self.Start;
            var d4 = other.GetNearestPoint(self.End) - self.End;
            return Mathf.Min(d1.magnitude, d2.magnitude, d3.magnitude, d4.magnitude);
        }
    }
}