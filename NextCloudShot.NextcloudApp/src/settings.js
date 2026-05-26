import axios from '@nextcloud/axios'
import { generateUrl } from '@nextcloud/router'

document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('nextcloudshot-personal-settings')
  if (!form) {
    return
  }

  const input = form.querySelector('[name="folder"]')
  const status = form.querySelector('[data-nextcloudshot-status]')

  form.addEventListener('submit', async (event) => {
    event.preventDefault()
    status.textContent = 'Сохранение...'

    try {
      await axios.put(generateUrl('/apps/nextcloudshot/api/settings'), {
        folder: input.value,
      })
      status.textContent = 'Сохранено.'
    } catch {
      status.textContent = 'Не удалось сохранить папку.'
    }
  })
})
