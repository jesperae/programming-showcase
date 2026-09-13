using UnityEngine;

[System.Serializable]
public class GamePosition
{
	[Tooltip("Position in world space or relative to a parent transform")]
	public Vector2 position;

	[Tooltip("The transform this position is relative to (null = world space)")]
	public Transform relativeTransform;

	[Tooltip("Optional occupant/owner ID - lets you filter positions by who or what they belong to")]
	public string occupantId;

	[Tooltip("Unique ID for quick lookup; multiple points with same ID allow random selection")]
	public string id;

	[HideInInspector]
	public bool isEditing = false;


	/// Gets the position in world space, considering transform hierarchies

	/// <param name="overrideTransform">Optional override transform that takes precedence over the position's relativeTransform</param>
	/// <returns>The position in world space</returns>
	public Vector2 GetWorldPosition(Transform overrideTransform = null)
	{
		// Use override transform if provided, otherwise use the position's own transform reference
		Transform parentTransform = overrideTransform != null ? overrideTransform : relativeTransform;

		// Convert local position to world space if we have a transform reference
		return parentTransform != null ? (Vector2)parentTransform.TransformPoint(position) : position;
	}


	/// Returns a string representation of this GamePosition

	/// <returns>A formatted string with position details</returns>
	public override string ToString()
	{
		string posType = relativeTransform != null ? "local" : "world";
		string occupant = !string.IsNullOrEmpty(occupantId) ? occupantId : "none";
		string posId = !string.IsNullOrEmpty(id) ? id : "unnamed";

		return $"[GamePos: {posId} ({position.x:F2}/{position.y:F2}) {posType}" +
			   (relativeTransform != null ? $" -> {relativeTransform.name}" : "") +
			   $" occupant: {occupant}]";
	}
}
