using UnityEngine;

namespace PLATEAU.Util
{
    public static class Vector3Ex
    {
        /// <summary> 各要素をmin, maxでクランプ </summary>
        public static Vector3 Clamp(this Vector3 x, float min, float max)
        {
            return new Vector3(Mathf.Clamp(x.x, min, max), Mathf.Clamp(x.y, min, max), Mathf.Clamp(x.z, min, max));
        }

        /// <summary> 各要素をmin,maxの各要素でクランプ </summary>
        public static Vector3 Clamp(Vector3 x, Vector3 min, Vector3 max)
        {
            return new Vector3(Mathf.Clamp(x.x, min.x, max.x), Mathf.Clamp(x.y, min.y, max.y), Mathf.Clamp(x.z, min.z, max.z));
        }

        /// <summary> xを差し替えたVector3返す。非破壊 </summary>
        public static Vector3 PutX(this Vector3 self, float x)
        {
            self.x = x;
            return self;
        }

        /// <summary> xyを差し替えたVector3返す。非破壊 </summary>
        public static Vector3 PutXy(this Vector3 self, float x, float y)
        {
            self.x = x;
            self.y = y;
            return self;
        }

        /// <summary> xzを差し替えたVector3返す。非破壊 </summary>
        public static Vector3 PutXz(this Vector3 self, float x, float z)
        {
            self.x = x;
            self.z = z;
            return self;
        }

        /// <summary> yを差し替えたVector3返す。非破壊 </summary>
        public static Vector3 PutY(this Vector3 self, float y)
        {
            self.y = y;
            return self;
        }

        /// <summary> yzを差し替えたVector3返す。非破壊 </summary>
        public static Vector3 PutYz(this Vector3 self, float y, float z)
        {
            self.y = y;
            self.z = z;
            return self;
        }

        /// <summary> zを差し替えたVector3返す。非破壊 </summary>
        public static Vector3 PutZ(this Vector3 self, float z)
        {
            self.z = z;
            return self;
        }

        /// <summary> xzを差し替えたVector3返す。非破壊 </summary>
        public static Vector3 PutXy(this Vector3 self, Vector2 xy)
        {
            self.x = xy.x;
            self.y = xy.y;
            return self;
        }

        /// <summary>
        /// new Vector2(self.x, self.y);
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector2 Xy(this Vector3 self)
        {
            return new Vector2(self.x, self.y);
        }

        /// <summary>
        /// new Vector2(self.x, self.z);
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector2 Xz(this Vector3 self)
        {
            return new Vector2(self.x, self.z);
        }


        /// <summary>
        /// new Vector2(self.x, self.z);
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector2 Yz(this Vector3 self)
        {
            return new Vector2(self.y, self.z);
        }

        /// <summary>
        /// Mathf.Min(self.z, Mathf.Min(self.x, self.y))
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Min(this Vector3 self)
        {
            return Mathf.Min(self.z, Mathf.Min(self.x, self.y));
        }

        /// <summary>
        /// Mathf.Max(self.z, Mathf.Max(self.x, self.y))
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Max(this Vector3 self)
        {
            return Mathf.Max(self.z, Mathf.Max(self.x, self.y));
        }

        /// <summary>
        /// self.x + self.y
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Sum(this Vector3 self)
        {
            return self.x + self.y + self.z;
        }

        /// <summary>
        /// (self.x + self.y) / 2
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Ave(this Vector3 self)
        {
            return (self.x + self.y + self.z) / 3;
        }

        /// <summary>
        /// new Vector3(Mathf.Abs(self.x), Mathf.Abs(self.y), Mathf.Abs(self.z))
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector3 Abs(this Vector3 self)
        {
            self.x = Mathf.Abs(self.x);
            self.y = Mathf.Abs(self.y);
            self.z = Mathf.Abs(self.z);
            return self;
        }

        /// <summary>
        /// new Vector3(self.x * mul.x, self.y * mul.y, self.z * mul.z)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="mul"></param>
        /// <returns></returns>
        public static Vector3 Scaled(this Vector3 self, Vector3 mul)
        {
            self.x *= mul.x;
            self.y *= mul.y;
            self.z *= mul.z;
            return self;
        }

        /// <summary>
        /// new Vector3(self.x / div.x, self.y / div.y, self.z / div.z)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="div"></param>
        /// <returns></returns>
        public static Vector3 RevScaled(this Vector3 self, Vector3 div)
        {
            self.x /= div.x;
            self.y /= div.y;
            self.z /= div.z;
            return self;
        }

        /// <summary> Vector3.Scaleの逆数版 a / b </summary>
        public static Vector3 RevScale(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        public static Vector3Int ToVector3Int(this Vector3 self)
        {
            return new Vector3Int((int)self.x, (int)self.y, (int)self.z);
        }

        /// <summary>
        /// 各成分に対してCeilToIntを適用してVector3Int型で返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector3Int CeilToInt(this Vector3 self)
        {
            return new Vector3Int(Mathf.CeilToInt(self.x), Mathf.CeilToInt(self.y), Mathf.CeilToInt(self.z));
        }

        /// <summary>
        /// 各成分に対してFloorToIntを適用してVector3Int型で返す
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector3Int FloorToInt(this Vector3 self)
        {
            return new Vector3Int(Mathf.FloorToInt(self.x), Mathf.FloorToInt(self.y), Mathf.FloorToInt(self.z));
        }

        /// <summary>
        /// selfがmin ~ maxのaabbの範囲内にあるかどうか
        /// </summary>
        /// <param name="self"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static bool Between(this Vector3 self, Vector3 min, Vector3 max)
        {
            return
                min.x <= self.x && self.x <= max.x &&
                min.y <= self.y && self.y <= max.y &&
                min.z <= self.z && self.z <= max.z;
        }

        /// <summary>
        /// return new Vector4(self.x, self.y, self.z, a);)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public static Vector4 Xyza(this Vector3 self, float a)
        {
            return new Vector4(self.x, self.y, self.z, a);
        }
    }
}
