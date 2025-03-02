using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BVH
{
    
    public static int MaxTrianglesPerNode = 4;
    private static List<Material> _materials = new List<Material>();
    public static int MaterialCount => _materials.Count;
    private static List<Mesh> _meshes = new List<Mesh>();
    public static int MeshCount => _meshes.Count;
    public static BVHNode Build(IEnumerable<Transform> transforms)
    {
        var triangles = new List<Triangle>();

        foreach (var transform in transforms)
        {
            CollectTriangles(transform, triangles);
        }

        return Build(triangles);
    }
    
    public static BVHNode Build(Scene scene)
    {
        var triangles = new List<Triangle>();
        
        foreach (var rootGameObject in scene.GetRootGameObjects())
        {
            CollectTriangles(rootGameObject.transform, triangles);
        }

        return Build(triangles);
    }

    public static BVHNode Build(List<Triangle> triangles)
    {
        if (triangles.Count == 0)
        {
            return null;
        }

        var node = Build(triangles, 0);
        return node;
    }
    
    public static bool GetTriangle(int index, out Triangle triangle)
    {
        return BVHSerializer.GetTriangle(index, out triangle);
    }
    
    public static bool GetMaterial(int index, out Material mat)
    {
        var cnt = _materials.Count;
        if (index < 0 || index >= cnt)
        {
            mat = default;
            return false;
        }
        mat = _materials[index];
        return true;
    }

    public static bool GetMaterialWithTriIndex(int index, out Material mat)
    {
        mat = default;
        return GetTriangle(index, out var triangle) && GetMaterial(triangle.MaterialIndex, out mat);
    }

    private static BVHNode Build(List<Triangle> triangles, int depth)
    {
        if (triangles.Count == 0)
        {
            return null;
        }
        
        if (triangles.Count <= MaxTrianglesPerNode)
        {
            var leafNode = new BVHNode(ComputeBounds(triangles));
            leafNode.Triangles = triangles;
            return leafNode;
        }
        
        var bounds = ComputeBounds(triangles);
        var axis = depth % 3;
        triangles.Sort((lhs, rhs) =>
        {
            var lhsCenter = lhs.GetBounds().center[axis];
            var rhsCenter = rhs.GetBounds().center[axis];
            return lhsCenter.CompareTo(rhsCenter);
        });
        
        var mid = triangles.Count / 2;
        var left = triangles.GetRange(0, mid);
        var right = triangles.GetRange(mid, triangles.Count - mid);
        
        var node = new BVHNode(bounds);
        node.Left = Build(left, depth + 1);
        node.Right = Build(right, depth + 1);
        
        return node;
    }
    
    private static void CollectTriangles(Transform transform, List<Triangle> triangles)
    {
        foreach (Transform child in transform)
        {
            CollectTriangles(child, triangles);
        }
        
        var meshFilter = transform.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            return;
        }
        
        var material = meshFilter.GetComponent<Renderer>().sharedMaterial;
        if (material == null)
        {
            return;
        }

        var materialIndex = _materials.IndexOf(material);
        if (materialIndex == -1)
        {
            materialIndex = _materials.Count;
            _materials.Add(material);
        }

        var mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            return;
        }
        
        var meshIndex = _meshes.IndexOf(mesh);
        if (meshIndex == -1)
        {
            meshIndex = _meshes.Count;
            _meshes.Add(mesh);
        }
        
        var vertices = mesh.vertices;
        var trianglesIndices = mesh.triangles;
        for (var i = 0; i < trianglesIndices.Length; i += 3)
        {
            // Get the world space vertices
            var v0 = transform.TransformPoint(vertices[trianglesIndices[i]]);
            var v1 = transform.TransformPoint(vertices[trianglesIndices[i + 1]]);
            var v2 = transform.TransformPoint(vertices[trianglesIndices[i + 2]]);
            triangles.Add(new Triangle(materialIndex, meshIndex, v0, v1, v2));
        }
    }
    
    private static AABB ComputeBounds(List<Triangle> triangles)
    {
        var bounds = new AABB(triangles[0].GetBounds());
        foreach (var triangle in triangles)
        {
            bounds = AABB.Union(bounds, new AABB(triangle.GetBounds()));
        }
        
        return bounds;
    }
}

public class BVHNode
{
    public AABB AABB;
    public BVHNode Left, Right;
    public List<Triangle> Triangles;

    public bool IsLeaf => Triangles != null;

    public int Count => 1 + (Left?.Count ?? 0) + (Right?.Count ?? 0);

    public BVHNode(AABB aabb)
    {
        AABB = aabb;
    }

    public bool GetHit(Ray ray, float maxDistance, out float closestT, out Triangle closestTriangle)
    {
        closestT = float.MaxValue;
        closestTriangle = default;
        
        if (!AABB.Intersect(ray, maxDistance)) return false;
        if (IsLeaf)
        {
            foreach (var triangle in Triangles)
            {
                if (triangle.Intersect(ray, maxDistance, out var t) && t < closestT)
                {
                    closestT = t;
                    closestTriangle = triangle;
                }
            }

            return closestT < maxDistance;
        }
        else
        {
            var hitLeft = false;
            var hitRight = false;
            if (Left is not null) hitLeft = Left.GetHit(ray, maxDistance, out closestT, out closestTriangle);
            if (Right is not null) hitRight = Right.GetHit(ray, maxDistance, out closestT, out closestTriangle);
            return hitLeft || hitRight;
        }
    }
}

public struct Triangle
{
    public int MaterialIndex;
    public int MeshIndex;
    public Vector3 V0, V1, V2;
    
    public Triangle(int materialIndex, int meshIndex, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        MaterialIndex = materialIndex;
        MeshIndex = meshIndex;
        V0 = v0;
        V1 = v1;
        V2 = v2;
    }
    public Bounds GetBounds()
    {
        var bound = new Bounds(V0, Vector3.zero);
        bound.Encapsulate(V1);
        bound.Encapsulate(V2);
        return bound;
    }

    public bool Intersect(Ray ray, float maxDistance, out float t)
    {
        t = float.MaxValue;
    
        var edge1 = V1 - V0;
        var edge2 = V2 - V0;

        var h = Vector3.Cross(ray.direction, edge2);
        var a = Vector3.Dot(edge1, h);

        // Ray is parallel to the triangle
        if (a > -0.00001f && a < 0.00001f)
            return false;

        var f = 1.0f / a;
        var s = ray.origin - V0;
        var u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f)
            return false;

        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(ray.direction, q);

        if (v < 0.0f || u + v > 1.0f)
            return false;

        t = f * Vector3.Dot(edge2, q);

        return t > 0.00001f && t < maxDistance;
    }
}

public struct AABB
{
    public Vector3 Min, Max;
    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public AABB(Bounds bounds)
    {
        Min = bounds.min;
        Max = bounds.max;
    }

    public bool Intersect(Ray ray, float maxDistance)
    {
        var invDir = new Vector3(1.0f / ray.direction.x, 1.0f / ray.direction.y, 1.0f / ray.direction.z);
        var tx1 = (Min.x - ray.origin.x) * invDir.x;
        var tx2 = (Max.x - ray.origin.x) * invDir.x;
        
        var tmin = Math.Min(tx1, tx2);
        var tmax = Math.Max(tx1, tx2);
        
        var ty1 = (Min.y - ray.origin.y) * invDir.y;
        var ty2 = (Max.y - ray.origin.y) * invDir.y;
        
        tmin = Math.Max(tmin, Math.Min(ty1, ty2));
        tmax = Math.Min(tmax, Math.Max(ty1, ty2));
        
        return tmax >= tmin && tmax >= 0 && tmin <= maxDistance;
    }
    
    public Bounds ToBounds()
    {
        return new Bounds((Min + Max) * 0.5f, Max - Min);
    }

    public static AABB Union(AABB lhs, AABB rhs)
    {
        var min = Vector3.Min(lhs.Min, rhs.Min);
        var max = Vector3.Max(lhs.Max, rhs.Max);
        return new AABB(min, max);
    }
}
