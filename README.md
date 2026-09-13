Programming Showcase

Welcome to my showcase!

This repository is a curated collection of systems I have built, organized by technology. Each folder is a self-contained project with its own README explaining how it works, usage examples, and file listings.


UNITY (C# Game Systems)

- Unity-GamePath -- waypoint path movement framework. ScriptableObject paths, four loop modes, and pluggable movement adapters via IMovementAdapter + IPathMoverTarget hooks.
- Unity-MenuSystem -- complete UI menu navigation and layout framework. Cursor-driven navigation, many option types, programmatic sub-menus, animation queues, and geometric layout shapes.


.NET (C# Libraries)

- dotNETProject-ActionSequencer -- fluent async task sequencing library. Chainable tasks, delays, parallel execution, loops, wait-for-condition, and abort support. Converted from a Unity coroutine system.
- dotNETProject-Entity -- lightweight entity registry and lifecycle system. Static and scoped registries, type-cached lookups, spatial queries, component-style storage, and tags.
- dotNETProject-KeyValueStorage -- simple key-value persistence with JSON file save/load, an ISaveable interface for structured data, and event hooks.
- dotNETProject-RuleEngine -- data-driven IF/THEN/ELSE rule engine with extensible conditions, AND/OR logic, target capture, and edge detection.


PHP (Business Operations)

- PHP-infrastructure-operations-suite -- four small examples of business operations systems built with PHP, JavaScript, jQuery, and action-based AJAX: a configuration analyzer, asset reconciliation, an asset batch manager, and a shared operations toolkit.


TYPESCRIPT (Web)

- Web-SystemsShowcase -- small reusable systems in TypeScript (job tracking, asset store, configuration, KPI tracking, condition evaluation): a shared framework-agnostic core, an Express REST API with a live event stream, and React + Angular dashboards.


Each project folder contains its own README with full details.


HIGH-LEVEL API EXAMPLES

The point of these systems: a lot of work in very few lines.

ActionSequencer -- order processing pipeline:

```csharp
await TaskSequencer.Create()
    .Add(() => order.Validate())
    .AddAsync(() => payment.ChargeAsync(order.Total))
    .AddIf(() => order.IsInternational, () => customs.FileDeclaration(order))
    .AddParallel(
        () => warehouse.ReserveStock(order),
        () => notifier.EmailCustomer(order, "confirmed"))
    .AddWaitUntil(() => payment.IsSettled)
    .Add(() => order.MarkFulfilled())
    .OnError(() => payment.Refund(order))
    .RunAsync();
```

Entity -- fleet/asset registry:

```csharp
// Nearest available certified technician to a job site
var tech = Entity.FindNearest<Technician>(jobSite.Location,
    filter: t => t.HasTag("available") && t.GetComponent<Certification>().Level >= 3);

// All equipment due for maintenance in a region
var due = Entity.FindInCircle<Equipment>(region.Center, region.RadiusKm)
    .Where(e => e.GetComponent<ServiceRecord>().IsOverdue);
```

KeyValueStorage -- preferences / session state:

```csharp
storage.Set("theme", "dark");
storage.Set("lastReport", reportId);
storage.SaveToFile("prefs.json");   // persists as JSON

// Structured save via ISaveable - auto-saves on SaveToFile
storage.Register(dashboardLayout);
```

RuleEngine -- approval workflow:

```csharp
var rules = new RuleEngine<Expense>()
    .When(e => e.Amount < 500).Then(e => e.Approve("auto"))
    .When(e => e.Amount < 5000 && e.HasReceipts).Then(e => e.RouteTo("manager"))
    .Otherwise().Then(e => e.RouteTo("director"));

rules.Evaluate(expense);   // declarative, data-driven
```

MenuSystem -- approval dashboard with queued reveals:

```csharp
// When a request is approved, its node pops in the menu automatically
MenuAutomation.QueueNodeReveal("request-1042");

// A rate-limited action shows a live countdown
menu.AddOption(new CooldownOption("Resend Invoice",
    isOnCooldown: () => mailer.OnCooldown,
    remainingTime: () => mailer.SecondsLeft,
    onSelected: () => mailer.Resend(invoice)));
```

GamePath -- guided onboarding tour / delivery route:

```csharp
var tour = new GamePathMover {
    Path = onboardingPath,            // waypoints: each widget to highlight
    Mode = LoopMode.Once,
    ReleaseAt = ReleaseTiming.OnEnd   // "drop off" at the last stop
};
tour.OnReachedEnd += () => ShowCompletionBadge();
tour.Initialize(highlightTransform);
tour.StartMoving();                 // call tour.Update() each FixedUpdate
```
