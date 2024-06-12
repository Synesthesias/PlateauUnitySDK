﻿using PLATEAU.RoadNetwork;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PLATEAU.Editor.RoadNetwork
{
    [CustomEditor(typeof(PLATEAURoadNetworkTester))]
    public class PLATEAURoadNetworkTesterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var cog = target as PLATEAURoadNetworkTester;
            if (!cog)
                return;

            base.OnInspectorGUI();
            if (GUILayout.Button("Create"))
                cog.CreateNetwork();

            if (GUILayout.Button("Serialize"))
                cog.RoadNetwork.Serialize();

            if (GUILayout.Button("Deserialize"))
                cog.RoadNetwork.Deserialize();

            if (GUILayout.Button("SplitCityObject"))
                cog.SplitCityObjectAsync();
        }
    }
}