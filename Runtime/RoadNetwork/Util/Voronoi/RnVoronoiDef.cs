using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Util.Voronoi
{
    public class RnVoronoiDef
    {
        public const double Epsilon = 1e-6;

        public static bool IsZero(double x) => Math.Abs(x) <= Epsilon;

        // 2次方程式の解を返す関数
        // 戻り値は解の個数
        public static int SolveQuadratic(double a, double b, double c, out double x1, out double x2)
        {
            x1 = x2 = 0.0;
            if (IsZero(a))
            {
                if (IsZero(b))
                    return 0;

                x1 = x2 = -c / b;
                return 1;
            }

            if (IsZero(b))
            {
                if (IsZero(c))
                {
                    x1 = x2 = 0.0;
                    return 1;
                }

                if (Math.Sign(b) == Math.Sign(c))
                    return 0;

                x1 = Math.Sqrt(-c / a);
                x2 = -x1;
                return 2;
            }


            // 判別式の計算
            double d = b * b - 4 * a * c;

            // 判別式が負の場合、実数解は存在しない
            if (d < 0)
                return 0;

            // 判別式が0の時、重解
            if (d == 0)
            {
                x1 = x2 = -b / (2 * a);
                return 1;
            }

            // 解の計算
            double sqrtDiscriminant = Math.Sqrt(d);
            x1 = (-b + sqrtDiscriminant) / (2 * a);
            x2 = (-b - sqrtDiscriminant) / (2 * a);
            return 2;
        }

    }

    /// <summary>
    /// グラフの頂点を表す
    /// </summary>
    public class VPoint
    {
        public Vector2d V { get; }

        public double x => V.x;

        public double y => V.y;

        public VPoint(Vector2d v)
        {
            V = v;
        }

        public VPoint(double x, double y)
        {
            V = new Vector2d(x, y);
        }

        public static implicit operator Vector2d(VPoint p)
        {
            return p.V;
        }
    }

    public class VEdge
    {
        private VPoint start;

        /// <summary>
        /// 開始点
        /// </summary>
        public VPoint Start
        {
            get => start;
            set => start = value;
        }


        private VPoint end;

        /// <summary>
        /// 終了点(半直線の場合はnull)
        /// </summary>
        public VPoint End
        {
            get => end;
            set => end = value;
        }

        /// <summary>
        /// 左側のサイトポイントインデックス
        /// </summary>
        public int LeftSiteIndex { get; set; }

        /// <summary>
        /// 右側のサイトポイント
        /// </summary>
        public int RightSiteIndex { get; set; }

        /// <summary>
        /// 辺の方向
        /// </summary>
        public Vector2d Direction { get; }

        /// <summary>
        /// 辺の最後の終了地点調整用. 半直線の反対側の辺
        /// </summary>
        public VEdge OppositeEdge { get; set; }

        /// <summary>
        /// sを開始地点として, サイトポイントl,rからなるエッジ
        /// </summary>
        /// <param name="start"></param>
        /// <param name="direction"></param>
        /// <param name="leftSiteIndex"></param>
        /// <param name="rightSiteIndex"></param>
        public VEdge(VPoint start, Vector2d direction, int leftSiteIndex, int rightSiteIndex)
        {
            this.start = start;
            this.end = null;
            LeftSiteIndex = leftSiteIndex;
            RightSiteIndex = rightSiteIndex;
            Direction = direction.normalized;
        }

        /// <summary>
        /// Start/Endから成るdirectionともともとのdirectionが一致しているかをチェックする
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        private void Check()
        {
            if (Start != null && End != null)
            {
                var d = End.V - Start.V;
                if (d.magnitude > 1e-8)
                {
                    var dir = d.normalized;
                    var dot = Vector2.Dot(dir.ToVector2(), Direction.ToVector2());
                    if (Mathf.Abs(dot) < 0.99f)
                    {
                        throw new InvalidDataException("direction");
                    }
                }

            }
        }

        /// <summary>
        /// 半直線a,bの交点を求める
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="p"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static bool TryGetCrossPoint(VEdge a, VEdge b, out Vector2d p, double epsilon)
        {
            p = new Vector2d();

            // sa + da * t1 = sb + db * t2
            // => sa_x + da_x * t1 = sb_x + db_x * t2
            //    sa_y + da_y * t1 = sb_y + db_y * t2
            // => da_y * sa_x + da_y * da_x * t1 = da_y * sb_x + da_y * db_x * t2
            //    da_x * sa_y + da_x * da_y * t1 = da_x * sb_y + da_x * db_y * t2
            // => da_y * sa_x - da_x * sa_y  = da_y * sb_x - da_x * sb_y + (da_y * db_x - da_x * db_y ) * t2
            // => sa×da = sb×da + (db×da) * t2
            // => t2 = (sa×da - sb×da) / db×da
            // => t2 = ((sa - sb)×da) / db×da = da×(sa - sb) / da×db
            // t1も同様
            var deno = Vector2d.Cross(a.Direction, b.Direction);
            if (Math.Abs(deno) <= 1e-6)
                return false;

            var t1 = Vector2d.Cross(b.Start.V - a.Start.V, b.Direction) / deno;
            var t2 = Vector2d.Cross(a.Direction, a.Start.V - b.Start.V) / deno;

            p = a.Start + a.Direction * t1;

            static bool IsTarget(VEdge e, double t, double ep)
            {
                return (t >= -ep /*&& (e.End == null || t <= 1 + epsilon)*/);// || Math.Abs(e.Direction.y) <= ep;
            }

            return IsTarget(a, t1, epsilon) && IsTarget(b, t2, epsilon);
        }

        public Vector2 ToVector2() => new Vector2((float)Start.x, (float)Start.y);
    }

    /// <summary>
    /// 二分木のノード
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class VTreeNode<T> where T : VTreeNode<T>
    {
        /// <summary>
        /// 葉かどうか
        /// </summary>
        public bool IsLeaf => Left == null && Right == null;

        /// <summary>
        /// 自身の親ノード
        /// </summary>
        public T Parent { get; set; }

        /// <summary>
        /// 左子ノード
        /// </summary>
        public T Left { get; private set; }

        /// <summary>
        /// 右子ノード
        /// </summary>
        public T Right { get; private set; }

        /// <summary>
        /// 一つ左のLeafを取得する
        /// </summary>
        public T LeftLeaf
        {
            get
            {
                if (IsLeaf == false)
                    return null;
                var lp = GetLeftParent((T)this);

                return GetLeftChild(lp);
            }
        }

        public T RightLeaf
        {
            get
            {
                if (IsLeaf == false)
                    return null;
                var rp = GetRightParent((T)this);
                return GetRightChild(rp);
            }
        }

        /// <summary>
        /// Left側の子の中で最も右にある葉要素)
        /// </summary>
        /// <returns></returns>
        public T ChildLeftLeaf
        {
            get
            {
                return GetLeftChild((T)this);
            }
        }

        /// <summary>
        /// Right側の子の中で最も左にある葉要素)
        /// </summary>
        /// <returns></returns>
        public T ChildRightLeaf
        {
            get
            {
                return GetRightChild((T)this);
            }
        }

        /// <summary>
        /// この木の高さ
        /// </summary>
        public int Height => GetHeight((T)this);

        /// <summary>
        /// 左子ノードを設定する
        /// </summary>
        /// <param name="p"></param>
        public void SetLeft(T p)
        {
            Left = p;
            if (p != null)
                p.Parent = (T)this;
        }

        /// <summary>
        /// 右子ノードを設定する
        /// </summary>
        /// <param name="p"></param>
        public void SetRight(T p)
        {
            Right = p;
            if (p != null)
                p.Parent = (T)this;
        }

        /// <summary>
        /// 自身がLeafの時, 自身をツリーから削除する
        /// </summary>
        public void RemoveLeaf()
        {
            if (IsLeaf == false)
                return;

            // 自分がルートの場合は削除しない(もともと孤立しているから)
            if (Parent == null)
                return;


            // 2世代上
            var grandParent = Parent.Parent;
            // 親がルートの場合は削除で終わり
            if (grandParent == null)
            {
                if (Parent.Left == this)
                    Parent.SetLeft(null);
                else
                    Parent.SetRight(null);
                return;
            }

            // 親がルートでない場合
            if (Parent.Left == this)
            {
                // gからみてLeft -> Leftの位置にp1がある場合
                // pの部分にxを入れる
                //     g            g
                //    /  \         /  \
                //   p    *   ->  x    *
                //  /  \
                // p1   x
                if (grandParent.Left == Parent)
                    grandParent.SetLeft(Parent.Right);

                // gからみてRight -> Rightの位置にp1がある場合
                // pの部分にxを入れる
                //     g            g
                //    /  \         /  \
                //    *   p   ->  *    x
                //       /  \
                //      p1   x
                if (grandParent.Right == Parent)
                    grandParent.SetRight(Parent.Right);
            }
            else
            {
                // gからみてLeft -> Rightの位置にp1がある場合
                // pの部分にxを入れる
                //     g            g
                //    /  \         /  \
                //   p    *   ->  x    *
                //  /  \
                // x   p1
                if (grandParent.Left == Parent)
                    grandParent.SetLeft(Parent.Left);

                // gからLeft -> Leftの形式の場合
                // pの部分にxを入れる
                //     g            g
                //    /  \         /  \
                //    *   p   ->  *    x
                //       /  \
                //      x   p1
                if (grandParent.Right == Parent)
                    grandParent.SetRight(Parent.Left);
            }

            //Parent = null;
        }

        /// <summary>
        /// pから親をたどって行く.
        /// p.Parentとpがpredicateを満たすまで親をたどる
        /// </summary>
        /// <param name="p"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        private static T FindParent(T p, Func<T, T, bool> predicate)
        {
            T node = p.Parent;
            T pLast = p;
            while (node != null && predicate(node, pLast) == false)
            {
                pLast = node;
                node = node.Parent;
            }

            return node;
        }

        /// <summary>
        /// 左側の親を取得する.
        /// fの左親はa. fの右親はc
        ///      a
        ///     /  \
        ///    b    c
        ///   / \  /  \
        ///  d   e f   g
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static T GetLeftParent(T p)
        {
            return FindParent(p, (parent, child) => parent.Left != child);
        }


        /// <summary>
        /// 右側の親を取得する.
        /// fの左親はa. fの右親はc
        ///      a
        ///     /  \
        ///    b    c
        ///   / \  /  \
        ///  d   e f   g
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static T GetRightParent(T p)
        {
            return FindParent(p, (parent, child) => parent.Right != child);
        }

        /// <summary>
        /// 自分の左の子供を取得する(p.Left以下で最も右にある葉要素)
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static T GetLeftChild(T p)
        {
            if (p == null)
                return null;
            var par = p.Left;
            if (par == null)
                return null;
            while (!par.IsLeaf)
                par = par.Right;
            return par;
        }

        /// <summary>
        /// 自分の右の子供を取得する(p.Right以下で左にある葉要素)
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static T GetRightChild(T p)
        {
            if (p == null)
                return null;
            var par = p.Right;
            if (par == null)
                return null;
            while (!par.IsLeaf)
                par = par.Left;
            return par;
        }

        /// <summary>
        /// このツリーの高さ(子の最大ネスト数)
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static int GetHeight(T p)
        {
            if (p == null)
                return 0;
            if (p.IsLeaf)
                return 1;

            return Math.Max(GetHeight(p.Left), GetHeight(p.Right)) + 1;
        }
    }

    public static class VTreeNodeEx
    {
        /// <summary>
        /// ノードの葉をすべて取得する(左から右に取得する)
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<T> GetAllLeafs<T>(this T self) where T : VTreeNode<T>
        {
            if (self.IsLeaf)
            {
                yield return self;
            }
            else
            {
                if (self.Left != null)
                {
                    foreach (var l in self.Left.GetAllLeafs())
                        yield return l;
                }
                if (self.Right != null)
                {
                    foreach (var r in self.Right.GetAllLeafs())
                        yield return r;
                }

            }
        }

        /// <summary>
        /// rootを根として木構造を文字列で表示する. strDigitは各ノードを表す文字列の幅
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="root"></param>
        /// <param name="toString"></param>
        /// <returns></returns>
        public static string BuildString<T>(this T root, Func<T, string> toString) where T : VTreeNode<T>
        {
            // widthのサイズの三角形ASCIIアートを返す
            static List<string> Triangle(int width)
            {
                var h = (width - 1) / 2;
                var ret = new List<string>();
                for (var i = 0; i < h; ++i)
                {
                    ret.Add($"/{new string(' ', i * 2 + 1)}\\");
                }

                return ret;
            }

            // 中央ぞろえ
            static string Centering(string text, int length)
            {
                var pad = length - text.Length;
                var left = pad / 2;
                return text.PadLeft(left + text.Length).PadRight(length);
            }

            List<string> Impl(T n)
            {
                if (n.IsLeaf)
                    return new() { toString(n) };

                var leftLines = Impl(n.Left);
                var rightLines = Impl(n.Right);

                var leftLen = leftLines.Any() ? leftLines.Max(x => x.Length) : 0;
                var rightLen = rightLines.Any() ? rightLines.Max(x => x.Length) : 0;

                var childWidth = Math.Max(leftLen, rightLen);

                List<string> ret = new();
                for (var i = 0; i < Mathf.Max(leftLines.Count, rightLines.Count); ++i)
                {
                    var l = Centering(i < leftLines.Count ? leftLines[i] : " ", childWidth);
                    var r = Centering(i < rightLines.Count ? rightLines[i] : " ", childWidth);
                    ret.Add($"{l} {r}");
                }

                var width = 2 * childWidth + 1;
                var triangle = Triangle(childWidth + 2 - 2);
                ret.InsertRange(0, triangle.Select(t => Centering(t, width)));
                ret.Insert(0, Centering(toString(n), width));
                return ret;
            }

            var lines = Impl(root);
            var sb = new StringBuilder();
            foreach (var l in lines)
                sb.AppendLine(l);
            return sb.ToString();
        }
    }
    /// <summary>
    /// y = a*x^2 + b*x + c
    /// </summary>
    public class VParabola
    {
        public double a;
        public double b;
        public double c;

        public VParabola(double a, double b, double c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public double GetY(double x)
        {
            return a * x * x + b * x + c;
        }

        /// <summary>
        /// y = 0となるxを求める
        /// </summary>
        /// <returns></returns>
        public int Solve(out double x1, out double x2)
        {
            return RnVoronoiDef.SolveQuadratic(a, b, c, out x1, out x2);
        }

        /// <summary>
        /// 交点を求める
        /// </summary>
        /// <param name="other"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public int GetCrossPoint(VParabola other, out Vector2d p1, out Vector2d p2)
        {
            p1 = p2 = new Vector2d();
            var p = new VParabola(a - other.a, b - other.b, c - other.c);
            var n = p.Solve(out var x1, out var x2);
            if (n == 0)
                return n;
            p1 = new Vector2d(x1, GetY(x1));
            p2 = new Vector2d(x2, GetY(x2));
            return n;
        }

    }

}