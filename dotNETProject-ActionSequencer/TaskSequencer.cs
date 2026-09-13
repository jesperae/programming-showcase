using System;
using System.Threading;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing
{
    // Static utility for creating and running async task sequences.
    public static class TaskSequencer
    {
        // Creates a new TaskSequence for fluent chaining.
        public static TaskSequence CreateSequence(string name = null)
        {
            return new TaskSequence(name);
        }

        // Executes a list of tasks sequentially.
        public static async Task DoRoutineAsync(object[] tasks)
        {
            foreach (var task in tasks)
            {
                if (task is Action action) action();
                else if (task is int delay) await Task.Delay(delay);
                else if (task is float f) await Task.Delay((int)(f * 1000));
                else if (task is Func<Task> asyncFunc) await asyncFunc();
                else throw new ArgumentException("Unsupported task type: " + task);
            }
        }

        // Executes tasks with abort condition checking.
        public static async Task DoRoutineWithAbortAsync(
            object[] tasks,
            Func<bool> abortCondition,
            Action abortCleanup,
            CancellationToken cancellationToken)
        {
            foreach (var task in tasks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    abortCleanup?.Invoke();
                    return;
                }

                if (abortCondition?.Invoke() == true)
                {
                    abortCleanup?.Invoke();
                    return;
                }

                if (task is Action action)
                {
                    action();
                }
                else if (task is int delay)
                {
                    // Check abort condition during delays
                    int elapsed = 0;
                    int step = Math.Min(16, delay);
                    while (elapsed < delay)
                    {
                        if (cancellationToken.IsCancellationRequested || abortCondition?.Invoke() == true)
                        {
                            abortCleanup?.Invoke();
                            return;
                        }
                        await Task.Delay(step);
                        elapsed += step;
                    }
                }
                else if (task is float f)
                {
                    int floatDelayMs = (int)(f * 1000);
                    int floatElapsed = 0;
                    int floatStep = Math.Min(16, floatDelayMs);
                    while (floatElapsed < floatDelayMs)
                    {
                        if (cancellationToken.IsCancellationRequested || abortCondition?.Invoke() == true)
                        {
                            abortCleanup?.Invoke();
                            return;
                        }
                        await Task.Delay(floatStep);
                        floatElapsed += floatStep;
                    }
                }
                else if (task is Func<Task> asyncFunc)
                {
                    await asyncFunc();
                }
                else
                {
                    throw new ArgumentException("Unsupported task type: " + task);
                }
            }
        }
    }
}
