using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Voxelizer;
using static UnityEngine.GraphicsBuffer;

public class VoxelizeTest : MonoBehaviour
{

    [SerializeField] private GameObject _targetObject;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField, Range(16, 64)] private int _resolution = 64;
    [SerializeField] private GameObject _stateUI;

    private static STATE state = STATE.IDLE;

    private Voxel_t[] voxels = null;
    private float unit = 0f;
    private Mesh drawMesh = null;
    private Mesh meshBuffer = null; // �ꎞ�ۑ��p�̃o�b�t�@

    private enum STATE
    {
        IDLE,
        CALC_ON_CPU,
        CALC_ON_GPU,
        DRAWING,
        DRAW_DONE
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (state == STATE.CALC_ON_CPU)
        {
            voxelizeOnCPU();
            state = STATE.DRAWING;
        }
        else if (state == STATE.CALC_ON_GPU)
        {
            Text t = _stateUI.GetComponent<Text>();
            t.text = "Calculating on GPU... ";
            voxelizeOnGPU();
            state = STATE.DRAWING;
        }
        else if (state == STATE.DRAWING)
        {
            drawMesh = Voxelizer.Render.BuildMesh(voxels, unit);
            meshBuffer = _targetObject.GetComponent<MeshFilter>().sharedMesh;
            _targetObject.GetComponent<MeshFilter>().sharedMesh = drawMesh;
            state = STATE.DRAW_DONE;
        }
    }

    // CPU��voxelize
    private void voxelizeOnCPU()
    {
        Text t = _stateUI.GetComponent<Text>();

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        Voxelizer.CPUVoxelizer.Voxelize(_targetObject, _resolution, out voxels, out unit);
        stopwatch.Stop();
        
        float processTime = stopwatch.ElapsedMilliseconds;
        t.text = "Voxelized on CPU! : \n" + ((int)processTime).ToString() + " ms";
    }

    // GPU��voxelize
    private void voxelizeOnGPU()
    {

        if (!SystemInfo.supportsComputeShaders)
        {
            UnityEngine.Debug.LogError("Compute Shaders are not supported on this device.");
            return;
        }

        Text t = _stateUI.GetComponent<Text>();

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        Voxelizer.GPUVoxelizer.VoxelizeSurface(ref _computeShader, in _targetObject, out voxels, out unit, _resolution);
        stopwatch.Stop();

        float processTime = stopwatch.ElapsedMilliseconds;
        t.text = "Voxelized on GPU! : \n" + ((int)processTime).ToString() + " ms";
    }

    // �M�Y�����V�[���r���[�ɕ`�悷��
    void OnDrawGizmos()
    {
        // ���_����ɃM�Y���̃T�C�Y��ݒ�
        float axisLength = 1000.0f;

        // X���i�ԁj
        Gizmos.color = Color.red;
        Gizmos.DrawLine(Vector3.zero, Vector3.right * axisLength);

        // Y���i�΁j
        Gizmos.color = Color.green;
        Gizmos.DrawLine(Vector3.zero, Vector3.up * axisLength);

        // Z���i�j
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * axisLength);

        // ���_�ɏ����ȋ��̂�`��
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(Vector3.zero, 1.0f);
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
        if (state == STATE.DRAW_DONE)
        {
            _targetObject.GetComponent<MeshFilter>().sharedMesh = meshBuffer;
            state = STATE.IDLE;

            Text t = _stateUI.GetComponent<Text>();
            t.text = "Press Any Button.";
        }
    }
}
