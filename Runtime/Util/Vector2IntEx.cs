using UnityEngine;

namespace PLATEAU.Util
{
    public static class Vector2IntIntEx
    {
        /// <summary>
		/// new Vector2Int(Mathf.Abs(self.x), Mathf.Abs(self.y))
		/// </summary>
		/// <param name="self"></param>
		/// <returns></returns>
		public static Vector2Int Abs(this Vector2Int self)
        {
            self.x = Mathf.Abs(self.x);
            self.y = Mathf.Abs(self.y);
            return self;
        }

        /// <summary> 各要素をmin, maxでクランプ </summary>
        public static Vector2Int Clamp(this Vector2Int x, int min, int max)
        {
            return new Vector2Int(Mathf.Clamp(x.x, min, max), Mathf.Clamp(x.y, min, max));
        }

        /// <summary> 各要素をmin,maxの各要素でクランプ </summary>
        public static Vector2Int Clamp(this Vector2Int x, Vector2Int min, Vector2Int max)
        {
            return new Vector2Int(Mathf.Clamp(x.x, min.x, max.x), Mathf.Clamp(x.y, min.y, max.y));
        }

        /// <summary> xを差し替えたVector2Int返す。非破壊 </summary>
        public static Vector2Int PutX(this Vector2Int self, int x)
        {
            self.x = x;
            return self;
        }

        /// <summary> yを差し替えたVector2Int返す。非破壊 </summary>
        public static Vector2Int PutY(this Vector2Int self, int y)
        {
            self.y = y;
            return self;
        }

        /// <summary>
        /// yを指定してVector3にする
        /// return new Vector3(self.x, y, self.y);
        /// </summary>
        /// <param name="self"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Vector3 Xay(this Vector2Int self, int y = 0)
        {
            return new Vector3(self.x, y, self.y);
        }

        /// <summary>
        /// zを指定してVector3にする
        /// return new Vector3(self.x, self.y, z);
        /// </summary>
        /// <param name="self"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public static Vector3 Xya(this Vector2Int self, int z = 0)
        {
            return new Vector3(self.x, self.y, z);
        }

        /// <summary>
        /// Mathf.Min(self.x, self.y)
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int Min(this Vector2Int self)
        {
            return Mathf.Min(self.x, self.y);
        }

        /// <summary>
        /// Mathf.Min(self.x, self.y)
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int Max(this Vector2Int self)
        {
            return Mathf.Max(self.x, self.y);
        }

        /// <summary>
        /// self.x + self.y
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int Sum(this Vector2Int self)
        {
            return self.x + self.y;
        }

        /// <summary>
        /// self.y / self.x
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int Tan(this Vector2Int self)
        {
            return self.y / self.x;
        }

        /// <summary>
        /// new Vector2IntInt(self.x * mul.x, self.y * mul.y)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="mul"></param>
        /// <returns></returns>
        public static Vector2Int Scaled(this Vector2Int self, Vector2Int mul)
        {
            self.x *= mul.x;
            self.y *= mul.y;
            return self;
        }

        /// <summary>
        /// new Vector2IntInt(self.x / div.x, self.y / div.y)
        /// </summary>
        /// <param name="self"></param>
        /// <param name="div"></param>
        /// <returns></returns>
        public static Vector2Int RevScaled(this Vector2Int self, Vector2Int div)
        {
            self.x /= div.x;
            self.y /= div.y;
            return self;
        }

        /// <summary> Vector2Int.Scaleの逆数版 a / b </summary>
        public static Vector2Int RevScale(Vector2Int a, Vector2Int b)
        {
            return new Vector2Int(a.x / b.x, a.y / b.y);
        }


        public static Vector2 ToVector2(this Vector2Int self)
        {
            return new Vector2(self.x, self.y);
        }
    }
}