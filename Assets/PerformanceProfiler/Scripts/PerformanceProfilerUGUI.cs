using System.Collections;
using System.Collections.Generic;
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

        StringBuilder bufferedString= new StringBuilder(320);

        // Use this for initialization
        void Start()
        {
            profiler = GetComponent<PerformanceProfiler>();
            if (text == null)
            {
                Debug.LogError("UGUIのtextをインスペクタから指定してください");
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
            bufferedString.Append("heap(KB):");
            bufferedString.Append(profiler.UsedHeapSize/1024);
            bufferedString.Append("\r\n");
            bufferedString.Append("mono heap size(KB):");
            bufferedString.Append(profiler.MonoHeapSize/1024);
            bufferedString.Append("\r\n");
            bufferedString.Append("used mono heap size(KB):");
            bufferedString.Append(profiler.MonoUsedSize/1024);
            bufferedString.Append("\r\n");
            bufferedString.Append("GC Alloc Num:");
            bufferedString.Append(profiler.GCcount);
            text.text = bufferedString.ToString();
            
            
        }
    }
}