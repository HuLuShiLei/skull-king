import { imSkin } from './im'
import type { SkinDefinition } from './types'

const SKIN_KEY = 'sk.skin'

export const skins: SkinDefinition[] = [imSkin]

/**
 * 当前皮肤。眼下只有一套，但入口收在这里，
 * 界面代码就不会到处写死 `skins/im/...` 的路径。
 */
export function activeSkin(): SkinDefinition {
  const wanted = localStorage.getItem(SKIN_KEY)

  return skins.find((s) => s.id === wanted) ?? skins[0]
}

export function selectSkin(id: string): void {
  localStorage.setItem(SKIN_KEY, id)
}
