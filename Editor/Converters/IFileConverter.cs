﻿namespace PlateauUnitySDK.Editor.Converters
{
    /// <summary>
    /// ファイル変換機能のインターフェイスです。
    /// </summary>
    internal interface IFileConverter
    {
        /// <summary>
        /// srcFilePathのファイルを変換し、dstFilePathに出力します。
        /// 成功時はtrue,失敗時はfalseを返します。
        /// </summary>
        bool Convert(string srcFilePath, string dstFilePath);
    }
}