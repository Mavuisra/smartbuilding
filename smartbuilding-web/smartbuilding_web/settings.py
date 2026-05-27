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

_database_url = os.getenv("DATABASE_URL", f"sqlite:///{BASE_DIR / 'db.sqlite3'}")
if _database_url.startswith("postgresql://"):
    from urllib.parse import parse_qs, urlparse

    parsed = urlparse(_database_url)
    if not parsed.hostname or not parsed.path:
        raise ValueError("DATABASE_URL PostgreSQL invalide")

    db_name = parsed.path.lstrip("/")
    query = parse_qs(parsed.query)
    conn_max_age = int(query.get("conn_max_age", ["60"])[0])

    DATABASES = {
        "default": {
            "ENGINE": "django.db.backends.postgresql",
            "NAME": db_name,
            "USER": parsed.username or "",
            "PASSWORD": parsed.password or "",
            "HOST": parsed.hostname,
            "PORT": str(parsed.port or 5432),
            "CONN_MAX_AGE": conn_max_age,
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

# En production (Render, etc.) : PostgreSQL persistant obligatoire.
# SQLite dans le conteneur est recréé à chaque déploiement → perte de données.
_is_production = (
    not DEBUG
    or os.getenv("RENDER", "").lower() in ("1", "true", "yes")
    or os.getenv("SBMS_PRODUCTION", "").lower() in ("1", "true", "yes")
)
_using_sqlite = DATABASES["default"]["ENGINE"] == "django.db.backends.sqlite3"
if _is_production and _using_sqlite:
    raise ImproperlyConfigured(
        "SBMS production exige DATABASE_URL PostgreSQL (service persistant). "
        "SQLite local dans le conteneur est effacé à chaque déploiement Git. "
        "Liez une base PostgreSQL Render à DATABASE_URL, puis redéployez."
    )

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
