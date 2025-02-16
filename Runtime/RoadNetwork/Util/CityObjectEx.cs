using PLATEAU.CityGML;
using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Graph;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PLATEAU.RoadNetwork.Util
{
    public static class CityObjectEx
    {
        private static readonly Regex lodRegex = new Regex(@"^LOD(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Lodレベルを取得する
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int GetLodLevel(this PLATEAUCityObjectGroup self)
        {
            if (!self)
                return 0;
            return self.Lod;
        }

        // CityGmlのAuxiliaryTrafficArea_function.xml(つくば市)から参照
        private static readonly Dictionary<string, RRoadTypeMask> functionValue2RoadType = new()
        {
            ["車道部"] = RRoadTypeMask.Road,
            ["車道交差部"] = RRoadTypeMask.Road,
            ["非常駐車帯"] = RRoadTypeMask.Road,
            ["側帯"] = RRoadTypeMask.Road,
            ["路肩"] = RRoadTypeMask.Road,
            ["停車帯"] = RRoadTypeMask.Road,
            ["乗合自動車停車所"] = RRoadTypeMask.Road,
            ["分離帯"] = RRoadTypeMask.Road,
            ["路面電車停車所"] = RRoadTypeMask.Road,
            ["自動車駐車場"] = RRoadTypeMask.Road,

            ["島"] = RRoadTypeMask.Median,
            ["交通島"] = RRoadTypeMask.Median,
            ["中央帯"] = RRoadTypeMask.Median,

            ["自転車道"] = RRoadTypeMask.SideWalk,
            ["歩道"] = RRoadTypeMask.SideWalk,
            ["歩道部"] = RRoadTypeMask.SideWalk,
            ["自転車歩行者道"] = RRoadTypeMask.SideWalk,
            ["植栽"] = RRoadTypeMask.SideWalk,
            ["植樹帯"] = RRoadTypeMask.SideWalk,
            ["植樹ます"] = RRoadTypeMask.SideWalk,
            ["歩道部の段差"] = RRoadTypeMask.SideWalk,
            ["自転車駐車場"] = RRoadTypeMask.SideWalk,

            ["車線"] = RRoadTypeMask.Lane,
            ["すりつけ区間"] = RRoadTypeMask.Road,
            ["踏切道"] = RRoadTypeMask.Road,
            ["副道"] = RRoadTypeMask.Road,

            // 不明なものは一旦Roadに
            ["軌道敷"] = RRoadTypeMask.Road,
            ["待避所"] = RRoadTypeMask.Road,
            ["軌道中心線"] = RRoadTypeMask.Road,
            ["軌道"] = RRoadTypeMask.Road,
            ["軌きょう"] = RRoadTypeMask.Road,
            ["軌間"] = RRoadTypeMask.Road,
            ["レール"] = RRoadTypeMask.Road,
            ["道床"] = RRoadTypeMask.Road,
        };

        public static RRoadTypeMask GetRoadType(this CityInfo.CityObjectList.CityObject self)
        {
            // 緊急輸送道路もはじく必要がある
            // LOD3からチェックする
            var ret = RRoadTypeMask.Empty;

            // COT_Roadは強制的に対象にする
            if ((self.CityObjectType & CityObjectType.COT_Road) != 0)
                ret |= RRoadTypeMask.Road;

            if (self.AttributesMap.TryGetValue("tran:function", out var tranFunction))
            {
                var str = tranFunction.StringValue;

                if (functionValue2RoadType.TryGetValue(str, out var v))
                {
                    ret |= v;
                }

                if (str.Contains("歩道"))
                {
                    ret |= RRoadTypeMask.SideWalk;
                }

                if (str.Contains("高速"))
                {
                    ret |= RRoadTypeMask.HighWay;
                }

                if (str.Contains("車線"))
                {
                    ret |= RRoadTypeMask.Lane;
                }
            }

            // LOD1
            if (self.AttributesMap.TryGetValue("tran:class", out var tranClass))
            {
                var str = tranClass.StringValue;
                if (str == "道路")
                {
                    ret |= RRoadTypeMask.Road;
                }
            }

            if (ret == 0)
                ret |= RRoadTypeMask.Undefined;

            return ret;
        }

    }
}