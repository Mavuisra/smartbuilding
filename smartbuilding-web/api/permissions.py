from rest_framework.permissions import BasePermission

from django.conf import settings


class IsExecutive(BasePermission):
    """PDG ou Administrateur — lecture consolidée."""

    def has_permission(self, request, view):
        user = request.user
        if not user or not user.is_authenticated:
            return False
        return user.role in settings.EXECUTIVE_ROLES or user.is_superuser
