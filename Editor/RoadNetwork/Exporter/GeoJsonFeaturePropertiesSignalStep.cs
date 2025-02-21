namespace PLATEAU.Editor.RoadNetwork.Exporter
{
    /// <summary>
    /// 信号現示階梯に関連するGeoJSONフィーチャのプロパティを定義するクラス
    /// </summary>
    public class GeoJsonFeaturePropertiesSignalStep : GeoJsonFeatureProperties
    {
        /// <summary>
        /// 信号現示階梯の一意な識別子
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 信号制御器の識別子
        /// </summary>
        public string SIGNALID { get; set; }

        /// <summary>
        /// 制御パターン番号
        /// </summary>
        public string PATTERNID { get; set; }

        /// <summary>
        /// 階梯順番
        /// </summary>
        public int ORDER { get; set; }

        /// <summary>
        /// 階梯の持続時間（スプリット）（秒）
        /// </summary>
        public int DURATION { get; set; }

        /// <summary>
        /// 制御信号灯器の識別子
        /// </summary>
        public string SIGLIGHT { get; set; }

        /// <summary>
        /// 進入許可車種規制マスク
        /// </summary>
        public int TYPEMASK { get; set; } = (int)(GeoJsonFeatureProperties.TypeConfig.Small | GeoJsonFeatureProperties.TypeConfig.Large | GeoJsonFeatureProperties.TypeConfig.Bus);

        /// <summary>
        /// 青現示方向リンクペア（これらは"->"で繋がれ，リンクペアは":"で区切られる）
        /// </summary>
        public string GREEN { get; set; }

        /// <summary>
        /// 青現示方向リンクペア（これらは"->"で繋がれ，リンクペアは":"で区切られる）
        /// </summary>
        public string YELLOW { get; set; }

        /// <summary>
        /// 青現示方向リンクペア（これらは"->"で繋がれ，リンクペアは":"で区切られる）
        /// </summary>
        public string RED { get; set; }
    }
}