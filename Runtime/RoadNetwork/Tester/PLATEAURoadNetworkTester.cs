﻿using PLATEAU.CityGML;
using PLATEAU.RoadAdjust.RoadNetworkToMesh;
using PLATEAU.RoadNetwork.Factory;
using PLATEAU.RoadNetwork.Structure;
using PLATEAU.RoadNetwork.Structure.Drawer;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using PLATEAUCityObjectGroup = PLATEAU.CityInfo.PLATEAUCityObjectGroup;

namespace PLATEAU.RoadNetwork.Tester
{
    [Serializable]
    [RequireComponent(typeof(PLATEAURnModelDrawerDebug))]
    public class PLATEAURoadNetworkTester : MonoBehaviour
    {
        // --------------------
        // start:フィールド
        // --------------------
        [field: SerializeField]
        public RoadNetworkFactory Factory { get; set; } = new RoadNetworkFactory();

        // シーンに配置している全てのPLATEAUCityObjectGroupを対象にするか
        [field: SerializeField]
        private bool TargetAll { get; set; } = true;

        // 道路ネットワーク作成用のPLATEAUCityObjectGroupのプリセットテーブル
        [field: SerializeField]
        public List<TestTargetPresets> TargetPresets { get; set; } = new();

        // 今回作成するPLATEAUCityObjectGroupのプリセットテーブル
        [field: SerializeField]
        public string TargetPresetName { get; set; } = "";

        // --------------------
        // end:フィールド
        // --------------------

        [Serializable]
        public class TestTargetPresets
        {
            public string name;
            public List<PLATEAUCityObjectGroup> targets = new List<PLATEAUCityObjectGroup>();
        }

        public List<PLATEAUCityObjectGroup> GetTargetCityObjects()
        {
            var ret = TargetAll
                ? (IList<PLATEAUCityObjectGroup>)GameObject.FindObjectsOfType<PLATEAUCityObjectGroup>()
                    .Where(cog => !IsGeneratedRoad(cog.transform)) // 自動生成メッシュは除外します
                    .ToArray()
                : TargetPresets
                    .FirstOrDefault(s => s.name == TargetPresetName)
                    ?.targets;
            if (ret == null)
                return new List<PLATEAUCityObjectGroup>();

            return ret
                .Where(c => c.transform.childCount == 0)
                .Where(c => c.CityObjects.rootCityObjects.Any(a => a.CityObjectType == CityObjectType.COT_Road))
                .Distinct()
                .ToList();
        }

        private bool IsGeneratedRoad(Transform tran)
        {
            return tran.GetComponent<PLATEAUReproducedRoad>() != null;
        }

        /// <summary>
        /// 道路ネットワークを作成する
        /// </summary>
        /// <returns></returns>
        public async Task<RnModel> CreateNetwork()
        {
            var go = gameObject;
            var targets = GetTargetCityObjects();
            var req = Factory.CreateRequest(targets, go);
            var model = await Factory.CreateRnModelAsync(req);
            return model;
        }

        /// <summary>
        /// 同名のCityObjectGroupがあった場合に最大のLODのもの以外を非表示にする
        /// </summary>
        public void RemoveSameNameCityObjectGroup()
        {
            var groups = GameObject.FindObjectsOfType<PLATEAUCityObjectGroup>();

            foreach (var g in groups.GroupBy(g => g.gameObject.name)
                         .Where(g => g.Count() > 1))
            {
                var level = g.Select(a => a.GetLodLevel()).Max();
                g.TryFindMax(a => a.GetLodLevel(), out var maxG);

                foreach (var a in g)
                {
                    if (a != maxG)
                    {
                        a.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}