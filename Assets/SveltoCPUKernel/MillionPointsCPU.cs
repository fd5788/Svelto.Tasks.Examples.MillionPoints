﻿//don't use the BENCHMARK define, it's not supported

#if !TEST2 && !TEST3
#define TEST1
#endif

#if UNITY_2018_1_OR_NEWER
using Unity.Collections;
#endif
using System.Runtime.InteropServices;
using UnityEngine;
using Random = UnityEngine.Random;

#if !TEST1 && !TEST2 && !TEST3
#warning either TEST1 OR TEST2 OR TEST3 preprocessor must be active to execute the test
#endif

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    [HelpURL("http://www.sebaslab.com/svelto-tasks-million-points-multiple-cpu-cores-threads-unity-jobs-system/")]
    public partial class MillionPointsCPU : MonoBehaviour
    {
        // ==============================
        
        [TextArea] public string Notes =
            "This is the Svelto.Tasks version of massive multithreaded transformation of particles on the CPU. I" +
            "try to find the best strategies to push the core utilization to 100%. Use Defines: Test1 for advanced" +
            "synchronization strategy. Test2 for naive synchronization strategy. Test3 to show how the main thread" +
            "and the other thread can run independently.";

#region Computer_shader_stuff_I_still_have_to_understand_properly

        ComputeBuffer _particleDataBuffer;
        ComputeBuffer _GPUInstancingArgsBuffer;

        readonly uint[] _GPUInstancingArgs = {0, 0, 0, 0, 0};

#endregion 

        [SerializeField] uint _particleCount;
        [SerializeField] Material _material;
        [SerializeField] Vector3 _BoundCenter = Vector3.zero;
        [SerializeField] Vector3 _BoundSize = new Vector3(300f, 300f, 300f);

        Mesh _pointMesh;

        public CPUParticleData[] _cpuParticleDataArr;
#if UNITY_2018_1_OR_NEWER && USE_NATIVE_ARRAYS
        public NativeArray<GPUParticleData> _gpuparticleDataArr;
#else        
        public GPUParticleData[] _gpuparticleDataArr;
#endif    
        MultiThreadedParallelTaskCollection _multiParallelTasks;

        const uint NUM_OF_SVELTO_THREADS = 28;

        void Awake()
        {
            if (this.enabled)
                GetComponent<ComputeShaders.MillionPointsGPU>().enabled = false;
            
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }

        void Start()
        {
            InitializeDataForDrawMeshInstancedIndirect();

            StartSveltoCPUWork();
        }

        void StartSveltoCPUWork()
        {
            //create all the parallel tasks and fill _multiParallelTask collection
            PrepareParallelTasks();

            //the task that runs on the mainthread. You may wonder why I used
            //ThreadSafeRun instead of Run. This is due to the code being not perfect
            //Run will execute the code until the first yield immediatly, which
            //can cause a lock in this case. ThreadSafeRun always run 
            //the whole code on the selected runner.
#if TEST1            
            SignalBasedAdvancedMultithreadYielding()
                .RunOnScheduler(StandardSchedulers.updateScheduler);
#elif TEST2            
            MainThreadLoopWithNaiveSynchronization()
                .RunOnScheduler(StandardSchedulers.updateScheduler);
#elif TEST3    
            MultiThreadsRunningIndipendently()
                .RunOnScheduler(StandardSchedulers.multiThreadScheduler);
#endif            
            //the task that will execute the _multiParallelTask collection also
            //run on another thread.
        }

        internal class ParticleCounter
        {
            public int particlesTransformed;
            public readonly uint particlesLimit;

            public ParticleCounter(uint limit)
            {
                particlesLimit = limit;
            }
        }

        void PrepareParallelTasks()
        {
            //create a collection of task that will run in parallel on several threads.
            //the number of threads and tasks to perform are not dependent.
            _multiParallelTasks = new MultiThreadedParallelTaskCollection(NUM_OF_SVELTO_THREADS, true);
            //in this case though we just want to perform a task for each thread
            //ParticlesCPUKernel is a task (IEnumerator) that executes the 
            //algebra operation on the particles. Each task perform the operation
            //on particlesPerThread particles
#if BENCHMARK            
            pc = new ParticleCounter(particlesPerThread - 16);
#else
            _pc = new ParticleCounter(0);
#endif
#if OLD_STYLE
            //calculate the number of particles per thread
            uint particlesPerThread = _particleCount / NUM_OF_SVELTO_THREADS;
            for (int i = 0; i < NUM_OF_SVELTO_THREADS; i++)
                _multiParallelTasks.Add(new ParticlesCPUKernel((int) (particlesPerThread * i), (int) particlesPerThread, this, _pc));
#else
            var particlesCpuKernel = new ParticlesCPUKernel(this);
            _multiParallelTasks.Add(ref particlesCpuKernel, (int)_particleCount);
#endif
        }
        
        void OnDisable()
        {
            //the application is shutting down. This is not that necessary in a 
            //standalone client, but necessary to stop the thread when the 
            //application is stopped in the Editor to stop all the threads.
            //tbh Unity should implement something to shut down the 
            //threads started by the running application
            _multiParallelTasks.Dispose();
            
            Debug.Log("clean up");
            
            TaskRunner.StopAndCleanupAllDefaultSchedulers();
            
            Cleanup();
        }

        void Cleanup()
        {
            if (_particleDataBuffer != null)
            {
                _particleDataBuffer.Release();
                _particleDataBuffer = null;
            }

            if (_GPUInstancingArgsBuffer != null)
            {
                _GPUInstancingArgsBuffer.Release();
                _GPUInstancingArgsBuffer = null;
            }
            
#if UNITY_2018_1_OR_NEWER && USE_NATIVE_ARRAYS
            _gpuparticleDataArr.Dispose();
#endif
        }

        void InitializeDataForDrawMeshInstancedIndirect()
        {
            _cpuParticleDataArr = new CPUParticleData[_particleCount];
#if UNITY_2018_1_OR_NEWER && USE_NATIVE_ARRAYS
            _gpuparticleDataArr = new NativeArray<GPUParticleData>((int) _particleCount, Allocator.Persistent);
#else
            _gpuparticleDataArr = new GPUParticleData[_particleCount];
#endif
            _particleDataBuffer = new ComputeBuffer((int) _particleCount, Marshal.SizeOf(typeof(GPUParticleData)));
            // set default position
            for (int i = 0; i < _particleCount; i++)
            {
                _cpuParticleDataArr[i].basePosition = new Vector3(Random.Range(-10.0f, 10.0f),
                                                                  Random.Range(-10.0f, 10.0f), Random.Range(-10.0f, 10.0f));
                _cpuParticleDataArr[i].rotationSpeed = Random.Range(1.0f, 100.0f);
            }

            for (int i = 0; i < _particleCount; i++)
            {
                _gpuparticleDataArr[i] =
                    new GPUParticleData(new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f),
                                                    Random.Range(0.0f, 1.0f)));
            }

            // creat point mesh
            _pointMesh          = new Mesh();
            _pointMesh.vertices = new[] {new Vector3(0, 0)};
            _pointMesh.normals  = new[] {new Vector3(0, 1, 0)};
            _pointMesh.SetIndices(new[] {0}, MeshTopology.Points, 0);

            _GPUInstancingArgsBuffer = new ComputeBuffer(1
                                                       , _GPUInstancingArgs.Length * sizeof(uint)
                                                       , ComputeBufferType.IndirectArguments);
            _GPUInstancingArgs[0] = (_pointMesh != null) ? _pointMesh.GetIndexCount(0) : 0;
            _GPUInstancingArgs[1] = _particleCount;
            _GPUInstancingArgsBuffer.SetData(_GPUInstancingArgs);

            var materialShader = Shader.Find("Custom/MillionPointsCPU");
            _material.shader = materialShader;
            _material.SetBuffer("_ParticleDataBuffer", _particleDataBuffer);
        }
        
        internal static float _time;
        ParticleCounter       _pc;
    }

    public struct CPUParticleData
    {
        public Vector3 basePosition;
        public float rotationSpeed;
    }

    public struct GPUParticleData
    {
        public Vector3 position;
        public Vector3 albedo;

        public GPUParticleData(Vector3 albedo):this()
        {
            this.albedo = albedo;
        }

        public GPUParticleData(Vector3 position, Vector3 albedo)
        {
            this.position = position;
            this.albedo = albedo;
        }
    }
}