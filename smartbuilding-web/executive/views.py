from django.shortcuts import render


MODULE_PAGES = {
    "personnel": {
        "title": "Personnel",
        "subtitle": "Employés, rôles et état RH synchronisés depuis le desktop.",
        "group": "Gestion",
    },
    "locations": {
        "title": "Locations",
        "subtitle": "Locataires, espaces et occupation des locaux.",
        "group": "Gestion",
    },
    "contrats": {
        "title": "Contrats",
        "subtitle": "Contrats actifs, échéances et montants mensuels.",
        "group": "Gestion",
    },
    "finance": {
        "title": "Finance",
        "subtitle": "Recettes, dépenses et mouvements financiers.",
        "group": "Gestion",
    },
    "presence": {
        "title": "Présence",
        "subtitle": "Présence et activité des employés.",
        "group": "Gestion",
    },
    "documents": {
        "title": "Documents",
        "subtitle": "Contrats, justificatifs et documents générés.",
        "group": "Gestion",
    },
    "maintenance": {
        "title": "Maintenance",
        "subtitle": "Équipements, interventions et suivi technique.",
        "group": "Gestion",
    },
    "incidents": {
        "title": "Incidents",
        "subtitle": "Incidents déclarés, priorités et statut de traitement.",
        "group": "Gestion",
    },
    "supervision": {
        "title": "Supervision",
        "subtitle": "Vue globale des opérations et signaux critiques.",
        "group": "Supervision",
    },
    "validations": {
        "title": "Validations",
        "subtitle": "Centre d'approbation des dépenses et demandes sensibles.",
        "group": "Supervision",
        "is_validation": True,
    },
    "activites-logs": {
        "title": "Activités & Logs",
        "subtitle": "Traçabilité des synchronisations et activités système.",
        "group": "Supervision",
    },
    "utilisateurs": {
        "title": "Utilisateurs",
        "subtitle": "Comptes, rôles et accès au système.",
        "group": "Supervision",
    },
    "rapports": {
        "title": "Rapports",
        "subtitle": "Indicateurs, exports et synthèses exécutives.",
        "group": "Supervision",
    },
    "synchronisation": {
        "title": "Synchronisation",
        "subtitle": "État des échanges SQLite local vers PostgreSQL cloud.",
        "group": "Système",
    },
    "parametres": {
        "title": "Paramètres",
        "subtitle": "Configuration entreprise, bâtiment et environnement.",
        "group": "Système",
    },
    "audit-securite": {
        "title": "Audit & Sécurité",
        "subtitle": "Contrôles, accès et événements de sécurité.",
        "group": "Système",
    },
}


def login_page(request):
    return render(request, "executive/login.html")


def dashboard_page(request):
    return render(request, "executive/dashboard.html")


def module_page(request, slug):
    module = MODULE_PAGES.get(slug)
    if module is None:
        module = {
            "title": "Module",
            "subtitle": "Page de supervision SBMS.",
            "group": "Gestion",
        }
    return render(
        request,
        "executive/module.html",
        {
            "module_slug": slug,
            "module": module,
            "modules": MODULE_PAGES,
        },
    )
