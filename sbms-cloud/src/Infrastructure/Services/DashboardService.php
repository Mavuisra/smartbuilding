<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Services;

use PDO;
use Sbms\Cloud\Infrastructure\Persistence\SyncEventRepository;
use Sbms\Cloud\Infrastructure\Persistence\SyncStoreRepository;

final class DashboardService
{
    public function __construct(
        private readonly PDO $pdo,
        private readonly SyncStoreRepository $store,
        private readonly SyncEventRepository $events,
    ) {
    }

    public function executiveSummary(): array
    {
        $today = new \DateTimeImmutable('today');
        $year = (int) $today->format('Y');
        $month = (int) $today->format('m');

        $rentMonth = $this->scalar(
            'SELECT COALESCE(SUM(AmountPaid), 0) FROM rentpayments
             WHERE DeletedAt IS NULL AND Year = ? AND Month = ?',
            [$year, $month]
        );
        $rentPlanned = $this->scalar(
            'SELECT COALESCE(SUM(AmountDue), 0) FROM rentpayments
             WHERE DeletedAt IS NULL AND Year = ? AND Month = ?',
            [$year, $month]
        );
        $rentTotal = $this->scalar(
            'SELECT COALESCE(SUM(AmountPaid), 0) FROM rentpayments WHERE DeletedAt IS NULL'
        );
        $expensesMonth = $this->scalar(
            'SELECT COALESCE(SUM(Amount), 0) FROM financialtransactions
             WHERE DeletedAt IS NULL AND Type = 2 AND YEAR(TransactionDate) = ? AND MONTH(TransactionDate) = ?',
            [$year, $month]
        );
        $expensesTotal = $this->scalar(
            'SELECT COALESCE(SUM(Amount), 0) FROM financialtransactions WHERE DeletedAt IS NULL AND Type = 2'
        );
        $lateCount = (int) $this->scalar(
            'SELECT COUNT(*) FROM rentpayments WHERE DeletedAt IS NULL AND IsLate = 1'
        );
        $rentLate = $this->scalar(
            'SELECT COALESCE(SUM(AmountDue - AmountPaid), 0) FROM rentpayments
             WHERE DeletedAt IS NULL AND IsLate = 1'
        );

        $totalPremises = (int) $this->scalar('SELECT COUNT(*) FROM premises WHERE DeletedAt IS NULL');
        $occupied = (int) $this->scalar('SELECT COUNT(*) FROM premises WHERE DeletedAt IS NULL AND IsOccupied = 1');
        $occupancy = $totalPremises > 0 ? ($occupied / $totalPremises * 100) : 0;

        $openIncidents = (int) $this->scalar(
            'SELECT COUNT(*) FROM incidents WHERE DeletedAt IS NULL AND Status NOT IN (3, 4)'
        );
        $activeLeases = (int) $this->scalar(
            'SELECT COUNT(*) FROM leasecontracts WHERE DeletedAt IS NULL AND Status = 1'
        );
        $totalTenants = (int) $this->scalar('SELECT COUNT(*) FROM tenants WHERE DeletedAt IS NULL');

        $recentSyncs = array_map(function (array $row) {
            return [
                'username' => $row['username'],
                'user_role' => $row['user_role'],
                'entity_type' => $row['entity_type'],
                'records_count' => (int) $row['records_count'],
                'created_at' => $row['created_at'],
            ];
        }, $this->events->recentSuccess(10));

        $availableBalance = (float) $rentTotal - (float) $expensesTotal;
        $availableMonth = (float) $rentMonth - (float) $expensesMonth;

        $alerts = [];
        if ((float) $rentLate > 0) {
            $alerts[] = [
                'title' => 'Loyers en retard',
                'message' => sprintf('$ %s à recouvrer (%d paiement(s))', number_format((float) $rentLate, 2), $lateCount),
                'severity' => 'Warning',
            ];
        }
        if ($openIncidents > 0) {
            $alerts[] = [
                'title' => 'Incidents ouverts',
                'message' => "{$openIncidents} incident(s) à traiter",
                'severity' => 'Error',
            ];
        }
        if (!$alerts) {
            $alerts[] = [
                'title' => 'Situation stable',
                'message' => 'Aucune alerte critique',
                'severity' => 'Success',
            ];
        }

        return [
            'monthlyRevenue' => (float) $rentTotal,
            'rentRevenue' => (float) $rentTotal,
            'rentCollectedTotal' => (float) $rentTotal,
            'monthlyExpenses' => (float) $expensesMonth,
            'expensesThisMonth' => (float) $expensesMonth,
            'totalExpenses' => (float) $expensesTotal,
            'availableBalance' => $availableBalance,
            'availableThisMonth' => $availableMonth,
            'netBalance' => $availableBalance,
            'netBalanceThisMonth' => $availableMonth,
            'treasuryBalance' => $availableBalance,
            'rentCollected' => (float) $rentMonth,
            'rentPlanned' => (float) $rentPlanned,
            'rentLateAmount' => (float) $rentLate,
            'latePaymentsCount' => $lateCount,
            'occupancyRate' => round($occupancy, 1),
            'occupiedPremises' => $occupied,
            'totalPremises' => $totalPremises,
            'openIncidents' => $openIncidents,
            'activeLeases' => $activeLeases,
            'totalTenants' => $totalTenants,
            'recentSyncEvents' => $recentSyncs,
            'alerts' => $alerts,
            'syncStoreCounts' => $this->syncCounts(),
        ];
    }

    public function syncHealth(): array
    {
        return [
            'status' => 'ok',
            'entityStoreRows' => (int) $this->scalar('SELECT COUNT(*) FROM synced_entity_store'),
            'lastSyncEvents' => $this->events->recent(5),
            'countsByType' => $this->syncCounts(),
        ];
    }

    private function syncCounts(): array
    {
        $stmt = $this->pdo->query(
            'SELECT entity_type, COUNT(*) AS cnt FROM synced_entity_store
             WHERE deleted_at IS NULL GROUP BY entity_type ORDER BY entity_type'
        );
        $out = [];
        foreach ($stmt->fetchAll() as $row) {
            $out[$row['entity_type']] = (int) $row['cnt'];
        }
        return $out;
    }

    private function scalar(string $sql, array $params = []): float|int|string
    {
        $stmt = $this->pdo->prepare($sql);
        $stmt->execute($params);
        return $stmt->fetchColumn();
    }
}
