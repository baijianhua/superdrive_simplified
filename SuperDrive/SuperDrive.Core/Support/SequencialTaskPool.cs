using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable ExplicitCallerInfoArgument
#pragma warning disable 4014
#pragma warning disable 1998

namespace SuperDrive.Core.Support
{
        public class SequencialTaskPool
        {
                private readonly Queue<MTask> _taskList = new Queue<MTask>(32);
                private readonly AutoResetEvent _taskWaiter = new AutoResetEvent(true);
                private TaskWrapper _tasksRunner;
                private readonly string _name;
                private int _maxRunnerCount;
                private int _runnerCount;

                public SequencialTaskPool(string name = "", int maxRunnerCount = 1)
                {
                        _maxRunnerCount = maxRunnerCount;
                        _name = name;
                        _tasksRunner = new TaskWrapper($"Sequential task pool [{name}]", RunAction, TaskCreationOptions.LongRunning);
                }

                private async Task RunAction(CancellationToken token)
                {
                        while (!token.IsCancellationRequested)
                        {
                                _taskWaiter.WaitOne();
                                if (_taskList.Count <= 0 ||  _runnerCount >= _maxRunnerCount) continue;

                                MTask task;
                                lock (_taskList) task = _taskList.Dequeue();
                                if (task == null || !task.IsValid() || task.ActualTask == null) continue;

                                Task.Run(() =>
                                {
                                        try
                                        {
                                                lock (_taskList)  _runnerCount++;
                                                var t = task.ActualTask.Invoke();
                                                t.ConfigureAwait(false);
                                                t.Wait(30*1000, token); //单个任务最多等待30秒钟。超时就不管理了。
                                                if (!t.IsCompleted)
                                                {
                                                        //TODO 怎么取消任务呢？让它强行终止，好像没提供这种机制。抛出异常？debug版可以这样做。
                                                        Env.Logger.Log($"任务运行时间过长，不再等待此任务{task},但任务仍在运行。", _name);
                                                }
                                        }
                                        catch (Exception e)
                                        {
                                                Env.Logger.Log($"执行任务发生异常。{task}", _name, stackTrace: e.StackTrace,level: LogLevel.Error);
                                        }
                                        finally
                                        {
                                                lock(_taskList) _runnerCount--;
                                                _taskWaiter.Set();
                                        }
                                }, token);
                        }

                        Env.Logger.Log($"Task pool is end now", _name);
                        lock (_taskList) _taskList.Clear();
                }
                public void PostTask(Func<Task> task, Func<bool> isValid = null, Func<string> toStringImpl = null)
                {
                        MTask mtask = new MTask
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
                                if (task == null || _taskList.Contains(task)) return;

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
                                        Env.Logger.Log($"Enqueue Task{task} runnerCount={_runnerCount}");
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
