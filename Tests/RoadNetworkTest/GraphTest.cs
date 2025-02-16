using NUnit.Framework;
using PLATEAU.RoadNetwork.Structure;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using UnityEngine;

namespace Tests.RoadNetworkTest
{
    public class GraphTest
    {
        private static void CheckLerpPointInLine(int index, Ray2D a, Ray2D b, float p)
        {
            var res = GeoGraph2D.CalcLerpPointInLine(a, b, p, out var v);
            // そもそも解無しの場合は無視(a,bが並行で p=1.0の場合等)
            if (!res)
                return;
            var t = (v - a.origin).magnitude;
            var near = b.GetNearestPoint(v);
            var bLen = (v - near).magnitude;

            // t : bLen = p : (1-p)になるか確認
            var lhs = Mathf.Abs(t * (1 - p));
            var rhs = Mathf.Abs(bLen * p);
            var del = Mathf.Abs(lhs - rhs);
            var label = $"[{index}] {a.ToLogString()}/{b.ToLogString()}[{p}].  |{lhs}| != {rhs}({p} & {bLen})";
            Assert.IsTrue(del < 1e-1f, label);
        }

        [Test()]
        public void RnWayNormalTest()
        {
            var points = new List<Vector3>()
            {
                new Vector3(0, 0, 0)
                , new Vector3(1, 0, 0)
                , new Vector3(1, 0, 1)
            };



            var ls = RnLineString.Create(points);
            var way = new RnWay(ls, false, false);

            var n1 = way.GetEdgeNormal(0);
            var n2 = way.GetEdgeNormal(1);

            void Check(Vector3 pos, bool isOut)
            {
                var res = way.IsOutSide(pos, out var n, out var o);
                Assert.AreEqual(isOut, res, $"{pos}");
            }
            Check(new Vector3(0.5f, 0, 0.5f), true);
        }

#if false
        [Test()]
        public void CalcLerpPointInLineTest()
        {
            var triCount = 1000;
            var posRange = 1000f;
            var pCount = 100;

            float RandPos()
            {
                return Random.Range(-posRange, posRange);
            }
            Ray2D CreateRandomRay()
            {
                var dir = Vector2Ex.Polar2Cart(Random.Range(0, 360));
                return new Ray2D(new Vector2(RandPos(), RandPos()), dir);
            }

            foreach (var i in Enumerable.Range(0, triCount))
            {
                var a = CreateRandomRay();
                var b = CreateRandomRay();
                foreach (var p in Enumerable.Range(0, pCount + 1).Select(x => 1f * x / pCount))
                {
                    GeoGraph2D.CalcLerpPointInLine(a, b, p, out var v);
                    CheckLerpPointInLine(i, a, b, p);
                }
            }

            CheckLerpPointInLine(
                -1
                , new Ray2D(new Vector2(457.00f, 244.00f), new Vector2(-0.39f, -0.92f))
                , new Ray2D(new Vector2(-579f, -137f), new Vector2(-0.45f, -0.89f))
                , 0f
            );
            CheckLerpPointInLine(
                -1
                , new Ray2D(new Vector2(457.00f, 244.00f), new Vector2(-0.39f, -0.92f))
                , new Ray2D(new Vector2(-579f, -137f), new Vector2(-0.45f, -0.89f))
                , 1f
                );
        }
#endif
        [Test()]
        public void GeoGraph2DLerpRayTest()
        {
            var triCount = 1000;
            var posRange = 1000f;
            var pCount = 100;

            float RandPos()
            {
                return Random.Range(-posRange, posRange);
            }
            Ray2D CreateRandomRay()
            {
                var dir = Vector2Ex.Polar2Cart(Random.Range(0, 360));
                return new Ray2D(new Vector2(RandPos(), RandPos()), dir);
            }

            void Check(Ray2D a, Ray2D b, Ray2D ray, float p)
            {
                for (var i = 0; i < 1000; i++)
                {
                    var len = Random.Range(-10000f, 10000f);

                    var pos = ray.origin + ray.direction * len;

                    var posA = a.GetNearestPoint(pos);
                    var posB = b.GetNearestPoint(pos);

                    var lenA = (posA - pos).magnitude;
                    var lenB = (posB - pos).magnitude;

                    // lenA : lenB = p : (1-p)
                    var x = lenA * (1 - p);
                    var y = lenB * p;

                    var p2 = lenA / (lenA + lenB);
                    if (Mathf.Abs(p2 - p) > 1e-1f)
                    {
                        var label = $"[{i}] {p} != {p2}. {a.ToLogString()}/{b.ToLogString()}/{ray.ToLogString()} {x} != {y}";
                        Assert.IsTrue(false, label);
                    }
                }
            }

            foreach (var i in Enumerable.Range(0, triCount))
            {
                var a = CreateRandomRay();
                var b = CreateRandomRay();

                foreach (var p in Enumerable.Range(0, pCount + 1).Select(x => 1f * x / pCount))
                {
                    var ray = GeoGraph2D.LerpRay2(a, b, p);
                    Check(a, b, ray, p);
                }
            }
        }

        /// <summary>
        /// RnLineString.GetAdvancedPointのテスト
        /// </summary>
        [Test()]
        public void LineStringAdvancedPointTest()
        {
            var points = Enumerable.Range(0, 10)
                .Select(i => new RnPoint(Vector3.right * i))
                .ToList();

            var lineString = new RnLineString(points);


            void Test(float offset, bool reverse, Vector3 ans, int ansStartIndex, int ansEndIndex)
            {
                var ret = lineString.GetAdvancedPoint(offset, reverse, out var startIndex, out var endIndex);

                Assert.AreEqual(ans, ret, $"ans {offset}/{reverse}");
                Assert.AreEqual(ansStartIndex, startIndex, $"startIndex. {offset}/{reverse}");
                Assert.AreEqual(ansEndIndex, endIndex, $"endIndex. {offset}/{reverse}");
            }

            for (var i = 0; i < points.Count - 1; ++i)
            {
                var l = i + 0.5f;
                Test(l, false, Vector3.right * l, i, i + 1);
                Test(l, true, Vector3.right * (points.Count - 1 - l), points.Count - 1 - i, points.Count - 1 - i - 1);
            }

            Test(points.Count - 1, false, points[^1].Vertex, points.Count - 2, points.Count - 1);
            Test(points.Count - 1, true, points[0].Vertex, 1, 0);

            Test(points.Count, false, points[^1].Vertex, points.Count - 1, points.Count - 1);
            Test(points.Count, true, points[0].Vertex, 0, 0);
        }

        /// <summary>
        /// RnWay.GetAdvancedPointのテスト
        /// </summary>
        [Test()]
        public void WayAdvancedPointTest()
        {
            var points = Enumerable.Range(0, 10)
                .Select(i => new RnPoint(Vector3.right * i))
                .ToList();

            var lineString = new RnLineString(points);

            void Test(RnWay way, float offset, bool reverse, float ans, int ansStartIndex = -1, int ansEndIndex = -1)
            {
                if (ansStartIndex < 0)
                {
                    ansStartIndex = Mathf.Min(points.Count - 1, Mathf.FloorToInt(offset));
                    ansEndIndex = Mathf.Min(points.Count - 1, Mathf.CeilToInt(offset));
                    if (reverse)
                    {
                        ansStartIndex = points.Count - 1 - ansStartIndex;
                        ansEndIndex = points.Count - 1 - ansEndIndex;
                    }
                }

                var ret = way.GetAdvancedPoint(offset, reverse, out var startIndex, out var endIndex);
                Assert.AreEqual(Vector3.right * ans, ret, $"ans {offset}/{reverse}/{way.IsReversed}");
                Assert.AreEqual(ansStartIndex, startIndex, $"startIndex. {offset}/{reverse}/{way.IsReversed}");
                Assert.AreEqual(ansEndIndex, endIndex, $"endIndex. {offset}/{reverse}/{way.IsReversed}");
            }


            var start = 0f;
            var end = points.Count - 1f;

            {
                var way = new RnWay(lineString, true, false);
                Test(way, 0.5f, false, end - 0.5f);
            }
            foreach (var rev in new[] { false, true })
            {
                var way = new RnWay(lineString, rev, false);

                for (var i = 0; i < points.Count - 1; ++i)
                {
                    var l = i + 0.5f;
                    var ans = l;
                    if (rev)
                        ans = points.Count - 1 - l;

                    Test(way, l, false, ans);
                    Test(way, l, true, (points.Count - 1 - ans));
                }

                {
                    var revAns = start;
                    var ans = end;
                    if (rev)
                        (revAns, ans) = (ans, revAns);
                    Test(way, points.Count - 1, false, ans, points.Count - 2, points.Count - 1);
                    Test(way, points.Count - 1, true, revAns, 1, 0);

                    Test(way, points.Count, false, ans, points.Count - 1, points.Count - 1);
                    Test(way, points.Count, true, revAns, 0, 0);
                }
            }
        }

        /// <summary>
        /// RnWay.Splitのテスト
        /// </summary>
        [Test()]
        public void WaySplitTest()
        {
            // 0 ~ 10の1刻みの点列
            var points = Enumerable.Range(0, 11)
                .Select(i => new RnPoint(Vector3.right * i))
                .ToList();

            var lineString = new RnLineString(points);
            foreach (var rev in new[] { false, true })
            {
                var way = new RnWay(lineString, rev, false);
                var ways = way.Split(2, false, i => i == 0 ? 0.1f : 0.9f);
                var frontLen = ways[0].CalcLength();
                var backLen = ways[1].CalcLength();
                Assert.AreEqual(1f, frontLen);
                Assert.AreEqual(9f, backLen);
            }
        }
    }
}