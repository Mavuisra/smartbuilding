"""Supprime les écritures financières en double (même loyer / même référence)."""

from django.core.management.base import BaseCommand
from django.db import transaction

from api.models import FinancialTransaction, SyncedEntityStore
from api.services.finance_ledger import dedupe_financial_transactions, financial_dedupe_key


class Command(BaseCommand):
    help = "Conserve une seule FinancialTransaction par clé métier (description, montant, date…)"

    def add_arguments(self, parser):
        parser.add_argument(
            "--dry-run",
            action="store_true",
            help="Affiche le nombre de doublons sans supprimer",
        )

    @transaction.atomic
    def handle(self, *args, **options):
        dry = options["dry_run"]
        txs = list(
            FinancialTransaction.objects.filter(deleted_at__isnull=True).order_by(
                "-updated_at"
            )
        )
        best: dict = {}
        for tx in txs:
            key = financial_dedupe_key(
                reference=tx.reference or "",
                description=tx.description or "",
                amount=tx.amount,
                transaction_date=tx.transaction_date,
                tx_type=tx.type,
            )
            if key not in best:
                best[key] = tx

        to_delete = [t for t in txs if t.id not in {b.id for b in best.values()}]
        self.stdout.write(
            f"ORM : {len(txs)} écriture(s), {len(to_delete)} doublon(s) à retirer."
        )

        if not dry and to_delete:
            for tx in to_delete:
                tx.delete()

        store_qs = SyncedEntityStore.objects.filter(
            entity_type="FinancialTransactions", deleted_at__isnull=True
        )
        store_rows = list(store_qs)
        store_best: dict = {}
        for row in store_rows:
            data = row.json_data if isinstance(row.json_data, dict) else {}
            key = financial_dedupe_key(
                reference=str(data.get("Reference") or data.get("reference") or ""),
                description=str(
                    data.get("Description") or data.get("description") or ""
                ),
                amount=data.get("Amount") or data.get("amount") or 0,
                transaction_date=data.get("TransactionDate")
                or data.get("transactionDate"),
                tx_type=data.get("Type") or data.get("type") or 1,
                related_entity_id=str(
                    data.get("RelatedEntityId") or data.get("relatedEntityId") or ""
                )
                or None,
            )
            prev = store_best.get(key)
            if prev is None or row.updated_at >= prev.updated_at:
                store_best[key] = row

        store_delete = [
            r for r in store_rows if r.id not in {b.id for b in store_best.values()}
        ]
        self.stdout.write(
            f"Magasin sync : {len(store_rows)} ligne(s), "
            f"{len(store_delete)} doublon(s) à retirer."
        )

        if not dry and store_delete:
            for row in store_delete:
                row.delete()

        if dry:
            self.stdout.write(self.style.WARNING("Mode dry-run — aucune suppression."))
        else:
            self.stdout.write(self.style.SUCCESS("Déduplication terminée."))
