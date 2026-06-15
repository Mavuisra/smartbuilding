# Generated manually for multi-tenant organizations

import uuid

import django.db.models.deletion
from django.db import migrations, models
from django.utils import timezone


def create_default_organization(apps, schema_editor):
    Organization = apps.get_model("api", "Organization")
    default_id = uuid.UUID("00000000-0000-0000-0000-000000000001")
    Organization.objects.get_or_create(
        id=default_id,
        defaults={
            "name": "Organisation principale",
            "slug": "organisation-principale",
            "database_name": "sbms_local",
            "is_active": True,
            "created_at": timezone.now(),
            "updated_at": timezone.now(),
        },
    )
    SyncedEntityStore = apps.get_model("api", "SyncedEntityStore")
    SyncedEntityStore.objects.filter(organization_id__isnull=True).update(
        organization_id=default_id
    )
    ServerSyncEvent = apps.get_model("api", "ServerSyncEvent")
    ServerSyncEvent.objects.filter(organization_id__isnull=True).update(
        organization_id=default_id
    )


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0004_synceddocument"),
    ]

    operations = [
        migrations.CreateModel(
            name="Organization",
            fields=[
                (
                    "id",
                    models.UUIDField(
                        default=uuid.uuid4,
                        editable=False,
                        primary_key=True,
                        serialize=False,
                    ),
                ),
                ("created_at", models.DateTimeField(default=timezone.now)),
                ("updated_at", models.DateTimeField(default=timezone.now)),
                ("is_synced", models.BooleanField(default=True)),
                ("deleted_at", models.DateTimeField(blank=True, null=True)),
                ("name", models.CharField(max_length=200)),
                ("slug", models.SlugField(max_length=80, unique=True)),
                ("database_name", models.CharField(blank=True, default="", max_length=120)),
                ("city", models.CharField(blank=True, default="", max_length=100)),
                ("description", models.TextField(blank=True, default="")),
                ("is_active", models.BooleanField(default=True)),
                ("created_by_username", models.CharField(blank=True, default="", max_length=150)),
            ],
            options={
                "verbose_name": "Organisation (tenant)",
                "verbose_name_plural": "Organisations (tenants)",
                "ordering": ["name"],
            },
        ),
        migrations.AddField(
            model_name="syncedentitystore",
            name="organization",
            field=models.ForeignKey(
                blank=True,
                null=True,
                on_delete=django.db.models.deletion.CASCADE,
                related_name="synced_entities",
                to="api.organization",
            ),
        ),
        migrations.AddField(
            model_name="synceddocument",
            name="organization",
            field=models.ForeignKey(
                blank=True,
                null=True,
                on_delete=django.db.models.deletion.CASCADE,
                related_name="synced_documents",
                to="api.organization",
            ),
        ),
        migrations.AddField(
            model_name="serversyncevent",
            name="organization",
            field=models.ForeignKey(
                blank=True,
                null=True,
                on_delete=django.db.models.deletion.SET_NULL,
                related_name="sync_events",
                to="api.organization",
            ),
        ),
        migrations.AddIndex(
            model_name="syncedentitystore",
            index=models.Index(
                fields=["organization", "entity_type", "updated_at"],
                name="api_synced__org_ent_upd_idx",
            ),
        ),
        migrations.RunPython(create_default_organization, migrations.RunPython.noop),
    ]
