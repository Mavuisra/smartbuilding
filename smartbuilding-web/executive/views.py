from django.shortcuts import render


def login_page(request):
    return render(request, "executive/login.html")


def dashboard_page(request):
    return render(request, "executive/dashboard.html")
