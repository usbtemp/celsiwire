using System;
using System.ComponentModel;

namespace Display
{
    sealed class BackgroundTask : IDisposable
    {
        private readonly BackgroundWorker worker;
        public bool Running { get { return worker.IsBusy; } }

        public BackgroundTask(DoWorkEventHandler work, RunWorkerCompletedEventHandler completed)
        {
            worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(work);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(completed);
        }

        public void Start()
        {
            worker.RunWorkerAsync();
        }

        public void Dispose()
        {
            worker.Dispose();
        }
    }
}
