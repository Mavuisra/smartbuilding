<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Services;

use PDO;
use Sbms\Cloud\Infrastructure\Persistence\DocumentRepository;
use Sbms\Cloud\Infrastructure\Persistence\SyncEventRepository;
use Sbms\Cloud\Infrastructure\Persistence\SyncStoreRepository;
use Sbms\Cloud\Infrastructure\Persistence\UserRepository;
use Sbms\Cloud\Infrastructure\Sync\SyncUtils;

final class ModuleHandlerRegistry
{
    public function __construct(
        private readonly PDO $pdo,
        private readonly SyncStoreRepository $store,
        private readonly SyncEventRepository $events,
        private readonly DocumentRepository $documents,
        private readonly UserRepository $users,
        private readonly DashboardService $dashboard,
    ) {
    }

    public function handle(string $slug): array
    {
        $slug = str_replace('_', '-', strtolower($slug));
        return match ($slug) {
            'dashboard' => $this->dashboardModule(),
            'rapports' => $this->reportsModule(),
            'documents' => $this->documentsModule(),
            'utilisateurs' => $this->usersModule(),
            'parametres' => $this->settingsModule(),
            'synchronisation' => $this->syncModule(),
            'journal' => $this->journalModule(),
            'finances', 'finance' => $this->financesModule(),
            'incidents' => $this->incidentsModule(),
            'personnel' => $this->personnelModule(),
            'locations', 'locations-list' => $this->locationsModule(),
            default => $this->genericFromStore($slug),
        };
    }

    private function modulePayload(string $title, array $rows, array $extra = []): array
    {
        return array_merge([
            'title' => $title,
            'rows' => $rows,
            'columns' => $rows ? array_keys($rows[0]) : [],
            'count' => count($rows),
        ], $extra);
    }

    private function dashboardModule(): array
    {
        $summary = $this->dashboard->executiveSummary();
        return $this->modulePayload('Tableau de bord', [], [
            'summary' => $summary,
            'kpis' => [
                ['label' => 'Trésorerie', 'value' => $summary['treasuryBalance']],
                ['label' => 'Loyers collectés', 'value' => $summary['rentCollected']],
                ['label' => 'Dépenses du mois', 'value' => $summary['expensesThisMonth']],
                ['label' => 'Taux occupation', 'value' => $summary['occupancyRate'] . '%'],
            ],
        ]);
    }

    private function personnelModule(): array
    {
        $stmt = $this->pdo->query(
            'SELECT Matricule, FirstName, LastName, Position, Department, Phone, IsActive
             FROM employees WHERE DeletedAt IS NULL ORDER BY LastName LIMIT 300'
        );
        $rows = [];
        foreach ($stmt->fetchAll() as $e) {
            $rows[] = [
                'Matricule' => trim($e['Matricule'] ?? '') ?: '—',
                'Nom' => trim(($e['FirstName'] ?? '') . ' ' . ($e['LastName'] ?? '')) ?: '—',
                'Poste' => $e['Position'] ?: '—',
                'Département' => $e['Department'] ?: '—',
                'Téléphone' => $e['Phone'] ?: '—',
                'Statut' => $e['IsActive'] ? 'Actif' : 'Inactif',
            ];
        }
        return $this->modulePayload('Personnel', $rows);
    }

    private function locationsModule(): array
    {
        $stmt = $this->pdo->query(
            'SELECT t.Name AS locataire, p.Name AS local, lc.ContractNumber, lc.Status, lc.MonthlyRent
             FROM leasecontracts lc
             LEFT JOIN tenants t ON t.Id = lc.TenantId
             LEFT JOIN premises p ON p.Id = lc.PremiseId
             WHERE lc.DeletedAt IS NULL ORDER BY lc.UpdatedAt DESC LIMIT 200'
        );
        $rows = [];
        foreach ($stmt->fetchAll() as $r) {
            $rows[] = [
                'Locataire' => $r['locataire'] ?? '—',
                'Local' => $r['local'] ?? '—',
                'Contrat' => $r['ContractNumber'] ?: '—',
                'Statut' => $this->leaseStatusLabel((int) ($r['Status'] ?? 0)),
                'Loyer' => '$ ' . number_format((float) $r['MonthlyRent'], 2),
            ];
        }
        return $this->modulePayload('Locations', $rows);
    }

    private function leaseStatusLabel(int $status): string
    {
        return match ($status) {
            1 => 'Actif',
            2 => 'Résilié',
            3 => 'Expiré',
            default => 'Brouillon',
        };
    }

    private function financesModule(): array
    {
        $stmt = $this->pdo->query(
            'SELECT Type, Category, Description, Amount, TransactionDate, Status, RequiresPdgApproval
             FROM financialtransactions WHERE DeletedAt IS NULL ORDER BY TransactionDate DESC LIMIT 200'
        );
        $rows = [];
        foreach ($stmt->fetchAll() as $r) {
            $rows[] = [
                'Type' => (int) $r['Type'] === 2 ? 'Dépense' : 'Recette',
                'Catégorie' => $r['Category'] ?: '—',
                'Description' => $r['Description'] ?: '—',
                'Montant' => '$ ' . number_format((float) $r['Amount'], 2),
                'Date' => $r['TransactionDate'],
                'Statut' => $r['Status'] ?: '—',
                'Validation PDG' => $r['RequiresPdgApproval'] ? 'Requise' : '—',
            ];
        }
        return $this->modulePayload('Finances', $rows);
    }

    private function incidentsModule(): array
    {
        $stmt = $this->pdo->query(
            'SELECT Code, Title, Severity, Status, Location, ReportedAt FROM incidents
             WHERE DeletedAt IS NULL ORDER BY ReportedAt DESC LIMIT 200'
        );
        $rows = [];
        foreach ($stmt->fetchAll() as $r) {
            $rows[] = [
                'Code' => $r['Code'] ?: '—',
                'Titre' => $r['Title'] ?: '—',
                'Gravité' => (string) ($r['Severity'] ?? '—'),
                'Statut' => (string) ($r['Status'] ?? '—'),
                'Lieu' => $r['Location'] ?: '—',
                'Signalé' => $r['ReportedAt'],
            ];
        }
        return $this->modulePayload('Incidents', $rows);
    }

    private function reportsModule(): array
    {
        return $this->modulePayload('Rapports', [], [
            'categories' => ['finances', 'locations', 'personnel', 'incidents'],
            'message' => 'Exports PDF disponibles depuis le desktop.',
        ]);
    }

    private function documentsModule(): array
    {
        $docs = $this->documents->listByCategory('rapports');
        $rows = [];
        foreach ($docs as $d) {
            $rows[] = [
                'Fichier' => $d['file_name'],
                'Type' => $d['entity_type'],
                'Taille' => number_format((int) $d['file_size'] / 1024, 1) . ' Ko',
                'Ajouté par' => $d['added_by'] ?: '—',
                'Date' => $d['updated_at'],
            ];
        }
        return $this->modulePayload('Documents', $rows);
    }

    private function usersModule(): array
    {
        $rows = [];
        foreach ($this->users->listActive() as $u) {
            $rows[] = [
                'Utilisateur' => $u['username'],
                'Nom' => $u['full_name'] ?: '—',
                'Rôle' => $u['role'],
                'Statut' => $u['is_active'] ? 'Actif' : 'Inactif',
                'Dernière connexion' => $u['last_login_at'] ?? '—',
            ];
        }
        return $this->modulePayload('Utilisateurs', $rows);
    }

    private function settingsModule(): array
    {
        return $this->modulePayload('Paramètres', [], [
            'appName' => 'Bloom Prosperity',
            'version' => 'SBMS Cloud PHP — parité desktop',
            'database' => $_ENV['DB_NAME'] ?? 'sbms_cloud',
        ]);
    }

    private function syncModule(): array
    {
        $counts = [];
        $stmt = $this->pdo->query(
            'SELECT entity_type, COUNT(*) AS cnt FROM synced_entity_store GROUP BY entity_type'
        );
        foreach ($stmt->fetchAll() as $r) {
            $counts[] = ['Type' => $r['entity_type'], 'Enregistrements' => (int) $r['cnt']];
        }
        $events = [];
        foreach ($this->events->recent(50) as $e) {
            $events[] = [
                'Utilisateur' => $e['username'],
                'Type' => $e['entity_type'],
                'Direction' => $e['direction'],
                'Nombre' => (int) $e['records_count'],
                'Succès' => $e['success'] ? 'Oui' : 'Non',
                'Date' => $e['created_at'],
            ];
        }
        return $this->modulePayload('Synchronisation', $counts, ['events' => $events]);
    }

    private function journalModule(): array
    {
        $rows = [];
        foreach ($this->events->recent(100) as $e) {
            $rows[] = [
                'Action' => strtoupper($e['direction']) . ' ' . $e['entity_type'],
                'Utilisateur' => $e['username'],
                'Rôle' => $e['user_role'],
                'Enregistrements' => (int) $e['records_count'],
                'Date' => $e['created_at'],
            ];
        }
        return $this->modulePayload('Journal', $rows);
    }

    private function genericFromStore(string $slug): array
    {
        $typeMap = [
            'suppliers' => 'Suppliers',
            'fournisseurs' => 'Suppliers',
            'visites' => 'Visitors',
            'consommations' => 'ConsumptionRecords',
            'technique' => 'Equipment',
        ];
        $entityType = $typeMap[$slug] ?? null;
        if (!$entityType) {
            return $this->modulePayload(ucfirst($slug), [], ['message' => 'Module non configuré']);
        }
        $rows = [];
        foreach ($this->store->rowsByType($entityType) as $row) {
            $data = $row['json_data'];
            $rows[] = [
                'Id' => substr($row['id'], 0, 8) . '…',
                'Nom' => SyncUtils::pick($data, 'Name', 'name', 'FullName', 'fullName') ?? '—',
            ];
        }
        return $this->modulePayload(ucfirst($slug), $rows);
    }
}
