from rest_framework.permissions import BasePermission

from django.conf import settings


class IsExecutive(BasePermission):
    """Utilisateurs authentifiés — aligné sur les rôles desktop (lecture portail web)."""

    DESKTOP_ROLES = {
        "Administrateur",
        "PDG",
        "Comptable",
        "Technique",
        "Gestionnaire",
    }

    def has_permission(self, request, view):
        user = request.user
        if not user or not user.is_authenticated:
            return False
        if user.is_superuser:
            return True
        if user.role in self.DESKTOP_ROLES:
            return True
        return user.role in getattr(settings, "EXECUTIVE_ROLES", [])


class IsDatabaseAdmin(BasePermission):
    """Réinitialisation base — PDG et Administrateur uniquement."""

    def has_permission(self, request, view):
        user = request.user
        if not user or not user.is_authenticated:
            return False
        if user.is_superuser:
            return True
        role = getattr(user, "role", "") or ""
        return role in ("Administrateur", "PDG", "ADMIN")
