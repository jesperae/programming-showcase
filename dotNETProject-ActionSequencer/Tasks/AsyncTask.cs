using System;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing.Tasks
{
    // Task that invokes an async function.
    public class AsyncTask : SerializableTask
    {
        private Func<Task> _asyncAction;

        public Func<Task> AsyncAction
        {
            get => _asyncAction;
            set => _asyncAction = value;
        }

        public override string GetDescription() => "Run async action";

        public override void AddToSequence(TaskSequence sequence)
        {
            if (_asyncAction != null)
                sequence.AddAsync(_asyncAction);
        }

        public override async Task ExecuteAsync()
        {
            if (_asyncAction != null)
                await _asyncAction();
        }
    }
}
