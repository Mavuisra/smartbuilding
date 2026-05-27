from django.urls import path

from api.views import (
    DashboardSummaryView,
    ExecutiveOverviewView,
    ExecutiveIncidentsView,
    ExecutiveSyncLogView,
    ExecutiveTenantsView,
    HealthView,
    LoginView,
    SyncStatusView,
    SyncPullView,
    SyncPushView,
)

urlpatterns = [
    path("health/", HealthView.as_view(), name="health"),
    path("auth/login/", LoginView.as_view(), name="auth-login"),
    path("sync/push/", SyncPushView.as_view(), name="sync-push"),
    path("sync/pull/", SyncPullView.as_view(), name="sync-pull"),
    path("sync/status/", SyncStatusView.as_view(), name="sync-status"),
    path("dashboard/summary/", DashboardSummaryView.as_view(), name="dashboard-summary"),
    path("executive/overview/", ExecutiveOverviewView.as_view(), name="executive-overview"),
    path("executive/tenants/", ExecutiveTenantsView.as_view(), name="executive-tenants"),
    path("executive/incidents/", ExecutiveIncidentsView.as_view(), name="executive-incidents"),
    path("executive/sync-logs/", ExecutiveSyncLogView.as_view(), name="executive-sync-logs"),
]
