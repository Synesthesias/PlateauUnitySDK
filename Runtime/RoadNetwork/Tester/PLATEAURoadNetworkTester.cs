using PLATEAU.CityGML;
using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Drawer;
using PLATEAU.RoadNetwork.Factory;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using static UnityEngine.GraphicsBuffer;

namespace PLATEAU.RoadNetwork
{
    public class PLATEAURoadNetworkTester : MonoBehaviour
    {
        public List<PLATEAUCityObjectGroup> targets = new List<PLATEAUCityObjectGroup>();

        public List<PLATEAUCityObjectGroup> tmp = new List<PLATEAUCityObjectGroup>();


        [SerializeField] private bool targetAll = false;

        [SerializeField]
        private RoadNetworkDrawerDebug drawer = new RoadNetworkDrawerDebug();

        [SerializeField]
        public List<PLATEAUCityObjectGroup> geoTestTargets = new List<PLATEAUCityObjectGroup>();

        [Serializable]
        public class TestTargetPresets
        {
            public string name;
            public List<PLATEAUCityObjectGroup> targets = new List<PLATEAUCityObjectGroup>();
        }

        public List<TestTargetPresets> savedTargets = new List<TestTargetPresets>();

        public string newTargetName = "";

        [SerializeField] private bool showGeoTest = false;

        [SerializeField] private RoadNetworkFactory factory = new RoadNetworkFactory();

        public RoadNetworkModel roadNetwork = null;

        public void OnDrawGizmos()
        {
            drawer.Draw(roadNetwork);

            if (showGeoTest)
            {
                var vertices = geoTestTargets
                    .Select(x => x.GetComponent<MeshCollider>())
                    .Where(x => x)
                    .SelectMany(x => x.sharedMesh.vertices.Select(a => a.Xz()))
                    .ToList();
                var convex = GeoGraph2d.ComputeConvexVolume(vertices);
                DebugUtil.DrawArrows(convex.Select(x => x.Xay()));
            }
        }

        public void Draw(PLATEAUCityObjectGroup cityObjectGroup)
        {
            var collider = cityObjectGroup.GetComponent<MeshCollider>();
            var cMesh = collider.sharedMesh;
            var isClockwise = GeoGraph2d.IsClockwise(cMesh.vertices.Select(v => new Vector2(v.x, v.y)));
            if (isClockwise)
            {
                DebugUtil.DrawArrows(cMesh.vertices.Select(v => v + Vector3.up * 0.2f));
            }
            else
            {
                DebugUtil.DrawArrows(cMesh.vertices.Reverse().Select(v => v + Vector3.up * 0.2f));
            }
        }

        public void CreateNetwork()
        {
            var factory = new RoadNetworkFactory();

            if (targetAll)
            {
                var allTargets = GameObject.FindObjectsOfType<PLATEAUCityObjectGroup>()
                    .Where(c => c.CityObjects.rootCityObjects.Any(a => a.CityObjectType == CityObjectType.COT_Road))
                    .ToList();

                roadNetwork = factory.CreateNetwork(allTargets);
            }
            else
            {
                // 重複は排除する
                targets = targets.Distinct().ToList();
                roadNetwork = factory.CreateNetwork(targets);
            }
        }
    }
}