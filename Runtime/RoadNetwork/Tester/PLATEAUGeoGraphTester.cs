using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.CityObject;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using static PLATEAU.RoadNetwork.Tester.PLATEAURnTesterLineString;

namespace PLATEAU.RoadNetwork.Tester
{
    public class PLATEAUGeoGraphTester : MonoBehaviour
    {

        [Serializable]
        public class SerializableRay2D
        {
            [SerializeField] public Vector2 origin;
            [SerializeField] public Vector2 direction;
            public Ray2D Ray => new Ray2D(origin, direction);

            public SerializableRay2D(Ray2D ray)
            {
                origin = ray.origin;
                direction = ray.direction;
            }

            public SerializableRay2D(Vector2 origin, Vector2 direction)
            {
                this.origin = origin;
                this.direction = direction;
            }

            public SerializableRay2D()
            {
                origin = Vector2.zero;
                direction = Vector2.zero;
            }

            public static implicit operator Ray2D(SerializableRay2D serializableRay2D) => serializableRay2D.Ray;
        }
        [Serializable]
        public class LerpLineTestParam
        {
            public bool enable = false;
            public PLATEAUCityObjectGroup target;
            public int indexA = 0;
            public int indexB = 1;
            public float p = 0.5f;
        }

        public LerpLineTestParam lerpLineTest = new LerpLineTestParam();

        [Serializable]
        public class LerpLineSegmentsTestParam
        {
            public bool enable = false;
            public bool showWay = true;
            public int indexA = 0;
            public int indexB = 1;
            public float p = 0.5f;

            public GeoGraph2D.DebugOption op = new GeoGraph2D.DebugOption();
        }
        public LerpLineSegmentsTestParam lerpSegmentsTest = new LerpLineSegmentsTestParam();

        [Serializable]
        public class ParabolaTestParam
        {
            public bool enable = false;
            public float b = 1f;
            public float p = 0.5f;
            public Ray2D ray => new Ray2D(rayOrig, rayDirection);
            public Vector2 rayOrig;
            public Vector2 rayDirection;
            public Vector2 pos;
            public float minX = 0f;
            public float maxX = 10f;
            public float deltaX = 0.1f;
        }
        public ParabolaTestParam parabolaTest = new ParabolaTestParam();

        [Serializable]
        public class ConvexTestParam
        {
            public bool enable = false;
            public List<PLATEAUCityObjectGroup> targets = new List<PLATEAUCityObjectGroup>();
        }
        public ConvexTestParam convexTest = new ConvexTestParam();

        [Serializable]
        public class UnionPolygonTestParam
        {

            public bool enable = false;
            public PLATEAUCityObjectGroup target;
        }
        public UnionPolygonTestParam unionPolygonTest = new UnionPolygonTestParam();


        [Header("Copy PLATEAUCityObjectGroup")]
        public List<PLATEAUCityObjectGroup> convertTargets = new List<PLATEAUCityObjectGroup>();

        [Serializable]
        public enum CopyMethod
        {
            None,
            //ConvexHull,
            Outline,
        }

        [SerializeField] private CopyMethod copyMethod;


        [Serializable]
        public class SplineTestParam
        {
            [Serializable]
            public class SplinePoint
            {
                public Vector2 point;
                public Vector2 dir;
                public Color color = Color.white;
            }

            public bool enable = false;
            public float drawPointRadius = 0.1f;
            public List<SplinePoint> points = new List<SplinePoint>();
        }
        [Header("Test")]
        public SplineTestParam splineTest = new SplineTestParam();

        static float3 ToFloat3(Vector3 v) => new float3(v.x, v.y, v.z);
        public void SplineTest(SplineTestParam p)
        {
            if (p.enable == false)
                return;
            if (p.points.Count < 2)
                return;

            void DrawPoint(SplineTestParam.SplinePoint pos)
            {
                DebugEx.DrawSphere(pos.point, p.drawPointRadius, pos.color);
                DebugEx.DrawArrow(pos.point, pos.point + pos.dir, bodyColor: pos.color);
            }
            var p1 = p.points[0];
            var p2 = p.points[1];
            DrawPoint(p1);
            DrawPoint(p2);
            var sp = new Spline(2);
            sp.Add(new BezierKnot(ToFloat3(p1.point.Xya()), -ToFloat3(p1.dir.Xya()), ToFloat3(p1.dir.Xya())));
            sp.Add(new BezierKnot(ToFloat3(p2.point.Xya()), -ToFloat3(p2.dir.Xya()), ToFloat3(p2.dir.Xya())));

            var positions = new List<Vector3>();
            foreach (var i in Enumerable.Range(0, 10))
            {
                var t = i / 9f;

                sp.Evaluate(t, out var pos, out var tangent, out var up);
                positions.Add(pos);
            }
            DebugEx.DrawLines(positions);
        }

        [Serializable]
        public class LerpPointInLineTestParam
        {
            public bool enable = false;
            public SerializableRay2D ray1 = new SerializableRay2D(Vector2.zero, Vector2.right);
            public SerializableRay2D ray2 = new SerializableRay2D(Vector2.zero, Vector2.right);
            public float p = 0.5f;
            public float drawRayLength = 10;
            public Vector2 drawTextScreenOffset = Vector2.zero;
        }

        [SerializeField] private LerpPointInLineTestParam lerpPointInLineTestParam;
        private void LerpPointInLineTest(LerpPointInLineTestParam p)
        {
            if (p.enable == false)
                return;

            var ray1 = p.ray1.Ray;
            var ray2 = p.ray2.Ray;
            DebugEx.DrawArrow(ray1.origin, ray1.origin + ray1.direction * p.drawRayLength, bodyColor: Color.red);
            DebugEx.DrawArrow(ray2.origin, ray2.origin + ray2.direction * p.drawRayLength, bodyColor: Color.blue);

            if (GeoGraph2D.CalcLerpPointInLine(ray1, ray2, p.p, out var pos))
            {
                DebugEx.DrawArrow(ray1.origin, pos, bodyColor: Color.yellow);
                var nearest = ray2.GetNearestPoint(pos);
                DebugEx.DrawArrow(nearest, pos, bodyColor: Color.white);

                DebugEx.DrawString($"ray1[{(pos - ray1.origin).magnitude}] :  ray2[{(nearest - pos).magnitude}] = {p.p} : {1 - p.p}", pos, p.drawTextScreenOffset);

                DebugEx.DrawSphere(pos, 0.1f, Color.green);
            }
        }


        [Serializable]
        public class Segment3DIntersectionTest
        {
            public bool enable = false;
            public GameObject v0;
            public GameObject v1;
            public GameObject v2;
            public GameObject v3;
            public AxisPlane plane = AxisPlane.Xz;
            public float normalEpsilon = 0.1f;
            public float sphereRadius = 0.1f;
            public void Update()
            {
                if (!enable)
                    return;
                if (!v0 || !v1 || !v2 || !v3)
                    return;
                var s1 = new LineSegment3D(v0.transform.position, v1.transform.position);
                var s2 = new LineSegment3D(v2.transform.position, v3.transform.position);

                DebugEx.DrawLine(s1.Start, s1.End, color: Color.red);
                DebugEx.DrawLine(s2.End, s2.Start, color: Color.blue);
                if (s1.TrySegmentIntersectionBy2D(s2, plane, normalEpsilon, out var intersection, out var t1,
                        out var t2))
                {
                    DebugEx.DrawSphere(intersection, sphereRadius, Color.green);
                }
            }
        }
        [SerializeField]
        private Segment3DIntersectionTest segment3DIntersectionTest = new Segment3DIntersectionTest();

        public void ConvertTrans()
        {
            foreach (var t in convertTargets)
            {
                var subDivided = SubDividedCityObjectFactory.ConvertCityObjects(new List<PLATEAUCityObjectGroup> { t });
                var num = 0;
                foreach (var so in subDivided.ConvertedCityObjects)
                {
                    foreach (var mesh in so.Meshes)
                    {
                        foreach (var subMesh in mesh.SubMeshes)
                        {
                            var polyIndices = new List<List<int>> { subMesh.Triangles };
                            if (copyMethod == CopyMethod.Outline)
                                polyIndices = subMesh.CreateOutlineIndices().ToList();

                            foreach (var indices in polyIndices)
                            {
                                var obj = new GameObject($"{so.Name}_{num++}");
                                var comp = obj.AddComponent<PLATEAUGeoGraphTesterLineString>();
                                obj.transform.parent = transform;
                                comp.LineString.plane = AxisPlane.Xz;
                                for (var i = 0; i < indices.Count; i++)
                                {
                                    var v0 = mesh.Vertices[indices[i]];
                                    var c = new GameObject($"v_{i}");
                                    c.transform.parent = obj.transform;
                                    c.transform.position = v0;
                                }
                            }
                        }
                    }
                }

            }
        }

        private void LerpSegmentsTest(LerpLineSegmentsTestParam param)
        {
            if (param.enable == false)
                return;
            if (param.p <= 0 || param.p >= 1)
                return;

            var childIndex = 0;
            var tr = transform;
            for (var i = 0; i < tr.childCount; i++)
            {
                var child = tr.GetChild(i);
                if (child.transform.childCount != 2)
                    continue;
                if (child.gameObject.activeInHierarchy == false)
                    continue;
                var left = child.GetChild(0).GetComponent<PLATEAUGeoGraphTesterLineString>();
                var right = child.GetChild(1).GetComponent<PLATEAUGeoGraphTesterLineString>();
                if (!left || !right)
                    continue;
                var leftVertices = left.GetVertices().Select(v => v.Xya()).ToList();
                var rightVertices = right.GetVertices().Select(v => v.Xya()).ToList();

                if (param.showWay)
                {
                    foreach (var v in new[] { leftVertices, rightVertices })
                    {
                        var color = DebugEx.GetDebugColor(childIndex++, 8);
                        DebugEx.DrawArrows(v, false, arrowSize: 0.1f, color: color, arrowUp: Vector3.forward);
                    }
                }
                var segments = GeoGraphEx.GetInnerLerpSegments(leftVertices, rightVertices, AxisPlane.Xy, param.p);
                DebugEx.DrawArrows(segments);
            }
        }

        private void ParabolaTest(ParabolaTestParam param)
        {
            if (param.enable == false)
                return;

            var p = param.p;
            var y = param.b;
            var p2 = p * p;
            var a = 2 * p - 1;
            var b = 2 * y * p2;
            var c0 = p2 * y * y;

            // 以下の二次方程式になる
            // ay^2 + 2by + c0 + p^2x^2 = 0

            //var c0 = 2 * p2 * y0;
            //var c1 = 4 * p4 * y02 - 4 * d * p2 * y02;
            //var c2 = -4 * d * p2;
            // y = (c0 ± √(c1 + c2*x^2)) / 2d

            var rangeX = Mathf.Sqrt(((b * b / (4 * a)) - c0) / p2);

            float GetY(float x)
            {
                if (Mathf.Abs(a) < GeoGraph2D.Epsilon)
                    return x * x / (2 * y) + y / 2f;
                var c = c0 + p * p * x * x;
                var d = b * b - 4 * a * c;
                var ret = (b - Mathf.Sqrt(d)) / (2 * a);
                return ret;
            }

            var pos = param.pos;
            var ray = param.ray;
            var border = GeoGraph2D.GetBorderParabola(ray, param.pos, param.p);
            var minX = border.RangeX.HasValue ? -border.RangeX.Value : param.minX;
            for (var x0 = -border.RangeX ?? param.minX; x0 <= (border.RangeX ?? param.maxX); x0 += param.deltaX)
            {
                var y0 = GetY(x0);
                var x1 = x0 + param.deltaX;
                var y1 = GetY(x1);
                var p0 = border.GetPoint(x0);
                var p1 = border.GetPoint(x1);
                //Debug.DrawLine(new Vector3(x0, y0), new Vector3(x1, y1));
                DebugEx.DrawLine(p0, p1);
            }

            DebugEx.DrawLine(ray.origin - ray.direction * 10, ray.origin + ray.direction * 10);
            DebugEx.DrawLine(pos, pos.Xya() + Vector3.forward);

            //Debug.DrawLine(Vector3.left * 10, Vector3.right * 10, Color.red);
            //Debug.DrawLine(Vector3.zero, Vector3.up, Color.green);
        }

        private void ConvexTest(ConvexTestParam p)
        {
            if (p.enable == false)
                return;
            var vertices = p.targets
                .Select(x => x.GetComponent<MeshCollider>())
                .Where(x => x)
                .SelectMany(x => x.sharedMesh.vertices.Select(a => a.Xz()))
                .ToList();
            var convex = GeoGraph2D.ComputeConvexVolume(vertices);
            DebugEx.DrawArrows(convex.Select(x => x.Xay()));
        }

        private void LerpLineTest(LerpLineTestParam p)
        {
            if (p.enable == false)
                return;
            if (!p.target)
                return;
            var vertices = p.target.GetComponent<MeshCollider>().sharedMesh.vertices;

            Vector2 GetVertex(int index)
            {
                return vertices[(index + vertices.Length) % vertices.Length].Xz();
            }

            Vector2 GetEdge(int index)
            {
                return (GetVertex(index + 1) - GetVertex(index)).normalized;
            }

            var rayA = new Ray2D(GetVertex(p.indexA), GetEdge(p.indexA));
            var rayB = new Ray2D(GetVertex(p.indexA), -GetEdge(p.indexA - 1));

            DebugEx.DrawArrow(GetVertex(p.indexA).Xay(), GetVertex(p.indexA + 1).Xay(), bodyColor: Color.red);
            DebugEx.DrawArrow(GetVertex(p.indexA).Xay(), GetVertex(p.indexA - 1).Xay(), bodyColor: Color.blue);

            var ray = GeoGraph2D.LerpRay(rayA, rayB, p.p);
            DebugEx.DrawRay(ray.origin.Xay(), ray.direction.Xay(), Color.green);
        }

        private void UnionPolygonTest(UnionPolygonTestParam p)
        {
            if (p.enable == false)
                return;
            if (!p.target)
                return;
            var mesh = p.target.GetComponent<MeshCollider>();
            DebugEx.DrawMesh(mesh.sharedMesh);

        }

        [Serializable]
        public class MeshOutlineTestParam
        {
            public bool enable = false;
            public bool showIndex = false;
            public Color color = Color.white;
            public float epsilon = 0.1f;
            public bool showLoop = true;
            public List<PLATEAUCityObjectGroup> targets;
        }
        public MeshOutlineTestParam meshOutlineTest = new MeshOutlineTestParam();
        private void MeshOutlineTest(MeshOutlineTestParam p)
        {
            if (p.enable == false)
                return;
            foreach (var target in p.targets)
            {
                if (!target)
                    continue;
                var mesh = target.GetComponent<MeshCollider>();
                var sw = new Stopwatch();
                sw.Start();
                var vertices = GeoGraph2D.ComputeMeshOutlineVertices(mesh.sharedMesh, v => v.Xz(), p.epsilon);
                sw.Stop();
                DebugEx.DrawString($"sec={sw.ElapsedMilliseconds}[ms]", vertices.FirstOrDefault());
                DebugEx.DrawLines(vertices, p.showLoop, p.color);
                if (p.showIndex)
                {
                    for (var i = 0; i < vertices.Count; i++)
                    {
                        DebugEx.DrawString($"{i}", vertices[i]);
                    }
                }
            }
        }

        [Serializable]
        public class SplitCityObjectTestParam
        {
            public List<PLATEAUCityObjectGroup> targets = new List<PLATEAUCityObjectGroup>();
            public bool doDestroySrcObject = false;
        }
        public SplitCityObjectTestParam splitCityObjectTestParam = new SplitCityObjectTestParam();

        public void OnDrawGizmos()
        {
            LerpSegmentsTest(lerpSegmentsTest);
            ParabolaTest(parabolaTest);
            ConvexTest(convexTest);
            LerpLineTest(lerpLineTest);
            UnionPolygonTest(unionPolygonTest);
            MeshOutlineTest(meshOutlineTest);
            SplineTest(splineTest);
            LerpPointInLineTest(lerpPointInLineTestParam);
            segment3DIntersectionTest.Update();
        }

    }
}