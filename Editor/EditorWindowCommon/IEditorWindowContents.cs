namespace PlateauUnitySDK.Editor.EditorWindowCommon
{
    /// <summary>
    /// EditorWindow に描画するコンテンツのインターフェイスです。
    /// DrawGUI()で描画します。 
    /// </summary>
    internal interface IEditorWindowContents
    {
        /// <summary>
        /// EditorWindow用のコンテンツを描画します。
        /// </summary>
        public void DrawGUI();
    }
}