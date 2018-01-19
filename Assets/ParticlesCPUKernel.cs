#if !COMPUTE_SHADERS
using System;
using System.Collections;
using UnityEngine;

class ParticlesCPUKernel : IEnumerator
{
    int count;
    int countn;
        
    CPUParticleData[] _particleDataArr;
    GPUParticleData[] _gpuparticleDataArr;
        
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
    
    static void Cross(ref Vector3 lhs, ref Vector3 rhs, out Vector3 result)
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
        
    public ParticlesCPUKernel(int i, int ncountn, MillionPoints t)
    {
        count = 0;
        countn = ncountn;
        _particleDataArr = t._cpuParticleDataArr;
        _gpuparticleDataArr = t._gpuparticleDataArr;
    }

    public bool MoveNext()
    {
        for (int i = count; i < countn; i++)
        {
            Vector3 randomVector;
            RandomVector((uint) i + 1, out randomVector);
            Cross(ref randomVector, ref _particleDataArr[i].BasePosition, out randomVector);

            var magnitude = 1.0f / randomVector.magnitude;
            randomVector.x *= magnitude;
            randomVector.y *= magnitude;
            randomVector.z *= magnitude;
                
            rotate_position(ref _particleDataArr[i].BasePosition, 
                            ref randomVector, _particleDataArr[i].rotationSpeed * MillionPoints._time, 
                            out _gpuparticleDataArr[i].Position);
        }

        return false;
    }

    public void Reset()
    {}

    public object Current { get; private set; }
}
#endif