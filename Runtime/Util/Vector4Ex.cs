using UnityEngine;

namespace PLATEAU.Util
{
    public static class Vector4Ex
    {
        /// <summary> 各要素をmin, maxでクランプ </summary>
        public static Vector4 Clamp(this Vector4 x, float min, float max)
        {
            return new Vector4(Mathf.Clamp(x.x, min, max), Mathf.Clamp(x.y, min, max), Mathf.Clamp(x.z, min, max), Mathf.Clamp(x.w, min, max));
        }

        /// <summary> 各要素をmin,maxの各要素でクランプ </summary>
        public static Vector4 Clamp(Vector4 x, Vector4 min, Vector4 max)
        {
            return new Vector4(
                Mathf.Clamp(x.x, min.x, max.x)
                , Mathf.Clamp(x.y, min.y, max.y)
                , Mathf.Clamp(x.z, min.z, max.z)
                , Mathf.Clamp(x.w, min.w, max.w));
        }

        /// <summary> xを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutX(this Vector4 self, float x)
        {
            self.x = x;
            return self;
        }

        /// <summary> xyを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutXy(this Vector4 self, float x, float y)
        {
            self.x = x;
            self.y = y;
            return self;
        }

        /// <summary> xzを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutXz(this Vector4 self, float x, float z)
        {
            self.x = x;
            self.z = z;
            return self;
        }

        /// <summary> yを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutY(this Vector4 self, float y)
        {
            self.y = y;
            return self;
        }

        /// <summary> yzを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutYz(this Vector4 self, float y, float z)
        {
            self.y = y;
            self.z = z;
            return self;
        }

        /// <summary> zを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutZ(this Vector4 self, float z)
        {
            self.z = z;
            return self;
        }

        /// <summary> xzを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutXy(this Vector4 self, Vector2 xy)
        {
            self.x = xy.x;
            self.y = xy.y;
            return self;
        }

        /// <summary> wを差し替えたVector4返す。非破壊 </summary>
        public static Vector4 PutW(this Vector4 self, float w)
        {
            self.w = w;
            return self;
        }

        /// <summary>
        /// new Vector3(self.x, self.y, self.z);
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector3 Xyz(this Vector4 self)
        {
            return new Vector3(self.x, self.y, self.z);
        }

        /// <summary>
        /// Mathf.Min(self.z, Mathf.Min(self.x, self.y))
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Min(this Vector4 self)
        {
            return Mathf.Min(self.w, Mathf.Min(self.z, Mathf.Min(self.x, self.y)));
        }

        /// <summary>
        /// Mathf.Max(self.z, Mathf.Max(self.x, self.y))
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Max(this Vector4 self)
        {
            return Mathf.Max(self.w, Mathf.Max(self.z, Mathf.Max(self.x, self.y)));
        }

        /// <summary>
        /// self.x + self.y
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Sum(this Vector4 self)
        {
            return self.x + self.y + self.z + self.w;
        }

        /// <summary>
        /// (self.x + self.y) / 2
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static float Ave(this Vector4 self)
        {
            return (self.x + self.y + self.z + self.w) / 4;
        }

        /// <summary>
        /// new Vector4(Mathf.Abs(self.x), Mathf.Abs(self.y), Mathf.Abs(self.z), Mathf.Abs(self.w))
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector4 Abs(this Vector4 self)
        {
            self.x = Mathf.Abs(self.x);
            self.y = Mathf.Abs(self.y);
            self.z = Mathf.Abs(self.z);
            self.w = Mathf.Abs(self.w);
            return self;
        }

        /// <summary>
        /// new Vector4(self.x * mul.x, self.y * mul.y, self.z * mul.z, self.w * mul.w)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="mul"></param>
        /// <returns></returns>
        public static Vector4 Scaled(this Vector4 self, Vector4 mul)
        {
            self.x *= mul.x;
            self.y *= mul.y;
            self.z *= mul.z;
            self.w *= mul.w;
            return self;
        }

        /// <summary>
        /// new Vector4(self.x / div.x, self.y / div.y, self.z / div.z, self.w / div.w)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="div"></param>
        /// <returns></returns>
        public static Vector4 RevScaled(this Vector4 self, Vector4 div)
        {
            self.x /= div.x;
            self.y /= div.y;
            self.z /= div.z;
            self.w /= div.w;
            return self;
        }

        /// <summary> Vector4.Scaleの逆数版 a / b </summary>
        public static Vector4 RevScale(Vector4 a, Vector4 b)
        {
            return new Vector4(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        /// <summary>
        /// selfがmin ~ maxのaabbの範囲内にあるかどうか
        /// </summary>
        /// <param name="self"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static bool Between(this Vector4 self, Vector4 min, Vector4 max)
        {
            return
                min.x <= self.x && self.x <= max.x &&
                min.y <= self.y && self.y <= max.y &&
                min.z <= self.z && self.z <= max.z &&
                min.w <= self.w && self.w <= max.w;
        }
    }
}
