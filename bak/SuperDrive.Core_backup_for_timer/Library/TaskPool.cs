using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Util;

namespace SuperDrive.Core.Library
{
    public class TaskPool
    {
        object _taskQueueLocker = new object();
        Queue<MTask> _taskList = new Queue<MTask>(32);
        AutoResetEvent _taskWaiter = new AutoResetEvent(false);
        CancellationTokenSource source = new CancellationTokenSource();
        

        public void PostTask(Action task, Func<bool> isValid, Func<string> toStringImpl = null)
        {
            MTask mtask = new MTask(task, isValid, toStringImpl);
            //Debug.WriteLine($"-------Post task {mtask}");
            PostTask(mtask);
        }


        public void Stop()
        {
            source.Cancel();
        }

        
        public async void PostTask(MTask task) //用async声明的函数，调用的时候会立即创建线程。
        {
            if (task == null) return;

            while (true)
            {
                if (_taskList.Count < 500)//允许队列中最多放置多少个任务。
                {
                    try
                    {
                        //Debug.WriteLine("Post task " + task);
                        lock (_taskQueueLocker) _taskList.Enqueue(task);
                        _taskWaiter.Set();
                        //如果成功添加，则退出。
                        break;
                    }
                    catch (Exception e)
                    {
                        //如果task执行太慢，这样会Enqueue很多Task，在内存受限时，会导致内存溢出异常。
                        //那么先让当前线程阻塞，让任务处理线程先工作一会儿。但这个动作并不会阻碍主线程。因为PostTask本身是Async的。
                        await SleepAndWaitForProcessing();
                    }
                }
                else
                {
                    await SleepAndWaitForProcessing();
                }
            }
        }

        async Task SleepAndWaitForProcessing()
        {
            //var count = Environment.TickCount;
            //Debug.WriteLine("+++++++tasks count before sleep=" + taskList.Count);
            if (_taskList.Count > 0)
            {
                //如果队列里面有任务，需要set一下，任务处理线程才能开始工作。
                //如果队列不空，其实_taskWaiter不会进入wait状态。
                _taskWaiter.Set();
            }
            await Task.Delay(400);
            //Debug.WriteLine("+++++++[" + (Environment.TickCount - count) + "]tasks count aftert sleep=" + taskList.Count);
        }

        public void Start()
        {
            var token = source.Token;
            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (_taskList.Count > 0)
                    {
                        //Debug.WriteLine("conusuming task" + taskList.Count);
                        MTask task;
                        lock (_taskQueueLocker) task = _taskList.Dequeue();

                        //Debug.WriteLine("CheckTask"+task);
                        //ReSharper disable once UseNullPropagation
                        if (task != null && task.IsValid() && task.ActualTask != null)
                        {
                            task.ActualTask.Invoke();
                            //Debug.WriteLine("--------Execute task" + task);
                        }
                        else
                        {
                            //Debug.WriteLine("--------Skip task" + task);
                        }

                    }
                    else
                    {
                        _taskWaiter.WaitOne();
                    }
                }
                _taskList.Clear();
            }, token);
        }
    }
}
