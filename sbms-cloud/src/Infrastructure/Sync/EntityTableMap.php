<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Sync;

/** Type sync desktop → table MySQL (identique EF / sbms_local). */
final class EntityTableMap
{
    public const SYNC_TYPE_TO_TABLE = [
        'Users' => 'users',
        'Employees' => 'employees',
        'Attendances' => 'attendances',
        'SalaryPayments' => 'salarypayments',
        'DisciplinaryNotes' => 'disciplinarynotes',
        'Buildings' => 'buildings',
        'BuildingInfos' => 'buildinginfos',
        'Landlords' => 'landlords',
        'LandlordActivities' => 'landlordactivities',
        'PropertyFloors' => 'propertyfloors',
        'PropertyApartments' => 'propertyapartments',
        'PropertyRooms' => 'propertyrooms',
        'Premises' => 'premises',
        'Tenants' => 'tenants',
        'TenantDependents' => 'tenantdependents',
        'LeaseContracts' => 'leasecontracts',
        'RentPayments' => 'rentpayments',
        'TenantActivities' => 'tenantactivities',
        'LeaseGuarantees' => 'leaseguarantees',
        'Equipment' => 'equipment',
        'MaintenanceRecords' => 'maintenancerecords',
        'RepairRecords' => 'repairrecords',
        'TechnicalAlerts' => 'technicalalerts',
        'FinancialTransactions' => 'financialtransactions',
        'Suppliers' => 'suppliers',
        'SupplierContracts' => 'suppliercontracts',
        'SupplierPayments' => 'supplierpayments',
        'Incidents' => 'incidents',
        'IncidentInterventions' => 'incidentinterventions',
        'ConsumptionRecords' => 'consumptionrecords',
        'Visitors' => 'visitors',
        'VisitorAppointments' => 'visitorappointments',
        'InventoryItems' => 'inventoryitems',
        'InventoryMaintenanceRecords' => 'inventorymaintenancerecords',
    ];

    public static function tableFor(string $entityType): ?string
    {
        return self::SYNC_TYPE_TO_TABLE[$entityType] ?? null;
    }
}
