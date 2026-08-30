-- MariaDB dump 10.19  Distrib 10.4.32-MariaDB, for Win64 (AMD64)
--
-- Host: localhost    Database: sbms_local
-- ------------------------------------------------------
-- Server version	10.4.32-MariaDB

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `__efmigrationshistory`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `__efmigrationshistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `attendances`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `attendances` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `EmployeeId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Date` datetime(6) NOT NULL,
  `CheckIn` datetime(6) DEFAULT NULL,
  `CheckOut` datetime(6) DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `PresenceStatus` longtext NOT NULL,
  `LateMinutes` int(11) NOT NULL,
  `WorkedHours` decimal(65,30) NOT NULL,
  `OvertimeHours` decimal(65,30) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Attendances_EmployeeId` (`EmployeeId`),
  CONSTRAINT `FK_Attendances_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `employees` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `buildinginfos`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `buildinginfos` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` longtext NOT NULL,
  `OwnerType` longtext NOT NULL,
  `LegalRepresentative` longtext DEFAULT NULL,
  `Address` longtext NOT NULL,
  `City` longtext NOT NULL,
  `Country` longtext NOT NULL,
  `Phone` longtext NOT NULL,
  `SecondaryPhone` longtext DEFAULT NULL,
  `Email` longtext NOT NULL,
  `NationalId` longtext NOT NULL,
  `TaxId` longtext DEFAULT NULL,
  `Website` longtext NOT NULL,
  `BankName` longtext DEFAULT NULL,
  `BankAccount` longtext DEFAULT NULL,
  `BuildingDisplayName` longtext NOT NULL,
  `BuildingType` longtext NOT NULL,
  `TotalFloors` int(11) NOT NULL,
  `TotalPremises` int(11) NOT NULL,
  `ApartmentCount` int(11) NOT NULL,
  `CommercialUnitCount` int(11) NOT NULL,
  `TotalAreaSqM` decimal(65,30) NOT NULL,
  `ParkingSpaces` int(11) NOT NULL,
  `HasElevator` tinyint(1) NOT NULL,
  `YearBuilt` int(11) DEFAULT NULL,
  `EquipmentAndInstallations` longtext NOT NULL,
  `ManagementRules` longtext NOT NULL,
  `TimeZoneId` longtext NOT NULL,
  `Currency` longtext NOT NULL,
  `UsdExchangeRate` decimal(65,30) NOT NULL,
  `DateFormat` longtext NOT NULL,
  `Language` longtext NOT NULL,
  `TimeFormat` longtext NOT NULL,
  `MaintenanceMode` tinyint(1) NOT NULL,
  `LogoPath` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `buildings`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `buildings` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `LandlordId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `Code` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `Address` longtext NOT NULL,
  `FloorCount` int(11) NOT NULL,
  `PremiseCount` int(11) NOT NULL,
  `BuildingType` longtext NOT NULL,
  `Capacity` int(11) NOT NULL,
  `Status` longtext NOT NULL,
  `Equipment` longtext NOT NULL,
  `Zones` longtext NOT NULL,
  `PhotoPath` longtext DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Buildings_LandlordId` (`LandlordId`),
  CONSTRAINT `FK_Buildings_Landlords_LandlordId` FOREIGN KEY (`LandlordId`) REFERENCES `landlords` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cachedemails`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `cachedemails` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `AccountId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `MessageId` longtext NOT NULL,
  `Subject` longtext NOT NULL,
  `FromAddress` longtext NOT NULL,
  `ToAddresses` longtext NOT NULL,
  `CcAddresses` longtext DEFAULT NULL,
  `BodyPreview` longtext NOT NULL,
  `BodyHtml` longtext DEFAULT NULL,
  `BodyText` longtext DEFAULT NULL,
  `ReceivedAt` datetime(6) NOT NULL,
  `IsRead` tinyint(1) NOT NULL,
  `IsImportant` tinyint(1) NOT NULL,
  `IsArchived` tinyint(1) NOT NULL,
  `IsDraft` tinyint(1) NOT NULL,
  `IsSpam` tinyint(1) NOT NULL,
  `AwaitingReply` tinyint(1) NOT NULL,
  `HasAttachments` tinyint(1) NOT NULL,
  `AttachmentPaths` longtext DEFAULT NULL,
  `Folder` longtext NOT NULL,
  `Category` longtext NOT NULL,
  `Priority` longtext NOT NULL,
  `AssignedTo` longtext NOT NULL,
  `Tags` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `consumptionrecords`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `consumptionrecords` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Type` int(11) NOT NULL,
  `PeriodStart` datetime(6) NOT NULL,
  `PeriodEnd` datetime(6) NOT NULL,
  `Quantity` decimal(65,30) NOT NULL,
  `Unit` longtext NOT NULL,
  `Cost` decimal(65,30) NOT NULL,
  `Currency` longtext NOT NULL,
  `MeterReference` longtext DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `Building` longtext NOT NULL,
  `EquipmentSource` longtext NOT NULL,
  `Responsible` longtext NOT NULL,
  `Status` longtext NOT NULL,
  `PeriodType` longtext NOT NULL,
  `VariationPercent` decimal(65,30) NOT NULL,
  `IsAnomaly` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  `CustomTypeLabel` longtext DEFAULT NULL,
  `ExpenseMotif` longtext DEFAULT NULL,
  `PaidBy` longtext NOT NULL DEFAULT '',
  `ReimbursementStatus` longtext NOT NULL DEFAULT 'Non applicable',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `disciplinarynotes`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `disciplinarynotes` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `EmployeeId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Category` longtext NOT NULL,
  `Title` longtext NOT NULL,
  `Description` longtext NOT NULL,
  `OccurredAt` datetime(6) NOT NULL,
  `IssuedBy` longtext DEFAULT NULL,
  `Severity` int(11) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_DisciplinaryNotes_EmployeeId` (`EmployeeId`),
  CONSTRAINT `FK_DisciplinaryNotes_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `employees` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `emailaccounts`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `emailaccounts` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Provider` longtext NOT NULL,
  `EmailAddress` longtext NOT NULL,
  `ImapHost` longtext NOT NULL,
  `ImapPort` int(11) NOT NULL,
  `SmtpHost` longtext NOT NULL,
  `SmtpPort` int(11) NOT NULL,
  `EncryptedPassword` longtext NOT NULL,
  `UseSsl` tinyint(1) NOT NULL,
  `FilterKeywords` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `employees`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `employees` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Matricule` varchar(50) NOT NULL,
  `FirstName` longtext NOT NULL,
  `LastName` longtext NOT NULL,
  `Email` longtext NOT NULL,
  `Phone` longtext NOT NULL,
  `Position` longtext NOT NULL,
  `Department` longtext NOT NULL,
  `HireDate` datetime(6) NOT NULL,
  `BaseSalary` decimal(18,2) NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `RhStatus` longtext NOT NULL,
  `ProfilePhotoPath` longtext DEFAULT NULL,
  `ContractPdfPath` longtext DEFAULT NULL,
  `SuspendedUntil` datetime(6) DEFAULT NULL,
  `SuspensionReason` longtext DEFAULT NULL,
  `DismissedAt` datetime(6) DEFAULT NULL,
  `DismissalReason` longtext DEFAULT NULL,
  `Address` longtext NOT NULL,
  `Gender` varchar(20) NOT NULL,
  `BirthDate` datetime(6) DEFAULT NULL,
  `NationalId` varchar(50) NOT NULL,
  `MaritalStatus` longtext NOT NULL,
  `EmergencyContactName` longtext NOT NULL,
  `EmergencyContactPhone` longtext NOT NULL,
  `Notes` longtext NOT NULL,
  `ContractNumber` varchar(50) NOT NULL,
  `ContractType` varchar(30) NOT NULL,
  `ContractStartDate` datetime(6) DEFAULT NULL,
  `ContractEndDate` datetime(6) DEFAULT NULL,
  `Supervisor` longtext NOT NULL,
  `WorkSchedule` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Employees_Matricule` (`Matricule`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `equipment`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `equipment` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `Category` longtext NOT NULL,
  `Location` longtext NOT NULL,
  `Status` int(11) NOT NULL,
  `LastMaintenanceDate` datetime(6) DEFAULT NULL,
  `NextMaintenanceDate` datetime(6) DEFAULT NULL,
  `Brand` longtext NOT NULL,
  `Model` longtext NOT NULL,
  `SerialNumber` longtext NOT NULL,
  `InstallationDate` datetime(6) DEFAULT NULL,
  `PurchaseValue` decimal(65,30) NOT NULL,
  `WarrantyUntil` datetime(6) DEFAULT NULL,
  `PowerSpec` longtext NOT NULL,
  `VoltageSpec` longtext NOT NULL,
  `FrequencySpec` longtext NOT NULL,
  `FuelType` longtext NOT NULL,
  `OperatingHours` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `financialtransactions`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `financialtransactions` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Type` int(11) NOT NULL,
  `Category` longtext NOT NULL,
  `Description` longtext NOT NULL,
  `Amount` decimal(65,30) NOT NULL,
  `TransactionDate` datetime(6) NOT NULL,
  `Reference` longtext DEFAULT NULL,
  `RelatedEntityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `Source` longtext NOT NULL,
  `PaymentMethod` longtext NOT NULL,
  `Status` longtext NOT NULL,
  `RecordedBy` longtext NOT NULL,
  `RequiresPdgApproval` tinyint(1) NOT NULL,
  `ApprovedAt` datetime(6) DEFAULT NULL,
  `ApprovedBy` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `incidentinterventions`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `incidentinterventions` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `IncidentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Technician` longtext NOT NULL,
  `InterventionType` longtext NOT NULL,
  `StartedAt` datetime(6) NOT NULL,
  `EndedAt` datetime(6) DEFAULT NULL,
  `Cost` decimal(65,30) NOT NULL,
  `Status` longtext NOT NULL,
  `Result` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_IncidentInterventions_IncidentId` (`IncidentId`),
  CONSTRAINT `FK_IncidentInterventions_Incidents_IncidentId` FOREIGN KEY (`IncidentId`) REFERENCES `incidents` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `incidents`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `incidents` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` longtext NOT NULL,
  `Title` longtext NOT NULL,
  `Description` longtext NOT NULL,
  `IncidentType` longtext NOT NULL,
  `Severity` int(11) NOT NULL,
  `Status` int(11) NOT NULL,
  `Location` longtext NOT NULL,
  `Building` longtext NOT NULL,
  `Responsible` longtext NOT NULL,
  `RiskLevel` longtext NOT NULL,
  `ReportedAt` datetime(6) NOT NULL,
  `ResolvedAt` datetime(6) DEFAULT NULL,
  `Cost` decimal(65,30) NOT NULL,
  `ReportedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `ResolutionNotes` longtext DEFAULT NULL,
  `HasPhoto` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  `EquipmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Incidents_EquipmentId` (`EquipmentId`),
  CONSTRAINT `FK_Incidents_Equipment_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `equipment` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventoryitems`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `inventoryitems` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `Category` longtext NOT NULL,
  `Quantity` int(11) NOT NULL,
  `Location` longtext NOT NULL,
  `Condition` longtext NOT NULL,
  `UnitValue` decimal(65,30) NOT NULL,
  `Notes` longtext DEFAULT NULL,
  `Status` longtext NOT NULL,
  `Responsible` longtext NOT NULL,
  `SerialNumber` longtext NOT NULL,
  `Building` longtext NOT NULL,
  `Brand` longtext NOT NULL,
  `Model` longtext NOT NULL,
  `LastMaintenanceDate` datetime(6) DEFAULT NULL,
  `NextMaintenanceDate` datetime(6) DEFAULT NULL,
  `EstimatedValue` decimal(65,30) NOT NULL,
  `UsageDuration` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventorymaintenancerecords`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `inventorymaintenancerecords` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `InventoryItemId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ScheduledDate` datetime(6) NOT NULL,
  `CompletedDate` datetime(6) DEFAULT NULL,
  `Description` longtext NOT NULL,
  `Cost` decimal(65,30) NOT NULL,
  `Technician` longtext NOT NULL,
  `RecordType` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_InventoryMaintenanceRecords_InventoryItemId` (`InventoryItemId`),
  CONSTRAINT `FK_InventoryMaintenanceRecords_InventoryItems_InventoryItemId` FOREIGN KEY (`InventoryItemId`) REFERENCES `inventoryitems` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `landlordactivities`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `landlordactivities` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `LandlordId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `OccurredAt` datetime(6) NOT NULL,
  `Category` longtext NOT NULL,
  `Title` longtext NOT NULL,
  `Description` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_LandlordActivities_LandlordId` (`LandlordId`),
  CONSTRAINT `FK_LandlordActivities_Landlords_LandlordId` FOREIGN KEY (`LandlordId`) REFERENCES `landlords` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `landlords`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `landlords` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ReferenceNumber` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `LandlordType` longtext NOT NULL,
  `Status` longtext NOT NULL,
  `Email` longtext NOT NULL,
  `Phone` longtext NOT NULL,
  `SecondaryPhone` longtext DEFAULT NULL,
  `Address` longtext DEFAULT NULL,
  `City` longtext DEFAULT NULL,
  `Country` longtext DEFAULT NULL,
  `NationalId` longtext DEFAULT NULL,
  `TaxId` longtext DEFAULT NULL,
  `ContactPerson` longtext DEFAULT NULL,
  `BankName` longtext DEFAULT NULL,
  `BankAccount` longtext DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `leasecontracts`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `leasecontracts` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `PremiseId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ContractNumber` varchar(255) NOT NULL,
  `StartDate` datetime(6) NOT NULL,
  `EndDate` datetime(6) NOT NULL,
  `MonthlyRent` decimal(18,2) NOT NULL,
  `Deposit` decimal(18,2) NOT NULL,
  `ContractType` longtext NOT NULL,
  `Clauses` longtext NOT NULL,
  `Status` int(11) NOT NULL,
  `CreatedBy` longtext DEFAULT NULL,
  `ValidatedBy` longtext DEFAULT NULL,
  `ModifiedBy` longtext DEFAULT NULL,
  `CancelledBy` longtext DEFAULT NULL,
  `ValidatedAt` datetime(6) DEFAULT NULL,
  `CancelledAt` datetime(6) DEFAULT NULL,
  `ContractPdfPath` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_LeaseContracts_ContractNumber` (`ContractNumber`),
  KEY `IX_LeaseContracts_PremiseId` (`PremiseId`),
  KEY `IX_LeaseContracts_TenantId` (`TenantId`),
  CONSTRAINT `FK_LeaseContracts_Premises_PremiseId` FOREIGN KEY (`PremiseId`) REFERENCES `premises` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_LeaseContracts_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `leaseguarantees`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `leaseguarantees` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `LeaseContractId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Amount` decimal(65,30) NOT NULL,
  `AmountRefunded` decimal(65,30) NOT NULL,
  `Status` longtext NOT NULL,
  `RefundedAt` datetime(6) DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `DischargePdfPath` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_LeaseGuarantees_LeaseContractId` (`LeaseContractId`),
  CONSTRAINT `FK_LeaseGuarantees_LeaseContracts_LeaseContractId` FOREIGN KEY (`LeaseContractId`) REFERENCES `leasecontracts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `maintenancerecords`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `maintenancerecords` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `EquipmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ScheduledDate` datetime(6) NOT NULL,
  `CompletedDate` datetime(6) DEFAULT NULL,
  `Description` longtext NOT NULL,
  `Cost` decimal(65,30) NOT NULL,
  `Technician` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_MaintenanceRecords_EquipmentId` (`EquipmentId`),
  CONSTRAINT `FK_MaintenanceRecords_Equipment_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `equipment` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `permissions`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `permissions` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `Module` longtext NOT NULL,
  `Description` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `premises`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `premises` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `BuildingId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `PropertyApartmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `Code` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `Floor` longtext NOT NULL,
  `Building` longtext NOT NULL,
  `PremiseType` longtext NOT NULL,
  `OccupancyStatus` longtext NOT NULL,
  `Capacity` int(11) NOT NULL,
  `Equipment` longtext NOT NULL,
  `ConditionNotes` longtext NOT NULL,
  `PhotoPath` longtext DEFAULT NULL,
  `AreaSqM` decimal(65,30) NOT NULL,
  `MonthlyRent` decimal(65,30) NOT NULL,
  `IsOccupied` tinyint(1) NOT NULL,
  `Description` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Premises_BuildingId` (`BuildingId`),
  CONSTRAINT `FK_Premises_Buildings_BuildingId` FOREIGN KEY (`BuildingId`) REFERENCES `buildings` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `propertyapartments`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `propertyapartments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FloorId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `UnitType` longtext NOT NULL,
  `AreaSqM` decimal(65,30) NOT NULL,
  `MonthlyRent` decimal(65,30) NOT NULL,
  `SortOrder` int(11) NOT NULL,
  `PremiseId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_PropertyApartments_FloorId` (`FloorId`),
  CONSTRAINT `FK_PropertyApartments_PropertyFloors_FloorId` FOREIGN KEY (`FloorId`) REFERENCES `propertyfloors` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `propertyfloors`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `propertyfloors` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `BuildingInfoId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `LevelNumber` int(11) NOT NULL,
  `Label` longtext NOT NULL,
  `SortOrder` int(11) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_PropertyFloors_BuildingInfoId` (`BuildingInfoId`),
  CONSTRAINT `FK_PropertyFloors_BuildingInfos_BuildingInfoId` FOREIGN KEY (`BuildingInfoId`) REFERENCES `buildinginfos` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `propertyrooms`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `propertyrooms` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ApartmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` longtext NOT NULL,
  `RoomType` longtext NOT NULL,
  `AreaSqM` decimal(65,30) NOT NULL,
  `SortOrder` int(11) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_PropertyRooms_ApartmentId` (`ApartmentId`),
  CONSTRAINT `FK_PropertyRooms_PropertyApartments_ApartmentId` FOREIGN KEY (`ApartmentId`) REFERENCES `propertyapartments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rentpayments`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `rentpayments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `LeaseContractId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Year` int(11) NOT NULL,
  `Month` int(11) NOT NULL,
  `AmountDue` decimal(65,30) NOT NULL,
  `AmountPaid` decimal(65,30) NOT NULL,
  `DueDate` datetime(6) NOT NULL,
  `PaidDate` datetime(6) DEFAULT NULL,
  `IsLate` tinyint(1) NOT NULL,
  `PenaltyAmount` decimal(65,30) NOT NULL,
  `PaymentStatus` longtext NOT NULL,
  `PaymentMethod` longtext NOT NULL,
  `TransactionReference` longtext DEFAULT NULL,
  `ReceiptNumber` longtext DEFAULT NULL,
  `ReceiptPdfPath` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_RentPayments_LeaseContractId` (`LeaseContractId`),
  CONSTRAINT `FK_RentPayments_LeaseContracts_LeaseContractId` FOREIGN KEY (`LeaseContractId`) REFERENCES `leasecontracts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `repairrecords`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `repairrecords` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `EquipmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ReportedDate` datetime(6) NOT NULL,
  `ResolvedDate` datetime(6) DEFAULT NULL,
  `Issue` longtext NOT NULL,
  `Resolution` longtext DEFAULT NULL,
  `Cost` decimal(65,30) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_RepairRecords_EquipmentId` (`EquipmentId`),
  CONSTRAINT `FK_RepairRecords_Equipment_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `equipment` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `salarypayments`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `salarypayments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `EmployeeId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Year` int(11) NOT NULL,
  `Month` int(11) NOT NULL,
  `Amount` decimal(65,30) NOT NULL,
  `GrossSalary` decimal(65,30) NOT NULL,
  `Bonuses` decimal(65,30) NOT NULL,
  `Penalties` decimal(65,30) NOT NULL,
  `OvertimePay` decimal(65,30) NOT NULL,
  `Advances` decimal(65,30) NOT NULL,
  `Deductions` decimal(65,30) NOT NULL,
  `NetAmount` decimal(65,30) NOT NULL,
  `Status` longtext NOT NULL,
  `ValidatedAt` datetime(6) DEFAULT NULL,
  `PaymentDate` datetime(6) NOT NULL,
  `Notes` longtext DEFAULT NULL,
  `PaySlipPdfPath` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_SalaryPayments_EmployeeId` (`EmployeeId`),
  CONSTRAINT `FK_SalaryPayments_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `employees` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `suppliercontracts`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `suppliercontracts` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `SupplierId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ContractNumber` longtext NOT NULL,
  `StartDate` datetime(6) NOT NULL,
  `EndDate` datetime(6) NOT NULL,
  `Description` longtext NOT NULL,
  `TotalValue` decimal(65,30) NOT NULL,
  `Status` longtext NOT NULL,
  `Building` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_SupplierContracts_SupplierId` (`SupplierId`),
  CONSTRAINT `FK_SupplierContracts_Suppliers_SupplierId` FOREIGN KEY (`SupplierId`) REFERENCES `suppliers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `supplierpayments`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `supplierpayments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `SupplierId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Amount` decimal(65,30) NOT NULL,
  `PaymentDate` datetime(6) NOT NULL,
  `DueDate` datetime(6) DEFAULT NULL,
  `InvoiceReference` longtext DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `Description` longtext NOT NULL,
  `Category` longtext NOT NULL,
  `IsPaid` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_SupplierPayments_SupplierId` (`SupplierId`),
  CONSTRAINT `FK_SupplierPayments_Suppliers_SupplierId` FOREIGN KEY (`SupplierId`) REFERENCES `suppliers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `suppliers`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `suppliers` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `Email` longtext NOT NULL,
  `Phone` longtext NOT NULL,
  `Address` longtext DEFAULT NULL,
  `TaxId` longtext DEFAULT NULL,
  `Category` longtext NOT NULL,
  `ServiceType` longtext NOT NULL,
  `Status` longtext NOT NULL,
  `ContactName` longtext NOT NULL,
  `Building` longtext NOT NULL,
  `Notes` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `synclogs`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `synclogs` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `StartedAt` datetime(6) NOT NULL,
  `CompletedAt` datetime(6) DEFAULT NULL,
  `Success` tinyint(1) NOT NULL,
  `RecordsPushed` int(11) NOT NULL,
  `RecordsPulled` int(11) NOT NULL,
  `ConflictsResolved` int(11) NOT NULL,
  `ErrorMessage` longtext DEFAULT NULL,
  `Direction` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `systemlogs`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `systemlogs` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Level` longtext NOT NULL,
  `Source` longtext NOT NULL,
  `Message` longtext NOT NULL,
  `Exception` longtext DEFAULT NULL,
  `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `technicalalerts`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `technicalalerts` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `EquipmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `Title` longtext NOT NULL,
  `Message` longtext NOT NULL,
  `AlertDate` datetime(6) NOT NULL,
  `IsAcknowledged` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_TechnicalAlerts_EquipmentId` (`EquipmentId`),
  CONSTRAINT `FK_TechnicalAlerts_Equipment_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `equipment` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tenantactivities`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `tenantactivities` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `OccurredAt` datetime(6) NOT NULL,
  `Category` longtext NOT NULL,
  `Title` longtext NOT NULL,
  `Description` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_TenantActivities_TenantId` (`TenantId`),
  CONSTRAINT `FK_TenantActivities_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tenantdependents`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `tenantdependents` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FullName` longtext NOT NULL,
  `Relationship` longtext NOT NULL,
  `DateOfBirth` datetime(6) DEFAULT NULL,
  `NationalId` longtext DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_TenantDependents_TenantId` (`TenantId`),
  CONSTRAINT `FK_TenantDependents_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tenants`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `tenants` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `DossierNumber` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `RentalStatus` longtext NOT NULL,
  `ProfilePhotoPath` longtext DEFAULT NULL,
  `Nationality` longtext DEFAULT NULL,
  `BusinessActivity` longtext DEFAULT NULL,
  `PersonCount` int(11) NOT NULL,
  `IdentityDocumentPath` longtext DEFAULT NULL,
  `ContractDocumentPath` longtext DEFAULT NULL,
  `Email` longtext NOT NULL,
  `Phone` longtext NOT NULL,
  `Company` longtext DEFAULT NULL,
  `Address` longtext DEFAULT NULL,
  `TenantCategory` longtext NOT NULL,
  `NationalId` longtext DEFAULT NULL,
  `IdDocumentType` longtext DEFAULT NULL,
  `IdDocumentExpiry` datetime(6) DEFAULT NULL,
  `DateOfBirth` datetime(6) DEFAULT NULL,
  `SecondaryPhone` longtext DEFAULT NULL,
  `Employer` longtext DEFAULT NULL,
  `PreviousAddress` longtext DEFAULT NULL,
  `Gender` longtext NOT NULL,
  `MaritalStatus` longtext NOT NULL,
  `SpouseName` longtext DEFAULT NULL,
  `ChildrenCount` int(11) NOT NULL,
  `Profession` longtext DEFAULT NULL,
  `EmergencyContactName` longtext DEFAULT NULL,
  `EmergencyContactPhone` longtext DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `userpermissions`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `userpermissions` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `PermissionId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_UserPermissions_UserId_PermissionId` (`UserId`,`PermissionId`),
  KEY `IX_UserPermissions_PermissionId` (`PermissionId`),
  CONSTRAINT `FK_UserPermissions_Permissions_PermissionId` FOREIGN KEY (`PermissionId`) REFERENCES `permissions` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_UserPermissions_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `users`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `users` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Username` varchar(100) NOT NULL,
  `Email` varchar(256) NOT NULL,
  `PasswordHash` varchar(512) NOT NULL,
  `FullName` varchar(200) NOT NULL,
  `Role` int(11) NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `PasswordResetToken` longtext DEFAULT NULL,
  `PasswordResetExpires` datetime(6) DEFAULT NULL,
  `LastLoginAt` datetime(6) DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Users_Email` (`Email`),
  UNIQUE KEY `IX_Users_Username` (`Username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `visitorappointments`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `visitorappointments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `VisitorId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `VisitorName` longtext NOT NULL,
  `HostName` longtext NOT NULL,
  `Purpose` longtext NOT NULL,
  `ScheduledAt` datetime(6) NOT NULL,
  `Room` longtext NOT NULL,
  `Building` longtext NOT NULL,
  `Status` longtext NOT NULL,
  `DurationMinutes` int(11) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `visitors`
--

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `visitors` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `VisitCode` longtext NOT NULL,
  `FullName` longtext NOT NULL,
  `Company` longtext DEFAULT NULL,
  `Phone` longtext DEFAULT NULL,
  `Email` longtext DEFAULT NULL,
  `IdDocument` longtext DEFAULT NULL,
  `IdDocumentType` longtext NOT NULL,
  `HostName` longtext NOT NULL,
  `Purpose` longtext NOT NULL,
  `VisitType` longtext NOT NULL,
  `AccessStatus` longtext NOT NULL,
  `Building` longtext NOT NULL,
  `Zone` longtext NOT NULL,
  `AllowedZones` longtext NOT NULL,
  `CheckInAt` datetime(6) NOT NULL,
  `CheckOutAt` datetime(6) DEFAULT NULL,
  `ExpectedCheckOutAt` datetime(6) DEFAULT NULL,
  `BadgeNumber` longtext DEFAULT NULL,
  `Notes` longtext DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `IsSynced` tinyint(1) NOT NULL,
  `DeletedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-07-04 21:37:35
