﻿using PLATEAU.RoadNetwork;
using PLATEAU.RoadNetwork.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Data
{
    /// <summary>
    /// 車両タイプ
    /// </summary>
    public enum VehicleType : int
    {
        Undefind = 0,
        // 小型車両
        Smarll = 1 << 0,
        // 大型車両
        Large = 1 << 1,

        Error = 0xfffffff
    }

    ///// <summary>
    ///// 交通規制情報
    ///// </summary>
    //[Serializable, RoadNetworkSerializeData(typeof(TrafficRegulationInfoData))]
    //public class TrafficRegulationInfoData : IPrimitiveData
    //{
    //    /// <summary>
    //    /// 停止線
    //    /// </summary>
    //    public StopLine stopLine;
    //    /// <summary>
    //    /// 速度制限
    //    /// </summary>
    //    public float speedLimit;
    //    /// <summary>
    //    /// 優先道路
    //    /// </summary>
    //    public RnID<RnDataLineString> yields;
    //}

    [System.Serializable]
    public class RnDataTrafficLightController : IPrimitiveData
    {
        //// #TODO : 消える予定
        //// 自分自身を表すId　シミュレーターからのIDを一応受け取る
        //[field: SerializeField]
        //[RoadNetworkSerializeMember]
        //public string MyId { get; set; }

        /// <summary>
        /// 対応している交差点
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID<RnDataRoadBase> Parent { get; set; }

        /// <summary>
        /// 対応する信号機
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<RnID<RnDataTrafficLight>> TrafficLights { get; set; }

        /// <summary>
        /// オフセットの基準となる信号機
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID<RnDataTrafficLight> OffsetTrafficLight { get; set; }

        /// <summary>
        /// 制御バターン
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<RnID<RnDataTrafficSignalPattern>> SignalPatterns { get; set; }

        /// <summary>
        /// サイクル長(制御パターン)
        /// RnDataTrafficSignalPhaseのSplitの合計値
        /// 設定時のエラーチェック必須
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<int> CycleTime { get; set; }

        /// <summary>
        /// フェーズ数（現示数）
        /// 設定時のエラーチェック必須
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<List<int>> PhaseTimeSet { get; set; }

        /// <summary>
        /// オフセットタイプ
        /// 0:相対　1:絶対
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<int> OffsetTypes { get; set; }

        /// <summary>
        /// オフセット値
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<int> Offsets { get; set; }

        /// <summary>
        /// 制御パターン開始時刻
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public List<DateTime> StartOffsets { get; set; }

        public static bool IsValid(RnDataTrafficLightController data)
        {
            //データ数が一致しているか
            //データ数値が正常か
            return true;
        }
    }

    [System.Serializable]
    public class RnDataTrafficLight : IPrimitiveData
    {
        //// #TODO : 消える予定
        //// 自分自身を表すId　シミュレーターからのIDを一応受け取る
        //[field: SerializeField]
        //[RoadNetworkSerializeMember]
        //public string MyId { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID< RnDataTrafficLightController> Parent { get; set; }

        /// <summary>
        /// 設置されている道路のID
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID<RnDataRoad> RoadId { get; set; }

        /// <summary>
        /// 規制対象レーン種別
        /// 注意　SDKのデータのどれに当たるか調査中
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public string LaneType { get; set; }

        /// <summary>
        /// 設置位置
        /// 停止からの距離 
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public float Distance { get; set; }

    }

    /// <summary>
    /// 信号制御器のパターン
    /// フェーズを所持
    /// 実装途中
    /// </summary>
    [System.Serializable]
    public class RnDataTrafficSignalPattern : IPrimitiveData
    {
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID<RnDataTrafficLightController> Parent { get; set; }
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<RnID<RnDataTrafficSignalPhase>> Phases { get; set; }

    }

    [System.Serializable]
    public class RnDataTrafficSignalPhase : IPrimitiveData
    {
        //// #TODO : 消える予定
        //// 自分自身を表すId　シミュレーターからのIDを一応受け取る
        //[field: SerializeField]
        //[RoadNetworkSerializeMember]
        //public string MyId { get; set; }

        //public RnID<RnDataTrafficLightController> ParentController { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID<RnDataTrafficSignalPattern> Parent { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public int Order { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public float Split { get; set; }

        /// <summary>
        /// 進入可能車種規制マスク
        /// VehicleTypeを参照
        /// </summary>
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public int EnterableVehicleTypeMask { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<RnID<RnDataRoad>> BlueRoadPairs { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<RnID<RnDataRoad>> YellowRoadPairs { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public List<RnID<RnDataRoad>> RedRoadPairs { get; set; }

    }
}

    ///// <summary>
    ///// 停止線
    ///// </summary>
    //[System.Serializable]
    //public class StopLine
    //{
    //    public RnID<RnDataLineString> line;
    //    public TrafficLight trafficLight;
    //    public bool bHasStopSign;
    //}

    ///// <summary>
    ///// 信号機の電球（概念）
    ///// 今の信号機は何色ですかと聞かれたらこれを返す
    ///// </summary>
    //[System.Serializable]
    //public class TrafficLight
    //{
    //    public LightBulb[] LightBulbs => lightBulbs;
    //    [SerializeField] private LightBulb[] lightBulbs;
    //}

    //[System.Serializable]
    //public class RnDataTrafficSignalStep
    //{
    //    public RnID<RnDataTrafficLightController> parent;
    //    public int paternID;
    //    public int phaseID;
    //    public int allowVehicleTypeMask;
    //    public List<RnID<RnDataRoad>> linkAtBlue;
    //    public List<RnID<RnDataRoad>> linkAtYellow;
    //    public List<RnID<RnDataRoad>> linkAtRed;
    //}


    ///// <summary>
    ///// 信号機の電球
    ///// </summary>
    //[System.Serializable]
    //public class LightBulb
    //{
    //    public BulbType Type => type;

    //    public BulbColor Color => color;

    //    public BulbStatus Status => status;

    //    [SerializeField] private BulbType type;
    //    [SerializeField] private BulbColor color;
    //    [SerializeField] private BulbStatus status;

    //    public LightBulb(BulbType type, BulbColor color, BulbStatus status)
    //    {
    //        this.type = type;
    //        this.color = color;
    //        this.status = status;
    //    }

    //    /// <summary>
    //    /// Type of each bulb.
    //    /// </summary>
    //    public enum BulbType
    //    {
    //        ANY_CIRCLE_BULB = 0,
    //        RED_BULB = 1,
    //        YELLOW_BULB = 2,
    //        GREEN_BULB = 3,
    //        LEFT_ARROW_BULB = 4,
    //        RIGHT_ARROW_BULB = 5,
    //        UP_ARROW_BULB = 6,
    //        DOWN_ARROW_BULB = 7,
    //        DOWN_LEFT_ARROW_BULB = 8,
    //        DOWN_RIGHT_ARROW_BULB = 9,
    //        CROSS_BULB = 10,
    //    }

    //    /// <summary>
    //    /// Bulb lighting status.
    //    /// </summary>
    //    public enum BulbStatus
    //    {
    //        SOLID_OFF = 0,        // Lights off.
    //        SOLID_ON = 1,        // Lights on.
    //        FLASHING = 2,        // Lights on every flashSec.
    //    }

    //    /// <summary>
    //    /// Bulb lighting color.
    //    /// </summary>
    //    public enum BulbColor
    //    {
    //        RED = 0,
    //        YELLOW = 1,
    //        GREEN = 2,
    //        WHITE = 3,
    //    }
    //}
}
