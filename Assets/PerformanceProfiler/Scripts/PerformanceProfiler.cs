using UnityEngine;

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

        public int CurrentFPS
        {
            get
            {
                return (int)showingFPSValue;
            }
        }
        
        public int MaxFPS
        {
            get
            {
                return (int)showingMaxFPSValue;
            }
        }
        
        public int MinFPS
        {
            get
            {
                return (int)showingMinFPSValue;
            }
        }

        int gc_start_count_=0;
        int gc_count=0;


        //FPS check
        readonly float FPSCheckIntervalSecond = 0.3f;
        int frameCount = 0;
        float prevTime=0f;
        float deltaTime=0f;
        public static float showingFPSValue=0f;
        float showingMaxFPSValue=0f;
        float showingMinFPSValue=float.MaxValue;
        int iterations;
        public static int particlesCount;

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

                if (iterations++ > 6)
                {
                    if (showingMinFPSValue > showingFPSValue) showingMinFPSValue = showingFPSValue;
                    if (showingMaxFPSValue < showingFPSValue) showingMaxFPSValue = showingFPSValue;
                }

                frameCount = 0;
                prevTime = Time.realtimeSinceStartup;
            }

            gc_count = System.GC.CollectionCount(0 /* generation */) - gc_start_count_;
        }
    }
}