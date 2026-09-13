using System;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing
{
    // Extension methods for TaskSequence to provide more intuitive APIs.
    public static class TaskSequenceExtensions
    {
        // Waits for a specific condition to be true before continuing.
        public static TaskSequence WaitFor(
            this TaskSequence sequence,
            Func<bool> condition)
        {
            return sequence.AddWaitWhile(() => !condition());
        }

        // Adds a do-while loop with an optional delay between iterations.
        public static TaskSequence AddDoWhile(
            this TaskSequence sequence,
            Func<bool> condition,
            Action action,
            int delayMs = 16)
        {
            return sequence.AddDoWhile(condition, (Action)action, delayMs);
        }

        // Creates a transition effect that smoothly changes a value over time until a target is reached.
        public static TaskSequence AddTransition<T>(
            this TaskSequence sequence,
            Func<T> getCurrentValue,
            Action<T> setNewValue,
            T targetValue,
            Func<T, T, bool> comparer,
            Func<T, T, float, T> interpolator,
            float smooth = 0.5f,
            int stepMs = 16)
        {
            return sequence.AddDoWhile(
                () => !comparer(getCurrentValue(), targetValue),
                (Action)(() =>
                {
                    var current = getCurrentValue();
                    var next = interpolator(current, targetValue, smooth * (stepMs / 1000f));
                    setNewValue(next);
                }),
                stepMs);
        }

        // Creates a time-based transition over a fixed duration using an interpolation function.
        public static TaskSequence AddTimedTransition<T>(
            this TaskSequence sequence,
            Func<T> getValue,
            Action<T> setValue,
            T startValue,
            T targetValue,
            float durationSeconds,
            Func<T, T, float, T> lerpFunction,
            int stepMs = 16)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int durationMs = (int)(durationSeconds * 1000);

            return sequence.AddDoWhile(
                () => stopwatch.ElapsedMilliseconds < durationMs,
                (Action)(() =>
                {
                    float t = (float)stopwatch.ElapsedMilliseconds / durationMs;
                    t = Math.Min(t, 1f);
                    setValue(lerpFunction(startValue, targetValue, t));
                }),
                stepMs);
        }
    }
}
