from django.db.models import Sum
from typing import Any
from django.utils import timezone
from rest_framework.permissions import AllowAny, IsAuthenticated
from rest_framework.response import Response
from rest_framework.views import APIView
from rest_framework_simplejwt.tokens import AccessToken

from api.models import (
    Building,
    ConsumptionRecord,
    Employee,
    Equipment,
    ExecutiveNotification,
    FinancialTransaction,
    Incident,
    InventoryItem,
    LeaseContract,
    Premise,
    RentPayment,
    ServerSyncEvent,
    SyncedEntityStore,
    Tenant,
    User,
    Visitor,
)
from api.permissions import IsExecutive
from api.responses import api_fail, api_ok
from api.sync.materializers import repair_employees_from_sync_store
from api.serializers import (
    LoginSerializer,
    SyncPullQuerySerializer,
    SyncPushRequestSerializer,
)
from api.services.dashboard import get_executive_overview, get_executive_summary, get_sync_health
from api.services.notifications import (
    maybe_notify_sync_push,
    notify_event,
    notify_granular_events_from_push,
    notify_login_failure,
)
from api.sync import apply_push, get_changes_since, is_syncable
from api.sync.utils import MIN_SYNC_DATETIME, normalize_sync_datetime


def _money(value):
    return float(value or 0)


def _iso(value):
    return value.isoformat() if value else None


def _module_payload(title, rows, kpis=None, actions=None):
    return {
        "title": title,
        "kpis": kpis or [],
        "rows": rows,
        "actions": actions or [],
    }


def _pick_sync_value(data: dict[str, Any], *keys: str, default: Any = "—") -> Any:
    for key in keys:
        if key in data and data[key] not in (None, ""):
            return data[key]
    return default


def _rows_from_sync_store(
    entity_types: list[str],
    mapper,
    limit: int = 300,
) -> list[dict[str, Any]]:
    rows = []
    stores = (
        SyncedEntityStore.objects.filter(entity_type__in=entity_types, deleted_at__isnull=True)
        .order_by("-updated_at")[:limit]
    )
    for s in stores:
        payload = s.json_data if isinstance(s.json_data, dict) else {}
        mapped = mapper(payload)
        if mapped:
            rows.append(mapped)
    return rows


class HealthView(APIView):
    permission_classes = [AllowAny]

    def get(self, request):
        return api_ok({"status": "ok", "service": "smartbuilding-web"})


class LoginView(APIView):
    permission_classes = [AllowAny]

    def post(self, request):
        serializer = LoginSerializer(
            data={
                "username": request.data.get("username") or request.data.get("Username"),
                "password": request.data.get("password") or request.data.get("Password"),
            }
        )
        if not serializer.is_valid():
            return api_fail("Identifiants requis.", errors=serializer.errors, status=400)
        username = serializer.validated_data["username"]
        password = serializer.validated_data["password"]

        try:
            user = User.objects.get(username=username, is_active=True, deleted_at__isnull=True)
        except User.DoesNotExist:
            # Compat demandée: premier accès web avec admin/admin.
            if username.strip().lower() == "admin" and password == "admin":
                user = User(
                    username="admin",
                    full_name="Administrateur SBMS",
                    role=User.Role.ADMIN,
                    is_active=True,
                    is_staff=True,
                )
                user.set_password("admin")
                user.save()
            else:
                notify_login_failure(username)
                return api_fail("Identifiants invalides.", status=401)

        if not user.check_password(password):
            # Compat demandée: accepte admin/admin et remet le hash à jour.
            if username.strip().lower() == "admin" and password == "admin":
                user.set_password("admin")
                user.is_active = True
                user.role = User.Role.ADMIN
                user.is_staff = True
                user.save(update_fields=["password", "password_hash_sync", "is_active", "role", "is_staff", "updated_at"])
            else:
                notify_login_failure(username)
                return api_fail("Identifiants invalides.", status=401)

        user.last_login_at = timezone.now()
        user.save(update_fields=["last_login_at"])

        token = AccessToken.for_user(user)
        expires = timezone.now() + token.lifetime

        return api_ok(
            {
                "token": str(token),
                "userId": str(user.id),
                "username": user.username,
                "fullName": user.full_name or user.username,
                "role": user.role,
                "permissions": [],
                "expiresAt": expires.isoformat().replace("+00:00", "Z"),
            }
        )


class SyncPushView(APIView):
    permission_classes = [IsAuthenticated]

    def post(self, request):
        serializer = SyncPushRequestSerializer(
            data={
                "entityType": request.data.get("entityType")
                or request.data.get("EntityType"),
                "entities": request.data.get("entities")
                or request.data.get("Entities")
                or [],
            }
        )
        if not serializer.is_valid():
            return api_fail(
                "Payload de synchronisation invalide.",
                errors=serializer.errors,
                status=400,
            )
        entity_type = serializer.validated_data["entityType"]
        entities = serializer.validated_data["entities"]

        if not is_syncable(entity_type):
            return api_fail(f"Type de sync inconnu : {entity_type}", status=400)

        try:
            applied = apply_push(entity_type, entities)
            ServerSyncEvent.objects.create(
                username=request.user.username,
                user_role=request.user.role,
                entity_type=entity_type,
                direction="push",
                records_count=applied,
                success=True,
            )
            maybe_notify_sync_push(
                entity_type=entity_type,
                records_count=applied,
                username=request.user.username,
                success=True,
            )
            notify_granular_events_from_push(
                entity_type=entity_type,
                entities=entities,
                username=request.user.username,
            )
            return api_ok(applied)
        except Exception as ex:
            ServerSyncEvent.objects.create(
                username=getattr(request.user, "username", ""),
                user_role=getattr(request.user, "role", ""),
                entity_type=entity_type or "",
                direction="push",
                records_count=0,
                success=False,
                error_message=str(ex),
            )
            maybe_notify_sync_push(
                entity_type=entity_type,
                records_count=0,
                username=getattr(request.user, "username", ""),
                success=False,
                error_message=str(ex),
            )
            return api_fail(str(ex), status=500)


class SyncPullView(APIView):
    permission_classes = [IsAuthenticated]

    def get(self, request):
        serializer = SyncPullQuerySerializer(data=request.query_params)
        if not serializer.is_valid():
            return api_fail(
                "Paramètres de synchronisation invalides.",
                errors=serializer.errors,
                status=400,
            )
        entity_type = serializer.validated_data["entityType"]
        since = normalize_sync_datetime(
            serializer.validated_data["since"],
            MIN_SYNC_DATETIME,
        ) or MIN_SYNC_DATETIME
        if not is_syncable(entity_type):
            return api_fail(f"Type de sync inconnu : {entity_type}", status=400)

        entities = get_changes_since(entity_type, since)
        ServerSyncEvent.objects.create(
            username=request.user.username,
            user_role=request.user.role,
            entity_type=entity_type,
            direction="pull",
            records_count=len(entities),
            success=True,
        )
        # Compat desktop EXE:
        # le client WPF lit directement SyncPullResponse (sans enveloppe ApiResponse).
        return Response(
            {
                "serverTimestamp": timezone.now().isoformat().replace("+00:00", "Z"),
                "entities": entities,
            }
        )


class DashboardSummaryView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        return api_ok(get_executive_summary())


class SyncStatusView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        return api_ok(get_sync_health())


class ExecutiveOverviewView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        return api_ok(get_executive_overview())


class ExecutiveTenantsView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        from api.models import Tenant

        rows = Tenant.objects.filter(deleted_at__isnull=True).order_by("name")[:500]
        data = [
            {
                "id": str(t.id),
                "name": t.name,
                "email": t.email,
                "phone": t.phone,
                "company": t.company,
                "status": t.rental_status,
                "updatedAt": t.updated_at.isoformat(),
            }
            for t in rows
        ]
        return api_ok(data)


class ExecutiveIncidentsView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        from api.models import Incident

        rows = Incident.objects.filter(deleted_at__isnull=True).order_by("-reported_at")[:200]
        data = [
            {
                "id": str(i.id),
                "code": i.code,
                "title": i.title,
                "severity": i.severity,
                "status": i.status,
                "location": i.location,
                "reportedAt": i.reported_at.isoformat(),
                "cost": float(i.cost),
            }
            for i in rows
        ]
        return api_ok(data)


class ExecutiveSyncLogView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        rows = ServerSyncEvent.objects.all().order_by("-created_at")[:100]
        data = [
            {
                "username": r.username,
                "role": r.user_role,
                "entityType": r.entity_type,
                "direction": r.direction,
                "recordsCount": r.records_count,
                "success": r.success,
                "errorMessage": r.error_message,
                "createdAt": r.created_at.isoformat(),
            }
            for r in rows
        ]
        return api_ok(data)


class ExecutiveModuleDataView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request, slug):
        handlers = {
            "personnel": self._personnel,
            "locations": self._locations,
            "contrats": self._contrats,
            "finance": self._finance,
            "presence": self._presence,
            "documents": self._documents,
            "maintenance": self._maintenance,
            "incidents": self._incidents,
            "supervision": self._supervision,
            "validations": self._validations,
            "activites-logs": self._activities,
            "utilisateurs": self._users,
            "rapports": self._reports,
            "synchronisation": self._sync,
            "parametres": self._settings,
            "audit-securite": self._audit,
        }
        handler = handlers.get(slug)
        if handler is None:
            return api_fail("Module inconnu.", status=404)
        return api_ok(handler())

    def _personnel(self):
        repair_employees_from_sync_store()
        rows = [
            {
                "Matricule": (e.employee_number or "").strip() or "—",
                "Nom": (e.full_name or "").strip() or "—",
                "Poste": e.position or "—",
                "Département": e.department or "—",
                "Téléphone": e.phone or "—",
                "Statut": "Actif" if e.is_active else "Inactif",
            }
            for e in Employee.objects.filter(deleted_at__isnull=True).order_by("full_name")[:300]
        ]
        return _module_payload(
            "Personnel",
            rows,
            [
                {"label": "Employés", "value": Employee.objects.filter(deleted_at__isnull=True).count()},
                {"label": "Actifs", "value": Employee.objects.filter(deleted_at__isnull=True, is_active=True).count()},
            ],
        )

    def _locations(self):
        rows = [
            {
                "Code": p.code or "—",
                "Local": p.name,
                "Bâtiment": p.building_name or "—",
                "Étage": p.floor or "—",
                "Loyer": _money(p.monthly_rent),
                "Statut": "Occupé" if p.is_occupied else "Libre",
            }
            for p in Premise.objects.filter(deleted_at__isnull=True).order_by("code")[:300]
        ]
        if not rows:
            rows = _rows_from_sync_store(
                ["Premises"],
                lambda d: {
                    "Code": _pick_sync_value(d, "Code", "code"),
                    "Local": _pick_sync_value(d, "Name", "name"),
                    "Bâtiment": _pick_sync_value(d, "Building", "building", "BuildingName", "buildingName"),
                    "Étage": _pick_sync_value(d, "Floor", "floor"),
                    "Loyer": _money(_pick_sync_value(d, "MonthlyRent", "monthlyRent", default=0)),
                    "Statut": "Occupé"
                    if bool(_pick_sync_value(d, "IsOccupied", "isOccupied", default=False))
                    else "Libre",
                },
            )
        total = Premise.objects.filter(deleted_at__isnull=True).count()
        occupied = Premise.objects.filter(deleted_at__isnull=True, is_occupied=True).count()
        if total == 0 and rows:
            total = len(rows)
            occupied = sum(1 for r in rows if r.get("Statut") == "Occupé")
        return _module_payload(
            "Locations",
            rows,
            [
                {"label": "Locaux", "value": total},
                {"label": "Occupés", "value": occupied},
                {"label": "Libres", "value": max(total - occupied, 0)},
            ],
        )

    def _contrats(self):
        rows = [
            {
                "Référence": c.contract_number or f"CT-{str(c.id)[:8]}",
                "Début": _iso(c.start_date),
                "Fin": _iso(c.end_date),
                "Loyer mensuel": _money(c.monthly_rent),
                "Garantie": _money(c.deposit),
                "Statut": c.status or "—",
            }
            for c in LeaseContract.objects.filter(deleted_at__isnull=True).order_by("-updated_at")[:300]
        ]
        if not rows:
            rows = _rows_from_sync_store(
                ["LeaseContracts"],
                lambda d: {
                    "Référence": _pick_sync_value(d, "ContractNumber", "contractNumber", "Reference", "reference"),
                    "Début": _pick_sync_value(d, "StartDate", "startDate"),
                    "Fin": _pick_sync_value(d, "EndDate", "endDate"),
                    "Loyer mensuel": _money(_pick_sync_value(d, "MonthlyRent", "monthlyRent", default=0)),
                    "Garantie": _money(_pick_sync_value(d, "Deposit", "deposit", default=0)),
                    "Statut": _pick_sync_value(d, "Status", "status"),
                },
            )
        return _module_payload(
            "Contrats",
            rows,
            [
                {"label": "Contrats", "value": LeaseContract.objects.filter(deleted_at__isnull=True).count()},
                {"label": "Actifs", "value": LeaseContract.objects.filter(deleted_at__isnull=True, status__icontains="actif").count()},
            ],
        )

    def _finance(self):
        rows = [
            {
                "Date": _iso(t.transaction_date),
                "Type": "Dépense" if t.type == FinancialTransaction.TxType.DEPENSE else "Recette",
                "Catégorie": t.category or "—",
                "Description": t.description or "—",
                "Montant": _money(t.amount),
                "Statut": t.status or "—",
            }
            for t in FinancialTransaction.objects.filter(deleted_at__isnull=True).order_by("-transaction_date")[:300]
        ]
        if not rows:
            rows = _rows_from_sync_store(
                ["FinancialTransactions"],
                lambda d: {
                    "Date": _pick_sync_value(d, "TransactionDate", "transactionDate", "CreatedAt", "createdAt"),
                    "Type": "Dépense"
                    if str(_pick_sync_value(d, "Type", "type", default="1")) in {"2", "Depense", "Dépense"}
                    else "Recette",
                    "Catégorie": _pick_sync_value(d, "Category", "category"),
                    "Description": _pick_sync_value(d, "Description", "description"),
                    "Montant": _money(_pick_sync_value(d, "Amount", "amount", default=0)),
                    "Statut": _pick_sync_value(d, "Status", "status"),
                },
            )
        expenses = FinancialTransaction.objects.filter(deleted_at__isnull=True, type=FinancialTransaction.TxType.DEPENSE).aggregate(t=Sum("amount"))["t"] or 0
        income = FinancialTransaction.objects.filter(deleted_at__isnull=True, type=FinancialTransaction.TxType.RECETTE).aggregate(t=Sum("amount"))["t"] or 0
        if expenses == 0 and income == 0 and rows:
            income = sum(r.get("Montant", 0) for r in rows if r.get("Type") == "Recette")
            expenses = sum(r.get("Montant", 0) for r in rows if r.get("Type") == "Dépense")
        return _module_payload(
            "Finance",
            rows,
            [
                {"label": "Recettes", "value": _money(income)},
                {"label": "Dépenses", "value": _money(expenses)},
                {"label": "Solde", "value": _money(income - expenses)},
            ],
        )

    def _presence(self):
        repair_employees_from_sync_store()
        rows = [
            {
                "Employé": (e.full_name or "").strip() or "—",
                "Département": e.department or "—",
                "Poste": e.position or "—",
                "Statut présence": "Présent/actif" if e.is_active else "Absent/inactif",
                "Dernière maj": _iso(e.updated_at),
            }
            for e in Employee.objects.filter(deleted_at__isnull=True).order_by("full_name")[:300]
        ]
        return _module_payload("Présence", rows, [{"label": "Présents/actifs", "value": Employee.objects.filter(deleted_at__isnull=True, is_active=True).count()}])

    def _documents(self):
        rows = [
            {"Type": "Contrat location", "Référence": c.contract_number or str(c.id)[:8], "Statut": c.status or "—", "Dernière maj": _iso(c.updated_at)}
            for c in LeaseContract.objects.filter(deleted_at__isnull=True).order_by("-updated_at")[:200]
        ]
        if not rows:
            rows = _rows_from_sync_store(
                ["LeaseContracts", "TenantActivities", "LeaseGuarantees"],
                lambda d: {
                    "Type": "Contrat location",
                    "Référence": _pick_sync_value(d, "ContractNumber", "contractNumber", "Reference", "reference"),
                    "Statut": _pick_sync_value(d, "Status", "status"),
                    "Dernière maj": _pick_sync_value(d, "UpdatedAt", "updatedAt"),
                },
                limit=200,
            )
        return _module_payload("Documents", rows, [{"label": "Documents suivis", "value": len(rows)}])

    def _maintenance(self):
        rows = [
            {"Équipement": e.name, "Catégorie": e.category or "—", "Statut": e.status or "—", "Localisation": e.location or "—", "Dernière maj": _iso(e.updated_at)}
            for e in Equipment.objects.filter(deleted_at__isnull=True).order_by("name")[:300]
        ]
        if not rows:
            rows = _rows_from_sync_store(
                ["Equipment"],
                lambda d: {
                    "Équipement": _pick_sync_value(d, "Name", "name"),
                    "Catégorie": _pick_sync_value(d, "Category", "category"),
                    "Statut": _pick_sync_value(d, "Status", "status"),
                    "Localisation": _pick_sync_value(d, "Location", "location"),
                    "Dernière maj": _pick_sync_value(d, "UpdatedAt", "updatedAt"),
                },
            )
        return _module_payload("Maintenance", rows, [{"label": "Équipements", "value": Equipment.objects.filter(deleted_at__isnull=True).count()}])

    def _incidents(self):
        rows = [
            {"Code": i.code or "—", "Titre": i.title, "Sévérité": i.severity, "Statut": i.status, "Lieu": i.location or "—", "Coût": _money(i.cost)}
            for i in Incident.objects.filter(deleted_at__isnull=True).order_by("-reported_at")[:300]
        ]
        if not rows:
            rows = _rows_from_sync_store(
                ["Incidents"],
                lambda d: {
                    "Code": _pick_sync_value(d, "Code", "code"),
                    "Titre": _pick_sync_value(d, "Title", "title"),
                    "Sévérité": _pick_sync_value(d, "Severity", "severity"),
                    "Statut": _pick_sync_value(d, "Status", "status"),
                    "Lieu": _pick_sync_value(d, "Location", "location", "Building", "building"),
                    "Coût": _money(_pick_sync_value(d, "Cost", "cost", default=0)),
                },
            )
        return _module_payload("Incidents", rows, [{"label": "Incidents", "value": Incident.objects.filter(deleted_at__isnull=True).count()}])

    def _supervision(self):
        summary = get_executive_summary()
        rows = [
            {"Indicateur": "Revenus mensuels", "Valeur": _money(summary["monthlyRevenue"]), "Statut": "Suivi"},
            {"Indicateur": "Dépenses mensuelles", "Valeur": _money(summary["monthlyExpenses"]), "Statut": "À contrôler"},
            {"Indicateur": "Occupation", "Valeur": f"{summary['occupancyRate']} %", "Statut": "Live"},
            {"Indicateur": "Incidents ouverts", "Valeur": summary["openIncidents"], "Statut": "Prioritaire"},
        ]
        return _module_payload("Supervision", rows, summary["quickStats"])

    def _validations(self):
        pending_statuses = ["En attente", "Attente", "Pending", "À valider", "A valider"]
        qs = FinancialTransaction.objects.filter(
            deleted_at__isnull=True,
            type=FinancialTransaction.TxType.DEPENSE,
        ).filter(status__in=pending_statuses).order_by("-updated_at")
        rows = [
            {
                "id": str(t.id),
                "Date": _iso(t.transaction_date),
                "Référence": t.reference or f"DEP-{str(t.id)[:8]}",
                "Catégorie": t.category or "—",
                "Description": t.description or "—",
                "Demandeur": t.recorded_by or "Comptable",
                "Montant": _money(t.amount),
                "Statut": t.status or "En attente",
                "_actions": ["approve", "reject"],
            }
            for t in qs[:300]
        ]
        total_amount = sum((t.amount for t in qs), 0)
        return _module_payload(
            "Validations",
            rows,
            [
                {"label": "Dépenses à valider", "value": qs.count()},
                {"label": "Montant en attente", "value": _money(total_amount)},
            ],
            actions=["approve-expense", "reject-expense"],
        )

    def _activities(self):
        rows = [
            {"Utilisateur": r.username or "Système", "Rôle": r.user_role or "—", "Type": r.entity_type, "Direction": r.direction, "Succès": "Oui" if r.success else "Non", "Date": _iso(r.created_at)}
            for r in ServerSyncEvent.objects.all().order_by("-created_at")[:300]
        ]
        return _module_payload("Activités & Logs", rows, [{"label": "Logs", "value": ServerSyncEvent.objects.count()}])

    def _users(self):
        rows = [
            {"Utilisateur": u.username, "Nom": u.full_name or "—", "Email": u.email or "—", "Rôle": u.role, "Actif": "Oui" if u.is_active else "Non", "Dernière connexion": _iso(u.last_login_at)}
            for u in User.objects.filter(deleted_at__isnull=True).order_by("username")[:300]
        ]
        return _module_payload("Utilisateurs", rows, [{"label": "Utilisateurs", "value": User.objects.filter(deleted_at__isnull=True).count()}])

    def _reports(self):
        summary = get_executive_summary()
        rows = [
            {"Rapport": "Situation financière", "Contenu": "Recettes, dépenses, solde", "Valeur clé": _money(summary["netBalance"])},
            {"Rapport": "Occupation", "Contenu": "Locaux occupés/libres", "Valeur clé": f"{summary['occupancyRate']} %"},
            {"Rapport": "Incidents", "Contenu": "Incidents ouverts", "Valeur clé": summary["openIncidents"]},
        ]
        return _module_payload("Rapports", rows, summary["quickStats"])

    def _sync(self):
        health = get_sync_health()
        rows = [
            {"Mesure": "Événements", "Valeur": health["totalEvents"]},
            {"Mesure": "Réussis", "Valeur": health["successfulEvents"]},
            {"Mesure": "Échoués", "Valeur": health["failedEvents"]},
            {"Mesure": "Taux succès", "Valeur": f"{health['successRate']} %"},
            {"Mesure": "Dernière sync", "Valeur": health["lastSyncAt"] or "—"},
        ]
        return _module_payload("Synchronisation", rows, [{"label": "Taux succès", "value": f"{health['successRate']} %"}])

    def _settings(self):
        rows = [
            {"Paramètre": "Bâtiments synchronisés", "Valeur": Building.objects.filter(deleted_at__isnull=True).count()},
            {"Paramètre": "Entités brutes sync", "Valeur": SyncedEntityStore.objects.count()},
            {"Paramètre": "Utilisateurs actifs", "Valeur": User.objects.filter(deleted_at__isnull=True, is_active=True).count()},
        ]
        return _module_payload("Paramètres", rows)

    def _audit(self):
        rows = [
            {"Contrôle": "Authentification JWT", "Statut": "Active", "Détail": "API protégée"},
            {"Contrôle": "Rôles exécutifs", "Statut": "Actif", "Détail": "PDG / Administrateur"},
            {"Contrôle": "Journal sync", "Statut": "Actif", "Détail": f"{ServerSyncEvent.objects.count()} événements"},
        ]
        return _module_payload("Audit & Sécurité", rows)


class ExpenseValidationActionView(APIView):
    permission_classes = [IsExecutive]

    def post(self, request, expense_id, action):
        try:
            tx = FinancialTransaction.objects.get(
                id=expense_id,
                deleted_at__isnull=True,
                type=FinancialTransaction.TxType.DEPENSE,
            )
        except FinancialTransaction.DoesNotExist:
            return api_fail("Dépense introuvable.", status=404)

        if action == "approve":
            tx.status = "Approuvé"
        elif action == "reject":
            tx.status = "Rejeté"
        else:
            return api_fail("Action invalide.", status=400)

        tx.updated_at = timezone.now()
        tx.is_synced = False
        tx.save(update_fields=["status", "updated_at", "is_synced"])
        ServerSyncEvent.objects.create(
            username=request.user.username,
            user_role=request.user.role,
            entity_type="FinancialTransaction",
            direction=f"validation:{action}",
            records_count=1,
            success=True,
        )
        notify_event(
            title="Validation de dépense",
            message=f"Dépense {tx.reference or str(tx.id)[:8]} {tx.status.lower()} par {request.user.username}.",
            severity=ExecutiveNotification.Severity.SUCCESS if action == "approve" else ExecutiveNotification.Severity.WARNING,
            source="Portail exécutif",
            action_type=f"expense_{action}",
            entity_type="FinancialTransactions",
            entity_count=1,
            created_by=request.user.username,
        )
        return api_ok({"id": str(tx.id), "status": tx.status})


class ExecutiveNotificationsView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        mark_read = str(request.query_params.get("markRead", "")).lower() in {"1", "true", "yes"}
        if mark_read:
            ExecutiveNotification.objects.filter(is_read=False).update(is_read=True)

        rows = ExecutiveNotification.objects.all().order_by("-created_at")[:100]
        data = [
            {
                "id": n.id,
                "title": n.title,
                "message": n.message,
                "severity": n.severity,
                "source": n.source or "Système",
                "actionType": n.action_type,
                "entityType": n.entity_type,
                "entityCount": n.entity_count,
                "createdBy": n.created_by or "Système",
                "isRead": n.is_read,
                "createdAt": n.created_at.isoformat(),
            }
            for n in rows
        ]
        unread_count = ExecutiveNotification.objects.filter(is_read=False).count()
        return api_ok({"unreadCount": unread_count, "items": data})
