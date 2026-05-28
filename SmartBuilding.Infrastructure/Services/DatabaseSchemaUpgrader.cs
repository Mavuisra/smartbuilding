using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Met à jour le schéma SQLite existant sans recréer la base (EnsureCreated ne migre pas).
/// </summary>
public static class DatabaseSchemaUpgrader
{
    public static async Task UpgradeAsync(SmartBuildingDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlite())
            return;

        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await EnsureColumnAsync(connection, "Premises", "Building", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Premises", "PremiseType", "TEXT NOT NULL DEFAULT ''", cancellationToken);

            await EnsureColumnAsync(connection, "Tenants", "TenantCategory", "TEXT NOT NULL DEFAULT 'Particulier'", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "NationalId", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "DateOfBirth", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "Gender", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "MaritalStatus", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "SpouseName", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "ChildrenCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "Profession", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "EmergencyContactName", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "EmergencyContactPhone", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "Notes", "TEXT NULL", cancellationToken);

            await EnsureTenantActivitiesTableAsync(connection, cancellationToken);

            await EnsureColumnAsync(connection, "FinancialTransactions", "Source", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "FinancialTransactions", "PaymentMethod", "TEXT NOT NULL DEFAULT 'Virement'", cancellationToken);
            await EnsureColumnAsync(connection, "FinancialTransactions", "Status", "TEXT NOT NULL DEFAULT 'Payé'", cancellationToken);
            await EnsureColumnAsync(connection, "FinancialTransactions", "RecordedBy", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "FinancialTransactions", "RequiresPdgApproval", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "FinancialTransactions", "ApprovedAt", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "FinancialTransactions", "ApprovedBy", "TEXT NULL", cancellationToken);

            await EnsureColumnAsync(connection, "Equipment", "Brand", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "Model", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "SerialNumber", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "InstallationDate", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "PurchaseValue", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "WarrantyUntil", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "PowerSpec", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "VoltageSpec", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "FrequencySpec", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "FuelType", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Equipment", "OperatingHours", "TEXT NOT NULL DEFAULT ''", cancellationToken);

            await EnsureColumnAsync(connection, "Suppliers", "Code", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Suppliers", "Category", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Suppliers", "ServiceType", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Suppliers", "Status", "TEXT NOT NULL DEFAULT 'Actif'", cancellationToken);
            await EnsureColumnAsync(connection, "Suppliers", "ContactName", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Suppliers", "Building", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Suppliers", "Notes", "TEXT NULL", cancellationToken);

            await EnsureColumnAsync(connection, "SupplierContracts", "Status", "TEXT NOT NULL DEFAULT 'Actif'", cancellationToken);
            await EnsureColumnAsync(connection, "SupplierContracts", "Building", "TEXT NOT NULL DEFAULT ''", cancellationToken);

            await EnsureColumnAsync(connection, "SupplierPayments", "DueDate", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "SupplierPayments", "Description", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "SupplierPayments", "Category", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "SupplierPayments", "IsPaid", "INTEGER NOT NULL DEFAULT 1", cancellationToken);

            await EnsureColumnAsync(connection, "InventoryItems", "Status", "TEXT NOT NULL DEFAULT 'Opérationnel'", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "Responsible", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "SerialNumber", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "Building", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "Brand", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "Model", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "LastMaintenanceDate", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "NextMaintenanceDate", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "EstimatedValue", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "InventoryItems", "UsageDuration", "TEXT NOT NULL DEFAULT ''", cancellationToken);

            await EnsureInventoryMaintenanceTableAsync(connection, cancellationToken);

            await EnsureColumnAsync(connection, "ConsumptionRecords", "Building", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "ConsumptionRecords", "EquipmentSource", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "ConsumptionRecords", "Responsible", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "ConsumptionRecords", "Status", "TEXT NOT NULL DEFAULT 'Normal'", cancellationToken);
            await EnsureColumnAsync(connection, "ConsumptionRecords", "PeriodType", "TEXT NOT NULL DEFAULT 'Mensuel'", cancellationToken);
            await EnsureColumnAsync(connection, "ConsumptionRecords", "VariationPercent", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "ConsumptionRecords", "Currency", "TEXT NOT NULL DEFAULT 'FC'", cancellationToken);
            await EnsureColumnAsync(connection, "ConsumptionRecords", "IsAnomaly", "INTEGER NOT NULL DEFAULT 0", cancellationToken);

            await EnsureColumnAsync(connection, "Incidents", "Code", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Incidents", "IncidentType", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Incidents", "Building", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Incidents", "Responsible", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Incidents", "RiskLevel", "TEXT NOT NULL DEFAULT 'Moyen'", cancellationToken);
            await EnsureColumnAsync(connection, "Incidents", "HasPhoto", "INTEGER NOT NULL DEFAULT 0", cancellationToken);

            await EnsureIncidentInterventionsTableAsync(connection, cancellationToken);

            await EnsureColumnAsync(connection, "Visitors", "VisitCode", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "VisitType", "TEXT NOT NULL DEFAULT 'Réunion'", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "AccessStatus", "TEXT NOT NULL DEFAULT 'Actif'", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "Building", "TEXT NOT NULL DEFAULT 'Tour SBMS'", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "Zone", "TEXT NOT NULL DEFAULT 'Réception'", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "Email", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "IdDocumentType", "TEXT NOT NULL DEFAULT 'CNI'", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "AllowedZones", "TEXT NOT NULL DEFAULT 'Réception'", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "ExpectedCheckOutAt", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Visitors", "Notes", "TEXT NULL", cancellationToken);

            await EnsureVisitorAppointmentsTableAsync(connection, cancellationToken);

            await EnsureColumnAsync(connection, "CachedEmails", "AccountId", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "CcAddresses", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "BodyText", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "IsImportant", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "IsArchived", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "IsDraft", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "IsSpam", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "AwaitingReply", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "Category", "TEXT NOT NULL DEFAULT 'Administration'", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "Priority", "TEXT NOT NULL DEFAULT 'Normal'", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "AssignedTo", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "CachedEmails", "Tags", "TEXT NOT NULL DEFAULT ''", cancellationToken);

            await EnsureColumnAsync(connection, "Employees", "Address", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "Gender", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "BirthDate", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "NationalId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "MaritalStatus", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "EmergencyContactName", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "EmergencyContactPhone", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "Notes", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "ContractNumber", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "ContractType", "TEXT NOT NULL DEFAULT 'CDI'", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "ContractStartDate", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "ContractEndDate", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "Supervisor", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "WorkSchedule", "TEXT NOT NULL DEFAULT 'Lun–Ven 8h–17h'", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "RhStatus", "TEXT NOT NULL DEFAULT 'Actif'", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "ProfilePhotoPath", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "ContractPdfPath", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "SuspendedUntil", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "SuspensionReason", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "DismissedAt", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Employees", "DismissalReason", "TEXT NULL", cancellationToken);

            await EnsureColumnAsync(connection, "Attendances", "PresenceStatus", "TEXT NOT NULL DEFAULT 'Non pointé'", cancellationToken);
            await EnsureColumnAsync(connection, "Attendances", "LateMinutes", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "Attendances", "WorkedHours", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "Attendances", "OvertimeHours", "REAL NOT NULL DEFAULT 0", cancellationToken);

            await EnsureColumnAsync(connection, "SalaryPayments", "GrossSalary", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "Bonuses", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "Penalties", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "OvertimePay", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "Advances", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "Deductions", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "NetAmount", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "Status", "TEXT NOT NULL DEFAULT 'En attente'", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "ValidatedAt", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "SalaryPayments", "PaySlipPdfPath", "TEXT NULL", cancellationToken);

            await EnsureDisciplinaryNotesTableAsync(connection, cancellationToken);

            await EnsureBuildingsTableAsync(connection, cancellationToken);
            await EnsureLeaseGuaranteesTableAsync(connection, cancellationToken);
            await EnsureColumnAsync(connection, "LeaseGuarantees", "DischargePdfPath", "TEXT NULL", cancellationToken);

            await EnsureColumnAsync(connection, "Tenants", "DossierNumber", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "RentalStatus", "TEXT NOT NULL DEFAULT 'Actif'", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "ProfilePhotoPath", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "Nationality", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "BusinessActivity", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "PersonCount", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "IdentityDocumentPath", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Tenants", "ContractDocumentPath", "TEXT NULL", cancellationToken);

            await EnsureColumnAsync(connection, "Premises", "BuildingId", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "Premises", "OccupancyStatus", "TEXT NOT NULL DEFAULT 'Disponible'", cancellationToken);
            await EnsureColumnAsync(connection, "Premises", "Capacity", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await EnsureColumnAsync(connection, "Premises", "Equipment", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Premises", "ConditionNotes", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "Premises", "PhotoPath", "TEXT NULL", cancellationToken);

            await EnsureColumnAsync(connection, "LeaseContracts", "ContractType", "TEXT NOT NULL DEFAULT 'Bureau de travail'", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "Clauses", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "CreatedBy", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "ValidatedBy", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "ModifiedBy", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "CancelledBy", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "ValidatedAt", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "CancelledAt", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "LeaseContracts", "ContractPdfPath", "TEXT NULL", cancellationToken);

            await EnsureColumnAsync(connection, "RentPayments", "PenaltyAmount", "REAL NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "RentPayments", "PaymentStatus", "TEXT NOT NULL DEFAULT 'En attente'", cancellationToken);
            await EnsureColumnAsync(connection, "RentPayments", "ReceiptPdfPath", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "RentPayments", "PaymentMethod", "TEXT NOT NULL DEFAULT 'Virement bancaire'", cancellationToken);
            await EnsureColumnAsync(connection, "RentPayments", "TransactionReference", "TEXT NULL", cancellationToken);

            await ExecuteNonQueryAsync(connection, """
                UPDATE Employees SET ContractNumber = 'CTR-' || Matricule
                WHERE ContractNumber IS NULL OR ContractNumber = '';
                UPDATE Employees SET ContractType = 'CDI'
                WHERE ContractType IS NULL OR ContractType = '';
                """, cancellationToken);

            await EnsureColumnAsync(connection, "BuildingInfos", "TimeZoneId", "TEXT NOT NULL DEFAULT 'Africa/Kinshasa'", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "Currency", "TEXT NOT NULL DEFAULT 'USD'", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "UsdExchangeRate", "REAL NOT NULL DEFAULT 2850", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "DateFormat", "TEXT NOT NULL DEFAULT 'dd/MM/yyyy'", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "Language", "TEXT NOT NULL DEFAULT 'Français'", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "TimeFormat", "TEXT NOT NULL DEFAULT '24 heures'", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "MaintenanceMode", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "LogoPath", "TEXT NULL", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "Phone", "TEXT NOT NULL DEFAULT '+243 81 234 5678'", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "Email", "TEXT NOT NULL DEFAULT 'contact@sbms.cd'", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "NationalId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await EnsureColumnAsync(connection, "BuildingInfos", "Website", "TEXT NOT NULL DEFAULT 'www.sbms.cd'", cancellationToken);

            await ExecuteNonQueryAsync(connection, """
                UPDATE BuildingInfos
                SET Name = 'SBMS Immobilier SARL',
                    Address = '123, Avenue de la Gombe',
                    City = 'Kinshasa',
                    Country = 'RDC',
                    Phone = '+243 81 234 5678',
                    Email = 'contact@sbms.cd',
                    Website = 'www.sbms.cd',
                    NationalId = 'ID Nat. —',
                    TimeZoneId = 'Africa/Kinshasa',
                    Currency = 'USD'
                WHERE Country = 'France'
                   OR City LIKE '%configurer%'
                   OR Name = 'Smart Building (SB)'
                   OR Phone IS NULL OR Phone = '';
                """, cancellationToken);
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }

    private static async Task EnsureInventoryMaintenanceTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "InventoryMaintenanceRecords", cancellationToken))
            return;

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS "InventoryMaintenanceRecords" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_InventoryMaintenanceRecords" PRIMARY KEY,
                "InventoryItemId" TEXT NOT NULL,
                "ScheduledDate" TEXT NOT NULL,
                "CompletedDate" TEXT NULL,
                "Description" TEXT NOT NULL,
                "Cost" REAL NOT NULL,
                "Technician" TEXT NOT NULL,
                "RecordType" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsSynced" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureIncidentInterventionsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "IncidentInterventions", cancellationToken))
            return;

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS "IncidentInterventions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_IncidentInterventions" PRIMARY KEY,
                "IncidentId" TEXT NOT NULL,
                "Technician" TEXT NOT NULL,
                "InterventionType" TEXT NOT NULL,
                "StartedAt" TEXT NOT NULL,
                "EndedAt" TEXT NULL,
                "Cost" REAL NOT NULL,
                "Status" TEXT NOT NULL,
                "Result" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsSynced" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureVisitorAppointmentsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "VisitorAppointments", cancellationToken))
            return;

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS "VisitorAppointments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_VisitorAppointments" PRIMARY KEY,
                "VisitorId" TEXT NULL,
                "VisitorName" TEXT NOT NULL,
                "HostName" TEXT NOT NULL,
                "Purpose" TEXT NOT NULL,
                "ScheduledAt" TEXT NOT NULL,
                "Room" TEXT NOT NULL,
                "Building" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "DurationMinutes" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsSynced" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureBuildingsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "Buildings", cancellationToken))
            return;

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS "Buildings" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Buildings" PRIMARY KEY,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Address" TEXT NOT NULL,
                "FloorCount" INTEGER NOT NULL DEFAULT 0,
                "PremiseCount" INTEGER NOT NULL DEFAULT 0,
                "BuildingType" TEXT NOT NULL DEFAULT 'Bureau',
                "Capacity" INTEGER NOT NULL DEFAULT 0,
                "Status" TEXT NOT NULL DEFAULT 'Actif',
                "Equipment" TEXT NOT NULL DEFAULT '',
                "Zones" TEXT NOT NULL DEFAULT '',
                "PhotoPath" TEXT NULL,
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsSynced" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureLeaseGuaranteesTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "LeaseGuarantees", cancellationToken))
            return;

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS "LeaseGuarantees" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LeaseGuarantees" PRIMARY KEY,
                "LeaseContractId" TEXT NOT NULL,
                "Amount" REAL NOT NULL,
                "AmountRefunded" REAL NOT NULL DEFAULT 0,
                "Status" TEXT NOT NULL DEFAULT 'Active',
                "RefundedAt" TEXT NULL,
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsSynced" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureDisciplinaryNotesTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "DisciplinaryNotes", cancellationToken))
            return;

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS "DisciplinaryNotes" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_DisciplinaryNotes" PRIMARY KEY,
                "EmployeeId" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "OccurredAt" TEXT NOT NULL,
                "IssuedBy" TEXT NULL,
                "Severity" INTEGER NOT NULL DEFAULT 1,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsSynced" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureTenantActivitiesTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "TenantActivities", cancellationToken))
            return;

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS "TenantActivities" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TenantActivities" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "OccurredAt" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsSynced" INTEGER NOT NULL,
                "DeletedAt" TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        DbConnection connection,
        string table,
        string column,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken))
            return;

        if (await ColumnExistsAsync(connection, table, column, cancellationToken))
            return;

        await ExecuteNonQueryAsync(
            connection,
            $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {columnDefinition}",
            cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        AddParameter(command, "$name", table);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
