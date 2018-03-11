using System;
using System.Collections;
using Svelto.Tasks.Enumerators;
using UnityEngine;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    //
    // Most advanced scenario to synchronize two different thread
    //
    
    public partial class MillionPointsCPU
    {
        IEnumerator SignalBasedAdvancedMultithreadYielding()
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            //these will help with synchronization between threads
            WaitForSignalEnumerator waitForSignal = new WaitForSignalEnumerator("MainThreadWait", 1000);
            WaitForSignalEnumerator otherwaitForSignal = new WaitForSignalEnumerator
                ("OtherThreadWait", () => isActiveAndEnabled == false, 1000);
            
            //Start the operations on other threads
            OperationsRunningOnOtherThreads(waitForSignal, otherwaitForSignal)
                .ThreadSafeRunOnSchedule(StandardSchedulers.multiThreadScheduler);

            //start the main thread loop
            while (true)
            {
                _time = Time.time / 10;

                //Since we want to feed the GPU with the data processed 
                //from the other thread, we can't set the particleDataBuffer
                //until this operation is done. For this reason we stall
                //the mainthread until the data is ready. This operation is advanced
                //as it could stall the game for ever if you don't know
                //what you are doing! It's faster than the naive way though
                //mind that I could have simply wrote
                //yield return otherwaitForSignal;
                //but I want the mainthread actually to stall so that
                //the profile can measure the time taken to wait here
                otherwaitForSignal.Complete();
                
                if (_pc.particlesTransformed < 999900)
                    Utility.Console.LogError("not enough particles transformed");
#if BENCHMARK
                if (PerformanceCheker.PerformanceProfiler.showingFPSValue > 30.0f)
                {
                    
                    if (_pc.particlesLimit >= 16)
                        _pc.particlesLimit -= 16;

                    PerformanceCheker.PerformanceProfiler.particlesCount = _pc.particlesTransformed;
                }
#endif
                _pc.particlesTransformed = 0;
                _particleDataBuffer.SetData(_gpuparticleDataArr);

                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);

                //tell to the other thread that now it can perform the operations
                //for the next frame.
                waitForSignal.Signal();

                //continue the cycle on the next frame
                yield return null;
            }
        }

        IEnumerator OperationsRunningOnOtherThreads(WaitForSignalEnumerator mainWaitForSignal,
                                                    WaitForSignalEnumerator otherWaitForSignal)
        {
            while (true)
            {
                //execute the tasks. The MultiParallelTask is a special collection
                //that uses N threads on its own to execute the tasks. The
                //complete operation is similar to the Unity Jobs complete 
                //operations. It stalls the thread where it's called from
                //until everything is done!
                yield return _multiParallelTasks;
                //yield return _multiParallelTasks;
                //the 1 Million particles operation are done, let's signal that the
                //result can now be used
                otherWaitForSignal.Signal();
                //yield until the application is over or the main thread will tell
                //us that now we can perform again the particles operation.
                //since we are not using the thread for anything else
                //we can stall the thread here until is done
                yield return mainWaitForSignal;
            }
        }

    }
}