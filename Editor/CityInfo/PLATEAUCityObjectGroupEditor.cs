﻿using System.Text;
using PLATEAU.CityInfo;
using PLATEAU.Editor.EditorWindow.Common;
using UnityEditor;
using UnityEngine;

namespace PLATEAU
{
    [CustomEditor(typeof(PLATEAUCityObjectGroup))]
    public class PLATEAUCityObjectGroupEditor : UnityEditor.Editor
    {
        Vector2 scroll;

        public void OnEnable()
        {
            UnityEditorInternal.ComponentUtility.MoveComponentUp(target as UnityEngine.Component);
        }

        public override void OnInspectorGUI()
        {
            var cog = target as PLATEAUCityObjectGroup;
            if (cog == null) return;
            SerializedProperty prop = serializedObject.FindProperty("serializedCityObjects");
            var json = prop.stringValue;

            PlateauEditorStyle.Heading("属性情報", null);
            using (PlateauEditorStyle.VerticalScopeLevel1())
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(400));
                EditorGUILayout.TextArea(json);
                EditorGUILayout.EndScrollView();
            }
           
            using (new EditorGUI.DisabledScope(true))
            {
                base.OnInspectorGUI();
            }
        }

        void OnSceneGUI()
        {
            #region Raycastテスト
            //今のところあくまでテストで、今後はクリック箇所の属性情報を表示する専用のモードが実装される予定です

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                if (Physics.Raycast(ray, out var hit, 100000.0f))
                {
                    if(hit.transform.TryGetComponent<PLATEAUCityObjectGroup>(out var cog))
                    {
                        var obj = cog.GetCityObject(hit);
                        if (obj == null)
                        {
                            Debug.Log("no matching CityObject.");
                            return;
                        }
                        var logAttribute = new StringBuilder();
                        foreach (var o in obj.cityObjects)
                        {
                            foreach (var kv in o.AttributesMap)
                            {
                                logAttribute.Append($"attribute: key = {kv.Key}, value = {kv.Value.DebugString()}\n");
                            }
                        }
                        Debug.Log(logAttribute);
                    }
                }
                else
                    Debug.Log("no hit");
            }

            #endregion Raycastテスト
        }
    }
}
