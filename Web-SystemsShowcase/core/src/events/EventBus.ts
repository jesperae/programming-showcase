// Minimal typed pub/sub event bus. Runs in Node and browsers with zero
// dependencies - every system in this package exposes one of these.

export type EventHandler<T> = (payload: T) => void;

export class EventBus<Events extends object> {
  private readonly handlers = new Map<keyof Events, Set<EventHandler<never>>>();

  // Subscribe. Returns an unsubscribe function.
  on<K extends keyof Events>(event: K, handler: EventHandler<Events[K]>): () => void {
    let set = this.handlers.get(event);
    if (!set) {
      set = new Set();
      this.handlers.set(event, set);
    }
    set.add(handler as EventHandler<never>);
    return () => this.off(event, handler);
  }

  off<K extends keyof Events>(event: K, handler: EventHandler<Events[K]>): void {
    this.handlers.get(event)?.delete(handler as EventHandler<never>);
  }

  emit<K extends keyof Events>(event: K, payload: Events[K]): void {
    this.handlers.get(event)?.forEach((h) => (h as EventHandler<Events[K]>)(payload));
  }

  clear(): void {
    this.handlers.clear();
  }
}
