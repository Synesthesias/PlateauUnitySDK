using PLATEAU.RoadNetwork.Graph;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.CityObject.Drawer
{
    [Serializable]
    public class PLATEAUSubDividedCityObjectDrawerDebug : MonoBehaviour
    {
        // --------------------
        // start:フィールド
        // --------------------
        public bool visible = false;
        // シーン上で選択中のCityObjectGroupのみ表示する
        public bool onlySelectedCityObjectGroupVisible = true;
        public int meshColorNum = 16;
        public bool showVertexIndex = false;
        public bool showOutline = false;
        public int showVertexIndexFontSize = 8;

        public RRoadTypeMask showRRoadTypeMask = RRoadTypeMask.All;
        public HashSet<SubDividedCityObject> TargetCityObjects { get; } = new();
        // --------------------
        // end:フィールド
        // --------------------


        internal void DrawMesh(SubDividedCityObject.Mesh mesh, SubDividedCityObject.SubMesh subMesh, Matrix4x4 mat, Color? color = null,
            float duration = 0f, bool depthTest = true)
        {
            for (var j = 0; j < subMesh.Triangles.Count; j += 3)
            {
                var v0 = mesh.Vertices[subMesh.Triangles[j]];
                var v1 = mesh.Vertices[subMesh.Triangles[j + 1]];
                var v2 = mesh.Vertices[subMesh.Triangles[j + 2]];

                var v = new[]
                {
                    mat * v0.Xyza(1f),
                    mat * v1.Xyza(1f),
                    mat * v2.Xyza(1f)
                }.Select(x => new Vector3(x.x, x.y, x.z));
                DebugEx.DrawLines(v, true, color, duration, depthTest);
            }
        }

        public void DrawCityObjects(List<SubDividedCityObject> cityObjects)
        {
            if (!visible)
                return;
            var index = 0;
            foreach (var item in cityObjects)
            {

                var coVisible =
                    item.Visible &&
                    (!TargetCityObjects.Any() || TargetCityObjects.Contains(item))
                    && (onlySelectedCityObjectGroupVisible == false || RnEx.GetSceneSelectedCityObjectGroups().Any(cog => item.CityObjectGroup == cog))
                    && ((item.GetRoadType(true) & showRRoadTypeMask) != 0)
                    ;

                if (coVisible == false)
                {
                    // インデックスは進めておかないとvisible切り替わるたびに色代わるの辛い
                    index += item.Meshes.Sum(m => m.SubMeshes.Count);
                    continue;
                }

                foreach (var mesh in item.Meshes)
                {

                    if (showOutline)
                    {
                        foreach (var subMesh in mesh.SubMeshes)
                        {
                            var polyIndices = subMesh.CreateOutlineIndices();
                            foreach (var indices in polyIndices)
                            {
                                for (var i = 0; i < indices.Count; i++)
                                {
                                    var v0 = mesh.Vertices[indices[i]];
                                    var v1 = mesh.Vertices[indices[(i + 1) % indices.Count]];

                                    DebugEx.DrawLine(v0, v1, color: DebugEx.GetDebugColor(index, meshColorNum),
                                        duration: 0f, depthTest: true);
                                }
                                index++;
                            }
                        }

                        continue;
                    }

                    if (showVertexIndex)
                    {
                        //foreach (var v in mesh.Vertices)
                        for (var i = 0; i < mesh.Vertices.Count; ++i)
                        {
                            var v = mesh.Vertices[i];
                            DebugEx.DrawString($"{i}", v.PutY(v.y + i * 0.01f), fontSize: showVertexIndexFontSize);
                        }
                    }

                    foreach (var subMesh in mesh.SubMeshes)
                    {

                        var mat = item.CityObjectGroup.transform.localToWorldMatrix;
                        DrawMesh(mesh, subMesh, mat, color: DebugEx.GetDebugColor(index, meshColorNum));
                        index++;
                    }
                }
            }
        }

        public void OnDrawGizmos()
        {
            if (visible == false)
                return;

            var target = GetComponent<PLATEAUSubDividedCityObjectGroup>();
            if (!target)
                return;
            DrawCityObjects(target.CityObjects ?? new List<SubDividedCityObject>());
        }
    }
}