using Newtonsoft.Json;
using PLATEAU.CityImport.Import.Convert;
using PLATEAU.CityInfo;
using PLATEAU.PolygonMesh;
using PLATEAU.RoadNetwork.Graph;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.CityObject
{
    /// <summary>
    /// PLATEAUCityObjectGroupを細分化したもの
    /// </summary>
    [Serializable]
    public class SubDividedCityObject
    {
        [Serializable]
        public class SubMesh
        {
            [field: SerializeField]
            public List<int> Triangles { get; set; } = new List<int>();

            /// <summary>
            /// Trianglesを連結しているサブメッシュに分割する。
            /// Trianglesが連結グラフの場合はそのまま返る
            /// </summary>
            /// <returns></returns>
            public List<SubMesh> Separate()
            {
                Dictionary<int, HashSet<int>> vertexToTriangle = new Dictionary<int, HashSet<int>>();
                for (var i = 0; i < Triangles.Count; i += 3)
                {
                    var t = i / 3;
                    for (var x = 0; x < 3; x++)
                    {
                        vertexToTriangle.GetValueOrCreate(Triangles[i + x]).Add(t);
                    }
                }

                var triNum = Enumerable.Range(0, Triangles.Count / 3).ToList();
                List<SubMesh> subMeshes = new List<SubMesh>();
                while (triNum.Any())
                {
                    var t = triNum[0];
                    var triIndices = new List<int> { t };
                    for (var a = 0; a < triIndices.Count; ++a)
                    {
                        var tt = triIndices[a];
                        triNum.Remove(tt);
                        for (var i = 0; i < 3; i++)
                        {
                            var v = Triangles[tt * 3 + i];
                            foreach (var ttt in vertexToTriangle[v])
                            {
                                if (triNum.Contains(ttt) && triIndices.Contains(ttt) == false)
                                    triIndices.Add(ttt);
                            }
                        }
                    }
                    var subMesh = new SubMesh();
                    subMesh.Triangles.AddRange(
                        triIndices.OrderBy(t => t)
                            .SelectMany(t => Enumerable.Range(0, 3).Select(x => Triangles[t * 3 + x])));
                    subMeshes.Add(subMesh);
                }

                return subMeshes;
            }


            /// <summary>
            /// ポリゴンの外形頂点のインデックス配列を計算
            /// </summary>
            /// <returns></returns>
            public List<List<int>> CreateOutlineIndices()
            {
                var ret = new List<List<int>>();

                // 各辺を三角形に紐づける
                // key : 辺の頂点インデックスのペア, 
                Dictionary<(int i0, int i1), HashSet<int>> edgeToTriangle = new();

                (int i0, int i1) GetEdge(int a, int b) => a > b ? (b, a) : (a, b);

                for (var i = 0; i < Triangles.Count; i += 3)
                {
                    var t = i / 3;
                    for (var x = 0; x < 3; x++)
                    {
                        var a = Triangles[i + x];
                        var b = Triangles[i + (x + 1) % 3];
                        var edge = GetEdge(a, b);
                        edgeToTriangle.GetValueOrCreate(edge).Add(t);
                    }
                }

                // 一つの三角形としか紐づいていない辺 => 外形
                var outlineEdges = edgeToTriangle
                    .Where(kv => kv.Value.Count == 1)
                    .Select(kv => kv.Key)
                    .ToList();

                while (outlineEdges.Any())
                {
                    var edge = outlineEdges[0];
                    outlineEdges.RemoveAt(0);
                    var indices = new List<int> { edge.i0, edge.i1 };
                    while (outlineEdges.Any())
                    {
                        var v0 = indices[0];
                        var lastV = indices[^1];
                        var index = outlineEdges.FindIndex(e => e.i1 == lastV || e.i0 == lastV);
                        if (index < 0)
                            break;

                        var e = outlineEdges[index];
                        outlineEdges.RemoveAt(index);
                        // 1周した
                        if (e.i1 == v0 || e.i0 == v0)
                            break;

                        // 逆側の頂点を追加
                        indices.Add(e.i0 == lastV ? e.i1 : e.i0);
                    }
                    ret.Add(indices);
                }

                return ret;
            }
            /// <summary>
            /// Deep
            /// </summary>
            /// <returns></returns>
            public SubMesh DeepCopy()
            {
                return new SubMesh { Triangles = Triangles.ToList() };
            }
        }

        [Serializable]
        public class Mesh
        {
            [field: SerializeField]
            public List<Vector3> Vertices { get; set; } = new List<Vector3>();

            [field: SerializeField]
            public List<SubMesh> SubMeshes { get; set; } = new List<SubMesh>();

            /// <summary>
            /// 頂点削減. 都市モデルで全てのTriangleが独立(頂点バッファに同じ頂点が複数ある)していて
            /// インデックスバッファサイズ = 頂点バッファサイズとなる用なモデルがあったので
            /// </summary>
            public void VertexReduction()
            {
                // key   : 頂点
                // value : 新しい頂点バッファの配列インデックス
                var vertexMap = new Dictionary<Vector3, int>();
                // 辞書に入れることで重複を削除
                foreach (var v in Vertices)
                {
                    vertexMap.TryAdd(v, vertexMap.Count);
                }

                // 重複した頂点を削除したことにより三角形が崩れるかチェックする
                var removeTriangleNum = 0;
                foreach (var subMesh in SubMeshes)
                {
                    HashSet<(int, int, int)> newTriangleSet = new();
                    for (var j = 0; j < subMesh.Triangles.Count; j += 3)
                    {
                        var i = Enumerable.Range(0, 3).Select(i => vertexMap[Vertices[subMesh.Triangles[j + i]]]).ToList();
                        if (i[0] == i[1] || i[0] == i[2] || i[1] == i[2])
                            continue;

                        // 同じ三角形を削除するためにソート
                        i.Sort();
                        newTriangleSet.Add((i[0], i[1], i[2]));
                    }
                    removeTriangleNum += subMesh.Triangles.Count / 3 - newTriangleSet.Count;
                    subMesh.Triangles = newTriangleSet.SelectMany(t => new[] { t.Item1, t.Item2, t.Item3 }).ToList();
                }

                var newVertices = new List<Vector3>(vertexMap.Count);
                newVertices.AddRange(Enumerable.Repeat(Vector3.zero, vertexMap.Count));
                foreach (var item in vertexMap)
                    newVertices[item.Value] = item.Key;

                // 削除されたときにログを出す
                if (removeTriangleNum > 0)
                    DebugEx.Log($"Vertex size {Vertices.Count} -> {newVertices.Count}, Remove triangle = {removeTriangleNum}");
                Vertices = newVertices;
            }


            public void Separate()
            {
                var newSubMesh = new List<SubMesh>();
                foreach (var m in SubMeshes)
                {
                    newSubMesh.AddRange(m.Separate());
                }
                SubMeshes = newSubMesh;
            }

            public Mesh DeepCopy()
            {
                return new Mesh
                {
                    SubMeshes = SubMeshes.Select(s => s.DeepCopy()).ToList(),
                    Vertices = Vertices.ToList()
                };
            }
        }

        [field: SerializeField]
        public string Name { get; private set; }

        // Inspectorで表示するためにシリアライズしておく
        [TextArea(1, 20)]
        [SerializeField]
        private string serializedCityObjects;

        private CityInfo.CityObjectList cityObjects;

        [field: SerializeField]
        public bool Visible { get; set; } = true;

        /// <summary>
        /// サイズは0 or 1
        /// </summary>
        [field: SerializeField]
        public List<Mesh> Meshes { get; private set; } = new List<Mesh>();

        [field: SerializeField]
        public List<SubDividedCityObject> Children { get; set; } = new List<SubDividedCityObject>();

        [field: SerializeField]
        public PLATEAUCityObjectGroup CityObjectGroup { get; private set; }

        // 自分の道路タイプ
        [field: SerializeField]
        public RRoadTypeMask SelfRoadType { get; private set; } = RRoadTypeMask.Empty;

        // 親の道路タイプ
        [field: SerializeField]
        public RRoadTypeMask ParentRoadType { get; private set; } = RRoadTypeMask.Empty;

        public string SerializedCityObjects => serializedCityObjects;

        public CityInfo.CityObjectList CityObjects
        {
            get
            {
                if (cityObjects != null)
                    return cityObjects;

                if (!string.IsNullOrEmpty(serializedCityObjects))
                    cityObjects = JsonConvert.DeserializeObject<CityInfo.CityObjectList>(serializedCityObjects);

                return cityObjects;
            }
        }

        public RRoadTypeMask GetRoadType(bool containsParent)
        {
            var ret = SelfRoadType;
            if (containsParent)
                ret |= ParentRoadType;
            return ret;
        }

        /// <summary>
        /// C++側の <see cref="PolygonMesh.Model"/> から変換して
        /// <see cref="SubDividedCityObject"/> を作ります。
        /// 子も再帰的に作ります。
        /// </summary>
        internal SubDividedCityObject(Model plateauModel, AttributeDataHelper attributeDataHelper)
        {
            Name = "Root";
            // RootはNodeが無いからnull
            attributeDataHelper.SetCurrentNode(null);
            Children = new List<SubDividedCityObject>();
            for (int i = 0; i < plateauModel.RootNodesCount; i++)
            {
                var rootNode = plateauModel.GetRootNodeAt(i);
                // 再帰的な子の生成です。
                Children.Add(new SubDividedCityObject(rootNode, attributeDataHelper.Copy(), RRoadTypeMask.Empty));
            }
        }

        /// <summary>
        /// C++側の <see cref="PolygonMesh.Node"/> から変換して
        /// <see cref="SubDividedCityObject"/> を作ります。
        /// 子も再帰的に作ります。
        /// </summary>
        private SubDividedCityObject(Node plateauNode, AttributeDataHelper attributeDataHelper, RRoadTypeMask parentTypeMask)
        {
            ParentRoadType = parentTypeMask;
            Name = plateauNode?.Name ?? "";
            attributeDataHelper.SetCurrentNode(plateauNode);
            if (plateauNode != null)
            {
                attributeDataHelper.SetTargetCityObjList(plateauNode.Mesh.CityObjectList);
                if (plateauNode.Mesh != null)
                {
                    var m = plateauNode.Mesh;
                    var vertices = new List<Vector3>(m.VerticesCount);
                    for (int i = 0; i < m.VerticesCount; i++)
                    {
                        var v = m.GetVertexAt(i).ToUnityVector();
                        vertices.Add(v);
                    }

                    var subMeshes = new List<SubMesh>(m.SubMeshCount);
                    for (var i = 0; i < m.SubMeshCount; ++i)
                    {
                        var subMesh = m.GetSubMeshAt(i);
                        var s = new SubMesh();
                        for (var j = subMesh.StartIndex; j <= subMesh.EndIndex; j += 3)
                        {
                            s.Triangles.Add(m.GetIndiceAt(j));
                            s.Triangles.Add(m.GetIndiceAt(j + 1));
                            s.Triangles.Add(m.GetIndiceAt(j + 2));
                        }
                        subMeshes.Add(s);
                    }
                    Meshes.Add(new Mesh
                    {
                        Vertices = vertices,
                        SubMeshes = subMeshes
                    });
                }
            }


            cityObjects = attributeDataHelper.GetSerializableCityObject();
            SelfRoadType = RRoadTypeMask.Empty;
            foreach (var root in cityObjects.rootCityObjects)
            {
                SelfRoadType |= root.GetRoadType();
            }

            for (int i = 0; i < plateauNode.ChildCount; i++)
            {
                var child = plateauNode.GetChildAt(i);
                Children.Add(new SubDividedCityObject(child, attributeDataHelper.Copy(), ParentRoadType | SelfRoadType));
                attributeDataHelper.AddOutsideChildren(child?.Name);
            }
            serializedCityObjects = JsonConvert.SerializeObject(cityObjects, Formatting.Indented);
        }

        public IEnumerable<SubDividedCityObject> GetAllChildren()
        {
            if (Children == null)
                yield break;
            foreach (var c in Children)
            {
                yield return c;
                foreach (var cc in c.GetAllChildren())
                {
                    yield return cc;
                }
            }
        }

        public void SetCityObjectGroup(PLATEAUCityObjectGroup group)
        {
            CityObjectGroup = group;
            foreach (var c in Children)
                c.SetCityObjectGroup(group);
        }

        public SubDividedCityObject DeepCopy()
        {
            var ret = MemberwiseClone() as SubDividedCityObject;
            ret.Meshes = Meshes.Select(m => m.DeepCopy()).ToList();
            ret.Children = Children.Select(m => m.DeepCopy()).ToList();
            return ret;
        }
    }

}