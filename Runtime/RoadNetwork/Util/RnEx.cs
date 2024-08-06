﻿using PLATEAU.CityExport.ModelConvert;
using PLATEAU.CityExport.ModelConvert.SubMeshConvert;
using PLATEAU.CityImport.Import.Convert;
using PLATEAU.CityInfo;
using PLATEAU.GranularityConvert;
using PLATEAU.PolygonMesh;
using PLATEAU.RoadNetwork.Mesh;
using PLATEAU.RoadNetwork.Structure;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Util
{
    public struct CombSet2<T>
    {
        public T A { get; }

        public T B { get; }

        public CombSet2(T a, T b)
        {
            A = a;
            B = b;
            if (A.GetHashCode() > B.GetHashCode())
                (A, B) = (B, A);
        }
    }

    public struct CombSet3<T>
    {
        public T A { get; }

        public T B { get; }

        public T C { get; }

        public CombSet3(T a, T b, T c)
        {
            A = a;
            B = b;
            C = c;
            if (A.GetHashCode() > B.GetHashCode())
                (A, B) = (B, A);
            if (B.GetHashCode() > C.GetHashCode())
                (B, C) = (C, B);
            if (A.GetHashCode() > B.GetHashCode())
                (A, B) = (B, A);
        }
    }

    public static class RnEx
    {
        public static CombSet2<T> CombSet<T>(T a, T b)
        {
            return new CombSet2<T>(a, b);
        }

        public static CombSet3<T> CombSet<T>(T a, T b, T c)
        {
            return new CombSet3<T>(a, b, c);
        }

        public static void Replace<T>(IList<T> self, T before, T after) where T : class
        {
            for (var i = 0; i < self.Count; i++)
            {
                if (self[i] == before)
                    self[i] = after;
            }
        }

        public static void ReplaceLane(IList<RnLane> self, RnLane before, RnLane after)
        {
            Replace(self, before, after);
        }

        /// <summary>
        /// ModelのRootNodeを返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        private static IEnumerable<Node> GetRootNodes(this Model self)
        {
            for (var i = 0; i < self.RootNodesCount; ++i)
                yield return self.GetRootNodeAt(i);
        }

        /// <summary>
        /// targetsの頂点をマージする
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="mergeEpsilon"></param>
        /// <param name="mergeCellLength"></param>
        /// <returns></returns>
        public static List<ConvertedCityObject> MergeVertices(List<ConvertedCityObject> targets, float mergeEpsilon, int mergeCellLength)
        {
            try
            {
                var ret = targets.Select(c => c.DeepCopy()).ToList();
                var vertexTable = GeoGraphEx.MergeVertices(
                    ret.SelectMany(c => c.Meshes.SelectMany(m => m.Vertices)),
                    mergeEpsilon, mergeCellLength);
                foreach (var m in ret.SelectMany(c => c.Meshes))
                {
                    m.Merge(vertexTable);
                }

                return ret;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new List<ConvertedCityObject>();
            }
        }

        [Serializable]
        internal class ConvertCityObjectResult
        {
            public List<ConvertedCityObject> ConvertedCityObjects { get; } = new List<ConvertedCityObject>();
        }


        internal static async Task<ConvertCityObjectResult> ConvertCityObjectsAsync(IEnumerable<PLATEAUCityObjectGroup> cityObjectGroups, float epsilon = 0.1f)
        {
            // NOTE : CityGranularityConverterを参考
            var cityObjectGroupList = cityObjectGroups.ToList();
            var nativeOption = new GranularityConvertOption(MeshGranularity.PerAtomicFeatureObject, 1);
            var transformList = new UniqueParentTransformList(cityObjectGroupList.Select(c => c.transform).ToArray());

            // 属性情報を記憶しておく
            var attributes = GmlIdToSerializedCityObj.ComposeFrom(transformList);

            var unityMeshToDllSubMeshConverter = new UnityMeshToDllSubMeshWithGameMaterial();

            // ゲームオブジェクトを共通ライブラリのModelに変換します。
            using var srcModel = UnityMeshToDllModelConverter.Convert(
                transformList,
                unityMeshToDllSubMeshConverter,
                true, // 非表示のゲームオブジェクトも対象に含めます。なぜなら、LOD0とLOD1のうちLOD1だけがActiveになっているという状況で、変換後もToolkitsのLOD機能を使えるようにするためです。
                VertexConverterFactory.NoopConverter());

            // 共通ライブラリの機能でモデルを分割・結合します。
            var converter = new GranularityConverter();
            var dstModel = converter.Convert(srcModel, nativeOption);
            var getter = new SerializedCityObjectGetterFromDict(attributes, dstModel);
            var attrHelper = new AttributeDataHelper(getter, nativeOption.Granularity, true);
            var cco = await Task.Run(() => new ConvertedCityObject(dstModel, attrHelper));

            foreach (var co in cityObjectGroupList)
            {
                var ccoChild = cco.GetAllChildren().FirstOrDefault(c => c.Name == co.name);
                if (ccoChild != null)
                {
                    ccoChild.SetCityObjectGroup(co);
                }
            }

            var ret = new ConvertCityObjectResult();
            ret.ConvertedCityObjects.AddRange(cco.GetAllChildren().Where(c => c.Children.Any() == false && c.Meshes.Any()));

            return ret;
        }
    }
}
