from rest_framework.response import Response


def api_ok(data=None, message: str | None = None, status=200):
    body = {"success": True, "data": data, "message": message, "errors": None}
    return Response(body, status=status)


def api_fail(message: str, errors=None, status=400):
    body = {"success": False, "data": None, "message": message, "errors": errors}
    return Response(body, status=status)
