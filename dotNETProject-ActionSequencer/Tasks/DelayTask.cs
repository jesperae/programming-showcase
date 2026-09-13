using System.Threading.Tasks;

namespace Grynsoft.TaskSequencing.Tasks
{
    // Task that adds a delay.
    public class DelayTask : SerializableTask
    {
        private int _durationMs = 1000;

        public int DurationMs
        {
            get => _durationMs;
            set => _durationMs = value;
        }

        public override string GetDescription() => $"Wait for {_durationMs} ms";

        public override void AddToSequence(TaskSequence sequence)
        {
            sequence.AddDelay(_durationMs);
        }

        public override async Task ExecuteAsync()
        {
            await Task.Delay(_durationMs);
        }
    }
}
