using System.Collections;
using Svelto.Tasks.Enumerators;
using UnityEngine;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    public partial class MillionPointsCPU
    {
        IEnumerator MainThreadOperations(ParticleCounter particleCounter)
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            var syncRunner = new SyncRunner();

            //these will help with synchronization between threads
            WaitForSignalEnumerator _waitForSignal = new WaitForSignalEnumerator(() => _breakIt);
            WaitForSignalEnumerator _otherwaitForSignal = new WaitForSignalEnumerator();

            //Start the operations on other threads
            OperationsRunningOnOtherThreads(_waitForSignal, _otherwaitForSignal)
                .ThreadSafeRunOnSchedule(StandardSchedulers.multiThreadScheduler);

            //start the mainloop
            while (true)
            {
                _time = Time.time / 10;
                
                //wait until the other thread tell us that the data is ready to be used.
                //Note that I am stalling the main thread here! This is entirely up to you
                //if you don't want to stall it, as you can see with the other use cases
                _otherwaitForSignal.RunOnSchedule(syncRunner);
#if BENCHMARK
                if (PerformanceCheker.PerformanceProfiler.showingFPSValue > 30.0f)
                {
                    
                    if (particleCounter.particlesLimit >= 16)
                        particleCounter.particlesLimit -= 16;

                    PerformanceCheker.PerformanceProfiler.particlesCount = particleCounter.particlesTransformed;
                }
                
                particleCounter.particlesTransformed = 0;
#endif    
                
                _particleDataBuffer.SetData(_gpuparticleDataArr);

                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);

                //tell to the other thread that now it can perform the operations
                //for the next frame.
                _waitForSignal.Signal();

                //continue the cycle on the next frame
                yield return null;
            }
        }
        
        IEnumerator OperationsRunningOnOtherThreads(WaitForSignalEnumerator waitForSignal,
            WaitForSignalEnumerator otherWaitForSignal)
        {
            //a SyncRunner stop the execution of the thread until the task is not completed
            //the parameter true means that the runner will sleep in between yields
            var syncRunner = new SyncRunner();

            while (_breakIt == false)
            {
                //execute the tasks. The MultiParallelTask is a special collection
                //that uses N threads on its own to execute the tasks. This thread
                //doesn't need to do anything else meanwhile and will yield until
                //is done. That's why the syncrunner can sleep between yields, so 
                //that this thread won't take much CPU just to wait the parallel 
                //tasks to finish
                _multiParallelTasks.ThreadSafeRunOnSchedule(syncRunner);                
                //the 1 Million particles operation are done, let's signal that the
                //result can now be used
                otherWaitForSignal.Signal();
                //wait until the application is over or the main thread will tell
                //us that now we can perform again the particles operation. This 
                //is an explicit while instead of a yield, just because if the _breakIt
                //condition, which is needed only because if this application runs
                //in the editor, the threads spawned will not stop until the Editor is 
                //shut down.
                waitForSignal.RunOnSchedule(syncRunner);
            }

            //the application is shutting down. This is not that necessary in a 
            //standalone client, but necessary to stop the thread when the 
            //application is stopped in the Editor to stop all the threads.
            _multiParallelTasks.ClearAndKill();

            TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();

            yield break;
        }
    }
}