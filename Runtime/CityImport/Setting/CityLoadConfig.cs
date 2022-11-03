﻿using System;
using System.Collections.Generic;
using System.Linq;
using PLATEAU.Interop;
using PLATEAU.Udx;
using PLATEAU.Util;
using UnityEngine;

namespace PLATEAU.CityImport.Setting
{
    /// <summary>
    /// <see cref="PLATEAUCityModelLoader"/> の設定です。
    /// </summary>
    [Serializable]
    internal class CityLoadConfig : ISerializationCallbackReceiver
    {
        // 都市モデル読み込みの全体設定です。
        [SerializeField] private string sourcePathBeforeImport;
        [SerializeField] private string sourcePathAfterImport;
        [SerializeField] private string[] areaMeshCodes;
        [SerializeField] private int coordinateZoneID = 9;
        // 対象となる緯度経度の範囲です。
        [SerializeField] private double minLatitude;
        [SerializeField] private double maxLatitude;
        [SerializeField] private double minLongitude;
        [SerializeField] private double maxLongitude;

        // 都市モデル読み込みの、パッケージ種ごとの設定です。
        private Dictionary<PredefinedCityModelPackage, PackageLoadSetting> perPackagePairSettings = new Dictionary<PredefinedCityModelPackage, PackageLoadSetting>();
        
        // Dictionary をシリアライズ化して保存するために Array化して保持するもの です。
        [SerializeField] private List<PredefinedCityModelPackage> perPackageSettingKeys = new List<PredefinedCityModelPackage>();
        [SerializeField] private List<PackageLoadSetting> perPackageSettingValues = new List<PackageLoadSetting>();
        

        public IEnumerable<KeyValuePair<PredefinedCityModelPackage, PackageLoadSetting>> ForEachPackagePair =>
            this.perPackagePairSettings;

        public PackageLoadSetting GetConfigForPackage(PredefinedCityModelPackage package)
        {
            return this.perPackagePairSettings[package];
        }

        public void InitWithPackageFlags(PredefinedCityModelPackage packageFlags)
        {
            this.perPackagePairSettings.Clear();
            foreach (var package in EnumUtil.EachFlags(packageFlags))
            {
                var predefined = CityModelPackageInfo.GetPredefined(package);
                var val = new PackageLoadSetting(true, predefined.hasAppearance, (uint)predefined.minLOD, (uint)predefined.maxLOD,
                    MeshGranularity.PerCityModelArea, false);
                this.perPackagePairSettings.Add(package, val);
            }
        }

        /// <summary>
        /// 設定に合うGMLファイルを検索します。
        /// 多数のファイルから検索するので、実行時間が長くなりがちである点にご注意ください。
        /// </summary>
        /// <param name="rootPath">検索元となる PLATEAUルートフォルダです。</param>
        /// <param name="collection">検索に利用した collection を outで返します。</param>
        /// <returns>検索にヒットしたGMLをパッケージごとに分けたものです。keyはパッケージ、 valueはそのパッケージに属するgmlファイルのパスのリストです。</returns>
        public Dictionary<PredefinedCityModelPackage, List<string>> SearchMatchingGMLList(string rootPath, out UdxFileCollection collection)
        {
            // 地域ID(メッシュコード)で絞り込みます。
            var meshCodes = AreaMeshCodes.Select(str => MeshCode.Parse(str)).ToArray();
            collection = UdxFileCollection.Find(rootPath).FilterByMeshCodes(meshCodes);
            
            // パッケージ種ごとの設定で「ロードする」にチェックが入っているパッケージ種で絞り込みます。
            var targetPackages =
                this
                    .ForEachPackagePair
                    .Where(pair => pair.Value.loadPackage)
                    .Select(pair => pair.Key);
            var foundGmls = new Dictionary<PredefinedCityModelPackage, List<string>>();
            
            // 絞り込まれたGMLパスを戻り値の辞書にコピーします。
            foreach (var package in targetPackages)
            {
                foreach (var gmlPath in collection.GetGmlFiles(package))
                {
                    if (!foundGmls.ContainsKey(package))
                    {
                        foundGmls[package] = new List<string>();
                    } 
                    foundGmls[package].Add(gmlPath);
                    Debug.Log($"found gml : {package}, {gmlPath}");
                }
            }
            return foundGmls;
        }

        public string SourcePathBeforeImport
        {
            get => this.sourcePathBeforeImport;
            set => this.sourcePathBeforeImport = value;
        }

        public string SourcePathAfterImport
        {
            get => this.sourcePathAfterImport;
            set => this.sourcePathAfterImport = value;
        }

        public string[] AreaMeshCodes
        {
            get => this.areaMeshCodes;
            set => this.areaMeshCodes = value;
        }

        public int CoordinateZoneID
        {
            get => this.coordinateZoneID;
            set => this.coordinateZoneID = value;
        }

        public void SetExtent(Extent extent)
        {
            
        }

        /// <summary>
        /// 緯度・経度での範囲です。
        /// ただし、高さの設定は無視され、高さの範囲は必ず -99,999 ～ 99,999 になります。
        /// </summary>
        public Extent Extent
        {
            get
            {
                var geoMin = new GeoCoordinate(this.minLatitude, this.minLongitude, -99999);
                var geoMax = new GeoCoordinate(this.maxLatitude, this.maxLongitude, 99999);
                return new Extent(geoMin, geoMax);
            }
            set
            {
                var geoMin = value.Min;
                var geoMax = value.Max;
                this.minLatitude = geoMin.Latitude;
                this.maxLatitude = geoMax.Latitude;
                this.minLongitude = geoMin.Longitude;
                this.maxLongitude = geoMax.Longitude;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            DictionarySerializer.OnBeforeSerialize(this.perPackagePairSettings, this.perPackageSettingKeys, this.perPackageSettingValues);    
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            this.perPackagePairSettings = DictionarySerializer.OnAfterSerialize(this.perPackageSettingKeys, this.perPackageSettingValues);
        }
    }
}
