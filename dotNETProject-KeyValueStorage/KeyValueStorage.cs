using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Grynsoft.KeyValueStorage
{
    // Interface for objects that can save/load themselves through the KeyValueStorage system.
    public interface ISaveable
    {
        // Unique key used to store/load this object's data.
        string GetSaveKey();

        // Display name for debugging/logging.
        string GetDisplayName();

        // Save this object's data under the given id.
        void SaveToData(string id);

        // Load this object's data from the given id.
        void LoadFromData(string id);
    }

    // Static key-value storage system with file persistence and structured save/load via ISaveable.
    public static class KeyValueStorage
    {
        private static readonly Dictionary<string, string> _data = new Dictionary<string, string>();
        private static readonly object _lock = new object();
        private static string _filePath = "savedata.json";

        // Raised after all data has been loaded from file.
        public static event Action? OnLoadComplete;

        // Raised before data is saved to file.
        public static event Action? OnBeforeSave;

        // Raised after data has been saved to file.
        public static event Action? OnSaveComplete;

        // Raised when the storage is fully ready for use.
        public static event Action? OnReady;

        // True after initial load is complete.
        public static bool IsReady { get; private set; }

        // Sets the file path for persistence.
        public static void SetFilePath(string path)
        {
            _filePath = path;
        }

        // Saves a value under the given key.
        public static void Save<T>(string key, T value)
        {
            lock (_lock)
            {
                _data[key] = JsonSerializer.Serialize(value, GetJsonOptions());
            }
        }

        // Loads a value by key, returning default if not found.
        public static T Load<T>(string key, T defaultValue = default!)
        {
            lock (_lock)
            {
                if (!_data.TryGetValue(key, out var json))
                    return defaultValue;
                return JsonSerializer.Deserialize<T>(json, GetJsonOptions()) ?? defaultValue;
            }
        }

        // Loads a value by key, returning true if found.
        public static bool TryLoad<T>(string key, out T value)
        {
            lock (_lock)
            {
                if (_data.TryGetValue(key, out var json))
                {
                    value = JsonSerializer.Deserialize<T>(json, GetJsonOptions())!;
                    return true;
                }
                value = default!;
                return false;
            }
        }

        // Checks if a key exists.
        public static bool HasKey(string key)
        {
            lock (_lock) return _data.ContainsKey(key);
        }

        // Deletes a key.
        public static void DeleteKey(string key)
        {
            lock (_lock) _data.Remove(key);
        }

        // Clears all data.
        public static void Clear()
        {
            lock (_lock) _data.Clear();
        }

        // Gets all keys.
        public static IReadOnlyCollection<string> GetKeys()
        {
            lock (_lock) return _data.Keys.ToArray();
        }

        // Persists all data to file.
        public static void SaveToFile()
        {
            OnBeforeSave?.Invoke();

            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_data, GetJsonOptions());
                File.WriteAllText(_filePath, json);
            }

            OnSaveComplete?.Invoke();
        }

        // Loads data from file.
        public static void LoadFromFile()
        {
            if (!File.Exists(_filePath))
            {
                IsReady = true;
                OnReady?.Invoke();
                OnLoadComplete?.Invoke();
                return;
            }

            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json, GetJsonOptions());

            lock (_lock)
            {
                _data.Clear();
                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                        _data[kvp.Key] = kvp.Value;
                }
            }

            IsReady = true;
            OnReady?.Invoke();
            OnLoadComplete?.Invoke();
        }

        // Resets all state (for testing or reinitialization).
        public static void Reset()
        {
            lock (_lock)
            {
                _data.Clear();
                IsReady = false;
                OnLoadComplete = null;
                OnBeforeSave = null;
                OnSaveComplete = null;
                OnReady = null;
            }
        }

        private static JsonSerializerOptions GetJsonOptions() => new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = true,
            Converters = { new SerializableVector2Converter() }
        };
    }

    // Serializable alternative to Vector2 for cross-platform storage.
    [Serializable]
    public struct SerializableVector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public SerializableVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static SerializableVector2 Zero => new SerializableVector2(0, 0);

        public float Magnitude => MathF.Sqrt(X * X + Y * Y);

        public static SerializableVector2 operator +(SerializableVector2 a, SerializableVector2 b)
            => new SerializableVector2(a.X + b.X, a.Y + b.Y);

        public static SerializableVector2 operator -(SerializableVector2 a, SerializableVector2 b)
            => new SerializableVector2(a.X - b.X, a.Y - b.Y);

        public static SerializableVector2 operator *(SerializableVector2 a, float scalar)
            => new SerializableVector2(a.X * scalar, a.Y * scalar);

        public override string ToString() => $"({X}, {Y})";
    }

    // JSON converter for SerializableVector2.
    public class SerializableVector2Converter : System.Text.Json.Serialization.JsonConverter<SerializableVector2>
    {
        public override SerializableVector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for SerializableVector2");
            float x = 0, y = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var name = reader.GetString();
                    reader.Read();
                    if (name == "X" || name == "x") x = reader.GetSingle();
                    else if (name == "Y" || name == "y") y = reader.GetSingle();
                }
            }
            return new SerializableVector2(x, y);
        }

        public override void Write(Utf8JsonWriter writer, SerializableVector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }
}
