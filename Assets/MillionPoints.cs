using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Random = UnityEngine.Random;

public class MillionPoints : MonoBehaviour 
{
    // ==============================
    #region Computer_shader_I_still_have_to_understand_properly
        
    [SerializeField]
    ComputeShader _ComputeShader;
    
    ComputeBuffer _ParticleDataBuffer;
    
    /// GPU Instancingの為の引数
    readonly uint[] _GPUInstancingArgs = { 0, 0, 0, 0, 0 };
    
    /// GPU Instancingの為の引数バッファ
    ComputeBuffer _GPUInstancingArgsBuffer;

    #endregion // Defines

    [SerializeField]
    int _particleCount = 256000;
    
    [SerializeField]
    [Range(-Mathf.PI, Mathf.PI)]
    float _phi = Mathf.PI;
    
    [SerializeField]
    Material _material;
    
    [SerializeField]
    Vector3 _BoundCenter = Vector3.zero;
    
    [SerializeField]
    Vector3 _BoundSize = new Vector3(300f, 300f, 300f);

    Mesh _pointMesh;
    
#if !COMPUTE_SHADERS    
        public CPUParticleData[] _cpuParticleDataArr;
        public GPUParticleData[] _gpuparticleDataArr;
        MultiThreadedParallelTaskCollection _multiParallelTask;
    
        const uint NUM_OF_SVELTO_THREADS = 16;
#else    
        GPUParticleData[] _gpuparticleDataArr;
        const int ThreadBlockSize = 256;
#endif    
    
    void Awake()
    {
        Application.targetFrameRate = 90;
        QualitySettings.vSyncCount = 0;
    }
    
    void Start()
    {
        _ParticleDataBuffer = new ComputeBuffer(_particleCount, Marshal.SizeOf(typeof(GPUParticleData)));
        _GPUInstancingArgsBuffer = new ComputeBuffer(1, 
            _GPUInstancingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
#if !COMPUTE_SHADERS        
        _cpuParticleDataArr = new CPUParticleData[_particleCount];
#endif        
        _gpuparticleDataArr = new GPUParticleData[_particleCount];
        
#if !COMPUTE_SHADERS        
        // set default position
        for (int i = 0; i < _particleCount; i++)
        {
            _cpuParticleDataArr[i].BasePosition = new Vector3(Random.Range(-10.0f, 10.0f), 
                                                           Random.Range(-10.0f, 10.0f), Random.Range(-10.0f, 10.0f));
            _cpuParticleDataArr[i].rotationSpeed = Random.Range(1.0f, 100.0f);
        }
        
        for (int i = 0; i < _particleCount; i++)
        {
            _gpuparticleDataArr[i].Albedo = new Vector3(Random.Range(0.0f, 1.0f), 
                                                        Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
        }
#else
        for (int i = 0; i < _particleCount; i++)
        {
            _gpuparticleDataArr[i].BasePosition = new Vector3(Random.Range(-10.0f, 10.0f), 
                                                              Random.Range(-10.0f, 10.0f), Random.Range(-10.0f, 10.0f));
            _gpuparticleDataArr[i].rotationSpeed = Random.Range(1.0f, 100.0f);
            _gpuparticleDataArr[i].Albedo = new Vector3(Random.Range(0.0f, 1.0f), 
                                                        Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
        }
        
        _ParticleDataBuffer.SetData(_gpuparticleDataArr);
        _gpuparticleDataArr = null;
#endif        
        
        // creat point mesh
        _pointMesh = new Mesh();
        _pointMesh.vertices = new Vector3[] {
            new Vector3 (0, 0),
        };
        _pointMesh.normals = new Vector3[] {
            new Vector3 (0, 1, 0),
        };
        _pointMesh.SetIndices(new int[] { 0 }, MeshTopology.Points, 0);
        
        _GPUInstancingArgs[0] = (_pointMesh != null) ? _pointMesh.GetIndexCount(0) : 0;
        _GPUInstancingArgs[1] = (uint)_particleCount;
        _GPUInstancingArgsBuffer.SetData(_GPUInstancingArgs);
        
        _material.SetBuffer("_ParticleDataBuffer", _ParticleDataBuffer);
        
#if COMPUTE_SHADERS
        var materialShader = Shader.Find("Custom/MillionPoints");
        _material.shader = materialShader;
    
        StartComputerShaderWork();
#else
        _material.shader = Shader.Find("Custom/MillionPointsCPU");
        
        StartSveltoCPUWork();
#endif        
    }

#if !COMPUTE_SHADERS
    void StartSveltoCPUWork()
    {
        var countn = _particleCount / NUM_OF_SVELTO_THREADS;

        _multiParallelTask = new MultiThreadedParallelTaskCollection(NUM_OF_SVELTO_THREADS, false);

        for (int i = 0; i < NUM_OF_SVELTO_THREADS; i++)
            _multiParallelTask.Add(new ParticlesCPUKernel((int) (countn * i), (int) countn, this));

        WaitForSignalEnumerator _waitForSignal = new WaitForSignalEnumerator();
        WaitForSignalEnumerator _otherwaitForSignal = new WaitForSignalEnumerator();

        MainThreadStuff(_waitForSignal, _otherwaitForSignal).ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
        MultithreadedStuff(_waitForSignal, _otherwaitForSignal)
            .ThreadSafeRunOnSchedule(StandardSchedulers.multiThreadScheduler);
    }

    IEnumerator MultithreadedStuff(WaitForSignalEnumerator waitForSignalEnumerator, WaitForSignalEnumerator otherWaitForSignalEnumerator)
    {
        var syncRunner = new SyncRunner(false);
        
        while (_breakIt == false)
        {
            yield return _multiParallelTask.ThreadSafeRunOnSchedule(syncRunner);

            otherWaitForSignalEnumerator.Signal();

            while (_breakIt == false && waitForSignalEnumerator.MoveNext() == true) ; 
        }

        _multiParallelTask.ClearAndKill();
        
        TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();
    }

    IEnumerator MainThreadStuff(WaitForSignalEnumerator waitForSignalEnumerator, WaitForSignalEnumerator otherWaitForSignalEnumerator)
    {
        var bounds = new Bounds(_BoundCenter, _BoundSize);
        
        var syncRunner = new SyncRunner(false);
        
        while (true)
        {
            yield return otherWaitForSignalEnumerator.RunOnSchedule(syncRunner);
            
            _time = Time.time;
            
            _ParticleDataBuffer.SetData(_gpuparticleDataArr);
            
            Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                bounds, _GPUInstancingArgsBuffer);

            waitForSignalEnumerator.Signal();

            yield return null;
        }
    }
    
    internal static float _time;
    volatile bool _breakIt;
    
#else
    void StartComputerShaderWork()
    {
        ComputerShaderRun().ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
    }

    IEnumerator ComputerShaderRun()
    {
        while (true)
        {
            // ComputeShader
            int kernelId = this._ComputeShader.FindKernel("MainCS");
            this._ComputeShader.SetFloat("_Time", Time.time / 5.0f);
            this._ComputeShader.SetBuffer(kernelId, "_CubeDataBuffer", this._ParticleDataBuffer);
            this._ComputeShader.Dispatch(kernelId, (Mathf.CeilToInt(this._particleCount / ThreadBlockSize) + 1), 1, 1);
        
            // GPU Instaicing
            this._GPUInstancingArgs[0] = (this._pointMesh != null) ? this._pointMesh.GetIndexCount(0) : 0;
            this._GPUInstancingArgs[1] = (uint)this._particleCount;
            this._GPUInstancingArgsBuffer.SetData(this._GPUInstancingArgs);
            this._material.SetBuffer("_ParticleDataBuffer", this._ParticleDataBuffer);
            Graphics.DrawMeshInstancedIndirect(this._pointMesh, 0, this._material,
                                               new Bounds(this._BoundCenter, this._BoundSize), this._GPUInstancingArgsBuffer);

            yield return null;
        }
    }
#endif
    void OnDisable()
    {
        if (_ParticleDataBuffer != null)
        {
            _ParticleDataBuffer.Release();
            _ParticleDataBuffer = null;
        }
        if (_GPUInstancingArgsBuffer != null)
        {
            _GPUInstancingArgsBuffer.Release();
            _GPUInstancingArgsBuffer = null;
        }
        
#if !COMPUTE_SHADERS        
        _breakIt = true;
#endif    
    }
}

#if !COMPUTE_SHADERS
public struct CPUParticleData
{
    public Vector3 BasePosition;
    public float rotationSpeed;
}
#endif

//[StructLayout(LayoutKind.Sequential, Pack=1)]
public struct GPUParticleData
{
#if COMPUTE_SHADERS    
    public Vector3 BasePosition;
#endif        
    public Vector3 Position;
    public Vector3 Albedo;
#if COMPUTE_SHADERS    
    public float rotationSpeed;
#endif    
}