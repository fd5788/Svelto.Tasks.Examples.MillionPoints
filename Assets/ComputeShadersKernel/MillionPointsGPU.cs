using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;
using Random = UnityEngine.Random;

namespace Svelto.Tasks.Example.MillionPoints.ComputeShaders
{
    public class MillionPointsGPU : MonoBehaviour
    {
        // ==============================

        [TextArea] public string Notes =
            "This is the original GPU code downloaded from github. It uses pure compute shaders to " +
            "transform the particles. Obviously the GPU is the best tool for this job, so the other" +
            "cases are only for demonstration purposes";
        [SerializeField] ComputeShader _ComputeShader;
        [SerializeField] int _particleCount = 1000000;
        [SerializeField] Material _material;
        [SerializeField] Vector3  _BoundCenter = Vector3.zero;
        [SerializeField] Vector3  _BoundSize   = new Vector3(300f, 300f, 300f);

        ComputeBuffer _particleDataBuffer;

        readonly uint[] _GPUInstancingArgs = {0, 0, 0, 0, 0};

        ComputeBuffer _GPUInstancingArgsBuffer;

        

        Mesh _pointMesh;
        GPUParticleData[] _gpuparticleDataArr;
        
        const int ThreadBlockSize = 256;

        void Awake()
        {
            if (this.enabled)
                GetComponent<Multithreading.MillionPointsCPU>().enabled = false;
            
            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 0;
        }

        void Start()
        {
            _gpuparticleDataArr = new GPUParticleData[_particleCount];

            _particleDataBuffer = new ComputeBuffer(_particleCount, Marshal.SizeOf(typeof(GPUParticleData)));

            for (int i = 0; i < _particleCount; i++)
            {
                _gpuparticleDataArr[i].BasePosition = new Vector3(Random.Range(-10.0f, 10.0f),
                    Random.Range(-10.0f, 10.0f), Random.Range(-10.0f, 10.0f));
                _gpuparticleDataArr[i].rotationSpeed = Random.Range(1.0f, 100.0f);
                _gpuparticleDataArr[i].Albedo = new Vector3(Random.Range(0.0f, 1.0f),
                    Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
            }

            _particleDataBuffer.SetData(_gpuparticleDataArr);
            _gpuparticleDataArr = null;

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

            var materialShader = Shader.Find("Custom/MillionPoints");
            _material.shader = materialShader;
            _material.SetBuffer("_ParticleDataBuffer", _particleDataBuffer);

            StartComputerShaderWork();
        }

        void StartComputerShaderWork()
        {
            ComputerShaderRun().RunOnScheduler(StandardSchedulers.updateScheduler);
        }

        IEnumerator ComputerShaderRun()
        {
            int kernelId = _ComputeShader.FindKernel("MainCS");
            _ComputeShader.SetBuffer(kernelId, "_CubeDataBuffer", _particleDataBuffer);

            while (true)
            {
                // ComputeShader
                _ComputeShader.SetFloat("_time", Time.time);
                _ComputeShader.Dispatch(kernelId, (Mathf.CeilToInt(_particleCount / (float) ThreadBlockSize) + 1), 1,
                    1);

                // GPU Instaicing
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    new Bounds(_BoundCenter, _BoundSize), _GPUInstancingArgsBuffer);

                yield return null;
            }
        }

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
        }
    }

    public struct GPUParticleData
    {
        public Vector3 BasePosition;
        public Vector3 Position;
        public Vector3 Albedo;

        public float rotationSpeed;
    }
}