using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SuperDrive.Core.Support
{
    /// <summary>
    /// 使用这个而不是用默认的Task.Run，是因为要支持IsValid，到了任务执行的时候，可以判断是否还需要执行。而不用每次创建、销毁CancellationToken.
    /// </summary>
    public class ParalellTaskPool
    {
        Queue<MTask> _taskList = new Queue<MTask>(32);
        AutoResetEvent _taskWaiter = new AutoResetEvent(false);
        private TaskWrapper _tasksRunner;
        private string _name;
        public ParalellTaskPool(string name="")
        {
            _name = name;
            _tasksRunner = new TaskWrapper($"TaskWrapper TaskPool [{name}]", RunAction,  TaskCreationOptions.LongRunning);
        }

#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        private async Task RunAction(CancellationToken token)
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        {
            MTask task;
            while (!token.IsCancellationRequested)
            {
                if (_taskList.Count > 0)
                {
                    lock (_taskList) task = _taskList.Peek();
                    //Env.Logger.Log($"Now pick task[{task}] from task pool", _name);
                    if (task != null && task.IsValid() && task.ActualTask != null)
                    {
                        //Env.Logger.Log($"start await task[{task}] from task pool", _name);
                        //CancellationTokenSource taskTokenSource = new CancellationTokenSource();
                        //taskTokenSource.Token
                        try
                        {
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                            task.ActualTask.Invoke();
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                        }
                        catch (Exception e)
                        {
                            Env.Logger.Log($"执行任务发生异常。{task}", _name,stackTrace:e.StackTrace,level:LogLevel.Error);
                        }
                        //Env.Logger.Log($"after await task[{task}] from task pool", _name);
                    }

                    lock (_taskList) _taskList.Dequeue();
                }
                else
                    _taskWaiter.WaitOne();
            }

            Env.Logger.Log($"Task pool is end now", _name);

            lock (_taskList) _taskList.Clear();
        }
        public void PostTask(Func<Task> task, Func<bool> isValid = null, Func<string> toStringImpl = null)
        {
            var mtask = new MTask
            {
                ActualTask = task,
                IsValidImpl = isValid,
                ToStringImpl = toStringImpl
            };
            PostTask(mtask);
        }


        public void Stop()
        {
            _taskWaiter.Set();
            _tasksRunner?.Stop();
            _tasksRunner = null;
        }

        
        public async void PostTask(MTask task) //用async声明的函数，调用的时候会立即创建线程。
        {
            lock (_taskList)
            {
                if (task == null || _taskList.Contains(task)) return;
            }

            while (true)
            {
                if (_taskList.Count > 500) //允许队列中最多放置多少个任务。
                {
                    _taskWaiter.Set();
                    await Task.Delay(400);
                }
                else
                {
                    lock (_taskList) _taskList.Enqueue(task);
                    _taskWaiter.Set();
                    //如果成功添加，则退出。
                    break;
                }
            }
        }

        public void Start()
        {
            if (_tasksRunner.IsRunning) return;
            _tasksRunner.Start();
            
        }
    }
}
