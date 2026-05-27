from django.utils import timezone
from rest_framework.permissions import AllowAny, IsAuthenticated
from rest_framework.views import APIView
from rest_framework_simplejwt.tokens import AccessToken

from api.models import ServerSyncEvent, User
from api.permissions import IsExecutive
from api.responses import api_fail, api_ok
from api.serializers import (
    LoginSerializer,
    SyncPullQuerySerializer,
    SyncPushRequestSerializer,
)
from api.services.dashboard import get_executive_overview, get_executive_summary, get_sync_health
from api.sync import apply_push, get_changes_since, is_syncable


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
            return api_fail("Identifiants invalides.", status=401)

        if not user.check_password(password):
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
        since = serializer.validated_data["since"]
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
        return api_ok(
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
