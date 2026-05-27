from django.urls import path

from executive import views

urlpatterns = [
    path("", views.dashboard_page, name="executive-dashboard"),
    path("login/", views.login_page, name="executive-login"),
    path("<slug:slug>/", views.module_page, name="executive-module"),
]
