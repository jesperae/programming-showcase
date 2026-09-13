using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing
{
    // A fluent interface for building and running sequences of async tasks.
    // Supports actions, delays, async operations, parallel tasks, looping, and abort conditions.
    public class TaskSequence
    {
        #region Global Tracking

        private static readonly List<TaskSequence> _allRunning = new List<TaskSequence>();
        private static readonly object _lock = new object();

        // Aborts all currently running sequences.
        public static void AbortAll()
        {
            lock (_lock)
            {
                for (int i = _allRunning.Count - 1; i >= 0; i--)
                    _allRunning[i].Abort();
                _allRunning.Clear();
            }
        }

        // Aborts the first running sequence matching the given name.
        public static void StopSequence(string name)
        {
            lock (_lock)
            {
                for (int i = _allRunning.Count - 1; i >= 0; i--)
                {
                    if (_allRunning[i].Name == name)
                    {
                        _allRunning[i].Abort();
                        return;
                    }
                }
            }
        }

        public static int RunningCount
        {
            get
            {
                lock (_lock) return _allRunning.Count;
            }
        }

        #endregion

        private readonly List<object> _tasks = new List<object>();
        private CancellationTokenSource _cts;
        private Func<bool> _abortCondition;
        private Action _abortCleanup;
        private Task _runningTask;

        // The name of the sequence, useful for debugging.
        public string Name { get; }

        // Returns true if the sequence is currently running.
        public bool IsRunning => _runningTask != null && !_runningTask.IsCompleted;

        // Creates a new sequence with an optional name.
        public TaskSequence(string name = null)
        {
            Name = name ?? $"Sequence_{GetHashCode()}";
        }

        // Adds a synchronous action to the sequence.
        public TaskSequence Add(Action action)
        {
            _tasks.Add(action);
            return this;
        }

        // Adds an async action to the sequence.
        public TaskSequence AddAsync(Func<Task> asyncAction)
        {
            _tasks.Add(asyncAction);
            return this;
        }

        // Adds a delay to the sequence (in milliseconds).
        public TaskSequence AddDelay(int delayMs)
        {
            _tasks.Add(delayMs);
            return this;
        }

        // Adds a delay to the sequence (in seconds).
        public TaskSequence AddDelay(float delaySeconds)
        {
            _tasks.Add((int)(delaySeconds * 1000));
            return this;
        }

        // Adds tasks to run in parallel, continuing when all complete.
        public TaskSequence AddParallelTasks(params object[] tasks)
        {
            _tasks.Add((Func<Task>)(() => RunParallelTasks(tasks)));
            return this;
        }

        // Adds a loop that runs while a condition is true.
        public TaskSequence AddWaitWhile(Func<bool> condition, params object[] tasks)
        {
            _tasks.Add((Func<Task>)(() => RunWhileAsync(condition, tasks)));
            return this;
        }

        // Adds a do-while loop that runs until a condition becomes true (executes at least once).
        public TaskSequence AddDoWhile(Func<bool> condition, params object[] tasks)
        {
            _tasks.Add((Func<Task>)(() => RunDoWhileAsync(condition, tasks)));
            return this;
        }

        // Adds a loop that runs a fixed number of times.
        public TaskSequence AddLoop(int count, params object[] tasks)
        {
            _tasks.Add((Func<Task>)(() => RunLoopAsync(count, tasks)));
            return this;
        }

        // Adds a callback when the sequence completes.
        public TaskSequence OnComplete(Action callback)
        {
            _tasks.Add(callback);
            return this;
        }

        // Waits for another sequence to complete before continuing.
        public TaskSequence WaitForSequence(TaskSequence sequence)
        {
            _tasks.Add((Func<Task>)(async () =>
            {
                while (sequence != null && sequence.IsRunning)
                    await Task.Delay(16);
            }));
            return this;
        }

        // Waits for a sequence (provided by a function) to complete.
        public TaskSequence WaitForSequence(Func<TaskSequence> sequenceGetter)
        {
            _tasks.Add((Func<Task>)(async () =>
            {
                var seq = sequenceGetter?.Invoke();
                while (seq != null && seq.IsRunning)
                    await Task.Delay(16);
            }));
            return this;
        }

        // Adds an abort condition that will stop the sequence and run cleanup when the condition becomes true.
        public TaskSequence AddAbortCondition(Func<bool> condition, Action cleanup = null)
        {
            _abortCondition = condition;
            _abortCleanup = cleanup;
            return this;
        }

        // Runs the sequence asynchronously, optionally with a completion callback.
        public Task RunAsync(Action onComplete = null)
        {
            if (IsRunning) throw new InvalidOperationException("Sequence is already running.");

            _cts = new CancellationTokenSource();
            lock (_lock) _allRunning.Add(this);

            _runningTask = RunRoutineAsync(() =>
            {
                lock (_lock) _allRunning.Remove(this);
                onComplete?.Invoke();
            });

            return _runningTask;
        }

        // Aborts the sequence immediately and runs cleanup if specified.
        public void Abort()
        {
            if (IsRunning)
            {
                _cts?.Cancel();
                _abortCleanup?.Invoke();
            }
        }

        private async Task RunRoutineAsync(Action onComplete)
        {
            try
            {
                await TaskSequencer.DoRoutineWithAbortAsync(
                    _tasks.ToArray(),
                    _abortCondition,
                    _abortCleanup,
                    _cts?.Token ?? CancellationToken.None);
            }
            finally
            {
                onComplete?.Invoke();
            }
        }

        private async Task RunParallelTasks(object[] tasks)
        {
            var running = new List<Task>();

            foreach (var task in tasks)
            {
                if (task is Action action)
                    running.Add(Task.Run(action));
                else if (task is int delay)
                    running.Add(Task.Delay(delay));
                else if (task is Func<Task> asyncFunc)
                    running.Add(asyncFunc());
                else if (task is float f)
                    running.Add(Task.Delay((int)(f * 1000)));
                else
                    throw new ArgumentException("Invalid task type: " + task);
            }

            await Task.WhenAll(running);
        }

        private async Task RunWhileAsync(Func<bool> condition, object[] tasks)
        {
            while (condition())
            {
                if (tasks.Length > 0)
                    await TaskSequencer.DoRoutineAsync(tasks);
                else
                    await Task.Delay(16);
            }
        }

        private async Task RunDoWhileAsync(Func<bool> condition, object[] tasks)
        {
            do
            {
                await TaskSequencer.DoRoutineAsync(tasks);
            }
            while (!condition());
        }

        private async Task RunLoopAsync(int count, object[] tasks)
        {
            for (int i = 0; i < count; i++)
                await TaskSequencer.DoRoutineAsync(tasks);
        }
    }
}
