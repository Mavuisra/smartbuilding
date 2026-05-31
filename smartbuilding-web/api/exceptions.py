"""Réponses API homogènes (y compris erreurs JWT / permissions DRF)."""

from rest_framework import status
from rest_framework.exceptions import APIException, AuthenticationFailed, NotAuthenticated, PermissionDenied
from rest_framework.views import exception_handler

from api.responses import api_fail


def api_exception_handler(exc, context):
    response = exception_handler(exc, context)
    if response is None:
        return response

    if isinstance(exc, (NotAuthenticated, AuthenticationFailed)):
        message = "Informations d'authentification non fournies."
        if hasattr(exc, "detail"):
            detail = exc.detail
            message = detail if isinstance(detail, str) else str(detail)
        return api_fail(message, status=status.HTTP_401_UNAUTHORIZED)

    if isinstance(exc, PermissionDenied):
        message = "Accès refusé."
        if hasattr(exc, "detail"):
            detail = exc.detail
            message = detail if isinstance(detail, str) else str(detail)
        return api_fail(message, status=status.HTTP_403_FORBIDDEN)

    if isinstance(exc, APIException):
        detail = exc.detail
        if isinstance(detail, list):
            message = "; ".join(str(x) for x in detail)
        elif isinstance(detail, dict):
            message = "; ".join(f"{k}: {v}" for k, v in detail.items())
        else:
            message = str(detail)
        return api_fail(message, status=response.status_code)

    return response
