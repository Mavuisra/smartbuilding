"""Re-matérialise les tables ORM à partir de SyncedEntityStore (après push desktop)."""

import json

from django.core.management.base import BaseCommand

from api.models import SyncedEntityStore
from api.sync.registry import get_registered_handlers
from api.sync.utils import inject_entity_id


class Command(BaseCommand):
    help = "Applique les handlers de sync sur toutes les entités du magasin JSON"

    def add_arguments(self, parser):
        parser.add_argument(
            "--entity-type",
            type=str,
            default="",
            help="Limiter à un type (ex: Premises)",
        )

    def handle(self, *args, **options):
        handlers = get_registered_handlers()
        entity_filter = (options.get("entity_type") or "").strip()
        qs = SyncedEntityStore.objects.filter(deleted_at__isnull=True)
        if entity_filter:
            qs = qs.filter(entity_type=entity_filter)

        applied = 0
        skipped = 0
        for row in qs.iterator():
            handler = handlers.get(row.entity_type)
            if not handler:
                skipped += 1
                continue
            data = row.json_data if isinstance(row.json_data, dict) else {}
            if not data:
                try:
                    data = json.loads("{}")
                except json.JSONDecodeError:
                    skipped += 1
                    continue
            handler(inject_entity_id(data, row.id))
            applied += 1

        self.stdout.write(
            self.style.SUCCESS(
                f"Matérialisation terminée : {applied} entité(s), {skipped} ignorée(s)."
            )
        )
