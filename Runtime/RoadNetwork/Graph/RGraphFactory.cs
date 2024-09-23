using PLATEAU.RoadNetwork.CityObject;
using System;
using System.Collections.Generic;
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
        public float mergeCellSize = 0.5f;
        public int mergeCellLength = 4;
        public float removeMidPointTolerance = 0.3f;
        public bool useOutlineOnly = false;
        // --------------------
        // end:フィールド
        // --------------------

        public RGraph CreateGraph(List<SubDividedCityObject> cityObjects)
        {
            var graph = RGraphEx.Create(cityObjects);

            if (useOutlineOnly)
            {
                graph.EdgeReduction();
                graph.RemoveInnerVertex();
            }

            if (reductionOnCreate)
                Reduction(graph);
            return graph;
        }

        public void Reduction(RGraph graph)
        {
            graph.Optimize(mergeCellSize, mergeCellLength, removeMidPointTolerance);
        }
    }
}