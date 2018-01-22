using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PerformanceCheker
{
    [RequireComponent(typeof(PerformanceProfiler))]
    public class PerformanceProfilerUGUI : MonoBehaviour
    {
        [SerializeField]
        Text text;
        
        PerformanceProfiler profiler;

        StringBuilder bufferedString = new StringBuilder(320);

        // Use this for initialization
        void Start()
        {
            profiler = GetComponent<PerformanceProfiler>();
            if (text == null)
            {
                Destroy(this);
            }
        }

        // Update is called once per frame
        void Update()
        {
            //clear
            bufferedString.Length = 0;
            
            bufferedString.Append("FPS:");
            bufferedString.Append(profiler.CurrentFPS);
            bufferedString.Append("\r\n");
            bufferedString.Append("Max FPS:");
            bufferedString.Append(profiler.MaxFPS);
            bufferedString.Append("\r\n");
            bufferedString.Append("Min FPS:");
            bufferedString.Append(profiler.MinFPS);
#if BENCHMARK                        
            bufferedString.Append("\r\n");
            bufferedString.Append("Particles Transformed:");
            bufferedString.Append(PerformanceProfiler.particlesCount);
#endif    
            bufferedString.Append("\r\n");
            bufferedString.Append("GC Alloc Num:");
            bufferedString.Append(profiler.GCcount);
            
            text.text = bufferedString.ToString();
        }
    }
}