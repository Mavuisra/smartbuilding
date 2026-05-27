from django.core.management.base import BaseCommand

from api.models import SyncedEntityStore
from api.sync import materializers
from api.sync.registry import register_all


class Command(BaseCommand):
    help = (
        "Réapplique le mapping employé (matricule, nom) depuis les données "
        "déjà synchronisées (SyncedEntityStore)."
    )

    def handle(self, *args, **options):
        register_all()
        count = 0
        for store in SyncedEntityStore.objects.filter(entity_type="Employees"):
            if not store.json_data:
                continue
            materializers.materialize_employee(store.json_data)
            count += 1
        self.stdout.write(self.style.SUCCESS(f"{count} employé(s) mis à jour."))
