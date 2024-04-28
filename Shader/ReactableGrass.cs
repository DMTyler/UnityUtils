using System;
using System.Collections.Generic;
using System.Linq;
using DM.Utils;
using UnityEngine;

public class ReactableGrass : MonoBehaviour
{
    [SerializeField] private float m_detectRadius = 0.2f;
    [SerializeField] private float m_pushDistance = 0.1f;
    [SerializeField] private Vector3 m_detectArea = new Vector3(1, 1, 1);
    private Material m_material;
    private List<Transform> m_transforms = new ();
    private static readonly int ReactObject0 = Shader.PropertyToID("_ReactObject0");
    private static readonly int ReactObject1 = Shader.PropertyToID("_ReactObject1");
    private static readonly int ReactObject2 = Shader.PropertyToID("_ReactObject2");
    private static readonly int ReactObject3 = Shader.PropertyToID("_ReactObject3");
    private static readonly int ReactObject4 = Shader.PropertyToID("_ReactObject4");
    private static readonly int ReactObject5 = Shader.PropertyToID("_ReactObject5");
    private static readonly int ReactObject6 = Shader.PropertyToID("_ReactObject6");
    private static readonly int ReactObject7 = Shader.PropertyToID("_ReactObject7");
    private static readonly int ReactObject8 = Shader.PropertyToID("_ReactObject8");
    private static readonly int ReactObject9 = Shader.PropertyToID("_ReactObject9");
    private void Start()
    {
        m_material = GetComponent<MeshRenderer>().material;
        if (m_material.IsNull() || m_material.shader.name != "Custom/Grass")
        {
#if UNITY_EDITOR
            Debug.LogError($"{gameObject.name}: Material is not assigned or shader is not Custom/Grass");
#endif
            this.enabled = false;
        }
    }

    private void Update()
    {
        var colliders = new Collider[10];
        var count = Physics.OverlapBoxNonAlloc(transform.position, m_detectArea, colliders, Quaternion.identity);
        if (count != 0)
        {
            var transforms = colliders.Where(c => c.CompareTag("Player") || c.CompareTag("Enemy"))
            .Select(c => c.transform)
            .ToList();
            
        }
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, m_detectArea);
    }
}
