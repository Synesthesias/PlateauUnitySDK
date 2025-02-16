using PLATEAU.Util.GeoGraph;
using UnityEngine;

namespace PLATEAU.RoadNetwork
{

    // 左右を表すenum
    public enum RnDir
    {
        /// <summary>
        /// 左
        /// </summary>
        Left,
        /// <summary>
        /// 右
        /// </summary>
        Right
    }

    public static class RnDirEx
    {
        /// <summary>
        /// 反対を取得
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static RnDir GetOpposite(this RnDir dir)
        {
            return dir == RnDir.Left ? RnDir.Right : RnDir.Left;
        }
    }

    /// <summary>
    /// 共通定義情報
    /// </summary>
    internal static class RnDef
    {
        public const AxisPlane Plane = AxisPlane.Xz;

        public static Vector2 ToVec2(Vector3 v)
        {
            return v.ToVector2(Plane);
        }

        public static Vector3 ToVec3(Vector2 v, float a = 0)
        {
            return v.ToVector3(Plane, a);
        }
    }

    /// <summary>
    /// デバッグ用の定数設定. (基本的にRelease版では全て切られる)
    /// </summary>
    internal static class RnDebugDef
    {
        public static readonly bool ShowDetailLog = false;
    }
}