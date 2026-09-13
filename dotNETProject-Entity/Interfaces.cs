using System;
using System.Collections.Generic;

namespace Grynsoft.EntitySystem
{
    // Interface for objects that need to reset their state (e.g., on player respawn).
    public interface IResettable
    {
        void ResetObject();
    }

    // Interface for entities that have a position in space.
    public interface IPositioned
    {
        float X { get; }
        float Y { get; }
        float Z { get; }
    }

    // Interface for entities that can be activated/deactivated.
    public interface IActivatable
    {
        bool IsActive { get; }
        void Activate();
        void Deactivate();
    }

    // Interface for entities that have a faction/team.
    public interface IFactionMember
    {
        string Faction { get; }
    }

    // Interface for entities that can be tagged.
    public interface ITaggable
    {
        IReadOnlyList<string> Tags { get; }
        bool HasTag(string tag);
    }
}
