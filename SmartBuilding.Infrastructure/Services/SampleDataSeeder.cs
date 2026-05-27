using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Consumption;
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Entities.Inventory;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Entities.Suppliers;
using SmartBuilding.Domain.Entities.Email;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Domain.Entities.Visitors;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Données d'exemple pour démonstration (insérées une seule fois si les tables sont vides).
/// </summary>
public static class SampleDataSeeder
{
    public static async Task SeedAsync(SmartBuildingDbContext context)
    {
        await SeedBuildingInfoAsync(context);
        await SeedPersonnelAsync(context);
        await SeedFinanceAsync(context);
        await SeedTechnicalAsync(context);
        await EnrichTechnicalEquipmentAsync(context);
        await SeedSuppliersAsync(context);
        await EnrichSuppliersAsync(context);
        await SeedInventoryAsync(context);
        await EnrichInventoryAsync(context);
        await SeedConsumptionAsync(context);
        await EnrichConsumptionAsync(context);
        await SeedIncidentsAsync(context);
        await EnrichIncidentsAsync(context);
        await SeedVisitorsAsync(context);
        await SeedEmailsAsync(context);
        await EnrichFinanceDataAsync(context);
        await context.SaveChangesAsync();
    }

    private static async Task SeedBuildingInfoAsync(SmartBuildingDbContext context)
    {
        var building = await context.BuildingInfos.FirstOrDefaultAsync();
        if (building is null)
            return;

        if (BuildingInfoDefaults.NeedsKinshasaNormalization(building))
            BuildingInfoDefaults.ApplyKinshasaDefaults(building);
    }

    private static async Task SeedLocationsAsync(SmartBuildingDbContext context)
    {
        if (await context.Premises.AnyAsync())
            return;

        var buildingName = await context.BuildingInfos.Select(b => b.Name).FirstOrDefaultAsync()
                           ?? "Tour SBMS";

        var premises = new[]
        {
            Premise("LOC-001", "Bureau 101", buildingName, "1er", "Bureau", 85, 500_000, true),
            Premise("LOC-002", "Bureau 102", buildingName, "1er", "Bureau", 62, 350_000, true),
            Premise("LOC-003", "Magasin RDC", buildingName, "RDC", "Magasin", 120, 750_000, true),
            Premise("LOC-004", "Appartement 201", buildingName, "2e", "Appartement", 95, 600_000, true),
            Premise("LOC-005", "Bureau 203", buildingName, "2e", "Bureau", 55, 280_000, true),
            Premise("LOC-006", "Entrepôt sous-sol", buildingName, "Sous-sol", "Entrepôt", 200, 400_000, false),
            Premise("LOC-007", "Boutique 12", buildingName, "RDC", "Magasin", 45, 420_000, false),
            Premise("LOC-008", "Bureau 305", buildingName, "3e", "Bureau", 70, 320_000, false),
        };

        context.Premises.AddRange(premises);
        await context.SaveChangesAsync();

        var tenants = new[]
        {
            Tenant("SARL Congo Tech", "Marie Kabila", "+243 81 234 5678", "contact@congotech.cd"),
            Tenant("Boutique Élégance", "Jean Mukendi", "+243 99 876 5432", "jean@elegance.cd"),
            Tenant("Cabinet Juridique Mwamba", "Patrick Mwamba", "+243 82 111 2233", "p.mwamba@law.cd"),
            Tenant("Famille Tshilombo", "Grace Tshilombo", "+243 97 555 8899", "grace.tshilombo@gmail.com"),
            Tenant("Startup GreenLab", "David Kasongo", "+243 90 444 3322", "david@greenlab.cd"),
        };

        context.Tenants.AddRange(tenants);
        await context.SaveChangesAsync();

        var today = DateTime.Today;
        var contracts = new[]
        {
            Contract(premises[0].Id, tenants[0].Id, "CTR-001", today.AddYears(-1), today.AddMonths(8), 500_000, 1_000_000, LeaseStatus.Actif),
            Contract(premises[1].Id, tenants[1].Id, "CTR-002", today.AddMonths(-6), today.AddYears(1), 350_000, 700_000, LeaseStatus.Actif),
            Contract(premises[2].Id, tenants[2].Id, "CTR-003", today.AddYears(-2), today.AddMonths(3), 750_000, 1_500_000, LeaseStatus.Actif),
            Contract(premises[3].Id, tenants[3].Id, "CTR-004", today.AddMonths(-10), today.AddYears(2), 600_000, 1_200_000, LeaseStatus.Actif),
            Contract(premises[4].Id, tenants[4].Id, "CTR-005", today.AddMonths(-3), today.AddMonths(9), 280_000, 560_000, LeaseStatus.Actif),
            Contract(premises[6].Id, tenants[1].Id, "CTR-006", today.AddYears(-3), today.AddMonths(-2), 420_000, 840_000, LeaseStatus.Resilie),
        };

        foreach (var c in contracts)
            context.LeaseContracts.Add(c);

        await context.SaveChangesAsync();

        foreach (var contract in contracts.Where(c => c.Status == LeaseStatus.Actif))
        {
            for (var i = 5; i >= 0; i--)
            {
                var d = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                var isCurrent = d.Year == today.Year && d.Month == today.Month;
                var due = new DateTime(d.Year, d.Month, Math.Min(5, DateTime.DaysInMonth(d.Year, d.Month)));

                decimal paid = contract.MonthlyRent;
                var isLate = false;

                if (isCurrent && contract.ContractNumber is "CTR-003" or "CTR-005")
                {
                    paid = contract.MonthlyRent * 0.4m;
                    isLate = true;
                }
                else if (d < new DateTime(today.Year, today.Month, 1).AddMonths(-1) && contract.ContractNumber == "CTR-002")
                {
                    paid = 0;
                    isLate = true;
                }

                context.RentPayments.Add(new RentPayment
                {
                    LeaseContractId = contract.Id,
                    Year = d.Year,
                    Month = d.Month,
                    AmountDue = contract.MonthlyRent,
                    AmountPaid = paid,
                    DueDate = due,
                    PaidDate = paid >= contract.MonthlyRent ? due.AddDays(2) : null,
                    IsLate = isLate || (paid < contract.MonthlyRent && due < today),
                    ReceiptNumber = paid > 0 ? $"REC-{contract.ContractNumber}-{d:yyyyMM}" : null,
                    IsSynced = true
                });
            }
        }

        await SeedTenantActivitiesAsync(context, tenants, today);
    }

    private static async Task EnrichTenantProfilesAsync(SmartBuildingDbContext context)
    {
        var allTenants = await context.Tenants.ToListAsync();
        if (allTenants.Count == 0)
            return;

        var profiles = new Dictionary<string, (string marital, string spouse, int children, string gender, string profession, string emergency, string emergencyPhone, string notes)>
        {
            ["Marie Kabila"] = ("Mariée", "Joseph Kabila", 2, "Féminin", "Directrice commerciale", "Joseph Kabila", "+243 81 200 0001", "Locataire professionnelle, paiements réguliers."),
            ["Jean Mukendi"] = ("Marié", "Claire Mukendi", 3, "Masculin", "Commerçant", "Claire Mukendi", "+243 99 800 0002", "Gère la boutique Élégance au RDC."),
            ["Patrick Mwamba"] = ("Célibataire", "", 0, "Masculin", "Avocat", "Office Mwamba", "+243 82 999 0000", "Contrat renouvelé chaque année."),
            ["Grace Tshilombo"] = ("Mariée", "Eric Tshilombo", 4, "Féminin", "Enseignante", "Eric Tshilombo", "+243 97 600 0003", "Famille résidente longue durée."),
            ["David Kasongo"] = ("Célibataire", "", 1, "Masculin", "CEO Startup", "Maman Kasongo", "+243 90 300 0004", "Startup en croissance, caution versée.")
        };

        var tenants = new List<Tenant>();
        foreach (var tenant in allTenants)
        {
            if (!profiles.TryGetValue(tenant.Name, out var p))
                continue;

            if (!string.IsNullOrWhiteSpace(tenant.Gender)
                && tenant.MaritalStatus is not ("" or "—" or null))
                continue;

            tenants.Add(tenant);
            tenant.TenantCategory = string.IsNullOrWhiteSpace(tenant.Company) ? "Particulier" : "Professionnel";
            tenant.MaritalStatus = p.marital;
            tenant.SpouseName = p.spouse;
            tenant.ChildrenCount = p.children;
            tenant.Gender = p.gender;
            tenant.Profession = p.profession;
            tenant.EmergencyContactName = p.emergency;
            tenant.EmergencyContactPhone = p.emergencyPhone;
            tenant.Notes = p.notes;
            tenant.NationalId = $"ID-{tenant.Id.ToString()[..8].ToUpper()}";
            tenant.DateOfBirth = tenant.Name switch
            {
                "Marie Kabila" => new DateTime(1985, 3, 12),
                "Jean Mukendi" => new DateTime(1978, 7, 22),
                "Patrick Mwamba" => new DateTime(1980, 11, 5),
                "Grace Tshilombo" => new DateTime(1990, 1, 18),
                "David Kasongo" => new DateTime(1992, 9, 30),
                _ => new DateTime(1988, 6, 15)
            };
        }

        await SeedTenantActivitiesAsync(context, tenants, DateTime.Today);
    }

    private static async Task SeedTenantActivitiesAsync(
        SmartBuildingDbContext context,
        IEnumerable<Tenant> tenants,
        DateTime today)
    {
        if (await context.TenantActivities.AnyAsync())
            return;

        foreach (var tenant in tenants)
        {
            context.TenantActivities.AddRange(
                new TenantActivity
                {
                    TenantId = tenant.Id,
                    OccurredAt = today.AddMonths(-2),
                    Category = "Contrat",
                    Title = "Signature de bail",
                    Description = $"Ouverture du dossier locataire pour {tenant.Name}.",
                    IsSynced = true
                },
                new TenantActivity
                {
                    TenantId = tenant.Id,
                    OccurredAt = today.AddMonths(-1),
                    Category = "Paiement",
                    Title = "Loyer encaissé",
                    Description = "Paiement partiel ou total enregistré au guichet.",
                    IsSynced = true
                },
                new TenantActivity
                {
                    TenantId = tenant.Id,
                    OccurredAt = today.AddDays(-14),
                    Category = "Famille",
                    Title = "Mise à jour famille",
                    Description = $"Situation : {tenant.MaritalStatus}, enfants : {tenant.ChildrenCount}.",
                    IsSynced = true
                });
        }
    }

    private static async Task SeedPersonnelAsync(SmartBuildingDbContext context)
    {
        if (await context.Employees.AnyAsync())
            return;

        var today = DateTime.Today;
        var employees = new[]
        {
            Employee("EMP-0001", "Admin", "Principal", "Direction", "Directeur général", 4_500, today.AddYears(-5), "CDI", null, "—"),
            Employee("EMP-0002", "Sophie", "Martin", "RH", "Responsable RH", 2_800, today.AddYears(-3), "CDI", null, "EMP-0001"),
            Employee("EMP-0003", "Paul", "Ngoy", "Technique", "Technicien maintenance", 1_900, today.AddYears(-2), "CDD", today.AddMonths(6), "EMP-0001"),
            Employee("EMP-0004", "Amina", "Bello", "Locations", "Gestionnaire locatif", 2_200, today.AddMonths(-14), "CDI", null, "EMP-0002"),
            Employee("EMP-0005", "Marc", "Dubois", "Finance", "Comptable", 2_600, today.AddYears(-4), "CDI", null, "EMP-0001"),
            Employee("EMP-0006", "Claire", "Mutombo", "Accueil", "Réceptionniste", 1_400, today.AddMonths(-8), "CDD", today.AddMonths(4), "EMP-0002"),
        };

        context.Employees.AddRange(employees);
        await context.SaveChangesAsync();

        var checkInBase = today.AddHours(8);
        context.Attendances.AddRange(
            Attendance(employees[0], today, checkInBase.AddMinutes(10), checkInBase.AddHours(9), null),
            Attendance(employees[1], today, checkInBase.AddMinutes(5), checkInBase.AddHours(9).AddMinutes(30), null),
            Attendance(employees[2], today, checkInBase.AddHours(1).AddMinutes(20), checkInBase.AddHours(10), null),
            Attendance(employees[3], today, checkInBase.AddMinutes(-30), checkInBase.AddHours(8).AddMinutes(45), null),
            Attendance(employees[4], today, null, null, "congé — formation"),
            Attendance(employees[5], today, null, null, null)
        );

        for (var i = 5; i >= 0; i--)
        {
            var d = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            foreach (var emp in employees.Where(e => e.IsActive))
            {
                context.SalaryPayments.Add(new SalaryPayment
                {
                    EmployeeId = emp.Id,
                    Year = d.Year,
                    Month = d.Month,
                    Amount = emp.BaseSalary,
                    PaymentDate = d.AddDays(27),
                    Notes = $"Paie {d:MMMM yyyy}",
                    IsSynced = true
                });
            }
        }
    }

    private static async Task SeedTechnicalAsync(SmartBuildingDbContext context)
    {
        if (await context.Equipment.AnyAsync())
            return;

        var today = DateTime.Today;
        var equipmentList = new List<Equipment>
        {
            Eq("GEN-250KVA-001", "Groupe électrogène 250 KVA", "Électricité", "Sous-sol — Local technique",
                EquipmentStatus.Operationnel, "Caterpillar", "C250D5", "CAT-250-88921", today.AddYears(-4),
                85_000_000, today.AddYears(2), "250 KVA", "400 V", "50 Hz", "Diesel", "1 250 h"),
            Eq("CLIM-ROOF-01", "Centrale climatisation toiture", "Climatisation", "Toiture — Zone A",
                EquipmentStatus.Operationnel, "Daikin", "VRV-IV", "DK-VRV-4421", today.AddYears(-3),
                120_000_000, today.AddYears(1), "180 kW", "400 V", "50 Hz", "", "8 400 h"),
            Eq("ASC-01", "Ascenseur principal", "Ascenseur", "Hall — Tour A",
                EquipmentStatus.Maintenance, "Otis", "Gen2", "OT-ASC-1002", today.AddYears(-6),
                45_000_000, today.AddMonths(-2), "630 kg", "380 V", "50 Hz", "", "—"),
            Eq("POMP-SS-02", "Pompe surpresseur", "Plomberie", "Sous-sol — Chaufferie",
                EquipmentStatus.Operationnel, "Grundfos", "CR 64", "GR-PMP-778", today.AddYears(-2),
                12_500_000, today.AddYears(3), "15 kW", "400 V", "50 Hz", "", "3 200 h"),
            Eq("CAM-RDC-04", "Système CCTV RDC", "Sécurité", "RDC — Local sécurité",
                EquipmentStatus.Operationnel, "Hikvision", "DS-7732", "HK-CCTV-901", today.AddYears(-1),
                8_900_000, today.AddYears(4), "32 canaux", "220 V", "50 Hz", "", "—"),
            Eq("CHAU-GAZ-01", "Chaudière gaz collective", "Plomberie", "Sous-sol — Chaufferie",
                EquipmentStatus.EnPanne, "Viessmann", "Vitodens 200", "VS-CH-554", today.AddYears(-5),
                28_000_000, today.AddMonths(-6), "350 kW", "380 V", "50 Hz", "Gaz naturel", "12 000 h"),
            Eq("TRANSFO-01", "Transformateur 800 kVA", "Électricité", "Sous-sol — Local HT",
                EquipmentStatus.Operationnel, "Schneider", "Trihal", "SC-TR-800", today.AddYears(-8),
                95_000_000, today.AddYears(5), "800 kVA", "20 kV / 400 V", "50 Hz", "", "—"),
            Eq("EXT-SEC-02", "Contrôle accès parking", "Sécurité", "Parking — B2",
                EquipmentStatus.Operationnel, "Bosch", "AMS", "BS-ACC-221", today.AddMonths(-18),
                4_200_000, today.AddYears(2), "—", "24 V", "—", "", "—"),
            Eq("VENT-03", "Ventilation parking", "Climatisation", "Parking — B1/B2",
                EquipmentStatus.Operationnel, "Soler & Palau", "TD-500", "SP-VENT-33", today.AddYears(-3),
                6_800_000, today.AddYears(1), "5,5 kW", "400 V", "50 Hz", "", "6 100 h"),
            Eq("UPS-TECH", "Onduleur salle technique", "Électricité", "3e — Local IT",
                EquipmentStatus.Maintenance, "APC", "Symmetra LX", "APC-UPS-88", today.AddYears(-2),
                18_000_000, today.AddYears(3), "40 kVA", "400 V", "50 Hz", "", "—"),
            Eq("PORT-01", "Portail automatique", "Sécurité", "Entrée principale",
                EquipmentStatus.Operationnel, "CAME", "F7000", "CM-PRT-12", today.AddYears(-4),
                3_500_000, today.AddYears(1), "—", "24 V", "—", "", "—"),
            Eq("DET-INC-01", "Détection incendie", "Sécurité", "Tous étages",
                EquipmentStatus.Operationnel, "Siemens", "FC721", "SI-FIRE-01", today.AddYears(-7),
                22_000_000, today.AddYears(2), "—", "24 V", "—", "", "—"),
        };

        context.Equipment.AddRange(equipmentList);
        await context.SaveChangesAsync();
        // Pas de MaintenanceRecords fictifs : les coûts réels passent par la trésorerie (loyers).
    }

    private static Equipment Eq(string code, string name, string category, string location,
        EquipmentStatus status, string brand, string model, string serial, DateTime installed,
        decimal purchase, DateTime warranty, string power, string voltage, string freq, string fuel, string hours) => new()
    {
        Code = code,
        Name = name,
        Category = category,
        Location = location,
        Status = status,
        Brand = brand,
        Model = model,
        SerialNumber = serial,
        InstallationDate = installed,
        PurchaseValue = purchase,
        WarrantyUntil = warranty,
        PowerSpec = power,
        VoltageSpec = voltage,
        FrequencySpec = freq,
        FuelType = fuel,
        OperatingHours = hours,
        LastMaintenanceDate = DateTime.Today.AddMonths(-3),
        NextMaintenanceDate = status == EquipmentStatus.Maintenance
            ? DateTime.Today.AddDays(5)
            : DateTime.Today.AddMonths(3),
        IsSynced = true
    };

    private static MaintenanceRecord Maint(Guid equipmentId, DateTime scheduled, DateTime? completed,
        string desc, decimal cost, string tech) => new()
    {
        EquipmentId = equipmentId,
        ScheduledDate = scheduled,
        CompletedDate = completed,
        Description = desc,
        Cost = cost,
        Technician = tech,
        IsSynced = true
    };

    private static async Task SeedSuppliersAsync(SmartBuildingDbContext context)
    {
        if (await context.Suppliers.AnyAsync())
            return;

        var today = DateTime.Today;
        var suppliers = new List<Supplier>
        {
            Sup("FRN-001", "Kin Elec Services", "Électricité", "Maintenance électrique", "Actif",
                "Jean Kabongo", "+243 81 200 1100", "contact@kinelec.cd", "Tour SBMS", "ID-KIN-001"),
            Sup("FRN-002", "Securimax RDC", "Sécurité", "Gardiennage & CCTV", "Actif",
                "Marie Tshisekedi", "+243 99 400 2200", "ops@securimax.cd", "Tour SBMS", "ID-SEC-002"),
            Sup("FRN-003", "ClimaPro Kinshasa", "Climatisation", "Climatisation & froid", "Actif",
                "Patrick Mwamba", "+243 82 555 3300", "service@climapro.cd", "Tour SBMS", "ID-CLI-003"),
            Sup("FRN-004", "Plomberie Express", "Plomberie", "Dépannage plomberie", "Actif",
                "Grace Mutombo", "+243 97 600 4400", "urgence@plombex.cd", "Tour SBMS", "ID-PLO-004"),
            Sup("FRN-005", "Nettoyage Premium", "Nettoyage", "Entretien espaces communs", "En attente",
                "David Kasongo", "+243 90 300 5500", "premium@nettoyage.cd", "Tour SBMS", "ID-NET-005"),
            Sup("FRN-006", "Ascenseurs Congo", "Ascenseur", "Maintenance ascenseurs", "Actif",
                "Sophie Martin", "+243 81 111 6600", "tech@asccongo.cd", "Tour SBMS", "ID-ASC-006"),
            Sup("FRN-007", "Energie Plus", "Énergie", "Audit énergétique", "Expiré",
                "Paul Ngoy", "+243 99 800 7700", "facturation@energieplus.cd", "Tour SBMS", "ID-ENR-007"),
            Sup("FRN-008", "Menuiserie Bâtiment A", "Maintenance", "Menuiserie & serrurerie", "Actif",
                "Claire Mukendi", "+243 97 555 8800", "atelier@menuiserie.cd", "Tour SBMS", "ID-MEN-008"),
            Sup("FRN-009", "Peinture & Finitions", "Maintenance", "Travaux peinture", "Actif",
                "Eric Tshilombo", "+243 82 999 9900", "devis@peinture.cd", "Tour SBMS", "ID-PEI-009"),
            Sup("FRN-010", "Fournitures Bureau SB", "Services", "Fournitures administratives", "Actif",
                "Admin Principal", "+243 81 000 0010", "achats@sbms.local", "Tour SBMS", "ID-FOU-010"),
        };

        context.Suppliers.AddRange(suppliers);
        await context.SaveChangesAsync();

        foreach (var s in suppliers)
        {
            var end = s.Status == "Expiré" ? today.AddDays(-10) : today.AddMonths(6 + suppliers.IndexOf(s) % 12);
            context.SupplierContracts.Add(new SupplierContract
            {
                SupplierId = s.Id,
                ContractNumber = $"CTR-{s.Code}",
                StartDate = today.AddYears(-1),
                EndDate = end,
                Description = $"Contrat cadre {s.Category}",
                TotalValue = 5_000_000 + suppliers.IndexOf(s) * 500_000,
                Status = end < today ? "Expiré" : "Actif",
                Building = s.Building,
                IsSynced = true
            });
        }

        await context.SaveChangesAsync();

        for (var i = 5; i >= 0; i--)
        {
            var m = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            foreach (var s in suppliers)
            {
                var paid = s.Code != "FRN-005" || i > 0;
                context.SupplierPayments.Add(new SupplierPayment
                {
                    SupplierId = s.Id,
                    Amount = 280_000 + (suppliers.IndexOf(s) * 45_000) + (i * 20_000),
                    PaymentDate = m.AddDays(15),
                    DueDate = m.AddDays(10),
                    InvoiceReference = $"FAC-{s.Code}-{m:yyyyMM}",
                    Description = $"Prestation {s.Category} — {m:MMMM yyyy}",
                    Category = s.Category,
                    Notes = paid ? null : "Relance envoyée",
                    IsPaid = paid,
                    IsSynced = true
                });
            }
        }

        context.SupplierPayments.Add(new SupplierPayment
        {
            SupplierId = suppliers[4].Id,
            Amount = 890_000,
            PaymentDate = today.AddDays(-5),
            DueDate = today.AddDays(-12),
            InvoiceReference = "FAC-FRN-005-IMPAYE",
            Description = "Nettoyage étages 2-3",
            Category = "Nettoyage",
            IsPaid = false,
            IsSynced = true
        });
    }

    private static Supplier Sup(string code, string name, string category, string serviceType, string status,
        string contact, string phone, string email, string building, string taxId) => new()
    {
        Code = code,
        Name = name,
        Category = category,
        ServiceType = serviceType,
        Status = status,
        ContactName = contact,
        Phone = phone,
        Email = email,
        Building = building,
        TaxId = taxId,
        Address = "12 Avenue du Commerce, Kinshasa",
        IsSynced = true
    };

    private static async Task EnrichSuppliersAsync(SmartBuildingDbContext context)
    {
        var suppliers = await context.Suppliers.ToListAsync();
        foreach (var s in suppliers)
        {
            if (string.IsNullOrWhiteSpace(s.Code))
                s.Code = $"FRN-{s.Id.ToString()[..6].ToUpper()}";
            if (string.IsNullOrWhiteSpace(s.Category))
                s.Category = "Services";
            if (string.IsNullOrWhiteSpace(s.Status))
                s.Status = "Actif";
            if (string.IsNullOrWhiteSpace(s.ServiceType))
                s.ServiceType = "Prestation";
        }

        if (suppliers.Count > 0 && !await context.SupplierContracts.AnyAsync())
        {
            var today = DateTime.Today;
            foreach (var s in suppliers)
            {
                context.SupplierContracts.Add(new SupplierContract
                {
                    SupplierId = s.Id,
                    ContractNumber = $"CTR-{s.Code}",
                    StartDate = today.AddYears(-1),
                    EndDate = today.AddMonths(6),
                    Description = "Contrat enrichi",
                    TotalValue = 2_000_000,
                    Status = "Actif",
                    Building = "Tour SBMS",
                    IsSynced = true
                });
            }
        }
    }

    private static async Task SeedInventoryAsync(SmartBuildingDbContext context)
    {
        if (await context.InventoryItems.AnyAsync())
            return;

        var today = DateTime.Today;
        var items = new List<InventoryItem>
        {
            Inv("INV-001", "Groupe électrogène 250 KVA", "Générateur", "Sous-sol", "Opérationnel", "Paul Ngoy", "Caterpillar", "C250D5", "CAT-250-88921", 85_000_000, "6 ans", today.AddMonths(-3), today.AddMonths(3)),
            Inv("INV-002", "Centrale climatisation toiture", "Climatisation", "Toiture", "Opérationnel", "Patrick Mwamba", "Daikin", "VRV-IV", "DK-VRV-4421", 120_000_000, "4 ans", today.AddMonths(-2), today.AddMonths(4)),
            Inv("INV-003", "Ascenseur principal Tour A", "Ascenseur", "Hall principal", "Maintenance", "Sophie Martin", "Otis", "Gen2", "OT-ASC-1002", 45_000_000, "8 ans", today.AddMonths(-1), today.AddDays(5)),
            Inv("INV-004", "Pompe surpresseur chaufferie", "Plomberie", "Sous-sol", "Opérationnel", "Grace Mutombo", "Grundfos", "CR 64", "GR-PMP-778", 12_500_000, "3 ans", today.AddMonths(-4), today.AddMonths(2)),
            Inv("INV-005", "Système CCTV RDC", "Caméras sécurité", "Réception", "Opérationnel", "Marie Tshisekedi", "Hikvision", "DS-7732", "HK-CCTV-901", 8_900_000, "2 ans", today.AddMonths(-6), today.AddMonths(6)),
            Inv("INV-006", "Chaudière gaz collective", "Plomberie", "Sous-sol", "Hors service", "Grace Mutombo", "Viessmann", "Vitodens 200", "VS-CH-554", 28_000_000, "9 ans", today.AddMonths(-8), today.AddDays(-12)),
            Inv("INV-007", "Transformateur 800 kVA", "Électricité", "Salle technique", "Opérationnel", "Jean Kabongo", "Schneider", "Trihal", "SC-TR-800", 95_000_000, "10 ans", today.AddMonths(-5), today.AddMonths(7)),
            Inv("INV-008", "Contrôle accès parking B2", "Équipement sécurité", "Parking", "Opérationnel", "Marie Tshisekedi", "Bosch", "AMS", "BS-ACC-221", 4_200_000, "18 mois", today.AddMonths(-2), today.AddMonths(10)),
            Inv("INV-009", "Ventilation parking B1/B2", "Climatisation", "Parking", "Opérationnel", "Patrick Mwamba", "Soler & Palau", "TD-500", "SP-VENT-33", 6_800_000, "4 ans", today.AddMonths(-3), today.AddMonths(5)),
            Inv("INV-010", "Onduleur salle technique IT", "Équipement informatique", "Salle technique", "Maintenance", "Jean Kabongo", "APC", "Symmetra LX", "APC-UPS-88", 18_000_000, "3 ans", today.AddMonths(-1), today.AddDays(8)),
            Inv("INV-011", "Portail automatique entrée", "Équipement sécurité", "Hall principal", "Opérationnel", "Marie Tshisekedi", "CAME", "F7000", "CM-PRT-12", 3_500_000, "5 ans", today.AddMonths(-7), today.AddMonths(5)),
            Inv("INV-012", "Détection incendie tous étages", "Sécurité", "Sous-sol", "Opérationnel", "Paul Ngoy", "Siemens", "FC721", "SI-FIRE-01", 22_000_000, "7 ans", today.AddMonths(-4), today.AddMonths(8)),
            Inv("INV-013", "Switch réseau cœur 48 ports", "Matériel réseau", "Salle technique", "Opérationnel", "Jean Kabongo", "Cisco", "C9300", "CS-NET-48", 15_600_000, "2 ans", today.AddMonths(-5), today.AddMonths(7)),
            Inv("INV-014", "Serveur virtualisation", "Équipement informatique", "Salle technique", "Critique", "Jean Kabongo", "Dell", "PowerEdge R740", "DL-SRV-01", 42_000_000, "3 ans", today.AddMonths(-2), today.AddDays(-3)),
            Inv("INV-015", "Mobilier bureau réception", "Mobilier administratif", "Réception", "Opérationnel", "Admin Principal", "Steelcase", "Series 1", "—", 4_800_000, "4 ans", today.AddYears(-1), today.AddYears(2)),
            Inv("INV-016", "Kit outils maintenance", "Outils maintenance", "Réserve", "Opérationnel", "Paul Ngoy", "Facom", "Pro Kit", "—", 2_200_000, "—", today.AddMonths(-6), today.AddMonths(6)),
            Inv("INV-017", "Consommables nettoyage (lot)", "Consommables internes", "Réserve", "Opérationnel", "David Kasongo", "—", "—", "—", 850_000, "—", today.AddMonths(-1), today.AddMonths(2)),
            Inv("INV-018", "Tableau électrique R+1", "Matériel électrique", "Bureau administratif", "Opérationnel", "Jean Kabongo", "Schneider", "Prisma", "SC-PR-101", 6_400_000, "6 ans", today.AddMonths(-9), today.AddMonths(3)),
            Inv("INV-019", "Robinetterie sanitaire bloc B", "Équipement plomberie", "Sous-sol", "Opérationnel", "Grace Mutombo", "Grohe", "Eurosmart", "—", 3_100_000, "5 ans", today.AddMonths(-10), today.AddMonths(4)),
            Inv("INV-020", "Échelle télescopique toiture", "Matériel technique", "Toiture", "Opérationnel", "Paul Ngoy", "Zarges", "TP4", "ZG-TP4-02", 1_800_000, "2 ans", today.AddMonths(-12), today.AddMonths(12)),
            Inv("INV-021", "Armoire extincteurs RDC", "Équipement sécurité", "Hall principal", "Opérationnel", "Paul Ngoy", "Minimax", "CO2", "—", 2_500_000, "—", today.AddMonths(-4), today.AddMonths(8)),
            Inv("INV-022", "Projecteur salle réunion 3e", "Équipement bâtiment", "Bureau administratif", "Opérationnel", "Admin Principal", "Epson", "EB-L200", "EP-PRJ-305", 3_600_000, "18 mois", today.AddMonths(-3), today.AddMonths(9)),
            Inv("INV-023", "Détecteur fuite sous-sol", "Plomberie", "Sous-sol", "Critique", "Grace Mutombo", "Honeywell", "WLD", "HW-WLD-09", 1_200_000, "1 an", today.AddMonths(-1), today.AddDays(2)),
            Inv("INV-024", "Routeur secours WAN", "Réseau", "Salle technique", "Opérationnel", "Jean Kabongo", "Fortinet", "FG-60F", "FT-WAN-60", 8_500_000, "2 ans", today.AddMonths(-2), today.AddMonths(10)),
        };

        context.InventoryItems.AddRange(items);
        await context.SaveChangesAsync();

        foreach (var item in items)
        {
            context.InventoryMaintenanceRecords.AddRange(
                InvMaint(item.Id, today.AddMonths(-3), today.AddMonths(-3).AddDays(2), "Maintenance préventive", 420_000, "Paul Ngoy", "Maintenance"),
                InvMaint(item.Id, today.AddMonths(-8), today.AddMonths(-8).AddDays(1), "Contrôle réglementaire", 280_000, "Paul Ngoy", "Maintenance"),
                InvMaint(item.Id, today.AddMonths(1), null, "Maintenance planifiée", 350_000, "Paul Ngoy", "Maintenance"));

            if (item.Status is "Critique" or "Hors service")
            {
                context.InventoryMaintenanceRecords.Add(
                    InvMaint(item.Id, today.AddDays(-5), today.AddDays(-3), "Intervention corrective urgente", 680_000, "Paul Ngoy", "Intervention"));
            }
        }

        for (var i = 5; i >= 0; i--)
        {
            var m = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            context.InventoryMaintenanceRecords.Add(InvMaint(
                items[0].Id, m.AddDays(8), m.AddDays(10),
                $"Contrôle groupe électrogène — {m:MMMM yyyy}", 380_000 + i * 25_000, "Paul Ngoy", "Maintenance"));
        }
    }

    private static InventoryItem Inv(string code, string name, string category, string location, string status,
        string responsible, string brand, string model, string serial, decimal value, string usage,
        DateTime lastMaint, DateTime nextMaint) => new()
    {
        Code = code,
        Name = name,
        Category = category,
        Location = location,
        Building = "Tour SBMS",
        Status = status,
        Responsible = responsible,
        Brand = brand,
        Model = model,
        SerialNumber = serial,
        EstimatedValue = value,
        UnitValue = value,
        Quantity = 1,
        UsageDuration = usage,
        Condition = status == "Hors service" ? "Défaillant" : "Bon",
        LastMaintenanceDate = lastMaint,
        NextMaintenanceDate = nextMaint,
        IsSynced = true
    };

    private static InventoryMaintenanceRecord InvMaint(Guid itemId, DateTime scheduled, DateTime? completed,
        string desc, decimal cost, string tech, string type) => new()
    {
        InventoryItemId = itemId,
        ScheduledDate = scheduled,
        CompletedDate = completed,
        Description = desc,
        Cost = cost,
        Technician = tech,
        RecordType = type,
        IsSynced = true
    };

    private static async Task EnrichInventoryAsync(SmartBuildingDbContext context)
    {
        var items = await context.InventoryItems.Include(i => i.MaintenanceRecords).ToListAsync();
        foreach (var i in items)
        {
            if (string.IsNullOrWhiteSpace(i.Status))
                i.Status = "Opérationnel";
            if (string.IsNullOrWhiteSpace(i.Building))
                i.Building = "Tour SBMS";
            if (i.EstimatedValue <= 0 && i.UnitValue > 0)
                i.EstimatedValue = i.UnitValue * Math.Max(i.Quantity, 1);
            if (!i.LastMaintenanceDate.HasValue)
                i.LastMaintenanceDate = DateTime.Today.AddMonths(-3);
            if (!i.NextMaintenanceDate.HasValue)
                i.NextMaintenanceDate = DateTime.Today.AddMonths(3);
        }

        if (items.Count > 0 && !await context.InventoryMaintenanceRecords.AnyAsync())
        {
            var today = DateTime.Today;
            foreach (var item in items.Take(8))
            {
                context.InventoryMaintenanceRecords.Add(
                    InvMaint(item.Id, today.AddMonths(-2), today.AddMonths(-2).AddDays(1),
                        "Enrichissement historique maintenance", 250_000, "Paul Ngoy", "Maintenance"));
            }
        }
    }

    private static async Task SeedConsumptionAsync(SmartBuildingDbContext context)
    {
        if (await context.ConsumptionRecords.AnyAsync())
            return;

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var records = new List<ConsumptionRecord>();

        void AddMonth(int monthsAgo, ConsumptionType type, string equip, decimal qty, string unit, decimal cost,
            string status, decimal variation, bool anomaly = false)
        {
            var end = monthStart.AddMonths(-monthsAgo);
            var start = end.AddDays(-28);
            records.Add(new ConsumptionRecord
            {
                Type = type,
                PeriodStart = start,
                PeriodEnd = end,
                Quantity = qty,
                Unit = unit,
                Cost = cost,
                Currency = "FC",
                Building = "Tour SBMS",
                EquipmentSource = equip,
                Responsible = "Paul Ngoy",
                Status = status,
                PeriodType = "Mensuel",
                VariationPercent = variation,
                IsAnomaly = anomaly,
                MeterReference = $"MTR-{type}-{end:yyyyMM}",
                IsSynced = true
            });
        }

        for (var m = 0; m < 12; m++)
        {
            var factor = 1m + m * 0.02m;
            AddMonth(m, ConsumptionType.Electricite, "Compteur principal — Tour SBMS", 48_500 + m * 1200, "kWh", 4_850_000 * factor, m == 0 ? "Élevé" : "Normal", m == 0 ? 14.2m : 3 + m);
            AddMonth(m, ConsumptionType.Eau, "Compteur eau RDC + étages", 820 + m * 15, "m³", 1_240_000 * factor, "Normal", 2 + m * 0.5m);
            AddMonth(m, ConsumptionType.Carburant, "Groupe électrogène 250 KVA", 950 - m * 20, "L", 2_280_000 * factor, m == 0 ? "Critique" : "Normal", m == 0 ? 22m : 5m, m == 0);
            AddMonth(m, ConsumptionType.Internet, "Fibre Orange Business", 2500, "GB", 890_000, "Normal", 0);
            AddMonth(m, ConsumptionType.Climatisation, "Centrale toiture Zone A", 12_400 + m * 300, "kWh", 3_100_000 * factor, m == 1 ? "Élevé" : "Normal", 8 + m);
            AddMonth(m, ConsumptionType.Eclairage, "Éclairage communs + parking", 6_200, "kWh", 620_000, "Normal", 1.5m);
            AddMonth(m, ConsumptionType.GroupeElectrogene, "Groupe secours sous-sol", 420, "L", 1_050_000, m == 0 ? "Élevé" : "Normal", 11m, m == 0);
            AddMonth(m, ConsumptionType.ReseauTechnique, "Baie réseau & switches", 180, "GB", 450_000, "Normal", 0);
        }

        AddMonth(0, ConsumptionType.Energie, "Audit énergétique global", 1, "kWh", 1_800_000, "Normal", 0);
        AddMonth(0, ConsumptionType.Electricite, "Ascenseurs (2 cabines)", 8_400, "kWh", 840_000, "Normal", 4.5m);
        AddMonth(0, ConsumptionType.Eau, "Arrosage toiture", 45, "m³", 95_000, "Économie", -12m);
        AddMonth(0, ConsumptionType.Internet, "Backup satellite", 120, "GB", 450, "Normal", 0);
        records[^1].Currency = "USD";
        records[^1].Unit = "USD";

        context.ConsumptionRecords.AddRange(records);
        await context.SaveChangesAsync();
    }

    private static async Task EnrichConsumptionAsync(SmartBuildingDbContext context)
    {
        var records = await context.ConsumptionRecords.ToListAsync();
        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.Building))
                r.Building = "Tour SBMS";
            if (string.IsNullOrWhiteSpace(r.Status))
                r.Status = "Normal";
            if (string.IsNullOrWhiteSpace(r.EquipmentSource))
                r.EquipmentSource = r.Type.ToString();
            if (string.IsNullOrWhiteSpace(r.Responsible))
                r.Responsible = "Paul Ngoy";
            if (string.IsNullOrWhiteSpace(r.PeriodType))
                r.PeriodType = "Mensuel";
            if (string.IsNullOrWhiteSpace(r.Currency))
                r.Currency = "FC";
        }
    }

    private static async Task SeedIncidentsAsync(SmartBuildingDbContext context)
    {
        if (await context.Incidents.AnyAsync())
            return;

        var today = DateTime.Today;
        var rnd = new Random(42);
        var types = new[]
        {
            ("Incendie", "Détecteur fumée déclenché — Hall principal", "Hall principal", IncidentSeverity.Critique, IncidentStatus.EnCours),
            ("Intrusion", "Tentative accès parking B2", "Parking", IncidentSeverity.Elevee, IncidentStatus.EnCours),
            ("Vol", "Effraction local réserve", "Réserve", IncidentSeverity.Elevee, IncidentStatus.Ouvert),
            ("Panne électrique", "Coupure secteur aile ouest", "Bureau administratif", IncidentSeverity.Elevee, IncidentStatus.EnCours),
            ("Fuite plomberie", "Fuite canalisation sous-sol", "Sous-sol", IncidentSeverity.Moyenne, IncidentStatus.EnCours),
            ("Problème réseau", "Perte connectivité baie IT", "Salle technique", IncidentSeverity.Moyenne, IncidentStatus.InterventionProgrammee),
            ("Climatisation", "Centrale clim — surchauffe", "Toiture", IncidentSeverity.Moyenne, IncidentStatus.EnCours),
            ("Générateur", "Démarrage auto groupe électrogène", "Sous-sol", IncidentSeverity.Faible, IncidentStatus.Resolu),
            ("Caméras sécurité", "Caméra 12 hors ligne", "Parking", IncidentSeverity.Moyenne, IncidentStatus.EnCours),
            ("Ascenseur", "Blocage cabine étage 3", "Ascenseur", IncidentSeverity.Elevee, IncidentStatus.EnCours),
            ("Accident", "Chute visiteur hall", "Hall principal", IncidentSeverity.Moyenne, IncidentStatus.Resolu),
            ("Court-circuit", "Disjoncteur R+2 déclenché", "Bureau administratif", IncidentSeverity.Elevee, IncidentStatus.Resolu),
        };

        var incidents = new List<Incident>();
        var idx = 1;
        foreach (var (type, title, loc, sev, status) in types)
        {
            var reported = today.AddDays(-rnd.Next(1, 45)).AddHours(-rnd.Next(0, 12));
            var resolved = status is IncidentStatus.Resolu or IncidentStatus.Cloture ? reported.AddHours(rnd.Next(4, 72)) : (DateTime?)null;
            var cost = sev switch
            {
                IncidentSeverity.Critique => 2_500_000 + rnd.Next(500_000),
                IncidentSeverity.Elevee => 800_000 + rnd.Next(400_000),
                IncidentSeverity.Moyenne => 250_000 + rnd.Next(150_000),
                _ => 80_000 + rnd.Next(50_000)
            };

            var inc = new Incident
            {
                Code = $"INC-{today:yyyy}-{idx:D3}",
                Title = title,
                Description = $"{title}. Signalement automatique système SBMS.",
                IncidentType = type,
                Severity = sev,
                Status = status,
                Location = loc,
                Building = "Tour SBMS",
                Responsible = idx % 2 == 0 ? "Paul Ngoy" : "Marie Tshisekedi",
                RiskLevel = sev == IncidentSeverity.Critique ? "Critique" : sev == IncidentSeverity.Elevee ? "Élevé" : "Moyen",
                ReportedAt = reported,
                ResolvedAt = resolved,
                Cost = status == IncidentStatus.Resolu ? cost : cost * 0.6m,
                HasPhoto = idx % 3 == 0,
                ResolutionNotes = resolved.HasValue ? "Intervention terminée — zone sécurisée." : null,
                IsSynced = true
            };
            incidents.Add(inc);
            idx++;
        }

        for (var m = 0; m < 8; m++)
        {
            var reported = today.AddMonths(-m).AddDays(-rnd.Next(1, 20));
            incidents.Add(new Incident
            {
                Code = $"INC-{reported:yyyyMM}-{idx:D3}",
                Title = "Maintenance préventive sécurité",
                Description = "Contrôle équipements sécurité programmé.",
                IncidentType = "Sécurité",
                Severity = IncidentSeverity.Faible,
                Status = IncidentStatus.Resolu,
                Location = "Salle technique",
                Building = "Tour SBMS",
                Responsible = "Paul Ngoy",
                RiskLevel = "Faible",
                ReportedAt = reported,
                ResolvedAt = reported.AddDays(2),
                Cost = 180_000,
                IsSynced = true
            });
            idx++;
        }

        context.Incidents.AddRange(incidents);
        await context.SaveChangesAsync();

        foreach (var inc in incidents)
        {
            if (inc.Status is IncidentStatus.Resolu or IncidentStatus.Cloture)
            {
                context.IncidentInterventions.Add(new IncidentIntervention
                {
                    IncidentId = inc.Id,
                    Technician = "Paul Ngoy",
                    InterventionType = "Corrective",
                    StartedAt = inc.ReportedAt.AddHours(1),
                    EndedAt = inc.ResolvedAt,
                    Cost = inc.Cost * 0.8m,
                    Status = "Terminée",
                    Result = "Problème résolu",
                    IsSynced = true
                });
            }
            else if (inc.Status is IncidentStatus.EnCours or IncidentStatus.InterventionProgrammee)
            {
                context.IncidentInterventions.Add(new IncidentIntervention
                {
                    IncidentId = inc.Id,
                    Technician = "Patrick Mwamba",
                    InterventionType = inc.Status == IncidentStatus.InterventionProgrammee ? "Planifiée" : "Urgente",
                    StartedAt = inc.ReportedAt.AddHours(2),
                    EndedAt = null,
                    Cost = 0,
                    Status = "En cours",
                    Result = "Intervention en cours",
                    IsSynced = true
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnrichIncidentsAsync(SmartBuildingDbContext context)
    {
        var incidents = await context.Incidents.ToListAsync();
        foreach (var i in incidents)
        {
            if (string.IsNullOrWhiteSpace(i.Code))
                i.Code = $"INC-{i.Id.ToString()[..6].ToUpper()}";
            if (string.IsNullOrWhiteSpace(i.IncidentType))
                i.IncidentType = "Autre";
            if (string.IsNullOrWhiteSpace(i.Building))
                i.Building = "Tour SBMS";
            if (string.IsNullOrWhiteSpace(i.Responsible))
                i.Responsible = "Paul Ngoy";
        }
    }

    private static async Task EnrichTechnicalEquipmentAsync(SmartBuildingDbContext context)
    {
        var equipment = await context.Equipment.ToListAsync();
        foreach (var e in equipment)
        {
            if (string.IsNullOrWhiteSpace(e.Brand) && e.Code == "GEN-250KVA-001")
            {
                e.Brand = "Caterpillar";
                e.Model = "C250D5";
                e.SerialNumber = "CAT-250-88921";
                e.PowerSpec = "250 KVA";
                e.VoltageSpec = "400 V";
                e.FrequencySpec = "50 Hz";
                e.FuelType = "Diesel";
                e.OperatingHours = "1 250 h";
            }
            else if (string.IsNullOrWhiteSpace(e.Brand))
            {
                e.Brand = "—";
                e.Model = "Standard";
            }

            if (e.LastMaintenanceDate is null)
                e.LastMaintenanceDate = DateTime.Today.AddMonths(-3);
            if (e.NextMaintenanceDate is null)
                e.NextMaintenanceDate = DateTime.Today.AddMonths(3);
        }

    }

    private static async Task SeedFinanceAsync(SmartBuildingDbContext context)
    {
        // Pas de données fictives : les recettes proviennent de Locations (loyers, cautions).
        // Les dépenses sont saisies manuellement dans le module Finances.
        await Task.CompletedTask;
    }

    private static async Task EnrichFinanceDataAsync(SmartBuildingDbContext context)
    {
        var txs = await context.FinancialTransactions.ToListAsync();
        foreach (var t in txs)
        {
            if (string.IsNullOrWhiteSpace(t.Source))
                t.Source = ResolveFinanceSource(t.Category);
            if (string.IsNullOrWhiteSpace(t.PaymentMethod))
                t.PaymentMethod = "Virement";
            if (string.IsNullOrWhiteSpace(t.Status))
                t.Status = "Payé";
            if (string.IsNullOrWhiteSpace(t.RecordedBy))
                t.RecordedBy = "Admin Principal";
        }

    }

    private static string ResolveFinanceSource(string category) => category switch
    {
        var c when c.Contains("Loyer", StringComparison.OrdinalIgnoreCase) || c.Contains("Caution", StringComparison.OrdinalIgnoreCase) || c.Contains("Remboursement", StringComparison.OrdinalIgnoreCase) => "Locations",
        var c when c.Contains("Salaire", StringComparison.OrdinalIgnoreCase) => "Personnel",
        var c when c.Contains("Maintenance", StringComparison.OrdinalIgnoreCase) || c.Contains("Sécurité", StringComparison.OrdinalIgnoreCase) => "Technique",
        var c when c.Contains("Énergie", StringComparison.OrdinalIgnoreCase) || c.Contains("Energie", StringComparison.OrdinalIgnoreCase) => "Consommations",
        var c when c.Contains("Facture", StringComparison.OrdinalIgnoreCase) => "Fournisseurs",
        _ => "Général"
    };

    private static Premise Premise(string code, string name, string building, string floor, string type,
        decimal area, decimal rent, bool occupied) => new()
    {
        Code = code,
        Name = name,
        Building = building,
        Floor = floor,
        PremiseType = type,
        AreaSqM = area,
        MonthlyRent = rent,
        IsOccupied = occupied,
        Description = $"{type} — {area} m²",
        IsSynced = true
    };

    private static Tenant Tenant(string company, string name, string phone, string email) => new()
    {
        Name = name,
        Company = company,
        Phone = phone,
        Email = email,
        Address = "12 Avenue du Commerce, Kinshasa, RDC",
        TenantCategory = "Professionnel",
        MaritalStatus = "—",
        Gender = "",
        IsSynced = true
    };

    private static LeaseContract Contract(
        Guid premiseId, Guid tenantId, string number,
        DateTime start, DateTime end, decimal rent, decimal deposit, LeaseStatus status) => new()
    {
        PremiseId = premiseId,
        TenantId = tenantId,
        ContractNumber = number,
        StartDate = start.Date,
        EndDate = end.Date,
        MonthlyRent = rent,
        Deposit = deposit,
        Status = status,
        IsSynced = true
    };

    private static Employee Employee(string matricule, string first, string last, string dept, string position,
        decimal salary, DateTime hireDate, string contractType, DateTime? contractEnd, string supervisor) => new()
    {
        Matricule = matricule,
        FirstName = first,
        LastName = last,
        Department = dept,
        Position = position,
        BaseSalary = salary,
        HireDate = hireDate,
        Email = $"{first.ToLower()}.{last.ToLower()}@sbms.local",
        Phone = $"+243 81 {Random.Shared.Next(100, 999):D3} {Random.Shared.Next(1000, 9999):D4}",
        Address = "12 Av. du Commerce, Kinshasa",
        Gender = first is "Sophie" or "Amina" or "Claire" ? "Féminin" : "Masculin",
        BirthDate = hireDate.AddYears(-28),
        NationalId = $"CNI-{matricule[^4..]}",
        MaritalStatus = first is "Sophie" or "Marc" ? "Marié(e)" : "Célibataire",
        EmergencyContactName = "Contact urgence",
        EmergencyContactPhone = "+243 99 000 0000",
        Notes = "Employé SBMS — données de démonstration.",
        ContractNumber = $"CTR-{matricule}",
        ContractType = contractType,
        ContractStartDate = hireDate,
        ContractEndDate = contractEnd,
        Supervisor = supervisor,
        WorkSchedule = "Lun–Ven 8h–17h",
        IsActive = true,
        IsSynced = true
    };

    private static Attendance Attendance(Employee emp, DateTime date, DateTime? checkIn, DateTime? checkOut, string? notes) =>
        new()
        {
            EmployeeId = emp.Id,
            Date = date.Date,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Notes = notes,
            IsSynced = true
        };

    private static async Task SeedVisitorsAsync(SmartBuildingDbContext context)
    {
        if (await context.Visitors.AnyAsync())
            return;

        var now = DateTime.Now;
        var today = DateTime.Today;

        var visitors = new List<Visitor>
        {
            Vis("VIS-20260522-001", "Jean-Pierre Mukendi", "Société ABC", "+243 81 234 5678", "Marie Tshisekedi", "Réunion commerciale", "Réunion", "Actif", "Réception", today.AddHours(-2), null, "B-1042"),
            Vis("VIS-20260522-002", "Grace Mutombo", "—", "+243 99 112 3344", "Admin Principal", "Livraison matériel", "Livraison", "Actif", "Parking", today.AddHours(-1), null, "B-1043"),
            Vis("VIS-20260522-003", "Patrick Mwamba", "TechServ SARL", "+243 82 445 6677", "Jean Kabongo", "Maintenance climatisation", "Maintenance", "Actif", "Salle technique", today.AddMinutes(-45), null, "B-1044"),
            Vis("VIS-20260521-004", "Sophie Martin", "Audit Plus", "+243 97 888 2211", "Paul Ngoy", "Audit sécurité", "Audit", "Sorti", "Bureau administratif", today.AddDays(-1).AddHours(9), today.AddDays(-1).AddHours(11), "B-1038"),
            Vis("VIS-20260521-005", "David Kasongo", "—", "+243 90 556 7788", "Réception SBMS", "Visite famille locataire", "Autre", "Sorti", "Hall principal", today.AddDays(-1).AddHours(14), today.AddDays(-1).AddHours(16), "B-1039"),
            Vis("VIS-20260520-006", "Amélie Dubois", "Fournisseur EU", "+33 6 12 34 56 78", "Sophie Martin", "Négociation contrat", "Réunion", "Sorti", "Salle réunion", today.AddDays(-2).AddHours(10), today.AddDays(-2).AddHours(12), "B-1035"),
            Vis("VIS-20260520-007", "Inconnu Test", "—", "—", "—", "Tentative accès", "Autre", "Refusé", "Zone sécurisée", today.AddDays(-2).AddHours(15), today.AddDays(-2).AddHours(15), null),
            Vis("VIS-20260519-008", "Paul Ngoy", "SBMS Interne", "+243 81 000 1111", "Marie Tshisekedi", "Inspection parking", "Visite technique", "Sorti", "Parking", today.AddDays(-3).AddHours(8), today.AddDays(-3).AddHours(9), "B-1030"),
            Vis("VIS-20260519-009", "Fatou Diallo", "Prestataire Net", "+243 84 333 9900", "David Kasongo", "Nettoyage RDC", "Prestataire", "Sorti", "Sous-sol", today.AddDays(-3).AddHours(6), today.AddDays(-3).AddHours(8), "B-1031"),
            Vis("VIS-20260518-010", "Marc Lambert", "—", "+243 85 777 4422", "Admin Principal", "Dépôt dossier", "Autre", "En attente", "Réception", today.AddHours(-0.5), null, null),
            Vis("VIS-20260517-011", "Claire Nsimba", "Banque BOA", "+243 81 909 8877", "Sophie Martin", "Réunion finance", "Réunion", "Sorti", "Bureau administratif", today.AddDays(-5).AddHours(11), today.AddDays(-5).AddHours(13), "B-1025"),
            Vis("VIS-20260517-012", "Eric Kabila", "—", "+243 99 444 5566", "Réception SBMS", "Candidat entretien", "Autre", "Sorti", "Réception", today.AddDays(-5).AddHours(9), today.AddDays(-5).AddHours(10), "B-1026"),
        };

        context.Visitors.AddRange(visitors);

        var appointments = new List<VisitorAppointment>
        {
            Appt("Jean Mukendi", "Marie Tshisekedi", "Signature bail", today.AddHours(2), "Salle réunion A", "Confirmé", 90),
            Appt("Grace Mutombo", "Paul Ngoy", "Point technique", today.AddHours(4), "Salle technique", "Confirmé", 60),
            Appt("Audit Plus", "Admin Principal", "Revue trimestrielle", today.AddDays(1).AddHours(10), "Bureau administratif", "En attente", 120),
            Appt("Patrick Mwamba", "Jean Kabongo", "Maintenance ascenseur", today.AddDays(1).AddHours(14), "Hall principal", "Confirmé", 45),
            Appt("Fournisseur EU", "Sophie Martin", "Livraison équipement", today.AddDays(2).AddHours(9), "Parking", "En attente", 30),
            Appt("Claire Nsimba", "Sophie Martin", "Comité finance", today.AddDays(3).AddHours(11), "Salle réunion B", "Confirmé", 60),
        };

        context.VisitorAppointments.AddRange(appointments);
        await context.SaveChangesAsync();
    }

    private static async Task SeedEmailsAsync(SmartBuildingDbContext context)
    {
        if (await context.CachedEmails.AnyAsync())
            return;

        var now = DateTime.Now;
        var today = DateTime.Today;
        var adminId = await context.Users.Select(u => u.Id).FirstOrDefaultAsync();

        if (!await context.EmailAccounts.AnyAsync() && adminId != Guid.Empty)
        {
            context.EmailAccounts.Add(new EmailAccount
            {
                UserId = adminId,
                Provider = "Gmail",
                EmailAddress = "admin@sbms-building.cd",
                ImapHost = "imap.gmail.com",
                ImapPort = 993,
                SmtpHost = "smtp.gmail.com",
                SmtpPort = 587,
                EncryptedPassword = "",
                UseSsl = true,
                FilterKeywords = "maintenance,facture,sécurité,contrat",
                IsSynced = true
            });
        }

        var emails = new List<CachedEmail>
        {
            Mail("Urgent — Fuite sous-sol parking B2", "Grace Mutombo <grace.mutombo@plomberie.cd>", "admin@sbms-building.cd",
                "Bonjour, intervention urgente requise au sous-sol B2. Merci de confirmer disponibilité technicien.",
                "Maintenance", "Urgent", today.AddHours(-1), false, true, true, false),
            Mail("Facture maintenance ascenseur — Mars", "Otis Service <factures@otis.com>", "compta@sbms-building.cd",
                "Veuillez trouver ci-joint la facture de maintenance préventive Tour A.",
                "Fournisseurs", "Important", today.AddHours(-3), false, true, false, true),
            Mail("Rapport inspection sécurité RDC", "Paul Ngoy <p.ngoy@sbms.local>", "admin@sbms-building.cd",
                "Rapport hebdomadaire : 0 incident critique. 2 anomalies mineures hall principal.",
                "Sécurité", "Normal", today.AddHours(-5), true, false, false, false),
            Mail("Relance loyer — Bureau 304", "Sophie Martin <s.martin@locataire.cd>", "compta@sbms-building.cd",
                "Paiement loyer mars en attente. Merci de régulariser sous 48h.",
                "Finance", "Important", today.AddDays(-1).AddHours(9), false, false, false, true),
            Mail("Contrat nettoyage 2026 — signature", "David Kasongo <d.kasongo@prestataire.cd>", "admin@sbms-building.cd",
                "Contrat annuel joint pour validation direction.",
                "Contrats", "Normal", today.AddDays(-1).AddHours(14), true, true, true, false),
            Mail("Réclamation bruit ventilation", "Locataire Tour B <contact@tourb.cd>", "reception@sbms-building.cd",
                "Plainte récurrente bruit ventilation nocturne étage 4.",
                "Réclamations", "Important", today.AddDays(-2).AddHours(10), false, false, false, false),
            Mail("Support IT — VPN accès distant", "Jean Kabongo <j.kabongo@sbms.local>", "admin@sbms-building.cd",
                "Configuration VPN pour accès supervision GTB depuis site annexe.",
                "Support", "Normal", today.AddDays(-2).AddHours(16), true, false, false, false),
            Mail("Demande badge visiteur groupe", "Réception SBMS <reception@sbms-building.cd>", "admin@sbms-building.cd",
                "Groupe de 12 visiteurs demain 10h — préparer badges temporaires.",
                "Administration", "Normal", today.AddDays(-3).AddHours(8), true, false, false, false),
            Mail("Alerte caméra hall — mouvement", "Hikvision Alert <alert@hikvision.local>", "securite@sbms-building.cd",
                "Détection mouvement zone hall 02:14. Vérification recommandée.",
                "Sécurité", "Urgent", today.AddDays(-3).AddHours(2), false, true, false, false),
            Mail("Devis climatisation toiture", "Daikin Pro <devis@daikin.cd>", "p.ngoy@sbms.local",
                "Devis révision installation toiture — validité 30 jours.",
                "Fournisseurs", "Normal", today.AddDays(-4).AddHours(11), true, true, true, false),
            Mail("Brouillon — Compte-rendu AG", "Admin Principal <admin@sbms-building.cd>", "conseil@sbms-building.cd",
                "Ébauche compte-rendu assemblée générale copropriété.",
                "Administration", "Normal", today.AddHours(-2), false, false, false, false, isDraft: true),
            Mail("Newsletter fournisseur électricité", "Schneider Promo <news@schneider.com>", "admin@sbms-building.cd",
                "Offres Q2 équipements basse tension.",
                "Fournisseurs", "Normal", today.AddDays(-5), true, false, false, false, isSpam: true),
        };

        context.CachedEmails.AddRange(emails);
        await context.SaveChangesAsync();
    }

    private static CachedEmail Mail(string subject, string from, string to, string body, string category, string priority,
        DateTime received, bool isRead, bool important, bool attachments, bool awaiting, bool isDraft = false, bool isSpam = false) => new()
    {
        MessageId = Guid.NewGuid().ToString("N"),
        Subject = subject,
        FromAddress = from,
        ToAddresses = to,
        BodyPreview = body,
        BodyText = body + "\n\n—\nSmart Building Management System",
        ReceivedAt = received,
        IsRead = isRead,
        IsImportant = important,
        HasAttachments = attachments,
        AwaitingReply = awaiting,
        IsDraft = isDraft,
        IsSpam = isSpam,
        Folder = isDraft ? "DRAFT" : isSpam ? "SPAM" : "INBOX",
        Category = category,
        Priority = priority,
        AssignedTo = category switch
        {
            "Maintenance" => "Paul Ngoy",
            "Sécurité" => "Marie Tshisekedi",
            "Finance" => "Sophie Martin",
            _ => "Admin Principal"
        },
        Tags = $"{category},SBMS",
        IsSynced = true
    };

    private static Visitor Vis(string code, string name, string? company, string phone, string host, string purpose,
        string type, string status, string zone, DateTime checkIn, DateTime? checkOut, string? badge) => new()
    {
        VisitCode = code,
        FullName = name,
        Company = company == "—" ? null : company,
        Phone = phone == "—" ? null : phone,
        HostName = host == "—" ? "Réception SBMS" : host,
        Purpose = purpose,
        VisitType = type,
        AccessStatus = status,
        Zone = zone,
        Building = "Tour SBMS",
        AllowedZones = $"Réception,Hall principal,{zone}",
        CheckInAt = checkIn,
        CheckOutAt = checkOut,
        ExpectedCheckOutAt = checkOut ?? checkIn.AddHours(3),
        BadgeNumber = badge,
        IdDocumentType = "CNI",
        IsSynced = true
    };

    private static VisitorAppointment Appt(string visitor, string host, string purpose, DateTime at, string room, string status, int mins) => new()
    {
        VisitorName = visitor,
        HostName = host,
        Purpose = purpose,
        ScheduledAt = at,
        Room = room,
        Building = "Tour SBMS",
        Status = status,
        DurationMinutes = mins,
        IsSynced = true
    };

    private static FinancialTransaction Tx(TransactionType type, string category, string desc, decimal amount,
        DateTime date, string reference, string source, string paymentMethod, string status) => new()
    {
        Type = type,
        Category = category,
        Description = desc,
        Amount = amount,
        TransactionDate = date,
        Reference = reference,
        Source = source,
        PaymentMethod = paymentMethod,
        Status = status,
        RecordedBy = "Admin Principal",
        IsSynced = true
    };
}
