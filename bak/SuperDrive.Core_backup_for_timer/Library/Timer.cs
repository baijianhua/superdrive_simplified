using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SuperDrive.Core.Library
{
    public class Timer
    {
        private TimeSpan _timeSpan ;
        
        private bool isRunning = false;
        private CancellationToken _token;
        private CancellationTokenSource _source;
        private Task waitingTask;
        public Timer(TimeSpan timeSpan, Action timeoutAction = null, bool autoStart = true)
        {
            Contract.Requires(timeSpan != null);

            TimeoutAction = timeoutAction;
            _timeSpan = timeSpan;
            if (autoStart)
            {
                Start();
            }
        }
        public async void Start()
        {
            //需要阻止重复调用Start。
            if (isRunning) return;

            isRunning = true;
            Contract.Requires(_timeSpan != null);
            Contract.Requires( TimeoutAction != null);
            _source = new CancellationTokenSource();
            _token = _source.Token;
            waitingTask =  Task.Delay(_timeSpan,_token);
            await waitingTask;
            if(!_token.IsCancellationRequested) TimeoutAction?.Invoke();
        }

        public async void Stop()
        {
            _source.Cancel();
            if (waitingTask != null) await waitingTask;
            isRunning = false;
        }

        public async void ReStart()
        {
            Stop();
            if (waitingTask != null) await waitingTask;
            //如果曾经运行过，等待上一个终止。
            Start();
        }
        private Action TimeoutAction { get; set; }
    }
}
