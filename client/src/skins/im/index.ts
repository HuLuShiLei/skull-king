import { defineAsyncComponent } from 'vue'

import DisguisePane from './DisguisePane.vue'
import ImLayout from './ImLayout.vue'
import type { SkinDefinition } from '../types'

/**
 * 企业 IM 皮肤：会话列表当大厅，聊天窗口当牌桌，成员列表当记分板，
 * 常用语快捷条当手牌。
 *
 * 外框和伪装面板必须静态引入：老板键要求瞬间切换，等一次网络请求就露馅了。
 * 房间内部那三块只有进房才用得到，留成异步的，首屏能轻一点。
 */
export const imSkin: SkinDefinition = {
  id: 'im',
  name: '协作沟通',
  layout: ImLayout,
  disguise: DisguisePane,
  feed: defineAsyncComponent(() => import('./MessageFeed.vue')),
  roster: defineAsyncComponent(() => import('./MemberRoster.vue')),
  actions: defineAsyncComponent(() => import('./ActionBar.vue')),
}
