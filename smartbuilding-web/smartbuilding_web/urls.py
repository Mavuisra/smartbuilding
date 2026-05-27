from django.contrib import admin
from django.urls import include, path

from api.views import HealthView

urlpatterns = [
    path("admin/", admin.site.urls),
    path("health/", HealthView.as_view(), name="health-root"),
    path("api/", include("api.urls")),
    path("", include("executive.urls")),
]
