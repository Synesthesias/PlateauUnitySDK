using PLATEAU.CityGML;
using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Drawer;
using PLATEAU.Util;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace PLATEAU.RoadNetwork
{
    [RequireComponent(typeof(PLATEAURoadNetwork))]
    public class PLATEAURoadNetworkTester : MonoBehaviour
    {
        public List<PLATEAUCityObjectGroup> targets = new List<PLATEAUCityObjectGroup>();

        public List<PLATEAUCityObjectGroup> tmp = new List<PLATEAUCityObjectGroup>();

        private PLATEAURoadNetwork Network => GetComponent<PLATEAURoadNetwork>();

        [SerializeField] private bool targetAll = false;

        [SerializeField]
        private PLATEAURoadNetworkDrawerDebug drawer = new PLATEAURoadNetworkDrawerDebug();

        public void OnDrawGizmos()
        {
            drawer.Draw(Network);
        }

        public void Draw(PLATEAUCityObjectGroup cityObjectGroup)
        {
            var collider = cityObjectGroup.GetComponent<MeshCollider>();
            var cMesh = collider.sharedMesh;
            var isClockwise = PolygonUtil.IsClockwise(cMesh.vertices.Select(v => new Vector2(v.x, v.y)));
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
            if (targetAll)
            {
                var allTargets = GameObject.FindObjectsOfType<PLATEAUCityObjectGroup>()
                    .Where(c => c.CityObjects.rootCityObjects.Any(a => a.CityObjectType == CityObjectType.COT_Road))
                    .ToList();

                Network.CreateNetwork(allTargets);
            }
            else
            {
                // 重複は排除する
                targets = targets.Distinct().ToList();
                Network.CreateNetwork(targets);
            }
        }
    }
}