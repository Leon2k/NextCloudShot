<?php
/** @var array $_ */
script('nextcloudshot', 'nextcloudshot-settings');
?>
<form id="nextcloudshot-personal-settings" class="section">
  <h2>NextCloudShot</h2>
  <p>
    <label for="nextcloudshot-folder">Папка со скриншотами</label>
  </p>
  <p>
    <input id="nextcloudshot-folder" name="folder" type="text" value="<?php p($_['folder']); ?>" placeholder="/Screenshots">
    <button type="submit">Сохранить</button>
  </p>
  <p class="settings-hint" data-nextcloudshot-status></p>
</form>
