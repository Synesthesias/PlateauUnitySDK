using PLATEAU.RoadNetwork.Structure;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace PLATEAU.RoadNetwork.Tester
{
    /// <summary>
    /// 線分(ポリゴン)を使ったテストクラス
    /// </summary>
    public class PLATEAUGeoGraphTesterLineString : PLATEAURnTesterLineStringHolder
    {
        [Serializable]
        public class EdgeBorderTestParam
        {
            public bool enable = false;
            public float allowAngle = 20f;
            public float skipAngle = 20f;
            public GeoGraph2D.DebugFindOppositeOption op = new GeoGraph2D.DebugFindOppositeOption();
        }
        public EdgeBorderTestParam edgeBorderTest = new EdgeBorderTestParam();

        [Serializable]
        public class SplitCollinearTestParam
        {
            public bool enable = false;
            public float allowAngle = 20f;
            public float midPointTolerance = 1f;
            public bool checkAdjustSegAngleOnly = false;
        }
        public SplitCollinearTestParam splitCollinearTest = new SplitCollinearTestParam();
        [Serializable]
        public class TunTypeTestParam
        {
            public bool enable = false;
            public RnTurnType turnType;
        }
        public TunTypeTestParam turnTypeTest = new TunTypeTestParam();

        [Serializable]
        public class RoadSliceTestParam
        {
            public bool enable = false;
            public long rnRoadId = -1;
            public AxisPlane plane = AxisPlane.Xz;
            public bool exec = false;
            public bool exec2 = false;
        }
        [FormerlySerializedAs("lineStringIntersectionTest")] public RoadSliceTestParam roadIntersectionTest = new RoadSliceTestParam();


        [Serializable]
        public class InsideTestParam
        {
            public bool enable = false;
            public GameObject target;
        }
        public InsideTestParam insideTest = new InsideTestParam();

        void InsideTest(InsideTestParam p)
        {
            if (p.enable == false)
                return;
            var target = p.target;
            if (!target)
                return;
            var vertices = GetVertices();
            var isInside = GeoGraph2D.IsInsidePolygon(target.transform.position.GetTangent(plane), vertices);
            DebugEx.DrawString($"{isInside}", target.transform.position);
        }

        private void SplitCollinearTest(SplitCollinearTestParam p)
        {
            if (p.enable == false)
                return;
            var vertices = GetVertices().Skip(1).ToList();
            var indices = GeoGraph2D.SplitByCollinearSegment(vertices, p.allowAngle, p.midPointTolerance, p.checkAdjustSegAngleOnly);

            for (var i = 0; i < indices.Count - 1; i++)
            {
                var start = indices[i];
                var end = indices[i + 1];
                var color = DebugEx.GetDebugColor(i % 2, 2);

                DebugEx.DrawLines(Enumerable.Range(start, end - start + 1).Select(i => vertices[i].ToVector3(plane)), false, color: color);
            }
            var afterVerts = indices.Select(i => vertices[i]).ToList();

            var ind = GeoGraph2D.FindMidEdge(afterVerts, edgeBorderTest.allowAngle, edgeBorderTest.skipAngle);

            DebugEx.DrawLines(
                ind.Select(i =>
                {
                    return vertices[indices[i]].ToVector3(plane);
                })

                , false, color: Color.green
                );

        }
        private void EdgeBorderTest(EdgeBorderTestParam p)
        {
            if (p.enable == false)
                return;
            var vertices = GetVertices().Skip(1).ToList();
            var res = RnEx.FindBorderEdges(vertices, p.allowAngle, p.skipAngle);


            //DrawLine(Enumerable.Range(0, edgeIndices[0] + 1), Color.green);
            DebugEx.DrawArrows(res.BorderVertices.Select(v => v.ToVector3(plane)), color: Color.red);
            //DrawLine(Enumerable.Range(edgeIndices.Last(), vertices.Count - edgeIndices.Last()), Color.green);
        }

        private void TurnTypeTest(TunTypeTestParam p)
        {
            if (p.enable == false)
                return;
            var vertices = GetVertices();
            if (vertices.Count < 3)
            {
                Debug.LogWarning("頂点数が足りません");
                return;
            }
            var d1 = vertices[1] - vertices[0];
            var d2 = vertices[2] - vertices[1];
            var type = RnTurnTypeEx.GetTurnType(d1, d2);
            DebugEx.DrawString($"{type}", vertices[0].ToVector3(plane));
        }

        private void RoadSliceTest(RoadSliceTestParam p)
        {
            var exec = p.exec;
            p.exec = false;
            var exec2 = p.exec2;
            p.exec2 = false;
            if (p.enable == false)
                return;
            var target = GameObject.FindAnyObjectByType<PLATEAURnStructureModel>();
            var plateauRnStructureModel = target;
            if (!plateauRnStructureModel || plateauRnStructureModel.RoadNetwork == null)
                return;

            var edges = GeoGraphEx.GetEdges(GetVertices3D(), false).Select(s => new LineSegment3D(s.Item1, s.Item2)).ToList();

            var roads = plateauRnStructureModel.RoadNetwork.Roads.Where(r =>
                p.rnRoadId < 0 || r.GetDebugMyIdOrDefault() == p.rnRoadId).ToList();
            foreach (var road in roads)
            {
                var isCrossed = false;
                foreach (var segment in edges)
                {
                    var res = road.GetLaneCrossPoints(segment);
                    if (res == null)
                        continue;
                    foreach (var v in res.TargetLines.SelectMany(w => w.Intersections))
                    {
                        DebugEx.DrawSphere(v.v, 0.3f, Color.red);
                        DebugEx.DrawString($"{v.index}", v.v);
                        isCrossed = true;
                    }

                    if (exec && isCrossed)
                    {
                        var sliceRes = plateauRnStructureModel.RoadNetwork.SliceRoadHorizontal(road, segment);
                        DebugEx.Log(sliceRes.Result);
                    }
                }

                if (exec2 && edges.Count >= 3 && isCrossed)
                {
                    plateauRnStructureModel.RoadNetwork.SliceRoadHorizontalAndConvert2Intersection(road, edges[0], edges[2]);
                }
            }

            var intersections = plateauRnStructureModel.RoadNetwork.Intersections.Where(r =>
                p.rnRoadId < 0 || r.GetDebugMyIdOrDefault() == p.rnRoadId).ToList();
            foreach (var road in intersections)
            {
                var isCrossed = false;
                foreach (var segment in edges)
                {
                    var res = road.GetEdgeCrossPoints(segment);
                    if (res == null)
                        continue;
                    foreach (var v in res.TargetLines.SelectMany(w => w.Intersections))
                    {
                        DebugEx.DrawSphere(v.v, 0.3f, Color.red);
                        DebugEx.DrawString($"{v.index}", v.v);
                        isCrossed = true;
                    }

                    if (exec && isCrossed)
                    {
                        var sliceRes = plateauRnStructureModel.RoadNetwork.SliceIntersectionHorizontal(road, segment);
                        DebugEx.Log(sliceRes.Result);
                    }
                }
            }
        }

        public void OnDrawGizmos()
        {
            if (!gameObject.activeInHierarchy)
                return;

            EdgeBorderTest(edgeBorderTest);
            TurnTypeTest(turnTypeTest);
            RoadSliceTest(roadIntersectionTest);
            InsideTest(insideTest);
            SplitCollinearTest(splitCollinearTest);
        }

    }
}