<?php

declare(strict_types=1);

namespace OCA\NextCloudShot\Service;

use OCP\Files\Folder;
use OCP\Files\IRootFolder;
use OCP\Files\Node;
use OCP\Files\NotFoundException;
use OCP\IURLGenerator;

class ScreenshotLibraryService {
    public function __construct(
        private IRootFolder $rootFolder,
        private IURLGenerator $urlGenerator,
    ) {
    }

    /** @return list<array<string, mixed>> */
    public function listForUser(string $userId, string $configuredFolder): array {
        $folderPath = '/' . trim($configuredFolder, '/');
        try {
            $node = $this->rootFolder->getUserFolder($userId)->get($folderPath);
        } catch (NotFoundException) {
            return [];
        }

        if (!$node instanceof Folder) {
            return [];
        }

        $images = array_filter(
            $node->getDirectoryListing(),
            static fn (Node $file): bool => !$file instanceof Folder && str_starts_with($file->getMimeType(), 'image/')
        );
        usort($images, static fn (Node $a, Node $b): int => $b->getMTime() <=> $a->getMTime());

        return array_map(fn (Node $file): array => [
            'id' => $file->getId(),
            'name' => $file->getName(),
            'path' => $file->getPath(),
            'size' => $file->getSize(),
            'modifiedAt' => gmdate(DATE_ATOM, $file->getMTime()),
            'mimeType' => $file->getMimeType(),
            'previewUrl' => $this->urlGenerator->linkToRouteAbsolute('core.Preview.getPreviewByFileId', [
                'fileId' => $file->getId(),
                'x' => 640,
                'y' => 420,
                'a' => true,
            ]),
            'fullPreviewUrl' => $this->urlGenerator->linkToRouteAbsolute('core.Preview.getPreviewByFileId', [
                'fileId' => $file->getId(),
                'x' => 1920,
                'y' => 1080,
                'a' => true,
            ]),
        ], $images);
    }
}
