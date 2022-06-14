﻿using LibPLATEAU.NET.CityGML;
using UnityEngine;

namespace PlateauUnitySDK.Runtime.CityMapMetaData
{
    public class CityMapMetaData : ScriptableObject
    {
        // TODO 未実装
        public IdToGmlTable idToGmlTable = new IdToGmlTable();
        public CityModelImportConfig cityModelImportConfig = new CityModelImportConfig();
        public string importSourcePath;
        public string exportFolderPath;
        
        public bool DoGmlTableContainsKey(string cityObjId)
        {
            return this.idToGmlTable.ContainsKey(cityObjId);
        }

        public void AddToGmlTable(string cityObjId, string gmlName)
        {
            this.idToGmlTable.Add(cityObjId, gmlName);
        }

        public bool TryGetValueFromGmlTable(string cityObjId, out string gmlFileName)
        {
            return this.idToGmlTable.TryGetValue(cityObjId, out gmlFileName);
        }

        public void ClearData()
        {
            this.idToGmlTable?.Clear();
            cityModelImportConfig.referencePoint = Vector3.zero;
            // MaxLod = 0;
            cityModelImportConfig.meshGranularity = MeshGranularity.PerPrimaryFeatureObject;
            this.importSourcePath = "";
            this.exportFolderPath = "";
        }
    }
}