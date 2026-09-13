using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New GamePath", menuName = "GamePath")]
public class GamePath : ScriptableObject
{
    public const string RANDOM_POS = "random";
    public List<GamePosition> positions = new List<GamePosition>();

    [Tooltip("When a transform is assigned here, it overrides individual position transforms")]
    public Transform transformOverride;

    public bool loop;
    private int currentIndex = 0;

    /// Check if the path has a position with the specified ID
    public bool HasPosition(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return positions.Exists(p => p != null && p.id == id);
    }

    /// Get a position by its ID
    public GamePosition GetPosition(string id)
    {
        return GetPosition(id, null);
    }

    /// Get a position by its ID, optionally filtered by character
    public GamePosition GetPosition(string id, string occupantId)
    {
        if (string.IsNullOrEmpty(id)) return null;

        // HANDLE RANDOM POSITION SELECTION
        if (id == RANDOM_POS)
        {
            // If character is specified, filter random selection to that character's positions
            if (!string.IsNullOrEmpty(occupantId))
            {
                var occupantPositions = new List<GamePosition>();
                foreach (var pos in positions)
                {
                    if (pos != null && pos.occupantId == occupantId)
                        occupantPositions.Add(pos);
                }

                if (occupantPositions.Count > 0)
                    return occupantPositions[Random.Range(0, occupantPositions.Count)];

                // No positions found for this character, return null
                return null;
            }

            // SELECT RANDOM POSITION FROM ALL POSITIONS
            if (positions.Count == 0) return null;
            return positions[Random.Range(0, positions.Count)];
        }
        else if (id.StartsWith(RANDOM_POS))
        {
            // EXTRACT CHARACTER NAME AFTER "random"
            string occupantName = id.Substring(RANDOM_POS.Length).Trim();
            if (!string.IsNullOrEmpty(occupantName))
            {
                // GET ALL POSITIONS FOR THIS CHARACTER
                var occupantPositions = new List<GamePosition>();
                foreach (var pos in positions)
                {
                    if (pos != null && !string.IsNullOrEmpty(pos.occupantId) &&
                        pos.occupantId.Equals(occupantName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        occupantPositions.Add(pos);
                    }
                }

                // SELECT RANDOM POSITION FROM CHARACTER'S POSITIONS
                if (occupantPositions.Count > 0)
                    return occupantPositions[Random.Range(0, occupantPositions.Count)];
            }
        }

        // STANDARD ID LOOKUP
        return positions.Find(p => p != null && p.id == id);
    }
    public GamePosition GetPosition(int index)
    {
        if (positions.Count > 0 && (index < 0 || index >= positions.Count)) return positions[0];
        return positions[index];
    }

    public GamePosition GetStart()
    {
        return positions.Count > 0 ? positions[0] : null;
    }

    public GamePosition GetEnd()
    {
        return positions.Count > 0 ? positions[positions.Count - 1] : null;
    }

    public bool GetNextPosition(out GamePosition position)
    {
        position = null;
        if (positions.Count == 0) return false;

        position = positions[currentIndex];
        currentIndex++;

        if (currentIndex >= positions.Count)
        {
            if (loop)
            {
                currentIndex = 0;
                return true;
            }
            currentIndex = positions.Count;
            return false;
        }
        return true;
    }

    public void Reset()
    {
        currentIndex = 0;
    }

    /// Gets the world position of a GamePosition in this path, accounting for transform overrides
    public Vector2 GetWorldPosition(GamePosition position)
    {
        if (position == null) return Vector2.zero;

        // Use path-level transform override if it exists
        return position.GetWorldPosition(transformOverride);
    }
    public Vector2 GetWorldPosition(int index)
    {
        if (positions.Count == 0 || index < 0 || index >= positions.Count) return Vector2.zero;
        return GetWorldPosition(positions[index]);
    }

    /// Gets the world position for a point identified by ID
    public Vector2 GetWorldPosition(string id)
    {
        GamePosition position = GetPosition(id);
        if (position == null) return Vector2.zero;

        return GetWorldPosition(position);
    }

    /// Gets world positions for all points in the path
    public List<Vector2> GetAllWorldPositions()
    {
        List<Vector2> worldPositions = new List<Vector2>();

        if (positions != null)
        {
            foreach (var pos in positions)
            {
                if (pos != null)
                {
                    worldPositions.Add(GetWorldPosition(pos));
                }
            }
        }

        return worldPositions;
    }

    public Vector2 GetWorldPositionAt(int index)
    {
        if (positions != null && index >= 0 && index < positions.Count && positions[index] != null)
        {
            return GetWorldPosition(positions[index]);
        }
        return Vector2.zero;
    }

    /// Gets a position for a specific character by index (0-based)
    public GamePosition GetOccupantPosition(string occupantId, int index)
    {
        if (string.IsNullOrEmpty(occupantId) || positions == null) return null;

        int foundCount = 0;
        foreach (var position in positions)
        {
            if (position != null && position.occupantId == occupantId)
            {
                if (foundCount == index)
                    return position;
                foundCount++;
            }
        }
        return null;
    }

    /// Gets a position for a specific character by index (0-based)
    public Vector2 GetOccupantWorldPosition(string occupantId, int index)
    {
        GamePosition position = GetOccupantPosition(occupantId, index);
        return position != null ? GetWorldPosition(position) : Vector2.zero;
    }
}