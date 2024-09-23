using Newtonsoft.Json;
using PLATEAU.CityAdjust.NonLibData;
using PLATEAU.CityImport.Import.Convert;
using PLATEAU.CityInfo;
using PLATEAU.PolygonMesh;
using PLATEAU.RoadNetwork.Graph;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
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

            public void Merge(Dictionary<Vector3, Vector3> vertexConvertTable)
            {
                var changed = GeoGraphEx.MergeMeshVertex(
                    Vertices, vertexConvertTable, out var vert, out var indices);
                if (changed == false)
                    return;
                Vertices = vert;
                foreach (var s in SubMeshes)
                {
                    var tri = new HashSet<Vector3Int>();
                    var newTriangles = new List<int>(s.Triangles.Count);
                    for (var i = 0; i < s.Triangles.Count; i += 3)
                    {
                        var v0 = indices[s.Triangles[i]];
                        var v1 = indices[s.Triangles[i + 1]];
                        var v2 = indices[s.Triangles[i + 2]];
                        // 3つの頂点が同じ場合は無視
                        if (v0 == v1 && v1 == v2 && v2 == v0)
                            continue;

                        // 三角形にならない場合は削除
                        // #NOTE : 接続情報が消える可能性があるがいったん許容
                        if (v0 == v1 || v1 == v2 || v2 == v0)
                            continue;
                        var vs = new[] { v0, v1, v2 }.OrderBy(x => x).ToArray();
                        var t = new Vector3Int(vs[0], vs[1], vs[2]);
                        // 同じ頂点の三角形がすでに登録されていたら無視
                        if (tri.Contains(t) == false)
                        {
                            tri.Add(t);
                            newTriangles.Add(v0);
                            newTriangles.Add(v1);
                            newTriangles.Add(v2);
                        }
                    }
                    s.Triangles = newTriangles;
                }
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

        [field: SerializeField]
        public List<Mesh> Meshes { get; set; } = new List<Mesh>();

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

        public SubDividedCityObject(PLATEAUContourMesh contourMesh)
        {
            Name = contourMesh.name;
            CityObjectGroup = contourMesh.GetComponent<PLATEAUCityObjectGroup>();
            Meshes.Add(new Mesh
            {
                Vertices = contourMesh.contourMesh.vertices.ToList(),
                SubMeshes = new List<SubMesh>
                {
                    new SubMesh
                    {
                        Triangles = contourMesh.contourMesh.triangles.ToList()
                    }
                }
            });
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