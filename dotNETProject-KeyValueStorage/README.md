KeyValueStorage
Simple Persistence for .NET

This is a lightweight key-value storage system with file persistence, structured save/load via ISaveable, and event hooks. I converted it from the Unity SaveData system, replacing PlayerPrefs and JsonUtility with a Dictionary and System.Text.Json.


HOW IT WORKS

At its simplest, you just save and load values by string key. Behind the scenes everything lives in an in-memory Dictionary. When you want to persist to disk, you call SaveToFile() which serializes the whole dictionary to JSON.

For more structured data, you can implement the ISaveable interface on your classes. This gives you SaveToData() and LoadFromData() methods so each class can manage its own serialization logic. You might store a list of player profiles, each one finding itself by ID.

There are also events you can hook into: OnLoadComplete fires after loading from file, OnBeforeSave and OnSaveComplete fire around saving to file, and OnReady fires when the storage is initialized.

A SerializableVector2 struct is included for storing 2D positions in JSON, since Unity's Vector2 doesn't serialize natively in System.Text.Json.


QUICK EXAMPLE

using Grynsoft.KeyValueStorage;

// Simple key-value
KeyValueStorage.Save("score", 1000);
var score = KeyValueStorage.Load("score", 0);

// File persistence
KeyValueStorage.SetFilePath("gamedata.json");
KeyValueStorage.SaveToFile();
KeyValueStorage.LoadFromFile();

// Structured save with ISaveable
public class PlayerData : ISaveable
{
    public string Id { get; set; }
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
}

// Events
KeyValueStorage.OnLoadComplete += () => Console.WriteLine("Loaded!");
KeyValueStorage.OnBeforeSave += () => Console.WriteLine("Saving...");
KeyValueStorage.OnSaveComplete += () => Console.WriteLine("Saved!");


MAIN API

Methods:
- Save<T>(key, value) -- store a value
- Load<T>(key, default) -- retrieve a value
- TryLoad<T>(key, out value) -- try to retrieve
- HasKey(key) -- check existence
- DeleteKey(key) -- remove a key
- Clear() -- remove all data
- GetKeys() -- get all keys
- SaveToFile() -- persist to disk
- LoadFromFile() -- load from disk
- SetFilePath(path) -- set persistence file
- Reset() -- clear all state and events

Events:
- OnLoadComplete -- after LoadFromFile() completes
- OnBeforeSave -- before SaveToFile() writes
- OnSaveComplete -- after SaveToFile() writes
- OnReady -- storage is initialized and ready

ISaveable interface:
- string GetSaveKey()
- string GetDisplayName()
- void SaveToData(string id)
- void LoadFromData(string id)


ORIGIN

Converted from Unity SaveData system. PlayerPrefs became an in-memory Dictionary. JsonUtility became System.Text.Json. SerializableVector2 was preserved with a custom JSON converter.


LICENSE

MIT -- see LICENSE file.
