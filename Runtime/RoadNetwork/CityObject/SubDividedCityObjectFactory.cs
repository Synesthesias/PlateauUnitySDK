using PLATEAU.CityAdjust.NonLibData;
using PLATEAU.CityAdjust.NonLibDataHolder;
using PLATEAU.CityExport.ModelConvert.SubMeshConvert;
using PLATEAU.CityImport.Import.Convert;
using PLATEAU.CityInfo;
using PLATEAU.GranularityConvert;
using PLATEAU.Native;
using PLATEAU.PolygonMesh;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using CityObjectList = PLATEAU.PolygonMesh.CityObjectList;
using Mesh = UnityEngine.Mesh;

namespace PLATEAU.RoadNetwork.CityObject
{
    public class SubDividedCityObjectFactory
    {
        /*
         * UnityMeshToDllModelConverterからコピペして作成している
         */
        public class CityObjectInfo
        {
            public Transform Transform { get; set; }

            public Mesh UnityMesh { get; set; }

            public PLATEAUCityObjectGroup CityObjectGroup { get; set; }

            public Mesh OriginalMesh { get; set; }

            public static CityObjectInfo Create(PLATEAUCityObjectGroup cog, bool useContourMesh)
            {
                var mesh = cog.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                    return null;
                if (mesh.triangles.Length % 3 != 0)
                {
                    Debug.LogWarning("Meshの三角形の数が3の倍数ではありません");
                    return null;
                }

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var submesh = mesh.GetSubMesh(i);
                    if (submesh.indexCount % 3 != 0)
                    {
                        Debug.LogWarning("SubMeshの三角形の数が3の倍数ではありません");
                        return null;
                    }
                }
                var originalMesh = mesh;
                if (useContourMesh)
                {
                    var contourMesh = cog.GetComponent<PLATEAUContourMesh>();
                    if (contourMesh)
                    {
                        var m = contourMesh.contourMesh;
                        mesh = new UnityEngine.Mesh
                        {
                            vertices = m.vertices,
                            triangles = m.triangles,
                            uv4 = m.uv4,
                            uv = new Vector2[m.uv4.Length],
                            colors = new Color[m.uv4.Length]
                        };
                    }
                }
                return new CityObjectInfo
                {
                    Transform = cog.transform,
                    UnityMesh = mesh,
                    OriginalMesh = originalMesh,
                    CityObjectGroup = cog
                };
            }

        }

        /// <summary>
        /// 引数で与えられたゲームオブジェクトとその子(再帰的)を <see cref="Model"/> に変換して返します。
        /// </summary>
        /// <param name="srcTransforms">変換対象ゲームオブジェクトのルートです。</param>
        /// <param name="unityMeshToDllSubMeshConverter"></param>
        /// <param name="exportDisabledGameObj">false のとき、非Activeのものは対象外とします。</param>
        /// <param name="vertexConverter">頂点座標を変換するメソッドで、 Vector3 から PlateauVector3d に変換する方法を指定します。</param>
        /// /// <param name="invertMesh">true : Mesh Normalを反転します</param>
        public static Model Convert(List<CityObjectInfo> srcTransforms, IUnityMeshToDllSubMeshConverter unityMeshToDllSubMeshConverter,
            bool exportDisabledGameObj, AdderAndCoordinateSystemConverter vertexConverter, bool invertMesh = false)
        {
            var model = Model.Create();
            foreach (var trans in srcTransforms)
            {
                ConvertRecursive(null, trans, model, unityMeshToDllSubMeshConverter, exportDisabledGameObj, vertexConverter, invertMesh);
            }
            return model;
        }

        /// <summary>
        /// Transform とその子を再帰的に <see cref="Node"/> に変換します。
        /// Unityのゲームオブエジェクトのヒエラルキーと <see cref="Node"/> の木構造が対応するようにします。
        /// </summary>
        /// <param name="parentNode"> <paramref name="trans"/> の親 Transform に対応する親 Node です。親がない（ルート）のときは null にします。</param>
        /// <param name="trans">このゲームオブジェクトとその子を再帰的に <see cref="Node"/> にします。</param>
        /// <param name="model"> <paramref name="parentNode"/> が null のとき、<see cref="Node"/> は <paramref name="model"/> に追加されます。</param>
        /// <param name="unityMeshToDllSubMeshConverter"></param>
        /// <param name="exportDisabledGameObj">false のとき、ActiveでないGameObjectは対象から除外します。</param>
        /// <param name="vertexConverter"></param>
        /// <param name="invertMesh">true : Mesh Normalを反転します</param>
        private static void ConvertRecursive(Node parentNode, CityObjectInfo trans, Model model, IUnityMeshToDllSubMeshConverter unityMeshToDllSubMeshConverter,
            bool exportDisabledGameObj, AdderAndCoordinateSystemConverter vertexConverter, bool invertMesh)
        {
            bool activeInHierarchy = trans.Transform.gameObject.activeInHierarchy;
            if ((!activeInHierarchy) && (!exportDisabledGameObj)) return;

            // メッシュを変換して Node を作ります。
            var node = GameObjToNode(trans, unityMeshToDllSubMeshConverter, vertexConverter, invertMesh);

            if (parentNode == null)
            {
                // ルートのときは Model に Node を追加します。
                model.AddNodeByCppMove(node);
                node = model.GetRootNodeAt(model.RootNodesCount - 1);

            }
            else
            {
                // ルートでないときは Node に Node を追加します。
                parentNode.AddChildNodeByCppMove(node);
                node = parentNode.GetChildAt(parentNode.ChildCount - 1);
            }

            node.IsActive = activeInHierarchy;
        }

        private static Node GameObjToNode(CityObjectInfo trans, IUnityMeshToDllSubMeshConverter unityMeshToDllSubMeshConverter,
            AdderAndCoordinateSystemConverter vertexConverter, bool invertMesh)
        {
            // ノード生成します。
            var node = Node.Create(trans.Transform.name);
            node.IsActive = trans.Transform.gameObject.activeInHierarchy;
            var localPos = vertexConverter.Convert(trans.Transform.localPosition).ToPlateauVector();
            node.LocalPosition = localPos;

            // スケールの座標変換の考え方
            // 例えば、UnityでLocalScaleが(2,3,4)だったとする。これをWUNに変換するとき、XYZの値を入れ替えて(2,4,3)としたい。
            // そこで単純に座標系変換処理をかけると(-2,4,3)となり、マイナスが余計である。
            // 余計なマイナスを取り除くために、(1,1,1)を同じ座標系に変換して(-1,1,1)とし、それでアダマール積をとることで座標変換のマイナスを除く。
            node.LocalScale = vertexConverter.ConvertOnlyCoordinateSystem(trans.Transform.localScale).ToPlateauVector() *
                              vertexConverter.ConvertOnlyCoordinateSystem(Vector3.one).ToPlateauVector();

            var srcEuler = trans.Transform.localRotation.eulerAngles;
            var convertedEuler = vertexConverter.ConvertOnlyCoordinateSystem(srcEuler);
            if (invertMesh)
            {
                // メッシュが裏返るような座標軸に変換する場合、Rotationを反転します。
                convertedEuler *= -1;
            }
            node.LocalRotation = Quaternion.Euler(convertedEuler).ToPlateauQuaternion();


            PolygonMesh.Mesh nativeMesh = null;
            // メッシュを変換します。
            nativeMesh = ConvertMesh(
                trans.UnityMesh, trans.CityObjectGroup, unityMeshToDllSubMeshConverter,
                vertexConverter, invertMesh);

            int subMeshCount = trans.UnityMesh.subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
            {
                var subMesh = trans.UnityMesh.GetSubMesh(i);
                if (subMesh.indexCount == 0) continue;
                int startId = subMesh.indexStart;
                int endId = startId + subMesh.indexCount - 1;
                Assert.IsTrue(startId < endId);
                Assert.IsTrue(endId < nativeMesh.IndicesCount);
            }


            // CityObjectListを作って渡します。
            if (trans.CityObjectGroup != null && nativeMesh != null)
            {
                AttachCityObjectGroupToNativeMesh(trans.CityObjectGroup, nativeMesh);
            }

            if (nativeMesh != null)
            {
                node.SetMeshByCppMove(nativeMesh);
            }

            return node;
        }

        /// <summary>
        /// <see cref="PLATEAUCityObjectGroup"/>を<see cref="CityObjectList"/>に変換し、それをnativeMeshに追加します。
        /// </summary>
        private static void AttachCityObjectGroupToNativeMesh(PLATEAUCityObjectGroup cityObjGroup, PolygonMesh.Mesh nativeMesh)
        {
            using var cityObjList = PolygonMesh.CityObjectList.Create();
            var allCityObjs = cityObjGroup.GetAllCityObjects();
            foreach (var cityObj in allCityObjs)
            {
                int primaryID = cityObj.CityObjectIndex[0];
                int atomicID = cityObj.CityObjectIndex[1];
                cityObjList.Add(new CityObjectIndex(primaryID, atomicID), cityObj.GmlID);
            }

            nativeMesh.CityObjectList = cityObjList;
        }

        private static PolygonMesh.Mesh ConvertMesh(Mesh unityMesh, PLATEAUCityObjectGroup cog, IUnityMeshToDllSubMeshConverter unityMeshToDllSubMeshConverter, VertexConverterBase vertexConverter, bool invertMesh)
        {
            // 頂点
            var vertCount = unityMesh.vertexCount;
            var unityVertices = unityMesh.vertices;
            var plateauVertices = new PlateauVector3d[vertCount];
            for (int i = 0; i < vertCount; i++)
            {
                plateauVertices[i] = vertexConverter.Convert(unityVertices[i]).ToPlateauVector();
            }

            // indices
            var unityIndices = unityMesh.triangles;
            var indexCount = unityIndices.Length;
            var plateauIndices = new uint[indexCount];
            for (int i = 0; i < indexCount; i++)
            {
                plateauIndices[i] = (uint)unityIndices[i];
            }

            // uv1
            var unityUv1 = unityMesh.uv;
            var uv1Count = unityUv1.Length;
            var plateauUv1 = new PlateauVector2f[uv1Count];
            for (int i = 0; i < uv1Count; i++)
            {
                var u = unityUv1[i];
                plateauUv1[i] = new PlateauVector2f(u.x, u.y);
            }

            // uv4
            var unityUv4 = unityMesh.uv4;
            var uv4Count = unityUv4.Length;
            var plateauUv4 = new PlateauVector2f[uv4Count];
            for (int i = 0; i < uv4Count; i++)
            {
                var u = unityUv4[i];
                plateauUv4[i] = new PlateauVector2f(u.x, u.y);
            }

            // vertexColor
            var unityColors = unityMesh.colors;
            var vertColorCount = unityColors.Length;
            var vertexColors = new PlateauVector3d[vertColorCount];
            for (int i = 0; i < vertColorCount; i++)
            {
                var color = unityColors[i];
                vertexColors[i] = new PlateauVector3d(color.r, color.g, color.b);
            }


            if (invertMesh)
            {
                for (int i = 0; i < plateauIndices.Length; i += 3)
                {
                    var first = plateauIndices[i];
                    var third = plateauIndices[i + 2];
                    plateauIndices[i] = third;
                    plateauIndices[i + 2] = first;
                }
            }

            var dllSubMeshes = unityMeshToDllSubMeshConverter.Convert(unityMesh, null);
            Assert.AreEqual(plateauUv1.Length, plateauVertices.Length);

            var dllMesh = PolygonMesh.Mesh.Create(
                plateauVertices, plateauIndices, plateauUv1, plateauUv4,
                dllSubMeshes.ToArray());
            // 補足:
            // 上の行で MergeMeshInfo に渡す実引数 includeTexture が常に true になっていますが、それで良いです。
            // 上の処理で テクスチャを含める/含めない の設定に即した SubMesh がすでにできているので、
            // C++側で特別にテクスチャを除く処理は不必要だからです。

            dllMesh.SetVertexColors(vertexColors);


            // CityObjectListを追加します
            if (cog != null)
            {
                using var col = CityObjectList.Create();
                foreach (var co in cog.GetAllCityObjects())
                {
                    var objId = co.CityObjectIndex;
                    col.Add(new CityObjectIndex(objId[0], objId[1]), co.GmlID);
                }

                dllMesh.CityObjectList = col;
            }

            return dllMesh;
        }

        public static bool TryGetHeight(Vector2 v, Mesh mesh, out float height)
        {
            height = 0f;
            var v3 = new Vector3[3];
            var v2 = new Vector2[3];
            var d = new Vector2[3];
            var e = new Vector2[3];
            var cross = new float[3];
            // TODO : 全探索しているので効率悪い
            for (var index = 0; index < mesh.triangles.Length; index += 3)
            {
                for (var x = 0; x < 3; x++)
                {
                    v3[x] = mesh.vertices[mesh.triangles[index + x]];
                    v2[x] = v3[x].Xz();
                }

                // 辺のベクトル
                for (var x = 0; x < 3; ++x)
                {
                    d[x] = v2[(x + 1) % 3] - v2[x];
                    e[x] = v2[x] - v;

                    if (e[x].sqrMagnitude < 0.01f)
                    {
                        height = v3[x].y;
                        return true;
                    }

                    cross[x] = Vector2Ex.Cross(e[x], d[x]);
                }

                // 三角形の内部にある場合
                if (cross.All(c => c >= 0) || cross.All(c => c <= 0))
                {
                    var n = Vector3.Cross(v3[1] - v3[0], v3[2] - v3[1]).normalized;
                    var a = v.Xay() - v3[0];
                    var nd = Vector3.Dot(n, a);
                    var dd = a - nd * n;
                    height = (v3[0] + dd).y;
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        internal class ConvertCityObjectResult
        {
            public List<SubDividedCityObject> ConvertedCityObjects { get; } = new List<SubDividedCityObject>();
        }


        /// <summary>
        /// cityObjectGroupsをSubDividedCityObjectに変換する
        /// </summary>
        /// <param name="cityObjectGroups"></param>
        /// <param name="useContourMesh"></param>
        /// <returns></returns>
        internal static ConvertCityObjectResult ConvertCityObjects(IEnumerable<PLATEAUCityObjectGroup> cityObjectGroups, bool useContourMesh = true)
        {
            using var progressBar = new ProgressDisplayDialogue();
            using var _ = new DebugTimer("ConvertCityObjects");
            // NOTE : CityGranularityConverterを参考
            var cityInfos = new List<CityObjectInfo>();
            foreach (var cityObjectGroup in cityObjectGroups)
            {
                var info = CityObjectInfo.Create(cityObjectGroup, useContourMesh);
                if (info != null)
                {
                    cityInfos.Add(info);
                }
            }
            var nativeOption = new GranularityConvertOption(ConvertGranularity.PerAtomicFeatureObject, 1);

            var transformList = new UniqueParentTransformList(cityInfos.Select(c => c.Transform).ToArray());

            // 属性情報を記憶しておく
            var attributes = new GmlIdToSerializedCityObj();
            attributes.ComposeFrom(transformList);

            var unityMeshToDllSubMeshConverter = new UnityMeshToDllSubMeshWithEmptyMaterial();

            // ゲームオブジェクトを共通ライブラリのModelに変換します。
            using var srcModel = Convert(
                cityInfos,
                unityMeshToDllSubMeshConverter,
                true, // 非表示のゲームオブジェクトも対象に含めます。なぜなら、LOD0とLOD1のうちLOD1だけがActiveになっているという状況で、変換後もToolkitsのLOD機能を使えるようにするためです。
                VertexConverterFactory.NoopConverter());

            // 共通ライブラリの機能でモデルを分割・結合します。
            var converter = new GranularityConverter();
            var dstModel = converter.Convert(srcModel, nativeOption);
            var getter = new SerializedCityObjectGetterFromDict(attributes, dstModel);
            var attrHelper = new AttributeDataHelper(getter, true);
            // var cco = await Task.Run(() => new SubDividedCityObject(dstModel, attrHelper));
            var cco = new SubDividedCityObject(dstModel, attrHelper);

            var ret = new ConvertCityObjectResult();


            // 高速化の為, 最初にテーブルを作っておく
            Dictionary<string, SubDividedCityObject> nameToSubDividedCityObject = new Dictionary<string, SubDividedCityObject>();
            foreach (var child in cco.GetAllChildren())
            {
                if (child == null)
                    continue;
                //// 最小の単位だけ使うので親ポリゴンは無視
                //if (child.Children.Any())
                //    continue;
                //// メッシュないものも無視
                //if (child.Meshes.Any() == false)
                //    continue;
                nameToSubDividedCityObject.TryAdd(child.Name, child);
            }

            for (var i = 0; i < cityInfos.Count; ++i)
            {
                var co = cityInfos[i];
                if (co.Transform == null)
                {
                    Debug.Log("skipping deleted game object.");
                    continue;
                }
                progressBar.SetProgress("最小地物分解", 100f * i / cityInfos.Count, "オブジェクト分解中");
                var ccoChild = nameToSubDividedCityObject.GetValueOrDefault(co.Transform.name);
                if (ccoChild == null)
                    continue;
                ccoChild.SetCityObjectGroup(co.CityObjectGroup);
                if (co.OriginalMesh != co.UnityMesh)
                {
                    try
                    {
                        void Visit(SubDividedCityObject sdvco)
                        {
                            foreach (var m in sdvco.Meshes)
                            {
                                for (var i = 0; i < m.Vertices.Count; i++)
                                {
                                    var v = m.Vertices[i];
                                    if (TryGetHeight(v.Xz(), co.OriginalMesh, out var height))
                                    {
                                        v.y = height;
                                    }
                                    else
                                    {
                                        DebugEx.LogError($"Failed to get height / {ccoChild.CityObjectGroup.name}");
                                    }

                                    m.Vertices[i] = v;
                                }
                            }

                            foreach (var c in sdvco.Children)
                            {
                                Visit(c);
                            }
                        }
                        Visit(ccoChild);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            ret.ConvertedCityObjects.AddRange(cco.GetAllChildren().Where(c => c.Children.Any() == false && c.Meshes.Any()));

            //foreach (var c in ret.ConvertedCityObjects)
            for (var i = 0; i < ret.ConvertedCityObjects.Count; ++i)
            {
                var c = ret.ConvertedCityObjects[i];
                progressBar.SetProgress("最小地物分解", 100f * i / ret.ConvertedCityObjects.Count, "頂点チェック中");
                // 全く同じ頂点を結合する
                foreach (var m in c.Meshes)
                    m.VertexReduction();
            }

            return ret;
        }


    }
}