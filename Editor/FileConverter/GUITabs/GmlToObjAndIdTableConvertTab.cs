﻿using PlateauUnitySDK.Editor.EditorWindowCommon;
using UnityEditor;

namespace PlateauUnitySDK.Editor.FileConverter.GUITabs
{
    // TODO BaseConvertTab.cs と共通する箇所が多いのでまとめられるか検討
    /// <summary>
    /// Gml -> Obj の変換タブと
    /// Gml -> IdFileTable の変換タブの機能を
    /// 混ぜ合わせて両方を一度に出力するGUIを作ります。
    /// </summary>
    public class GmlToObjAndIdTableConvertTab : ScrollableEditorWindowContents
    {
        private BaseConvertTab[] tabs = new BaseConvertTab[]
        {
            new GmlToObjFileConvertTab(),
            new GmlToIdFileTableConvertTab()
        };

        private string SourceFileExtension => "gml";
        private string sourceFilePath;

        public GmlToObjAndIdTableConvertTab()
        {
            this.sourceFilePath = ConvertFileSelectorGUIUtil.DefaultPath();
        }
        
        protected override void DrawScrollable()
        {
            using (PlateauEditorStyle.VerticalScope())
            {
                foreach (var tab in tabs)
                {
                    tab.HeaderInfoGUI();
                }    
            }

            ConvertFileSelectorGUIUtil.FileSelectGUI(
                ref this.sourceFilePath,
                SourceFileExtension,
                ConvertFileSelectorGUIUtil.FilePanelType.Open,
                $"Select {SourceFileExtension} File"
            );
            foreach (var tab in tabs)
            {
                tab.SourceFilePath = this.sourceFilePath;
            }
            foreach (var tab in tabs)
            {
                HeaderDrawer.Draw($"gml to {tab.DestFileExtension}");
                HeaderDrawer.IncrementDepth();
                tab.DstFileSelectGUI();
                tab.ConfigureGUIOuter();
                HeaderDrawer.DecrementDepth();
            }
            ConvertFileSelectorGUIUtil.PrintConvertButton(Convert);
        }

        private bool Convert()
        {
            bool isSuccess = true;
            foreach (var tab in tabs)
            {
                isSuccess &= tab.FileConverter.Convert(tab.SourceFilePath, tab.DestFilePath);
            }

            return isSuccess;
        }
    }
}