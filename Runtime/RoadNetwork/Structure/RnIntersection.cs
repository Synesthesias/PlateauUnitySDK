using PLATEAU.CityInfo;
using PLATEAU.RoadNetwork.Util;
using PLATEAU.RoadNetwork.Util.Voronoi;
using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Splines;

namespace PLATEAU.RoadNetwork.Structure
{
    [Serializable]
    public partial class RnTrack : ARnParts<RnTrack>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------

        /// <summary>
        /// 流入元
        /// </summary>
        public RnWay FromBorder { get; set; }

        /// <summary>
        /// 流出先
        /// </summary>
        public RnWay ToBorder { get; set; }

        /// <summary>
        /// 経路
        /// </summary>
        public Spline Spline { get; set; }

        /// <summary>
        /// 曲がり具合
        /// </summary>
        public RnTurnType TurnType { get; set; }

        //----------------------------------
        // end: フィールド
        //----------------------------------
        // シリアライズ化の為にデフォルトコンストラクタは必要
        public RnTrack() { }

        public RnTrack(RnWay from, RnWay to, Spline spline, RnTurnType turnType = RnTurnType.Straight)
        {
            FromBorder = from;
            ToBorder = to;
            Spline = spline;
            TurnType = turnType;
        }

        /// <summary>
        /// 入口/出口が同じかどうか
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public bool IsSameInOut(RnWay from, RnWay to)
        {
            return FromBorder.IsSameLineReference(from) && ToBorder.IsSameLineReference(to);
        }

        /// <summary>
        /// 入口/出口が同じかどうか
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool IsSameInOut(RnTrack other)
        {
            return IsSameInOut(other.FromBorder, other.ToBorder);
        }

        /// <summary>
        /// FromBorderもしくはToBorderがwayと同じかどうか
        /// </summary>
        /// <param name="way"></param>
        /// <returns></returns>
        public bool ContainsBorder(RnWay way)
        {
            return FromBorder.IsSameLineReference(way) || ToBorder.IsSameLineReference(way);
        }
    }

    // 交差点における曲がり具合
    public enum RnTurnType
    {
        // 左後ろ
        LeftBack,
        // 左折
        LeftTurn,
        // 左前
        LeftFront,
        // 直進
        Straight,
        // 右前
        RightFront,
        // 右折
        RightTurn,
        // 右後ろ
        RightBack,
        // Uターン
        UTurn,
    }

    public static class RnTurnTypeEx
    {
        // enumの数(キャッシュ用)
        public static readonly int Count = Enum.GetValues(typeof(RnTurnType)).Length;

        // 左折関係か
        public static bool IsLeft(this RnTurnType self)
        {
            return self == RnTurnType.LeftBack || self == RnTurnType.LeftTurn || self == RnTurnType.LeftFront;
        }

        // 右折関係か
        public static bool IsRight(this RnTurnType self)
        {
            return self == RnTurnType.RightBack || self == RnTurnType.RightTurn || self == RnTurnType.RightFront;
        }

        // 流入方向と流出方向から曲がり具合を取得
        public static RnTurnType GetTurnType(Vector3 from, Vector3 to, AxisPlane axis)
        {
            return GetTurnType(from.ToVector2(axis), to.ToVector2(axis));
        }

        // 流入方向と流出方向から曲がり具合を取得
        public static RnTurnType GetTurnType(Vector2 from, Vector2 to)
        {
            var ang = -Vector2.SignedAngle(from, to) + 180f;
            // #NOTE : AVENUEのアルゴリズム
            if (ang is > 10f and < 67.5f)
                return RnTurnType.LeftBack;
            if (ang is >= 67.5f and < 112.5f)
                return RnTurnType.LeftTurn;
            if (ang is >= 112.5f and < 157.5f)
                return RnTurnType.LeftFront;
            if (ang is >= 157.5f and < 202.5f)
                return RnTurnType.Straight;
            if (ang is >= 202.5f and < 247.5f)
                return RnTurnType.RightFront;
            if (ang is >= 247.5f and < 292.5f)
                return RnTurnType.RightTurn;
            if (ang is >= 292.5f and < 337.5f)
                return RnTurnType.RightBack;
            return RnTurnType.UTurn;
        }
    }

    // 交差点への進行タイプ
    [Flags]
    public enum RnFlowTypeMask
    {
        Empty = 0,
        // 流入
        Inbound = 1 << 0,
        // 流出
        Outbound = 1 << 1,
        // 両方
        Both = Inbound | Outbound,
    }

    [Serializable]
    public partial class RnNeighbor : ARnParts<RnNeighbor>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------

        // Roadとの境界線
        public RnWay Border { get; set; }

        // 隣接道路(交差点)基本的にRoadだが、初期のPLATEAUモデルによってはIntersectionもあり得るため基底クラスで持っている
        // 隣接していない場合はnull
        public RnRoadBase Road { get; set; }

        //----------------------------------
        // end: フィールド
        //----------------------------------

        // この境界が道路と接続しているか(中央分離帯含む)
        public bool IsBorder => Road != null;

        // 有効値判定(Border != null)
        public bool IsValid => Border != null;

        /// <summary>
        /// 中央分離帯との境界線か
        /// </summary>
        public bool IsMedianBorder
        {
            get
            {
                if (IsBorder == false)
                    return false;

                if (Road is RnRoad road)
                {
                    if (road.MedianLane != null && road.MedianLane.AllBorders.Any(b => b.IsSameLineReference(Border)))
                        return true;
                }

                return false;
            }
        }

        // この境界線に対して流入/流出するタイプを取得
        public RnFlowTypeMask GetFlowType()
        {
            if (Border == null || Road == null)
                return RnFlowTypeMask.Empty;
            if (Border.IsValid == false)
                return RnFlowTypeMask.Empty;

            if (Road is RnRoad road)
            {
                var ret = RnFlowTypeMask.Empty;
                if (road.GetConnectedLanes(Border).Any(l => l.IsMedianLane == false && l.NextBorder.IsSameLineReference(Border)))
                    ret |= RnFlowTypeMask.Inbound;
                if (road.GetConnectedLanes(Border).Any(l => l.IsMedianLane == false && l.PrevBorder.IsSameLineReference(Border)))
                    ret |= RnFlowTypeMask.Outbound;
                return ret;
            }
            // 交差点同士の接続の場合はレーン全部対象
            else if (Road is RnIntersection)
            {
                return RnFlowTypeMask.Both;
            }

            return RnFlowTypeMask.Empty;
        }

        public Vector3 CalcCenter()
        {
            return Border.GetLerpPoint(0.5f);   // 計算負荷削減のため way[0], way[way.Count-1]の平均でもいいかも
        }

        /// <summary>
        /// この境界とつながっているレーン
        /// </summary>
        /// <returns></returns>
        public RnLane GetConnectedLane()
        {
            return GetConnectedLanes().FirstOrDefault();
        }

        /// <summary>
        /// この境界とつながっているレーンリスト. 基本的に0 or 1
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RnLane> GetConnectedLanes()
        {
            if (Border == null)
                yield break;
            if (Road == null)
                yield break;

            if (Road is RnRoad road)
            {
                foreach (var lane in road.MainLanes)
                {
                    // Borderと同じ線上にあるレーンを返す
                    if (lane.AllBorders.Any(b => b.IsSameLineReference(Border)))
                        yield return lane;
                }
            }
        }
    }

    /// <summary>
    /// 交差点
    /// </summary>
    [Serializable]
    public partial class RnIntersection : RnRoadBase
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------

        // 交差点の外形情報. 時計回り/反時計回りかは保証されていないが, 連結はしている
        private List<RnNeighbor> edges = new List<RnNeighbor>();

        // 信号制御器
        public TrafficSignalLightController SignalController { get; set; } = null;

        // 交差点内のトラック
        private List<RnTrack> tracks = new();

        //　道路と道路の間に入れる空交差点かどうかの判定
        public bool IsEmptyIntersection { get; private set; }


        //----------------------------------
        // end: フィールド
        //----------------------------------

        /// <summary>
        /// 他の道路との境界線Edge取得. edges.Where(e => e.IsBorder)と同義
        /// </summary>
        public IEnumerable<RnNeighbor> Neighbors => edges.Where(e => e.IsBorder);

        /// <summary>
        /// 輪郭のEdge取得
        /// </summary>
        public IReadOnlyList<RnNeighbor> Edges => edges;

        // 交差点内のトラック
        public IReadOnlyList<RnTrack> Tracks => tracks;

        public RnIntersection() { }

        public RnIntersection(PLATEAUCityObjectGroup targetTran)
        {
            AddTargetTran(targetTran);
        }

        public RnIntersection(IEnumerable<PLATEAUCityObjectGroup> targetTrans)
        {
            foreach (var t in targetTrans)
                AddTargetTran(t);
        }

        /// <summary>
        /// 隣接するRnRoadBaseを取得重複チェックはしていないので注意
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<RnRoadBase> GetNeighborRoads()
        {
            foreach (var neighbor in Neighbors)
            {
                if (neighbor.Road != null)
                    yield return neighbor.Road;
            }
        }

        /// <summary>
        /// 隣接道路との境界線を取得
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<NeighborBorder> GetBorders()
        {
            foreach (var edge in Neighbors)
            {
                yield return new NeighborBorder
                {
                    BorderWay = edge.Border,
                    NeighborRoad = edge.Road
                };
            }
        }



        /// <summary>
        /// 隣接情報追加. borderがnullの場合は追加しない
        /// </summary>
        /// <param name="road"></param>
        /// <param name="border"></param>
        public void AddEdge(RnRoadBase road, RnWay border)
        {
            if (border == null)
                return;
            edges.Add(new RnNeighbor { Road = road, Border = border });
        }

        /// <summary>
        /// edgesを差し替え
        /// afterEdgesは必ず連結している者とする
        /// </summary>
        /// <param name="afterEdges"></param>
        public void ReplaceEdges(List<RnNeighbor> afterEdges)
        {
            this.edges = afterEdges;
        }

        public Vector3 GetCenterPoint()
        {
            var ret = Neighbors.SelectMany(n => n.Border.Vertices).Aggregate(Vector3.zero, (a, b) => a + b);
            var cnt = Neighbors.Sum(n => n.Border.Count);
            return ret / cnt;
        }

        /// <summary>
        /// roadとの境界線をbordersに置き換える
        /// </summary>
        /// <param name="road"></param>
        /// <param name="borders"></param>
        /// <param name="reBuildTrack"></param>
        public void ReplaceEdges(RnRoad road, List<RnWay> borders, bool reBuildTrack = true)
        {
            RemoveEdges(n => n.Road == road);
            edges.AddRange(borders.Select(b => new RnNeighbor { Road = road, Border = b }));
        }


        /// <summary>
        /// borderを持つEdgeの隣接道路情報をafterRoadに差し替える.
        /// 戻り値は差し替えが行われた数
        /// </summary>
        /// <param name="border"></param>
        /// <param name="afterRoad"></param>
        public int ReplaceEdgeLink(RnWay border, RnRoadBase afterRoad)
        {
            var replaceCount = 0;
            foreach (var e in edges.Where(e => e.Border.IsSameLineReference(border)))
            {
                e.Road = afterRoad;
                replaceCount++;
            }

            return replaceCount;
        }


        /// <summary>
        /// predicateで指定した隣接情報を削除する
        /// </summary>
        /// <param name="predicate"></param>
        public void RemoveEdges(Func<RnNeighbor, bool> predicate)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                var n = edges[i];
                if (predicate(n))
                {
                    edges.RemoveAt(i);
                    i--;
                    // トラックからも削除する
                    tracks.RemoveAll(x => x.FromBorder == n.Border || x.ToBorder == n.Border);
                }
            }
        }

        public RnTrack FindTrack(RnNeighbor from, RnNeighbor to)
        {
            var c = tracks.FindAll(t => t.FromBorder == from.Border);
            var res = c.FindAll(t => t.ToBorder == to.Border);
            Assert.IsTrue(res.Count <= 1);    // fromとtoの組み合わせが重複することは無いはず
            return res.Count == 0 ? null : res.First();
        }

        public bool ContainTrack(RnNeighbor from, RnNeighbor to)
        {
            return FindTrack(from, to) != null;
        }

        /// <summary>
        /// トラック情報を追加/更新する.
        /// 同じfrom/toのトラックがすでにある場合は上書きする. そうでない場合は追加する
        /// </summary>
        /// <param name="track"></param>
        public bool TryAddOrUpdateTrack(RnTrack track)
        {
            if (track == null)
                return false;
            // trackの入口/出口がこの交差点のものかチェックする
            if (!edges.Any(e =>
                    e.Border.IsSameLineReference(track.FromBorder) || !edges.Any(e => e.Border.IsSameLineReference(track.ToBorder))))
            {
                DebugEx.LogError("交差点に含まれないトラックが追加されようとしています");
                return false;
            }

            // それに同じ物が入口/出口のものがあれば削除する
            tracks.RemoveAll(t => t.IsSameInOut(track));

            // track追加
            tracks.Add(track);
            return true;
        }

        /// <summary>
        /// トラック情報を追加/更新する.
        /// 同じfrom/toのトラックがすでにある場合は上書きする. そうでない場合は追加する
        /// </summary>
        public bool TryAddOrUpdateTrack(RnNeighbor from, RnNeighbor to)
        {
            var turnType = RnTurnTypeEx.GetTurnType(-from.Border.GetEdgeNormal(0).normalized, to.Border.GetEdgeNormal(0).normalized, RnModel.Plane);

            var track = CreateTrack(this, from, to, turnType);
            if (track == null) return false;
            return TryAddOrUpdateTrack(track);

            static RnTrack CreateTrack(RnIntersection inters, RnNeighbor from, RnNeighbor to, RnTurnType edgeTurnType)
            {
                var borderEdgeGroups = inters.CreateEdgeGroup().Where(e => e.IsBorder).ToList();
                var fromEg = borderEdgeGroups.FirstOrDefault(eg => eg.Key == from.Road);
                var toEg = borderEdgeGroups.FirstOrDefault(eg => eg.Key == to.Road);
                if (fromEg == null || toEg == null) return null;
                var thickLinTables = new RnTracksBuilder.ThickCenterLineTables();
                var trackBuilder = new RnTracksBuilder();
                return trackBuilder.MakeTrack(inters, from, BuildTrackOption.Default(), fromEg, thickLinTables, new RnTracksBuilder.OutBound(edgeTurnType, toEg, to));

            }
        }

        public void RemoveTrack(RnTrack track)
        {
            tracks.Remove(track);
        }

        public void RemoveTrack(RnNeighbor from, RnNeighbor to)
        {
            var track = FindTrack(from, to);
            if (track == null)
            {
                Assert.IsTrue(false);   // 現状 Trackは必ず存在する操作しか提供されないはず
                return;
            }

            tracks.Remove(track);
        }

        /// <summary>
        /// 指定した条件のトラックを削除する
        /// </summary>
        /// <param name="predicate"></param>
        public void RemoveTracks(Predicate<RnTrack> predicate)
        {
            tracks.RemoveAll(predicate);
        }

        public void ClearTracks()
        {
            tracks.Clear();
        }

        /// <summary>
        /// road/laneに接続している隣接情報を削除する
        /// </summary>
        /// <param name="road"></param>
        /// <param name="lane"></param>
        public void RemoveEdge(RnRoad road, RnLane lane)
        {
            RemoveEdges(x => x.Road == road && ((lane.PrevBorder?.IsSameLineReference(x.Border) ?? false) || (lane.NextBorder?.IsSameLineReference(x.Border) ?? false)));
        }

        /// <summary>
        /// 自身を切断する
        /// </summary>
        public override void DisConnect(bool removeFromModel)
        {
            base.DisConnect(removeFromModel);
            // リンクを削除する
            foreach (var e in Edges)
            {
                e.Road?.UnLink(this);
                e.Road = null;
            }

            if (removeFromModel)
                ParentModel?.RemoveIntersection(this);
        }

        /// <summary>
        /// 情報を直接書き換えるので呼び出し注意(相互に隣接情報を維持するように書き換える必要がある)
        /// 隣接情報をfrom -> toに変更する.
        /// (from/to側の隣接情報は変更しない)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public override void ReplaceNeighbor(RnRoadBase from, RnRoadBase to)
        {
            if (from == null)
                return;
            foreach (var e in Edges)
            {
                if (e.Road == from)
                    e.Road = to;
            }
        }




        public void BuildTracks(BuildTrackOption op = null)
        {
            new RnTracksBuilder().BuildTracks(this, op);
        }

        public bool IsAligned()
        {
            // 時計回りになっているかどうか
            for (var i = 0; i < edges.Count; ++i)
            {
                var e0 = edges[i];
                var e1 = edges[(i + 1) % edges.Count];
                if (e0.Border.GetPoint(-1) != e1.Border.GetPoint(0))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// edgesの順番を整列する. 各Edgeが連結かつ時計回りになるように整列する
        /// </summary>
        public void Align()
        {
            for (var i = 0; i < edges.Count; ++i)
            {
                var e0 = edges[i].Border;
                for (var j = i + 1; j < edges.Count; ++j)
                {
                    var e1 = edges[j].Border;
                    if (RnPoint.Equals(e0.GetPoint(-1), e1.GetPoint(0)))
                    {
                        (edges[i + 1], edges[j]) = (edges[j], edges[i + 1]);
                        break;
                    }
                    if (RnPoint.Equals(e0.GetPoint(-1), e1.GetPoint(-1)))
                    {
                        e1.Reverse(false);
                        (edges[i + 1], edges[j]) = (edges[j], edges[i + 1]);
                        break;
                    }
                }
            }

            // 時計回りになるように整列
            if (GeoGraph2D.IsClockwise(edges.Select(e => e.Border[0].ToVector2(RnModel.Plane))) == false)
            {
                foreach (var e in edges)
                    e.Border.Reverse(true);
                edges.Reverse();
            }

            // 法線は必ず外向きを向くようにする
            foreach (var e in Edges)
            {
                AlignEdgeNormal(e);
            }
        }

        /// <summary>
        /// 必ず 境界線の間に輪郭線が来るように少し移動させて間に微小なEdgeを追加する
        /// </summary>
        public void SeparateContinuousBorder()
        {
            Align();
            for (var i = 0; i < edges.Count; ++i)
            {
                var e0 = edges[i];
                var e1 = edges[(i + 1) % edges.Count];

                if (e0.IsBorder && e1.IsBorder && e0.Road != e1.Road)
                {
                    var p0 = e0.Border.GetPoint(-1);
                    var p1 = e1.Border.GetPoint(0);

                    // 1cmずらす
                    var offset = 0.01f;
                    if (e0.Border.Count < 2 || e1.Border.Count < 2)
                    {
                        DebugEx.LogError("境界線の頂点数が2未満です");
                        continue;
                    }
                    var newP0 = new RnPoint(e0.Border.GetAdvancedPoint(offset, true));
                    var newP1 = new RnPoint(e1.Border.GetAdvancedPoint(offset, false));
                    var ls = new RnLineString(new[] { newP0, p0, newP1 });

                    static void AdjustPoint(RnNeighbor e, RnPoint oldPoint, RnPoint newPoint)
                    {
                        e.Border.LineString.ReplacePoint(oldPoint, newPoint);
                        foreach (var ls in e.Road?.GetAllLineStringsDistinct() ?? new HashSet<RnLineString>())
                            ls.ReplacePoint(oldPoint, newPoint);
                    }
                    AdjustPoint(e0, p0, newP0);
                    AdjustPoint(e1, p1, newP1);


                    var way = new RnWay(ls, false, true);
                    edges.Insert(i + 1, new RnNeighbor { Road = null, Border = way });
                    i++;
                }
            }
        }
        /// <summary>
        /// 所属するすべてのWayを取得
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<RnWay> AllWays()
        {
            return base.AllWays().Concat(edges.Select(e => e.Border));
        }
        /// <summary>
        /// デバッグ用) その道路の中心を表す代表頂点を返す
        /// </summary>
        /// <returns></returns>
        public override Vector3 GetCentralVertex()
        {
            // 全エッジの中心点の重心を返す
            return Vector3Ex.Centroid(Neighbors
                .Select(n => n.Border)
                .Where(b => b != null)
                .Select(b => b.GetLerpPoint(0.5f)));
        }

        public void SetIsEmptyIntersection(bool val)
        {
            IsEmptyIntersection = val;
        }

        public override bool Check()
        {
            if (IsAligned() == false)
            {
                DebugEx.LogError($"ループしていない輪郭の交差点 {this.GetTargetTransName()}");
                return false;
            }
            return true;
        }
#if false
        /// <summary>
        /// a,bを繋ぐ経路を計算する
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="tangentLength"></param>
        public RnLane CalcTrackWay(RnRoadBase from, RnRoadBase to, float tangentLength = 10f)
        {
            if (from == to)
                return null;
            var nFrom = Neighbors.FirstOrDefault(n => n.Road == from);
            var nTo = Neighbors.FirstOrDefault(n => n.Road == to);
            if (nFrom == null || nTo == null)
                return null;
            if (nFrom.Border.Count < 2 || nTo.Border.Count < 2)
                return null;

            var fromLane = nFrom.GetConnectedLane();
            if (fromLane == null)
                return null;

            var toLane = nTo.GetConnectedLane();
            if (toLane == null)
                return null;


            fromLane.TryGetBorderNormal(nFrom.Border, out var aLeftPos, out var aLeftNormal, out var aRightPos, out var aRightNormal);
            toLane.TryGetBorderNormal(nTo.Border, out var bLeftPos, out var bLeftNormal, out var bRightPos, out var bRightNormal);

            var rightSp = new Spline
            {
                new(aLeftPos, tangentLength * aLeftNormal, tangentLength *aLeftNormal),
                new(bRightPos, tangentLength *bRightNormal, tangentLength *bRightNormal)
            };

            var leftSp = new Spline
            {
                new(aRightPos, tangentLength *aRightNormal, tangentLength *aRightNormal),
                new(bLeftPos, tangentLength *bLeftNormal, tangentLength *bLeftNormal)
            };

            var rates = Enumerable.Range(0, 10).Select(i => 1f * i / (9)).ToList();
            var leftWay = new RnWay(RnLineString.Create(rates.Select(t =>
            {
                leftSp.Evaluate(t, out var pos, out var tang, out var up);
                return (Vector3)pos;
            })));
            var rightWay = new RnWay(RnLineString.Create(rates.Select(t =>
            {
                rightSp.Evaluate(t, out var pos, out var tang, out var up);
                return (Vector3)pos;
            })));
            return new RnLane(leftWay, rightWay, nFrom.Border, nTo.Border);
        }
#endif
        // ---------------
        // Static Methods
        // ---------------
        public static RnIntersection CreateEmptyIntersection(List<RnWay> borderLeft2Right, RnRoad prev, RnRoad next)
        {
            var ret = new RnIntersection
            {
                IsEmptyIntersection = true,
                edges = new List<RnNeighbor>(borderLeft2Right.Count * 2)
            };

            foreach (var border in borderLeft2Right)
            {
                var neighbor = new RnNeighbor { Road = prev, Border = border };
                ret.edges.Add(neighbor);

                var pos = border.GetLerpPoint(0.5f);
                var dir = border.GetEdgeNormal(0).normalized;

                var (from, to) = (new BezierKnot(pos, dir, -dir), new BezierKnot(pos, -dir, dir));

                var flow = neighbor.GetFlowType();
                if ((flow & RnFlowTypeMask.Inbound) != 0)
                {
                    ret.tracks.Add(new RnTrack(border, border, new Spline { from, to }));
                }
                else if ((flow & RnFlowTypeMask.Outbound) != 0)
                {
                    (to, from) = (from, to);
                    ret.tracks.Add(new RnTrack(border, border, new Spline { from, to }));
                }
            }

            foreach (var border in borderLeft2Right.Reversed())
            {
                ret.edges.Add(new RnNeighbor { Road = next, Border = border.ReversedWay() });
            }
            return ret;
        }

        /// <summary>
        /// 輪郭線の法線方向を外側向くように整える
        /// </summary>
        /// <param name="edge"></param>
        private static void AlignEdgeNormal(RnNeighbor edge)
        {
            if (edge.Border.IsReverseNormal)
                edge.Border.IsReverseNormal = false;
        }

        /// <summary>
        /// edgeの法線方向取得(外側を向いているはず)
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public static Vector3 GetEdgeNormal(RnNeighbor edge)
        {
            // 一応向きを整える
            AlignEdgeNormal(edge);
            return edge.Border.GetEdgeNormal((edge.Border.Count - 1) / 2);
        }

        /// <summary>
        /// edgeの法線方向(2D)取得(外側を向いているはず)
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public static Vector2 GetEdgeNormal2D(RnNeighbor edge)
        {
            return GetEdgeNormal(edge).GetTangent(RnDef.Plane);
        }

        /// <summary>
        /// edgeの中心点取得
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public static Vector3 GetEdgeCenter(RnNeighbor edge)
        {
            return edge.Border.GetLerpPoint(0.5f);
        }

        /// <summary>
        /// edgeの中心点(2D)取得
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public static Vector2 GetEdgeCenter2D(RnNeighbor edge)
        {
            return GetEdgeCenter(edge).ToVector2(RnDef.Plane);
        }

    }

    public static class RnIntersectionEx
    {
        public class EdgeGroup
        {
            public RnRoadBase Key => Edges.First().Road;

            // 時計回りになるように入っている
            // =(交差点の外から見て0が右側)
            public List<RnNeighbor> Edges { get; } = new();

            public bool IsBorder => Key != null;

            // 流入してくるボーダー
            public IEnumerable<RnNeighbor> InBoundEdges => Edges.Where(n => (n.GetFlowType() & RnFlowTypeMask.Inbound) != 0);

            // 流出するボーダー
            public IEnumerable<RnNeighbor> OutBoundEdges => Edges.Where(n => (n.GetFlowType() & RnFlowTypeMask.Outbound) != 0);

            // 右側
            public EdgeGroup RightSide { get; set; }

            // 左側
            public EdgeGroup LeftSide { get; set; }

            // 有効なエッジかどうか
            public bool IsValid => Edges.Count > 0 && Edges.All(x => x.Border.IsValidOrDefault());

            // 法線方向
            public Vector3 Normal => Edges.First(x => x.Border.IsValidOrDefault()).Border.GetEdgeNormal(0).normalized;
        };

        /// <summary>
        /// 交差点のEdgesをRoadごとにグループ化する.
        /// RoadA -> null(境界線じゃない部分) -> RoadB -> null -> RoadC -> null -> RoadAのようになる.
        /// 順番はEdgesの順番を保持する
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static List<EdgeGroup> CreateEdgeGroup(this RnIntersection self)
        {
            if (self.Edges.Count == 0)
                return new List<EdgeGroup>();
            self.Align();
            var ret = new List<EdgeGroup> { new() };

            for (var i = 0; i < self.Edges.Count; ++i)
            {
                var eg = ret[^1];
                var e0 = self.Edges[i];
                if (eg.Edges.Any() == false || eg.Key == e0.Road)
                {
                    eg.Edges.Add(e0);
                }
                else
                {
                    var next = new EdgeGroup();
                    next.Edges.Add(e0);
                    eg.LeftSide = next;
                    next.RightSide = eg;
                    ret.Add(next);
                }
            }

            if (ret.Count <= 1)
                return ret;

            if (ret[0].Key == ret[^1].Key)
            {
                ret[^1].Edges.AddRange(ret[0].Edges);
                ret.RemoveAt(0);
            }
            ret[0].RightSide = ret[^1];
            ret[^1].LeftSide = ret[0];

            return ret;
        }

        /// <summary>
        /// borderWayで指定したRnNeighborを取得する(基本的に0か1)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="borderWay"></param>
        /// <returns></returns>
        public static IEnumerable<RnNeighbor> FindEdges(this RnIntersection self, RnWay borderWay)
        {
            if (self == null || borderWay == null)
                return Enumerable.Empty<RnNeighbor>();

            return self.Edges.Where(e => e.Border?.IsSameLineReference(borderWay) ?? false);
        }

        /// <summary>
        /// 交差点内で各道路への中央ラインを通る経路グラフ
        /// </summary>

        public class CenterLineGraph
        {
            /// <summary>
            /// ボロノイ図の各点
            /// </summary>
            public class SitePoint
            {
                public RnNeighbor Edge { get; set; }

                public RnPoint Point { get; set; }
            }

            /// <summary>
            /// グラフを構成する点
            /// </summary>
            public class SiteNode
            {
                // 位置
                public RnPoint Vertex { get; set; }

                // 接続ノード
                public List<SiteNode> Neighbors { get; } = new();

                /// <summary>
                /// 依存するボロノイ辺情報
                /// </summary>
                public HashSet<VoronoiData<SitePoint>.Edge> SiteEdges { get; } = new();

                /// <summary>
                /// 自身が道路の入り口を表す場合. その道路の参照. そうでない場合はnull
                /// </summary>
                public RnRoadBase StartKey { get; set; }

                public bool IsAlongTo(EdgeGroup eg)
                {
                    var lt = eg.LeftSide.Edges[0];
                    var rt = eg.RightSide.Edges[0];
                    return SiteEdges.Any(e =>
                    {
                        var l = e.LeftSitePoint.Edge;
                        var r = e.RightSitePoint.Edge;
                        return (l == lt || r == lt) && (l == rt || r == rt);
                    });
                }
            }

            /// <summary>
            /// 中央ラインの各点
            /// </summary>
            public List<SiteNode> Nodes { get; } = new();

            /// <summary>
            /// Key   : 起点となるRnRoadBase(交差点に接続している道路)
            /// Value : (Key : 終点となるRnRoadBase(交差点に接続している道路), Value : 経路)
            /// </summary>
            public Dictionary<RnRoadBase, Dictionary<RnRoadBase, RnWay>> CenterLines { get; } = new();
        }

        /// <summary>
        /// 交差点内で各道路への中央ラインを通る経路グラフを作成する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="refineInterval"></param>
        /// <returns></returns>
        public static CenterLineGraph CreateCenterLineGraph(this RnIntersection self, float refineInterval = 3f)
        {
            // 境界線じゃない外周部分のedge取得
            var outlines = self.Edges.Where(e => e.IsBorder == false);

            // 十分に細かい線分に分割したうえでボロノイ分割を行う.
            // 左右で別のEdgeGroupのボロノイ辺を繋ぐと中央ラインが見えてくる
            var vs =
                outlines.SelectMany(e => e.Border.LineString.Refined(refineInterval).Points.Select(p => new CenterLineGraph.SitePoint { Edge = e, Point = p })).ToList();
            var vd = RnVoronoiEx.CalcVoronoiData(vs, v => new Vector2d(v.Point.Vertex.Xz()));

            var ret = new CenterLineGraph();
            CenterLineGraph.SiteNode AddNode(Vector2d? v, VoronoiData<CenterLineGraph.SitePoint>.Edge e)
            {
                if (v == null)
                    return null;

                var l = e.LeftSitePoint.Point.Vertex;
                var r = e.RightSitePoint.Point.Vertex;

                var lenL = (v.Value.ToVector2() - l.Xz()).magnitude;
                var lenR = (v.Value.ToVector2() - r.Xz()).magnitude;

                var y = Mathf.Lerp(l.y, r.y, lenL / (lenL + lenR));
                var vertex = new Vector3((float)v.Value.x, y, (float)v.Value.y);

                var n = ret.Nodes.FirstOrDefault(n => (n.Vertex.Vertex.Xz() - vertex.Xz()).magnitude < 0.5f);
                if (n == null)
                {
                    n = new CenterLineGraph.SiteNode { Vertex = new RnPoint(vertex) };
                    ret.Nodes.Add(n);
                }

                n.SiteEdges.Add(e);

                return n;
            }

            foreach (var e in vd.Edges)
            {
                // 同じEdge同士は無視
                if (e.LeftSitePoint.Edge == e.RightSitePoint.Edge)
                    continue;

                var st = AddNode(e.Start, e);
                var en = AddNode(e.End, e);
                if (st != null && en != null)
                {
                    st?.Neighbors?.Add(en);
                    en?.Neighbors?.Add(st);
                }
            }

            foreach (var eg in self.CreateEdgeGroup())
            {
                if (eg.Key == null)
                    continue;

                var minDistance = float.MaxValue;
                CenterLineGraph.SiteNode minSiteNode = null;
                foreach (var n in ret.Nodes)
                {
                    if (n.IsAlongTo(eg) == false)
                        continue;

                    var ls = RnLineString.Create(eg.Edges.Select(e => e.Border).SelectMany(e => e.Points));
                    var way = new RnWay(ls, false, false);
                    if (way.IsOutSide(n.Vertex, out var nearest, out var distance))
                        continue;

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        minSiteNode = n;
                    }
                }

                if (minSiteNode != null)
                {
                    minSiteNode.StartKey = eg.Key;
                }
            }

            // ダイクストラ法でお互いの道路までの経路を求める
            Dictionary<RnRoadBase, RnWay> Dijkstra(CenterLineGraph.SiteNode start)
            {
                Dictionary<CenterLineGraph.SiteNode, (float len, CenterLineGraph.SiteNode last)> dict = new();
                HashSet<CenterLineGraph.SiteNode> visited = new();
                dict[start] = (0, null);
                while (dict.Any(x => visited.Contains(x.Key) == false))
                {
                    dict.Where(x => visited.Contains(x.Key) == false).TryFindMinElement(x => x.Value.len, out var e);
                    visited.Add(e.Key);
                    foreach (var n in e.Key.Neighbors)
                    {
                        var d = e.Value.len + (n.Vertex.Vertex - e.Key.Vertex.Vertex).magnitude;
                        if (dict.TryAdd(n, (d, e.Key)) == false)
                        {
                            if (d < dict[n].len)
                            {
                                dict[n] = (d, e.Key);
                            }
                        }
                    }
                }

                var paths = new Dictionary<RnRoadBase, RnWay>();
                foreach (var x in ret.Nodes.Where(x => x.StartKey != null && x != start))
                {
                    if (dict.ContainsKey(x) == false)
                        continue;

                    var points = new List<RnPoint>();
                    points.Add(x.Vertex);
                    var n = x;
                    while (n != null && dict.ContainsKey(n))
                    {
                        n = dict[n].last;
                        if (n != null)
                            points.Add(n.Vertex);
                    }

                    points.Reverse();
                    paths[x.StartKey] = new RnWay(RnLineString.Create(points));
                }

                return paths;
            }

            foreach (var n in ret.Nodes.Where(x => x.StartKey != null))
                ret.CenterLines[n.StartKey] = Dijkstra(n);

            return ret;
        }

        public static CenterLineGraph CreateCenterLineGraphOrDefault(this RnIntersection self, float refineInterval = 3f)
        {
            try
            {
                return self.CreateCenterLineGraph(refineInterval);
            }
            catch (Exception e)
            {
                // #FIXME : 例外が発生する状況もおかしいので要チェック
                DebugEx.LogWarning(e);
                return null;
            }
        }

        /// <summary>
        /// borderWayで指定した境界線と繋がっているレーンリスト(基本的に0か1)
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<RnLane> GetConnectedLanes(this RnIntersection self, RnWay borderWay)
        {
            return self.FindEdges(borderWay).SelectMany(n => n.GetConnectedLanes());
        }


        /// <summary>
        /// selfの全LinestringとlineSegmentの交点を取得する. ただし2D平面に射影したうえでの交点を返す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="lineSegment"></param>
        /// <returns></returns>
        public static LineCrossPointResult GetEdgeCrossPoints(this RnIntersection self, LineSegment3D lineSegment)
        {
            var targetLines = self.Edges
                .Select(l => l.Border)
                .Concat(self.SideWalks.SelectMany(s => s.SideWays));
            return RnEx.GetLineIntersections(lineSegment, targetLines);
        }

        /// <summary>
        /// 2D平面に射影したうえでの内部判定
        /// </summary>
        /// <param name="self"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public static bool IsInside2D(this RnIntersection self, Vector3 pos)
        {
            List<RnPoint> points = new List<RnPoint>(self.Edges.Sum(x => x.Border.Count));
            foreach (var p in self.Edges.SelectMany(e => e.Border.Points))
            {
                if (points.Any() && points.Last() == p)
                    continue;
                points.Add(p);
            }
            if (points.Count > 1 && points[0] == points[^1])
                points.RemoveAt(points.Count - 1);
            return GeoGraph2D.IsInsidePolygon(pos.Xz(), points.Select(x => x.Vertex.Xz()).ToList());
        }

#if false
        public class RecLine
        {
            public class PartialWay
            {
                public RnWay Way { get; set; }

                public RnPoint Start { get; set; }

                public RnPoint End { get; set; }

                public PartialWay(RnWay way) : this(way, way.GetPoint(0), way.GetPoint(-1))
                {
                }

                public PartialWay(RnWay way, RnPoint start, RnPoint end)
                {
                    Way = way;
                    Start = start;
                    End = end;
                }

                public PartialWayWork ToWork()
                {
                    return new PartialWayWork(this);
                }
            }

            // right -> left方向（時計回り)に格納されている
            public List<RnWay> Lines { get; } = new();

            public List<RecLine> Parents { get; set; } = new();

            public PartialWay LeftSide { get; set; }

            public PartialWay RightSide { get; set; }

            public class PartialWayWork : IList<RnPoint>
            {
                public RnWay Way { get; }

                // 開始インデックス
                public int StartIndex { get; private set; }

                // 終了インデックス
                public int EndIndex { get; private set; }

                public PartialWayWork(PartialWay way)
                {
                    Way = way.Way;
                    StartIndex = way.Way.FindPointIndex(way.Start);
                    EndIndex = way.Way.FindPointIndex(way.End) + 1;
                }


                public IEnumerator<RnPoint> GetEnumerator()
                {
                    for (var i = StartIndex; i < EndIndex; ++i)
                        yield return Way.GetPoint(i);
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    return GetEnumerator();
                }

                public void Add(RnPoint item)
                {
                    Insert(Count, item);
                }

                public void Clear()
                {
                    // Clearは許可しない
                    throw new NotImplementedException();
                }

                public bool Contains(RnPoint item)
                {
                    return Way.Points.Skip(StartIndex).Take(EndIndex - StartIndex).Contains(item);
                }

                public void CopyTo(RnPoint[] array, int arrayIndex)
                {
                    // CopyToは許可しない
                    throw new NotImplementedException();
                }

                public bool Remove(RnPoint item)
                {
                    // Removeは許可しない
                    throw new NotImplementedException();
                }

                public int Count => EndIndex - StartIndex;

                public bool IsReadOnly => false;

                public int IndexOf(RnPoint item)
                {
                    var wayIndex = Way.FindPointIndex(item);
                    var index = FromWayIndex(wayIndex);
                    // 範囲外
                    if (index < 0 || index >= EndIndex)
                        return -1;
                    return index;
                }

                public void Insert(int index, RnPoint item)
                {
                    // Addと共通なのでCountも許可する
                    if (index < 0 || index > Count)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    var lineStringIndex = Way.SwitchIndex(ToWayIndex(index));
                    Way.LineString.Points.Insert(lineStringIndex, item);
                    EndIndex++;
                }

                public void RemoveAt(int index)
                {
                    if (index < 0 || index >= Count)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    var lineStringIndex = Way.SwitchIndex(ToWayIndex(index));
                    Way.LineString.Points.RemoveAt(lineStringIndex);
                    EndIndex--;
                }

                public RnPoint this[int index]
                {
                    get
                    {
                        if (index < 0 || index >= Count)
                            throw new ArgumentOutOfRangeException(nameof(index));
                        return Way.GetPoint(ToWayIndex(index));
                    }
                    // setは許可しない
                    set => throw new NotImplementedException();
                }

                public RnPoint Project(Vector3 pos, Vector3 dir)
                {
                    var nearest = Vector3.zero;
                    float len = float.MaxValue;
                    int minIndex = -1;
                    var index = 0;

                    foreach (var s in GeoGraphEx.GetEdges(this.Select(p => p.Vertex), false))
                    {
                        var seg = new LineSegment3D(s.Item1, s.Item2);

                        if (seg.TryHalfLineIntersectionBy2D(pos, dir, AxisPlane.Xz, -1f, out var v, out var t1,
                                out var _))
                        {
                            v = seg.Lerp(t1);
                            if ((v - pos).sqrMagnitude < len)
                            {
                                minIndex = index;
                                len = (v - pos).sqrMagnitude;
                                nearest = v;
                            }
                        }

                        index++;
                    }

                    if (minIndex < 0)
                        return null;

                    var tolerance = 1e-1f;
                    if ((this[minIndex] - nearest).magnitude < tolerance)
                    {
                        if (minIndex == 0)
                            return null;
                        return this[minIndex];
                    }

                    if ((this[minIndex + 1] - nearest).magnitude < 1e-1f)
                        return this[minIndex + 1];

                    var p = new RnPoint(nearest);
                    Insert(minIndex + 1, p);
                    return p;
                }

                private int ToWayIndex(int index)
                {
                    return index + StartIndex;
                }

                private int FromWayIndex(int index)
                {
                    return index - StartIndex;
                }
            }

            public RecLine CreateChild()
            {
                Vector2 Vec2(Vector3 v) => v.ToVector2(AxisPlane.Xz);

                var leftWay = new PartialWayWork(LeftSide);
                var rightWay = new PartialWayWork(RightSide);
                var leftSides = new List<RnPoint> { leftWay[0], leftWay[1] };
                var rightSides = new List<RnPoint> { rightWay[0], rightWay[1] };

                float GetLastLength(List<RnPoint> sides)
                {
                    return Vec2(sides[^1].Vertex - sides[^2].Vertex).magnitude;
                }
                var leftLength = GetLastLength(leftSides);
                var rightLength = GetLastLength(rightSides);

                while (leftSides.Count < leftWay.Count || rightSides.Count < rightWay.Count)
                {
                    var isLeft = leftSides.Count < leftWay.Count &&
                                 (rightSides.Count >= rightWay.Count || leftLength < rightLength);
                    if (isLeft)
                    {
                        leftSides.Add(leftWay[leftSides.Count]);
                    }
                    else
                    {
                        rightSides.Add(rightWay[rightSides.Count]);
                    }

                    var convex = GeoGraph2D.ComputeConvexVolume(
                        leftSides.Concat(rightSides)
                        , v => v.Vertex, AxisPlane.Xz, sameLineTolerance: 1e-3f).ToHashSet();

                    if (convex.Count != (leftSides.Count + rightSides.Count))
                    {
                        if (isLeft)
                            leftSides.RemoveAt(leftSides.Count - 1);
                        else
                            rightSides.RemoveAt(rightSides.Count - 1);
                        break;
                    }

                    if (isLeft)
                        leftLength += GetLastLength(leftSides);
                    else
                        rightLength += GetLastLength(rightSides);
                }

                var left = new PartialWay(leftWay.Way, leftSides[0], leftSides[^1]);
                var right = new PartialWay(rightWay.Way, rightSides[0], rightSides[^1]);

                var right2LeftDir = (leftSides[0].Vertex - rightSides[0].Vertex);
                if (leftLength < rightLength)
                {
                    var rp = rightWay.Project(leftSides[^1].Vertex, -right2LeftDir);
                    if (rp != null)
                        right.End = rp;
                }
                else
                {
                    var lp = leftWay.Project(rightSides[^1].Vertex, right2LeftDir);
                    if (lp != null)
                        left.End = lp;
                }

                var ret = new RecLine
                {
                    LeftSide = left,
                    RightSide = right,
                    Parents = new List<RecLine> { this }
                };
                var lens = Lines.Select(l => l.CalcLength()).ToList();

                var sum = lens.Sum();
                var w = 0f;
                var last = right.End;
                foreach (var f in lens)
                {
                    w += f / sum;
                    var p = w > 1 - 1e-3f ? left.End : new RnPoint(Vector3.Lerp(right.End.Vertex, left.End.Vertex, w));
                    ret.Lines.Add(new RnWay(RnLineString.Create(new[] { last, p })));
                    last = p;
                }

                return ret;
            }
        }

        public static List<RecLine> CreateRecLine(this RnIntersection self)
        {
            var edgeGroups = self.CreateEdgeGroup();
            var ret = new List<RecLine>();
            Dictionary<RnLineString, RnLineString> buffer = new();

            RecLine.PartialWay PartialWay(RnWay way)
            {
                var line = buffer.GetValueOrCreate(way.LineString, ls => ls.Clone());
                var w = new RnWay(line, way.IsReversed, way.IsReverseNormal);
                return new RecLine.PartialWay(w);
            }

            foreach (var index in Enumerable.Range(0, edgeGroups.Count))
            {
                var e = edgeGroups[index];
                if (e.Key != null)
                {
                    var rc = new RecLine();
                    rc.Lines.AddRange(e.Edges.Select(n => n.Border));
                    var right = edgeGroups[(index - 1 + edgeGroups.Count) % edgeGroups.Count];
                    var left = edgeGroups[(index + 1) % edgeGroups.Count];
                    rc.LeftSide = PartialWay(left.Edges.First().Border);
                    rc.RightSide = PartialWay(right.Edges.First().Border.ReversedWay());
                    ret.Add(rc);
                }
            }
            return ret;
        }


        /// <summary>
        /// a,bを繋ぐ経路を計算する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="fromBorder"></param>
        /// <param name="toBorder"></param>
        /// <param name="tangentLength"></param>
        /// <param name="splitLength"></param>
        public static RnWay CalcTrackWay(this RnIntersection self, RnWay fromBorder, RnWay toBorder, float tangentLength = 10f, float splitLength = 2f)
        {
            if (fromBorder == toBorder)
                return null;
            var nFrom = self.Neighbors.FirstOrDefault(n => n.Border == fromBorder);
            var nTo = self.Neighbors.FirstOrDefault(n => n.Border == toBorder);
            if (nFrom == null || nTo == null)
                return null;
            if (nFrom.Border.Count < 2 || nTo.Border.Count < 2)
                return null;

            var fromLane = nFrom.GetConnectedLane();
            if (fromLane == null)
                return null;

            var toLane = nTo.GetConnectedLane();
            if (toLane == null)
                return null;


            fromLane.TryGetBorderNormal(nFrom.Border, out var aLeftPos, out var aLeftNormal, out var aRightPos, out var aRightNormal);
            toLane.TryGetBorderNormal(nTo.Border, out var bLeftPos, out var bLeftNormal, out var bRightPos, out var bRightNormal);

            fromBorder.GetLerpPoint(0.5f, out var fromPos);
            toBorder.GetLerpPoint(0.5f, out var toPos);

            var rightSp = new Spline
            {
                new(fromPos, tangentLength * aLeftNormal, tangentLength *aLeftNormal),
                new(toPos, tangentLength *bRightNormal, tangentLength *bRightNormal)
            };

            var num = Mathf.Max(2, Mathf.CeilToInt((fromPos - toPos).magnitude / splitLength));
            var rates = Enumerable.Range(0, num).Select(i => 1f * i / (num - 1)).ToList();
            return new RnWay(RnLineString.Create(rates.Select(t =>
            {
                rightSp.Evaluate(t, out var pos, out var tang, out var up);
                return (Vector3)pos;
            })));
        }
#endif
    }
}