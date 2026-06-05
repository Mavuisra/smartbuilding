from django.shortcuts import redirect, render

from api.module_handlers import get_module_handler
from executive.module_registry import (
    build_navigation,
    default_web_module_for_role,
    get_module,
    is_web_portal_module,
    module_meta,
    resolve_slug,
)

DESKTOP_TEMPLATES = {
    "rapports": "executive/module_rapports.html",
    "utilisateurs": "executive/module_utilisateurs.html",
    "parametres": "executive/module_parametres.html",
    "synchronisation": "executive/module_synchronisation.html",
    "journal": "executive/module_journal.html",
}


def login_page(request):
    return render(request, "executive/login.html")


def dashboard_page(request):
    return redirect("executive-module", slug=default_web_module_for_role("Administrateur"))


def module_page(request, slug):
    slug_norm = resolve_slug(slug)

    if not is_web_portal_module(slug_norm):
        return redirect("executive-module", slug=default_web_module_for_role("Administrateur"))

    mod = get_module(slug_norm)
    if mod is None and get_module_handler(slug_norm) is None:
        return redirect("executive-module", slug="rapports")

    meta = module_meta(slug_norm)
    nav = build_navigation("Administrateur")
    template = DESKTOP_TEMPLATES.get(slug_norm, "executive/module.html")

    return render(
        request,
        template,
        {
            "module_slug": slug_norm,
            "module": meta,
            "navigation": nav,
            "location_children": [],
        },
    )
