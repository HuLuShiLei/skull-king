/**
 * 手机上弹出输入法时，layout viewport 高度不变，键盘直接盖住底部的输入框和
 * 发送按钮——打字看不见自己打的什么。visualViewport 才是真正可见的那块，
 * 把它的高度当应用高度，键盘一弹整个界面就压到键盘上方。
 *
 * iOS 还会顺手把整页往上顶一截，所以每次变化都把窗口滚回原点。
 */
export function installViewportHeight(): void {
  const viewport = window.visualViewport

  function apply() {
    const height = viewport?.height ?? window.innerHeight

    document.documentElement.style.setProperty('--app-height', `${Math.round(height)}px`)

    if (window.scrollY !== 0) {
      window.scrollTo(0, 0)
    }
  }

  apply()

  viewport?.addEventListener('resize', apply)
  viewport?.addEventListener('scroll', apply)
  window.addEventListener('resize', apply)
  window.addEventListener('orientationchange', apply)
}
