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
from api.permissions import IsDatabaseAdmin, IsExecutive
from api.responses import api_fail, api_ok
from api.sync.materializers import repair_employees_from_sync_store
from api.serializers import (
    LoginSerializer,
    SyncPullQuerySerializer,
    SyncPushRequestSerializer,
)
from api.module_handlers import get_module_handler
from api.permission_codes import permissions_for_role
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


class LogoutView(APIView):
    permission_classes = [AllowAny]

    def post(self, request):
        from django.contrib.auth import logout as django_logout

        django_logout(request)
        return api_ok({"loggedOut": True})


class SessionCheckView(APIView):
    permission_classes = [AllowAny]

    def get(self, request):
        user = request.user
        if not user or not user.is_authenticated:
            return api_ok({"authenticated": False})
        return api_ok(
            {
                "authenticated": True,
                "username": user.username,
                "fullName": getattr(user, "full_name", None) or user.username,
                "role": getattr(user, "role", ""),
            }
        )


class LoginView(APIView):
    permission_classes = [AllowAny]

    @staticmethod
    def _login_payload(request):
        username = request.data.get("username") or request.data.get("Username")
        password = request.data.get("password") or request.data.get("Password")
        if (username is None or password is None) and request.body:
            try:
                import json

                raw = json.loads(request.body)
                if isinstance(raw, dict):
                    username = username or raw.get("username") or raw.get("Username")
                    password = password or raw.get("password") or raw.get("Password")
            except (json.JSONDecodeError, TypeError, ValueError):
                pass
        return username, password

    @staticmethod
    def _bootstrap_admin_passwords():
        return {"admin", "Admin@2026"}

    @classmethod
    def _resolve_login_user(cls, username: str, password: str):
        """Connexion cloud standard : admin / Admin@2026 (insensible à la casse)."""
        normalized = (username or "").strip()
        lowered = normalized.lower()
        bootstrap_passwords = cls._bootstrap_admin_passwords()

        if lowered == "admin" and password in bootstrap_passwords:
            user = (
                User.objects.filter(username__iexact="admin")
                .order_by("-updated_at")
                .first()
            )
            if user is None:
                user = User(
                    username="admin",
                    full_name="Administrateur SBMS",
                    role=User.Role.ADMIN,
                    is_active=True,
                    is_staff=True,
                )
            user.username = "admin"
            user.full_name = user.full_name or "Administrateur SBMS"
            user.role = User.Role.ADMIN
            user.is_active = True
            user.is_staff = True
            user.deleted_at = None
            user.set_password(password)
            user.save()
            return user

        return (
            User.objects.filter(
                username__iexact=normalized,
                is_active=True,
                deleted_at__isnull=True,
            )
            .order_by("-updated_at")
            .first()
        )

    def post(self, request):
        username_raw, password_raw = self._login_payload(request)
        serializer = LoginSerializer(
            data={
                "username": username_raw,
                "password": password_raw,
            }
        )
        if not serializer.is_valid():
            return api_fail("Identifiants requis.", errors=serializer.errors, status=400)
        username = serializer.validated_data["username"]
        password = serializer.validated_data["password"]

        user = self._resolve_login_user(username, password)
        if user is None:
            notify_login_failure(username)
            return api_fail("Identifiants invalides.", status=401)

        if not user.check_password(password):
            notify_login_failure(username)
            return api_fail("Identifiants invalides.", status=401)

        user.last_login_at = timezone.now()
        user.save(update_fields=["last_login_at"])

        from django.contrib.auth import login as django_login

        django_login(request, user)

        token = AccessToken.for_user(user)
        expires = timezone.now() + token.lifetime

        return api_ok(
            {
                "token": str(token),
                "userId": str(user.id),
                "username": user.username,
                "fullName": user.full_name or user.username,
                "role": user.role,
                "permissions": permissions_for_role(user.role),
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
        try:
            return api_ok(get_executive_overview())
        except Exception as ex:
            import logging

            logging.getLogger(__name__).exception("executive/overview")
            return api_fail(f"Erreur tableau de bord : {ex}", status=500)


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
        from executive.module_registry import can_access_module, resolve_slug

        slug = resolve_slug(slug)
        if not can_access_module(request.user.role, slug):
            return api_fail("Accès refusé à ce module.", status=403)
        handler = get_module_handler(slug)
        if handler is None:
            return api_fail(f"Module inconnu : {slug}", status=404)
        return api_ok(handler())


class ExecutiveNavigationView(APIView):
    """Menu aligné sur ModuleRegistry desktop + permissions par rôle."""

    permission_classes = [IsExecutive]

    def get(self, request):
        from executive.module_registry import build_navigation

        return api_ok(build_navigation(request.user.role))


class ExpenseValidationActionView(APIView):
    permission_classes = [IsExecutive]

    def post(self, request, expense_id, action):
        from api.services.finance_pdg import apply_pdg_validation

        tx, error = apply_pdg_validation(
            expense_id,
            action,
            getattr(request.user, "username", "") or "PDG",
        )
        if error:
            return api_fail(error, status=404 if "introuvable" in error.lower() else 400)

        ServerSyncEvent.objects.create(
            username=request.user.username,
            user_role=request.user.role,
            entity_type="FinancialTransactions",
            direction=f"validation:{action}",
            records_count=1,
            success=True,
        )
        notify_event(
            title="Validation de dépense",
            message=(
                f"Dépense {tx.reference or str(tx.id)[:8]} — statut « {tx.status} » "
                f"par {request.user.username}."
            ),
            severity=(
                ExecutiveNotification.Severity.SUCCESS
                if action == "approve"
                else ExecutiveNotification.Severity.WARNING
            ),
            source="Portail exécutif",
            action_type=f"expense_{action}",
            entity_type="FinancialTransactions",
            entity_count=1,
            created_by=request.user.username,
        )
        return api_ok(
            {
                "id": str(tx.id),
                "status": tx.status,
                "approvedBy": tx.approved_by,
                "approvedAt": tx.approved_at.isoformat() if tx.approved_at else None,
            }
        )


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


class DatabaseInfoView(APIView):
    permission_classes = [IsDatabaseAdmin]

    def get(self, request):
        from api.services.database_reset import database_info

        info = database_info()
        return api_ok(info)


class DatabaseResetView(APIView):
    permission_classes = [IsDatabaseAdmin]

    def post(self, request):
        import json
        import urllib.error
        import urllib.request

        from api.services.database_reset import (
            CONFIRM_PHRASE,
            database_info,
            is_render_host,
            reset_application_database,
            user_may_reset_database,
        )

        if not user_may_reset_database(request.user):
            return api_fail("Seuls le PDG et l'administrateur peuvent réinitialiser la base.", status=403)

        confirm = (request.data.get("confirmPhrase") or "").strip()
        if confirm != CONFIRM_PHRASE:
            return api_fail(f'Phrase de confirmation incorrecte. Saisissez exactement : "{CONFIRM_PHRASE}"', status=400)

        target = (request.data.get("target") or "server").strip().lower()
        if target not in ("server", "remote"):
            return api_fail('Cible invalide. Utilisez "server" ou "remote".', status=400)

        if target == "remote":
            if is_render_host():
                return api_fail(
                    "Vous êtes déjà sur le serveur en ligne. Utilisez la réinitialisation « base de ce serveur ».",
                    status=400,
                )
            info = database_info()
            url = f"{info['remoteApiUrl']}/api/executive/admin/reset-database/"
            body = json.dumps({"confirmPhrase": CONFIRM_PHRASE, "target": "server"}).encode("utf-8")
            auth = request.headers.get("Authorization", "")
            req = urllib.request.Request(
                url,
                data=body,
                headers={
                    "Content-Type": "application/json",
                    "Authorization": auth,
                },
                method="POST",
            )
            try:
                with urllib.request.urlopen(req, timeout=120) as resp:
                    payload = json.loads(resp.read().decode())
            except urllib.error.HTTPError as ex:
                try:
                    err_body = json.loads(ex.read().decode())
                    msg = err_body.get("message") or str(ex)
                except Exception:
                    msg = str(ex)
                return api_fail(f"Échec sur le serveur en ligne : {msg}", status=502)
            return api_ok(
                {
                    "target": "remote",
                    "remoteUrl": info["remoteApiUrl"],
                    "remoteResult": payload.get("data"),
                },
                message="Base en ligne réinitialisée.",
            )

        result = reset_application_database(reseed_accounts=True)
        ServerSyncEvent.objects.create(
            username=request.user.username,
            user_role=request.user.role,
            entity_type="Database",
            direction="reset",
            records_count=result.get("deletedRecords", 0),
            success=True,
        )
        return api_ok(
            result,
            message="Base de ce serveur réinitialisée. Comptes admin/pdg recréés.",
        )
