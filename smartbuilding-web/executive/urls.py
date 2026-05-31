from django.urls import path
from django.views.generic import RedirectView

from executive import views

# Alias historiques → slugs alignés desktop
_LEGACY_REDIRECTS = [
    ("finance/", "finances/"),
    ("contrats/", "locations-list/"),
    ("presence/", "personnel/"),
    ("maintenance/", "technique/"),
    ("activites-logs/", "journal/"),
]

urlpatterns = [
    path("", views.dashboard_page, name="executive-dashboard"),
    path("login/", views.login_page, name="executive-login"),
]
urlpatterns += [
    path(old, RedirectView.as_view(url="/" + new, permanent=False))
    for old, new in _LEGACY_REDIRECTS
]
urlpatterns += [
    path("<slug:slug>/", views.module_page, name="executive-module"),
]
