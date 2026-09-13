using System;
using System.Threading.Tasks;
using Grynsoft.TaskSequencing;

namespace TaskSequencingExample
{
    class Program
    {
        static async Task Main()
        {
            Console.WriteLine("=== TaskSequencer Examples ===\n");

            // 1. Basic sequence with delay
            Console.WriteLine("1. Basic sequence:");
            await TaskSequencer.CreateSequence("Basic")
                .Add(() => Console.WriteLine("  Step 1: Hello"))
                .AddDelay(500)
                .Add(() => Console.WriteLine("  Step 2: After 500ms"))
                .RunAsync();

            // 2. Parallel tasks
            Console.WriteLine("\n2. Parallel tasks:");
            await TaskSequencer.CreateSequence("Parallel")
                .AddParallelTasks(
                    () => Console.WriteLine("  Task A"),
                    300,
                    () => Console.WriteLine("  Task B (after 300ms)")
                )
                .Add(() => Console.WriteLine("  All parallel done"))
                .RunAsync();

            // 3. Loop
            Console.WriteLine("\n3. Fixed loop:");
            await TaskSequencer.CreateSequence("Loop")
                .AddLoop(3, () => Console.WriteLine("  Looping!"))
                .RunAsync();

            // 4. Wait while + abort
            Console.WriteLine("\n4. Wait for condition:");
            bool ready = false;
            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                ready = true;
            });

            await TaskSequencer.CreateSequence("WaitFor")
                .WaitFor(() => ready)
                .Add(() => Console.WriteLine("  Condition met!"))
                .RunAsync();

            // 5. OnComplete callback
            Console.WriteLine("\n5. OnComplete callback:");
            await TaskSequencer.CreateSequence("Callback")
                .Add(() => Console.WriteLine("  Working..."))
                .AddDelay(100)
                .OnComplete(() => Console.WriteLine("  Done!"))
                .RunAsync();

            // 6. Smooth transition
            Console.WriteLine("\n6. Smooth value transition:");
            float currentValue = 0f;
            await TaskSequencer.CreateSequence("Transition")
                .AddTransition(
                    () => currentValue,
                    v => { currentValue = v; Console.WriteLine($"  Value: {currentValue:F2}"); },
                    1f,
                    (a, b) => Math.Abs(a - b) < 0.01f,
                    (a, b, t) => a + (b - a) * t,
                    0.1f,
                    16)
                .RunAsync();

            // 7. Serializable tasks
            Console.WriteLine("\n7. Serializable tasks:");
            var delayTask = new Grynsoft.TaskSequencing.Tasks.DelayTask { DurationMs = 200 };
            var actionTask = new Grynsoft.TaskSequencing.Tasks.ActionTask { Action = () => Console.WriteLine("  Action task fired!") };
            var parallelTask = new Grynsoft.TaskSequencing.Tasks.ParallelTask
            {
                ParallelTasks = { delayTask, actionTask }
            };

            await TaskSequencer.CreateSequence("Serializable")
                .AddAsync(() => parallelTask.ExecuteAsync())
                .Add(() => Console.WriteLine("  Serializable tasks complete"))
                .RunAsync();

            Console.WriteLine("\n=== All examples complete ===");
        }
    }
}
