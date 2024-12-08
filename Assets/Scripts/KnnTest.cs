using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.UI.CanvasScaler;

public class KnnTest : MonoBehaviour
{
    [SerializeField] private GameObject _source;
    [SerializeField] private GameObject _target;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField] private GameObject _stateUI;
    private Material lineMaterial = null;

    private static STATE state = STATE.IDLE;

    private int sourceVertCount = 0;
    private int targetVertCount = 0;
    private Vector3[] sourceVertices;
    private Vector3[] targetVertices;

    private int[] nnIndices;
    private Vector3[] secondVertices;
    private float processTime;


    private enum STATE
    {
        IDLE,
        CALC_ON_CPU,
        CALC_ON_GPU,
        DRAWING
    }


    // Start is called before the first frame update
    void Start()
    {
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        }

        var sourceMesh = _source.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = sourceMesh.vertices;
        sourceVertices = vertices.Distinct().ToArray();
        sourceVertCount = sourceVertices.Length;

        var targetMesh = _target.GetComponent<MeshFilter>().sharedMesh;
        vertices = targetMesh.vertices;
        targetVertices = vertices.Distinct().ToArray();
        targetVertCount = targetVertices.Length;

        nnIndices = new int[sourceVertCount];
    }

    // Update is called once per frame
    void Update()
    {
        if (state == STATE.CALC_ON_CPU)
        {
            calcNearestNeighborsOnCPU();
            state = STATE.DRAWING;
        }
        else if (state == STATE.CALC_ON_GPU)
        {
            calcNearestNeighborsOnGPU();
            state = STATE.DRAWING;
        }
    }

    // CPUで最近傍探索（kd-tree）
    private void calcNearestNeighborsOnCPU()
    {

        Text t = _stateUI.GetComponent<Text>();
        t.text = "Calculating on CPU... ";

        nnIndices = new int[] { };
        secondVertices = new Vector3[sourceVertCount];

        List<Vector3> points = new List<Vector3>(targetVertices);

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        // create kd-tree
        var tree = new KdTree();
        tree.Build(points);

        // query
        for (int si = 0; si < sourceVertCount; ++si) 
        {
            var vert = sourceVertices[si];
            var q = tree.Nearest(vert);
            secondVertices[si] = q;
        }
        stopwatch.Stop();
        processTime = stopwatch.ElapsedMilliseconds;
        t.text = "Calc NN on CPU! : " + ((int)processTime).ToString() + "ms";
    }

    // GPUで最近傍探索（GPUの各コアで線形探索）
    private void calcNearestNeighborsOnGPU()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            UnityEngine.Debug.LogError("Compute Shaders are not supported on this device.");
            return;
        }

        Text t = _stateUI.GetComponent<Text>();
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        // send voxel data to GPU
        _computeShader.SetInt("_SourceVertCount", sourceVertCount);
        _computeShader.SetInt("_TargetVertCount", targetVertCount);

        // generate ComputeBuffer representing vertex array
        var vertBuffer_s = new ComputeBuffer(sourceVertCount, Marshal.SizeOf(typeof(Vector3)));
        vertBuffer_s.SetData(sourceVertices);
        var vertBuffer_t = new ComputeBuffer(targetVertCount, Marshal.SizeOf(typeof(Vector3)));
        vertBuffer_t.SetData(targetVertices);
        var nnIndicesBuffer = new ComputeBuffer(sourceVertCount, Marshal.SizeOf(typeof(int)));
        nnIndicesBuffer.SetData(nnIndices);

        // send mesh data to GPU kernel
        var calcNN = new Voxelizer.Kernel(_computeShader, "CalcNN");
        _computeShader.SetBuffer(calcNN.Index, "_SourceVertBuffer", vertBuffer_s);
        _computeShader.SetBuffer(calcNN.Index, "_TargetVertBuffer", vertBuffer_t);
        _computeShader.SetBuffer(calcNN.Index, "_NearestNeighborIndices", nnIndicesBuffer);

        // execute
        _computeShader.Dispatch(calcNN.Index, sourceVertCount / (int)calcNN.ThreadX + 1, (int)calcNN.ThreadY, (int)calcNN.ThreadZ);

        nnIndicesBuffer.GetData(nnIndices);

        // dispose buffer
        vertBuffer_s.Release();
        vertBuffer_t.Release();
        nnIndicesBuffer.Release();
        
        stopwatch.Stop();
        processTime = stopwatch.ElapsedMilliseconds;
        t.text = "Calc NN on GPU! : " + ((int)processTime).ToString() + "ms";
    }

    void OnRenderObject()
    {
        if (state == STATE.DRAWING)
        {
            lineMaterial.SetPass(0);

            GL.Begin(GL.LINES);
            GL.Color(UnityEngine.Color.green);
            for (int si = 0; si < sourceVertCount; ++si)
            {
                Vector3 secondVertex;
                if (nnIndices.Length <= 0) 
                {
                    secondVertex = secondVertices[si];
                }
                else
                {
                    int ti = nnIndices[si];
                    secondVertex = targetVertices[ti];
                }
                GL.Vertex(sourceVertices[si]);
                GL.Vertex(secondVertex);
            }
            GL.End();

        }
    }

    public void OnClickCPU()
    {
        if (state == STATE.IDLE)
        {
            state = STATE.CALC_ON_CPU;
        }
    }

    public void OnClickGPU()
    {
        if (state == STATE.IDLE)
        {
            state = STATE.CALC_ON_GPU;
        }
    }

    public void OnClickReset()
    {
        if (state == STATE.DRAWING)
        {
            state = STATE.IDLE;

            Text t = _stateUI.GetComponent<Text>();
            t.text = "Press Any Button.";
        }
    }
}
