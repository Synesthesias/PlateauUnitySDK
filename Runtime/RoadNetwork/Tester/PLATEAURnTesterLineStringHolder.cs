using PLATEAU.Util.GeoGraph;
using System.Collections.Generic;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Tester
{
    [RequireComponent(typeof(PLATEAURnTesterLineString))]
    public class PLATEAURnTesterLineStringHolder : MonoBehaviour
    {
        PLATEAURnTesterLineString lineString;

        public PLATEAURnTesterLineString LineString
        {
            get
            {
                if (!lineString)
                {
                    lineString = GetComponent<PLATEAURnTesterLineString>();
                    if (!lineString)
                        lineString = gameObject.AddComponent<PLATEAURnTesterLineString>();
                }

                return lineString;
            }
        }

        /// <summary>
        /// 2dに射影するときの平面
        /// </summary>
        public AxisPlane plane
        {
            get
            {
                return !LineString ? AxisPlane.Xy : LineString.plane;
            }
        }

        public Color color
        {
            get
            {
                return !LineString ? Color.white : LineString.color;
            }
        }


        /// <summary>
        /// Vector2の頂点を取得
        /// </summary>
        /// <returns></returns>
        public List<Vector2> GetVertices()
        {
            return !LineString ? new List<Vector2>() : LineString.GetVertices();
        }

        /// <summary>
        /// Vector3の頂点を取得
        /// </summary>
        /// <returns></returns>
        public List<Vector3> GetVertices3D()
        {
            return !LineString ? new List<Vector3>() : LineString.GetVertices3D();
        }

    }
}