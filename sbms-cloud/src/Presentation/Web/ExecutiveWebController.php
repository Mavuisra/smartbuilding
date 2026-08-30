<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Web;

use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Infrastructure\Services\ModuleRegistry;
use Slim\Psr7\Response;
use Twig\Environment;

final class ExecutiveWebController
{
    private const TEMPLATES = [
        'dashboard' => 'module_dashboard.twig',
        'rapports' => 'module_rapports.twig',
        'documents' => 'module_documents.twig',
        'utilisateurs' => 'module_utilisateurs.twig',
        'parametres' => 'module_parametres.twig',
        'synchronisation' => 'module_synchronisation.twig',
        'journal' => 'module_journal.twig',
    ];

    public function __construct(private readonly Environment $twig)
    {
    }

    public function login(ServerRequestInterface $request): ResponseInterface
    {
        return $this->render('login.twig', []);
    }

    public function home(ServerRequestInterface $request): ResponseInterface
    {
        return $this->redirect('/dashboard/');
    }

    public function module(ServerRequestInterface $request, array $args): ResponseInterface
    {
        $slug = ModuleRegistry::resolveSlug($args['slug'] ?? 'dashboard');
        if (!ModuleRegistry::isWebPortalModule($slug)) {
            return $this->redirect('/dashboard/');
        }

        $template = self::TEMPLATES[$slug] ?? 'module.twig';
        return $this->render($template, [
            'module_slug' => $slug,
            'module' => ModuleRegistry::moduleMeta($slug),
            'navigation' => ModuleRegistry::buildNavigation('Administrateur'),
            'location_children' => [],
        ]);
    }

    private function render(string $template, array $context): ResponseInterface
    {
        $html = $this->twig->render($template, $context);
        $response = new Response(200);
        $response->getBody()->write($html);
        return $response->withHeader('Content-Type', 'text/html; charset=utf-8');
    }

    private function redirect(string $path): ResponseInterface
    {
        return (new Response(302))->withHeader('Location', $path);
    }
}
