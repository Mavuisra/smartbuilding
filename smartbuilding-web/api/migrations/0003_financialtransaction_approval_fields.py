from django.db import migrations, models


class Migration(migrations.Migration):

    dependencies = [
        ("api", "0002_executivenotification"),
    ]

    operations = [
        migrations.AddField(
            model_name="financialtransaction",
            name="requires_pdg_approval",
            field=models.BooleanField(default=False),
        ),
        migrations.AddField(
            model_name="financialtransaction",
            name="approved_at",
            field=models.DateTimeField(blank=True, null=True),
        ),
        migrations.AddField(
            model_name="financialtransaction",
            name="approved_by",
            field=models.CharField(blank=True, default="", max_length=120),
        ),
    ]
