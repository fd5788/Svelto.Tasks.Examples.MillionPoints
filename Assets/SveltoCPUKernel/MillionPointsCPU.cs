#define TEST1

using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using Svelto.Tasks.Enumerators;
using Random = UnityEngine.Random;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    public class MillionPointsCPU : MonoBehaviour
    {
        // ==============================

        #region Computer_shader_stuff_I_still_have_to_understand_properly

        ComputeBuffer _particleDataBuffer;

        /// GPU Instancingの為の引数
        readonly uint[] _GPUInstancingArgs = {0, 0, 0, 0, 0};

        /// GPU Instancingの為の引数バッファ
        ComputeBuffer _GPUInstancingArgsBuffer;

        #endregion // Defines

        [SerializeField] int _particleCount = 256000;

        [SerializeField] Material _material;

        [SerializeField] Vector3 _BoundCenter = Vector3.zero;

        [SerializeField] Vector3 _BoundSize = new Vector3(300f, 300f, 300f);

        Mesh _pointMesh;

        public CPUParticleData[] _cpuParticleDataArr;
        public GPUParticleData[] _gpuparticleDataArr;
        MultiThreadedParallelTaskCollection _multiParallelTask;

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

            _particleDataBuffer = new ComputeBuffer(_particleCount, Marshal.SizeOf(typeof(GPUParticleData)));

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
            //calculate the number of particles per thread
            var particlesPerThread = _particleCount / NUM_OF_SVELTO_THREADS;
            //create a collection of task that will run in parallel on several threads.
            //the number of threads and tasks to perform are not dipendennt.
            _multiParallelTask = new MultiThreadedParallelTaskCollection(NUM_OF_SVELTO_THREADS, false);
            //in this case though we just want to perform a task for each thread
            //ParticlesCPUKernel is a task (IEnumerator) that executes the 
            //algebra operation on the particles. Each task perform the operation
            //on particlesPerThread particles
            for (int i = 0; i < NUM_OF_SVELTO_THREADS; i++)
                _multiParallelTask.Add(new ParticlesCPUKernel((int) (particlesPerThread * i), (int) particlesPerThread, this));

            //these will help with synchronization between threads
            WaitForSignalEnumerator _waitForSignal = new WaitForSignalEnumerator();
            WaitForSignalEnumerator _otherwaitForSignal = new WaitForSignalEnumerator();

            //the task that runs on the mainthread. You may wonder why I used
            //ThreadSafeRun instead of Run. This is due to the code being not perfect
            //Run will execute the code until the first yield immediatly, which
            //can cause a lock in this case. ThreadSafeRun always run 
            //the whole code on the selected runner.
#if TEST1            
            MainThreadStuffOption1(_waitForSignal, _otherwaitForSignal)
                .ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
#elif TEST2            
            MainThreadStuffOption2()
                .ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
#elif TEST3            
            MainThreadStuffOption3(_waitForSignal, _otherwaitForSignal)
                .ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
#endif            
            //the task that will execute the _multiParallelTask collection also
            //run on another thread.
#if !TEST2            
            MultithreadedStuff(_waitForSignal, _otherwaitForSignal)
                .ThreadSafeRunOnSchedule(StandardSchedulers.multiThreadScheduler);
#endif    
        }

        IEnumerator MultithreadedStuff(WaitForSignalEnumerator waitForSignalEnumerator,
            WaitForSignalEnumerator otherWaitForSignalEnumerator)
        {
            //a SyncRunner stop the execution of the thread until the task is not completed
            //the parameter true means that the runner will sleep in between yields
            var syncRunner = new SyncRunner(true);

            while (_breakIt == false)
            {
                //execute the tasks. The MultiParallelTask is a special collection
                //that uses N threads on its own to execute the tasks. This thread
                //doesn't need to do anything else meanwhile and will yield until
                //is done. That's why the syncrunner can sleep between yields, so 
                //that this thread won't take much CPU just to wait the parallel 
                //tasks to finish
                yield return _multiParallelTask.ThreadSafeRunOnSchedule(syncRunner);
                //the 1 Million particles operation are done, let's signal that the
                //result can now be used
                otherWaitForSignalEnumerator.Signal();
                //wait until the application is over or the main thread will tell
                //us that now we can perform again the particles operation. This 
                //is an explicit while instead of a yield, just because if the _breakIt
                //condition, which is needed only because if this application runs
                //in the editor, the threads spawned will not stop until the Editor is 
                //shut down.
                while (_breakIt == false && waitForSignalEnumerator.MoveNext() == true) ;
            }

            //the application is shutting down. This is not that necessary in a 
            //standalone client, but necessary to stop the thread when the 
            //application is stopped in the Editor to stop all the threads.
            _multiParallelTask.ClearAndKill();

            TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();
        }

        //this is our friendly main thread!
        IEnumerator MainThreadStuffOption1(WaitForSignalEnumerator waitForSignalEnumerator,
            WaitForSignalEnumerator otherWaitForSignalEnumerator)
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            var syncRunner = new SyncRunner(true);

            while (true)
            {
                //wait until the other thread tell us that the data is read to be used
                //note that I am stalling the main thread here! This is entirely up to you
                //if you don't want to stall it, run the task on a normal scheduler.
                //you will se the frame rate going super fast, but the operations will
                //NOT be applied every frame, but only when the other thread says that
                //the operations are done.
                
                _time = Time.time;
                
                _particleDataBuffer.SetData(_gpuparticleDataArr);
                
                yield return otherWaitForSignalEnumerator.RunOnSchedule(syncRunner);

                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);
                
                //tell to the other thread that now it can perform the operations
                //for the next frame.
                waitForSignalEnumerator.Signal();

                //continue the cycle on the next frame
                yield return null;
            }
        }
        
        //this is our friendly main thread!
        IEnumerator MainThreadStuffOption2()
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            var syncRunner = new SyncRunner(true);

            while (true)
            {
                _time = Time.time;
                
                yield return _multiParallelTask.ThreadSafeRunOnSchedule(syncRunner);
                
                _particleDataBuffer.SetData(_gpuparticleDataArr);
                
                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);
                
                //continue the cycle on the next frame
                yield return null;
            }
        }
        
        //this is our friendly main thread!
        IEnumerator MainThreadStuffOption3(WaitForSignalEnumerator waitForSignalEnumerator,
            WaitForSignalEnumerator otherWaitForSignalEnumerator)
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            var syncRunner = new SyncRunner(true);

            while (true)
            {
                while (otherWaitForSignalEnumerator.RunOnSchedule(StandardSchedulers.updateScheduler).MoveNext() ==
                       false)
                {
                    //render the particles. I use DrawMeshInstancedIndirect but
                    //there aren't any compute shaders running. This is so cool!
                    Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                        bounds, _GPUInstancingArgsBuffer);

                    yield return null;
                }    
                
                _time = Time.time;
                        
                _particleDataBuffer.SetData(_gpuparticleDataArr);
                
                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);
                
                //tell to the other thread that now it can perform the operations
                //for the next frame.
                waitForSignalEnumerator.Signal();

                //continue the cycle on the next frame
                yield return null;
            }
        }
        
        internal static float _time;
        volatile bool _breakIt;

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