using System;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing.Tasks
{
    // Task that invokes a synchronous action.
    public class ActionTask : SerializableTask
    {
        private Action _action;

        public Action Action
        {
            get => _action;
            set => _action = value;
        }

        public override string GetDescription() => "Run action";

        public override void AddToSequence(TaskSequence sequence)
        {
            if (_action != null)
                sequence.Add(_action);
        }

        public override Task ExecuteAsync()
        {
            _action?.Invoke();
            return Task.CompletedTask;
        }
    }
}
