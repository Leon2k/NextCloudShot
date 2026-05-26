<?php

declare(strict_types=1);

namespace OCA\NextCloudShot\Controller;

use OCA\NextCloudShot\AppInfo\Application;
use OCA\NextCloudShot\Service\ScreenshotLibraryService;
use OCP\AppFramework\Controller;
use OCP\AppFramework\Http\DataResponse;
use OCP\IConfig;
use OCP\IRequest;
use OCP\IUserSession;

class ApiController extends Controller {
    public function __construct(
        string $appName,
        IRequest $request,
        private IUserSession $userSession,
        private IConfig $config,
        private ScreenshotLibraryService $library,
    ) {
        parent::__construct($appName, $request);
    }

    /**
     * @NoAdminRequired
     * @NoCSRFRequired
     */
    public function screenshots(): DataResponse {
        $userId = $this->requireUserId();
        $folder = $this->config->getUserValue($userId, Application::APP_ID, 'folder', Application::DEFAULT_FOLDER);
        return new DataResponse([
            'folder' => $folder,
            'screenshots' => $this->library->listForUser($userId, $folder),
        ]);
    }

    private function requireUserId(): string {
        $user = $this->userSession->getUser();
        if ($user === null) {
            throw new \RuntimeException('Authenticated user required.');
        }
        return $user->getUID();
    }
}
