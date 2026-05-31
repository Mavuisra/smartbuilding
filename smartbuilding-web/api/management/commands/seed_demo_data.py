"""Données de démonstration pour le tableau de bord local (sans desktop)."""

from datetime import date
from decimal import Decimal
import uuid

from django.core.management.base import BaseCommand
from django.utils import timezone

from api.models import (
    Employee,
    FinancialTransaction,
    LeaseContract,
    Premise,
    RentPayment,
    Tenant,
)


class Command(BaseCommand):
    help = "Insère un jeu minimal de données pour tester le portail web en local"

    def handle(self, *args, **options):
        today = timezone.localdate()
        y, m = today.year, today.month

        tenant, _ = Tenant.objects.get_or_create(
            id=uuid.UUID("11111111-1111-1111-1111-111111111101"),
            defaults={
                "name": "Jean Mukendi",
                "email": "jean.mukendi@example.cd",
                "phone": "+243 81 000 0001",
                "rental_status": "Actif",
            },
        )
        premise, _ = Premise.objects.get_or_create(
            id=uuid.UUID("22222222-2222-2222-2222-222222222202"),
            defaults={
                "code": "A-101",
                "name": "Appartement 101",
                "building_name": "Tour SBMS",
                "floor": "1",
                "monthly_rent": Decimal("850.00"),
                "is_occupied": True,
            },
        )
        contract, _ = LeaseContract.objects.get_or_create(
            id=uuid.UUID("33333333-3333-3333-3333-333333333303"),
            defaults={
                "tenant": tenant,
                "premise": premise,
                "contract_number": "CT-DEMO-001",
                "start_date": date(y, 1, 1),
                "monthly_rent": Decimal("850.00"),
                "deposit": Decimal("1700.00"),
                "status": "Actif",
            },
        )
        RentPayment.objects.update_or_create(
            id=uuid.UUID("44444444-4444-4444-4444-444444444404"),
            defaults={
                "lease_contract": contract,
                "year": y,
                "month": m,
                "amount_due": Decimal("850.00"),
                "amount_paid": Decimal("850.00"),
                "payment_status": "Payé",
                "is_late": False,
            },
        )
        Employee.objects.get_or_create(
            id=uuid.UUID("55555555-5555-5555-5555-555555555505"),
            defaults={
                "employee_number": "EMP-001",
                "full_name": "Marie Kabila",
                "position": "Gestionnaire",
                "department": "Location",
                "is_active": True,
                "monthly_salary": Decimal("1200.00"),
            },
        )
        FinancialTransaction.objects.get_or_create(
            id=uuid.UUID("66666666-6666-6666-6666-666666666606"),
            defaults={
                "type": FinancialTransaction.TxType.DEPENSE,
                "category": "Maintenance",
                "description": "Réparation ascenseur (démo — validation PDG)",
                "amount": Decimal("320.00"),
                "status": "En attente validation PDG",
                "recorded_by": "Comptable",
                "requires_pdg_approval": True,
            },
        )
        self.stdout.write(self.style.SUCCESS("Données démo insérées — rechargez le tableau de bord."))
