using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    public partial class MillionPointsCPU
    {
        IEnumerator MainThreadLoopWithNaiveSynchronization()
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            var syncRunner = new SyncRunner();

            while (_breakIt == false)
            {
                _time = Time.time;

                //exploit continuation here. Note that we are using the SyncRunner here
                //this will actually stall the mainthread and its execution until
                //the multiParallelTask is done
                yield return _multiParallelTasks.ThreadSafeRunOnSchedule(syncRunner);
                //then it resumes here, copying the result to the particleDataBuffer.
                //remember, multiParalleTasks is not executing anymore until the next frame!
                //so the array is safe to use
                _particleDataBuffer.SetData(_gpuparticleDataArr);
                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);

                //continue the cycle on the next frame
                yield return null;
            }

            //the application is shutting down. This is not that necessary in a 
            //standalone client, but necessary to stop the thread when the 
            //application is stopped in the Editor to stop all the threads.
            _multiParallelTasks.ClearAndKill();

            TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();
        }
    }
}