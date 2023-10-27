using System;
using System.Collections.Generic;
using PLATEAU.CityImport.Config;
using PLATEAU.Dataset;
using PLATEAU.Editor.EditorWindow.Common;
using PLATEAU.Native;
using PLATEAU.Texture;
using PLATEAU.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PLATEAU.Editor.CityImport.GUIParts
{
    /// <summary>
    /// 地図タイルのズームレベルを選択するGUIです。
    /// </summary>
    internal class MapZoomLevelSelectGUI : IEditorDrawable
    {
        private readonly ReliefLoadConfig config;
        private bool zoomLevelSearchButtonPushed;
        private GeoCoordinate geoCoord;
        private MapZoomLevelSearchResult zoomLevelSearchResult = new MapZoomLevelSearchResult{AvailableZoomLevelMax = -1, AvailableZoomLevelMin = -1, IsValid = false};
        private string mapTileUrl;
        public MapZoomLevelSelectGUI(ReliefLoadConfig conf, string mapTileUrl, MeshCode firstMeshCode)
        {
            config = conf;
            geoCoord = firstMeshCode.Extent.Center;
            OnMapTileUrlChanged(mapTileUrl);
        }

        public void OnMapTileUrlChanged(string mapTileUrlArg)
        {
            mapTileUrl = mapTileUrlArg;
            zoomLevelSearchButtonPushed = false;
        }
        
        public void Draw()
        {
            int zoomLevel = config.MapTileZoomLevel;
                            if (zoomLevelSearchButtonPushed)
                            {
                                if (zoomLevelSearchResult.IsValid)
                                {
                                    var zoomChoicesStr = new List<string>();
                                    var zoomChoicesInt = new List<int>();
                                    for (int i = zoomLevelSearchResult.AvailableZoomLevelMin;
                                         i <= zoomLevelSearchResult.AvailableZoomLevelMax;
                                         i++)
                                    {
                                        zoomChoicesStr.Add(i.ToString());
                                        zoomChoicesInt.Add(i);
                                    }

                                    zoomLevel = EditorGUILayout.IntPopup("ズームレベル", zoomLevel, zoomChoicesStr.ToArray(),
                                        zoomChoicesInt.ToArray());
                                }
                            }
                            else
                            {
                                zoomLevel = EditorGUILayout.IntField("ズームレベル", config.MapTileZoomLevel);
                                zoomLevel = Math.Min(zoomLevel, ReliefLoadConfig.MaxZoomLevel);
                                zoomLevel = Math.Max(zoomLevel, ReliefLoadConfig.MinZoomLevel);
                            }
                            
                            config.MapTileZoomLevel = zoomLevel;
                            PlateauEditorStyle.CenterAlignHorizontal(() =>
                            {
                                if (PlateauEditorStyle.MiniButton("利用可能ズームレベルを検索", 180))
                                {
                                    using var progressBar = new ProgressBar();
                                    progressBar.Display("利用可能ズームレベルを検索中...", 0.1f);

                                    try
                                    {
                                        zoomLevelSearchResult = new MapZoomLevelSearcher().Search(mapTileUrl,
                                            geoCoord.Latitude, geoCoord.Longitude);
                                        zoomLevelSearchButtonPushed = true;
                                        config.MapTileZoomLevel = zoomLevelSearchResult.AvailableZoomLevelMax;
                                    }
                                    catch (Exception)
                                    {
                                        Dialogue.Display("ズームレベルを検索できませんでした。地図URLを確認してください。", "OK");
                                        zoomLevelSearchResult.IsValid = false;
                                    }
                                }
                            });

                            if (zoomLevelSearchButtonPushed)
                            {
                                if (zoomLevelSearchResult.IsValid)
                                {
                                    EditorGUILayout.HelpBox(
                                        $"ズームレベルは {zoomLevelSearchResult.AvailableZoomLevelMin} から {zoomLevelSearchResult.AvailableZoomLevelMax} まで利用可能です",
                                        MessageType.Info);
                                }
                                else
                                {
                                    EditorGUILayout.HelpBox("URLに誤りがあります", MessageType.Error);
                                }
                            }
        }

        public void Dispose() {}
    }
}