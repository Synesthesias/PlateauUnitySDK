﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using PLATEAU.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PLATEAU.Tests.TestUtils
{
    public static class SceneUtil
    {
        public static void DestroyAllGameObjectsInActiveScene()
        {
            var rootObjs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var obj in rootObjs)
            {
                Object.DestroyImmediate(obj);
            }
        }

        

        /// <summary>
        /// EditModeテストの実行時にUnityが自動的に立ち上げる、デフォルトのテストシーンを返します。
        /// </summary>
        public static Scene GetEditModeTestScene()
        {
            int numOpenScene = SceneManager.sceneCount;
            Scene testScene = default;
            bool found = false;
            for (int i = 0; i < numOpenScene; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                // Editモードのシーンは無名であると仮定します。
                if (scene.name == "")
                {
                    testScene = scene;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new Exception("Test Scene is not found.");
            }

            return testScene;
        }


        public static List<GameObject> GetRootObjectsOfEditModeTestScene()
        {
            return GameObjectUtil.ListGameObjsInScene(GetEditModeTestScene());
        }
    }
}