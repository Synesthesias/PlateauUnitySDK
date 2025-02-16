using PLATEAU.RoadNetwork.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace PLATEAU.RoadNetwork.Structure
{
    /// <summary>
    /// 交通規制の生成補助クラス
    /// </summary>
    public class TrafficRegulationInfoCreateHelper
    {

    }

    /// <summary>
    /// 信号制御器
    /// </summary>
    [Serializable]
    public partial class TrafficSignalLightController : ARnParts<TrafficSignalLightController>
    {

        public static bool CreateDefault(RnIntersection intersection)
        {
            Assert.IsNotNull(intersection);
            // 信号制御器の作成、信号機の作成
            var trafficController =
                new TrafficSignalLightController("SignalController" + intersection.DebugMyId, intersection, intersection.GetCenterPoint());

            // 信号制御器の登録
            intersection.SignalController = trafficController;

            var lights = TrafficSignalLight.CreateTrafficLights(intersection);
            trafficController.TrafficLights.AddRange(lights);

            // 3つ以上道路が交差している時のみ信号制御器を配置する
            var nLights = lights.Count;
            if (nLights <= 2)
            {
                intersection.SignalController = null;
                return true;
            }

            // 各道路の広さを計算
            var widthList = new List<(TrafficSignalLight, float)>(nLights);
            var e = lights.GetEnumerator();
            e.MoveNext();
            for (int i = 0; i < nLights; i++)
            {
                var p0 = e.Current.Neighbor.First().First();
                var p1 = e.Current.Neighbor.Last().Last();
                widthList.Add((e.Current, Vector3.Distance(p0, p1)));
                e.MoveNext();
            }

            // 主道路、従道路の決定(3つ以上道路が交差している時、道路幅がもっとも広い二つを主道路とする。)
            widthList.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            widthList.GetEnumerator().MoveNext();
            var subLights = new List<TrafficSignalLight>(nLights - 2);
            for (int i = 0; i < nLights - 2; i++)
                subLights.Add(widthList[i].Item1);
            var mainLights = new List<TrafficSignalLight>(2);
            for (int i = nLights - 2; i < nLights; i++)
                mainLights.Add(widthList[i].Item1);

            // デフォ値の設定
            var pattern = new TrafficSignalControllerPattern
            {
                Parent = trafficController,
                ControlPatternId = "Default",
                OffsetSeconds = 0,
                OffsetTrafficLight = null,
                OffsetType = OffsetRelationType.Absolute,
                StartOffsets = DateTime.MinValue,
            };

            var cnt = 0;
            foreach (var def in DefaultVal.Default)
            {
                var phase = new TrafficSignalControllerPhase(cnt.ToString());
                phase.Parent = pattern;
                phase.Order = cnt++;
                phase.Split = def.split;
                phase.EnterableVehicleTypeMask = (int)(VehicleType.Smarll | VehicleType.Large);

                foreach (var main in mainLights)
                {
                    // ToDo: phase.BlueRoadPairsのデータ構造的に表現出来ない　構造に問題がある
                    if (def.main == DefaultVal.Color.Blue)
                    {

                    }
                    else if (def.main == DefaultVal.Color.Yellow)
                    {

                    }
                    else if (def.main == DefaultVal.Color.Red)
                    {

                    }
                    if (def.sub == DefaultVal.Color.Blue)
                    {

                    }
                    else if (def.sub == DefaultVal.Color.Yellow)
                    {

                    }
                    else if (def.sub == DefaultVal.Color.Red)
                    {

                    }
                }
                pattern.Phases.Add(phase);
            }
            trafficController.SignalPatterns.Add(pattern);

            return false;
        }

        public struct DefaultVal
        {

            /*
             * サイクル100秒、フェーズ数6
             * P1　44秒　主：青　従：赤
             * P2　3秒　主：黄　従：赤
             * P3　3秒　主：赤　従：赤
             * P4　44秒　主：赤　従：青
             * P5　3秒　主：赤　従：黄
             * P6　3秒　主：赤　従：赤
             */

            public enum Color
            {
                Blue,
                Yellow,
                Red
            }

            public struct DataSet
            {
                public DataSet(float split, Color main, Color sub)
                {
                    this.split = split;
                    this.main = main;
                    this.sub = sub;
                }
                public readonly float split;
                public readonly Color main; // 主
                public readonly Color sub;  // 従
            }

            public static readonly DataSet[] Default = new DataSet[]
            {
                new DataSet(44, Color.Blue, Color.Red),
                new DataSet(3, Color.Yellow, Color.Red),
                new DataSet(3, Color.Red, Color.Red),
                new DataSet(44, Color.Red, Color.Blue),
                new DataSet(3, Color.Red, Color.Yellow),
                new DataSet(3, Color.Red, Color.Red),
            };
        }



        /// <summary>
        /// デシリアライズ用
        /// </summary>
        public TrafficSignalLightController() { }

        public TrafficSignalLightController(string id, RnIntersection node, in Vector3 position)
            : base()
        {
            Parent = node;
            //this.Position = position;
        }

        // デバッグ用
        public string DebugId { get => "t_" + Parent.DebugMyId; }

        // RnIntersection
        public RnRoadBase Parent { get; private set; } = null;

        public RnIntersection CorrespondingNode { get => Parent as RnIntersection; }


        //public bool GapImpedanceFlag { get; private set; } = false;
        public List<TrafficSignalLight> TrafficLights { get; private set; } = new List<TrafficSignalLight>();
        public List<TrafficSignalControllerPattern> SignalPatterns { get; private set; } = new List<TrafficSignalControllerPattern>();

        public Vector3 Position { get => Parent.GetCentralVertex(); }
    }

    /// <summary>
    /// 信号灯器
    /// 注意　配置されている道路に交差点も設定出来るため注意
    /// </summary>
    public partial class TrafficSignalLight : ARnParts<TrafficSignalLight>
    {
        /// <summary>
        /// デシリアライズ用
        /// </summary>
        public TrafficSignalLight() { }

        public TrafficSignalLight(TrafficSignalLightController controller, RnRoadBase road, List<RnWay> intersectionNeighborBorder)
        {
            Assert.IsNotNull(controller);
            this.Parent = controller;
            Assert.IsNotNull(road);
            Road = road;

            // intersectionNeighborBorderが必ずroadの境界線に含まれていることを確認
            foreach (var intersectionBorder in intersectionNeighborBorder)
            {
                // bool wasFound = false;
                foreach (var border in road.GetBorders().Select(x => x.BorderWay))
                {
                    if (intersectionBorder == border)
                    {
                        // wasFound = true;
                        break;
                    }
                }
                // ここの理屈が合っているか確認するためコメントアウト
                //Assert.IsTrue(wasFound, "交差点の境界辺と道路の境界辺が一致しません");
            }

            Assert.IsNotNull(intersectionNeighborBorder);
            Neighbor = intersectionNeighborBorder;
        }

        public void SetStatus(TrafficSignalLightBulb.Status status)
        {

        }

        public TrafficSignalLightController Parent { get; private set; }

        /// <summary>
        /// 交差点or道路
        /// </summary>
        public RnRoadBase Road { get; private set; }

        public string LaneType { get; private set; }
        public float Distance { get; private set; }

        public List<RnWay> Neighbor { get; private set; } = new List<RnWay>();

        ///// <summary>
        ///// 対応する停止線
        ///// </summary>
        //public StopLine stopLine;

        public Vector3 Position
        {
            get
            {
                if (Neighbor == null)
                    return Vector3.zero;
                if (Neighbor.Count == 0)
                    return Vector3.zero;

                // 道路と交差点の境界線の中心を疑似的に求める　（平均だと計算コストが高いため）
                var a = Neighbor.First().First();
                var b = Neighbor.Last().Last();
                var center = (a + b) * 0.5f;
                return center;
            }
        }

        /// <summary>
        /// RnIntersectionに接する各RnRoadに対してTrafficSignalLightを生成する
        /// </summary>
        /// <param name="intersection"></param>
        /// <returns></returns>
        public static IReadOnlyCollection<TrafficSignalLight> CreateTrafficLights(RnIntersection intersection)
        {
            TrafficSignalLightController controller = intersection.SignalController;
            Dictionary<RnRoadBase, List<RnWay>> roads = new();
            foreach (var item in intersection.Neighbors)
            {
                List<RnWay> borderList = null;
                if (roads.TryGetValue(item.Road, out borderList) == false)
                {
                    borderList = new();
                    roads.Add(item.Road, borderList);
                }

                borderList.Add(item.Border);

            }

            var lights = new List<TrafficSignalLight>(roads.Count);
            foreach (var road in roads)
            {
                lights.Add(new TrafficSignalLight(controller, road.Key/* as RnRoad*/, road.Value));
            }

            return lights;
        }

    }

    /// <summary>
    /// 信号制御のパターン
    /// 開始時刻、制御パターンID、フェーズのリスト、信号灯のオフセットを持つ
    /// </summary>
    public partial class TrafficSignalControllerPattern : ARnParts<TrafficSignalControllerPattern>
    {
        /// <summary>
        /// デシリアライズ用
        /// </summary>
        public TrafficSignalControllerPattern() { }

        public TrafficSignalLightController Parent { get; set; }
        public List<TrafficSignalControllerPhase> Phases { get; private set; } = new List<TrafficSignalControllerPhase>();
        public float OffsetSeconds { get; set; } = 0;
        public TrafficSignalLight OffsetTrafficLight { get; set; } = null;
        public OffsetRelationType OffsetType { get; set; } = OffsetRelationType.Absolute;
        public DateTime StartOffsets { get; set; } = DateTime.MinValue;

        public string ControlPatternId { get; set; } = "_undefind";

    }

    /// <summary>
    /// 信号制御のフェーズ
    /// 信号制御のパターンに含まれる要素
    /// </summary>
    public partial class TrafficSignalControllerPhase : ARnParts<TrafficSignalControllerPhase>
    {
        /// <summary>
        /// デシリアライズ用
        /// </summary>
        public TrafficSignalControllerPhase() { }

        public TrafficSignalControllerPhase(string id)
        {
            this.Name = id;
        }
        public TrafficSignalControllerPattern Parent { get; set; }
        public int Order { get; set; }
        public float Split { get; set; }
        public int EnterableVehicleTypeMask { get; set; }

        /// <summary>
        /// 青信号時に通過出来る道路のID
        /// 注意　交差点のIDではない
        /// </summary>
        public List<RnRoadBase> BlueRoadPairs { get; set; }

        /// <summary>
        /// 青信号時に通過出来る道路のID
        /// 注意　交差点のIDではない
        /// </summary>
        public List<RnRoadBase> YellowRoadPairs { get; set; }

        /// <summary>
        /// 青信号時に通過出来る道路のID
        /// 注意　交差点のIDではない
        /// </summary>
        public List<RnRoadBase> RedRoadPairs { get; set; }

        //public Dictionary<TrafficSignalLight, RnRoad> DirectionMap { get; set; }

        public string Name { get; set; }
    }

    ///// <summary>
    ///// 信号灯器の点灯をずらす
    ///// </summary>
    //public class TrafficLightSignalOffset
    //{
    //    public TrafficSignalLight ReferenceSignalLight { get; set; }
    //    public float Seconds { get; set; }
    //}

    /// <summary>
    /// 交通規制情報
    /// </summary>
    public class TrafficRegulationInfo
    {
        /// <summary>
        /// 停止線
        /// </summary>
        public StopLine stopLine;
        /// <summary>
        /// 速度制限
        /// </summary>
        public float speedLimit;
        /// <summary>
        /// 優先道路
        /// </summary>
        public RnID<RnDataLineString> yields;
    }

    /// <summary>
    /// 停止線
    /// </summary>
    [System.Serializable]
    public class StopLine
    {
        public RnDataLineString line;
        public TrafficSignalLight trafficLight;
        public bool bHasStopSign;
    }

    /// <summary>
    /// 信号機の電球（概念）
    /// 信号機は何色ですかと聞かれたらこれを返す
    /// クラス名がTrafficSignalLightと混じって分かりずらいので変えたい
    /// </summary>
    [System.Serializable]
    public class TrafficSignalLightBulb
    {
        public LightBulb[] LightBulbs => lightBulbs;
        [SerializeField] private LightBulb[] lightBulbs;

        public enum Status
        {
            // 0
            Undefind = 0,

            // 0x0000000X
            Stop = 0x0001,
            Attention = 0x00000002,
            Go = 0x0003,

            // 0x000000X0
            Flashing = 0x00000010,

            // 0x00000X00
            BlueArrow = 0x000000100,
            YellowArrow = 0x000000200,

            //...

            // 0x0X000000
            UserDefind1 = 0x01000000,
            UserDefind2 = 0x02000000,
            UserDefind3 = 0x03000000,
            UserDefind4 = 0x04000000,
            UserDefind5 = 0x05000000,
            UserDefind6 = 0x06000000,
            //...
        }
    }

    /// <summary>
    /// 信号機の電球
    /// </summary>
    [System.Serializable]
    public class LightBulb
    {
        public BulbType Type => type;

        public BulbColor Color => color;

        public BulbStatus Status => status;

        [SerializeField] private BulbType type;
        [SerializeField] private BulbColor color;
        [SerializeField] private BulbStatus status;

        public LightBulb(BulbType type, BulbColor color, BulbStatus status)
        {
            this.type = type;
            this.color = color;
            this.status = status;
        }

        /// <summary>
        /// Type of each bulb.
        /// </summary>
        public enum BulbType
        {
            ANY_CIRCLE_BULB = 0,
            RED_BULB = 1,
            YELLOW_BULB = 2,
            GREEN_BULB = 3,
            LEFT_ARROW_BULB = 4,
            RIGHT_ARROW_BULB = 5,
            UP_ARROW_BULB = 6,
            DOWN_ARROW_BULB = 7,
            DOWN_LEFT_ARROW_BULB = 8,
            DOWN_RIGHT_ARROW_BULB = 9,
            CROSS_BULB = 10,
        }

        /// <summary>
        /// Bulb lighting status.
        /// </summary>
        public enum BulbStatus
        {
            SOLID_OFF = 0,        // Lights off.
            SOLID_ON = 1,        // Lights on.
            FLASHING = 2,        // Lights on every flashSec.
        }

        /// <summary>
        /// Bulb lighting color.
        /// </summary>
        public enum BulbColor
        {
            RED = 0,
            YELLOW = 1,
            GREEN = 2,
            WHITE = 3,
        }
    }
}
