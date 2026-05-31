from django.shortcuts import redirect, render

from api.module_handlers import get_module_handler
from executive.module_registry import build_navigation, get_module, module_meta, resolve_slug


def login_page(request):
    return render(request, "executive/login.html")


def dashboard_page(request):
    return render(request, "executive/dashboard.html")


def module_page(request, slug):
    slug_norm = resolve_slug(slug)
    mod = get_module(slug)
    if mod is None and get_module_handler(slug_norm) is None:
        return redirect("executive-dashboard")

    meta = module_meta(slug)
    # Navigation par défaut (admin) — le JS filtre selon permissions utilisateur
    nav = build_navigation("Administrateur")

    template = (
        "executive/module_finances.html"
        if slug_norm in ("finances", "finance")
        else "executive/module.html"
    )
    return render(
        request,
        template,
        {
            "module_slug": slug,
            "module": meta,
            "navigation": nav,
            "location_children": [c for c in (mod.children if mod else [])],
        },
    )
