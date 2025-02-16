using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Util.Voronoi
{

    public class VoronoiData<T>
    {
        public class Edge
        {
            /// <summary>
            /// 開始点
            /// </summary>
            public Vector2d? Start { get; set; }

            /// <summary>
            /// 終了点(半直線の場合はnull)
            /// </summary>
            public Vector2d? End { get; set; }

            /// <summary>
            /// 左側のサイトポイント
            /// </summary>
            public T LeftSitePoint => LeftSitePoints.FirstOrDefault();

            /// <summary>
            /// 右側のサイトポイント
            /// </summary>
            public T RightSitePoint => RightSitePoints.FirstOrDefault();

            /// <summary>
            /// 辺の左側にあるサイトポイント情報
            /// </summary>
            public List<T> LeftSitePoints => Parent.SitePointGroups[LeftSiteIndex];

            /// <summary>
            /// 辺の右側にあるサイトポイント情報
            /// </summary>
            public List<T> RightSitePoints => Parent.SitePointGroups[RightSiteIndex];

            /// <summary>
            /// 左側のサイトポイントインデックス
            /// </summary>
            public int LeftSiteIndex { get; set; }

            /// <summary>
            /// 右側のサイトポイントインデックス
            /// </summary>
            public int RightSiteIndex { get; set; }

            /// <summary>
            /// 辺の方向
            /// </summary>
            public Vector2d Direction { get; set; }

            private VoronoiData<T> Parent { get; }

            public Edge(VoronoiData<T> parent, VEdge edge)
            {
                Parent = parent;
                Direction = edge.Direction.normalized;
                LeftSiteIndex = edge.LeftSiteIndex;
                RightSiteIndex = edge.RightSiteIndex;
                Start = edge.Start?.V;
                End = edge.End?.V;
            }
        }

        /// <summary>
        /// サイトポイント
        /// (同じ頂点を持つサイトポイントが複数あったときのためにリストで持っておく)
        /// </summary>
        public List<List<T>> SitePointGroups { get; set; }

        /// <summary>
        /// 構成エッジリスト
        /// </summary>
        public List<Edge> Edges { get; }

        /// <summary>
        /// 構成頂点リスト
        /// </summary>
        public List<Vector2d> Points { get; }

        public VoronoiData(List<List<T>> sitePointGroups, List<VPoint> points, List<VEdge> edges)
        {
            SitePointGroups = sitePointGroups;
            Points = points.Select(v => v.V).ToList();
            Edges = edges.Select(e => new Edge(this, e)).ToList();
        }

    }

    public static class RnVoronoiEx
    {
        private const double Epsilon = 1e-8;

        private static bool IsZero(double x) => Math.Abs(x) <= Epsilon;

        /// <summary>
        /// ビーチライン. (木構造の構成要素)
        /// </summary>
        public class BeachLine : VTreeNode<BeachLine>
        {
            private Work Work { get; }

            /// <summary>
            /// この放物線を構成するサイトポイント
            /// </summary>
            public Vector2d Site => Work.SitePoints[SiteIndex];

            private int siteIndex = -1;

            /// <summary>
            /// この放物線を構成するサイトポイントのインデックス
            /// </summary>
            public int SiteIndex
            {
                get
                {
                    return IsLeaf ? siteIndex : -1;
                }
                set => siteIndex = value;
            }

            /// <summary>
            /// 自身がノードの場合. 左右の子供の葉で構成される垂直二等分線
            /// </summary>
            public VEdge Edge { get; set; }

            /// <summary>
            /// この弧に含まれる円イベント
            /// </summary>
            public Event CircleEvent { get; set; }

            /// <summary>
            /// 葉作成
            /// </summary>
            /// <param name="work"></param>
            /// <param name="siteIndex"></param>
            public BeachLine(Work work, int siteIndex)
            {
                Work = work;
                SiteIndex = siteIndex;
            }

            /// <summary>
            /// ノード作成
            /// </summary>
            /// <param name="work"></param>
            /// <param name="edge"></param>
            /// <param name="left"></param>
            /// <param name="right"></param>
            public BeachLine(Work work, VEdge edge, BeachLine left, BeachLine right)
            {
                Work = work;
                Edge = edge;
                SetLeft(left);
                SetRight(right);
            }

            /// <summary>
            /// 自分自身の木構造を文字列で表示する
            /// </summary>
            /// <param name="root"></param>
            /// <returns></returns>
            public string ToTreeString(BeachLine root)
            {
                return root.BuildString(x => $"{x.SiteIndex}");
            }
        }

        public enum EventType
        {
            SiteEvent,
            CircleEvent
        }

        public struct EventKey
        {
            public EventType Type { get; set; }

            public Vector2d Point { get; set; }
        }

        public class Event
        {
            /// <summary>
            /// 走査線位置
            /// </summary>
            public VPoint Point { get; set; }

            /// <summary>
            /// イベントタイプ
            /// </summary>
            public EventType Type { get; }

            /// <summary>
            /// Siteイベント用
            /// </summary>
            public int SiteIndex { get; }

            /// <summary>
            /// CircleEvent用
            /// </summary>
            public BeachLine Arch { get; set; }

            /// <summary>
            /// CircleEvent用. 交点
            /// </summary>
            public Vector2d CrossPoint { get; set; }

            private Event(VPoint v, EventType eventType, int siteIndex)
            {
                Point = v;
                Type = eventType;
                Arch = null;
                SiteIndex = siteIndex;
            }

            public static Event SiteEvent(VPoint v, int siteIndex)
            {
                return new Event(v, EventType.SiteEvent, siteIndex);
            }

            public static Event CircleEvent(VPoint v, BeachLine arch, VPoint crossPoint)
            {
                return new Event(v, EventType.CircleEvent, -1) { Arch = arch, CrossPoint = crossPoint };
            }
        }


        private class QueueComparer : IComparer<EventKey>
        {
            public int Compare(EventKey a, EventKey b)
            {
                // yは大きい順
                var dy = Math.Sign(b.Point.y - a.Point.y);
                if (dy != 0)
                    return dy;
                // xは小さい順
                var dx = Math.Sign(a.Point.x - b.Point.x);
                if (dx != 0)
                    return dx;
                // CircleEventが先
                return (int)a.Type - (int)b.Type;
            }
        }

        public class Work
        {
            /// <summary>
            /// イベントキュー
            /// </summary>
            private SortedList<EventKey, Queue<Event>> EventQueue { get; } = new(new QueueComparer());

            /// <summary>
            /// 構成エッジリスト
            /// </summary>
            public List<VEdge> Edges { get; } = new List<VEdge>();

            /// <summary>
            /// 構成頂点リスト
            /// </summary>
            public List<VPoint> Points { get; } = new List<VPoint>();

            /// <summary>
            /// ビーチラインの根
            /// </summary>
            public BeachLine Root { get; set; }

            /// <summary>
            /// 削除予定のイベント
            /// </summary>
            public HashSet<Event> DeleteEvents { get; } = new HashSet<Event>();


            public double LineY { get; set; }

            /// <summary>
            /// キューにイベントがある
            /// </summary>
            public bool AnyEvent => EventQueue.Any();


            private readonly List<VPoint> sitePoints;
            /// <summary>
            /// サイトポイント
            /// </summary>
            public IReadOnlyList<VPoint> SitePoints => sitePoints;

            public Work(List<Vector2d> vertices)
            {
                sitePoints = vertices.Select(v => new VPoint(v)).ToList();
            }

            public void Enqueue(Event e)
            {
                var key = new EventKey { Point = e.Point.V, Type = e.Type };
                if (EventQueue.ContainsKey(key) == false)
                    EventQueue.Add(key, new Queue<Event>());
                EventQueue[key].Enqueue(e);
            }

            public Event Dequeue()
            {
                var q = EventQueue.First();
                var ret = q.Value.Dequeue();
                if (q.Value.Count == 0)
                    EventQueue.RemoveAt(0);
                return ret;
            }

            public IEnumerable<Event> Events => EventQueue.SelectMany(x => x.Value);


            public static bool TryGetY(double ly, Vector2d p, double x, out double y)
            {
                if (CalcBeachLine(p, ly, out var parabola))
                {
                    y = parabola.GetY(x);
                    return true;
                }

                y = 0.0;
                return false;
            }


            private VEdge Edge(VPoint start, int leftSiteIndex, int rightSiteIndex)
            {
                // l, rの垂直二等分線
                var l = SitePoints[leftSiteIndex];
                var r = SitePoints[rightSiteIndex];
                var direction = (new Vector2d(r.y - l.y, -(r.x - l.x))).normalized;
                return new VEdge(start, direction, leftSiteIndex, rightSiteIndex);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="start"></param>
            /// <param name="leftSiteIndex"></param>
            /// <param name="rightSiteIndex"></param>
            /// <param name="leftEdge"></param>
            /// <param name="rightEdge"></param>
            private void Edge(VPoint start, int leftSiteIndex, int rightSiteIndex, out VEdge leftEdge, out VEdge rightEdge)
            {
                // l, rの垂直二等分線
                var l = SitePoints[leftSiteIndex];
                var r = SitePoints[rightSiteIndex];
                var direction = (new Vector2d(r.y - l.y, -(r.x - l.x))).normalized;
                leftEdge = new VEdge(start, direction, leftSiteIndex, rightSiteIndex);
                rightEdge = new VEdge(start, -direction, rightSiteIndex, leftSiteIndex);

                leftEdge.OppositeEdge = rightEdge;
            }


            private double GetXOfEdge(BeachLine par, double y)
            {
                var left = BeachLine.GetLeftChild(par);
                var right = BeachLine.GetRightChild(par);

                var l = left.Site;
                var r = right.Site;

                // yがp.V.yと同じ場合放物線にならない(y軸平行な直線になる)
                // その場合の交点はp.V.xのまま
                if (CalcBeachLine(l, y, out var leftParabola) == false)
                {
                    return l.x;
                }

                if (CalcBeachLine(r, y, out var rightParabola) == false)
                {
                    return r.x;
                }

                var n = leftParabola.GetCrossPoint(rightParabola, out var v1, out var v2);

                if (n == 0)
                    throw new InvalidDataException("x");

                if (n == 1)
                    return v1.x;

                // rの方が先に出てきた場合lの左右で交わる -> lの右側
                if (l.y < r.y)
                    return Math.Max(v1.x, v2.x);

                // lの方が先に出てきた場合rの左右で交わる -> rの左側
                if (r.y < l.y)
                    return Math.Min(v1.x, v2.x);

                throw new InvalidDataException("x");
            }

            /// <summary>
            /// ビーチラインの中からxに対応する放物線を取得する
            /// </summary>
            /// <param name="x"></param>
            /// <param name="centerLeft"></param>
            /// <param name="centerRight"></param>
            /// <returns></returns>
            public void GetParabolaByX(double x, out BeachLine centerLeft, out BeachLine centerRight)
            {
                centerLeft = centerRight = null;
                BeachLine par = Root;
                while (!par.IsLeaf)
                {
                    var tmpX = GetXOfEdge(par, LineY);
                    if (Math.Abs(tmpX - x) <= Epsilon)
                    {
                        centerLeft = par.ChildLeftLeaf;
                        centerRight = par.ChildRightLeaf;
                        return;
                    }
                    par = tmpX >= x ? par.Left : par.Right;
                }

                centerLeft = centerRight = par;
            }

            /// <summary>
            /// 放物線を挿入する
            /// </summary>
            /// <param name="siteIndex"></param>
            public void InsertParabola(int siteIndex)
            {
                var newSite = SitePoints[siteIndex];
                if (Root == null)
                {
                    Root = new BeachLine(this, siteIndex);
                    return;
                }
                GetParabolaByX(newSite.x, out var centerLeft, out var centerRight);
                var center = centerLeft;
                RemoveCircleEvent(center);

                // centerとnewSiteが同じy座標の場合. 放物線は1点でしか交わらないのでツリーの変更もそれに合わせる
                var isSameY = (center.Site.y - newSite.y) <= Epsilon;
                if (isSameY)
                {
                    var (leftIndex, rightIndex) = (center.SiteIndex, siteIndex);
                    if (center.Site.x > newSite.x)
                        (leftIndex, rightIndex) = (rightIndex, leftIndex);

                    VPoint start = new VPoint((newSite + center.Site) * 0.5);
                    Points.Add(start);
                    Edge(start, leftIndex, rightIndex, out var el, out var er);
                    Edges.Add(el);
                    center.Edge = el;

                    var arcLeft = new BeachLine(this, leftIndex);
                    var arcRight = new BeachLine(this, rightIndex);
                    center.SetRight(arcRight);
                    center.SetLeft(arcLeft);

                    CheckCircles(arcLeft, arcRight);
                }
                else
                {
                    var (leftIndex, rightIndex) = (center.SiteIndex, siteIndex);
                    // center.site.y > newSite.yなのでTryGetYは成功する
                    TryGetY(LineY, center.Site, newSite.x, out var newSiteY);

                    VPoint start = new VPoint(newSite.x, newSiteY);
                    Points.Add(start);

                    // startを起点に二つの半直線を作成
                    Edge(start, leftIndex, rightIndex, out var el, out var er);
                    Edges.Add(el);
                    center.Edge = er;

                    var arcLeft = new BeachLine(this, center.SiteIndex);
                    var arcMid = new BeachLine(this, siteIndex);
                    var arcRight = new BeachLine(this, center.SiteIndex);

                    center.SetRight(arcRight);
                    center.SetLeft(new BeachLine(this, el, arcLeft, arcMid));

                    CheckCircles(arcLeft, arcRight);
                }
            }

            private void CheckCircles(params BeachLine[] parabolas)
            {
                foreach (var c in parabolas)
                    CheckCircle(c);
            }

            /// <summary>
            /// 円イベントチェック
            /// </summary>
            /// <param name="b"></param>
            private void CheckCircle(BeachLine b)
            {
                var lp = BeachLine.GetLeftParent(b);
                var rp = BeachLine.GetRightParent(b);

                var leftChild = BeachLine.GetLeftChild(lp);
                var rightChild = BeachLine.GetRightChild(rp);

                // 
                if (leftChild == null || rightChild == null || leftChild.SiteIndex == rightChild.SiteIndex)
                    return;

                var s = GetEdgeIntersection(lp.Edge, rp.Edge);
                if (s == null)
                    return;

                // 走査線よりも上にある場合は無視
                var d = Math.Min((leftChild.Site - s.V).magnitude, (rightChild.Site - s.V).magnitude);
                if (s.y - d > (LineY + Epsilon))
                    return;

                var point = new VPoint(s.x, s.y - d);
                Points.Add(point);

                AddCircleEvent(point, b, s);
            }

            /// <summary>
            /// 辺同士の交点を取得する
            /// </summary>
            /// <param name="a"></param>
            /// <param name="b"></param>
            /// <returns></returns>
            private VPoint GetEdgeIntersection(VEdge a, VEdge b)
            {
                if (VEdge.TryGetCrossPoint(a, b, out var v, Epsilon) == false)
                    return null;

                VPoint p = new VPoint(v.x, v.y);
                Points.Add(p);
                return p;
            }

            /// <summary>
            /// 円イベントを削除する
            /// </summary>
            /// <param name="b"></param>
            private void RemoveCircleEvent(BeachLine b)
            {
                if (b.CircleEvent == null)
                    return;
                DeleteEvents.Add(b.CircleEvent);
                b.CircleEvent = null;
            }

            /// <summary>
            /// 円イベントを追加する
            /// </summary>
            /// <param name="p"></param>
            /// <param name="b"></param>
            /// <param name="crossPoint"></param>
            private void AddCircleEvent(VPoint p, BeachLine b, VPoint crossPoint)
            {
                Event e = Event.CircleEvent(p, b, crossPoint);
                b.CircleEvent = e;
                Enqueue(e);
            }

            /// <summary>
            /// e.archの弧を削除する
            /// </summary>
            /// <param name="e"></param>
            public void RemoveParabola(Event e)
            {
                // p1が消える
                BeachLine p1 = e.Arch;

                var xl = BeachLine.GetLeftParent(p1);
                var xr = BeachLine.GetRightParent(p1);
                var p0 = BeachLine.GetLeftChild(xl);
                var p2 = BeachLine.GetRightChild(xr);

                if (TryGetY(LineY, p1.Site, e.Point.x, out var pointY) == false)
                    return;

                //VPoint p = new VPoint(e.point.x, pointY);
                VPoint p = new VPoint(e.CrossPoint);
                RemoveCircleEvent(p2);
                RemoveCircleEvent(p0);

                Points.Add(p);

                xl.Edge.End = p;
                xr.Edge.End = p;


                // 自分を削除する
                p1.RemoveLeaf();

                // p0/p2の共通の親を探す
                BeachLine higher = null;
                BeachLine par = p1.Parent;
                while (par != null)
                {
                    if (par.ChildLeftLeaf == p0 && par.ChildRightLeaf == p2)
                        higher = par;
                    par = par.Parent;
                }


                higher.Edge = Edge(p, p0.SiteIndex, p2.SiteIndex);
                Edges.Add(higher.Edge);
                p1.Parent = null;

                CheckCircles(p0, p2);
            }


            /// <summary>
            /// 二つに分けていた半直線を結合する
            /// </summary>
            public void CheckEdgeEnd()
            {
                foreach (var e in Edges)
                {
                    if (e.OppositeEdge == null)
                        continue;

                    e.Start = e.OppositeEdge.End;
                    e.OppositeEdge = null;
                }
            }
        }

        public static VoronoiData<T> CalcVoronoiData<T>(List<T> points, Func<T, Vector2d> toVec2)
        {
            var grouped = points.GroupBy(toVec2).ToList();
            var sitePoints = grouped.Select(g => g.Key).ToList();
            var work = new Work(sitePoints);

            for (var i = 0; i < work.SitePoints.Count; ++i)
            {
                //p.V -= Min;
                var ev = Event.SiteEvent(work.SitePoints[i], i);
                work.Enqueue(ev);
            }

            while (work.AnyEvent)
            {
                var e = work.Dequeue();
                work.LineY = e.Point.y;
                if (work.DeleteEvents.Contains(e))
                {
                    work.DeleteEvents.Remove(e);
                    continue;
                }
                switch (e.Type)
                {
                    case EventType.SiteEvent:
                        work.InsertParabola(e.SiteIndex);
                        break;
                    case EventType.CircleEvent:
                        work.RemoveParabola(e);
                        break;
                }
            }

            work.CheckEdgeEnd();
            return new VoronoiData<T>(grouped.Select(g => g.ToList()).ToList(), work.Points, work.Edges);
        }



        /// <summary>
        /// ビーチライン計算. p.y &lt; lineYの時は存在しない
        /// </summary>
        /// <param name="p"></param>
        /// <param name="lineY"></param>
        /// <param name="ret"></param>
        /// <returns></returns>
        public static bool CalcBeachLine(Vector2d p, double lineY, out VParabola ret)
        {
            double dp = 2 * (p.y - lineY);
            if (dp <= Epsilon)
            {
                ret = null;
                return false;
            }
            dp = 1 / dp;
            var a = dp;
            var b = -2 * p.x * dp;
            var c = (p.x * p.x - lineY * lineY + p.y * p.y) * dp;
            ret = new VParabola(a, b, c);
            return true;
        }
    }

}