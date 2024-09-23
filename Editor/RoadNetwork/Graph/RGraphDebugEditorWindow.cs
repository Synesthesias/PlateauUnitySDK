using PLATEAU.RoadNetwork;
using PLATEAU.RoadNetwork.Graph;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PLATEAU.Editor.RoadNetwork.Graph
{
    public class RGraphDebugEditorWindow : EditorWindow
    {
        public interface IInstanceHelper
        {
            // グラフ取得
            PLATEAURGraph GetGraph();

            // グラフ作成
            PLATEAURGraph CreateGraph();

            HashSet<RFace> TargetFaces { get; }

            HashSet<REdge> TargetEdges { get; }

            HashSet<RVertex> TargetVertices { get; }

            bool IsTarget(RFace face);

            //// モデル作成する
            //void CreateRnModel();

            //// PLATEAURGraphDrawerDebug GetDrawer();

            //void CreateTranMesh();
        }

        private const string WindowName = "PLATEAU RGraph Editor";

        public IInstanceHelper InstanceHelper { get; set; }

        // 作成時にマージも同時に行うかどうか
        public bool mergeOnCreate = true;
        private float mergeCellSize = 0.2f;
        private int mergeCellLength = 2;
        private float heightTolerance = 0.1f;
        private float removeMidPointTolerance = 0.3f;

        private int showVertexId = -1;

        public HashSet<RFace> TargetFaces { get; } = new();

        private class FaceEdit
        {
            public void Update(RGraphDebugEditorWindow work, RFace f)
            {
                if (f == null)
                    return;

                RnEditorUtil.TargetToggle($"ID[{f.DebugMyId}]", work.InstanceHelper.TargetFaces, f);
                EditorGUILayout.EnumFlagsField("RoadType", f.RoadTypes);
                if (GUILayout.Button("Remove Inner Vertex"))
                {
                    f.RemoveInnerVertex();
                }
            }
        }
        FaceEdit faceEdit = new FaceEdit();

        private class EdgeEdit
        {
            public void Update(RGraphDebugEditorWindow work, REdge e)
            {
                if (e == null)
                    return;
                RnEditorUtil.TargetToggle($"ID[{e.DebugMyId}]", work.InstanceHelper.TargetEdges, e);
                using (new EditorGUI.DisabledScope(false))
                {
                    EditorGUILayout.LabelField("Edge ID", e.DebugMyId.ToString());
                    EditorGUILayout.LongField("V0", e.V0.GetDebugMyIdOrDefault());
                    EditorGUILayout.LongField("V1", e.V1.GetDebugMyIdOrDefault());
                }
            }
        }
        EdgeEdit edgeEdit = new EdgeEdit();

        private class VertexEdit
        {

            public void Update(RGraphDebugEditorWindow work, RVertex v)
            {
                if (v == null)
                    return;

                RnEditorUtil.TargetToggle($"ID[{v.DebugMyId}]", work.InstanceHelper.TargetVertices, v);
                using (new EditorGUI.DisabledScope(false))
                {
                    EditorGUILayout.Vector3Field("Pos", v.Position);
                    foreach (var e in v.Edges)
                    {
                        EditorGUILayout.LongField("Edge", (long)e.DebugMyId);
                    }
                }

            }
        }
        VertexEdit vertexEdit = new VertexEdit();


        public void Reinitialize() { }

        private void Initialize() { }

        private void OnEnable()
        {
            Initialize();
        }

        private void VertexGUI(RVertex v)
        {
            if ((int)v.DebugMyId != showVertexId)
                return;
            RnEditorUtil.Separator();
            EditorGUILayout.IntField("ID", (int)v.DebugMyId);
            foreach (var e in v.GetNeighborVertices())
            {
                EditorGUILayout.IntField("Neighbor", (int)e.DebugMyId);
            }
            RnEditorUtil.Separator();
        }

        /// <Summary>
        /// ウィンドウのパーツを表示します。
        /// </Summary>
        private void OnGUI()
        {
            if (InstanceHelper == null)
                return;

            var target = InstanceHelper.GetGraph();
            if (!target)
            {

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Graph"))
                    {
                        target = InstanceHelper.CreateGraph();
                        if (mergeOnCreate)
                        {
                            target.Graph.Optimize(mergeCellSize, mergeCellLength, removeMidPointTolerance);
                        }
                    }
                    mergeOnCreate = EditorGUILayout.Toggle("MergeOnCreate", mergeOnCreate);
                }
                return;
            }

            var graph = target.Graph;
            if (graph == null)
                return;


            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Optimize"))
                {
                    graph.Optimize(mergeCellSize, mergeCellLength, removeMidPointTolerance);
                }

                mergeCellSize = EditorGUILayout.FloatField("mergeCellSize", mergeCellSize);
                mergeCellLength = EditorGUILayout.IntField("MergeCellLength", mergeCellLength);
                removeMidPointTolerance = EditorGUILayout.FloatField("RemoveMidPointTolerance", removeMidPointTolerance);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("InsertVertexInNearEdge"))
                {
                    graph.InsertVertexInNearEdge(mergeCellSize);
                }

                if (GUILayout.Button("Vertex Reduction"))
                {
                    graph.VertexReduction(mergeCellSize, mergeCellLength, removeMidPointTolerance);
                }

                if (GUILayout.Button("Edge Reduction"))
                    graph.EdgeReduction();

                if (GUILayout.Button("Separate Face"))
                    graph.SeparateFaces();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check Intersection"))
                {
                    graph.InsertVerticesInEdgeIntersection(heightTolerance);
                }
                heightTolerance = EditorGUILayout.FloatField("HeightEpsilon", heightTolerance);
            }

            //if (GUILayout.Button("Create RnModel"))
            //    InstanceHelper.CreateRnModel();

            //if (GUILayout.Button("Create TranMEsh"))
            //    InstanceHelper.CreateTranMesh();

            RnEditorUtil.Separator();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Face", graph.Faces.Count);
                EditorGUILayout.IntField("Edge", graph.GetAllEdges().Count());
                EditorGUILayout.IntField("Vertex", graph.GetAllVertices().Count());
            }

            showVertexId = EditorGUILayout.IntField("ShowVertexId", showVertexId);


            RnEditorUtil.Separator();
            EditorGUILayout.LabelField("=============== Face ==============");
            foreach (var face in graph.Faces)
            {
                if (InstanceHelper.IsTarget(face) || InstanceHelper.TargetFaces.Contains(face))
                {
                    RnEditorUtil.Separator();
                    faceEdit.Update(this, face);
                }
            }

            RnEditorUtil.Separator();
            EditorGUILayout.LabelField("=============== Edge ==============");
            foreach (var edge in InstanceHelper.TargetEdges)
            {
                RnEditorUtil.Separator();
                edgeEdit.Update(this, edge);
            }

            RnEditorUtil.Separator();
            EditorGUILayout.LabelField("=============== Vertex ==============");
            foreach (var vertex in InstanceHelper.TargetVertices)
            {
                RnEditorUtil.Separator();
                vertexEdit.Update(this, vertex);
            }
        }

        /// <summary>
        /// ウィンドウを取得する、存在しない場合に生成する
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="focus"></param>
        /// <returns></returns>
        public static RGraphDebugEditorWindow OpenWindow(IInstanceHelper instance, bool focus)
        {
            var ret = GetWindow<RGraphDebugEditorWindow>(WindowName, focus);
            ret.InstanceHelper = instance;
            return ret;
        }

        /// <summary>
        /// ウィンドウのインスタンスを確認する
        /// ラップ関数
        /// </summary>
        /// <returns></returns>
        public static bool HasOpenInstances()
        {
            return HasOpenInstances<RGraphDebugEditorWindow>();
        }

    }
}