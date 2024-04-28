using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DM.Utils;
using NaughtyAttributes;
using UnityEngine;
using UnityEditor;

public class SmoothNormalManager : MonoSingleton<SmoothNormalManager>
{
    public List<GameObject> targets = new List<GameObject>();

    [SerializeField, ProgressBar("Progress", 1)]
    private float m_progress;
    [Button("Bake")]
    private async void Bake()
    {
        m_progress = 0;
        Debug.Log("Start Baking");
        foreach (var target in targets)
        {
            if (target.IsNull())
            {
                Debug.LogError($"{nameof(SmoothNormals)}: Target: {target} is null");
                continue;
            }
            foreach (var item in target.GetComponentsInChildren<MeshFilter>())
            {
                if (item.sharedMesh.IsNull())
                {
                    continue;
                }
                SmoothNormals(item.sharedMesh);
            }

            foreach (var item in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (item.sharedMesh.IsNull())
                    continue;
                SmoothNormals(item.sharedMesh);
            }

            await UniTask.Yield();
            m_progress += 1f / targets.Count;
        }
        Debug.Log("Bake Finished");
        return;
        
        void SmoothNormals(Mesh mesh)
        {
            var normalDict = new Dictionary<Vector3, List<NormalWeight>>();
            var triangles = mesh.triangles;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var smoothNormals = mesh.normals;

            for (var i = 0; i <= triangles.Length - 3; i += 3)
            {
                var triangle = new[] { triangles[i], triangles[i + 1], triangles[i + 2] };
                for (int j = 0; j < 3; j++)
                {
                    int vertexIndex = triangle[j];
                    var vertex = vertices[vertexIndex];
                    if (!normalDict.ContainsKey(vertex))
                    {
                        normalDict.Add(vertex, new List<NormalWeight>());
                    }

                    NormalWeight nw;
                    Vector3 lineA;
                    Vector3 lineB;
                    
                    if (j == 0)
                    {
                        lineA = vertices[triangle[1]] - vertex;
                        lineB = vertices[triangle[2]] - vertex;
                    }
                    else if (j == 1)
                    {
                        lineA = vertices[triangle[2]] - vertex;
                        lineB = vertices[triangle[0]] - vertex;
                    }
                    else
                    {
                        lineA = vertices[triangle[0]] - vertex;
                        lineB = vertices[triangle[1]] - vertex;
                    }

                    // 避免精度问题
                    lineA *= 10000f;
                    lineB *= 10000f;

                    var angle =
                        Mathf.Acos(Mathf.Max(
                            Mathf.Min(Vector3.Dot(lineA, lineB) / (lineA.magnitude * lineB.magnitude), 1), -1));
                    
                    nw.normal = Vector3.Cross(lineA, lineB).normalized;
                    nw.weight = angle;
                    normalDict[vertex].Add(nw);
                }
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                var vertex = vertices[i];
                if (!normalDict.ContainsKey(vertex))
                {
                    continue;
                }

                var normalList = normalDict[vertex];

                var smoothNormal = Vector3.zero;
                var weightSum = 0f;
                
                for (var j = 0; j < normalList.Count; j++)
                {
                    var nw = normalList[j];
                    weightSum += nw.weight;
                }

                for (var j = 0; j < normalList.Count; j++)
                {
                    var nw = normalList[j];
                    smoothNormal += nw.normal * nw.weight / weightSum;
                }

                smoothNormal = smoothNormal.normalized;
                
               // 转换至切线空间
                var normal = normals[i];
                var tangent = tangents[i];
                var binormal = (Vector3.Cross(normal, tangent) * tangent.w).normalized;
                var tbn = new Matrix4x4(tangent, binormal, normal, Vector3.one);
                tbn = tbn.transpose;
                smoothNormals[i] = tbn.MultiplyVector(smoothNormal).normalized;
                
                //smoothNormals[i] = smoothNormal;
            }

            mesh.SetUVs(7, smoothNormals);
        }
    }
    
    public struct NormalWeight
    {
        public Vector3 normal;
        public float weight;
    }
}