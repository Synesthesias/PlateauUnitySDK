namespace PLATEAU.Editor.RoadNetwork.Exporter
{
    /// <summary>
    /// 信号制御機に関連するGeoJSONフィーチャのプロパティを定義するクラス
    /// </summary>
    public class GeoJsonFeaturePropertiesSignalController : GeoJsonFeatureProperties
    {
        /// <summary>
        /// 信号制御器の一意な識別子
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 割り当てられたノードの識別子
        /// </summary>
        public string ALLOCNODE { get; set; }

        /// <summary>
        /// 制御対象の制御信号灯器識別子（:で連結可）
        /// </summary>
        public string SIGLIGHT { get; set; }

        /// <summary>
        /// オフセット基準信号制御器の識別子
        /// </summary>
        public string OFFSETBASESIGID { get; set; }

        /// <summary>
        ///　時間帯別信号制御パターン数
        /// </summary>
        public int NUMOFPATTERN { get; set; } = 1;// 時間帯の別の制御切り替えは非対応

        /// <summary>
        /// 使用する制御パターン識別子（:で連結可）
        /// </summary>
        public string PATTERNID { get; set; }

        /// <summary>
        /// 制御サイクル長（秒）
        /// </summary>
        public string INITCYCLE { get; set; }

        /// <summary>
        /// 現示数
        /// </summary>
        public string PHASENUM { get; set; }

        /// <summary>
        /// オフセットタイプ
        /// 1:相対オフセット，0：絶対オフセット
        /// </summary>
        public int OFFSETTYPE { get; set; }

        /// <summary>
        /// オフセット値（秒）
        /// </summary>
        public int OFFSET { get; set; }

        /// <summary>
        /// 制御パターン開始時刻（:で連結可）
        /// hh/mm/ss
        /// </summary>
        public string STARTTIME { get; set; }
    }
}