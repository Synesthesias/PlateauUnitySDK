using PLATEAU.Util;
using PLATEAU.Util.GeoGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Structure
{
    /// <summary>
    /// WayのPointsをReadOnlyで返すラッパー
    /// </summary>
    public class RnWayPoints : IReadOnlyList<RnPoint>
    {
        private readonly RnWay way;

        public RnWayPoints(RnWay way)
        {
            this.way = way;
        }

        public IEnumerator<RnPoint> GetEnumerator()
        {
            return way.Points.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => way.Count;

        public RnPoint this[int index] => way.GetPoint(index);
    }

    public class RnWayEqualityComparer : IEqualityComparer<RnWay>
    {
        // 同じLineStringであれば同一判定とする
        public bool SameLineIsEqual { get; set; } = true;

        /// <summary>
        /// sameLineIsEqual : 同じLineStringであれば同一判定とする
        /// </summary>
        /// <param name="sameLineIsEqual"></param>
        public RnWayEqualityComparer(bool sameLineIsEqual) { SameLineIsEqual = sameLineIsEqual; }

        public bool Equals(RnWay x, RnWay y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            if (SameLineIsEqual)
            {
                return x.IsSameLine(y);
            }

            return x.IsReversed == y.IsReversed && x.IsReverseNormal == y.IsReverseNormal && Equals(x.LineString, y.LineString);
        }

        public int GetHashCode(RnWay obj)
        {
            if (SameLineIsEqual)
                return obj.LineString.GetHashCode();
            return HashCode.Combine(obj.IsReversed, obj.IsReverseNormal, obj.LineString);
        }
    }

    /// <summary>
    /// 方向を持つ線分クラス. 車線を構成する1ライン
    /// 同じ線分だけど向きが逆ということが多々あるのでメモリ削減 & 比較を楽にするため
    /// </summary>
    [Serializable]
    public class RnWay : ARnParts<RnWay>, IReadOnlyList<Vector3>
    {
        //----------------------------------
        // start: フィールド
        //----------------------------------
        // LineStringの向きが逆かどうか
        public bool IsReversed { get; set; } = false;

        // 法線計算用. 進行方向左側が道かどうか
        public bool IsReverseNormal { get; set; } = false;

        // 頂点群
        public RnLineString LineString { get; private set; }

        //----------------------------------
        // end: フィールド
        //----------------------------------

        /// <summary>
        /// 頂点情報を返す
        /// </summary>
        public IEnumerable<Vector3> Vertices
        {
            get
            {
                for (var i = 0; i < Count; i++)
                    yield return this[i];
            }
        }

        /// <summary>
        /// 頂点情報をPoint型で返す(頂点変更できるように)
        /// </summary>
        public IEnumerable<RnPoint> Points
        {
            get
            {
                for (var i = 0; i < Count; i++)
                    yield return GetPoint(i);
            }
        }

        /// <summary>
        /// 頂点取得
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public RnPoint GetPoint(int index)
        {
            // 負数の時は逆からのインデックスに変換
            var i = ToRawIndex(index, true);
            return LineString.Points[i];
        }

        /// <summary>
        /// 頂点書き換え. 戻り値は変更前の値
        /// </summary>
        /// <param name="index"></param>
        /// <param name="p"></param>
        public RnPoint SetPoint(int index, RnPoint p)
        {
            var i = ToRawIndex(index, true);
            var ret = LineString.Points[i];
            LineString.Points[i] = p;
            return ret;
        }

        // 頂点数
        public int Count => LineString?.Count ?? 0;

        // 2頂点以上ある有効な道かどうか
        public bool IsValid => LineString?.IsValid ?? false;

        public RnWay(RnLineString lineString, bool isReversed = false, bool isReverseNormal = false)
        {
            LineString = lineString;
            IsReversed = isReversed;
            IsReverseNormal = isReverseNormal;
        }

        // デシリアライズのために必要
        public RnWay() { }

        /// <summary>
        /// 反転させたWayを返す(非破壊)
        /// </summary>
        /// <returns></returns>
        public RnWay ReversedWay()
        {
            return new RnWay(LineString, !IsReversed, !IsReverseNormal);
        }

        /// <summary>
        /// 線の向きを反転させる
        /// </summary>
        /// <param name="keepNormalDir">法線の向きは保持する</param>
        public void Reverse(bool keepNormalDir)
        {
            IsReversed = !IsReversed;
            // #NOTE : 反転させた段階で法線も逆になるのでkeepするときにIsReverseNormalを反転させる
            if (keepNormalDir)
                IsReverseNormal = !IsReverseNormal;
        }

        /// <summary>
        /// Reversedを考慮したインデックスへ変換する
        /// </summary>
        /// <param name="index"></param>
        /// <param name="allowMinus">負数の場合は逆から検索</param>
        /// <returns></returns>
        private int ToRawIndex(int index, bool allowMinus = false)
        {
            if (allowMinus && index < 0)
                index = Count + index;
            return IsReversed ? Count - 1 - index : index;
        }

        /// <summary>
        /// IsReversed ? Count - 1 - index : index
        /// LineStringとWayのインデックスの相互変換
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public int SwitchIndex(int index)
        {
            return IsReversed ? Count - 1 - index : index;
        }

        /// <summary>
        /// 頂点アクセス
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vector3 this[int index]
        {
            get
            {
                return LineString[ToRawIndex(index)];
            }
        }

        /// <summary>
        /// 頂点 vertexIndex -> vertexIndex, vertexIndex -> vertexIndex + 1の方向に対して
        /// 道の外側を向いている法線ベクトルの平均を返す.正規化はされていない
        /// </summary>
        /// <param name="vertexIndex"></param>
        /// <returns></returns>
        public Vector3 GetVertexNormal(int vertexIndex)
        {
            // 頂点数1の時は不正値を返す
            if (Count <= 1)
                return Vector3.zero;

            var ret = LineString.GetVertexNormal(ToRawIndex(vertexIndex));
            if (IsReversed != IsReverseNormal)
                ret *= -1;
            return ret;
        }

        /// <summary>
        /// 頂点の法線ベクトルをリストで返す
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Vector3> GetVertexNormals()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return GetVertexNormal(i);
            }
        }

        /// <summary>
        /// 頂点 startVertexIndex, startVertexIndex + 1で構成される辺の法線ベクトルを返す
        /// 道の外側を向いている. 正規化はされていない
        /// </summary>
        /// <param name="startVertexIndex"></param>
        /// <returns></returns>
        public Vector3 GetEdgeNormal(int startVertexIndex)
        {
            var index = ToRawIndex(startVertexIndex);
            // LineStringのGetEdgeNormalはindex, index+1で見るようになっているので
            // 逆方向の時は-1する必要がある
            if (IsReversed)
                index -= 1;
            var ret = LineString.GetEdgeNormal(index);
            if (IsReversed != IsReverseNormal)
                ret *= -1;
            return ret;
        }

        /// <summary>
        /// 辺の法線ベクトルをリストで返す
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Vector3> GetEdgeNormals()
        {
            for (var i = 0; i < Count - 1; i++)
            {
                yield return GetEdgeNormal(i);
            }
        }

        /// <summary>
        /// Xz平面だけで見たときの, 半直線rayの最も近い交点を返す
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="intersection"></param>
        /// <returns></returns>
        public bool HalfLineIntersectionXz(Ray ray, out Vector3 intersection)
        {
            var ray2d = new Ray2D { direction = ray.direction.Xz(), origin = ray.origin.Xz() };

            intersection = Vector3.zero;
            var minLen = float.MaxValue;
            for (var i = 0; i < Count - 1; ++i)
            {
                var p1 = this[i];
                var p2 = this[i + 1];
                if (LineUtil.HalfLineSegmentIntersection(ray2d, p1.Xz(), p2.Xz(), out Vector2 _, out var t1, out var t2))
                {
                    var inter3d = Vector3.Lerp(p1, p2, t2);
                    var len = (inter3d - ray.origin).sqrMagnitude;
                    if (len < minLen)
                    {
                        minLen = len;
                        intersection = inter3d;
                    }
                }
            }
            return minLen < float.MaxValue;
        }

        /// <summary>
        /// Xz平面だけで見たときの, 線分(st, en)との最も近い交点を返す
        /// </summary>
        /// <param name="st"></param>
        /// <param name="en"></param>
        /// <param name="intersection"></param>
        /// <returns></returns>
        public bool SegmentIntersectionXz(Vector3 st, Vector3 en, out Vector3 intersection)
        {
            var st2d = st.Xz();
            var en2d = en.Xz();

            intersection = Vector3.zero;
            var minLen = float.MaxValue;
            for (var i = 0; i < Count - 1; ++i)
            {
                var p1 = this[i];
                var p2 = this[i + 1];
                if (LineUtil.SegmentIntersection(st2d, en2d, p1.Xz(), p2.Xz(), out Vector2 _, out var t1, out var t2))
                {
                    var inter3d = Vector3.Lerp(p1, p2, t2);
                    var len = (inter3d - st).sqrMagnitude;
                    if (len < minLen)
                    {
                        minLen = len;
                        intersection = inter3d;
                    }
                }
            }
            return minLen < float.MaxValue;
        }

        /// <summary>
        /// 境界線の中央の点を返す
        /// 線分の距離をp : (1-p)で分割した点をmidPointに入れて返す. 戻り値は midPointを含む線分のインデックス(i ~ i+1の線分上にmidPointがある) 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="midPoint"></param>
        /// <returns></returns>
        public int GetLerpPoint(float p, out Vector3 midPoint)
        {
            return LineUtil.GetLineSegmentLerpPoint(this, p, out midPoint);
        }

        public Vector3 GetLerpPoint(float p)
        {
            GetLerpPoint(p, out var midPoint);
            return midPoint;
        }

        /// <summary>
        /// 自身をnum分割して返す. 分割できない(頂点空）の時は空リストを返す
        /// </summary>
        /// <returns></returns>
        public List<RnWay> Split(int num, bool insertNewPoint, Func<int, float> rateSelector = null)
        {
            var ret = LineString.Split(num, insertNewPoint, rateSelector).Select(s => new RnWay(s, IsReversed, IsReverseNormal)).ToList();
            if (IsReversed)
                ret.Reverse();
            return ret;
        }

        /// <summary>
        /// 法線に沿って移動する
        /// </summary>
        /// <param name="offset"></param>
        public void MoveAlongNormal(float offset)
        {
            if (IsValid == false)
                return;

            // 頂点数が2の時は特殊処理
            if (Count == 2)
            {
                var n = GetEdgeNormal(0).normalized;
                foreach (var p in Points)
                    p.Vertex += n * offset;

                return;
            }

            var index = 0;
            // 現在見る点と次の点の辺/頂点の法線を保存しておく
            // 線分の法線
            var edgeNormal = new[] { GetEdgeNormal(0).normalized, GetEdgeNormal(Mathf.Min(Count - 1, 1)).normalized };
            // 頂点の法線
            var vertexNormal = new[] { GetVertexNormal(0), GetVertexNormal(1) };
            var delta = offset;
            for (var i = 0; i < Count; ++i)
            {
                var en0 = edgeNormal[index];
                var en1 = edgeNormal[(index + 1) & 1];
                var vn = vertexNormal[index];

                // 形状維持するためにオフセット距離を変える
                // en0成分の移動量がdeltaになるように, vnの移動量を求める
                var m = Vector3.Dot(vn, en0);
                // p0->p1->p2でp0 == p2だったりした場合に0除算が発生するのでチェック
                // #TODO : 移動した結果頂点が重なる場合があるのでその対策が必要
                var d = delta;
                bool isZero = Mathf.Abs(m) < 1e-5f;
                if (isZero == false)
                    d /= m;
                var o = vn * d;
                if (i < Count - 1)
                {
                    vertexNormal[index] = GetVertexNormal(i + 1);
                    edgeNormal[index] = GetEdgeNormal(Mathf.Min(Count - 2, i + 1));
                    index = (index + 1) & 1;
                }
                GetPoint(i).Vertex += o;
                // 次の頂点計算のためにen1線分の移動量を入れる
                // #TODO : vnが0ベクトルの時の対応
                delta = d * Vector3.Dot(vn, en1);
            }
        }

        /// <summary>
        /// Way全体を動かす
        /// </summary>
        /// <param name="offset"></param>
        public void Move(Vector3 offset)
        {
            for (var i = 0; i < Count; ++i)
            {
                GetPoint(i).Vertex += offset;
            }
        }


        /// <summary>
        /// 自身のクローンを作成する.
        /// cloneVertexがtrueの時は頂点もクローンする
        /// </summary>
        /// <returns></returns>
        public RnWay Clone(bool cloneVertex = true)
        {
            return new RnWay(LineString.Clone(cloneVertex), IsReversed, IsReverseNormal);
        }

        public IEnumerator<Vector3> GetEnumerator()
        {
            return Vertices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 同じ線分かどうか
        /// </summary>
        /// <param name="other"></param>
        /// <param name="onlyReferenceEqual"></param>
        /// <returns></returns>
        public bool IsSameLine(RnWay other, bool onlyReferenceEqual = true)
        {
            if (onlyReferenceEqual)
                return LineString == other.LineString;
            return RnLineString.Equals(LineString, other.LineString);
        }
    }


    public static class RnWayEx
    {
        public static IEnumerable<LineSegment2D> GetEdges2D(this RnWay self)
        {
            if (self == null)
                yield break;
            foreach (var e in GeoGraphEx.GetEdges(self.Vertices.Select(x => x.Xz()), false))
                yield return new LineSegment2D(e.Item1, e.Item2);
        }

        public static int FindPoint(this RnWay self, RnPoint point)
        {
            return self.LineString.Points.FindIndex(p => p == point);
        }

        /// <summary>
        /// self.GetPoint(index) == pointとなるindexを返す. 見つからない場合は-1が返る.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static int FindPointIndex(this RnWay self, RnPoint point)
        {
            var index = self.LineString.Points.IndexOf(point);
            if (index < 0)
                return index;
            return self.SwitchIndex(index);
        }

        /// <summary>
        /// nearestからRnWay上の最も近い点を探す
        /// </summary>
        /// <param name="self"></param>
        /// <param name="pos"></param>
        /// <param name="nearest"></param>
        /// <returns></returns>
        public static bool FindNearestPoint(this RnWay self, Vector3 pos, out Vector3 nearest)
        {
            nearest = Vector3.zero;
            var minLen = float.MaxValue;
            foreach (var s in GeoGraphEx.GetEdges(self, false))
            {
                var v = new LineSegment3D(s.Item1, s.Item2).GetNearestPoint(pos);
                var len = (v - pos).sqrMagnitude;
                if (len < minLen)
                {
                    minLen = len;
                    nearest = v;
                }
            }
            return minLen < float.MaxValue;
        }

        /// <summary>
        /// nullチェック込みのIsValid
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsValidOrDefault(this RnWay self)
        {
            return self?.IsValid ?? false;
        }

        /// <summary>
        /// 線分の長さを取得
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float CalcLength(this RnWay self)
        {
            return self.LineString.CalcLength();
        }
    }
}