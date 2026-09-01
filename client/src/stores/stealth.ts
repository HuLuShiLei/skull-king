import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'

const SETTINGS_KEY = 'sk.stealth'

interface StealthSettings {
  bossKey: string
  hideOnBlur: boolean
  blurDelaySeconds: number
  documentTitle: string
}

const DEFAULTS: StealthSettings = {
  bossKey: 'Escape',
  hideOnBlur: true,
  blurDelaySeconds: 8,
  documentTitle: '协作平台',
}

function load(): StealthSettings {
  try {
    return { ...DEFAULTS, ...JSON.parse(localStorage.getItem(SETTINGS_KEY) ?? '{}') }
  } catch {
    return { ...DEFAULTS }
  }
}

/**
 * 摸鱼防护。核心是一个「伪装态」开关：打开时整个界面只剩下预置的工作对话，
 * 牌桌、手牌、分数全部退场，且不留任何可疑残留。
 */
export const useStealthStore = defineStore('stealth', () => {
  const settings = ref<StealthSettings>(load())
  const disguised = ref(false)
  const unread = ref(0)

  let blurTimer: number | null = null

  const bossKeyLabel = computed(() =>
    settings.value.bossKey === 'Escape' ? 'Esc' : settings.value.bossKey.toUpperCase(),
  )

  watch(
    settings,
    (value) => localStorage.setItem(SETTINGS_KEY, JSON.stringify(value)),
    { deep: true },
  )

  watch(
    [disguised, unread],
    () => {
      // 伪装态下连标题的未读数都不能露，那是最容易被瞥见的地方。
      const badge = !disguised.value && unread.value > 0 ? `(${unread.value}) ` : ''
      document.title = `${badge}${settings.value.documentTitle}`
    },
    { immediate: true },
  )

  function toggle() {
    disguised.value = !disguised.value

    if (!disguised.value) {
      unread.value = 0
    }
  }

  function reveal() {
    disguised.value = false
    unread.value = 0
  }

  function notify() {
    if (disguised.value || !document.hasFocus()) {
      unread.value += 1
    }
  }

  function clearUnread() {
    unread.value = 0
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
  }

  function install() {
    window.addEventListener('keydown', handleKeydown)
    window.addEventListener('blur', handleBlur)
    window.addEventListener('focus', handleFocus)
  }

  function uninstall() {
    window.removeEventListener('keydown', handleKeydown)
    window.removeEventListener('blur', handleBlur)
    window.removeEventListener('focus', handleFocus)
    handleFocus()
  }

  return {
    settings,
    disguised,
    unread,
    bossKeyLabel,
    toggle,
    reveal,
    notify,
    clearUnread,
    install,
    uninstall,
  }
})
