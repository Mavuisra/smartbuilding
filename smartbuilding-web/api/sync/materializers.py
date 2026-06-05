from api.models import (
    Building,
    ConsumptionRecord,
    Employee,
    Equipment,
    FinancialTransaction,
    Incident,
    InventoryItem,
    LeaseContract,
    Premise,
    RentPayment,
    Supplier,
    SyncedEntityStore,
    Tenant,
    User,
    Visitor,
)
from api.sync.registry import register
from api.sync.utils import (
    inject_entity_id,
    map_base_fields,
    normalize_sync_datetime,
    parse_bool,
    parse_date,
    parse_datetime,
    parse_decimal,
    parse_int,
    parse_uuid,
    pick,
)

_ORM_BY_TYPE = {
    "Premises": Premise,
    "Tenants": Tenant,
    "LeaseContracts": LeaseContract,
}


def ensure_entity_materialized(entity_type: str, entity_id) -> bool:
    """Matérialise un parent depuis le magasin sync si absent de l'ORM."""
    uid = parse_uuid(entity_id)
    if not uid:
        return False

    model = _ORM_BY_TYPE.get(entity_type)
    if model is not None and model.objects.filter(id=uid).exists():
        return True

    try:
        store = SyncedEntityStore.objects.get(id=uid, entity_type=entity_type)
    except SyncedEntityStore.DoesNotExist:
        return False

    from api.sync.registry import _HANDLERS, register_all

    register_all()
    handler = _HANDLERS.get(entity_type)
    if handler is None:
        return False

    payload = store.json_data if isinstance(store.json_data, dict) else {}
    handler(inject_entity_id(payload, uid))
    return model is None or model.objects.filter(id=uid).exists()


@register("Users")
def materialize_user(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return

    username = (pick(data, "Username", "username") or "").strip()
    user = User.objects.filter(id=uid).first()
    if user is None and username:
        user = User.objects.filter(username__iexact=username).first()
    if user is None:
        user = User(id=uid, username=username or "sync")

    # Ne change jamais la PK d'un compte cloud existant: "admin" peut déjà exister
    # comme compte bootstrap avec un UUID différent de celui du desktop.
    if str(user.id) == str(uid):
        map_base_fields(user, data)
    else:
        if created := pick(data, "CreatedAt", "createdAt"):
            user.created_at = normalize_sync_datetime(created, user.created_at) or user.created_at
        if updated := pick(data, "UpdatedAt", "updatedAt"):
            user.updated_at = normalize_sync_datetime(updated, user.updated_at) or user.updated_at
        deleted = pick(data, "DeletedAt", "deletedAt")
        user.deleted_at = normalize_sync_datetime(deleted)
        user.is_synced = True

    user.username = username or user.username
    user.email = pick(data, "Email", "email") or ""
    user.full_name = pick(data, "FullName", "fullName") or ""
    pw = pick(data, "PasswordHash", "passwordHash")
    if pw:
        user.password_hash_sync = pw
    role = pick(data, "Role", "role")
    if isinstance(role, int):
        roles = ["", "Administrateur", "Comptable", "Technique", "Gestionnaire", "Réceptionniste"]
        user.role = roles[role] if role < len(roles) else User.Role.GESTIONNAIRE
    elif role:
        user.role = str(role)

    if username.lower() in ("admin", "admini", "admin2"):
        user.role = User.Role.ADMIN

    user.is_active = parse_bool(pick(data, "IsActive", "isActive"), True)
    user.last_login_at = normalize_sync_datetime(pick(data, "LastLoginAt", "lastLoginAt"))
    user.is_staff = user.role in (User.Role.ADMIN, User.Role.PDG)
    user.save()


@register("BuildingInfos")
def materialize_building_info(data: dict):
    """Profil patrimoine desktop → table Building côté web."""
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Building.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.name = (
        pick(data, "BuildingDisplayName", "buildingDisplayName")
        or pick(data, "Name", "name")
        or "Patrimoine SBMS"
    )
    obj.address = pick(data, "Address", "address") or ""
    obj.city = pick(data, "City", "city") or ""
    obj.floors = parse_int(pick(data, "TotalFloors", "totalFloors", "Floors", "floors"), 1)
    obj.save()


@register("Buildings")
def materialize_building(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Building.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.name = pick(data, "Name", "name") or ""
    obj.address = pick(data, "Address", "address") or ""
    obj.city = pick(data, "City", "city") or ""
    obj.floors = parse_int(pick(data, "Floors", "floors"), 1)
    obj.save()


@register("Premises")
def materialize_premise(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Premise.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.code = pick(data, "Code", "code") or ""
    obj.name = pick(data, "Name", "name") or ""
    obj.floor = pick(data, "Floor", "floor") or ""
    obj.building_name = pick(data, "Building", "building") or ""
    obj.premise_type = pick(data, "PremiseType", "premiseType") or ""
    obj.monthly_rent = parse_decimal(pick(data, "MonthlyRent", "monthlyRent"), 0)
    obj.is_occupied = parse_bool(pick(data, "IsOccupied", "isOccupied"), False)
    obj.area_sq_m = parse_decimal(pick(data, "AreaSqM", "areaSqM"), 0)
    bid = pick(data, "BuildingId", "buildingId")
    if bid:
        obj.building_ref_id = bid
    obj.save()


@register("Tenants")
def materialize_tenant(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Tenant.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.dossier_number = pick(data, "DossierNumber", "dossierNumber") or ""
    obj.name = pick(data, "Name", "name") or ""
    obj.email = pick(data, "Email", "email") or ""
    obj.phone = pick(data, "Phone", "phone") or ""
    obj.company = pick(data, "Company", "company") or ""
    obj.rental_status = pick(data, "RentalStatus", "rentalStatus") or "Actif"
    obj.tenant_category = pick(data, "TenantCategory", "tenantCategory") or ""
    obj.save()


@register("LeaseContracts")
def materialize_lease(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = LeaseContract.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.contract_number = pick(data, "ContractNumber", "contractNumber") or ""
    obj.start_date = parse_date(pick(data, "StartDate", "startDate"))
    obj.end_date = parse_date(pick(data, "EndDate", "endDate"))
    obj.monthly_rent = parse_decimal(pick(data, "MonthlyRent", "monthlyRent"), 0)
    obj.deposit = parse_decimal(pick(data, "Deposit", "deposit"), 0)
    status = pick(data, "Status", "status")
    if isinstance(status, int):
        statuses = ["Brouillon", "Actif", "Résilié", "Expiré"]
        obj.status = statuses[status - 1] if 0 < status <= len(statuses) else "Actif"
    elif status:
        obj.status = str(status)
    pid = pick(data, "PremiseId", "premiseId")
    tid = pick(data, "TenantId", "tenantId")
    if pid:
        ensure_entity_materialized("Premises", pid)
        obj.premise_id_sync = pid
        if Premise.objects.filter(id=pid).exists():
            obj.premise_id = pid
        else:
            obj.premise_id = None
    if tid:
        ensure_entity_materialized("Tenants", tid)
        obj.tenant_id_sync = tid
        if Tenant.objects.filter(id=tid).exists():
            obj.tenant_id = tid
        else:
            obj.tenant_id = None
    obj.save()


@register("RentPayments")
def materialize_rent_payment(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = RentPayment.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.year = parse_int(pick(data, "Year", "year"), 2026)
    obj.month = parse_int(pick(data, "Month", "month"), 1)
    obj.amount_due = parse_decimal(pick(data, "AmountDue", "amountDue"), 0)
    obj.amount_paid = parse_decimal(pick(data, "AmountPaid", "amountPaid"), 0)
    obj.due_date = parse_date(pick(data, "DueDate", "dueDate"))
    obj.paid_date = parse_date(pick(data, "PaidDate", "paidDate"))
    obj.is_late = parse_bool(pick(data, "IsLate", "isLate"), False)
    obj.payment_status = pick(data, "PaymentStatus", "paymentStatus") or ""

    obj.lease_contract_id = None
    obj.lease_contract_id_sync = None
    lid = pick(data, "LeaseContractId", "leaseContractId")
    if lid:
        ensure_entity_materialized("LeaseContracts", lid)
        obj.lease_contract_id_sync = lid
        if LeaseContract.objects.filter(id=lid).exists():
            obj.lease_contract_id = lid

    try:
        obj.save()
    except Exception:
        obj.lease_contract_id = None
        obj.save()


def _parse_tx_type(value) -> int:
    if isinstance(value, int):
        return value
    if isinstance(value, str):
        low = value.lower()
        if low in ("depense", "dépense", "expense", "2"):
            return FinancialTransaction.TxType.DEPENSE
        if low in ("recette", "income", "revenue", "1"):
            return FinancialTransaction.TxType.RECETTE
    return parse_int(value, FinancialTransaction.TxType.RECETTE)


@register("FinancialTransactions")
def materialize_finance(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = FinancialTransaction.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    tx_type = pick(data, "Type", "type")
    obj.type = _parse_tx_type(tx_type)
    obj.category = pick(data, "Category", "category") or ""
    obj.description = pick(data, "Description", "description") or ""
    obj.amount = parse_decimal(pick(data, "Amount", "amount"), 0)
    tx_dt = parse_datetime(pick(data, "TransactionDate", "transactionDate"))
    if tx_dt:
        from django.utils import timezone as dj_tz

        if tx_dt.tzinfo is None:
            tx_dt = dj_tz.make_aware(tx_dt, dj_tz.get_current_timezone())
        obj.transaction_date = tx_dt
    elif not obj.transaction_date:
        from django.utils import timezone as dj_tz

        obj.transaction_date = dj_tz.now()
    obj.reference = pick(data, "Reference", "reference") or ""
    obj.payment_method = pick(data, "PaymentMethod", "paymentMethod") or ""
    obj.status = pick(data, "Status", "status") or ""
    obj.recorded_by = pick(data, "RecordedBy", "recordedBy") or ""
    obj.requires_pdg_approval = parse_bool(
        pick(data, "RequiresPdgApproval", "requiresPdgApproval"), False
    )
    obj.approved_at = normalize_sync_datetime(pick(data, "ApprovedAt", "approvedAt"))
    obj.approved_by = pick(data, "ApprovedBy", "approvedBy") or ""
    obj.save()


def _employee_matricule(data: dict) -> str:
    """Desktop SBMS envoie Matricule (pas EmployeeNumber)."""
    return (
        pick(data, "Matricule", "matricule", "EmployeeNumber", "employeeNumber") or ""
    ).strip()


def _employee_full_name(data: dict) -> str:
    """Desktop SBMS envoie FirstName + LastName (pas FullName)."""
    full = pick(data, "FullName", "fullName") or pick(data, "Name", "name") or ""
    if str(full).strip():
        return str(full).strip()
    first = (pick(data, "FirstName", "firstName") or "").strip()
    last = (pick(data, "LastName", "lastName") or "").strip()
    return f"{first} {last}".strip()


@register("Employees")
def materialize_employee(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Employee.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    matricule = _employee_matricule(data)
    if matricule:
        obj.employee_number = matricule
    full_name = _employee_full_name(data)
    if full_name:
        obj.full_name = full_name
    position = pick(data, "Position", "position") or ""
    if position:
        obj.position = position
    department = pick(data, "Department", "department") or ""
    if department:
        obj.department = department
    email = pick(data, "Email", "email") or ""
    if email:
        obj.email = email
    phone = pick(data, "Phone", "phone") or ""
    if phone:
        obj.phone = phone
    obj.is_active = parse_bool(pick(data, "IsActive", "isActive"), True)
    obj.monthly_salary = parse_decimal(
        pick(data, "BaseSalary", "baseSalary", "MonthlySalary", "monthlySalary"),
        0,
    )
    obj.save()


def repair_employees_from_sync_store() -> int:
    """Réapplique le mapping pour les employés dont matricule/nom sont vides."""
    repaired = 0
    for store in SyncedEntityStore.objects.filter(entity_type="Employees"):
        if not store.json_data:
            continue
        emp = Employee.objects.filter(id=store.id, deleted_at__isnull=True).first()
        if emp is None:
            continue
        if (emp.employee_number or "").strip() and (emp.full_name or "").strip():
            continue
        materialize_employee(store.json_data)
        repaired += 1
    return repaired


@register("Suppliers")
def materialize_supplier(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Supplier.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.name = pick(data, "Name", "name") or ""
    obj.contact_person = pick(data, "ContactPerson", "contactPerson") or ""
    obj.email = pick(data, "Email", "email") or ""
    obj.phone = pick(data, "Phone", "phone") or ""
    obj.category = pick(data, "Category", "category") or ""
    obj.save()


@register("Incidents")
def materialize_incident(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Incident.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.code = pick(data, "Code", "code") or ""
    obj.title = pick(data, "Title", "title") or ""
    obj.description = pick(data, "Description", "description") or ""
    obj.incident_type = pick(data, "IncidentType", "incidentType") or ""
    sev = pick(data, "Severity", "severity")
    if isinstance(sev, int):
        sevs = ["Faible", "Moyenne", "Élevée", "Critique"]
        obj.severity = sevs[sev - 1] if 0 < sev <= len(sevs) else "Moyenne"
    elif sev:
        obj.severity = str(sev)
    st = pick(data, "Status", "status")
    if isinstance(st, int):
        sts = ["Ouvert", "En cours", "Résolu", "Clôturé"]
        obj.status = sts[st - 1] if 0 < st <= len(sts) else "Ouvert"
    elif st:
        obj.status = str(st)
    obj.location = pick(data, "Location", "location") or ""
    obj.building = pick(data, "Building", "building") or ""
    obj.reported_at = parse_datetime(pick(data, "ReportedAt", "reportedAt")) or obj.reported_at
    obj.cost = parse_decimal(pick(data, "Cost", "cost"), 0)
    obj.save()


@register("Equipment")
def materialize_equipment(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Equipment.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.name = pick(data, "Name", "name") or ""
    obj.category = pick(data, "Category", "category") or ""
    status = pick(data, "Status", "status")
    obj.status = str(status) if status is not None else ""
    obj.location = pick(data, "Location", "location") or ""
    obj.save()


@register("ConsumptionRecords")
def materialize_consumption(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = ConsumptionRecord.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    ct = pick(data, "ConsumptionType", "consumptionType", "Type", "type")
    obj.consumption_type = str(ct) if ct is not None else ""
    obj.period_start = parse_date(pick(data, "PeriodStart", "periodStart"))
    obj.period_end = parse_date(pick(data, "PeriodEnd", "periodEnd"))
    obj.quantity = parse_decimal(pick(data, "Quantity", "quantity"), 0)
    obj.cost = parse_decimal(pick(data, "Cost", "cost"), 0)
    obj.save()


@register("Visitors")
def materialize_visitor(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Visitor.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.full_name = pick(data, "FullName", "fullName") or pick(data, "Name", "name") or ""
    obj.company = pick(data, "Company", "company") or ""
    obj.purpose = pick(data, "Purpose", "purpose") or pick(data, "VisitPurpose", "visitPurpose") or ""
    obj.check_in_at = parse_datetime(pick(data, "CheckInAt", "checkInAt")) or obj.check_in_at
    obj.check_out_at = parse_datetime(pick(data, "CheckOutAt", "checkOutAt"))
    obj.save()


@register("InventoryItems")
def materialize_inventory(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = InventoryItem.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.name = pick(data, "Name", "name") or ""
    obj.category = pick(data, "Category", "category") or ""
    obj.quantity = parse_int(pick(data, "Quantity", "quantity"), 0)
    obj.unit = pick(data, "Unit", "unit") or ""
    obj.location = pick(data, "Location", "location") or ""
    obj.save()


def register_handlers():
    """Les décorateurs @register ont déjà enregistré les handlers."""
    return
