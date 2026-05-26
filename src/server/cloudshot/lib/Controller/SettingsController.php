<?php

declare(strict_types=1);

namespace OCA\CloudShot\Controller;

use OCA\CloudShot\AppInfo\Application;
use OCP\AppFramework\Controller;
use OCP\AppFramework\Http\DataResponse;
use OCP\IConfig;
use OCP\IRequest;
use OCP\IUserSession;

class SettingsController extends Controller {
    public function __construct(
        string $appName,
        IRequest $request,
        private IUserSession $userSession,
        private IConfig $config,
    ) {
        parent::__construct($appName, $request);
    }

    /**
     * @NoAdminRequired
     * @NoCSRFRequired
     */
    public function get(): DataResponse {
        $userId = $this->requireUserId();
        return new DataResponse([
            'folder' => $this->config->getUserValue($userId, Application::APP_ID, 'folder', '/CloudShot/Screenshots'),
        ]);
    }

    /** @NoAdminRequired */
    public function save(string $folder): DataResponse {
        $folder = '/' . trim(str_replace('..', '', $folder), '/');
        if ($folder === '/') {
            $folder = '/CloudShot/Screenshots';
        }
        $this->config->setUserValue($this->requireUserId(), Application::APP_ID, 'folder', $folder);
        return new DataResponse(['folder' => $folder]);
    }

    private function requireUserId(): string {
        $user = $this->userSession->getUser();
        if ($user === null) {
            throw new \RuntimeException('Authenticated user required.');
        }
        return $user->getUID();
    }
}
