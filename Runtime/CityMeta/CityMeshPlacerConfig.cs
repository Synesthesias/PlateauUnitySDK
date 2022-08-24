﻿using PLATEAU.CommonDataStructure;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static PLATEAU.CityMeta.CityMeshPlacerConfig;

namespace PLATEAU.CityMeta
{
    /// <summary>
    /// 3Dモデルをロードして現在のシーンに配置する設定です。
    /// 
    /// 目的は以下の2つです。
    /// ・モデル配置時に ScenePlacementGUI からユーザー選択の設定を受け取り、 CityMeshPlacerToScene に渡すこと
    /// ・インポート時の設定を保存する目的で SerializeField を保持すること。 
    /// </summary>
    [Serializable]
    internal class CityMeshPlacerConfig : ISerializationCallbackReceiver
    {
        private Dictionary<GmlType, ScenePlacementConfigPerType> perTypeConfigs;

        // シリアライズ時に Dictionary を List形式にします。
        [SerializeField] private List<GmlType> keys = new List<GmlType>();
        [SerializeField] private List<ScenePlacementConfigPerType> values = new List<ScenePlacementConfigPerType>();

        public enum PlaceMethod
        {
            /// <summary> 変換したLODをすべてシーンに配置します。 </summary>
            PlaceAllLod,
            /// <summary> 変換したLODのうち最大のものをシーンに配置します。 </summary>
            PlaceMaxLod,
            /// <summary> 選択したLODを配置します。そのLODが見つからなければ配置しません。 </summary>
            PlaceSelectedLodOrDoNotPlace,
            /// <summary> 選択したLODを配置します。そのLODが見つからなければ、見つかる中で最大のLODを配置します。 </summary>
            PlaceSelectedLodOrMax,
            /// <summary> シーンに配置しません。 </summary>
            DoNotPlace
        }

        public static readonly string[] PlaceMethodDisplay = new string[]
        {
            "全LODを配置", "最大LODを配置", "選択LODを配置、なければ配置しない", "選択LODを配置、なければそれ以下で最大のLODを配置", "配置しない"
        };

        public CityMeshPlacerConfig()
        {
            // 各タイプごとの設定を初期化します。
            this.perTypeConfigs = GmlTypeConvert.ComposeTypeDict<ScenePlacementConfigPerType>();
        }
        
        public CityMeshPlacerConfig SetPlaceMethodForAllTypes(PlaceMethod placeMethod)
        {
            var dict = this.perTypeConfigs;
            foreach (var type in dict.Keys)
            {
                dict[type].placeMethod = placeMethod;
            }

            return this;
        }
        
        public CityMeshPlacerConfig SetSelectedLodForAllTypes(int lod)
        {
            var dict = this.perTypeConfigs;
            foreach (var type in dict.Keys)
            {
                dict[type].selectedLod = lod;
            }

            return this;
        }

        public ScenePlacementConfigPerType GetPerTypeConfig(GmlType type)
        {
            return this.perTypeConfigs[type];
        }

        public IReadOnlyList<GmlType> AllGmlTypes()
        {
            return this.perTypeConfigs.Keys.ToArray();
        }

        /// <summary> シリアライズするときに List形式に直します。 </summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            DictionarySerializer.OnBeforeSerialize(this.perTypeConfigs, this.keys, this.values);
        }

        /// <summary> デシリアライズするときに List から Dictionary 形式に直します。 </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            this.perTypeConfigs = DictionarySerializer.OnAfterSerialize(this.keys, this.values);
        }

    }

    /// <summary>
    /// <see cref="CityMeshPlacerConfig"/> の 1タイプあたりの設定項目です。
    /// </summary>
    [Serializable]
    internal class ScenePlacementConfigPerType
    {
        public PlaceMethod placeMethod = PlaceMethod.PlaceMaxLod;
        public int selectedLod;
        public ulong cityObjectTypeFlags = ~0ul; // 初期値は ulong の全bitを立てて Everything にします。
    }

    internal static class PlaceMethodExtension
    {
        /// <summary>
        /// 設定項目で <see cref="ScenePlacementConfigPerType.selectedLod"/> を使うかどうかは
        /// <see cref="CityMeshPlacerConfig.PlaceMethod"/> に依るので、使うかどうかを返します。
        /// </summary>
        public static bool DoUseSelectedLod(this PlaceMethod method)
        {
            return method == PlaceMethod.PlaceSelectedLodOrMax ||
                   method == PlaceMethod.PlaceSelectedLodOrDoNotPlace;
        }

        /// <summary>
        /// 3Dモデルファイルのシーンへの配置について、配置設定と3Dモデルに存在するLODの範囲から、配置時に探索対象とすべきLODの範囲を求めます。
        /// </summary>
        public static MinMax<int> LodRangeToPlace(this PlaceMethod placeMethod, MinMax<int> availableObjLodRange, int selectedLod)
        {
            var placeRange = new MinMax<int>(availableObjLodRange);
            // LODを数値指定する設定なら、その指定LODを最大LODとします。
            if (placeMethod.DoUseSelectedLod())
            {

                int min = placeRange.Min;
                int max = Math.Min(selectedLod, placeRange.Max);
                placeRange.SetMinMax(min, max);
            }

            // 1つのLODのみを探索する設定なら、範囲を1つに狭めます。
            if (placeMethod.DoSearchOnlyOneLod())
            {
                int max = placeRange.Max;
                placeRange.SetMinMax(max, max);
            }

            return placeRange;
        }

        public static bool DoesAllowMultipleLodPlaced(this PlaceMethod method)
        {
            return method == PlaceMethod.PlaceAllLod;
        }

        private static bool DoSearchOnlyOneLod(this PlaceMethod method)
        {
            return method == PlaceMethod.PlaceSelectedLodOrDoNotPlace;
        }
    }
}