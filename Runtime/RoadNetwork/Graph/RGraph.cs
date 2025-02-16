using PLATEAU.CityInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace PLATEAU.RoadNetwork.Graph
{
    /// <summary>
    /// 道路タイプ
    /// </summary>
    [Flags]
    public enum RRoadTypeMask
    {
        /// <summary>
        /// 何もなし
        /// </summary>
        Empty = 0,
        /// <summary>
        /// 不正な値(今後要素が増えると後ろに追加する必要があるので最初に定義する)
        /// </summary>
        Undefined = 1 << 0,
        /// <summary>
        /// 車道
        /// </summary>
        Road = 1 << 1,
        /// <summary>
        /// 歩道
        /// </summary>
        SideWalk = 1 << 2,
        /// <summary>
        /// 中央分離帯
        /// </summary>
        Median = 1 << 3,
        /// <summary>
        /// 高速道路
        /// </summary>
        HighWay = 1 << 4,
        /// <summary>
        /// 車線. Lod3.1以上だとこれが車線を表す
        /// </summary>
        Lane = 1 << 5,
        /// <summary>
        /// 全ての値
        /// </summary>
        All = ~(0)
    }

    public static class RRoadTypeEx
    {
        /// <summary>
        /// 車道部分
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsRoad(this RRoadTypeMask self)
        {
            return (self & RRoadTypeMask.Road) != 0;
        }

        /// <summary>
        /// 交通道路
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsHighWay(this RRoadTypeMask self)
        {
            return (self & RRoadTypeMask.HighWay) != 0;
        }

        /// <summary>
        /// 歩道
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsSideWalk(this RRoadTypeMask self)
        {
            return (self & RRoadTypeMask.SideWalk) != 0;
        }

        /// <summary>
        /// 歩道
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsMedian(this RRoadTypeMask self)
        {
            return (self & RRoadTypeMask.Median) != 0;
        }

        /// <summary>
        /// 車線
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsLane(this RRoadTypeMask self)
        {
            return (self & RRoadTypeMask.Lane) != 0;
        }
        /// <summary>
        /// selfがflagのどれかを持っているかどうか
        /// </summary>
        /// <param name="self"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool HasAnyFlag(this RRoadTypeMask self, RRoadTypeMask flag)
        {
            return (self & flag) != 0;
        }
    }

    /// <summary>
    /// 接点
    /// </summary>
    [Serializable]
    public class RVertex : ARnParts<RVertex>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------

        /// <summary>
        /// 接続辺
        /// </summary>
        private HashSet<REdge> edges = new HashSet<REdge>();

        /// <summary>
        /// 位置
        /// </summary>
        [field: SerializeField]
        public Vector3 Position { get; set; }

        //----------------------------------
        // start: フィールド
        //----------------------------------

        /// <summary>
        /// 接続辺
        /// </summary>
        public IReadOnlyCollection<REdge> Edges => edges;

        public RRoadTypeMask TypeMask
        {
            get
            {
                var ret = RRoadTypeMask.Empty;
                foreach (var edge in Edges)
                {
                    foreach (var face in edge.Faces)
                        ret |= face.RoadTypes;
                }

                return ret;
            }
        }


        public IEnumerable<RFace> GetFaces()
        {
            foreach (var edge in Edges)
            {
                foreach (var face in edge.Faces)
                    yield return face;
            }
        }

        public RVertex(Vector3 v)
        {
            Position = v;
        }

        /// <summary>
        /// 基本呼び出し禁止. 接続辺追加
        /// </summary>
        /// <param name="edge"></param>
        public void AddEdge(REdge edge)
        {
            if (edges.Contains(edge))
                return;

            edges.Add(edge);
        }

        /// <summary>
        /// 基本呼び出し禁止. 接続辺削除
        /// </summary>
        /// <param name="edge"></param>
        public void RemoveEdge(REdge edge)
        {
            edges.Remove(edge);
        }

        /// <summary>
        /// 自分自身を外す.
        /// removeEdge = trueの時は自分を持っている辺も削除する
        /// </summary>
        public void DisConnect(bool removeEdge = false)
        {
            if (removeEdge)
            {
                // 自分を持っている辺を削除する
                foreach (var e in Edges.ToList())
                    e.DisConnect();
            }
            else
            {
                // 自分を持っている辺から自分を削除する
                foreach (var e in Edges.ToList())
                    e.RemoveVertex(this);
            }

        }

        /// <summary>
        /// 自分自身を解除するうえで, 今まであったつながりは残すようにする
        /// </summary>
        public void DisConnectWithKeepLink()
        {
            var neighbors = GetNeighborVertices().ToList();

            // 自分と繋がっている辺は一旦削除
            foreach (var e in Edges.ToList())
                e.DisConnect();

            // 貼りなおす
            for (var i = 0; i < neighbors.Count; i++)
            {
                var v0 = neighbors[i];
                if (v0 == null)
                    continue;
                for (var j = i; j < neighbors.Count; ++j)
                {
                    var v1 = neighbors[j];
                    if (v1 == null)
                        continue;
                    if (v0.IsNeighbor(v1))
                        continue;
                    // 新しい辺を作成する
                    var _ = new REdge(v0, v1);
                }
            }
        }

        /// <summary>
        /// 隣接頂点を取得
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RVertex> GetNeighborVertices()
        {
            foreach (var edge in Edges)
            {
                if (edge.V0 == this)
                {
                    Assert.IsTrue(edge.V1 != this);
                    yield return edge.V1;
                }
                else
                {
                    Assert.IsTrue(edge.V0 != this);
                    yield return edge.V0;
                }
            }
        }

        /// <summary>
        /// otherとの直接の辺を持っているか
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsNeighbor(RVertex other)
        {
            return Edges.Any(e => e.Vertices.Contains(other));
        }


        /// <summary>
        /// 自身をdstにマージする
        /// </summary>
        public void MergeTo(RVertex dst, bool checkEdgeMerge = true)
        {
            if (this == dst)
            {
                Debug.LogWarning("Merge self");
                return;
            }
            // srcに繋がっている辺に変更を通知する
            var tmpEdges = Edges.ToList();
            foreach (var e in tmpEdges)
            {
                e.ChangeVertex(this, dst);
            }
            // 自分の接続は解除する
            DisConnect();
            if (checkEdgeMerge == false)
                return;
            // 同じ頂点を持っている辺もマージする
            var queue = dst.Edges.ToList();
            while (queue.Any())
            {
                var edge = queue[0];
                queue.RemoveAt(0);
                for (var i = 0; i < queue.Count;)
                {
                    if (edge.IsSameVertex(queue[i]))
                    {
                        queue[i].MergeTo(edge);
                        queue.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }
    }


    /// <summary>
    /// 辺
    /// </summary>
    [Serializable]
    public class REdge
        : ARnParts<REdge>
    {
        public enum VertexType
        {
            V0,
            V1,
        }

        //----------------------------------
        // start: フィールド
        //----------------------------------

        /// <summary>
        /// 接続面
        /// </summary>
        private HashSet<RFace> faces = new HashSet<RFace>();

        /// <summary>
        /// 構成頂点(2個)
        /// </summary>
        [SerializeField]
        private RVertex[] vertices = new RVertex[2];

        //----------------------------------
        // end: フィールド
        //----------------------------------

        /// <summary>
        /// 開始点
        /// </summary>
        public RVertex V0 => GetVertex(VertexType.V0);

        /// <summary>
        /// 終了点
        /// </summary>
        public RVertex V1 => GetVertex(VertexType.V1);

        /// <summary>
        /// 接続面
        /// </summary>
        public IReadOnlyCollection<RFace> Faces => faces;

        /// <summary>
        /// 構成頂点(2個)
        /// </summary>
        public IReadOnlyList<RVertex> Vertices => vertices;

        /// <summary>
        /// 有効な辺かどうか. 2つの頂点が存在して、かつ異なるかどうか
        /// </summary>
        public bool IsValid => V0 != null && V1 != null && V0 != V1;

        public REdge(RVertex v0, RVertex v1)
        {
            SetVertex(VertexType.V0, v0);
            SetVertex(VertexType.V1, v1);
        }

        /// <summary>
        /// 隣接しているEdgeを取得
        /// </summary>
        /// <returns></returns>
        public IEnumerable<REdge> GetNeighborEdges()
        {
            // 同じeが複数回返ることは無いはず
            // 返る場合V0とV1が同じ辺を共有しているがそれはthis以外は存在無いはずなので
            // (全く同じ辺は２つは無い前提)
            if (V0 != null)
            {
                foreach (var e in V0.Edges)
                {
                    if (e != this)
                        yield return e;
                }
            }

            if (V1 != null && V1 != V0)
            {
                foreach (var e in V1.Edges)
                {
                    if (e != this)
                        yield return e;
                }
            }
        }

        /// <summary>
        /// 頂点ノード取得
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public RVertex GetVertex(VertexType type)
        {
            return vertices[(int)type];
        }

        /// <summary>
        /// 頂点ノードを差し替え
        /// </summary>
        /// <param name="type"></param>
        /// <param name="vertex"></param>
        public void SetVertex(VertexType type, RVertex vertex)
        {
            var old = vertices[(int)type];
            if (old == vertex)
                return;

            old?.RemoveEdge(this);
            vertices[(int)type] = vertex;
            vertex?.AddEdge(this);
        }

        /// <summary>
        /// 頂点from -> toに変更する
        /// fromを持っていない場合は無視
        /// 変更した結果両方ともtoになる場合は接続が解除される
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void ChangeVertex(RVertex from, RVertex to)
        {
            if (V0 == from)
            {
                // 両方ともtoになる場合は接続が解除される
                if (V1 == to)
                    DisConnect();
                else
                    SetVertex(VertexType.V0, to);
            }

            if (V1 == from)
            {
                // 両方ともtoになる場合は接続が解除される
                if (V0 == to)
                    DisConnect();
                else
                    SetVertex(VertexType.V1, to);
            }
        }

        /// <summary>
        /// 基本呼び出し禁止. 隣接面追加
        /// </summary>
        /// <param name="face"></param>
        public void AddFace(RFace face)
        {
            if (faces.Contains(face))
                return;
            faces.Add(face);
        }

        /// <summary>
        /// 基本呼び出し禁止. 面のつながりを消す(親のFaceからのみ呼び出す)
        /// </summary>
        /// <param name="face"></param>
        public void RemoveFace(RFace face)
        {
            faces.Remove(face);
        }

        /// <summary>
        /// 頂点を削除する
        /// </summary>
        /// <param name="vertex"></param>
        public void RemoveVertex(RVertex vertex)
        {
            if (V0 == vertex)
                SetVertex(VertexType.V0, null);
            if (V1 == vertex)
                SetVertex(VertexType.V1, null);
        }

        /// <summary>
        /// 自分の接続を解除する
        /// </summary>
        public void DisConnect()
        {
            // 子に自分の接続を解除するように伝える
            foreach (var v in vertices.ToList())
            {
                v?.RemoveEdge(this);
            }

            // 親に自分の接続を解除するように伝える
            foreach (var p in faces.ToList())
            {
                p?.RemoveEdge(this);
            }

            faces.Clear();
            Array.Fill(vertices, null);
        }

        /// <summary>
        /// vで2つに分割する, 元のedgeはV0->v, 新しいEdgeはv->V1になる. 新しいEdgeを返す
        /// </summary>
        /// <param name="v"></param>
        public REdge SplitEdge(RVertex v)
        {
            var lastV1 = V1;
            SetVertex(VertexType.V1, v);
            var newEdge = new REdge(v, lastV1);
            foreach (var p in Faces)
            {
                p.AddEdge(newEdge);
            }

            return newEdge;
        }

        /// <summary>
        /// 同じ頂点を参照しているかどうか. (順序は問わない)
        /// </summary>
        /// <param name="v0"></param>
        /// <param name="v1"></param>
        /// <returns></returns>
        public bool IsSameVertex(RVertex v0, RVertex v1)
        {
            return (V0 == v0 && V1 == v1) || (V0 == v1 && V1 == v0);
        }

        public bool IsSameVertex(REdge other)
        {
            return IsSameVertex(other.V0, other.V1);
        }

        /// <summary>
        /// otherと共有している頂点があるかどうか
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsShareAnyVertex(REdge other)
        {
            return IsShareAnyVertex(other, out _);
        }

        /// <summary>
        /// otherと共有している頂点があるかどうか
        /// </summary>
        /// <param name="other"></param>
        /// <param name="sharedVertex"></param>
        /// <returns></returns>
        public bool IsShareAnyVertex(REdge other, out RVertex sharedVertex)
        {
            if (V0 == other.V0 || V1 == other.V0)
            {
                sharedVertex = other.V0;
                return true;
            }

            if (V0 == other.V1 || V1 == other.V1)
            {
                sharedVertex = other.V1;
                return true;
            }

            sharedVertex = null;
            return false;
        }

        /// <summary>
        /// vertexを含むかどうか
        /// </summary>
        /// <param name="vertex"></param>
        /// <returns></returns>
        public bool Contains(RVertex vertex)
        {
            return V0 == vertex || V1 == vertex;
        }

        /// <summary>
        /// vertexと反対側の頂点を取得する. vertexが含まれていない場合はnullを返す
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="opposite"></param>
        /// <returns></returns>
        public bool TryGetOppositeVertex(RVertex vertex, out RVertex opposite)
        {
            if (V0 == vertex)
            {
                opposite = V1;
                return true;
            }

            if (V1 == vertex)
            {
                opposite = V0;
                return true;
            }

            opposite = null;
            return false;
        }

        /// <summary>
        /// vertexと反対側の頂点を取得する. vertexが含まれていない場合はnullを返す
        /// </summary>
        /// <param name="vertex"></param>
        /// <returns></returns>
        public RVertex GetOppositeVertex(RVertex vertex)
        {
            if (TryGetOppositeVertex(vertex, out var opposite))
                return opposite;
            return null;
        }

        /// <summary>
        /// edgeと共通する頂点がある場合, 反対側の頂点を返す.
        /// 共有しない場合はnullを返す
        /// edgeが同じ辺の場合はnullを返す
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public RVertex GetOppositeVertex(REdge edge)
        {
            if (edge == null)
                return null;

            if (IsSameVertex(edge.V0, edge.V1))
                return null;

            if (IsShareAnyVertex(edge, out var sharedVertex))
                return GetOppositeVertex(sharedVertex);
            return null;
        }

        /// <summary>
        /// 自身をdstにマージする
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="checkFaceMerge"></param>
        public void MergeTo(REdge dst, bool checkFaceMerge = true)
        {
            var tmpFaces = Faces.ToList();
            foreach (var face in tmpFaces)
            {
                face.ChangeEdge(this, dst);
            }

            // 最後に自分の接続は解除する
            DisConnect();

            if (checkFaceMerge == false)
                return;
            var queue = dst.Faces.ToList();
            while (queue.Any())
            {
                var poly = queue[0];
                queue.RemoveAt(0);
                for (var i = 0; i < queue.Count;)
                {
                    if (poly.IsSameEdges(queue[i]))
                    {
                        // 辺は全て同じなので高速化のため移動処理は行わない
                        queue[i].TryMergeTo(poly);
                        queue.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 辺の集合
    /// </summary>
    [Serializable]
    public class RFace : ARnParts<RFace>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------
        /// <summary>
        /// 表示非表示
        /// </summary>
        [field: SerializeField]
        public bool Visible { get; set; } = true;

        /// <summary>
        /// 対応するCityObjectGroup
        /// </summary>
        [SerializeField]
        private PLATEAUCityObjectGroup cityObjectGroup = null;

        /// <summary>
        /// 道路タイプ
        /// </summary>
        [field: SerializeField]
        public RRoadTypeMask RoadTypes { get; set; }

        /// <summary>
        /// LodLevel
        /// </summary>
        [field: SerializeField]
        public int LodLevel { get; set; }

        /// <summary>
        /// 親グラフ
        /// </summary>
        private RGraph graph = null;

        /// <summary>
        /// 構成辺
        /// </summary>
        private HashSet<REdge> edges = new HashSet<REdge>();

        //----------------------------------
        // end: フィールド
        //----------------------------------

        /// <summary>
        /// 構成辺
        /// </summary>
        public IReadOnlyCollection<REdge> Edges => edges;

        /// <summary>
        /// 接続面
        /// </summary>
        public RGraph Graph => graph;

        /// <summary>
        /// 関連するCityObjectGroup
        /// </summary>
        public PLATEAUCityObjectGroup CityObjectGroup => cityObjectGroup;

        // 有効なポリゴンかどうか
        public bool IsValid => edges.Count > 0;

        public RFace(RGraph graph, PLATEAUCityObjectGroup cityObjectGroup, RRoadTypeMask roadType, int lodLevel)
        {
            RoadTypes = roadType;
            LodLevel = lodLevel;
            this.cityObjectGroup = cityObjectGroup;
            this.graph = graph;
        }

        /// <summary>
        /// 辺追加
        /// </summary>
        /// <param name="edge"></param>
        public void AddEdge(REdge edge)
        {
            if (edges.Contains(edge))
                return;

            edges.Add(edge);
            edge.AddFace(this);
        }

        /// <summary>
        /// 親グラフ削除
        /// </summary>
        /// <param name="g"></param>
        public void RemoveGraph(RGraph g)
        {
            if (Graph == g)
                graph = null;
        }

        /// <summary>
        /// 親グラフ差し替え
        /// </summary>
        /// <param name="g"></param>
        public void SetGraph(RGraph g)
        {
            if (graph == g)
                return;

            graph?.RemoveFace(this);
            graph = g;
        }

        ///// <summary>
        ///// 基本呼ぶの禁止. edgeをposの後ろに追加する
        ///// </summary>
        ///// <param name="edge"></param>
        ///// <param name="pos"></param>
        //public void InsertEdge(REdge edge, REdge pos)
        //{
        //    if (edges.Contains(edge))
        //        return;
        //    var index = edges.IndexOf(pos);
        //    edges.Insert(index + 1, edge);
        //    edge.AddFace(this);
        //}

        /// <summary>
        /// 辺削除
        /// </summary>
        /// <param name="edge"></param>
        public void RemoveEdge(REdge edge)
        {
            edges.Remove(edge);
            // 子から自分を削除
            edge.RemoveFace(this);
        }

        /// <summary>
        /// 辺の変更
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void ChangeEdge(REdge from, REdge to)
        {
            // fromを含んでいない場合は無視する
            if (edges.Contains(from) == false)
                return;
            RemoveEdge(from);
            AddEdge(to);
        }

        /// <summary>
        /// 同じEdgeで構成されているかどうか
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsSameEdges(RFace other)
        {
            if (edges.Count != other.edges.Count)
                return false;
            return other.Edges.All(e => Edges.Contains(e));
        }

        /// <summary>
        /// 基本呼び出し禁止. 自身をdstにマージする.
        /// CityObjectGroupが異なる場合はマージできない.
        /// moveEdge = falseの時は自身のEdgesは移動しない.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="moveEdge"></param>
        public bool TryMergeTo(RFace dst)
        {
            if (dst.CityObjectGroup && CityObjectGroup && dst.CityObjectGroup != CityObjectGroup)
            {
                Debug.LogWarning($"CityObjectGroupが異なるFaceは統合できません. {CityObjectGroup} != {dst.CityObjectGroup}");
                return false;
            }

            dst.RoadTypes |= RoadTypes;
            dst.cityObjectGroup = CityObjectGroup;
            dst.LodLevel = Mathf.Max(dst.LodLevel, LodLevel);

            foreach (var e in Edges)
                dst.AddEdge(e);
            DisConnect();
            return true;
        }


        /// <summary>
        /// 自分の接続を解除する
        /// </summary>
        public void DisConnect()
        {
            // 子に自分の接続を解除するように伝える
            foreach (var e in Edges)
                e.RemoveFace(this);

            // 親に自分の接続を解除するように伝える
            Graph?.RemoveFace(this);

            edges.Clear();
        }
    }

    [Serializable]
    public class RGraph : ARnParts<RGraph>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------
        private HashSet<RFace> faces = new HashSet<RFace>();

        //----------------------------------
        // end: フィールド
        //----------------------------------
        /// <summary>
        /// 面
        /// </summary>
        public IReadOnlyCollection<RFace> Faces => faces;

        /// <summary>
        /// 全Edgeを取得(重い)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<REdge> GetAllEdges()
        {
            return Faces.SelectMany(p => p.Edges).Distinct();
        }

        /// <summary>
        /// 全Vertexを取得(重い)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RVertex> GetAllVertices()
        {
            return GetAllEdges().SelectMany(e => e.Vertices).Distinct();
        }

        /// <summary>
        /// 親Face追加
        /// </summary>
        /// <param name="face"></param>
        public void AddFace(RFace face)
        {
            if (face == null)
                return;
            if (faces.Contains(face))
                return;
            faces.Add(face);
            face.SetGraph(this);
        }

        /// <summary>
        /// 親Face削除
        /// </summary>
        /// <param name="face"></param>
        public void RemoveFace(RFace face)
        {
            faces.Remove(face);
            face?.RemoveGraph(this);
        }
    }

    public class RFaceGroup
    {
        public RGraph Graph { get; }

        public PLATEAUCityObjectGroup CityObjectGroup { get; }

        public HashSet<RFace> Faces { get; } = new HashSet<RFace>();

        public RRoadTypeMask RoadTypes
        {
            get
            {
                return Faces.Aggregate((RRoadTypeMask)0, (a, f) => a | f.RoadTypes);
            }
        }

        public RFaceGroup(RGraph graph, PLATEAUCityObjectGroup cityObjectGroup, IEnumerable<RFace> faces)
        {
            Graph = graph;
            CityObjectGroup = cityObjectGroup;
            foreach (var face in faces)
                Faces.Add(face);
        }
    }

    public static class RVertexEx
    {
        /// <summary>
        /// faceSelectorで指定したRFaceだけのRRoadTypeを統合して取得
        /// </summary>
        /// <param name="self"></param>
        /// <param name="faceSelector"></param>
        /// <returns></returns>
        public static RRoadTypeMask GetRoadType(this RVertex self, Func<RFace, bool> faceSelector)
        {
            faceSelector ??= _ => true;
            RRoadTypeMask roadType = RRoadTypeMask.Empty;
            foreach (var face in self.GetFaces().Where(faceSelector))
            {
                roadType |= face.RoadTypes;
            }
            return roadType;
        }

        /// <summary>
        /// faceSelectorで指定したRFaceだけのLodLevelの最大値を取得
        /// </summary>
        /// <param name="self"></param>
        /// <param name="faceSelector"></param>
        /// <returns></returns>
        public static int GetMaxLodLevel(this RVertex self, Func<RFace, bool> faceSelector = null)
        {
            faceSelector ??= _ => true;
            var lodLevel = 0;
            foreach (var face in self.GetFaces().Where(faceSelector))
            {
                lodLevel = Mathf.Max(lodLevel, face.LodLevel);
            }

            return lodLevel;
        }


    }
}
