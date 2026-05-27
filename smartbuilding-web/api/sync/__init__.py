def __getattr__(name):
    if name in ("apply_push", "get_changes_since", "is_syncable"):
        from api.sync import registry

        return getattr(registry, name)
    raise AttributeError(name)

__all__ = ["apply_push", "get_changes_since", "is_syncable"]
