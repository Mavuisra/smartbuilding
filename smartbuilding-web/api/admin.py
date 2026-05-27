from django.contrib import admin
from django.contrib.auth.admin import UserAdmin as BaseUserAdmin

from api.models import (
    Building,
    FinancialTransaction,
    Incident,
    LeaseContract,
    Premise,
    RentPayment,
    ServerSyncEvent,
    SyncedEntityStore,
    Tenant,
    User,
)


@admin.register(User)
class UserAdmin(BaseUserAdmin):
    list_display = ("username", "full_name", "role", "is_active", "last_login_at")
    list_filter = ("role", "is_active")
    fieldsets = (
        (None, {"fields": ("username", "password")}),
        ("Profil", {"fields": ("full_name", "email", "role")}),
        ("Permissions", {"fields": ("is_active", "is_staff", "is_superuser")}),
    )


admin.site.register(Tenant)
admin.site.register(Premise)
admin.site.register(Building)
admin.site.register(LeaseContract)
admin.site.register(RentPayment)
admin.site.register(FinancialTransaction)
admin.site.register(Incident)
admin.site.register(SyncedEntityStore)
admin.site.register(ServerSyncEvent)
