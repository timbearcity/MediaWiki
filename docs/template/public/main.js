// The navbar's icons form (theme picker) is rendered by the template after `start` runs, so it is awaited.
function navbarIcons() {
  const find = () => document.querySelector('header .navbar .icons')
  return new Promise(resolve => {
    if (find()) return resolve(find())
    const observer = new MutationObserver(() => {
      if (!find()) return
      observer.disconnect()
      resolve(find())
    })
    observer.observe(document.querySelector('header') ?? document.body, { childList: true, subtree: true })
  })
}

// Adds a version picker to the navbar. The site root holds versions.json and one folder per release.
async function addVersionPicker() {
  const rel = document.querySelector('meta[name="docfx:rel"]')?.content
  if (rel === undefined) return

  const versionRoot = new URL(rel || './', location.href)
  const siteRoot = new URL('../', versionRoot)
  const current = versionRoot.pathname.split('/').at(-2)
  const page = location.href.slice(versionRoot.href.length)

  let versions
  try {
    versions = (await (await fetch(new URL('versions.json', siteRoot))).json()).versions
  } catch {
    return // a local `docfx serve` has no versions.json
  }
  if (!versions.includes(current)) return

  const icons = await navbarIcons()

  const select = document.createElement('select')
  select.className = 'form-select form-select-sm me-2'
  select.ariaLabel = 'Version'
  for (const v of versions) select.add(new Option(v, v, false, v === current))

  // Stay on the same page when the other release has it, otherwise land on that release's home page.
  select.addEventListener('change', async () => {
    const target = new URL(`${select.value}/`, siteRoot)
    const samePage = new URL(page, target)
    const res = await fetch(samePage, { method: 'HEAD' }).catch(() => null)
    location.href = res?.ok ? samePage : target
  })
  icons.prepend(select)
}

export default {
  start: () => { addVersionPicker() },
}
