using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Factory;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.RoadNetwork.Util.Voronoi;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static PLATEAU.RoadNetwork.Factory.RoadNetworkFactory;

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
        // シーン上で選択中のCityObjectGroupのみ表示する
        public bool onlySelectedCityObjectGroupVisible = true;

        public RRoadTypeMask showFaceType = RRoadTypeMask.All;
        public RRoadTypeMask removeFaceType = RRoadTypeMask.Empty;

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
            // FaceGroupで表示
            FaceGroup,
            SideWalk,
        }
        // 描画モード
        public DrawMode drawMode = DrawMode.Normal;

        [Header("FaceGroup")]
        public FaceGroupOption faceGroupOption = new FaceGroupOption { visible = true };

        [Header("Normal")]
        public FaceOption faceOption = new FaceOption { visible = true };
        public EdgeOption edgeOption = new EdgeOption { visible = true };
        public VertexOption vertexOption = new VertexOption { visible = false };

        [Header("SideWalk")]
        public DrawSideWalkOption drawSideWalkOption = new DrawSideWalkOption();

        [Header("Color")]
        public bool useMultiRoadTypeColor = false;

        [SerializeField]
        public List<RoadTypeMaskOption> roadTypeMaskOptions = RoadTypeMaskOption.CreateDefault();

        // --------------------
        // end:フィールド
        // --------------------


        public HashSet<object> InVisibleObjects { get; } = new();
        public HashSet<object> SelectedObjects { get; } = new();

        public class DrawerModel : RnDebugDrawerModel<RGraph> { }


        public class DrawWork : DrawerModel.DrawFrameWork
        {
            public PLATEAURGraphDrawerDebug Self { get; set; }

            Dictionary<RRoadTypeMask, Color> roadTypeMaskColors = new();

            public DrawWork(PLATEAURGraphDrawerDebug self, RGraph model)
                : base(model)
            {
                Self = self;

                foreach (var t in self.roadTypeMaskOptions)
                {
                    if (t.enable == false)
                        continue;
                    //if (Enum.TryParse<RRoadTypeMask>(t.type, true, out var type))
                    {
                        roadTypeMaskColors[t.type] = t.color;
                    }
                }
            }

            public override bool IsGuiSelected(object obj)
            {
                return Self.SelectedObjects.Contains(obj);
            }


            public Color GetColor(RRoadTypeMask roadType)
            {
                Color ret = Color.black;
                var n = 0;

                // Roadはほぼすべてに入っているので除く
                // ただしRoadしかない場合はそのまま
                roadType = AdjustRoadTypeMask(roadType);

                foreach (var item in roadTypeMaskColors)
                {
                    if (roadType.HasFlag(item.Key))
                    {
                        if (Self.useMultiRoadTypeColor == false)
                            return item.Value;
                        ret += item.Value;
                        n++;
                    }
                }
                if (n > 0)
                    ret /= n;

                return ret;
            }
        }

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

        [Serializable]
        public class DrawSideWalkOption
        {
            public DrawOption outsideColor = new DrawOption { color = Color.green };
            public DrawOption insideColor = new DrawOption { color = Color.blue };
            public DrawOption startColor = new DrawOption { color = Color.red };
            public DrawOption endColor = new DrawOption { color = Color.yellow };
        }

        public class Drawer<T> : DrawerModel.Drawer<DrawWork, T>
        {
        }

        [Serializable]
        public class FaceDrawer : Drawer<RFace>
        {
            public override IEnumerable<PLATEAUCityObjectGroup> GetTargetGameObjects(RFace self)
            {
                if (self.CityObjectGroup)
                    yield return self.CityObjectGroup;
            }
        }

        [Serializable]
        public class FaceGroupDrawer : Drawer<RFaceGroup>
        {
            public bool showLaneNum = false;

            public override IEnumerable<PLATEAUCityObjectGroup> GetTargetGameObjects(RFaceGroup self)
            {
                if (self.CityObjectGroup)
                    yield return self.CityObjectGroup;
            }

        }

        [Serializable]
        public class EdgeDrawer : Drawer<REdge> { }

        [Serializable]
        public class VertexDrawer : Drawer<RVertex> { }

        [Serializable]
        public class VertexOption : VertexDrawer
        {
            public int size = 10;
            public DrawOption neighborOption = new DrawOption { visible = false, color = Color.yellow };
            public bool showPos = false;
            public bool showEdgeCount = false;
            public bool useAnyFaceVertexColor = false;

            protected override bool DrawImpl(DrawWork work, RVertex vertex)
            {
                var text = $"●";
                if (showEdgeCount)
                    text += $"{vertex.Edges.Count}";

                if (work.Self.showId.HasFlag(RPartsFlag.Vertex))
                    text += $"[{vertex.DebugMyId}]";

                if (showPos)
                    text += $"({vertex.Position.x:F2},{vertex.Position.z:F2})";

                work.Self.DrawString(text, vertex.Position, fontSize: size, color: work.GetColor(vertex.GetTypeMaskOrDefault(useAnyFaceVertexColor)));
                //DebugEx.DrawSphere(vertex.Position, 0.3f, color: GetColor(vertex.GetTypeMaskOrDefault(useAnyFaceVertexColor)));

                if (neighborOption.visible)
                {
                    foreach (var e in vertex.Edges)
                    {
                        work.Self.DrawLine(e.V0.Position, e.V1.Position, neighborOption.color);
                    }
                }

                return true;
            }
        }
        [Serializable]
        public class EdgeOption : EdgeDrawer
        {
            public bool useAnyFaceVertexColor = true;
            // 辺が属するFaceの数を表示する
            public bool showNeighborCount = false;

            protected override bool DrawImpl(DrawWork work, REdge edge)
            {
                var color = work.GetColor(edge.GetTypeMaskOrDefault(useAnyFaceVertexColor));
                work.Self.DrawLine(edge.V0.Position, edge.V1.Position, color);
                if (work.Self.showId.HasFlag(RPartsFlag.Edge))
                    work.Self.DrawString($"[{edge.DebugMyId}]", (edge.V0.Position + edge.V1.Position) / 2);

                if (showNeighborCount)
                {
                    work.Self.DrawString($"{edge.Faces.Count}", (edge.V0.Position + edge.V1.Position) / 2);
                }

                work.Self.vertexOption.Draw(work, edge.V0, work.visibleType);
                work.Self.vertexOption.Draw(work, edge.V1, work.visibleType);
                return true;
            }

        }

        [Serializable]
        public class FaceOption : FaceDrawer
        {
            [Serializable]
            class NormalDrawer : FaceDrawer
            {
                public Color color = Color.white;
                public bool showOutline = true;
                public bool showConvexVolume = false;
                public bool showOutlineLoop = false;
                public bool showEdgeCount = false;
                public FaceOption Parent { get; set; }

                protected override bool DrawImpl(DrawWork work, RFace face)
                {
                    var vertices = Parent.FrameOutlineVertices;

                    if (work.Self.showId.HasFlag(RPartsFlag.Face))
                    {
                        var center = Vector3Ex.Centroid(vertices.Select(v => v.Position));
                        work.Self.DrawString($"F[{face.DebugMyId}]", center);
                    }

                    if (showEdgeCount)
                    {
                        var center = Vector3Ex.Centroid(vertices.Select(v => v.Position));
                        work.Self.DrawString($"Edge={face.Edges.Count}", center);

                    }

                    if (showConvexVolume)
                    {
                        work.Self.DrawArrows(face.ComputeConvexHullVertices().Select(v => v.Position), isLoop: true, color: color);
                    }
                    else if (showOutline)
                    {
                        RGraphEx.OutlineVertex2Edge(vertices, out var edges);
                        foreach (var e in edges)
                        {
                            work.Self.edgeOption.Draw(work, e, work.visibleType);
                        }
                    }
                    else
                    {
                        foreach (var e in face.Edges)
                        {
                            work.Self.edgeOption.Draw(work, e, work.visibleType);
                        }
                    }
                    return true;
                }
            }

            [Serializable]
            class TerminateDrawer : FaceDrawer
            {
                public FaceOption Parent { get; set; }
                public bool showEdge = false;
                public bool showBorderVertex = false;
                public bool showReducedVertex = false;
                public bool showReducedBorderVertex = false;

                protected override bool DrawImpl(DrawWork work, RFace face)
                {
                    var vertices = Parent.FrameOutlineVertices;
                    var outlineGroup = RGraphEx
                        .CreateOutlineBorderGroup(vertices,
                            e => e.Faces.Select(f => f.CityObjectGroup).FirstOrDefault(f => f != face.CityObjectGroup));
                    if (outlineGroup.Count != 2)
                        return false;
                    var group = outlineGroup.FirstOrDefault(x => x.Key == null);
                    if (group == null)
                        return false;

                    List<RVertex> terminateVertices = new();
                    for (var i = 0; i < group.Edges.Count; ++i)
                    {
                        var e = group.Edges[i];
                        if (terminateVertices.Any())
                        {
                            var v = e.GetOppositeVertex(terminateVertices.Last());
                            terminateVertices.Add(v);
                        }
                        else if (group.Edges.Count > 1)
                        {

                            if (group.Edges[1].Contains(e.V0))
                            {
                                terminateVertices.Add(e.V1);
                                terminateVertices.Add(e.V0);
                            }
                            else
                            {
                                terminateVertices.Add(e.V0);
                                terminateVertices.Add(e.V1);
                            }
                        }
                    }

                    if (showEdge)
                    {
                        for (var i = 0; i < terminateVertices.Count - 1; ++i)
                        {
                            var st = terminateVertices[i];
                            var en = terminateVertices[(i + 1) % terminateVertices.Count];
                            if (st != null && en != null)
                                work.Self.DrawArrow(st.Position, en.Position);
                        }
                    }
                    var vs = terminateVertices.Select(v => RnDef.ToVec2(v.Position)).ToList();
                    var res = RnEx.FindBorderEdges(vs);
                    if (showBorderVertex)
                        work.Self.DrawArrows(res.BorderVertices.Select(v => RnDef.ToVec3(v)), color: Color.red);
                    if (showReducedVertex)
                        work.Self.DrawArrows(res.ReducedVertices.Select(v => v.ToVector3(RnDef.Plane)), color: Color.green);
                    if (showReducedBorderVertex)
                        work.Self.DrawArrows(res.ReducedBorderVertices.Select(v => v.ToVector3(RnDef.Plane)), color: Color.yellow);
                    return true;
                }
            }

            [Serializable]
            class CenterSkeletonDrawer : FaceDrawer
            {
                public FaceOption Parent { get; set; }

                public Color centerSkeletonColor = Color.red;
                public float centerSkeletonRefineInterval = -1f;

                protected override bool DrawImpl(DrawWork work, RFace self)
                {
                    var targetPoints = Parent.FrameOutlineVertices
                        .ToList();
                    var points = targetPoints.Select(v => v.Position.ToVector2(RnDef.Plane)).ToList();


                    if (centerSkeletonRefineInterval > 0.1f)
                    {
                        for (var i = 0; i < points.Count;)
                        {
                            var v0 = points[i];
                            var v1 = points[(i + 1) % points.Count];

                            var len = (v0 - v1).magnitude;
                            var num = (int)(len / centerSkeletonRefineInterval);
                            if (num <= 0)
                            {
                                i++;
                            }
                            else
                            {
                                var add = new List<Vector2>();
                                for (var x = 0; x < num; ++x)
                                {
                                    var p = (x + 1f) / (num + 1f);
                                    add.Add(Vector2.Lerp(v0, v1, p));
                                }

                                points.InsertRange(i + 1, add);
                                i += add.Count + 1;
                            }
                        }
                    }

                    var voronoi = RnVoronoiEx.CalcVoronoiData(points, v => new Vector2d(v));

                    var vertices2d = points.Select(v => v).ToList();

                    var edges = GeoGraphEx.GetEdges(vertices2d, true)
                        .Select(v => new LineSegment2D(v.Item1, v.Item2))
                        .ToList();

                    foreach (var e in voronoi.Edges)
                    {
                        if (e.Start == null && e.End == null)
                            continue;

                        if (e.Start == null || e.End == null)
                        {
                            var origin = (e.Start ?? e.End.Value).ToVector2();
                            var dir = e.Start == null ? -e.Direction.ToVector2() : e.Direction.ToVector2();
                            var ray = new Ray2D(origin, dir);

                            //DrawLine(origin.ToVector3(RnDef.Plane), origin.ToVector3(RnDef.Plane) + 100 * dir.ToVector3(RnDef.Plane), centerSkeletonColor);

                            //if (GeoGraph2D.IsInsidePolygon(ray.origin, vertices2d) == false)
                            //    continue;
                            //var ans = edges.Select(e =>
                            //    {
                            //        var res = e.TryHalfLineIntersection(ray.origin, ray.direction, out var inter,
                            //            out var t1,
                            //            out var t2);
                            //        return new { res, inter, t1, t2 };
                            //    })
                            //    .Where(x => x.res)
                            //    .TryFindMin(x => (x.inter - ray.origin).sqrMagnitude, out var m);
                            //if (ans)
                            //{
                            //    DrawLine(ray.origin.ToVector3(RnDef.Plane), m.inter.ToVector3(RnDef.Plane), centerSkeletonColor);
                            //}
                        }
                        else
                        {
                            var start = e.Start.Value.ToVector2();
                            var end = e.End.Value.ToVector2();
                            var ans = edges.Select(e =>
                                {
                                    var res = e.TrySegmentIntersection(start, end, out var inter, out var t1,
                                        out var t2);
                                    return new { res, inter, t1, t2 };
                                }).Where(x => x.res)
                                .TryFindMinElement(x => (x.inter - start).sqrMagnitude, out var m);

                            //DrawLine(start.ToVector3(RnDef.Plane), end.ToVector3(RnDef.Plane), centerSkeletonColor);
                            if (ans)
                            {
                                //DrawLine(start.ToVector3(RnDef.Plane), m.inter.ToVector3(RnDef.Plane), centerSkeletonColor);
                            }
                            else
                            {
                                if (GeoGraph2D.IsInsidePolygon(start, vertices2d) &&
                                    GeoGraph2D.IsInsidePolygon(end, vertices2d))
                                {
                                    work.Self.DrawLine(start.ToVector3(RnDef.Plane), end.ToVector3(RnDef.Plane),
                                        centerSkeletonColor);
                                }
                            }
                        }
                    }

                    return true;
                }
            }

            public bool showOutline = true;
            public bool showConvexVolume = false;

            [SerializeField]
            NormalDrawer normalDrawer = new NormalDrawer { visible = true };

            [SerializeField]
            TerminateDrawer terminateDrawer = new TerminateDrawer { visible = false };

            [SerializeField]
            CenterSkeletonDrawer centerSkeletonDrawer = new CenterSkeletonDrawer { visible = false };

            // 子Drawer用
            public List<RVertex> FrameOutlineVertices { get; set; } = new();

            protected override IEnumerable<DrawerModel.Drawer<DrawWork, RFace>> GetChildDrawers()
            {
                yield return normalDrawer;
                yield return terminateDrawer;
                yield return centerSkeletonDrawer;
            }
            protected override bool DrawImpl(DrawWork work, RFace face)
            {
                if (face.Visible == false)
                    return false;

                var roadTypes = AdjustRoadTypeMask(face.RoadTypes);
                if (roadTypes.HasAnyFlag(work.Self.showFaceType) == false)
                    return false;

                if (roadTypes.HasAnyFlag(work.Self.removeFaceType))
                    return false;

                FrameOutlineVertices = face.ComputeOutlineVertices();
                normalDrawer.Parent = this;
                terminateDrawer.Parent = this;
                centerSkeletonDrawer.Parent = this;
                return true;
            }
        }


        [Serializable]
        public class FaceGroupOption : FaceGroupDrawer
        {
            public RRoadTypeMask showCityObjectOutLinePackMask = RoadNetworkFactory.RoadPackTypes;
            protected override bool DrawImpl(DrawWork work, RFaceGroup face)
            {
                var roadTypes = AdjustRoadTypeMask(face.RoadTypes);
                if (roadTypes.HasAnyFlag(work.Self.showFaceType) == false)
                    return false;

                if (roadTypes.HasAnyFlag(work.Self.removeFaceType))
                    return false;

                var vertices = face.ComputeOutlineVertices(f => true);

                RGraphEx.OutlineVertex2Edge(vertices, out var edges);
                foreach (var e in edges)
                {
                    work.Self.edgeOption.Draw(work, e, work.visibleType);
                }
                return true;
            }
        }

        public static RRoadTypeMask AdjustRoadTypeMask(RRoadTypeMask roadType)
        {
            // Undefinedは不正値なので除く
            // ただしUndefinedだけの場合はそのまま
            //if (roadType != RRoadTypeMask.Undefined)
            //{
            //    roadType &= ~RRoadTypeMask.Undefined;
            //}

            // Roadはほぼすべてに入っているので除く
            // ただしRoadしかない場合はそのまま
            if (roadType != RRoadTypeMask.Road)
            {
                roadType &= ~RRoadTypeMask.Road;
            }


            return roadType;
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
            foreach (var x in SelectedObjects)
            {
                if (x is RFace f)
                    faceOption.Draw(work, f, RnDebugDrawerBase.VisibleType.GuiSelected);
            }

            foreach (var p in graph.Faces)
                faceOption.Draw(work, p, RnDebugDrawerBase.VisibleType.NonSelected);
        }

        private void DrawFaceGroup(RGraph graph, DrawWork work)
        {
            var mask = ~faceGroupOption.showCityObjectOutLinePackMask;
            var faceGroups = graph.GroupBy((f0, f1) =>
            {
                var m0 = f0.RoadTypes & mask;
                var m1 = f1.RoadTypes & mask;
                return m0 == m1;
            });

            foreach (var f in faceGroups)
            {
                RnDebugDrawerBase.VisibleType visibleType;
                if (SelectedObjects.Contains(f.CityObjectGroup))
                    visibleType = RnDebugDrawerBase.VisibleType.GuiSelected;
                else
                    visibleType = RnDebugDrawerBase.VisibleType.NonSelected;
                faceGroupOption.Draw(work, f, visibleType);
            }
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
            var work = new DrawWork(this, graph);
            switch (drawMode)
            {
                case DrawMode.Normal:
                    DrawNormal(graph, work);
                    break;
                case DrawMode.FaceGroup:
                    DrawFaceGroup(graph, work);
                    break;
                case DrawMode.SideWalk:
                    DrawSideWalk(graph, work);
                    break;
            }
        }
    }
}