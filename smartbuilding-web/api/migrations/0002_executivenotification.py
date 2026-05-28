from django.db import migrations, models
import django.utils.timezone


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0001_initial"),
    ]

    operations = [
        migrations.CreateModel(
            name="ExecutiveNotification",
            fields=[
                ("id", models.BigAutoField(primary_key=True, serialize=False)),
                ("title", models.CharField(max_length=200)),
                ("message", models.TextField(blank=True, default="")),
                (
                    "severity",
                    models.CharField(
                        choices=[
                            ("Info", "Info"),
                            ("Success", "Success"),
                            ("Warning", "Warning"),
                            ("Error", "Error"),
                        ],
                        default="Info",
                        max_length=16,
                    ),
                ),
                ("source", models.CharField(blank=True, default="", max_length=80)),
                ("action_type", models.CharField(blank=True, default="", max_length=80)),
                (
                    "entity_type",
                    models.CharField(blank=True, db_index=True, default="", max_length=64),
                ),
                ("entity_count", models.IntegerField(default=0)),
                ("created_by", models.CharField(blank=True, default="", max_length=150)),
                ("is_read", models.BooleanField(default=False)),
                (
                    "created_at",
                    models.DateTimeField(
                        db_index=True, default=django.utils.timezone.now
                    ),
                ),
            ],
            options={"ordering": ["-created_at"]},
        ),
    ]
