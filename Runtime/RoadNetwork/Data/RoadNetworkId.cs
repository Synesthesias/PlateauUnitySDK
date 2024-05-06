﻿using System;
using UnityEngine;

namespace PLATEAU.RoadNetwork.Data
{
    [Serializable]
    public struct RnId<TPrimDataType> where TPrimDataType : IPrimitiveData
    {
        // PropertyDrawerでアクセスするため
        public const string IdFieldName = nameof(id);
        // 不正値
        public static RnId<TPrimDataType> Undefined => new RnId<TPrimDataType>(-1);

        // Listのindexアクセスがintなのでuintじゃなくてintにしておく
        // structなので初期値は基本0. その時に不正値扱いにするために0は不正値とする
        [SerializeField]
        private int id;

        public int Id => id - 1;

        // 有効なIdかどうか
        public bool IsValid => id > 0;

        // int型への暗黙の型変換
        public static implicit operator int(RnId<TPrimDataType> id) => id.Id;

        public RnId(int id)
        {
            this.id = id + 1;
        }
    }
}