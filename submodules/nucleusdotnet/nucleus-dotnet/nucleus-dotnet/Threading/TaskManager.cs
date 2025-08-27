using Nucleus.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Nucleus.Threading {
    public class TaskManager {
        private readonly bool finished;

        private readonly List<ThreadTask> queuedTasks;
        private readonly ThreadData[] threadDatas;

        private readonly Thread[] threads;
        private bool autoSort = false;

        public List<ThreadTask> QueuedTasks { get { return queuedTasks; } }
        public ThreadData[] ThreadData { get { return threadDatas; } }
        public bool NoTasks { get { return queuedTasks.Count == 0; } }
        public int ThreadCount { get; private set; }

        public TaskManager(int numThreads) {
            ThreadCount = numThreads;
            queuedTasks = new List<ThreadTask>();

            threads = new Thread[numThreads];
            threadDatas = new ThreadData[ThreadCount];

            for (int i = 0; i < numThreads; i++) {
                ThreadData tData = new ThreadData(i);
                threadDatas[i] = tData;
                Thread t = new Thread(ThreadCallback);
                threads[i] = t;
            }
        }

        public int Compare(ThreadTask a, ThreadTask b) {
            return (a.Priority).CompareTo(b.Priority);
        }

        public ThreadTask QueueTask(string name, Action<ThreadTask> taskCallback, object data = null) {
            lock (queuedTasks) {
                ThreadTask nTask = new ThreadTask(this, name, taskCallback, data);
                queuedTasks.Add(nTask);
                if (autoSort) {
                    queuedTasks.Sort(Compare);
                }
                return nTask;
            }
        }

        public ThreadTask QueueDisabledTask(string name, Action<ThreadTask> taskCallback, object data = null) {
            lock (queuedTasks) {
                ThreadTask nTask = new ThreadTask(this, name, taskCallback, data);
                nTask.Enabled = false;
                queuedTasks.Add(nTask);
                if (autoSort) {
                    queuedTasks.Sort(Compare);
                }
                return nTask;
            }
        }

        public void UpdatePriority() {
            lock (queuedTasks) {
                queuedTasks.Sort(Compare);
            }
            autoSort = true;
        }

        public void Start() {
            for (int i = 0; i < ThreadCount; i++) {
                threads[i].Start(threadDatas[i]);
            }
        }

        private ThreadTask GrabTask(long currentTime) {
            lock (queuedTasks) {
                queuedTasks.Sort(Compare);
                foreach (ThreadTask task in queuedTasks) {
                    if (!task.Enabled ||
                        currentTime < task.OffssetTime ||
                        !task.CanExecute) {
                        continue;
                    }

                    queuedTasks.Remove(task);
                    return task;
                }

                return null;
            }
        }

        private void ReturnTask(ThreadTask task) {
            task.AssignThread(-1);
            task.SetProgress(0);

            lock (queuedTasks) {
                //queuedTasks.Insert(0, task);
                queuedTasks.Add(task);
                queuedTasks.Sort(Compare);
            }
        }

        public double MinTimeTaskLog { get; set; } = 250;

        private void ThreadCallback(object state) {
            ThreadData tData = (ThreadData)state;
            ConsoleU.WriteLine($"[{tData.ThreadID}] THREAD {tData.ThreadID} ON", ConsoleColor.Green, OutputLevel.Medium);

            for (; ; ) {
                long startTime = Stopwatch.GetTimestamp();
                ThreadTask task = GrabTask(startTime);

                if (task != null) {
                    tData.CurrentTask = task;

                    task.AssignThread(tData.ThreadID);

                    // execute task
                    task.Task(task);

                    long endTime = Stopwatch.GetTimestamp();
                    bool doLog = false;
                    double sElapsed = ((endTime - startTime) / (double)Stopwatch.Frequency) * 1000.0;
                    if (sElapsed > MinTimeTaskLog) {
                        doLog = true;
                        ConsoleU.WriteLine($"[{tData.ThreadID}] TASK {task}", ConsoleColor.Green, OutputLevel.Medium);
                    }

                    // return to queued if its not finished
                    if (!task.Finished) {
                        bool printReturn = doLog;

                        if (printReturn) {
                            if (task.DependsOn.Length > 0) {
                                string depends = "";
                                bool first = true;
                                for (int i = 0; i < task.DependsOn.Length; i++) {
                                    ThreadTask depend = task.DependsOn[i];
                                    string name = depend.Finished ? $"{depend.Name}(DONE)" : depend.Name;
                                    depends += !first ? (", " + name) : name;
                                    if (first) {
                                        first = false;
                                    }
                                }

                                if (task.NeedReexecution) {
                                    ConsoleU.WriteLine($"[{tData.ThreadID}] RET TASK: {task} FOR {depends} AND RE-EXECUTION ({task.ReexecutionMessage})", ConsoleColor.Yellow, OutputLevel.Medium);
                                } else {
                                    ConsoleU.WriteLine($"[{tData.ThreadID}] RET TASK: {task} FOR {depends}", ConsoleColor.Yellow, OutputLevel.Medium);
                                }
                            } else if (task.NeedReexecution) {
                                ConsoleU.WriteLine($"[{tData.ThreadID}] RET TASK: {task} FOR RE-EXECUTION ({task.ReexecutionMessage})", ConsoleColor.Yellow, OutputLevel.Medium);
                            } else {
                                ConsoleU.WriteLine($"[{tData.ThreadID}] RET TASK: {task} FOR UNKNOWN REASON", ConsoleColor.Red, OutputLevel.High);
                            }
                        }

                        task.ResetToReexecute();
                        ReturnTask(task);
                    } else {
                        ConsoleU.WriteLine($"[{tData.ThreadID}] FINISH TASK: {task} ELAPSED: {(sElapsed / 1000.0)}s", ConsoleColor.Green, OutputLevel.Medium);
                    }

                    //tData.CurrentTask = null; // dont remove so we can read the last task
                }

                //Thread.Sleep(100);
                //Thread.Sleep(1);// 1ms sleep
                Thread.Sleep(TimeSpan.FromTicks(1));// 1 tick sleep

                if (finished) {
                    break;
                }
            }
        }
    }
}
