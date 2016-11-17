using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SuperDrive.Core.Support
{
    public class TaskWrapper
    {
        private Task _task;
        private Func<CancellationToken,Task> _userAction;
        private CancellationTokenSource _cts;
        private TaskCreationOptions _taskCreationOptions;
        private string _name;

        //限定传入类型为Func<...,Task>是为了避免传入async Action，那样没法等待async function结束。
        public TaskWrapper(string name,Func<CancellationToken,Task> userAction, TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
        {
            Util.CheckParam(userAction != null);

            _userAction = userAction;
            _taskCreationOptions = taskCreationOptions;
            _name = name;
        }
        public bool IsRunning => _task != null && !_task.IsCompleted;

        public bool IsCancellationRequested => _cts?.IsCancellationRequested?? true;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            //TODO Task Scheduler是什么东西？
            Action action = () =>
            {
                try
                {
                    Task t = _userAction(token);
                    //TODO 如果t内部有await, 这里又想要wait这个任务，如果不是控制台程序，必须ConfigureAwait false,否则就死锁了。
                    t.ConfigureAwait(false);
                    //这是怎么实现的？在这里Wait一个AutoResetEvent? 然后_userAction里面的await，也是启动一个线程，然后wait一个Event? 线程结束后release event?
                    t.Wait(token);
                }
                catch (Exception e)
                {
                    //这个action本身不能是异步的。否则这个action一启动，Task就ranto completion了。
                    //定义这个action，并要求user action返回Task,都是为了防止用户传入async action.
                    
                    //这个action还要保证它不会先于它调用的那些东西退出。所以这个包装的action，又去wait真正的action。
                    //但在wait的过程中，那个任务真的结束了，它会反过来告诉TaskWrapper
                    //你可以执行清理工作了，TaskWrapper会设置CancellationTokenSource.Cance,就引发了这个wait的异常。        

                    //这是一个正常的逻辑。所以不做任何处理，直接退出就行了。是内部的真正的userAction真的执行完了。
                    //它告诉这个包装者，不必再等了。这是需要的。否则内部的执行完了，这个包装者不知道。状态都没清理。

                    //外界使用这个TaskWrapper的时候，需要检查它是不是已经被清理了。不过有Start/Stop两个接口够了。用户
                    //如果错误的调用了stop(相当于它已经被dispose),再次调用start，那也没关系，再次启动一下就完了。
                }

                //到这里 _task.IsCompleted就==true了。
            }; 
            _task = new Task(action,token, _taskCreationOptions);
            _task.ConfigureAwait(false);
            _task.ContinueWith(t => Stop(),token);
            _task.Start();
        }

        public void Stop()
        {
            if (IsRunning)
            {
                _cts?.Cancel();
                try
                {
                    _task?.Wait(TimeSpan.FromSeconds(2)); //最多等待两秒钟
                }
                catch (Exception)
                {
                    //cts.Cancel,等待task的人会收到异常。以前没收到，那是因为等待的task不对。
                    Env.Logger.Log($"Task wrapper {_name} wait exception");
                }
                
                _cts?.Dispose();
                _task = null;
                _cts = null;
            }
        }
    }
}
