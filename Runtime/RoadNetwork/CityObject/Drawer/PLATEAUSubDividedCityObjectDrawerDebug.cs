using PLATEAU.RoadNetwork.Graph;
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
        public int meshColorNum = 16;
        public bool showVertexIndex = false;
        public int showVertexIndexFontSize = 8;

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
                if (item.Visible == false || (TargetCityObjects.Any() && TargetCityObjects.Contains(item) == false))
                {
                    // インデックスは進めておかないとvisible切り替わるたびに色代わるの辛い
                    index += item.Meshes.Sum(m => m.SubMeshes.Count);
                    continue;
                }

                foreach (var mesh in item.Meshes)
                {
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