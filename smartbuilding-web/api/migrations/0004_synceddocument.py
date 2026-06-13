import uuid

from django.db import migrations, models
from django.utils import timezone


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0003_financialtransaction_approval_fields"),
    ]

    operations = [
        migrations.CreateModel(
            name="SyncedDocument",
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
                ("entity_type", models.CharField(db_index=True, max_length=64)),
                ("entity_id", models.UUIDField(db_index=True)),
                (
                    "category",
                    models.CharField(db_index=True, default="rapports", max_length=32),
                ),
                ("file_name", models.CharField(max_length=260)),
                (
                    "mime_type",
                    models.CharField(default="application/pdf", max_length=120),
                ),
                ("file_data", models.BinaryField()),
                ("file_size", models.BigIntegerField(default=0)),
                (
                    "content_sha256",
                    models.CharField(blank=True, db_index=True, default="", max_length=64),
                ),
                ("added_by", models.CharField(blank=True, default="", max_length=150)),
                ("created_at", models.DateTimeField(default=timezone.now)),
                ("updated_at", models.DateTimeField(default=timezone.now)),
            ],
            options={
                "indexes": [
                    models.Index(
                        fields=["entity_type", "entity_id"],
                        name="api_synceddo_entity__a8e2c4_idx",
                    ),
                    models.Index(
                        fields=["category", "updated_at"],
                        name="api_synceddo_categor_51f0b1_idx",
                    ),
                ],
            },
        ),
    ]
