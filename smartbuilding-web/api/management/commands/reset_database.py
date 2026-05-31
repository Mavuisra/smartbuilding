from django.core.management.base import BaseCommand

from api.services.database_reset import CONFIRM_PHRASE, reset_application_database


class Command(BaseCommand):
    help = "Réinitialise toutes les données SBMS (comptes admin/pdg recréés)."

    def add_arguments(self, parser):
        parser.add_argument(
            "--confirm",
            type=str,
            default="",
            help=f'Phrase obligatoire : "{CONFIRM_PHRASE}"',
        )
        parser.add_argument(
            "--no-reseed",
            action="store_true",
            help="Ne pas recréer admin/pdg après purge",
        )

    def handle(self, *args, **options):
        if options["confirm"].strip() != CONFIRM_PHRASE:
            self.stderr.write(
                self.style.ERROR(f'Utilisez --confirm "{CONFIRM_PHRASE}"')
            )
            return
        result = reset_application_database(reseed_accounts=not options["no_reseed"])
        self.stdout.write(
            self.style.SUCCESS(
                f"Base réinitialisée ({result['engine']}) — "
                f"{result['deletedRecords']} enregistrement(s) supprimés."
            )
        )
