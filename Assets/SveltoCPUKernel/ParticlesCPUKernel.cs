using System;
using System.Collections;
using System.Threading;
#if UNITY_2018_1_OR_NEWER && USE_NATIVE_ARRAYS
using Unity.Collections;
#endif    
using UnityEngine;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    struct ParticlesCPUKernel : 
#if OLD_STYLE
IEnumerator
#else
    IMultiThreadParallelizable
#endif
    {
#if OLD_STYLE
        int startIndex;
        int endIndex;
#endif

        CPUParticleData[] _particleDataArr;
#if UNITY_2018_1_OR_NEWER && USE_NATIVE_ARRAYS      
        NativeArray<GPUParticleData> _gpuparticleDataArr;
#else
        GPUParticleData[] _gpuparticleDataArr;
#endif        
        MillionPointsCPU.ParticleCounter _pc;

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
            return Hash(seed) / 4294967295.0f; // 2^32-1
        }

        static void RandomUnitVector(uint seed, out Vector3 result)
        {
            /*  float PI2 = 6.28318530718;
              float z = 1 - 2 * Random(seed);
              float xy = sqrt(1.0 - z * z);
              float sn, cs;
              sincos(PI2 * Random(seed + 1), sn, cs);
              return float3(sn * xy, cs * xy, z);*/
            float PI2 = 6.28318530718f;
            float z = 1.0f - 2.0f * Randomf(seed);
            float xy = (float) Math.Sqrt(1.0 - z * z);
            float sn, cs;
            var value = PI2 * Randomf(seed + 1);
            sn = (float) Math.Sin(value);
            cs = (float) Math.Cos(value);
            result.x = sn * xy;
            result.y = cs * xy;
            result.z = z;
        }

        static void RandomVector(uint seed, out Vector3 result)
        {
            //return RandomUnitVector(seed) * sqrt(Random(seed + 2));
            RandomUnitVector(seed, out result);
            var sqrt = (float) Math.Sqrt(Randomf(seed + 2));
            result.x = result.x * sqrt;
            result.y = result.y * sqrt;
            result.z = result.z * sqrt;
        }

        static float quat_from_axis_angle(ref Vector3 axis, float angle, out Vector3 result)
        {
            /*
             float4 qr;
        float half_angle = (angle * 0.5) * 3.14159 / 180.0;
        qr.x = axis.x * sin(half_angle);
        qr.y = axis.y * sin(half_angle);
        qr.z = axis.z * sin(half_angle);
        qr.w = cos(half_angle);
        return qr;
             */
            float half_angle = (angle * 0.5f) * 3.14159f / 180.0f;
            var sin = (float) Math.Sin(half_angle);
            result.x = axis.x * sin;
            result.y = axis.y * sin;
            result.z = axis.z * sin;
            return (float) Math.Cos(half_angle);
        }

        static void Cross(ref Vector3 lhs, ref Vector3 rhs, out Vector3 result)
        {
            result.x = lhs.y * rhs.z - lhs.z * rhs.y;
            result.y = lhs.z * rhs.x - lhs.x * rhs.z;
            result.z = lhs.x * rhs.y - lhs.y * rhs.x;
        }

        static void rotate_position(ref Vector3 position, ref Vector3 axis, float angle, out Vector3 result)
        {
            /*
                    float4 q = quat_from_axis_angle(axis, angle);
                    float3 v = position.xyz;
                    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
                    */
            Vector3 q;
            var w = quat_from_axis_angle(ref axis, angle, out q);
            Cross(ref q, ref position, out result);
            result.x = result.x + w * position.x;
            result.y = result.y + w * position.y;
            result.z = result.z + w * position.z;
            Vector3 otherResult;
            Cross(ref q, ref result, out otherResult);
            result.x = position.x + 2.0f * otherResult.x;
            result.y = position.y + 2.0f * otherResult.y;
            result.z = position.z + 2.0f * otherResult.z;
        }
#if OLD_STYLE
        public ParticlesCPUKernel(int startIndex, int numberOfParticles, MillionPointsCPU t,
                                  MillionPointsCPU.ParticleCounter pc):this()
        {
            this.startIndex = startIndex;
            endIndex = startIndex + numberOfParticles;
            _particleDataArr = t._cpuParticleDataArr;
            _gpuparticleDataArr = t._gpuparticleDataArr;
            _pc = pc;
        }
#else
        public ParticlesCPUKernel(MillionPointsCPU t):this()
        {
            _particleDataArr    = t._cpuParticleDataArr;
            _gpuparticleDataArr = t._gpuparticleDataArr;
        }
#endif

#if OLD_STYLE
public bool MoveNext()
        {
            int i;
            for (i = startIndex; i < endIndex - _pc.particlesLimit; i++)
            {
                Vector3 randomVector;
                RandomVector((uint) i + 1, out randomVector);
                Cross(ref randomVector, ref _particleDataArr[i].basePosition, out randomVector);

                randomVector.Normalize();
                
#if UNITY_2018_1_OR_NEWER && USE_NATIVE_ARRAYS
                Vector3 position;
                rotate_position(ref _particleDataArr[i].basePosition,
                    ref randomVector, _particleDataArr[i].rotationSpeed * MillionPointsCPU._time,
                    out position);
                
                _gpuparticleDataArr[i] = new GPUParticleData(position, _gpuparticleDataArr[i].albedo);
#else
                rotate_position(ref _particleDataArr[i].basePosition,
                                ref randomVector, _particleDataArr[i].rotationSpeed * MillionPointsCPU._time,
                                out _gpuparticleDataArr[i].position);
#endif                
            }

            var value = i - startIndex;
            Interlocked.Add(ref _pc.particlesTransformed, value);

            return false;
        }

        public void Reset()
        {}

        public object Current { get; private set; }
#else
        public void Update(int i)
        {
                Vector3 randomVector;
                RandomVector((uint) i + 1, out randomVector);
                Cross(ref randomVector, ref _particleDataArr[i].basePosition, out randomVector);

                randomVector.Normalize();
#if UNITY_2018_1_OR_NEWER && USE_NATIVE_ARRAYS
                Vector3 position;
                rotate_position(ref _particleDataArr[i].basePosition,
                    ref randomVector, _particleDataArr[i].rotationSpeed * MillionPointsCPU._time,
                    out position);
                
                _gpuparticleDataArr[i] = new GPUParticleData(position, _gpuparticleDataArr[i].albedo);
#else
                rotate_position(ref _particleDataArr[i].basePosition,
                                ref randomVector, _particleDataArr[i].rotationSpeed * MillionPointsCPU._time,
                                out _gpuparticleDataArr[i].position);
#endif                
        }
#endif                
    }
}