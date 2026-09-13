using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SimpleMenuOption
{
    public string Text;
    public string Title;
    [TextArea] public string Description;
    public Action OnSelected;
    public Func<bool> IsEnabled;

    /// Fired when this option becomes/stops being the highlighted choice (e.g. to show/revert a preview)
    public Action OnHoverEnter;
    public Action OnHoverExit;

    public bool CanSelect => IsEnabled == null || IsEnabled();

    /// Returns the text to display for this option. Override to provide dynamic text (e.g. live cooldown).
    public virtual string GetDisplayText() => Text;

    public virtual string GetTitle() => Title;
    public virtual string GetDescription() => Description;

    public SimpleMenuOption() { }

    public SimpleMenuOption(string text, Action onSelected, Func<bool> isEnabled = null)
    {
        Text = text;
        OnSelected = onSelected;
        IsEnabled = isEnabled;
    }

    public SimpleMenuOption(string text, string title, string description, Action onSelected, Func<bool> isEnabled = null)
    {
        Text = text;
        Title = title;
        Description = description;
        OnSelected = onSelected;
        IsEnabled = isEnabled;
    }

    public override string ToString() => GetDisplayText();
}

/// A menu option that groups other options into a sub-menu. Selecting it opens its
/// Children as a nested list (with an automatic "Back" entry) instead of firing an action.
public class SimpleMenuOptionGroup : SimpleMenuOption
{
    public List<SimpleMenuOption> Children = new List<SimpleMenuOption>();

    public SimpleMenuOptionGroup(string text) : base(text, null) { }

    public override string GetDisplayText() => Text + " >";
}

/// SimpleMenuOption with a live cooldown display (e.g. rate-limited actions, retry timers).
/// Shows "Text (3.2s)" while cooling down and cannot be selected until it elapses.
public class CooldownOption : SimpleMenuOption
{
    public Func<bool> IsOnCooldown;
    public Func<float> RemainingTime;

    public CooldownOption(string text, Func<bool> isOnCooldown, Func<float> remainingTime, Action onSelected)
    {
        Text = text;
        Title = text;
        IsOnCooldown = isOnCooldown;
        RemainingTime = remainingTime;
        OnSelected = onSelected;
        IsEnabled = () => IsOnCooldown == null || !IsOnCooldown();
    }

    public override string GetDisplayText()
    {
        if (IsOnCooldown != null && IsOnCooldown() && RemainingTime != null)
            return $"{Text} ({RemainingTime():0.0}s)";
        return Text;
    }
}
