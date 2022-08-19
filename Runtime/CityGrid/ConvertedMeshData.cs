﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PLATEAU.Util;
using PLATEAU.Util.Async;
using UnityEngine;
using UnityEngine.Networking;

namespace PLATEAU.CityGrid
{
    /// <summary>
    /// DLL側の PlateauMesh を Unity向けに変換したものです。
    /// </summary>
    internal class ConvertedMeshData
    {
        private readonly Vector3[] vertices;
        private readonly Vector2[] uv1;
        private readonly Vector2[] uv2;
        private readonly Vector2[] uv3;
        private readonly List<List<int>> subMeshTriangles;
        private readonly List<CityGML.Texture> plateauTextures;
        private string Name { get; }
        private readonly Dictionary<int, Texture> subMeshIdToTexture;
        private const string shaderName = "Standard";
        private int SubMeshCount => this.subMeshTriangles.Count;

        public ConvertedMeshData(Vector3[] vertices, Vector2[] uv1, Vector2[] uv2, Vector2[] uv3, List<List<int>> subMeshTriangles,List<CityGML.Texture> plateauTextures, string name)
        {
            this.vertices = vertices;
            this.uv1 = uv1;
            this.uv2 = uv2;
            this.uv3 = uv3;
            this.subMeshTriangles = subMeshTriangles;
            this.plateauTextures = plateauTextures;
            Name = name;
            this.subMeshIdToTexture = new Dictionary<int, Texture>();
        }

        private void AddTexture(int subMeshId, Texture tex)
        {
            this.subMeshIdToTexture.Add(subMeshId, tex);
        }

        /// <summary>
        /// メッシュの形状を変更したあとに必要な後処理です。
        /// </summary>
        private static void PostProcess(Mesh mesh)
        {
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        public async Task PlaceToScene(Transform parentTrans, string gmlAbsolutePath)
        {
            var mesh = GenerateMesh();
            if (mesh.vertexCount <= 0) return;
            var meshObj = GameObjectUtil.AssureGameObjectInChild(Name, parentTrans);
            var meshFilter = GameObjectUtil.AssureComponent<MeshFilter>(meshObj);
            meshFilter.mesh = mesh;
            var renderer = GameObjectUtil.AssureComponent<MeshRenderer>(meshObj);
            
            await LoadTextures(this, this.plateauTextures, gmlAbsolutePath);
            
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                materials[i] = new Material(Shader.Find(shaderName));
                if (this.subMeshIdToTexture.TryGetValue(i, out var tex))
                {
                    if (tex != null)
                    {
                        materials[i].mainTexture = tex;
                        materials[i].name = tex.name;
                    }
                }
            }
            renderer.materials = materials;
        }
        
        private Mesh GenerateMesh()
        {
            var mesh = new Mesh
            {
                vertices = this.vertices,
                uv = this.uv1,
                subMeshCount = this.subMeshTriangles.Count
            };
            // subMesh ごとに Indices(Triangles) を UnityのMeshにコピーします。
            for (int i = 0; i < this.subMeshTriangles.Count; i++)
            {
                mesh.SetTriangles(this.subMeshTriangles[i], i);
            }

            PostProcess(mesh);
            mesh.name = Name;
            return mesh;
        }
        
        private static async Task LoadTextures(ConvertedMeshData meshData, IReadOnlyList<CityGML.Texture> plateauTextures, string gmlAbsolutePath)
        {
            for (int i = 0; i < meshData.SubMeshCount; i++)
            {
                var plateauTex = plateauTextures[i];
                if (plateauTex == null) continue;
                string texUrl = plateauTex.Url;
                if (texUrl == "noneTexture")
                {
                    meshData.AddTexture(i, null);
                    continue;
                }
                string textureFullPath = Path.GetFullPath(Path.Combine(gmlAbsolutePath, "../", texUrl));
                var request = UnityWebRequestTexture.GetTexture($"file://{textureFullPath}");
                request.timeout = 3;
                await request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"failed to load texture : {textureFullPath} result = {(int)request.result}");
                    continue;
                }
                Texture texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                texture.name = Path.GetFileNameWithoutExtension(texUrl);
                meshData.AddTexture(i, texture);
                
            }
        }
    }
}