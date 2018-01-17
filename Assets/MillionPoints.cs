using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Threading;
using Svelto.Tasks;
using Random = UnityEngine.Random;

//[RequireComponent(typeof(MeshRenderer))]
//[RequireComponent(typeof(MeshFilter))]

public class MillionPoints : MonoBehaviour {

    // ==============================
    #region // Defines
        
    const int ThreadBlockSize = 256;

    struct ParticleData
    {
        public Vector3 BasePosition;
        public Vector3 Position;
        public Vector3 Albedo;
        public float rotationSpeed;
    }

    #endregion // Defines

    // --------------------------------------------------
    #region // Serialize Fields

    [SerializeField]
    int _particleCount = 250000;

    [SerializeField]
    [Range(-Mathf.PI, Mathf.PI)]
    float _phi = Mathf.PI;

    [SerializeField]
    ComputeShader _ComputeShader;
    
    [SerializeField]
    Material _material;
    
    /// 表示領域の中心座標
    [SerializeField]
    Vector3 _BoundCenter = Vector3.zero;
    
    /// 表示領域のサイズ
    [SerializeField]
    Vector3 _BoundSize = new Vector3(300f, 300f, 300f);

    #endregion // Serialize Fields

    // --------------------------------------------------
    #region // Private Fields

    ComputeBuffer _ParticleDataBuffer;
    
    /// GPU Instancingの為の引数
    uint[] _GPUInstancingArgs = new uint[5] { 0, 0, 0, 0, 0 };
    
    /// GPU Instancingの為の引数バッファ
    ComputeBuffer _GPUInstancingArgsBuffer;

    // point for particle
    Mesh _pointMesh;
    private ParticleData[] _particleDataArr;
    private MultiThreadedParallelTaskCollection _multiParallelTask;

    #endregion // Private Fields

    // --------------------------------------------------
    #region // MonoBehaviour Methods

    void Awake()
    {
        Application.targetFrameRate = 90;
        QualitySettings.vSyncCount = 0;
    }
    
    public const uint NUM_OF_THREADS = 16;

    void Start()
    {
        // バッファ生成
        this._ParticleDataBuffer = new ComputeBuffer(this._particleCount, Marshal.SizeOf(typeof(ParticleData)));
        this._GPUInstancingArgsBuffer = new ComputeBuffer(1, this._GPUInstancingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _particleDataArr = new ParticleData[this._particleCount];
        
        // set default position
        for (int i = 0; i < _particleCount; i++)
        {
            _particleDataArr[i].BasePosition = new Vector3(Random.Range(-10.0f, 10.0f), Random.Range(-10.0f, 10.0f), Random.Range(-10.0f, 10.0f));
            _particleDataArr[i].Albedo = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
            _particleDataArr[i].rotationSpeed = Random.Range(1.0f, 100.0f);
        }
        this._ParticleDataBuffer.SetData(_particleDataArr);
        
        // creat point mesh
        _pointMesh = new Mesh();
        _pointMesh.vertices = new Vector3[] {
            new Vector3 (0, 0),
        };
        _pointMesh.normals = new Vector3[] {
            new Vector3 (0, 1, 0),
        };
        _pointMesh.SetIndices(new int[] { 0 }, MeshTopology.Points, 0);
        
        // GPU Instaicing
        this._GPUInstancingArgs[0] = (this._pointMesh != null) ? this._pointMesh.GetIndexCount(0) : 0;
        this._GPUInstancingArgs[1] = (uint)this._particleCount;
        this._GPUInstancingArgsBuffer.SetData(this._GPUInstancingArgs);
        this._material.SetBuffer("_ParticleDataBuffer", this._ParticleDataBuffer);
               
        var countn = _particleCount / NUM_OF_THREADS;

        _multiParallelTask = new MultiThreadedParallelTaskCollection(NUM_OF_THREADS, false);
        
        for (int i = 0; i < NUM_OF_THREADS; i++)
            _multiParallelTask.Add(new Enumerator((int) (countn * i), (int) countn, this));

        Run().Run();
    }
    
    static uint Hash(uint s)
    {
        s ^= 2747636419u;
        s *= 2654435769u;
        s ^= s >> 16;
        s *= 2654435769u;
        s ^= s >> 16;
        s *= 2654435769u;
        return s;
    }

    static float Randomf(uint seed)
    {
        return (float)(Hash(seed)) / 4294967295.0f; // 2^32-1
    }
    
    static void RandomUnitVector(uint seed, out Vector3 result)
    {
        float PI2 = 6.28318530718f;
        float z = 1 - 2 * Randomf(seed);
        float xy = (float)Math.Sqrt(1.0 - z * z);
        float sn, cs;
        var value = PI2 * Randomf(seed + 1);
        sn = (float)Math.Sin(value);
        cs = (float)Math.Cos(value);
        result.x = sn * xy;
        result.y = cs * xy;
            result.z = z;
    }
    
    static void RandomVector(uint seed, out Vector3 result)
    {
        RandomUnitVector(seed, out result);
        var sqrt = (float)Math.Sqrt(Randomf(seed + 2));
        result.x = result.x * sqrt;
        result.y = result.z * sqrt;
        result.y = result.z * sqrt;
    }
    
    static float quat_from_axis_angle(ref Vector3 axis, float angle, out Vector3 result)
    {
        float half_angle = (angle * 0.5f) * 3.14159f / 180.0f;
        var sin = (float)Math.Sin(half_angle);
        result.x = axis.x * sin;
        result.y = axis.y * sin;
        result.z = axis.z * sin;
        return (float)Math.Cos(half_angle);
    }
    
    public static void Cross(ref Vector3 lhs, ref Vector3 rhs, out Vector3 result)
    {
        result.x = lhs.y * rhs.z - lhs.z * rhs.y; 
        result.y = lhs.z * rhs.x - lhs.x * rhs.z; 
        result.z = lhs.x * rhs.y - lhs.y * rhs.x;
    }

    static void rotate_position(ref Vector3 position, ref Vector3 axis, float angle, out Vector3 result)
    {
        Vector3 q;
        var w = quat_from_axis_angle(ref axis, angle, out q);
        Cross(ref q, ref position, out result);
        result.x = result.x + w * position.x;
        result.y = result.y + w * position.y;
        result.z = result.z + w * position.z;
        Cross(ref q, ref result, out result);
        result.x = position.x + 2.0f * result.x;
        result.y = position.y + 2.0f * result.y;
        result.z = position.z + 2.0f * result.z;
    }

    class Enumerator : IEnumerator
    {
        private int count;
        private int countn;
        private MillionPoints t;

        public Enumerator(int i, int countn, MillionPoints t)
        {
            count = 0;
            this.countn = countn;
            this.t = t;
        }

        public bool MoveNext()
        {
            var _particleDataArr = t._particleDataArr;
            for (int i = count; i < countn; i++)
            {
                Vector3 randomVector;
                RandomVector((uint) i + 1, out randomVector);
                Cross(ref randomVector, ref _particleDataArr[i].BasePosition, out randomVector);

                var magnitude = 1.0f / randomVector.magnitude;
                randomVector.x *= magnitude;
                randomVector.y *= magnitude;
                randomVector.z *= magnitude;
                
                rotate_position(ref _particleDataArr[i].BasePosition, ref randomVector, _particleDataArr[i].rotationSpeed * _time, out _particleDataArr[i].Position);
            }

            return false;
        }

        public void Reset()
        {
        }

        public object Current { get; private set; }
    }
    
    IEnumerator UpdateIt(int count, int countn)
    {
        
        
            
            // ComputeShader
            //  int kernelId = this._ComputeShader.FindKernel("MainCS");
            //    this._ComputeShader.SetFloat("_time", Time.time / 5.0f);
            //      this._ComputeShader.SetBuffer(kernelId, "_CubeDataBuffer", this._ParticleDataBuffer);
//        this._ComputeShader.Dispatch(kernelId, (Mathf.CeilToInt(this._particleCount / ThreadBlockSize) + 1), 1, 1);
        
        yield break;
    }

    private static float _time;

    IEnumerator Run()
    {
        while (true)
        {
            _time = Time.time;
            
            yield return _multiParallelTask.ThreadSafeRunOnSchedule(StandardSchedulers.syncScheduler);
            
            this._ParticleDataBuffer.SetData(_particleDataArr);
            Graphics.DrawMeshInstancedIndirect(this._pointMesh, 0, this._material,
                new Bounds(this._BoundCenter, this._BoundSize), this._GPUInstancingArgsBuffer);

            yield return null;
        }
    }

    void OnDestroy()
    {
        if (this._ParticleDataBuffer != null)
        {
            this._ParticleDataBuffer.Release();
            this._ParticleDataBuffer = null;
        }
        if (this._GPUInstancingArgsBuffer != null)
        {
            this._GPUInstancingArgsBuffer.Release();
            this._GPUInstancingArgsBuffer = null;
        }
        
        TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();
        _multiParallelTask.ClearAndKill();
    }
    
    #endregion // MonoBehaviour Method
}
