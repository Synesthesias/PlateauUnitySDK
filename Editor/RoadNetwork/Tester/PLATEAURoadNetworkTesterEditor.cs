using PLATEAU.Editor.RoadNetwork.CityObject;
using PLATEAU.RoadNetwork.CityObject;
using PLATEAU.RoadNetwork.CityObject.Drawer;
using PLATEAU.RoadNetwork.Factory;
using PLATEAU.RoadNetwork.Structure;
using PLATEAU.RoadNetwork.Tester;
using PLATEAU.RoadNetwork.Util;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace PLATEAU.Editor.RoadNetwork.Tester
{
    [CustomEditor(typeof(PLATEAURoadNetworkTester))]
    public class PLATEAURoadNetworkTesterEditor : UnityEditor.Editor
    {

        public override void OnInspectorGUI()
        {
            var obj = target as PLATEAURoadNetworkTester;
            if (!obj)
                return;


            base.OnInspectorGUI();
            if (GUILayout.Button("Create"))
            {
                obj.CreateNetwork();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var model = obj.GetComponent<PLATEAURnStructureModel>();
                if (model && model.RoadNetwork != null)
                {
                    if (GUILayout.Button("Create Empty Road"))
                        model.RoadNetwork.CreateEmptyRoadBetweenInteraction();

                    if (GUILayout.Button("Remove Empty Road"))
                        model.RoadNetwork.RemoveEmptyRoadBetweenIntersection();
                }
                if (model && model.RoadNetwork != null)
                {
                    if (GUILayout.Button("Create Empty Intersection"))
                        model.RoadNetwork.CreateEmptyIntersectionBetweenRoad();

                    if (GUILayout.Button("Remove Empty Intersection"))
                        model.RoadNetwork.RemoveEmptyIntersectionBetweenRoad();
                }
            }
            if (GUILayout.Button("Check Lod"))
                obj.RemoveSameNameCityObjectGroup();
        }
    }
}