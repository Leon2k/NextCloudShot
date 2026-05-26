<?php

declare(strict_types=1);

namespace OCA\NextCloudShot\Settings;

use OCA\NextCloudShot\AppInfo\Application;
use OCP\AppFramework\Http\TemplateResponse;
use OCP\IConfig;
use OCP\IUserSession;
use OCP\Settings\ISettings;

class Personal implements ISettings {
    public function __construct(
        private IConfig $config,
        private IUserSession $userSession,
    ) {
    }

    public function getForm(): TemplateResponse {
        $user = $this->userSession->getUser();
        $folder = Application::DEFAULT_FOLDER;
        if ($user !== null) {
            $folder = $this->config->getUserValue($user->getUID(), Application::APP_ID, 'folder', Application::DEFAULT_FOLDER);
        }

        return new TemplateResponse(Application::APP_ID, 'personal_settings', [
            'folder' => $folder,
        ]);
    }

    public function getSection(): string {
        return 'additional';
    }

    public function getPriority(): int {
        return 55;
    }
}
