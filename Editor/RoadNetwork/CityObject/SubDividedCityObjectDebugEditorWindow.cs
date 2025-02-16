using PLATEAU.RoadNetwork.CityObject;
using PLATEAU.RoadNetwork.Graph;
using PLATEAU.RoadNetwork.Util;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PLATEAU.Editor.RoadNetwork.CityObject
{
    public class SubDividedCityObjectDebugEditorWindow : EditorWindow
    {
        public interface IInstanceHelper
        {
            // グラフ取得
            PLATEAUSubDividedCityObjectGroup GetCityObjects();

            bool IsTarget(SubDividedCityObject cityObject);

            HashSet<SubDividedCityObject> TargetCityObjects { get; }

            // モデル作成する
            // void CreateRnModel();

            //void CreateTranMesh();
        }

        private const string WindowName = "SubDividedCityObject Editor";

        public IInstanceHelper InstanceHelper { get; set; }

        private HashSet<object> Foldouts { get; } = new();
        private RRoadTypeMask filterMask = RRoadTypeMask.All;

        public void EditSubDividedCityObject(SubDividedCityObject e)
        {
            if (e == null)
                return;
            RnEditorUtil.TargetToggle($"{e.Name}", InstanceHelper.TargetCityObjects, e);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.EnumFlagsField("SelfRoadType", e.SelfRoadType);
                EditorGUILayout.EnumFlagsField("ParentRoadType", e.ParentRoadType);
                foreach (var root in e.CityObjects.rootCityObjects)
                {
                    RnEditorUtil.Separator();
                    EditorGUILayout.LabelField(root.GmlID);
                    EditorGUILayout.EnumFlagsField("Type", root.GetRoadType());
                }

                if (RnEditorUtil.Foldout("Json", Foldouts, (e, "Json")))
                {
                    EditorGUILayout.TextArea(e.SerializedCityObjects);
                }
            }

            RnEditorUtil.Separator();
            EditorGUILayout.LabelField("Children");
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var child in e.Children)
                {
                    if (RnEditorUtil.Foldout(child.Name, Foldouts, child))
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            using var _ = new EditorGUI.IndentLevelScope();
                            EditSubDividedCityObject(child);
                        }
                    }
                }
            }

        }

        /// <Summary>
        /// ウィンドウのパーツを表示します。
        /// </Summary>
        private void OnGUI()
        {
            if (InstanceHelper == null)
                return;

            var cityObjects = InstanceHelper.GetCityObjects();
            if (!cityObjects || !(cityObjects.CityObjects?.Any() ?? false))
            {
                return;
            }

            if (GUILayout.Button("Clear Selected"))
                InstanceHelper.TargetCityObjects.Clear();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("CityObjects", cityObjects.CityObjects.Count);
            }

            filterMask = (RRoadTypeMask)EditorGUILayout.EnumPopup("Filter", filterMask);
            RnEditorUtil.Separator();
            foreach (var cog in cityObjects.CityObjects)
            {
                if ((filterMask & cog.SelfRoadType) == 0)
                    continue;
                if (InstanceHelper.IsTarget(cog) || InstanceHelper.TargetCityObjects.Contains(cog))
                {
                    if (RnEditorUtil.Foldout(cog.Name, Foldouts, cog))
                    {
                        using var _ = new EditorGUI.IndentLevelScope();
                        EditSubDividedCityObject(cog);
                    }
                }
            }
        }


        /// <summary>
        /// ウィンドウを取得する、存在しない場合に生成する
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="focus"></param>
        /// <returns></returns>
        public static SubDividedCityObjectDebugEditorWindow OpenWindow(IInstanceHelper instance, bool focus)
        {
            var ret = GetWindow<SubDividedCityObjectDebugEditorWindow>(WindowName, focus);
            ret.InstanceHelper = instance;
            return ret;
        }

    }
}