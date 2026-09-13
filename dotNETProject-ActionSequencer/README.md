TaskSequencer
Async Task Sequencing for .NET

This is a fluent, chainable async task sequencing library for C#/.NET. I originally built it as a Unity coroutine system (ActionSequencer) and then converted it to use async/await and Task instead of Unity coroutines.


HOW IT WORKS

The idea is simple: you build a sequence of tasks by chaining method calls together, then run it. Each task runs one after another in order. You can add delays, run things in parallel, loop, wait for conditions, and even abort the whole thing mid-way.

The main class is TaskSequence. You create one using TaskSequencer.CreateSequence(), then chain .Add(), .AddDelay(), .AddParallelTasks(), etc. onto it. When you call .RunAsync(), it executes everything in order.

Behind the scenes, each step is either a simple Action, an async function, or a SerializableTask (which is a polymorphic task type that can be serialized and inspected in editors). The serializable tasks include DelayTask, ParallelTask, WhileLoopTask, FixedLoopTask, AsyncTask, and ActionTask.

There's also an extension system. You can mark methods with [TaskSequenceMethod] and the system will discover them via reflection so they show up in a UI for non-programmers to use.


QUICK EXAMPLE

await TaskSequencer.CreateSequence("MySequence")
    .Add(() => Console.WriteLine("Hello"))
    .AddDelay(1000)
    .Add(() => Console.WriteLine("After 1s"))
    .RunAsync();

Parallel tasks:

await TaskSequencer.CreateSequence()
    .AddParallelTasks(
        () => Console.WriteLine("Task 1"),
        500,
        () => Console.WriteLine("Task 2")
    )
    .Add(() => Console.WriteLine("After parallel"))
    .RunAsync();

Loops:

await TaskSequencer.CreateSequence()
    .AddLoop(3, () => Console.WriteLine("Looping"))
    .RunAsync();

Abort conditions:

await TaskSequencer.CreateSequence()
    .AddAbortCondition(() => gameCancelled, () => Cleanup())
    .Add(() => DoWork())
    .AddDelay(5000)
    .Add(() => DoMoreWork())
    .RunAsync();


MAIN API

TaskSequence methods:
- Add(Action) -- add a synchronous action
- AddAsync(Func<Task>) -- add an async action
- AddDelay(int ms) or AddDelay(float sec) -- add a delay
- AddParallelTasks(params object[]) -- run tasks concurrently, wait for all
- AddWaitWhile(Func<bool>, params object[]) -- loop while condition is true
- AddDoWhile(Func<bool>, params object[]) -- do-while loop (runs at least once)
- AddLoop(int count, params object[]) -- fixed iteration count
- OnComplete(Action) -- callback when sequence finishes
- WaitForSequence(TaskSequence) -- wait for another sequence
- AddAbortCondition(Func<bool>, Action) -- abort and cleanup when condition is true
- RunAsync(Action onComplete) -- start execution, returns Task
- Abort() -- cancel immediately and run cleanup

Static methods:
- TaskSequencer.CreateSequence(name) -- create a new sequence
- TaskSequence.AbortAll() -- abort all running sequences
- TaskSequence.StopSequence(name) -- abort a named sequence
- TaskSequence.RunningCount -- count of active sequences

Extensions:
- WaitFor(Func<bool>) -- wait until condition is true
- AddDoWhile(cond, action, delayMs) -- simplified do-while
- AddTransition(get, set, target, compare, lerp, smooth) -- smooth value transition
- AddTimedTransition(get, set, start, target, duration, lerp) -- fixed-duration transition

Serializable tasks (base class SerializableTask with AddToSequence(), ExecuteAsync(), GetDescription()):
- DelayTask -- configurable delay
- ParallelTask -- run sub-tasks concurrently
- WhileLoopTask -- loop with condition
- FixedLoopTask -- fixed iteration count
- AsyncTask -- wraps a Func<Task>
- ActionTask -- wraps a synchronous Action


ORIGIN

Converted from the Unity ActionSequencer system (Grynsoft 2D Engine). Unity coroutines (IEnumerator + MonoBehaviour.StartCoroutine) were replaced with async/await + Task.Delay. WaitForSecondsRealtime became Task.Delay. UnityEvent became Action.


LICENSE

MIT -- see LICENSE file.
