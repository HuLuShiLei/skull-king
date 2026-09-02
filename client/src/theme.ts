/**
 * 外观主题。偏好存在 localStorage，解析结果写到 <html data-theme>，
 * CSS 只认 light / dark 两个值。
 *
 * 跟随系统时要自己听 prefers-color-scheme：光靠 CSS 媒体查询没法同步
 * 手机浏览器顶栏的 theme-color，强制浅色时也会被系统深色顶栏拆穿。
 */
export type ThemePreference = 'light' | 'dark' | 'system'

export type ThemeResolved = 'light' | 'dark'

const KEY = 'sk.theme'

const THEME_COLOR: Record<ThemeResolved, string> = {
  light: '#ebedf0',
  dark: '#141516',
}

export const THEME_OPTIONS: { id: ThemePreference; label: string }[] = [
  { id: 'light', label: '浅色' },
  { id: 'dark', label: '深色' },
  { id: 'system', label: '跟随系统' },
]

let media: MediaQueryList | null = null
let preference: ThemePreference = 'system'

function readPreference(): ThemePreference {
  try {
    const raw = localStorage.getItem(KEY)
    if (raw === 'light' || raw === 'dark' || raw === 'system') {
      return raw
    }
  } catch {
    // 无痕模式读不了也没关系，当跟随系统。
  }

  return 'system'
}

function systemDark(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches
}

export function resolveTheme(pref: ThemePreference): ThemeResolved {
  if (pref === 'light' || pref === 'dark') {
    return pref
  }

  return systemDark() ? 'dark' : 'light'
}

function setThemeColor(pref: ThemePreference, resolved: ThemeResolved) {
  const color = THEME_COLOR[resolved]
  let meta = document.querySelector('meta[name="theme-color"]')

  if (!meta) {
    meta = document.createElement('meta')
    meta.setAttribute('name', 'theme-color')
    document.head.appendChild(meta)
  }

  meta.setAttribute('content', color)

  let scheme = document.querySelector('meta[name="color-scheme"]')

  if (!scheme) {
    scheme = document.createElement('meta')
    scheme.setAttribute('name', 'color-scheme')
    document.head.appendChild(scheme)
  }

  // 强制浅/深时把 color-scheme 锁死，否则 iOS 会按系统给输入框配色，和页面拧着。
  scheme.setAttribute('content', pref === 'system' ? 'light dark' : resolved)

  // iOS 加到主屏幕后的状态栏，black-translucent 配深色底、default 配浅色底。
  let status = document.querySelector('meta[name="apple-mobile-web-app-status-bar-style"]')

  if (!status) {
    status = document.createElement('meta')
    status.setAttribute('name', 'apple-mobile-web-app-status-bar-style')
    document.head.appendChild(status)
  }

  status.setAttribute('content', resolved === 'dark' ? 'black-translucent' : 'default')
}

export function applyTheme(pref: ThemePreference = preference) {
  preference = pref
  const resolved = resolveTheme(pref)
  const root = document.documentElement

  root.dataset.theme = resolved
  root.style.colorScheme = resolved
  root.style.background = ''
  setThemeColor(pref, resolved)
}

export function getThemePreference(): ThemePreference {
  return preference
}

export function setThemePreference(pref: ThemePreference) {
  preference = pref

  try {
    localStorage.setItem(KEY, pref)
  } catch {
    // 写不进去就这一次会话有效。
  }

  applyTheme(pref)
}

function onSystemChange() {
  if (preference === 'system') {
    applyTheme('system')
  }
}

export function installTheme() {
  preference = readPreference()
  applyTheme(preference)

  if (typeof matchMedia !== 'function') {
    return
  }

  media = matchMedia('(prefers-color-scheme: dark)')

  // addListener 留给旧 WebView，addEventListener 在 iOS 13 的 matchMedia 上没有。
  if (typeof media.addEventListener === 'function') {
    media.addEventListener('change', onSystemChange)
  } else if (typeof media.addListener === 'function') {
    media.addListener(onSystemChange)
  }
}
