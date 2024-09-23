using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Graph.Drawer
{
    [Serializable]
    public class PLATEAURGraphDrawerDebug : MonoBehaviour
    {
        // --------------------
        // start:フィールド
        // --------------------
        public bool visible = false;
        public bool showAll = true;

        public bool onlySelectedCityObjectGroupVisible = true;

        public RRoadTypeMask showFaceType = RRoadTypeMask.All;

        [Flags]
        public enum RPartsFlag
        {
            Vertex = 1 << 0,
            Edge = 1 << 1,
            Face = 1 << 2,
            All = ~0
        }
        public RPartsFlag showId = 0;


        public enum DrawMode
        {
            Normal,
            SideWalk,
        }
        // 描画モード
        public DrawMode drawMode = DrawMode.Normal;

        [Serializable]
        public class FaceOption : DrawOption
        {
            public bool showOutline = true;
            public bool showConvexVolume = false;
            public RRoadTypeMask showOutlineMask = RRoadTypeMask.Road;
            public RRoadTypeMask showOutlineRemoveMask = RRoadTypeMask.Empty;
            public bool showCityObjectOutline = false;
            public bool showOutlineLoop = false;
        }

        public FaceOption faceOption = new FaceOption();
        [Serializable]
        public class EdgeOption : DrawOption
        {
            public bool useAnyFaceVertexColor = false;
        }
        public EdgeOption edgeOption = new EdgeOption();

        [Serializable]
        public class VertexOption : DrawOption
        {
            public int size = 10;
            public DrawOption neighborOption = new DrawOption { visible = false, color = Color.yellow };
            public bool showPos = false;
            public bool showEdgeCount = false;
            public bool useAnyFaceVertexColor = false;
        }
        public VertexOption vertexOption = new VertexOption();

        [Serializable]
        public class RoadTypeMaskOption
        {
            public RRoadTypeMask type;
            public Color color;
            public bool enable = true;

            public static List<RoadTypeMaskOption> CreateDefault()
            {
                var ret = new List<RoadTypeMaskOption>();
                var i = 0;
                var values = Enum.GetValues(typeof(RRoadTypeMask)).Cast<RRoadTypeMask>().ToList();
                foreach (var t in values)
                {
                    if (t == RRoadTypeMask.Empty || t == RRoadTypeMask.All)
                        continue;
                    var color = DebugEx.GetDebugColor(i++, values.Count());
                    ret.Add(new RoadTypeMaskOption { type = t, color = color, enable = true });
                }
                return ret;
            }
        }

        [SerializeField]
        public List<RoadTypeMaskOption> roadTypeMaskOptions = RoadTypeMaskOption.CreateDefault();

        [Serializable]
        public class DrawSideWalkOption
        {
            public DrawOption outsideColor = new DrawOption { color = Color.green };
            public DrawOption insideColor = new DrawOption { color = Color.blue };
            public DrawOption startColor = new DrawOption { color = Color.red };
            public DrawOption endColor = new DrawOption { color = Color.yellow };
        }
        public DrawSideWalkOption drawSideWalkOption = new DrawSideWalkOption();

        // --------------------
        // end:フィールド
        // --------------------


        public HashSet<RFace> TargetFaces { get; } = new();
        public HashSet<REdge> TargetEdges { get; } = new();
        public HashSet<RVertex> TargetVertices { get; } = new();

        private class DrawWork
        {
            public RGraph graph;

            public HashSet<RVertex> visitedVertices = new HashSet<RVertex>();

            public HashSet<REdge> visitedEdges = new HashSet<REdge>();

            public HashSet<RFace> visitedFaces = new HashSet<RFace>();

        }

        private void DrawArrows(IEnumerable<Vector3> vertices
            , bool isLoop = false
            , float arrowSize = 0.5f
            , Vector3? arrowUp = null
            , Color? color = null
            , Color? arrowColor = null
            , float duration = 0f
            , bool depthTest = true)
        {
            DebugEx.DrawArrows(vertices, isLoop, arrowSize, arrowUp, color, arrowColor, duration, depthTest);
        }
        private void DrawString(string text, Vector3 worldPos, Vector2? screenOffset = null, Color? color = null, int? fontSize = null)
        {
            DebugEx.DrawString(text, worldPos, screenOffset, color, fontSize);
        }

        private void DrawLine(Vector3 start, Vector3 end, Color? color = null)
        {
            Debug.DrawLine(start, end, color ?? Color.white);
        }

        public static void DrawLines(
            IEnumerable<Vector3> vertices
            , bool isLoop = false
            , Color? color = null
            , float duration = 0f
            , bool depthTest = true)
        {
            DebugEx.DrawLines(vertices, isLoop, color, duration, depthTest);
        }

        private void DrawArrow(
            Vector3 start
            , Vector3 end
            , float arrowSize = 0.5f
            , Vector3? arrowUp = null
            , Color? bodyColor = null
            , Color? arrowColor = null
            , float duration = 0f
            , bool depthTest = true)
        {
            DebugEx.DrawArrow(start, end, arrowSize, arrowUp, bodyColor, arrowColor, duration, depthTest);
        }

        public Color GetColor(RRoadTypeMask roadType)
        {
            Color ret = Color.black;
            var n = 0;
            foreach (var t in roadTypeMaskOptions)
            {
                if (t.enable && roadType.HasFlag(t.type))
                {
                    ret += t.color;
                    n++;
                }
            }
            if (n > 0)
                ret /= n;

            return ret;
        }

        private IEnumerable<PLATEAUCityObjectGroup> GetSelectedCityObjectGroups()
        {
            return Selection.gameObjects.Select(go => go.GetComponent<PLATEAUCityObjectGroup>()).Where(cog => cog != null);
        }

        private void Draw(VertexOption op, RVertex vertex, DrawWork work)
        {
            if (op.visible == false)
                return;

            if (work.visitedVertices.Contains(vertex))
                return;

            work.visitedVertices.Add(vertex);

            var text = $"●";
            if (op.showEdgeCount)
                text += $"{vertex.Edges.Count}";

            if (showId.HasFlag(RPartsFlag.Vertex))
                text += $"[{vertex.DebugMyId}]";

            if (op.showPos)
                text += $"({vertex.Position.x:F2},{vertex.Position.z:F2})";

            DrawString(text, vertex.Position, fontSize: op.size, color: GetColor(vertex.GetTypeMaskOrDefault(op.useAnyFaceVertexColor)));

            if (TargetVertices.Contains(vertex))
            {
                if (op.neighborOption.visible)
                {
                    //foreach (var b in vertex.GetNeighborVertices())
                    //{
                    //    DrawLine(vertex.Position, b.Position, op.neighborOption.color);
                    //}

                    foreach (var e in vertex.Edges)
                    {
                        DrawLine(e.V0.Position, e.V1.Position, op.neighborOption.color);
                    }
                }
            }
        }

        private void Draw(EdgeOption op, REdge edge, DrawWork work)
        {
            if (work.visitedEdges.Contains(edge))
                return;
            work.visitedEdges.Add(edge);
            if (showAll == false && (TargetEdges?.Any() == true) && TargetEdges.Contains(edge) == false)
                return;
            if (op.visible)
            {
                var color = GetColor(edge.GetTypeMaskOrDefault(op.useAnyFaceVertexColor));
                DrawLine(edge.V0.Position, edge.V1.Position, color);
                if (showId.HasFlag(RPartsFlag.Edge))
                    DrawString($"[{edge.DebugMyId}]", (edge.V0.Position + edge.V1.Position) / 2);
            }

            Draw(vertexOption, edge.V0, work);
            Draw(vertexOption, edge.V1, work);
        }

        private void Draw(FaceOption op, RFace face, DrawWork work)
        {
            if (op.visible == false)
                return;

            if (face.Visible == false)
                return;

            if (showAll == false && (TargetFaces?.Any() == true) && TargetFaces.Contains(face) == false)
                return;

            if (face.RoadTypes.HasAnyFlag(showFaceType) == false)
                return;

            if (work.visitedFaces.Contains(face))
                return;

            work.visitedFaces.Add(face);
            if (onlySelectedCityObjectGroupVisible)
            {
#if UNITY_EDITOR
                var show = GetSelectedCityObjectGroups().Any(cog => face.CityObjectGroup == cog);
                if (show == false)
                    return;
#endif
            }

            var vertices = op.showCityObjectOutline ? work.graph.ComputeOutlineVerticesByCityObjectGroup(face.CityObjectGroup, op.showOutlineMask, op.showOutlineRemoveMask)
                : face.ComputeOutlineVertices();

            //bool isLoop = true;
            //if (vertices.Count > 1)
            //{
            //    if ((vertices[0].Edges.Any(e => e.V0 == vertices[^1] || e.V1 == vertices[^1])) == false)
            //    {
            //        isLoop = false;
            //    }

            //}
            RGraphEx.OutlineVertex2Edge(vertices, out var edges);
            if (showId.HasFlag(RPartsFlag.Face))
            {
                var center = vertices.Aggregate(Vector3.zero, (a, v) => v.Position + a) / vertices.Count;
                DrawString($"F[{face.DebugMyId}]", center);
            }

            if (op.showConvexVolume)
            {
                DrawArrows(face.ComputeConvexHullVertices().Select(v => v.Position), isLoop: true, color: edgeOption.color);
            }
            else if (op.showOutline)
            {

                var ii = 0;
                foreach (var v in vertices)
                {
                    //DebugEx.DrawString((ii++).ToString(), v.Position, color: Color.white);
                }
                foreach (var e in edges)
                {
                    Draw(edgeOption, e, work);
                }
            }
            else
            {
                foreach (var e in face.Edges)
                {
                    Draw(edgeOption, e, work);
                }
            }
        }

        private void DrawSideWalk(RGraph graph, DrawWork work)
        {
            void Draw(List<REdge> edges, DrawOption option)
            {
                if (option.visible == false)
                    return;
                foreach (var e in edges)
                {
                    DrawLine(e.V0.Position, e.V1.Position, option.color);
                }
            }

            foreach (var face in graph.Faces)
            {
                if (face.RoadTypes.IsSideWalk() == false)
                    continue;

                var success = face.CreateSideWalk(out var outsideEdges, out var insideEdges, out var startEdges, out var endEdges);
                if (!success)
                    continue;
                Draw(outsideEdges, drawSideWalkOption.outsideColor);
                Draw(insideEdges, drawSideWalkOption.insideColor);
                Draw(startEdges, drawSideWalkOption.startColor);
                Draw(endEdges, drawSideWalkOption.endColor);
            }
        }

        private void DrawNormal(RGraph graph, DrawWork work)
        {

            foreach (var p in graph.Faces)
                Draw(faceOption, p, work);
        }

        public void OnDrawGizmos()
        {
            if (visible == false)
                return;

            var target = GetComponent<PLATEAURGraph>();
            if (!target)
                return;
            var graph = target.Graph;
            if (graph == null)
                return;
            var work = new DrawWork { graph = graph };
            switch (drawMode)
            {
                case DrawMode.Normal:
                    DrawNormal(graph, work);
                    break;
                case DrawMode.SideWalk:
                    DrawSideWalk(graph, work);
                    break;
            }
        }
    }
}