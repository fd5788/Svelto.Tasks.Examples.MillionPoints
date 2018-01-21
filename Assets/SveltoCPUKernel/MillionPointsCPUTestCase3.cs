using System;
using System.Collections;
using Svelto.Utilities;
using UnityEngine;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    public partial class MillionPointsCPU
    {
        IEnumerator MainLoopOnOtherThread()
        {
            var syncRunner = new SyncRunner();

            var then = DateTime.Now;

            RenderingOnCoroutineRunner().ThreadSafeRun();
            var CopyBufferOnUpdateRunner = new SimpleEnumerator(this); //let's avoid useless allocations

            while (_breakIt == false)
            {
                _time = (float) (DateTime.Now - then).TotalSeconds;
                //exploit continuation here. Note that we are using the SyncRunner here
                //this will actually stall the mainthread and its execution until
                //the multiParallelTask is done
                yield return _multiParallelTask.ThreadSafeRunOnSchedule(syncRunner);
                //then it resumes here, copying the result to the particleDataBuffer.
                //remember, multiParalleTasks is not executing anymore until the next frame!
                //so the array is safe to use
                var continuator = CopyBufferOnUpdateRunner.ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);

                while (_breakIt == false && continuator.MoveNext() == true) ThreadUtility.Yield();
            }

            //the application is shutting down. This is not that necessary in a 
            //standalone client, but necessary to stop the thread when the 
            //application is stopped in the Editor to stop all the threads.
            _multiParallelTask.ClearAndKill();

            TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();
        }

        IEnumerator RenderingOnCoroutineRunner()
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            while (true)
            {
                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);

                //continue the cycle on the next frame
                yield return null;
            }
        }
        
        class SimpleEnumerator:IEnumerator
        {
            MillionPointsCPU _million;

            public SimpleEnumerator(MillionPointsCPU million)
            {
                _million = million;
            }
            
            public bool MoveNext()
            {
                _million._particleDataBuffer.SetData(_million._gpuparticleDataArr);

                return false;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public object Current { get; }
        }
    }
}