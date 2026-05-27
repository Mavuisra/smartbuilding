import os
from datetime import timedelta
from pathlib import Path

from django.core.exceptions import ImproperlyConfigured
from dotenv import load_dotenv

load_dotenv()

BASE_DIR = Path(__file__).resolve().parent.parent

SECRET_KEY = os.getenv("DJANGO_SECRET_KEY", "dev-only-change-me")
DEBUG = os.getenv("DJANGO_DEBUG", "True").lower() in ("1", "true", "yes")
ALLOWED_HOSTS = [
    h.strip()
    for h in os.getenv(
        "DJANGO_ALLOWED_HOSTS",
        "localhost,127.0.0.1,smartbuilding-0kbk.onrender.com",
    ).split(",")
    if h.strip()
]

# Render fournit automatiquement le hostname du service déployé.
_render_host = os.getenv("RENDER_EXTERNAL_HOSTNAME", "").strip()
if _render_host and _render_host not in ALLOWED_HOSTS:
    ALLOWED_HOSTS.append(_render_host)

# Tous les sous-domaines *.onrender.com (évite DisallowedHost après rename URL).
if ".onrender.com" not in ALLOWED_HOSTS:
    ALLOWED_HOSTS.append(".onrender.com")

INSTALLED_APPS = [
    "django.contrib.admin",
    "django.contrib.auth",
    "django.contrib.contenttypes",
    "django.contrib.sessions",
    "django.contrib.messages",
    "django.contrib.staticfiles",
    "corsheaders",
    "rest_framework",
    "rest_framework_simplejwt",
    "api",
    "executive",
]

MIDDLEWARE = [
    "django.middleware.security.SecurityMiddleware",
    "corsheaders.middleware.CorsMiddleware",
    "django.contrib.sessions.middleware.SessionMiddleware",
    "django.middleware.common.CommonMiddleware",
    "django.middleware.csrf.CsrfViewMiddleware",
    "django.contrib.auth.middleware.AuthenticationMiddleware",
    "django.contrib.messages.middleware.MessageMiddleware",
    "django.middleware.clickjacking.XFrameOptionsMiddleware",
]

ROOT_URLCONF = "smartbuilding_web.urls"

TEMPLATES = [
    {
        "BACKEND": "django.template.backends.django.DjangoTemplates",
        "DIRS": [BASE_DIR / "templates"],
        "APP_DIRS": True,
        "OPTIONS": {
            "context_processors": [
                "django.template.context_processors.request",
                "django.contrib.auth.context_processors.auth",
                "django.contrib.messages.context_processors.messages",
            ],
        },
    },
]

WSGI_APPLICATION = "smartbuilding_web.wsgi.application"


def _resolve_database_url() -> str:
    """Résout l'URL PostgreSQL (Render : variable sur le service WEB, pas seulement la base)."""
    raw = (
        os.getenv("DATABASE_URL")
        or os.getenv("POSTGRES_URL")
        or os.getenv("INTERNAL_DATABASE_URL")
        or ""
    ).strip().strip('"').strip("'")

    if raw:
        return raw

    user = os.getenv("POSTGRES_USER") or os.getenv("PGUSER")
    password = os.getenv("POSTGRES_PASSWORD") or os.getenv("PGPASSWORD")
    host = os.getenv("POSTGRES_HOST") or os.getenv("PGHOST")
    port = os.getenv("POSTGRES_PORT") or os.getenv("PGPORT") or "5432"
    name = os.getenv("POSTGRES_DB") or os.getenv("PGDATABASE")
    if user and password and host and name:
        return f"postgresql://{user}:{password}@{host}:{port}/{name}"

    on_render = os.getenv("RENDER", "").lower() in ("1", "true", "yes")
    if on_render or os.getenv("SBMS_PRODUCTION", "").lower() in ("1", "true", "yes"):
        raise ImproperlyConfigured(
            "DATABASE_URL absente sur le service WEB Render. "
            "Dashboard Render → smartbuilding-web → Environment : collez l'URL interne "
            "PostgreSQL (postgresql://…@dpg-…-a/dimplomate). "
            "Ou onglet Connexions → lier la base « dimplomate » au service web."
        )

    return f"sqlite:///{BASE_DIR / 'db.sqlite3'}"


_database_url = _resolve_database_url()
if _database_url.startswith("postgres://"):
    # Render fournit souvent postgres:// au lieu de postgresql://
    _database_url = "postgresql://" + _database_url[len("postgres://") :]

if _database_url.startswith("postgresql://"):
    from urllib.parse import parse_qs, urlparse

    parsed = urlparse(_database_url)
    if not parsed.hostname or not parsed.path:
        raise ImproperlyConfigured(
            "DATABASE_URL PostgreSQL invalide. Vérifiez host et nom de base (/dimplomate)."
        )

    db_name = parsed.path.lstrip("/").split("?")[0]
    query = parse_qs(parsed.query)
    conn_max_age = int(query.get("conn_max_age", ["60"])[0])

    pg_options: dict[str, str] = {}
    host = parsed.hostname or ""
    # Render Postgres : interne (dpg-xxx-a) → prefer ; externe (*.render.com) → require
    if host.startswith("dpg-") and "render.com" not in host:
        pg_options["sslmode"] = os.getenv("DATABASE_SSLMODE", "prefer")
    elif host.startswith("dpg-") or "render.com" in host:
        pg_options["sslmode"] = os.getenv("DATABASE_SSLMODE", "require")

    DATABASES = {
        "default": {
            "ENGINE": "django.db.backends.postgresql",
            "NAME": db_name,
            "USER": parsed.username or "",
            "PASSWORD": parsed.password or "",
            "HOST": host,
            "PORT": str(parsed.port or 5432),
            "CONN_MAX_AGE": conn_max_age,
            "OPTIONS": pg_options,
        }
    }
else:
    db_path = _database_url.replace("sqlite:///", "")
    DATABASES = {
        "default": {
            "ENGINE": "django.db.backends.sqlite3",
            "NAME": db_path if os.path.isabs(db_path) else BASE_DIR / db_path,
        }
    }

AUTH_PASSWORD_VALIDATORS = [
    {"NAME": "django.contrib.auth.password_validation.UserAttributeSimilarityValidator"},
    {"NAME": "django.contrib.auth.password_validation.MinimumLengthValidator"},
]

LANGUAGE_CODE = "fr-fr"
TIME_ZONE = "Africa/Kinshasa"
USE_I18N = True
USE_TZ = True

STATIC_URL = "static/"
STATIC_ROOT = BASE_DIR / "staticfiles"
DEFAULT_AUTO_FIELD = "django.db.models.BigAutoField"

AUTH_USER_MODEL = "api.User"

CORS_ALLOWED_ORIGINS = [
    o.strip()
    for o in os.getenv(
        "CORS_ALLOWED_ORIGINS",
        "http://localhost:8000,http://127.0.0.1:8000",
    ).split(",")
    if o.strip()
]

_render_url = os.getenv("RENDER_EXTERNAL_URL", "").strip().rstrip("/")
if _render_url and _render_url not in CORS_ALLOWED_ORIGINS:
    CORS_ALLOWED_ORIGINS.append(_render_url)

CSRF_TRUSTED_ORIGINS = list(CORS_ALLOWED_ORIGINS)
if _render_url and _render_url not in CSRF_TRUSTED_ORIGINS:
    CSRF_TRUSTED_ORIGINS.append(_render_url)

REST_FRAMEWORK = {
    "DEFAULT_AUTHENTICATION_CLASSES": (
        "rest_framework_simplejwt.authentication.JWTAuthentication",
    ),
    "DEFAULT_PERMISSION_CLASSES": (
        "rest_framework.permissions.IsAuthenticated",
    ),
    "DEFAULT_RENDERER_CLASSES": (
        "rest_framework.renderers.JSONRenderer",
    ),
}

_jwt_key = os.getenv("JWT_SIGNING_KEY", SECRET_KEY)
SIMPLE_JWT = {
    "ACCESS_TOKEN_LIFETIME": timedelta(hours=8),
    "REFRESH_TOKEN_LIFETIME": timedelta(days=7),
    "SIGNING_KEY": _jwt_key,
    "AUTH_HEADER_TYPES": ("Bearer",),
}

# Rôles autorisés sur le portail PDG (lecture consolidée)
EXECUTIVE_ROLES = {"PDG", "Administrateur"}
