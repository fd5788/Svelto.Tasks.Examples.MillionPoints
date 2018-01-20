using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GC回数
/// MonoHeapのサイズ
/// Monoの使用メモリサイズ
/// FPSなどを表示する
/// </summary>
namespace PerformanceCheker
{

    public class PerformanceProfiler : MonoBehaviour
    {
        public int GCcount
        {
            get
            {
                return gc_count;
            }
        }

        public long UsedHeapSize
        {
            get
            {
                return used_heap_size_;
            }
        }

        public long MonoHeapSize
        {
            get
            {
                return mono_heap_size_;
            }
        }

        public long MonoUsedSize
        {
            get
            {
                return mono_used_size_;
            }
        }

        public int CurrentFPS
        {
            get
            {
                return (int)showingFPSValue;
            }
        }

        private int gc_start_count_=0;
        private long used_heap_size_=0;
        private long mono_heap_size_=0;
        private long mono_used_size_=0;
        private int gc_count=0;


        //FPS check
        readonly float FPSCheckIntervalSecond = 0.2f;
        private int frameCount = 0;
        private float prevTime=0f;
        private float deltaTime=0f;
        private float showingFPSValue=0f;
       
        void Awake()
        {
            gc_start_count_ = System.GC.CollectionCount(0 /* generation */);
        }

        // Update is called once per frame
        void Update()
        {
            ++frameCount;
            deltaTime = Time.realtimeSinceStartup - prevTime;
            if (deltaTime >= FPSCheckIntervalSecond)
            {
                showingFPSValue = frameCount / deltaTime;

                frameCount = 0;
                prevTime = Time.realtimeSinceStartup;
            }


            gc_count = System.GC.CollectionCount(0 /* generation */) - gc_start_count_;
            used_heap_size_ = UnityEngine.Profiling.Profiler.usedHeapSizeLong;
            mono_heap_size_ = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong();
            mono_used_size_ = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        }
    }
}