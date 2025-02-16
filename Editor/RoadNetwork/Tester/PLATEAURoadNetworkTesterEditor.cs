using PLATEAU.RoadNetwork.CityObject;
using PLATEAU.RoadNetwork.Graph;
using PLATEAU.RoadNetwork.Structure;
using PLATEAU.RoadNetwork.Tester;
using PLATEAU.Util;
using PLATEAU.Util.Async;
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
        private HashSet<object> foldouts = new();
        public override void OnInspectorGUI()
        {
            var obj = target as PLATEAURoadNetworkTester;
            if (!obj)
                return;

            base.OnInspectorGUI();
            if (GUILayout.Button("Create"))
                obj.CreateNetwork().ContinueWithErrorCatch();

            if (GUILayout.Button("Check Lod"))
                obj.RemoveSameNameCityObjectGroup();

            if (RnEditorUtil.Foldout("Option", foldouts, "Option"))
            {
                var cityObjects = obj.GetComponent<PLATEAUSubDividedCityObjectGroup>();

                var rGraphComp = obj.GetComponent<PLATEAURGraph>();


                if (GUILayout.Button("Create SubDivided City Object"))
                {
                    cityObjects = obj.gameObject.GetOrAddComponent<PLATEAUSubDividedCityObjectGroup>();
                    var subDividedRes =
                        SubDividedCityObjectFactory.ConvertCityObjects(obj.GetTargetCityObjects(), useContourMesh: obj.Factory.UseContourMesh);
                    cityObjects.CityObjects = subDividedRes.ConvertedCityObjects;
                }

                if (cityObjects && cityObjects.CityObjects.Any() && GUILayout.Button("Create RGraph"))
                {
                    rGraphComp = cityObjects.gameObject.GetOrAddComponent<PLATEAURGraph>();

                    var subDividedCityObjects
                        = cityObjects
                            .CityObjects
                            .Where(x => x.CityObjectGroup != null)
                            .Select(x => (x, x.CityObjectGroup.transform.localToWorldMatrix)).ToList();

                    var graphTask = Task.Run(() =>
                    {
                        rGraphComp.Graph = obj.Factory.GraphFactory.CreateGraph(subDividedCityObjects);
                    });
                    graphTask.ContinueWithErrorCatch();
                }


                if (rGraphComp && rGraphComp.Graph != null && GUILayout.Button("Create RModel"))
                {
                    var rnModel = obj.gameObject.GetOrAddComponent<PLATEAURnStructureModel>();

                    rnModel.RoadNetwork = obj.Factory.CreateRnModelAsync(rGraphComp.Graph).Result;
                }
            }


        }
    }
}