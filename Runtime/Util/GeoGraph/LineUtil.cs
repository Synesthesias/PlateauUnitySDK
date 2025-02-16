using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLATEAU.Util.GeoGraph
{
    /// <summary>
    /// 直線に関する便利関数
    /// </summary>
    public static class LineUtil
    {
        private const float Epsilon = 1e-3f;

        public struct Line
        {
            public Line(in Vector3 p0, in Vector3 p1)
            {
                this.p0 = p0;
                this.p1 = p1;
            }

            private Vector3 p0;
            private Vector3 p1;

            public Vector3 P0 { get => p0; }
            public Vector3 P1 { get => p1; }
            public float SqrMag { get => (p0 - p1).sqrMagnitude; }
            public float Mag { get => (p0 - p1).magnitude; }

            public Vector3 DirectionA2B { get => (p1 - p0).normalized; }
            public Vector3 DirectionB2A { get => (p0 - p1).normalized; }

            public Vector3 VecA2B { get => (p1 - p0); }
            public Vector3 VecB2A { get => (p0 - p1); }

        }

        /// <summary>
        /// 2直線の距離を求める
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <param name="isParallel"></param>
        /// <returns></returns>
        public static float DistanceBetweenLines(in Line line1, in Line line2, out bool isParallel)
        {
            Vector3 cross = Vector3.Cross(line1.DirectionA2B, line2.DirectionA2B);
            float denominator = cross.magnitude;

            // 直線が平行な場合
            if (denominator == 0)
            {
                isParallel = true;
                Vector3 diff = line1.P0 - line2.P0;
                return Vector3.Cross(diff, line1.DirectionA2B).magnitude / line1.DirectionA2B.magnitude;
            }

            isParallel = false;
            Vector3 diffPoints = line2.P0 - line1.P0;
            return Mathf.Abs(Vector3.Dot(diffPoints, cross)) / denominator;
        }

        /// <summary>
        /// 交差するline0とline1の交点を求める
        /// 注意点としてline0,line1は線分ではなく永遠に延びる直線として扱う
        /// 交差しない場合は動作未定義
        /// </summary>
        /// <param name="line0"></param>
        /// <param name="line1"></param>
        /// <param name="intersectionPoint"></param>
        /// <returns></returns>
        public static bool Intersect(in Line line0, in Line line1, out Vector3 intersectionPoint)
        {
            Vector3 p1 = line0.P0;
            Vector3 d1 = line0.DirectionA2B;
            Vector3 p2 = line1.P0;
            Vector3 d2 = line1.DirectionA2B;

            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float b = Vector3.Dot(d1, d2);
            float c = Vector3.Dot(d1, r);

            float denominator = a * e - b * b;

            // 直線が平行な場合
            if (Mathf.Abs(denominator) < Mathf.Epsilon)
            {
                intersectionPoint = Vector3.zero;
                return false;
            }

            float s = (b * f - c * e) / denominator;

            // 交差点を計算
            intersectionPoint = p1 + s * d1;

            return true;
        }

        /// <summary>
        /// line上にpointが存在するかどうかを判定する
        /// ただし pointはlineの延長線上にあることを前提とする
        /// </summary>
        /// <param name="line"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool ContainsPoint(in Line line, in Vector3 point)
        {
            // 線分の方向ベクトル
            Vector3 direction = line.P1 - line.P0;

            // 点から線分の始点と終点へのベクトル
            Vector3 toPointFromStart = point - line.P0;
            Vector3 toPointFromEnd = point - line.P1;

            // 内積を計算
            float dotStart = Vector3.Dot(toPointFromStart, direction);
            float dotEnd = Vector3.Dot(toPointFromEnd, -direction);

            // 内積が両方とも正であれば点は線分上にある
            return dotStart >= 0 && dotEnd >= 0;
        }

        /// <summary>
        /// ライン上にポイントが存在するかどうかを判定する
        /// </summary>
        /// <param name="line"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        [System.Obsolete("動作未検証の関数")]
        public static bool IsPointOnLineSegment(Line line, Vector3 p)
        {
            // ベクトルを計算
            Vector3 AB = line.P1 - line.P0;
            Vector3 AP = p - line.P0;

            // スカラー値 t を計算
            float t = Vector3.Dot(AP, AB) / Vector3.Dot(AB, AB);

            // t が 0 から 1 の範囲内にあるか確認
            return t >= 0 && t <= 1;
        }

        /// <summary>
        /// 交差しないline0とline1の最短距離を求める
        /// ただし、line0,line1は線分ではなく永遠に延びる直線として扱う
        /// 交差する場合は動作未定義
        /// </summary>
        /// <param name="line0"></param>
        /// <param name="line1"></param>
        /// <param name="closestPoint1"></param>
        /// <param name="closestPoint2"></param>
        //public static void ClosestPoints(
        //    Line line0, Line line1, out Vector3 closestPoint1, out Vector3 closestPoint2)
        //{
        //    Vector3 p1 = line0.P0;
        //    Vector3 d1 = line0.DirectionA2B;
        //    Vector3 p2 = line1.P0;
        //    Vector3 d2 = line1.DirectionA2B;

        //    Vector3 r = p1 - p2;
        //    float a = Vector3.Dot(d1, d1);
        //    float e = Vector3.Dot(d2, d2);
        //    float f = Vector3.Dot(d2, r);
        //float d = Vector3.Dot(d1, r);

        //    float s = (Vector3.Dot(d1, r) * e - f * Vector3.Dot(d1, d2)) / (a * e - Vector3.Dot(d1, d2) * Vector3.Dot(d1, d2));
        //    float t = (f + s * Vector3.Dot(d1, d2)) / e;

        //    closestPoint1 = p1 + s * d1;
        //    closestPoint2 = p2 + t * d2;
        //}

        //public static void ClosestPoints(
        //    Line line0, Line line1, out Vector3 closestPoint1, out Vector3 closestPoint2)
        //{
        //    Vector3 p1 = line0.P0;
        //    Vector3 d1 = line0.DirectionB2A;
        //    Vector3 p2 = line1.P0;
        //    Vector3 d2 = line1.DirectionB2A;

        //    Vector3 r = p1 - p2;
        //    float a = Vector3.Dot(d1, d1);
        //    float b = Vector3.Dot(d1, d2);
        //    float c = Vector3.Dot(d2, d2);
        //    float d = Vector3.Dot(d1, r);
        //    float e = Vector3.Dot(d2, r);
        //    float denominator = a * c - b * b;

        //    float s = (b * e - c * d) / denominator;
        //    float t = (a * e - b * d) / denominator;

        //    closestPoint1 = p1 + s * d1;
        //    closestPoint2 = p2 + t * d2;

        //}


        public static void ClosestPoints(Line line0, Line line1, out Vector3 closestPoint1,
     out Vector3 closestPoint2)
        {
            Vector3 p1 = line0.P0;
            Vector3 d1 = line0.DirectionB2A;
            Vector3 p2 = line1.P0;
            Vector3 d2 = line1.DirectionB2A;

            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float b = Vector3.Dot(d1, d2);
            float c = Vector3.Dot(d2, d2);
            float d = Vector3.Dot(d1, r);
            float e = Vector3.Dot(d2, r);
            float denominator = a * c - b * b;

            if (denominator != 0)
            {
                float s = (b * e - c * d) / denominator;
                float t = (a * e - b * d) / denominator;

                closestPoint1 = p1 + s * d1;
                closestPoint2 = p2 + t * d2;
            }
            else
            {
                // 直線が平行な場合
                closestPoint1 = p1;
                closestPoint2 = p2 + Vector3.Project(r, d2);
            }
        }
        //public static void FindClosestPoints(Line line0, Line line1, out Vector3 closestPointLine1, out Vector3 closestPointLine2)
        //{
        //    Vector3 p1 = line0.P0;
        //    Vector3 d1 = line0.DirectionB2A; 
        //    Vector3 p2; 
        //    Vector3 d2;

        //    Vector3 r = p1 - p2;
        //    float a = Vector3.Dot(d1, d1);
        //    float b = Vector3.Dot(d1, d2);
        //    float c = Vector3.Dot(d2, d2);
        //    float d = Vector3.Dot(d1, r);
        //    float e = Vector3.Dot(d2, r);
        //    float denominator = a * c - b * b;

        //    if (denominator != 0)
        //    {
        //        float s = (b * e - c * d) / denominator;
        //        float t = (a * e - b * d) / denominator;

        //        closestPointLine1 = p1 + s * d1;
        //        closestPointLine2 = p2 + t * d2;
        //    }
        //    else
        //    {
        //        // 直線が平行な場合
        //        closestPointLine1 = p1;
        //        closestPointLine2 = p2 + Vector3.Project(r, d2);
        //    }
        //}

        /// <summary>
        /// 線分lineとrayの衝突判定を行う
        /// radiusで判定に余裕を持たせる
        /// また、closestPointは衝突地点ではなく線分上の最も近い点なので注意
        /// </summary>
        public static float CheckHit(Line line, float radius, in Ray ray,
            out Vector3 closestPoint, out Vector3 closestPoint2)
        {
            var rayLine = new Line(ray.origin, ray.origin + ray.direction * 1000/*DEBUG用*/);
            var dis = DistanceBetweenLines(line, rayLine, out var isParallel);

            // 平行時は通常の方法で計算できないので判定を取らないようにする
            if (isParallel)
            {
                return NoHit(out closestPoint, out closestPoint2);
            }

            // 距離が離れている場合はヒットしていない
            if (radius < dis)
            {
                return NoHit(out closestPoint, out closestPoint2);
            }


            // 最も近い点の算出とそれが線分上にあるか

            // 線分上に存在するか
            bool isPointOnline;


            // 交差している
            if (dis <= Mathf.Epsilon)
            {
                Intersect(line, rayLine, out closestPoint);
                isPointOnline = LineUtil.ContainsPoint(line, closestPoint);
                closestPoint2 = closestPoint;
            }
            else// 交差しない場合
            {
                LineUtil.ClosestPoints(line, rayLine, out closestPoint, out closestPoint2);
                isPointOnline = LineUtil.ContainsPoint(line, closestPoint);
            }

            // 交差点が線分上にない場合はヒットしていない
            if (isPointOnline == false)
            {
                return NoHit(out closestPoint, out closestPoint2);
            }

            //Debug.DrawLine(line.P0, line.P1, Color.magenta);
            //Debug.DrawLine(rayLine.P0, rayLine.P1, Color.green, 2.0F);
            //Debug.DrawLine(closestPoint, closestPoint2, Color.magenta, 2.0F);

            return dis;

            static float NoHit(out Vector3 closestPoint, out Vector3 closestPoint2)
            {
                closestPoint = Vector3.zero;
                closestPoint2 = Vector3.zero;
                return float.MinValue;
            }

        }

        /// <summary>
        /// 最近棒を求める
        /// 直線で計算するので注意
        /// </summary>
        /// <param name="line"></param>
        /// <param name="ray"></param>
        /// <param name="closestPoint"></param>
        /// <param name="closestPoint2"></param>
        /// <returns></returns>
        public static float FindClosestPoint(Line line, in Ray ray,
    out Vector3 closestPoint, out Vector3 closestPoint2)
        {
            var rayLine = new Line(ray.origin, ray.origin + ray.direction * 1000/*DEBUG用*/);
            var dis = DistanceBetweenLines(line, rayLine, out var isParallel);

            // 平行時は通常の方法で計算できないので判定を取らないようにする
            if (isParallel)
            {
                return NoHit(out closestPoint, out closestPoint2);
            }

            // 最も近い点の算出とそれが線分上にあるか


            // 交差している
            if (dis <= Mathf.Epsilon)
            {
                Intersect(line, rayLine, out closestPoint);
                closestPoint2 = closestPoint;
            }
            else// 交差しない場合
            {
                LineUtil.ClosestPoints(line, rayLine, out closestPoint, out closestPoint2);
            }

            return dis;

            static float NoHit(out Vector3 closestPoint, out Vector3 closestPoint2)
            {
                closestPoint = Vector3.zero;
                closestPoint2 = Vector3.zero;
                return float.MinValue;
            }

        }

        /// <summary>
        /// 線分lineとrayの距離を測る
        /// ただし、radius以上離れていたら無効値を返す
        /// </summary>
        /// <param name="line"></param>
        /// <param name="radius"></param>
        /// <param name="ray"></param>
        /// <returns></returns>
        public static float CheckDistance(Line line, float radius, in Ray ray)
        {
            var rayLine = new Line(ray.origin, ray.origin + ray.direction);
            var dis = DistanceBetweenLines(line, rayLine, out var isParallel);

            // 平行時は通常の方法で計算できないので判定を取らないようにする
            if (isParallel)
            {
                return NoHit();
            }

            // 距離が離れている場合はヒットしていない
            if (radius < dis)
            {
                return NoHit();
            }

            return dis;

            static float NoHit()
            {
                return float.MinValue;
            }

        }

        /// <summary>
        /// a,bを通る直線,c,dを通る直線の交点を求める
        /// 平行な場合はfalse
        /// 交わる場合 intersectionに交点が入る
        /// </summary>
        /// <param name="a">直線1の始点</param>
        /// <param name="b">直線1の終点</param>
        /// <param name="c">直線2の始点</param>
        /// <param name="d">直線2の終点</param>
        /// <param name="intersection">交点が入る</param>
        /// <param name="abT">intersection = Vector2.LerpUnclamped(a, b, abT)となるabTが入る</param>
        /// <param name="cdT">intersection = Vector2.LerpUnclamped(c, d, cdT)となるcdTが入る</param>
        /// <returns></returns>
        public static bool LineIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 intersection, out float abT, out float cdT)
        {
            // https://qiita.com/zu_rin/items/09876d2c7ec12974bc0f
            abT = cdT = 0f;
            intersection = Vector2.zero;

            var deno = Vector2Ex.Cross(b - a, d - c);
            if (Mathf.Abs(deno) < Epsilon)
                return false;

            abT = Vector2Ex.Cross(c - a, d - c) / deno;
            cdT = Vector2Ex.Cross(b - a, a - c) / deno;
            intersection = Vector2.LerpUnclamped(a, b, abT);
            return true;
        }

        /// <summary>
        /// 直線rayAと直線rayBの交点を求める
        /// </summary>
        /// <param name="rayA"></param>
        /// <param name="rayB"></param>
        /// <param name="intersection"></param>
        /// <param name="rayAOffset">intersection = rayA.origin + ray.direction * rayAOffsetとなる値</param>
        /// <param name="rayBOffset">intersection = rayB.origin + ray.direction * rayBOffsetとなる値</param>
        /// <returns></returns>
        public static bool LineIntersection(Ray2D rayA, Ray2D rayB, out Vector2 intersection, out float rayAOffset,
            out float rayBOffset)
        {
            return LineIntersection(rayA.origin, rayA.origin + rayA.direction, rayB.origin, rayB.origin + rayB.direction,
                out intersection, out rayAOffset, out rayBOffset);
        }

        /// <summary>
        /// 半直線halfLineと線分(p1, p2)の交点を返す.
        /// 交わらない場合はfalseが返る
        /// </summary>
        /// <param name="halfLine"></param>
        /// <param name="p1">線分を構成する点1</param>
        /// <param name="p2">線分を構成する点2</param>
        /// <param name="intersection">交点</param>
        /// <param name="halfLineOffset">intersection = halfLine.origin + halfLine.direction * halfLineOffsetとなる値</param>
        /// <param name="t2">intersection = Vector2.Lerp(p1, p2, t2)となる値</param>
        /// <returns></returns>
        public static bool HalfLineSegmentIntersection(Ray2D halfLine, Vector2 p1, Vector2 p2, out Vector2 intersection, out float halfLineOffset, out float t2)
        {
            var ret = LineIntersection(halfLine.origin, halfLine.origin + halfLine.direction, p1, p2, out intersection, out halfLineOffset,
                out t2);
            // halfLineは半直線なので後ろになければOK
            // p1,p2は線分なので0~1の範囲内ならOK
            return ret && halfLineOffset >= 0f && t2 is >= 0f and <= 1f;
        }

        /// <summary>
        /// 直線lineと線分(p1,p2)の交点を返す
        /// 交わらない場合はfalseが返る
        /// </summary>
        /// <param name="line"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="intersection"></param>
        /// <param name="lineOffset">intersection = line.origin + line.direction * lineOffsetとなる値</param>
        /// <param name="segmentT">intersection = Vector2.LerpUnClamped(p1, p2, segmentT)となる値</param>
        /// <returns></returns>
        public static bool LineSegmentIntersection(Ray2D line, Vector2 p1, Vector2 p2, out Vector2 intersection,
            out float lineOffset, out float segmentT)
        {
            var ret = LineIntersection(line.origin, line.origin + line.direction, p1, p2, out intersection, out lineOffset,
                out segmentT);
            // p1,p2は線分なので0~1の範囲内ならOK
            return ret && segmentT is >= 0f and <= 1f;

        }

        /// <summary>
        /// 線分h(s1St, s1En)と線分(s2St, s2En)の交点を返す.
        /// 交わらない場合はfalseが返る
        /// </summary>
        /// <param name="s1St">線分1を構成する点1</param>
        /// <param name="s1En">線分1を構成する点2</param>
        /// <param name="s2St">線分2を構成する点1</param>
        /// <param name="s2En">線分2を構成する点2</param>
        /// <param name="intersection"></param>
        /// <param name="t1">intersection = Vector2.Lerp(s1St, s1En, t1)となるt1が入る</param>
        /// <param name="t2">intersection = Vector2.Lerp(s2St, s2En, t2)となるt2が入る</param>
        /// <returns></returns>
        public static bool SegmentIntersection(Vector2 s1St, Vector2 s1En, Vector2 s2St, Vector2 s2En,
            out Vector2 intersection, out float t1, out float t2)
        {
            var ret = LineIntersection(s1St, s1En, s2St, s2En, out intersection, out t1,
                out t2);
            // halfLineは半直線なので後ろになければOK
            // p1,p2は線分なので0~1の範囲内ならOK
            return ret && t1 is >= 0f and <= 1f && t2 is >= 0f and <= 1f;
        }


        /// <summary>
        /// verticesで構成された線分の長さを求める
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static float GetLineSegmentLength(IEnumerable<Vector3> vertices)
        {
            return GeoGraphEx.GetEdges(vertices, false).Sum(item => (item.Item2 - item.Item1).magnitude);
        }

        /// <summary>
        /// verticesを頂点とした連続した線分に対して, 
        /// 前半と後半の長さがp : (1-p)でとなるをmidPointに入れて返す.
        /// 戻り値は midPointを含む線分のインデックスがfloatで返る. 空頂点だと-1が返る
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="p"></param>
        /// <param name="midPoint"></param>
        /// <returns></returns>
        public static float GetLineSegmentLerpPoint(IReadOnlyList<Vector3> vertices, float p, out Vector3 midPoint)
        {
            // 0 ~ 1の間でClampする
            p = Mathf.Clamp(p, 0, 1);

            var length = GetLineSegmentLength(vertices) * p;
            var len = 0f;
            midPoint = Vector3.zero;
            if (vertices.Count == 0)
                return -1;

            midPoint = vertices[0];
            for (var i = 0; i < vertices.Count - 1; ++i)
            {
                var p0 = vertices[i];
                var p1 = vertices[i + 1];
                var l = (p1 - p0).magnitude;
                len += l;
                if (len >= length && l > float.Epsilon)
                {
                    var f = 1f - ((len - length) / l);
                    midPoint = Vector3.Lerp(p0, p1, f);
                    return i + f;
                }
            }

            return -1;
        }

        /// <summary>
        /// verticesで表される線分の中央地点を返す.
        /// 戻り値はvertices[i] ~ vertices[i+1]に中央地点があるときのi
        /// verticesが空の時は-1が返る
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="midPoint"></param>
        /// <returns></returns>
        public static float GetLineSegmentMidPoint(IReadOnlyList<Vector3> vertices, out Vector3 midPoint)
        {
            return GetLineSegmentLerpPoint(vertices, 0.5f, out midPoint);
        }

        /// <summary>
        /// verticesで表される線分をsplitNumで等分する
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public static List<List<Vector3>> SplitLineSegments(IReadOnlyList<Vector3> vertices, int num)
        {
            if (vertices.Count == 0)
                return new List<List<Vector3>>();
            var ret = new List<List<Vector3>>();
            var length = GetLineSegmentLength(vertices) / num;
            var len = 0f;
            List<Vector3> subVertices = new List<Vector3> { vertices[0] };
            for (var i = 1; i < vertices.Count; ++i)
            {
                var p0 = subVertices.Last();
                var p1 = vertices[i];
                var l = (p1 - p0).magnitude;
                len += l;
                // lenがlengthを超えたら分割線分を追加
                while (len >= length && l > Epsilon)
                {
                    var f = (len - length) / l;
                    var end = Vector3.Lerp(p0, p1, f);
                    if (f >= float.Epsilon)
                    {
                        subVertices.Add(end);
                    }

                    ret.Add(subVertices);
                    len -= length;
                    p1 = end;
                }

                subVertices.Add(p1);
            }

            // 最後の要素は無条件で返す
            if (subVertices.Any())
            {
                subVertices.Add(vertices.Last());
                ret.Add(subVertices);
            }

            return ret;
        }

        /// <summary>
        /// p0,p1を通る直線に対して,aから最も近い直線状のポイントを返す
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public static Vector3 GetNearestPointWithRay(Vector3 p0, Vector3 p1, Vector3 a)
        {
            var d = (p1 - p0).normalized;
            // (a.x - (p0.x + d.x*t), a.y - (p0.y + d.y*t))・(d.x, d.y) = 0
            //   d.x*a.x - (p0.x * d.x + d.x^2*t)
            // + d.y*a.y - (p0.y * d.y + d.y^2*t)
            // = 0

            var t = Vector3.Dot(d, a) - Vector3.Dot(p0, d);
            return p0 + t * d;
        }

        /// <summary>
        /// pからselfへの最も近い点を返す. tはreturn = self.origin + self.direction * tとなるt
        /// </summary>
        /// <param name="self"></param>
        /// <param name="p"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Vector2 GetNearestPoint(this Ray2D self, Vector2 p, out float t)
        {
            var d = self.direction;
            t = Vector2.Dot(self.direction, p - self.origin);
            return self.origin + t * d;
        }

        /// <summary>
        /// pからselfへの最も近い点を返す.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Vector2 GetNearestPoint(this Ray2D self, Vector2 p)
        {
            return self.GetNearestPoint(p, out var _);
        }

        public static bool IsMouseDown()
        {
            var evt = Event.current;
            var mouseDown = evt.type == EventType.MouseDown && evt.button == 0 && evt.alt == false;
            return mouseDown;
        }
    }
}