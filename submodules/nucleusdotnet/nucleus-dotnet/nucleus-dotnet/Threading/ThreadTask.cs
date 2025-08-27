using System;
using System.Diagnostics;
using System.Linq;

namespace Nucleus.Threading {
    public class ThreadTask {
        public int ThreadID { get; private set; }
        public string Prefix { get; private set; }

        public Action<ThreadTask> Task { get; private set; }
        public float Progress { get; private set; }
        public bool Finished { get; private set; }
        public bool Enabled { get; set; } = true;

        public long StartTime { get; private set; }
        public string Name { get; private set; }

        public object Data { get; set; }

        public ThreadTask[] DependsOn { get; private set; }
        public TaskManager TaskManager { get; private set; }
        public bool NeedReexecution { get; private set; }
        public string ReexecutionMessage { get; private set; }

        public int Priority { get; set; } = int.MaxValue;
        public double OffssetTime { get; set; }

        public Action OnFinished { get; set; }

        public ThreadTask(TaskManager taskManager, string name, Action<ThreadTask> task, object data = null) {
            TaskManager = taskManager;
            ThreadID = -1;
            Task = task;
            Progress = 0;
            Data = data;
            DependsOn = new ThreadTask[0];

            StartTime = Stopwatch.GetTimestamp();
            Name = name;
        }

        public bool CanExecute {
            get {
                if (Enabled) {
                    for (int i = 0; i < DependsOn.Length; i++) {
                        if (!DependsOn[i].Finished) {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }
        }

        public override string ToString() {
            if (Data == null) {
                return $"{Name} {(Finished ? "(DONE)" : "")}";
            } else {
                return $"{Name} DATA ({Data}){(Finished ? " (DONE)" : "")}";
            }
        }

        public void SetToReexecute(string msg) {
            ReexecutionMessage = msg;
            NeedReexecution = true;
        }
        public void ResetToReexecute() {
            NeedReexecution = false;
        }

        public void AssignThread(int threadID) {
            ThreadID = threadID;
            Prefix = $"[{threadID}]";
        }

        public void SetProgress(float progress) {
            Progress = progress;
        }

        public void SetFinished() {
            Finished = true;
            Progress = 1.0f;

            if (OnFinished != null) {
                OnFinished();
            }
        }

        public void SetDependency(params ThreadTask[] tasks) {
            var list = DependsOn.ToList();
            list.AddRange(tasks);

            // clear finished from list
            for (int i = 0; i < list.Count; i++) {
                if (list[i].Finished) {
                    list.RemoveAt(i);
                    i--;
                }
            }
            DependsOn = list.ToArray();
        }
    }
}
