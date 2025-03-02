using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class BVHSerializer
{
    public static string AssetPath = "Assets/SerializedBVH.BVH";

    private static List<float> _triangleData = new List<float>();
    private static List<float> _aabbData = new List<float>();
    
    private static int _triangleIndexStartPointer = 0;
    private static int _triangleIndexEndPointer = 0;
    
    public static bool GetTriangle(int index, out Triangle triangle)
    {
        triangle = default;
        if (index < 0 || index >= _triangleData.Count / 10)
        {
            Debug.LogError($"Invalid index: {index}");
            return false;
        }
        
        var startIndex = index * 11;
        var materialIndex = (int)_triangleData[startIndex];
        var meshIndex = (int)_triangleData[startIndex + 1];
        var v0 = new Vector3(_triangleData[startIndex + 2], _triangleData[startIndex + 3], _triangleData[startIndex + 4]);
        var v1 = new Vector3(_triangleData[startIndex + 5], _triangleData[startIndex + 6], _triangleData[startIndex + 7]);
        var v2 = new Vector3(_triangleData[startIndex + 8], _triangleData[startIndex + 9], _triangleData[startIndex + 10]);
        triangle = new Triangle(materialIndex, meshIndex, v0, v1, v2);
        return true;
    }

    public static void Serialize(BVHNode node, 
        out List<float> triangleData,
        out List<float> aabbData)
    {
        Reset();
        
        var current = 0;
        SerializeBVHNode(node, -1, ref current);

        aabbData = new List<float>();
        triangleData = new List<float>();

        aabbData.AddRange(_aabbData);
        triangleData.AddRange(_triangleData);
    }
    public static void Write(BVHNode node)
    {
        Reset();
        
        var current = 0;
        SerializeBVHNode(node, -1, ref current);
        
        WriteBinary();
    }

    public static bool Read(string path, out BVHNode root)
    {
        root = null;
        if (!path.ToLower().EndsWith(".bvh"))
        {
            Debug.LogError($"Invalid file extension: {path}");
            return false;
        }
        
        // try
        {
            using (var reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                var triPointCount = reader.ReadInt32();
                var triPointData = new List<float>();
                for (var i = 0; i < triPointCount; i+= 11)
                {
                    triPointData.Add(reader.ReadInt32()); // Material index
                    triPointData.Add(reader.ReadInt32()); // Mesh index
                    for (var j = 0; j < 9; j++) // Vertices
                    {
                        triPointData.Add(reader.ReadSingle());
                    }
                }

                var allNodeData = new List<NodeSerializedData>();
                var aabbCount = reader.ReadInt32();
                
                for (var i = 0; i < aabbCount; i+= 9)
                {
                    var dataSingleNode = new float[9];
                    for (int j = 0; j < 9; j++)
                    {
                        dataSingleNode[j] = reader.ReadSingle();
                    }
                    
                    var min = new Vector3(dataSingleNode[0], dataSingleNode[1], dataSingleNode[2]);
                    var max = new Vector3(dataSingleNode[3], dataSingleNode[4], dataSingleNode[5]);
                    var parentIndex = (int)dataSingleNode[6] >= 0 ? (int)dataSingleNode[6] / 9 : -1;
                    var leftChildPointer = (int)dataSingleNode[7] > 0 ? (int)dataSingleNode[7] / 9 : (int)dataSingleNode[7];
                    var rightChildPointer = (int)dataSingleNode[8] > 0 ? (int)dataSingleNode[8] / 9 : (int)dataSingleNode[8];
                    
                    allNodeData.Add(new NodeSerializedData
                    {
                        Min = min,
                        Max = max,
                        ParentIndex = parentIndex,
                        LeftChildPointer = leftChildPointer,
                        RightChildPointer = rightChildPointer
                    });
                }

                var allNodes = new BVHNode[allNodeData.Count];
                for (var i = 0; i < allNodeData.Count; i++)
                {
                    var nodeData = allNodeData[i];
                    var aabb = new AABB(nodeData.Min, nodeData.Max);
                    allNodes[i] = new BVHNode(aabb);
                    if (nodeData.ParentIndex < 0)
                    {
                        root = allNodes[i];
                    }
                }
                
                for (var i = 0; i < allNodeData.Count; i++)
                {
                    var nodeData = allNodeData[i];
                    var node = allNodes[i];
                    if (nodeData.LeftChildPointer > 0)
                    {
                        node.Left = allNodes[nodeData.LeftChildPointer];
                    }

                    if (nodeData.RightChildPointer > 0)
                    {
                        node.Right = allNodes[nodeData.RightChildPointer];
                    }
                    
                    if (nodeData.LeftChildPointer < 0)
                    {
                        var triangleStart = -nodeData.LeftChildPointer - 1;
                        var triangleEnd = -nodeData.RightChildPointer - 1;
                        
                        var triPoints = triPointData.GetRange(triangleStart, triangleEnd - triangleStart); 
                        var triangles = new List<Triangle>();
                        for (var j = 0; j < triPoints.Count; j+= 11) // 11 = 1 (Material index) + 1 (Mesh index) + 3 * 3 (Vertices)
                        {
                            var materialIndex = (int)triPoints[j];
                            var meshIndex = (int)triPoints[j + 1];
                            var v0 = new Vector3(triPoints[j + 2], triPoints[j + 3], triPoints[j + 4]);
                            var v1 = new Vector3(triPoints[j + 5], triPoints[j + 6], triPoints[j + 7]);
                            var v2 = new Vector3(triPoints[j + 8], triPoints[j + 9], triPoints[j + 10]);
                            triangles.Add(new Triangle(materialIndex, meshIndex, v0, v1, v2));
                        }
                        node.Triangles = triangles;
                    }
                }
            }
            return true;
        }
        /*
        catch(Exception e)
        {
            Debug.LogError($"Failed to read BVH file at: {path}, " + e.Message);
            return false;
        }
        */
    }
    private static void SerializeBVHNode(BVHNode node, int parentIndex, ref int currentIndex)
    {
        if (node is null) return;
        
        var nodeIndex = currentIndex;
        currentIndex += 9;
        
        AddVectors(_aabbData, node.AABB.Min); // Min [nodeIndex]
        AddVectors(_aabbData, node.AABB.Max); // Max [nodeIndex + 3]
        
        _aabbData.Add(parentIndex); // Parent index [nodeIndex + 6]
        if (node.IsLeaf)
        {
            var triangleCount = node.Triangles.Count;
            // 11 = 1 (Material index) + 1 (Mesh index) + 3 * 3 (Vertices) be aware that mat index will 
            _triangleIndexEndPointer = _triangleIndexStartPointer + triangleCount * 11; // range = [start, end)
            _aabbData.Add(-(_triangleIndexStartPointer + 1)); // Triangle start pointer (negative) [nodeIndex + 7]
            _aabbData.Add(-(_triangleIndexEndPointer + 1)); // Triangle end pointer (negative) [nodeIndex + 8]
            _triangleIndexStartPointer = _triangleIndexEndPointer;
            SerializeTriangles(node.Triangles);
        }
        else
        {
            // Left child pointer [nodeIndex + 7]
            if (node.Left is not null)
                _aabbData.Add(nodeIndex + 9); 
            else
                _aabbData.Add(0); 
            
            _aabbData.Add(0); // Reserved position for right child pointer [nodeIndex + 8]. Will be filled later.
            var rightChildIndex = nodeIndex + 8;

            if (node.Left is not null)
                SerializeBVHNode(node.Left, nodeIndex, ref currentIndex);
            
            // If has a right child, update pointer
            if (node.Right is not null)
                _aabbData[rightChildIndex] = currentIndex;
            
            if (node.Right is not null)
                SerializeBVHNode(node.Right, nodeIndex, ref currentIndex);
        }
    }
    
    private static void SerializeTriangles(List<Triangle> triangles)
    {
        AddTriangles(_triangleData, triangles.ToArray());
    }
    
    private static void AddVectors(List<float> dataList, params Vector3[] vectors)
    {
        foreach (var vector in vectors)
        {
            dataList.Add(vector.x);
            dataList.Add(vector.y);
            dataList.Add(vector.z);
        }
    }
    
    private static void AddTriangles(List<float> dataList, params Triangle[] triangles)
    {
        foreach (var triangle in triangles)
        {
            dataList.Add(triangle.MaterialIndex);
            dataList.Add(triangle.MeshIndex);
            AddVectors(dataList, new[] { triangle.V0, triangle.V1, triangle.V2 });
        }
    }

    private static void WriteBinary()
    {
        try
        {
            using (var writer = new BinaryWriter(File.Open(AssetPath, FileMode.Create)))
            {
                writer.Write(_triangleData.Count);
                foreach (var value in _triangleData)
                {
                    writer.Write(value);
                }

                writer.Write(_aabbData.Count);
                foreach (var value in _aabbData)
                {
                    writer.Write(value);
                }
            }

            if (File.Exists(AssetPath))
            {
                var fileInfo = new FileInfo(AssetPath);
                var exceptedSize = (_triangleData.Count + _aabbData.Count) * sizeof(float) + 2 * sizeof(int);
                Debug.Assert(fileInfo.Length == exceptedSize, $"File size mismatch. " +
                                                              $"Excepted: {fileInfo.Length}. Actual: {exceptedSize}");
            }
        }
        catch(Exception e)
        {
            Debug.LogError($"Failed to write BVH file at: {AssetPath}, " + e.Message);
        }
    }

    private static void Reset()
    {
        _triangleData.Clear();
        _triangleData.Clear();
        _aabbData.Clear();
        _triangleIndexEndPointer = 0;
        _triangleIndexStartPointer = 0;
    }

    private struct NodeSerializedData
    {
        public Vector3 Min;
        public Vector3 Max;
        public int ParentIndex;
        public int LeftChildPointer;
        public int RightChildPointer;
    }
}
