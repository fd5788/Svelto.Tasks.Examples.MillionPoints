using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Svelto.Tasks.Example.MillionPoints.UnityJobs
{
    public class MillionPointsCPUUnityJobs : MonoBehaviour
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

        public NativeArray<CPUParticleData> _cpuParticleDataArr;
        public NativeArray<GPUParticleData> _gpuparticleDataArr;
        MultiThreadedParallelTaskCollection _multiParallelTasks;

        void Awake()
        {
            if (this.enabled)
                GetComponent<ComputeShaders.MillionPointsGPU>().enabled = false;
            
            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 0;
        }

        void Start()
        {
            _cpuParticleDataArr = new NativeArray<CPUParticleData>((int) _particleCount, Allocator.Persistent);
            _gpuparticleDataArr = new NativeArray<GPUParticleData>((int) _particleCount, Allocator.Persistent);

            _particleDataBuffer = new ComputeBuffer((int) _particleCount, Marshal.SizeOf(typeof(GPUParticleData)));

            // set default position
            for (int i = 0; i < _particleCount; i++)
            {
                _cpuParticleDataArr[i] = new CPUParticleData(new Vector3(Random.Range(-10.0f, 10.0f),
                    Random.Range(-10.0f, 10.0f), Random.Range(-10.0f, 10.0f)), Random.Range(1.0f, 100.0f));
            }

            for (int i = 0; i < _particleCount; i++)
            {
                _gpuparticleDataArr[i] = new GPUParticleData(new Vector3(Random.Range(0.0f, 1.0f),
                    Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f)));
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
            
            _bounds = new Bounds(_BoundCenter, _BoundSize);
        }

        void Update()
        {
            ParticlesCPUKernel job = new ParticlesCPUKernel(this);

            _time = Time.time / 10;

            var jobSchedule = job.Schedule(_particleCount, 1);
            
            jobSchedule.Complete();
            
            _particleDataBuffer.SetData(_gpuparticleDataArr);
            
            Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                                               _bounds, _GPUInstancingArgsBuffer);
        }

        internal static float _time;
        Bounds _bounds;

        void OnDisable()
        {
            _cpuParticleDataArr.Dispose();
            _gpuparticleDataArr.Dispose();
            
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
        }
    }

    public struct CPUParticleData
    {
        public Vector3 BasePosition;
        public float rotationSpeed;

        public CPUParticleData(Vector3 vector3, float range)
        {
            BasePosition = vector3;
            rotationSpeed = range;
        }
    }

    public struct GPUParticleData
    {
        public Vector3 Position;
        public Vector3 Albedo;

        public GPUParticleData(Vector3 vector3): this()
        {
            Albedo = vector3;
        }

        public GPUParticleData(Vector3 position, Vector3 albedo)
        {
            Position = position;
            Albedo = albedo;
        }
    }
}