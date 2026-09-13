using System;
using System.Collections.Generic;
using Grynsoft.KeyValueStorage;

namespace KeyValueStorageExample
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("=== KeyValueStorage Examples ===\n");

            // 1. Simple key-value
            Console.WriteLine("1. Simple key-value:");
            KeyValueStorage.Save("score", 1000);
            KeyValueStorage.Save("playerName", "Gryn");
            KeyValueStorage.Save("isComplete", true);

            var score = KeyValueStorage.Load("score", 0);
            var name = KeyValueStorage.Load("playerName", "Unknown");
            var isComplete = KeyValueStorage.Load("isComplete", false);

            Console.WriteLine($"  score={score}, name={name}, isComplete={isComplete}");

            // 2. Check and delete
            Console.WriteLine("\n2. HasKey and DeleteKey:");
            Console.WriteLine($"  Has 'score': {KeyValueStorage.HasKey("score")}");
            KeyValueStorage.DeleteKey("score");
            Console.WriteLine($"  Has 'score' after delete: {KeyValueStorage.HasKey("score")}");

            // 3. Structured save with ISaveable
            Console.WriteLine("\n3. Structured save with ISaveable:");
            var player1 = new PlayerData { Id = "p1", Level = 5, Health = 80f };
            var player2 = new PlayerData { Id = "p2", Level = 12, Health = 100f };
            player1.SaveToData("p1");
            player2.SaveToData("p2");

            var loaded = PlayerData.Get("p1");
            Console.WriteLine($"  Loaded p1: Level={loaded.Level}, Health={loaded.Health}");

            // 4. SerializableVector2
            Console.WriteLine("\n4. SerializableVector2:");
            KeyValueStorage.Save("position", new SerializableVector2(3.5f, 7.2f));
            var pos = KeyValueStorage.Load("position", SerializableVector2.Zero);
            Console.WriteLine($"  Position: {pos}");

            // 5. File persistence
            Console.WriteLine("\n5. File persistence:");
            KeyValueStorage.SetFilePath("example_save.json");
            KeyValueStorage.SaveToFile();
            Console.WriteLine("  Saved to file.");

            KeyValueStorage.Clear();
            Console.WriteLine($"  After clear, score exists: {KeyValueStorage.HasKey("playerName")}");

            KeyValueStorage.LoadFromFile();
            var restoredName = KeyValueStorage.Load("playerName", "Unknown");
            Console.WriteLine($"  After load from file, playerName={restoredName}");

            // 6. Events
            Console.WriteLine("\n6. Events:");
            KeyValueStorage.Reset();
            KeyValueStorage.OnLoadComplete += () => Console.WriteLine("  -> Load complete event fired!");
            KeyValueStorage.OnBeforeSave += () => Console.WriteLine("  -> Before save event fired!");
            KeyValueStorage.OnSaveComplete += () => Console.WriteLine("  -> Save complete event fired!");

            KeyValueStorage.Save("test", 42);
            KeyValueStorage.SaveToFile();
            KeyValueStorage.LoadFromFile();

            // 7. TryLoad
            Console.WriteLine("\n7. TryLoad:");
            if (KeyValueStorage.TryLoad("test", out int testValue))
                Console.WriteLine($"  Found test={testValue}");
            if (!KeyValueStorage.TryLoad("missing", out int missing))
                Console.WriteLine("  'missing' key not found (expected)");

            Console.WriteLine("\n=== All examples complete ===");
        }
    }

    // Example ISaveable implementation
    public class PlayerData : ISaveable
    {
        public string Id { get; set; } = "";
        public int Level { get; set; }
        public float Health { get; set; }

        private const string SAVE_KEY = "PlayerData";

        public string GetSaveKey() => SAVE_KEY;
        public string GetDisplayName() => Id;

        public void SaveToData(string id)
        {
            var list = KeyValueStorage.Load(SAVE_KEY, new List<PlayerData>());
            list.RemoveAll(x => x.Id == Id);
            list.Add(this);
            KeyValueStorage.Save(SAVE_KEY, list);
        }

        public void LoadFromData(string id)
        {
            var list = KeyValueStorage.Load(SAVE_KEY, new List<PlayerData>());
            var found = list.Find(x => x.Id == id);
            if (found != null) { Level = found.Level; Health = found.Health; }
        }

        public static PlayerData Get(string id)
        {
            var list = KeyValueStorage.Load(SAVE_KEY, new List<PlayerData>());
            return list.Find(x => x.Id == id) ?? new PlayerData { Id = id };
        }
    }
}
