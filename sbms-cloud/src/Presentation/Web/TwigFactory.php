<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Web;

use Twig\Environment;
use Twig\Loader\FilesystemLoader;

final class TwigFactory
{
    public static function create(string $templatesPath): Environment
    {
        $loader = new FilesystemLoader($templatesPath);
        return new Environment($loader, [
            'cache' => false,
            'autoescape' => 'html',
        ]);
    }
}
