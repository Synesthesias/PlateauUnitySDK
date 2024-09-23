using PLATEAU.CityAdjust.NonLibData;
using PLATEAU.CityAdjust.NonLibDataHolder;
using PLATEAU.CityExport.ModelConvert;
using PLATEAU.CityExport.ModelConvert.SubMeshConvert;
using PLATEAU.CityImport.Import.Convert;
using PLATEAU.CityInfo;
using PLATEAU.GranularityConvert;
using PLATEAU.PolygonMesh;
using PLATEAU.RoadNetwork.CityObject;
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
        public static List<SubDividedCityObject> MergeVertices(List<SubDividedCityObject> targets, float mergeEpsilon, int mergeCellLength)
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
                return new List<SubDividedCityObject>();
            }
        }

        [Serializable]
        internal class ConvertCityObjectResult
        {
            public List<SubDividedCityObject> ConvertedCityObjects { get; } = new List<SubDividedCityObject>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cityObjectGroups"></param>
        /// <param name="epsilon"></param>
        /// <param name="useContourMesh"></param>
        /// <returns></returns>
        internal static async Task<ConvertCityObjectResult> ConvertCityObjectsAsync(IEnumerable<PLATEAUCityObjectGroup> cityObjectGroups, float epsilon = 0.1f, bool useContourMesh = true)
        {
            // NOTE : CityGranularityConverterを参考
            var cityObjectGroupList = new List<PLATEAUCityObjectGroup>();
            var contourMeshList = new List<PLATEAUContourMesh>();
            foreach (var cityObjectGroup in cityObjectGroups)
            {
                if (useContourMesh)
                {
                    var contourMesh = cityObjectGroup.GetComponent<PLATEAUContourMesh>();
                    if (contourMesh)
                    {
                        contourMeshList.Add(contourMesh);
                    }
                    else
                    {
                        cityObjectGroupList.Add(cityObjectGroup);
                    }

                }
                else
                {
                    cityObjectGroupList.Add(cityObjectGroup);
                }
            }
            var nativeOption = new GranularityConvertOption(MeshGranularity.PerAtomicFeatureObject, 1);

            var transformList = new UniqueParentTransformList(cityObjectGroupList.Select(c => c.transform).ToArray());

            // 属性情報を記憶しておく
            var attributes = new GmlIdToSerializedCityObj();
            attributes.ComposeFrom(transformList);

            var unityMeshToDllSubMeshConverter = new UnityMeshToDllSubMeshWithEmptyMaterial();

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
            var attrHelper = new AttributeDataHelper(getter, true);
            var cco = await Task.Run(() => new SubDividedCityObject(dstModel, attrHelper));

            var ret = new ConvertCityObjectResult();
            foreach (var co in cityObjectGroupList)
            {
                var ccoChild = cco.GetAllChildren().FirstOrDefault(c => c.Name == co.name);
                if (ccoChild != null)
                {
                    ccoChild.SetCityObjectGroup(co);
                }
            }
            ret.ConvertedCityObjects.AddRange(cco.GetAllChildren().Where(c => c.Children.Any() == false && c.Meshes.Any()));

            foreach (var cm in contourMeshList)
            {
                var sco = new SubDividedCityObject(cm);
                ret.ConvertedCityObjects.Add(sco);
            }
            return ret;
        }

        /// <summary>
        /// Sceneエディタ上で選択されているかどうか
        /// </summary>
        /// <param name="cog"></param>
        /// <returns></returns>
        public static bool IsEditorSceneSelected(PLATEAUCityObjectGroup cog)
        {
#if UNITY_EDITOR
            if (!cog)
                return false;
            return UnityEditor.Selection.gameObjects.Contains(cog.gameObject);
#else
            return false;
#endif
        }

        public static RnLineString CreateInnerLerpLineString(IReadOnlyList<Vector3> leftVertices, IReadOnlyList<Vector3> rightVertices, RnPoint start, RnPoint end, RnWay startBorder, RnWay endBorder, float t, float pointSkipDistance = 1e-3f)
        {
            var line = new RnLineString();
            void AddPoint(RnPoint p)
            {
                if (p == null)
                    return;
                line.AddPointOrSkip(p, pointSkipDistance);
            }

            AddPoint(start);
            var segments = GeoGraphEx.GetInnerLerpSegments(leftVertices, rightVertices, AxisPlane.Xz, t);
            // 1つ目の点はボーダーと重複するのでスキップ
            // #TODO : 実際はボーダーよりも外側にあるのはすべてスキップすべき
            foreach (var s in segments.Skip(1))
                AddPoint(new RnPoint(s));
            AddPoint(end);
            // 自己交差があれば削除する
            var plane = AxisPlane.Xz;
            GeoGraph2D.RemoveSelfCrossing(line.Points
                , t => t.Vertex.GetTangent(plane)
                , (p1, p2, p3, p4, inter, f1, f2) => new RnPoint(Vector3.Lerp(p1, p2, f1)));

            return line;
        }
    }
}
