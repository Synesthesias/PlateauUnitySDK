﻿using PLATEAU.RoadNetwork;
using PLATEAU.RoadNetwork.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static PLATEAU.RoadNetwork.Factory.RoadNetworkFactory;

namespace PLATEAU.Editor.RoadNetwork
{

    public class RnModelDebugEditorWindow : EditorWindow
    {
        public interface IInstanceHelper
        {
            RnModel GetModel();

            HashSet<RnRoad> TargetRoads { get; }

            HashSet<RnIntersection> TargetIntersections { get; }

            HashSet<RnLane> TargetLanes { get; }

            HashSet<RnWay> TargetWays { get; }

            public bool IsTarget(RnRoadBase roadBase);
        }

        private const string WindowName = "Debug RnModel Editor";

        private IInstanceHelper InstanceHelper { get; set; }

        private class LaneEdit
        {
            private class LaneSplitEdit
            {
                public int splitNum = 2;
            }
            private readonly LaneSplitEdit splitEdit = new LaneSplitEdit();

            [Serializable]
            public class LaneWidthEdit
            {
                public LaneWayMoveOption moveOption = LaneWayMoveOption.MoveBothWay;
                public float width = 0f;
                public float moveWidth = 0f;
            }

            private readonly LaneWidthEdit widthEdit = new LaneWidthEdit();

            ulong laneNormalId = ulong.MaxValue;
            public float rightWayPos = 0f;
            public float leftWayPos = 0f;


            public void Update(RnModelDebugEditorWindow work, RnLane lane)
            {
                if (lane == null)
                    return;

                RnEditorUtil.TargetToggle($"Id '{lane.DebugMyId.ToString()}'", work.InstanceHelper.TargetLanes, lane);
                using (new EditorGUI.DisabledScope(false))
                {
                    EditorGUILayout.LongField("PrevBorder", (long)(lane.PrevBorder?.DebugMyId ?? ulong.MaxValue));
                    EditorGUILayout.LongField("NextBorder", (long)(lane.NextBorder?.DebugMyId ?? ulong.MaxValue));
                }
                // 情報表示

                if (rightWayPos != 0f && lane.RightWay != null)
                {
                    rightWayPos = 0f;
                }

                if (leftWayPos != 0f && lane.LeftWay != null)
                {
                    leftWayPos = 0f;
                }

                using (var _ = new EditorGUILayout.HorizontalScope())
                {
                    splitEdit.splitNum = EditorGUILayout.IntField("SplitNum", splitEdit.splitNum);
                    if (GUILayout.Button("Split"))
                    {
                        if (lane.Parent is RnRoad road)
                        {
                            var lanes = lane.SplitLane(splitEdit.splitNum, true);
                            foreach (var item in lanes)
                            {
                                var l = item.Key;
                                var parent = l.Parent as RnRoad;
                                parent?.ReplaceLane(l, item.Value);
                            }
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledGroupScope(false))
                    {
                        EditorGUILayout.FloatField("Width", lane.CalcWidth());
                    }

                    widthEdit.width = EditorGUILayout.FloatField("->", widthEdit.width);
                    widthEdit.moveOption = (LaneWayMoveOption)EditorGUILayout.EnumPopup("MoveOption", widthEdit.moveOption);
                    if (GUILayout.Button("SetWidth"))
                    {
                        lane.TrySetWidth(widthEdit.width, widthEdit.moveOption);
                    }
                }

                if (widthEdit.moveWidth != 0f)
                {
                    switch (widthEdit.moveOption)
                    {
                        case LaneWayMoveOption.MoveBothWay:
                            lane.LeftWay?.MoveAlongNormal(widthEdit.moveWidth * 0.5f);
                            lane.RightWay?.MoveAlongNormal(widthEdit.moveWidth * 0.5f);
                            break;
                        case LaneWayMoveOption.MoveLeftWay:
                            lane.LeftWay?.MoveAlongNormal(widthEdit.moveWidth);
                            break;
                        case LaneWayMoveOption.MoveRightWay:
                            lane.RightWay?.MoveAlongNormal(widthEdit.moveWidth);
                            break;
                    }
                    widthEdit.moveWidth = 0f;
                }
            }
        }
        LaneEdit laneEdit = new LaneEdit();


        private class RoadEdit
        {
            // 左側レーン数
            public int leftLaneCount = -1;
            // 右側レーン数
            public int rightLaneCount = -1;

            // 中央分離帯幅
            public float medianWidth = 0;
            public LaneWayMoveOption medianWidthOption = LaneWayMoveOption.MoveBothWay;

            public void Update(RnModelDebugEditorWindow work, RnRoad road)
            {
                if (road == null)
                    return;
                var roadGroup = road.CreateRoadGroupOrDefault();
                if (roadGroup == null)
                    return;

                RnEditorUtil.TargetToggle($"Id '{road.DebugMyId.ToString()}'", work.InstanceHelper.TargetRoads, road);
                using (new EditorGUI.DisabledScope(false))
                {
                    EditorGUILayout.LongField("Prev", (long)(road.Prev?.DebugMyId ?? ulong.MaxValue));
                    EditorGUILayout.LongField("Next", (long)(road.Next?.DebugMyId ?? ulong.MaxValue));

                }

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("LaneCount");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"L ({roadGroup.GetLeftLaneCount()}) ->", GUILayout.Width(45));
                        leftLaneCount = EditorGUILayout.IntField(leftLaneCount, GUILayout.Width(45));
                        EditorGUILayout.LabelField($"R ({roadGroup.GetRightLaneCount()}) ->", GUILayout.Width(45));
                        rightLaneCount = EditorGUILayout.IntField(rightLaneCount, GUILayout.Width(45));

                        if (GUILayout.Button("ChangeLaneCount"))
                        {
                            roadGroup.SetLeftLaneCount(leftLaneCount);
                            roadGroup.SetRightLaneCount(rightLaneCount);
                        }
                    }

                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    medianWidth = EditorGUILayout.FloatField("MedianWidth", medianWidth);
                    medianWidthOption = (LaneWayMoveOption)EditorGUILayout.EnumPopup("MoveOption", medianWidthOption);
                    if (GUILayout.Button("SetMedianWidth"))
                    {
                        roadGroup.SetMedianWidth(medianWidth, medianWidthOption);
                    }

                    if (GUILayout.Button("RemoveMedian"))
                    {
                        roadGroup.RemoveMedian(medianWidthOption);
                    }
                }

                if (GUILayout.Button("DisConnect"))
                {
                    road.DisConnect(false);
                }

                if (GUILayout.Button("Convert2Intersection"))
                {
                    road.ParentModel.Convert2Intersection(road);
                }
            }
        }
        RoadEdit roadEdit = new RoadEdit();

        private class IntersectionEdit
        {
            public long convertPrevRoadId = -1;
            public long convertNextRoadId = -1;
            public void Update(RnModelDebugEditorWindow work, RnIntersection intersection)
            {
                if (intersection == null)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    RnEditorUtil.TargetToggle($"Id '{intersection.DebugMyId.ToString()}'", work.InstanceHelper.TargetIntersections, intersection);
                    using (new EditorGUI.DisabledScope(false))
                    {
                        EditorGUILayout.LabelField("Intersection ID", intersection.DebugMyId.ToString());
                        foreach (var b in intersection.Neighbors)
                        {
                            EditorGUILayout.LabelField($"Road:{((RnRoadBase)b.Road).GetDebugMyIdOrDefault()}, Border:{b.Border.GetDebugMyIdOrDefault()}");
                        }
                    }
                }

                if (GUILayout.Button("DisConnect"))
                {
                    intersection.DisConnect(false);
                }


                using (new EditorGUILayout.HorizontalScope())
                {
                    convertPrevRoadId = EditorGUILayout.LongField("PrevRoadId", convertPrevRoadId);
                    convertNextRoadId = EditorGUILayout.LongField("NextRoadId", convertNextRoadId);

                    var prev = intersection.Neighbors.Select(n => n.Road)
                        .FirstOrDefault(r => r != null && r.DebugMyId == (ulong)convertPrevRoadId);
                    var next = intersection.Neighbors.Select(n => n.Road)
                        .FirstOrDefault(r => r != null && r.DebugMyId == (ulong)convertNextRoadId);

                    if (GUILayout.Button("Convert2Road"))
                    {
                        intersection.ParentModel.Convert2Road(intersection, prev, next);
                    }
                }

            }
        }
        IntersectionEdit intersectionEdit = new IntersectionEdit();

        public class WayEdit
        {

        }

        public void Reinitialize()
        {
        }

        private void Initialize()
        {
        }

        private void OnEnable()
        {
            Initialize();
        }

        /// <Summary>
        /// ウィンドウのパーツを表示します。
        /// </Summary>
        private void OnGUI()
        {
            var model = InstanceHelper?.GetModel();
            if (model == null)
                return;
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Lane Edit");

            foreach (var l in InstanceHelper.TargetLanes)
            {
                EditorGUILayout.Separator();
                laneEdit.Update(this, l);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Road Edit");
            foreach (var r in model.Roads)
            {
                if (InstanceHelper.IsTarget(r) == false && InstanceHelper.TargetRoads.Contains(r) == false)
                    continue;
                EditorGUILayout.Separator();
                roadEdit.Update(this, r);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Intersection Edit");
            foreach (var i in model.Intersections)
            {
                if (InstanceHelper.IsTarget(i) == false && InstanceHelper.TargetIntersections.Contains(i) == false)
                    continue;
                EditorGUILayout.Separator();
                intersectionEdit.Update(this, i);
            }
        }

        /// <summary>
        /// ウィンドウを取得する、存在しない場合に生成する
        /// </summary>
        /// <param name="instanceHelper"></param>
        /// <param name="focus"></param>
        /// <returns></returns>
        public static RnModelDebugEditorWindow OpenWindow(IInstanceHelper instanceHelper, bool focus)
        {
            var ret = GetWindow<RnModelDebugEditorWindow>(WindowName, focus);
            ret.InstanceHelper = instanceHelper;
            return ret;
        }

        /// <summary>
        /// ウィンドウのインスタンスを確認する
        /// ラップ関数
        /// </summary>
        /// <returns></returns>
        public static bool HasOpenInstances()
        {
            return HasOpenInstances<RnModelDebugEditorWindow>();
        }

    }
}