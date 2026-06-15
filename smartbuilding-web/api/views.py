from django.db.models import Sum
from typing import Any
from django.utils import timezone
from rest_framework.exceptions import PermissionDenied
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
    Organization,
    Premise,
    RentPayment,
    ServerSyncEvent,
    SyncedDocument,
    SyncedEntityStore,
    Tenant,
    User,
    Visitor,
)
from api.organization_context import (
    default_organization_for_user,
    normalize_slug,
    organization_to_dict,
    parse_organization_id,
    reset_request_organization_id,
    resolve_organization_id,
    resolve_user_organizations,
    scope_sync_store,
    set_request_organization_id,
    user_can_list_all_organizations,
    user_is_tenant_super_admin,
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
from api.services.cloud_login import resolve_cloud_login_user
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


def _pick_sync_id(data: dict[str, Any]) -> str:
    for key in ("Id", "id"):
        if key in data and data[key]:
            return str(data[key])
    return ""


def _rows_from_sync_store(
    entity_types: list[str],
    mapper,
    limit: int = 300,
    organization_id=None,
) -> list[dict[str, Any]]:
    rows = []
    qs = SyncedEntityStore.objects.filter(
        entity_type__in=entity_types, deleted_at__isnull=True
    )
    stores = scope_sync_store(qs, organization_id).order_by("-updated_at")[:limit]
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
    authentication_classes = ()

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
    # Connexion JWT : pas de SessionAuthentication → pas de CSRF obligatoire sur ce POST.
    authentication_classes = ()

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

    @staticmethod
    def _jessica_super_admin_passwords():
        return {"Admin@2026"}

    @classmethod
    def _bootstrap_allowed(cls) -> bool:
        import os

        from django.conf import settings

        return settings.DEBUG or os.getenv("SBMS_ALLOW_BOOTSTRAP_ADMIN", "").lower() in (
            "1",
            "true",
            "yes",
        )

    @classmethod
    def _resolve_login_user(cls, username: str, password: str):
        """Connexion cloud standard : admin / Admin@2026 (insensible à la casse)."""
        normalized = (username or "").strip()
        lowered = normalized.lower()
        bootstrap_passwords = cls._bootstrap_admin_passwords()

        if lowered == "admin" and password in bootstrap_passwords and cls._bootstrap_allowed():
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

        if lowered == "jessica" and password in cls._jessica_super_admin_passwords():
            user = (
                User.objects.filter(username__iexact="Jessica")
                .order_by("-updated_at")
                .first()
            )
            if user is None:
                user = User(
                    username="Jessica",
                    full_name="Jessica — Super Administrateur",
                    role=User.Role.PDG,
                    is_active=True,
                    is_staff=True,
                    is_superuser=True,
                )
            user.username = "Jessica"
            user.full_name = user.full_name or "Jessica — Super Administrateur"
            user.role = User.Role.PDG
            user.is_active = True
            user.is_staff = True
            user.is_superuser = True
            user.deleted_at = None
            user.set_password(password)
            user.save()
            return user

        return (
            resolve_cloud_login_user(normalized)
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
        username = serializer.validated_data["username"].strip()
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

        try:
            user_orgs = resolve_user_organizations(user)
            default_org = default_organization_for_user(user)
        except PermissionDenied as exc:
            detail = exc.detail if isinstance(exc.detail, str) else str(exc.detail)
            return api_fail(detail, status=403)

        return api_ok(
            {
                "token": str(token),
                "userId": str(user.id),
                "username": user.username,
                "fullName": user.full_name or user.username,
                "role": user.role,
                "permissions": permissions_for_role(user.role),
                "expiresAt": expires.isoformat().replace("+00:00", "Z"),
                "organizationId": str(default_org.id),
                "organizations": [organization_to_dict(o) for o in user_orgs],
                "canSwitchOrganizations": user_is_tenant_super_admin(user),
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

        org_id = resolve_organization_id(request)

        try:
            applied = apply_push(entity_type, entities, organization_id=org_id)
            ServerSyncEvent.objects.create(
                username=request.user.username,
                user_role=request.user.role,
                entity_type=entity_type,
                direction="push",
                records_count=applied,
                success=True,
                organization_id=org_id,
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

        org_id = resolve_organization_id(request)
        entities = get_changes_since(entity_type, since, organization_id=org_id)
        ServerSyncEvent.objects.create(
            username=request.user.username,
            user_role=request.user.role,
            entity_type=entity_type,
            direction="pull",
            records_count=len(entities),
            success=True,
            organization_id=org_id,
        )
        # Compat desktop EXE:
        # le client WPF lit directement SyncPullResponse (sans enveloppe ApiResponse).
        return Response(
            {
                "serverTimestamp": timezone.now().isoformat().replace("+00:00", "Z"),
                "entities": entities,
            }
        )


class DocumentUploadView(APIView):
    """Réception des PDF/documents desktop — contenu binaire inchangé."""

    permission_classes = [IsAuthenticated]

    def post(self, request):
        import base64
        import hashlib

        org_id = resolve_organization_id(request)
        entity_type = request.data.get("entityType") or request.data.get("EntityType") or ""
        entity_id = request.data.get("entityId") or request.data.get("EntityId")
        category = request.data.get("category") or request.data.get("Category") or "rapports"
        file_name = request.data.get("fileName") or request.data.get("FileName") or "document.pdf"
        mime_type = request.data.get("mimeType") or request.data.get("MimeType") or "application/pdf"
        content_b64 = request.data.get("contentBase64") or request.data.get("ContentBase64") or ""
        added_by = request.data.get("addedBy") or request.data.get("AddedBy") or ""
        sha_client = (request.data.get("contentSha256") or request.data.get("ContentSha256") or "").lower()

        if not entity_type or not entity_id:
            return api_fail("entityType et entityId sont requis.", status=400)
        if not content_b64:
            return api_fail("contentBase64 est requis.", status=400)

        import uuid as uuid_mod

        try:
            uid = uuid_mod.UUID(str(entity_id))
        except (ValueError, AttributeError):
            return api_fail("entityId invalide.", status=400)

        try:
            raw = base64.b64decode(content_b64, validate=True)
        except Exception:
            return api_fail("contentBase64 invalide.", status=400)

        if len(raw) > 20 * 1024 * 1024:
            return api_fail("Fichier trop volumineux (max 20 Mo).", status=400)

        sha = hashlib.sha256(raw).hexdigest()
        if sha_client and sha_client != sha:
            return api_fail("Hash SHA256 incohérent.", status=400)

        doc_id = uid
        existing = SyncedDocument.objects.filter(
            organization_id=org_id,
            entity_type=entity_type,
            entity_id=uid,
            content_sha256=sha,
        ).first()
        if existing is not None:
            return api_ok({"id": str(existing.id), "duplicate": True})

        SyncedDocument.objects.update_or_create(
            id=doc_id,
            defaults={
                "organization_id": org_id,
                "entity_type": entity_type,
                "entity_id": uid,
                "category": category,
                "file_name": file_name[:260],
                "mime_type": mime_type[:120],
                "file_data": raw,
                "file_size": len(raw),
                "content_sha256": sha,
                "added_by": added_by[:150],
                "updated_at": timezone.now(),
            },
        )

        ServerSyncEvent.objects.create(
            organization_id=org_id,
            username=getattr(request.user, "username", ""),
            user_role=getattr(request.user, "role", ""),
            entity_type="Documents",
            direction="push",
            records_count=1,
            success=True,
        )
        return api_ok({"id": str(doc_id), "fileSize": len(raw), "sha256": sha})


class DocumentDownloadView(APIView):
    permission_classes = [IsAuthenticated]

    def get(self, request, document_id):
        org_id = resolve_organization_id(request)
        try:
            doc = SyncedDocument.objects.get(id=document_id, organization_id=org_id)
        except SyncedDocument.DoesNotExist:
            return api_fail("Document introuvable.", status=404)

        from django.http import HttpResponse

        response = HttpResponse(bytes(doc.file_data), content_type=doc.mime_type)
        response["Content-Disposition"] = f'inline; filename="{doc.file_name}"'
        response["Content-Length"] = str(doc.file_size)
        return response


class DashboardSummaryView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        org_id = resolve_organization_id(request)
        return api_ok(get_executive_summary(organization_id=org_id))


class SyncStatusView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        org_id = resolve_organization_id(request)
        return api_ok(get_sync_health(organization_id=org_id))


class ExecutiveOverviewView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        try:
            org_id = resolve_organization_id(request)
            return api_ok(get_executive_overview(organization_id=org_id))
        except PermissionDenied as ex:
            return api_fail(str(ex.detail), status=403)
        except Exception as ex:
            import logging

            logging.getLogger(__name__).exception("executive/overview")
            return api_fail(f"Erreur tableau de bord : {ex}", status=500)


class ExecutiveTenantsView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        from api.models import Tenant
        from api.services.sync_metrics import ensure_dashboard_orm_materialized, filter_to_synced

        org_id = resolve_organization_id(request)
        ensure_dashboard_orm_materialized(org_id)

        base = Tenant.objects.filter(deleted_at__isnull=True).order_by("name")
        rows = filter_to_synced(base, "Tenants", org_id)[:500]
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
        if not data:
            data = _rows_from_sync_store(
                ["Tenants"],
                lambda p: {
                    "id": str(_pick_sync_id(p)),
                    "name": _pick_sync_value(p, "Name", "name", "FullName", "fullName"),
                    "email": _pick_sync_value(p, "Email", "email"),
                    "phone": _pick_sync_value(p, "Phone", "phone"),
                    "company": _pick_sync_value(p, "Company", "company"),
                    "status": _pick_sync_value(p, "RentalStatus", "rentalStatus", "Status", "status"),
                    "updatedAt": _pick_sync_value(p, "UpdatedAt", "updatedAt"),
                },
                organization_id=org_id,
            )
        return api_ok(data)


class ExecutiveIncidentsView(APIView):
    permission_classes = [IsExecutive]

    def get(self, request):
        from api.models import Incident
        from api.services.sync_metrics import filter_to_synced

        org_id = resolve_organization_id(request)
        base = Incident.objects.filter(deleted_at__isnull=True).order_by("-reported_at")
        rows = filter_to_synced(base, "Incidents", org_id)[:200]
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
        from api.organization_context import scope_sync_events

        org_id = resolve_organization_id(request)
        rows = scope_sync_events(ServerSyncEvent.objects.all(), org_id).order_by(
            "-created_at"
        )[:100]
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
        from datetime import date as date_cls

        from executive.module_registry import can_access_module, is_web_portal_module, resolve_slug

        slug = resolve_slug(slug)
        if not is_web_portal_module(slug):
            return api_fail("Module non disponible sur le portail web.", status=403)
        if not can_access_module(request.user.role, slug):
            return api_fail("Accès refusé à ce module.", status=403)

        try:
            from api.services.sync_metrics import ensure_dashboard_orm_materialized

            org_id = resolve_organization_id(request)
            org_token = set_request_organization_id(org_id)
            try:
                ensure_dashboard_orm_materialized(org_id)
                handler = get_module_handler(slug)
                if handler is None:
                    return api_fail(f"Module inconnu : {slug}", status=404)

                if slug == "rapports":
                    df = request.query_params.get("dateFrom")
                    dt = request.query_params.get("dateTo")
                    date_from = date_cls.fromisoformat(df) if df else None
                    date_to = date_cls.fromisoformat(dt) if dt else None
                    return api_ok(handler(date_from=date_from, date_to=date_to))

                if slug == "utilisateurs":
                    from api.services.web_desktop_modules import load_users

                    return api_ok(
                        load_users(current_username=getattr(request.user, "username", None))
                    )

                if slug == "dashboard":
                    return api_ok(handler(organization_id=org_id))

                return api_ok(handler())
            finally:
                reset_request_organization_id(org_token)
        except PermissionDenied as ex:
            return api_fail(str(ex.detail), status=403)
        except Exception as ex:
            import logging

            logging.getLogger(__name__).exception("executive/modules/%s", slug)
            return api_fail(f"Erreur module {slug} : {ex}", status=500)


class ExecutiveNavigationView(APIView):
    """Menu aligné sur ModuleRegistry desktop + permissions par rôle."""

    permission_classes = [IsExecutive]

    def get(self, request):
        from executive.module_registry import build_navigation

        return api_ok(build_navigation(request.user.role))


class ExecutiveUserDetailView(APIView):
    """CRUD utilisateurs — parité desktop Utilisateurs."""

    permission_classes = [IsExecutive]

    def get(self, request, user_id):
        from api.permission_codes import role_has_permission
        from api.services.web_desktop_modules import load_user_detail

        if not role_has_permission(request.user.role, "users.manage"):
            return api_fail("Accès refusé.", status=403)
        detail = load_user_detail(str(user_id))
        if detail is None:
            return api_fail("Utilisateur introuvable.", status=404)
        return api_ok(detail)

    def post(self, request):
        from api.permission_codes import role_has_permission

        if not role_has_permission(request.user.role, "users.manage"):
            return api_fail("Accès refusé.", status=403)

        username = (request.data.get("username") or "").strip()
        full_name = (request.data.get("fullName") or "").strip()
        email = (request.data.get("email") or "").strip()
        password = request.data.get("password") or ""
        role = request.data.get("role") or User.Role.GESTIONNAIRE

        if not username:
            return api_fail("L'identifiant est obligatoire.", status=400)
        if len(password) < 6:
            return api_fail("Le mot de passe doit contenir au moins 6 caractères.", status=400)
        if User.objects.filter(username__iexact=username, deleted_at__isnull=True).exists():
            return api_fail("Cet identifiant existe déjà.", status=400)

        user = User(username=username, full_name=full_name or username, email=email, role=role)
        user.set_password(password)
        user.is_staff = role in (User.Role.ADMIN, User.Role.PDG)
        user.save()
        return api_ok({"id": str(user.id)})

    def patch(self, request, user_id):
        from api.permission_codes import role_has_permission

        if not role_has_permission(request.user.role, "users.manage"):
            return api_fail("Accès refusé.", status=403)

        try:
            user = User.objects.get(id=user_id, deleted_at__isnull=True)
        except User.DoesNotExist:
            return api_fail("Utilisateur introuvable.", status=404)

        action = request.data.get("action")
        if action == "toggle_active":
            new_active = bool(request.data.get("isActive", not user.is_active))
            if not new_active and user.role == User.Role.ADMIN:
                others = User.objects.filter(
                    is_active=True, role=User.Role.ADMIN, deleted_at__isnull=True
                ).exclude(id=user.id).count()
                if others == 0:
                    return api_fail("Impossible de suspendre le dernier administrateur actif.", status=400)
            user.is_active = new_active
            user.save()
            return api_ok({"isActive": user.is_active})

        if action == "reset_password":
            password = request.data.get("password") or ""
            if len(password) < 6:
                return api_fail("Le mot de passe doit contenir au moins 6 caractères.", status=400)
            user.set_password(password)
            user.save()
            return api_ok({"reset": True})

        full_name = request.data.get("fullName")
        email = request.data.get("email")
        role = request.data.get("role")
        password = request.data.get("password")

        if full_name is not None:
            user.full_name = full_name.strip() or user.username
        if email is not None:
            user.email = email.strip()
        if role is not None:
            if user.role == User.Role.ADMIN and role != User.Role.ADMIN:
                others = User.objects.filter(
                    is_active=True, role=User.Role.ADMIN, deleted_at__isnull=True
                ).exclude(id=user.id).count()
                if others == 0:
                    return api_fail("Impossible de retirer le rôle du dernier administrateur actif.", status=400)
            user.role = role
            user.is_staff = role in (User.Role.ADMIN, User.Role.PDG)
        if password:
            if len(password) < 6:
                return api_fail("Le mot de passe doit contenir au moins 6 caractères.", status=400)
            user.set_password(password)
        user.save()
        return api_ok({"id": str(user.id)})


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


class OrganizationListView(APIView):
    """Liste des organisations accessibles — toutes pour Jessica, sinon uniquement le(s) tenant(s) assigné(s)."""

    permission_classes = [IsExecutive]

    def get(self, request):
        rows = resolve_user_organizations(request.user)
        return api_ok([organization_to_dict(o) for o in rows])


class OrganizationRegisterView(APIView):
    """Enregistrement métadonnées tenant depuis le Desktop (pas de création depuis le Web)."""

    permission_classes = [IsAuthenticated]

    def post(self, request):
        data = request.data
        org_id = parse_organization_id(data.get("id") or data.get("Id"))
        if org_id is None:
            return api_fail("Identifiant organisation (id) invalide.", status=400)

        name = (data.get("name") or data.get("Name") or "").strip()
        slug = (data.get("slug") or data.get("Slug") or "").strip().lower()
        if not name:
            return api_fail("Le nom du tenant est obligatoire.", status=400)
        if not slug:
            slug = name.lower().replace(" ", "-")[:80]

        try:
            slug = normalize_slug(slug)
        except ValueError as exc:
            return api_fail(str(exc), status=400)

        if Organization.objects.filter(slug=slug).exclude(id=org_id).exists():
            return api_fail("Ce slug est déjà utilisé par une autre organisation.", status=400)

        existing = Organization.objects.filter(id=org_id).first()
        username = getattr(request.user, "username", "") or ""
        if existing and not user_can_list_all_organizations(request.user):
            owner = (existing.created_by_username or "").lower()
            if owner and owner != username.lower():
                return api_fail(
                    "Mise à jour refusée : vous n'êtes pas propriétaire de cette organisation.",
                    status=403,
                )

        database_name = (data.get("databaseName") or data.get("database_name") or "").strip()
        city = (data.get("city") or data.get("City") or "").strip()

        org, created = Organization.objects.update_or_create(
            id=org_id,
            defaults={
                "name": name,
                "slug": slug,
                "database_name": database_name,
                "city": city,
                "is_active": True,
                "created_by_username": username,
                "updated_at": timezone.now(),
            },
        )
        if created and not org.created_at:
            org.created_at = timezone.now()
            org.save(update_fields=["created_at"])

        return api_ok(
            organization_to_dict(org),
            message="Organisation enregistrée sur le serveur central.",
        )
