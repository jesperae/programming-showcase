using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing.Tasks
{
    // Task that loops a fixed number of times.
    public class FixedLoopTask : SerializableTask
    {
        private int _count = 1;
        private List<SerializableTask> _loopTasks = new List<SerializableTask>();

        public int Count
        {
            get => _count;
            set => _count = value;
        }

        public List<SerializableTask> LoopTasks
        {
            get => _loopTasks;
            set => _loopTasks = value ?? new List<SerializableTask>();
        }

        public override string GetDescription()
        {
            int taskCount = _loopTasks?.Count ?? 0;
            return $"Loop {taskCount} tasks {_count} time(s)";
        }

        public override void AddToSequence(TaskSequence sequence)
        {
            var taskObjects = new List<object>();
            foreach (var task in _loopTasks)
            {
                taskObjects.Add((System.Func<Task>)(() => task.ExecuteAsync()));
            }
            sequence.AddLoop(_count, taskObjects.ToArray());
        }

        public override async Task ExecuteAsync()
        {
            for (int i = 0; i < _count; i++)
            {
                foreach (var task in _loopTasks)
                {
                    await task.ExecuteAsync();
                }
            }
        }
    }
}
