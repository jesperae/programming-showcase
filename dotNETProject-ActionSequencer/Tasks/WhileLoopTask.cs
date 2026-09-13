using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing.Tasks
{
    // Task that loops while a condition is true.
    public class WhileLoopTask : SerializableTask
    {
        private Func<bool> _condition;
        private List<SerializableTask> _loopTasks = new List<SerializableTask>();

        public Func<bool> Condition
        {
            get => _condition;
            set => _condition = value;
        }

        public List<SerializableTask> LoopTasks
        {
            get => _loopTasks;
            set => _loopTasks = value ?? new List<SerializableTask>();
        }

        public override string GetDescription()
        {
            int count = _loopTasks?.Count ?? 0;
            return _condition == null
                ? "Loop while condition is true (Not configured)"
                : $"Loop {count} tasks while condition is true";
        }

        public override void AddToSequence(TaskSequence sequence)
        {
            if (_condition == null) return;

            var taskObjects = new List<object>();
            foreach (var task in _loopTasks)
            {
                taskObjects.Add((System.Func<Task>)(() => task.ExecuteAsync()));
            }

            sequence.AddWaitWhile(_condition, taskObjects.ToArray());
        }

        public override async Task ExecuteAsync()
        {
            if (_condition == null) return;

            while (_condition())
            {
                foreach (var task in _loopTasks)
                {
                    await task.ExecuteAsync();
                }
            }
        }
    }
}
