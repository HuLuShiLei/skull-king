import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'

const SETTINGS_KEY = 'sk.stealth'

interface StealthSettings {
  bossKey: string
  hideOnBlur: boolean
  blurDelaySeconds: number
  documentTitle: string
  desktopNotify: boolean
}

export interface StealthNotice {
  title: string
  body?: string
  /** 同类通知互相覆盖，避免连弹 */
  tag?: string
}

// 自动伪装默认关着：切个窗口回来发现自己在假聊天界面，比被人看见还容易慌。
// 想要的人去设置里自己打开，老板键是随时都在的。
const DEFAULTS: StealthSettings = {
  bossKey: 'Escape',
  hideOnBlur: false,
  blurDelaySeconds: 8,
  documentTitle: '协作平台',
  desktopNotify: false,
}

function load(): StealthSettings {
  try {
    return { ...DEFAULTS, ...JSON.parse(localStorage.getItem(SETTINGS_KEY) ?? '{}') }
  } catch {
    return { ...DEFAULTS }
  }
}

function clip(text: string, max = 80): string {
  const next = text.replace(/\s+/g, ' ').trim()
  return next.length > max ? `${next.slice(0, max)}…` : next
}

function permissionNow(): NotificationPermission | 'unsupported' {
  return typeof Notification === 'undefined' ? 'unsupported' : Notification.permission
}

/**
 * 摸鱼防护。核心是一个「伪装态」开关：打开时整个界面只剩下预置的工作对话，
 * 牌桌、手牌、分数全部退场，且不留任何可疑残留。
 */
export const useStealthStore = defineStore('stealth', () => {
  const settings = ref<StealthSettings>(load())
  const disguised = ref(false)
  const unread = ref(0)
  const notifyPermission = ref(permissionNow())

  let blurTimer: number | null = null
  const openNotices: Notification[] = []

  const bossKeyLabel = computed(() =>
    settings.value.bossKey === 'Escape' ? 'Esc' : settings.value.bossKey.toUpperCase(),
  )

  const notifySupported = computed(() => notifyPermission.value !== 'unsupported')
  const notifyDenied = computed(() => notifyPermission.value === 'denied')
  const notifyOn = computed(
    () => settings.value.desktopNotify && notifyPermission.value === 'granted',
  )

  watch(
    settings,
    (value) => localStorage.setItem(SETTINGS_KEY, JSON.stringify(value)),
    { deep: true },
  )

  watch(
    [disguised, unread, () => settings.value.documentTitle],
    () => {
      // 伪装态下连标题的未读数都不能露，那是最容易被瞥见的地方。
      const badge = !disguised.value && unread.value > 0 ? `(${unread.value}) ` : ''
      document.title = `${badge}${settings.value.documentTitle}`
    },
    { immediate: true },
  )

  function isAway(): boolean {
    return disguised.value || !document.hasFocus() || document.visibilityState === 'hidden'
  }

  function dismissNotices() {
    for (const notice of openNotices) {
      notice.close()
    }

    openNotices.length = 0
  }

  function clearUnread() {
    unread.value = 0
    dismissNotices()
  }

  function toggle() {
    disguised.value = !disguised.value

    if (!disguised.value) {
      clearUnread()
    }
  }

  function reveal() {
    disguised.value = false
    clearUnread()
  }

  function showDesktopNotice(payload: StealthNotice) {
    if (!settings.value.desktopNotify || typeof Notification === 'undefined') {
      return
    }

    if (Notification.permission !== 'granted') {
      return
    }

    try {
      const notice = new Notification(payload.title, {
        body: payload.body ? clip(payload.body) : undefined,
        tag: payload.tag,
        silent: true,
        icon: '/favicon.svg',
      })

      notice.onclick = () => {
        window.focus()
        notice.close()
      }

      openNotices.push(notice)
    } catch {
      // 权限显示已授予、真正弹窗却被系统策略拦住时会走到这里。
    }
  }

  function notify(payload: StealthNotice) {
    if (!isAway()) {
      return
    }

    unread.value += 1
    showDesktopNotice(payload)
  }

  async function setDesktopNotify(on: boolean) {
    if (!on) {
      settings.value.desktopNotify = false
      return false
    }

    if (typeof Notification === 'undefined') {
      notifyPermission.value = 'unsupported'
      settings.value.desktopNotify = false
      return false
    }

    let permission = Notification.permission

    if (permission === 'default') {
      permission = await Notification.requestPermission()
    }

    notifyPermission.value = permission
    const granted = permission === 'granted'
    settings.value.desktopNotify = granted
    return granted
  }

  function handleKeydown(event: KeyboardEvent) {
    if (event.key !== settings.value.bossKey) {
      return
    }

    const target = event.target as HTMLElement | null

    // 在输入框里按 Esc 通常是想取消输入，别抢走。
    if (target && ['INPUT', 'TEXTAREA'].includes(target.tagName) && !disguised.value) {
      return
    }

    event.preventDefault()
    toggle()
  }

  function handleBlur() {
    if (!settings.value.hideOnBlur || disguised.value) {
      return
    }

    blurTimer = window.setTimeout(() => {
      disguised.value = true
    }, settings.value.blurDelaySeconds * 1000)
  }

  function handleFocus() {
    if (blurTimer !== null) {
      window.clearTimeout(blurTimer)
      blurTimer = null
    }

    if (document.visibilityState === 'visible') {
      clearUnread()
    }
  }

  function handleVisibility() {
    if (document.visibilityState === 'visible') {
      handleFocus()
    }
  }

  function install() {
    window.addEventListener('keydown', handleKeydown)
    window.addEventListener('blur', handleBlur)
    window.addEventListener('focus', handleFocus)
    document.addEventListener('visibilitychange', handleVisibility)
  }

  function uninstall() {
    window.removeEventListener('keydown', handleKeydown)
    window.removeEventListener('blur', handleBlur)
    window.removeEventListener('focus', handleFocus)
    document.removeEventListener('visibilitychange', handleVisibility)
    handleFocus()
  }

  return {
    settings,
    disguised,
    unread,
    bossKeyLabel,
    notifySupported,
    notifyDenied,
    notifyOn,
    toggle,
    reveal,
    notify,
    clearUnread,
    setDesktopNotify,
    install,
    uninstall,
  }
})
