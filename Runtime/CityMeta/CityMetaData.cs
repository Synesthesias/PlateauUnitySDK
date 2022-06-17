﻿using UnityEngine;

namespace PlateauUnitySDK.Runtime.CityMeta
{
    public class CityMetaData : ScriptableObject
    {
        public IdToGmlTable idToGmlTable = new IdToGmlTable();
        public CityImporterConfig cityImporterConfig = new CityImporterConfig();

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

        public void DoClearIdToGmlTable()
        {
            this.idToGmlTable?.Clear();
        }
    }
}