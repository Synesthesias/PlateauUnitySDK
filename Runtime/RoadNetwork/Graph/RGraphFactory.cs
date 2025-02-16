using PLATEAU.RoadNetwork.CityObject;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphs;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Graph
{
    [Serializable]
    public class RGraphFactory
    {
        // --------------------
        // start:フィールド
        // --------------------
        public bool reductionOnCreate = true;
        public float mergeCellSize = 0.2f;
        public int mergeCellLength = 2;
        public float removeMidPointTolerance = 0.3f;
        // LOD1モデルは高さ情報がないため、高さの許容誤差を設定する
        public float lod1HeightTolerance = 1.5f;
        public bool useCityObjectOutline = true;

        [Header("Optimize")]

        public bool adjustSmallLodHeight = true;

        public bool edgeReduction = true;

        public bool vertexReduction = true;

        public bool insertVertexInNearEdge = true;

        public bool removeIsolatedEdgeFromFace = true;

        public bool separateFace = true;

        public bool modifySideWalkShape = true;
        // --------------------
        // end:フィールド
        // --------------------

        public RGraph CreateGraph(List<SubDividedCityObject> cityObjects)
        {
            var tmp = cityObjects.Where(x => x.CityObjectGroup != null)
                .Select(x => (x, x.CityObjectGroup.transform.localToWorldMatrix)).ToList();
            return CreateGraph(tmp);
        }

        public RGraph CreateGraph(List<(SubDividedCityObject cityObjects, Matrix4x4 mat)> cityObjects)
        {
            var graph = RGraphEx.Create(cityObjects, useCityObjectOutline);

            if (reductionOnCreate)
            {
                Optimize(graph);
            }

            return graph;
        }

        void Optimize(RGraph self)
        {
            if (adjustSmallLodHeight)
            {
                self.AdjustSmallLodHeight(mergeCellSize, mergeCellLength, lod1HeightTolerance);
            }

            if (edgeReduction)
            {
                self.EdgeReduction();
            }

            if (vertexReduction)
            {
                self.VertexReduction(mergeCellSize, mergeCellLength, removeMidPointTolerance);
            }

            if (removeIsolatedEdgeFromFace)
            {
                self.RemoveIsolatedEdgeFromFace();
            }

            if (edgeReduction)
            {
                self.EdgeReduction();
            }

            if (insertVertexInNearEdge)
            {
                self.InsertVertexInNearEdge(removeMidPointTolerance);
            }

            if (edgeReduction)
            {
                self.EdgeReduction();
            }

            if (separateFace)
            {
                self.SeparateFaces();
            }

            // 非連結部分を削除した結果, 孤立した辺ができる可能性もあるのでここでも実行
            if (removeIsolatedEdgeFromFace)
            {
                self.RemoveIsolatedEdgeFromFace();
            }

            if (modifySideWalkShape)
            {
                self.ModifySideWalkShape();
            }
        }
    }
}