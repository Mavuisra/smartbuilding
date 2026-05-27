from django.core.management.base import BaseCommand

from api.sync.materializers import repair_employees_from_sync_store
from api.sync.registry import register_all


class Command(BaseCommand):
    help = (
        "Réapplique le mapping employé (matricule, nom) depuis les données "
        "déjà synchronisées (SyncedEntityStore)."
    )

    def handle(self, *args, **options):
        register_all()
        count = repair_employees_from_sync_store()
        self.stdout.write(self.style.SUCCESS(f"{count} employé(s) mis à jour."))
