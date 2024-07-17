﻿using PLATEAU.RoadNetwork.Data;
using PLATEAU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace PLATEAU.RoadNetwork.Drawer
{
    [Serializable]
    public class RoadNetworkDrawerDebug
    {
        // --------------------
        // start:フィールド
        // --------------------
        [SerializeField] private bool visible = true;
        // Laneの頂点の内側を向くベクトルの中央点を表示する
        [SerializeField] private bool showInsideNormalMidPoint = false;
        // 頂点インデックスを表示する
        [SerializeField] private bool showVertexIndex = false;
        // 頂点の座標を表示する
        [SerializeField] private bool showVertexPos = false;
        // 頂点表示するときのフォントサイズ
        [SerializeField] private int showVertexFontSize = 20;
        // レーン描画するときに法線方向へオフセットを入れる
        [SerializeField] private float edgeOffset = 10f;
        [SerializeField] private bool showSplitLane = false;
        [SerializeField] private float splitLaneRate = 0.5f;
        [SerializeField] private float yScale = 1f;

        [Serializable]
        private class DrawOption
        {
            public bool visible = true;
            public Color color = Color.white;
        }

        [Serializable]
        private class NodeOption
        {
            public bool visible = true;

            public DrawOption showTrack = new DrawOption();

            public DrawOption showBorder = new DrawOption();

            public DrawOption showSplitTrack = new DrawOption();
        }
        [SerializeField] private NodeOption nodeOp = new NodeOption();

        [Serializable]
        private class LinkOption
        {
            public bool visible = true;
        }

        [SerializeField] private LinkOption linkOp = new LinkOption();
        [Serializable]
        private class WayOption
        {
            // 法線を表示する
            public bool showNormal = true;

            // 反転したWayの矢印色
            public Color normalWayArrowColor = Color.yellow;
            // 通常Wayの矢印色
            public Color reverseWayArrowColor = Color.blue;

            public float arrowSize = 0.5f;
        }
        [SerializeField] private WayOption wayOp = new WayOption();

        [Serializable]
        private class LaneOption
        {
            public bool visible = true;
            public float bothConnectedLaneAlpha = 1f;
            public float validWayAlpha = 0.75f;
            public float invalidWayAlpha = 0.3f;
            public bool showAttrText = false;
            public bool showId = false;
            public DrawOption showLeftWay = new DrawOption();
            public DrawOption showRightWay = new DrawOption();
            // 境界線を表示する
            public DrawOption showPrevBorder = new DrawOption();
            public DrawOption showNextBorder = new DrawOption();
            /// <summary>
            /// レーン描画するときのアルファを返す
            /// </summary>
            /// <param name="self"></param>
            /// <returns></returns>
            public float GetLaneAlpha(RnLane self)
            {
                if (self.IsBothConnectedLane)
                    return bothConnectedLaneAlpha;
                if (self.IsValidWay)
                    return validWayAlpha;
                return invalidWayAlpha;
            }
        }
        [SerializeField] private LaneOption laneOp = new LaneOption();

        [Serializable]
        private class SideWalkOption : DrawOption
        {

        }
        [SerializeField] private SideWalkOption sideWalkRoadOp = new SideWalkOption();


        [Serializable]
        private class LaneSplitOption
        {
            public ulong targetLaneId = 0;

            public int splitNum = 2;

            public bool execute = false;
        }

        [SerializeField] private LaneSplitOption laneSplitOp = new LaneSplitOption();


        // --------------------
        // end:フィールド
        // --------------------

        private void DrawArrows(IEnumerable<Vector3> vertices
            , bool isLoop = false
            , float arrowSize = 0.5f
            , Vector3? arrowUp = null
            , Color? color = null
            , Color? arrowColor = null
            , float duration = 0f
            , bool depthTest = true)
        {
            if (Mathf.Abs(yScale - 1f) < 1e-3f)
                DebugEx.DrawArrows(vertices, isLoop, arrowSize, arrowUp, color, arrowColor, duration, depthTest);
            else
                DebugEx.DrawArrows(vertices.Select(v => v.PutY(v.y * yScale)), isLoop, arrowSize, arrowUp, color, arrowColor, duration, depthTest);
        }
        public void DrawString(string text, Vector3 worldPos, Vector2? screenOffset = null, Color? color = null, int? fontSize = null)
        {
            DebugEx.DrawString(text, worldPos.PutY(worldPos.y * yScale), screenOffset, color, fontSize);
        }

        public void DrawLine(Vector3 start, Vector3 end, Color? color = null)
        {
            Debug.DrawLine(start.PutY(start.y * yScale), end.PutY(end.y * yScale), color ?? Color.white);
        }

        public void DrawArrow(
            Vector3 start
            , Vector3 end
            , float arrowSize = 0.5f
            , Vector3? arrowUp = null
            , Color? bodyColor = null
            , Color? arrowColor = null
            , float duration = 0f
            , bool depthTest = true)
        {
            DebugEx.DrawArrow(start.PutY(start.y * yScale), end.PutY(end.y * yScale), arrowSize, arrowUp, bodyColor, arrowColor, duration, depthTest);
        }

        public void Draw(RnModel roadNetwork)
        {
            if (!visible)
                return;
            if (roadNetwork == null)
                return;

            // 道描画
            void DrawWay(RnWay way, Color color, Color? arrowColor = null)
            {
                if (way == null)
                    return;
                if (way.Count <= 1)
                    return;
                // 矢印色は設定されていない場合は反転しているかどうかで返る
                if (arrowColor.HasValue)
                    arrowColor = way.IsReversed ? wayOp.reverseWayArrowColor : wayOp.normalWayArrowColor;

                DrawArrows(way.Vertices.Select((v, i) => v + -edgeOffset * way.GetVertexNormal(i)), false, color: color, arrowColor: arrowColor, arrowSize: wayOp.arrowSize);

                if (showVertexIndex)
                {
                    foreach (var item in way.Vertices.Select((v, i) => new { v, i }))
                        DrawString(item.i.ToString(), item.v, color: Color.red, fontSize: showVertexFontSize);
                }

                if (showVertexPos)
                {
                    foreach (var item in way.Vertices.Select((v, i) => new { v, i }))
                        DrawString(item.v.ToString(), item.v, color: Color.red, fontSize: showVertexFontSize);
                }

                foreach (var i in Enumerable.Range(0, way.Count))
                {
                    var v = way[i];
                    var n = way.GetVertexNormal(i);

                    // 法線表示
                    if (wayOp.showNormal)
                    {
                        DrawLine(v, v + n * 0.3f, color: Color.yellow);
                    }
                    // 中央線
                    if (showInsideNormalMidPoint)
                    {
                        if (way.HalfLineIntersectionXz(new Ray(v - n * 0.01f, -n), out var intersection))
                        {
                            DrawArrow(v, (v + intersection) * 0.5f);
                        }
                    }
                }
            }

            foreach (var node in roadNetwork.Nodes)
            {
                if (nodeOp.visible == false)
                    break;

                for (var i = 0; i < node.Neighbors.Count; ++i)
                {
                    var n = node.Neighbors[i];
                    if (nodeOp.showBorder.visible)
                        DrawWay(n.Border, nodeOp.showBorder.color);

                    if (nodeOp.showSplitTrack.visible)
                    {
                        for (var j = i + 1; j < node.Neighbors.Count; ++j)
                        {
                            var n2 = node.Neighbors[j];
                            if (n == n2)
                                continue;
                            var way = node.CalcTrackWay(n.Link, n2.Link);
                            if (way != null)
                            {
                                foreach (var w in way.BothWays)
                                    DrawWay(w, nodeOp.showSplitTrack.color);
                            }
                        }
                    }
                }

                if (nodeOp.showTrack.visible)
                {
                    foreach (var l in node.Lanes)
                    {
                        foreach (var w in l.BothWays)
                            DrawWay(w, nodeOp.showTrack.color);
                    }
                }
            }

            void DrawLane(RnLane lane)
            {
                if (laneOp.visible == false)
                    return;

                if (laneOp.showId)
                    DebugEx.DrawString($"L[{lane.DebugMyId}]", lane.GetCenter());
                var offset = Vector3.up * (lane.DebugMyId % 10);
                if (laneOp.showLeftWay.visible)
                {
                    DrawWay(lane.LeftWay, color: laneOp.showLeftWay.color.PutA(laneOp.GetLaneAlpha(lane)));
                    if (laneOp.showAttrText)
                        DebugEx.DrawString($"L:{lane.DebugMyId}", lane.LeftWay[0] + offset);
                }

                if (laneOp.showRightWay.visible)
                {
                    DrawWay(lane.RightWay, color: laneOp.showRightWay.color.PutA(laneOp.GetLaneAlpha(lane)));
                    if (laneOp.showAttrText)
                        DebugEx.DrawString($"R:{lane.DebugMyId}", lane.RightWay[0] + offset);
                }

                if (laneOp.showPrevBorder.visible)
                {
                    if (lane.PrevBorder.IsValidOrDefault())
                    {
                        var type = lane.GetBorderDir(RnLaneBorderType.Prev);
                        if (laneOp.showAttrText)
                            DebugEx.DrawString($"[{lane.DebugMyId}]prev={type.ToString()}", lane.PrevBorder.Points.Last() + offset, Vector2.up * 100);
                        DrawWay(lane.PrevBorder, color: laneOp.showPrevBorder.color);
                    }
                }

                if (laneOp.showNextBorder.visible)
                {
                    if (lane.NextBorder.IsValidOrDefault())
                    {
                        var type = lane.GetBorderDir(RnLaneBorderType.Next);
                        if (laneOp.showAttrText)
                            DebugEx.DrawString($"[{lane.DebugMyId}]next={type.ToString()}", lane.NextBorder.Points.Last() + offset, Vector2.up * 100);
                        DrawWay(lane.NextBorder, color: laneOp.showNextBorder.color);
                    }
                }

                if (showSplitLane && lane.HasBothBorder)
                {
                    var vers = lane.GetInnerLerpSegments(splitLaneRate);
                    DrawArrows(vers, false, color: Color.red, arrowSize: 0.1f);
                }
            }
            foreach (var link in roadNetwork.Links)
            {
                if (linkOp.visible == false)
                    break;
                foreach (var lane in link.AllLanes)
                {
                    DrawLane(lane);
                }


                //foreach (var i in Enumerable.Range(0, l.vertices.Count))
                //{
                //    var v = l.vertices[i];
                //    var n = l.GetVertexNormal(i).normalized;
                //    if (showNormal)
                //    {
                //        DrawLine(v, v + n * 0.3f, color: Color.yellow);
                //    }

                //    if (showInsideNormalMidPoint)
                //    {
                //        if (l.HalfLineIntersectionXz(new Ray(v - n * 0.01f, -n), out var intersection))
                //        {
                //            DebugUtil.DrawArrow(v, (v + intersection) * 0.5f);
                //        }
                //    }
                //}


            }

            foreach (var sw in roadNetwork.SideWalks)
            {
                if (sideWalkRoadOp.visible == false)
                    break;
                DrawWay(new RnWay(sw), color: sideWalkRoadOp.color);
            }

            if (laneSplitOp.execute)
            {
                laneSplitOp.execute = false;
                var lane = roadNetwork.CollectAllLanes().FirstOrDefault(l => l.DebugMyId == laneSplitOp.targetLaneId);
                if (lane != null)
                {
                    if (lane.Parent is RnLink link)
                    {
                        var lanes = lane.SplitLane(laneSplitOp.splitNum, true);
                        foreach (var l in lanes)
                        {
                            link.RemoveLane(l.Key);
                            foreach (var newLane in l.Value)
                                link.AddMainLane(newLane);
                        }
                    }
                }
            }
        }
    }
}