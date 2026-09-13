using System;
using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing.Tasks
{
    // Base class for serializable tasks in a TaskSequence.
    public abstract class SerializableTask
    {
        // Adds this task to a TaskSequence.
        public abstract void AddToSequence(TaskSequence sequence);

        // Executes the task directly as an async operation.
        public abstract Task ExecuteAsync();

        // Gets a human-readable description of this task for display.
        public virtual string GetDescription()
        {
            string typeName = GetType().Name;
            if (typeName.EndsWith("Task"))
                typeName = typeName.Substring(0, typeName.Length - 4);
            return typeName;
        }

        // Validates the task during serialization.
        public virtual void OnValidate() { }
    }
}
