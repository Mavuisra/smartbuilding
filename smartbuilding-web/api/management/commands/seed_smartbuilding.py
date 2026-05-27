import bcrypt
from django.core.management.base import BaseCommand

from api.models import User


class Command(BaseCommand):
    help = "Crée les comptes PDG et admin par défaut"

    def handle(self, *args, **options):
        accounts = [
            ("admin", "Admin@2026", User.Role.ADMIN, "Administrateur SBMS"),
            ("pdg", "Pdg@2026", User.Role.PDG, "Directeur Général"),
        ]
        for username, password, role, full_name in accounts:
            user, created = User.objects.get_or_create(
                username=username,
                defaults={"full_name": full_name, "role": role, "is_staff": True},
            )
            user.full_name = full_name
            user.role = role
            user.is_active = True
            user.password_hash_sync = bcrypt.hashpw(
                password.encode("utf-8"), bcrypt.gensalt()
            ).decode("utf-8")
            user.set_password(password)
            user.save()
            action = "créé" if created else "mis à jour"
            self.stdout.write(self.style.SUCCESS(f"Compte {username} {action} ({role})"))
