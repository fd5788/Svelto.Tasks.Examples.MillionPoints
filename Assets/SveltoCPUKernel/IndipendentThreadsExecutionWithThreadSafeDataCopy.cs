using System;
using System.Collections;
using UnityEngine;

//
//I was torn between showing this example or not this is different from the other. If enabled this shows how to keep
//the two thread running without synchronization, while sharing code in thread safe fashion. Should write another
//article about this. The Mesh will be rendered at the speed of the VSync, but the particles data will be updated
//only when the parallel operations have finished to execute.
//

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    public partial class MillionPointsCPU
    {
        //yes this is running from another thread
        IEnumerator MultiThreadsRunningIndipendently()
        {
            var then = DateTime.Now;

            //Let's start the MainThread Loop
            RenderingOnCoroutineRunner().ThreadSafeRunOnSchedule(StandardSchedulers.coroutineScheduler);
            
            var CopyBufferOnUpdateRunner = new SimpleEnumerator(this); //let's avoid useless allocations
            
            while (true)
            {
                _time = (float) (DateTime.Now - then).TotalSeconds;
                //The main thread will be stuck until the multiParallelTask has been
                //executed. A MultiParallelTaskCollection relies on its own
                //internal threads to run, so although the Main thread is stuck
                //the operation will complete
                yield return _multiParallelTasks;
                //then it resumes here, however the just computed particles 
                //cannot be passed to the compute buffer now,
                //as the Unity methods are not thread safe
                //so I have to run a simple enumerator on the main thread
                var continuator = CopyBufferOnUpdateRunner.ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
                //and I will wait it to complete, still exploting the continuation wrapper.
                //We need to wait the MainThread to finish its operation before to run the 
                //next iteration. 
                yield return continuator;
            }
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