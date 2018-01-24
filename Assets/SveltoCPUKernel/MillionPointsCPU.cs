using System.Runtime.InteropServices;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    public partial class MillionPointsCPU : MonoBehaviour
    {
        // ==============================

        #region Computer_shader_stuff_I_still_have_to_understand_properly

        ComputeBuffer _particleDataBuffer;

        /// GPU Instancingの為の引数
        readonly uint[] _GPUInstancingArgs = {0, 0, 0, 0, 0};

        /// GPU Instancingの為の引数バッファ
        ComputeBuffer _GPUInstancingArgsBuffer;

        #endregion // Defines

        [SerializeField] uint _particleCount = 256000;

        [SerializeField] Material _material;

        [SerializeField] Vector3 _BoundCenter = Vector3.zero;

        [SerializeField] Vector3 _BoundSize = new Vector3(300f, 300f, 300f);

        Mesh _pointMesh;

        public CPUParticleData[] _cpuParticleDataArr;
        public GPUParticleData[] _gpuparticleDataArr;
        MultiThreadedParallelTaskCollection _multiParallelTasks;

        const uint NUM_OF_SVELTO_THREADS = 16;

        void Awake()
        {
            if (this.enabled)
                GetComponent<ComputeShaders.MillionPointsGPU>().enabled = false;
            
            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 0;
        }

        void Start()
        {
            _cpuParticleDataArr = new CPUParticleData[_particleCount];
            _gpuparticleDataArr = new GPUParticleData[_particleCount];

            _particleDataBuffer = new ComputeBuffer((int) _particleCount, Marshal.SizeOf(typeof(GPUParticleData)));

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

            // creat point mesh
            _pointMesh = new Mesh();
            _pointMesh.vertices = new Vector3[]
            {
                new Vector3(0, 0),
            };
            _pointMesh.normals = new Vector3[]
            {
                new Vector3(0, 1, 0),
            };
            _pointMesh.SetIndices(new int[] {0}, MeshTopology.Points, 0);

            _GPUInstancingArgsBuffer = new ComputeBuffer(1,
                _GPUInstancingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            _GPUInstancingArgs[0] = (_pointMesh != null) ? _pointMesh.GetIndexCount(0) : 0;
            _GPUInstancingArgs[1] = (uint) _particleCount;
            _GPUInstancingArgsBuffer.SetData(_GPUInstancingArgs);

            var materialShader = Shader.Find("Custom/MillionPointsCPU");
            _material.shader = materialShader;
            _material.SetBuffer("_ParticleDataBuffer", _particleDataBuffer);

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
            MainThreadOperations(pc)
                .ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
#elif TEST2            
            MainThreadLoopWithNaiveSynchronization()
                .ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
#elif TEST3    
            MainLoopOnOtherThread()
                .ThreadSafeRunOnSchedule(StandardSchedulers.multiThreadScheduler);
#endif            
            //the task that will execute the _multiParallelTask collection also
            //run on another thread.
        }

        internal class ParticleCounter
        {
            public int particlesTransformed;
            public uint particlesLimit;

            public ParticleCounter(uint limit)
            {
                particlesLimit = limit;
            }
        }

        void PrepareParallelTasks()
        {
            //calculate the number of particles per thread
            uint particlesPerThread = _particleCount / NUM_OF_SVELTO_THREADS;
            //create a collection of task that will run in parallel on several threads.
            //the number of threads and tasks to perform are not dipendennt.
            _multiParallelTasks = new MultiThreadedParallelTaskCollection(NUM_OF_SVELTO_THREADS, false);
            //in this case though we just want to perform a task for each thread
            //ParticlesCPUKernel is a task (IEnumerator) that executes the 
            //algebra operation on the particles. Each task perform the operation
            //on particlesPerThread particles
#if BENCHMARK            
            pc = new ParticleCounter(particlesPerThread - 16);
#else
            pc = new ParticleCounter(0);
#endif
            
            for (int i = 0; i < NUM_OF_SVELTO_THREADS; i++)
                _multiParallelTasks.Add(new ParticlesCPUKernel((int) (particlesPerThread * i), (int) particlesPerThread, this, pc));
        }

        internal static float _time;
        volatile bool _breakIt;
        ParticleCounter pc;

        void OnDisable()
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

            _breakIt = true;
        }
    }

    public struct CPUParticleData
    {
        public Vector3 BasePosition;
        public float rotationSpeed;
    }

    public struct GPUParticleData
    {
        public Vector3 Position;
        public Vector3 Albedo;
    }
}