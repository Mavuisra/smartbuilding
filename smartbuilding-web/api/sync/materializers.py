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
    Tenant,
    User,
    Visitor,
)
from api.sync.registry import register
from api.sync.utils import (
    map_base_fields,
    parse_bool,
    parse_date,
    parse_datetime,
    parse_decimal,
    parse_int,
    pick,
)


@register("Users")
def materialize_user(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    user, _ = User.objects.get_or_create(id=uid, defaults={"username": "sync"})
    map_base_fields(user, data)
    user.username = pick(data, "Username", "username") or user.username
    user.email = pick(data, "Email", "email") or ""
    user.full_name = pick(data, "FullName", "fullName") or ""
    pw = pick(data, "PasswordHash", "passwordHash")
    if pw:
        user.password_hash_sync = pw
    role = pick(data, "Role", "role")
    if isinstance(role, int):
        roles = ["", "Administrateur", "Comptable", "Technique", "Gestionnaire"]
        user.role = roles[role] if role < len(roles) else User.Role.GESTIONNAIRE
    elif role:
        user.role = str(role)
    user.is_active = parse_bool(pick(data, "IsActive", "isActive"), True)
    user.last_login_at = parse_datetime(pick(data, "LastLoginAt", "lastLoginAt"))
    user.save()


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
        obj.premise_id_sync = pid
        obj.premise_id = pid
    if tid:
        obj.tenant_id_sync = tid
        obj.tenant_id = tid
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
    lid = pick(data, "LeaseContractId", "leaseContractId")
    if lid:
        obj.lease_contract_id_sync = lid
        obj.lease_contract_id = lid
    obj.save()


@register("FinancialTransactions")
def materialize_finance(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = FinancialTransaction.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    tx_type = pick(data, "Type", "type")
    obj.type = parse_int(tx_type, 1)
    obj.category = pick(data, "Category", "category") or ""
    obj.description = pick(data, "Description", "description") or ""
    obj.amount = parse_decimal(pick(data, "Amount", "amount"), 0)
    obj.transaction_date = parse_datetime(
        pick(data, "TransactionDate", "transactionDate")
    ) or obj.transaction_date
    obj.reference = pick(data, "Reference", "reference") or ""
    obj.payment_method = pick(data, "PaymentMethod", "paymentMethod") or ""
    obj.status = pick(data, "Status", "status") or ""
    obj.recorded_by = pick(data, "RecordedBy", "recordedBy") or ""
    obj.save()


@register("Employees")
def materialize_employee(data: dict):
    uid = pick(data, "Id", "id")
    if not uid:
        return
    obj, _ = Employee.objects.get_or_create(id=uid)
    map_base_fields(obj, data)
    obj.employee_number = pick(data, "EmployeeNumber", "employeeNumber") or ""
    obj.full_name = pick(data, "FullName", "fullName") or pick(data, "Name", "name") or ""
    obj.position = pick(data, "Position", "position") or ""
    obj.department = pick(data, "Department", "department") or ""
    obj.email = pick(data, "Email", "email") or ""
    obj.phone = pick(data, "Phone", "phone") or ""
    obj.is_active = parse_bool(pick(data, "IsActive", "isActive"), True)
    obj.monthly_salary = parse_decimal(pick(data, "MonthlySalary", "monthlySalary"), 0)
    obj.save()


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
