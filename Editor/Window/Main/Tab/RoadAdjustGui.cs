﻿using PLATEAU.Editor.Window.Common;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PLATEAU.Editor.Window.Main.Tab
{
    public class RoadAdjustGui : ITabContent
    {
        
        public VisualElement CreateGui()
        {
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    $"Packages/{PathUtil.packageFormalName}/Resources/PlateauUIDocument/RoadNetwork/RoadNetwork_Main.uxml"
                );
            if (visualTree == null)
            {
                Debug.LogError("Failed to load gui.");
            }

            var container = visualTree.CloneTree();
            InitRoadNetworkMain(container);
            return container;
        }
        
        
        public void Dispose()
        {
            
        }

        public void OnTabUnselect()
        {
        }

        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        private bool InitRoadNetworkMain(TemplateContainer container)
        {
            // Main uxmlの確認
            var main = container.Q<VisualElement>("RoadNetwork_Main");
            if (main == null)
            {
                Debug.LogError("Failed InitRoadNetworkMain()");
                return false;
            }

            // 仮　不要な要素を非表示にする
            var s = main.Q<UnityEngine.UIElements.ScrollView>();
            if (s != null)
                s.style.display = DisplayStyle.None;

            // 各radioButtonの取得
            var menuGroup = main.Q<VisualElement>("MenuGroup");
            Func<string, RadioButton> r = (string n) => { return menuGroup.Q<RadioButton>(n); };
            string[] radioButtonNames = 
                { "MenuGenerate", "MenuEdit", "MenuAdd", "MenuTrafficRule" };
            var radioButtons = new Dictionary<string, RadioButton>(radioButtonNames.Length);
            for (var i = 0; i< radioButtonNames.Length; i++)
                radioButtons[radioButtonNames[i]] = r(radioButtonNames[i]);
            foreach (var item in radioButtons)
            {
                if (item.Value == null)
                {
                    Debug.LogError("Failed InitRoadNetworkMain()");
                    return false;
                }
            }

            // radioButtonの初期化
            radioButtons["MenuGenerate"].RegisterCallback<ChangeEvent<bool>>(e =>
            {
                if (e.newValue == false)
                    return;
                SyncTabStatus(radioButtons, container); // Todo 処理負荷が掛かるようなら部分更新に修正する(trueになったら生成、falseになったら破棄)
            });

            radioButtons["MenuEdit"].RegisterCallback<ChangeEvent<bool>>(e =>
            {
                if (e.newValue == false)
                    return;
                SyncTabStatus(radioButtons, container);
            });

            radioButtons["MenuAdd"].RegisterCallback<ChangeEvent<bool>>(e =>
            {
                if (e.newValue == false)
                    return;
                SyncTabStatus(radioButtons, container);
            });

            radioButtons["MenuTrafficRule"].RegisterCallback<ChangeEvent<bool>>(e =>
            {
                if (e.newValue == false)
                    return;
                SyncTabStatus(radioButtons, container);
            });

            // タブの状態に同期
            SyncTabStatus(radioButtons, container);

            void SyncTabStatus(IReadOnlyDictionary<string, RadioButton> radioButtons, VisualElement root)
            {
                foreach (var item in radioButtons)
                {
                    var key = item.Key;
                    var val = item.Value;
                    switch (key)
                    {
                        case "MenuGenerate":
                            SyncTabInstance<RoadGuiParts.RoadGenerate>(root, val);
                            break;

                        case "MenuEdit":
                            SyncTabInstance<RoadGuiParts.RoadEditPanel>(root, val);
                            break;

                        case "MenuAdd":
                            SyncTabInstance<RoadGuiParts.RoadAddPanel>(root, val);
                            break;

                        case "MenuTrafficRule":
                            SyncTabInstance<RoadGuiParts.RoadTrafficRulePanel>(root, val);
                            break;

                        default:
                            break;
                    }
                }

                static void SyncTabInstance<_Type>(VisualElement root, RadioButton val)
                    where _Type : RoadGuiParts.RoadAdjustGuiPartBase, new()
                {
                    var gui = val.userData as RoadGuiParts.RoadAdjustGuiPartBase;
                    if (gui == null)
                    {
                        gui = new _Type();
                        val.userData = gui;
                    }
                    if (val.value)
                        gui.Init(root);
                    else
                        gui.Terminate(root);
                }
            }
            return true;
        }
    }
}