using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing.Tasks
{
    // Task that runs multiple tasks in parallel.
    public class ParallelTask : SerializableTask
    {
        private List<SerializableTask> _parallelTasks = new List<SerializableTask>();

        public List<SerializableTask> ParallelTasks
        {
            get => _parallelTasks;
            set => _parallelTasks = value ?? new List<SerializableTask>();
        }

        public override string GetDescription()
        {
            int count = _parallelTasks?.Count ?? 0;
            return count == 0 ? "Run tasks in parallel (Empty)" : $"Run {count} tasks in parallel";
        }

        public override void AddToSequence(TaskSequence sequence)
        {
            // Build task objects for parallel execution
            var tasks = new List<object>();
            foreach (var task in _parallelTasks)
            {
                tasks.Add((System.Func<Task>)(() => task.ExecuteAsync()));
            }
            sequence.AddParallelTasks(tasks.ToArray());
        }

        public override async Task ExecuteAsync()
        {
            var running = new List<Task>();
            foreach (var task in _parallelTasks)
            {
                running.Add(task.ExecuteAsync());
            }
            await Task.WhenAll(running);
        }
    }
}
