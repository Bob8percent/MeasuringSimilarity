using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MeasuringSimilarity : MonoBehaviour
{

    [SerializeField] private GameObject _destination;
    [SerializeField] private GameObject _target;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField, Range(16, 64)] private int _resolution = 16;
    [SerializeField] private GameObject _similarityText;

    private GameObject cloneTarget;
    private float similarity;

    void Start()
    {
        cloneTarget = Instantiate(_target);
        MeshRenderer meshRenderer = cloneTarget.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
        SkinnedMeshRenderer skinnedMeshRenderer = cloneTarget.GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer != null)
        {
            skinnedMeshRenderer.enabled = false;
        }

        similarity = 0;
    }

    void Update()
    {
        // メッシュが編集されていないときは類似率を計算しない
        //if (!ShapeMatching.IsEditMesh(ref _target, ref target))
        //{
        //    return;
        //}
        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("Compute Shaders are not supported on this device.");
            return;
        }

        // ShapeMatching法を使って、比較するオブジェクトを整列
        ShapeMatching.AlignMesh(ref cloneTarget, ref _destination);

        var translation = cloneTarget.transform.position;
        var rotation = cloneTarget.transform.rotation;
        Debug.LogFormat("translation: ({0}, {1}, {2})", translation.x, translation.y, translation.z);
        Debug.LogFormat("rotation: ({0}, {1}, {2})", rotation.x, rotation.y, rotation.z);

        // それぞれのメッシュの類似度を求める
        Mesh displayMesh = _target.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Mesh targetMesh = cloneTarget.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Mesh destinationMesh = _destination.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        similarity = Voxelizer.GPUVoxelizer.CalcSimilarity(ref _computeShader, cloneTarget, _destination, ref displayMesh, _resolution);

        // 類似率をゲーム画面に表示
        Text t = _similarityText.GetComponent<Text>();
        float s = Mathf.Floor((similarity * 100) * Mathf.Pow(10, 2)) / Mathf.Pow(10, 2);
        t.text = "類似度: " + ((int)s).ToString() + "%";
    }

    // ギズモをシーンビューに描画する
    void OnDrawGizmos()
    {
        // 原点を基準にギズモのサイズを設定
        float axisLength = 1000.0f;

        // X軸（赤）
        Gizmos.color = Color.red;
        Gizmos.DrawLine(Vector3.zero, Vector3.right * axisLength);

        // Y軸（緑）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(Vector3.zero, Vector3.up * axisLength);

        // Z軸（青）
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * axisLength);

        // 原点に小さな球体を描画
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(Vector3.zero, 1.0f);
    }    
}