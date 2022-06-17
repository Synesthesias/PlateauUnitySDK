﻿using System;
using System.IO;
using System.Linq;
using LibPLATEAU.NET.CityGML;
using PlateauUnitySDK.Runtime.CityMeta;
using PlateauUnitySDK.Runtime.Util;
using UnityEditor;
using UnityEngine;

namespace PlateauUnitySDK.Editor.Converters
{
    /// <summary>
    /// 注意: これは旧式です。新しい版 <see cref="CityMetaDataGenerator"/> を利用してください。
    /// この古い版は CityModel から idToGmlFileTable を生成します。
    /// しかし、実際は変換時のオブジェクト分けの粒度に合わせた Tableを生成すべきなので、
    /// 新しい版ではメッシュから idToGmlFileTable を作成します。 
    /// </summary>
    [Obsolete]
    internal class GmlToCityMetaDataConverter : IFileConverter
    {
        private CityMapMetaDataGeneratorConfig config;
        // public CityMetaData LastConvertedCityMetaData { get; set; }

        public GmlToCityMetaDataConverter()
        {
            this.config = new CityMapMetaDataGeneratorConfig();
        }

        /// <summary>
        /// gmlファイルをロードして CityMapInfo に書き込みます。
        /// </summary>
        public bool Convert(string srcGmlPath, string dstTableFullPath)
        {
            return ConvertInner(srcGmlPath, dstTableFullPath, null);
        }

        /// <summary>
        /// ロードを省略し、ロード済みの cityModel をもとに変換します。
        /// </summary>
        public bool ConvertWithoutLoad(CityModel cityModel, string srcGmlPath, string dstTableFullPath)
        {
            if (cityModel == null)
            {
                Debug.LogError("cityModel is null.");
                return false;
            }

            return ConvertInner(srcGmlPath, dstTableFullPath, cityModel);
        }

        /// <summary>
        /// <see cref="CityMetaData"/> に変換します。
        /// 引数の <paramref name="cityModel"/> が null の場合、<see cref="CityModel"/> を新たにロードして変換します。
        /// <paramref name="cityModel"/> が nullでない場合、ロードを省略してそのモデルを変換します。
        /// </summary>
        public bool ConvertInner(string srcGmlPath, string dstTableFullPath, CityModel cityModel)
        {
            if (!IsPathValid(srcGmlPath, dstTableFullPath)) return false;

            try
            {
                cityModel ??= CityGml.Load(srcGmlPath, this.config.ParserParams, DllLogCallback.UnityLogCallbacks);
                
                // IdToGmlTable を作成します。
                var cityObjectIds = cityModel.RootCityObjects
                    .SelectMany(co => co.CityObjectDescendantsDFS)
                    .Select(co => co.ID);
                var mapInfo = LoadOrCreateMetaData(dstTableFullPath, this.config.DoClearIdToGmlTable);
                string gmlFileName = Path.GetFileNameWithoutExtension(srcGmlPath);
                foreach (string id in cityObjectIds)
                {
                    if (mapInfo.DoGmlTableContainsKey(id)) continue;
                    mapInfo.AddToGmlTable(id, gmlFileName);
                }
                
                // 追加情報を書き込みます。
                // LastConvertedCityMetaData = mapInfo;
                EditorUtility.SetDirty(mapInfo);
                AssetDatabase.SaveAssets();
                return true;
            }catch (FileLoadException e)
            {
                Debug.LogError($"Failed to load gml file.\n gml path = {srcGmlPath}\n{e}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error generating {nameof(CityMetaData)}.\ngml path = {srcGmlPath}\n{e}");
                return false;
            }
        }

        private static bool IsPathValid(string srcGmlPath, string dstTableFullPath)
        {
            if (!PathUtil.IsValidInputFilePath(srcGmlPath, "gml", false))
            {
                return false;
            }
            if(!PathUtil.IsValidOutputFilePath(dstTableFullPath, "asset"))
            {
                return false;
            }
            // TODO outputPathがAssetsフォルダ内かどうかチェックする
            return true;
        }


        public CityMapMetaDataGeneratorConfig Config
        {
            set => this.config = value;
            get => this.config;
        }
        
        /// <summary>
        /// 指定パスの <see cref="CityMetaData"/> をロードします。
        /// ファイルが存在しない場合、新しく作成します。
        /// ファイルが存在する場合、<paramref name="doClearOldMapInfo"/> が false ならばそれをロードします。
        /// true ならば中身のデータを消してからロードします。
        /// </summary>
        private static CityMetaData LoadOrCreateMetaData(string dstAssetPath, bool doClearOldMapInfo)
        {
            bool doFileExists = File.Exists(dstAssetPath);
            if (!doFileExists)
            {
                var instance = ScriptableObject.CreateInstance<CityMetaData>();
                AssetDatabase.CreateAsset(instance, dstAssetPath);
                AssetDatabase.SaveAssets();
            }
            var loadedMetaData = AssetDatabase.LoadAssetAtPath<CityMetaData>(dstAssetPath);
            if (doClearOldMapInfo)
            {
                loadedMetaData.DoClearIdToGmlTable();
            }
            return loadedMetaData;
        }
    }
}