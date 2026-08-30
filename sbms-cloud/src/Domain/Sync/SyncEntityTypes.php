<?php

declare(strict_types=1);

namespace Sbms\Cloud\Domain\Sync;

final class SyncEntityTypes
{
    public const ALL = [
        'Users',
        'Employees',
        'Attendances',
        'SalaryPayments',
        'DisciplinaryNotes',
        'Buildings',
        'BuildingInfos',
        'Landlords',
        'LandlordActivities',
        'PropertyFloors',
        'PropertyApartments',
        'PropertyRooms',
        'Premises',
        'Tenants',
        'TenantDependents',
        'LeaseContracts',
        'RentPayments',
        'TenantActivities',
        'LeaseGuarantees',
        'Equipment',
        'MaintenanceRecords',
        'RepairRecords',
        'TechnicalAlerts',
        'FinancialTransactions',
        'Suppliers',
        'SupplierContracts',
        'SupplierPayments',
        'Incidents',
        'IncidentInterventions',
        'ConsumptionRecords',
        'Visitors',
        'VisitorAppointments',
        'InventoryItems',
        'InventoryMaintenanceRecords',
    ];

    public static function isSyncable(string $entityType): bool
    {
        return in_array($entityType, self::ALL, true);
    }
}
