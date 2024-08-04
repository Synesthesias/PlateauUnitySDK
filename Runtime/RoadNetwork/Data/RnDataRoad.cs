﻿using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Structure;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Data
{
    [Serializable, RoadNetworkSerializeData(typeof(RnRoad))]
    public class RnDataRoad : RnDataRoadBase
    {
        // 対象のtranオブジェクト
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public PLATEAUCityObjectGroup TargetTran { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID<RnDataRoadBase> Next { get; set; }

        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnID<RnDataRoadBase> Prev { get; set; }

        // 本線レーン
        [field: SerializeField]
        [RoadNetworkSerializeMember("mainLanes")]
        public List<RnID<RnDataLane>> MainLanes { get; set; } = new List<RnID<RnDataLane>>();

        // 即性情報
        [field: SerializeField]
        [RoadNetworkSerializeMember]
        public RnRoadAttribute RnRoadAttribute { get; set; }
    }
}