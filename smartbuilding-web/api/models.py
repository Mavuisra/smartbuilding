import uuid

from django.contrib.auth.models import AbstractBaseUser, BaseUserManager, PermissionsMixin
from django.db import models
from django.utils import timezone


class BaseEntity(models.Model):
    """Aligné sur SmartBuilding.Domain.Common.BaseEntity."""

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    created_at = models.DateTimeField(default=timezone.now)
    updated_at = models.DateTimeField(default=timezone.now)
    is_synced = models.BooleanField(default=True)
    deleted_at = models.DateTimeField(null=True, blank=True)

    class Meta:
        abstract = True

    @property
    def is_deleted(self):
        return self.deleted_at is not None


class UserManager(BaseUserManager):
    def create_user(self, username, password=None, **extra_fields):
        if not username:
            raise ValueError("username requis")
        user = self.model(username=username, **extra_fields)
        if password:
            user.set_password(password)
        user.save(using=self._db)
        return user

    def create_superuser(self, username, password=None, **extra_fields):
        extra_fields.setdefault("is_staff", True)
        extra_fields.setdefault("is_superuser", True)
        extra_fields.setdefault("role", User.Role.ADMIN)
        return self.create_user(username, password, **extra_fields)


class User(AbstractBaseUser, PermissionsMixin):
    """Utilisateurs desktop + portail PDG (UUID aligné sync desktop)."""

    class Role(models.TextChoices):
        ADMIN = "Administrateur", "Administrateur"
        PDG = "PDG", "PDG"
        COMPTABLE = "Comptable", "Comptable"
        TECHNIQUE = "Technique", "Technique"
        GESTIONNAIRE = "Gestionnaire", "Gestionnaire"

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    created_at = models.DateTimeField(default=timezone.now)
    updated_at = models.DateTimeField(default=timezone.now)
    is_synced = models.BooleanField(default=True)
    deleted_at = models.DateTimeField(null=True, blank=True)

    username = models.CharField(max_length=150, unique=True)
    email = models.EmailField(blank=True, default="")
    full_name = models.CharField(max_length=200, blank=True, default="")
    password_hash_sync = models.CharField(max_length=200, blank=True, default="")
    role = models.CharField(
        max_length=32, choices=Role.choices, default=Role.GESTIONNAIRE
    )
    is_active = models.BooleanField(default=True)
    is_staff = models.BooleanField(default=False)
    last_login_at = models.DateTimeField(null=True, blank=True)

    objects = UserManager()

    USERNAME_FIELD = "username"
    REQUIRED_FIELDS: list[str] = []

    def set_password(self, raw_password):
        import bcrypt

        self.password_hash_sync = bcrypt.hashpw(
            raw_password.encode("utf-8"), bcrypt.gensalt()
        ).decode("utf-8")
        super().set_password(raw_password)

    def check_password(self, raw_password):
        if self.password_hash_sync:
            import bcrypt

            try:
                return bcrypt.checkpw(
                    raw_password.encode("utf-8"),
                    self.password_hash_sync.encode("utf-8"),
                )
            except ValueError:
                pass
        return super().check_password(raw_password)


class Building(BaseEntity):
    name = models.CharField(max_length=200, default="")
    address = models.TextField(blank=True, default="")
    city = models.CharField(max_length=100, blank=True, default="")
    floors = models.IntegerField(default=1)


class Premise(BaseEntity):
    building_ref = models.ForeignKey(
        Building, null=True, blank=True, on_delete=models.SET_NULL, related_name="premises"
    )
    code = models.CharField(max_length=50, default="")
    name = models.CharField(max_length=200, default="")
    floor = models.CharField(max_length=50, blank=True, default="")
    building_name = models.CharField(max_length=200, blank=True, default="")
    premise_type = models.CharField(max_length=100, blank=True, default="")
    monthly_rent = models.DecimalField(max_digits=14, decimal_places=2, default=0)
    is_occupied = models.BooleanField(default=False)
    area_sq_m = models.DecimalField(max_digits=10, decimal_places=2, default=0)


class Tenant(BaseEntity):
    dossier_number = models.CharField(max_length=50, blank=True, default="")
    name = models.CharField(max_length=200, default="")
    email = models.EmailField(blank=True, default="")
    phone = models.CharField(max_length=50, blank=True, default="")
    company = models.CharField(max_length=200, blank=True, default="")
    rental_status = models.CharField(max_length=50, blank=True, default="Actif")
    tenant_category = models.CharField(max_length=50, blank=True, default="Particulier")


class LeaseContract(BaseEntity):
    premise = models.ForeignKey(
        Premise, null=True, on_delete=models.SET_NULL, related_name="leases"
    )
    tenant = models.ForeignKey(
        Tenant, null=True, on_delete=models.SET_NULL, related_name="leases"
    )
    premise_id_sync = models.UUIDField(null=True, blank=True)
    tenant_id_sync = models.UUIDField(null=True, blank=True)
    contract_number = models.CharField(max_length=80, blank=True, default="")
    start_date = models.DateField(null=True, blank=True)
    end_date = models.DateField(null=True, blank=True)
    monthly_rent = models.DecimalField(max_digits=14, decimal_places=2, default=0)
    deposit = models.DecimalField(max_digits=14, decimal_places=2, default=0)
    status = models.CharField(max_length=50, blank=True, default="Actif")


class RentPayment(BaseEntity):
    lease_contract = models.ForeignKey(
        LeaseContract, null=True, on_delete=models.SET_NULL, related_name="payments"
    )
    lease_contract_id_sync = models.UUIDField(null=True, blank=True)
    year = models.IntegerField(default=2026)
    month = models.IntegerField(default=1)
    amount_due = models.DecimalField(max_digits=14, decimal_places=2, default=0)
    amount_paid = models.DecimalField(max_digits=14, decimal_places=2, default=0)
    due_date = models.DateField(null=True, blank=True)
    paid_date = models.DateField(null=True, blank=True)
    is_late = models.BooleanField(default=False)
    payment_status = models.CharField(max_length=50, blank=True, default="En attente")


class FinancialTransaction(BaseEntity):
    class TxType(models.IntegerChoices):
        RECETTE = 1, "Recette"
        DEPENSE = 2, "Dépense"

    type = models.IntegerField(choices=TxType.choices, default=TxType.RECETTE)
    category = models.CharField(max_length=120, blank=True, default="")
    description = models.TextField(blank=True, default="")
    amount = models.DecimalField(max_digits=14, decimal_places=2, default=0)
    transaction_date = models.DateTimeField(default=timezone.now)
    reference = models.CharField(max_length=120, blank=True, default="")
    payment_method = models.CharField(max_length=80, blank=True, default="")
    status = models.CharField(max_length=50, blank=True, default="Payé")
    recorded_by = models.CharField(max_length=120, blank=True, default="")
    requires_pdg_approval = models.BooleanField(default=False)
    approved_at = models.DateTimeField(null=True, blank=True)
    approved_by = models.CharField(max_length=120, blank=True, default="")


class Employee(BaseEntity):
    employee_number = models.CharField(max_length=50, blank=True, default="")
    full_name = models.CharField(max_length=200, default="")
    position = models.CharField(max_length=120, blank=True, default="")
    department = models.CharField(max_length=120, blank=True, default="")
    email = models.EmailField(blank=True, default="")
    phone = models.CharField(max_length=50, blank=True, default="")
    is_active = models.BooleanField(default=True)
    monthly_salary = models.DecimalField(max_digits=14, decimal_places=2, default=0)


class Supplier(BaseEntity):
    name = models.CharField(max_length=200, default="")
    contact_person = models.CharField(max_length=120, blank=True, default="")
    email = models.EmailField(blank=True, default="")
    phone = models.CharField(max_length=50, blank=True, default="")
    category = models.CharField(max_length=100, blank=True, default="")


class Incident(BaseEntity):
    code = models.CharField(max_length=50, blank=True, default="")
    title = models.CharField(max_length=300, default="")
    description = models.TextField(blank=True, default="")
    incident_type = models.CharField(max_length=100, blank=True, default="")
    severity = models.CharField(max_length=50, blank=True, default="Moyenne")
    status = models.CharField(max_length=50, blank=True, default="Ouvert")
    location = models.CharField(max_length=200, blank=True, default="")
    building = models.CharField(max_length=200, blank=True, default="")
    reported_at = models.DateTimeField(default=timezone.now)
    cost = models.DecimalField(max_digits=14, decimal_places=2, default=0)


class Equipment(BaseEntity):
    name = models.CharField(max_length=200, default="")
    category = models.CharField(max_length=100, blank=True, default="")
    status = models.CharField(max_length=50, blank=True, default="")
    location = models.CharField(max_length=200, blank=True, default="")


class ConsumptionRecord(BaseEntity):
    consumption_type = models.CharField(max_length=50, blank=True, default="")
    period_start = models.DateField(null=True, blank=True)
    period_end = models.DateField(null=True, blank=True)
    quantity = models.DecimalField(max_digits=14, decimal_places=2, default=0)
    cost = models.DecimalField(max_digits=14, decimal_places=2, default=0)


class Visitor(BaseEntity):
    full_name = models.CharField(max_length=200, default="")
    company = models.CharField(max_length=200, blank=True, default="")
    purpose = models.CharField(max_length=300, blank=True, default="")
    check_in_at = models.DateTimeField(default=timezone.now)
    check_out_at = models.DateTimeField(null=True, blank=True)


class InventoryItem(BaseEntity):
    name = models.CharField(max_length=200, default="")
    category = models.CharField(max_length=100, blank=True, default="")
    quantity = models.IntegerField(default=0)
    unit = models.CharField(max_length=30, blank=True, default="")
    location = models.CharField(max_length=200, blank=True, default="")


class SyncedEntityStore(models.Model):
    """Copie JSON générique — miroir exact des payloads desktop."""

    id = models.UUIDField(primary_key=True)
    entity_type = models.CharField(max_length=64, db_index=True)
    json_data = models.JSONField(default=dict)
    created_at = models.DateTimeField(default=timezone.now)
    updated_at = models.DateTimeField(default=timezone.now)
    deleted_at = models.DateTimeField(null=True, blank=True)

    class Meta:
        indexes = [
            models.Index(fields=["entity_type", "updated_at"]),
        ]


class ServerSyncEvent(models.Model):
    """Journal côté serveur : chaque push/pull des gérants."""

    id = models.BigAutoField(primary_key=True)
    username = models.CharField(max_length=150, blank=True, default="")
    user_role = models.CharField(max_length=32, blank=True, default="")
    entity_type = models.CharField(max_length=64, db_index=True)
    direction = models.CharField(max_length=16, default="push")
    records_count = models.IntegerField(default=0)
    success = models.BooleanField(default=True)
    error_message = models.TextField(blank=True, default="")
    created_at = models.DateTimeField(default=timezone.now, db_index=True)

    class Meta:
        ordering = ["-created_at"]
