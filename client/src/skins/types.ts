import type { Component } from 'vue'

/**
 * 一套皮肤要提供的全部组件。游戏逻辑全在 store 和服务端，
 * 皮肤只决定「这局牌看起来像什么软件」，所以再加一套 Excel 或看板皮肤
 * 只需要实现这个接口，不用碰任何规则代码。
 */
export interface SkinDefinition {
  id: string

  /** 显示给用户的名字，出现在设置里。 */
  name: string

  /** 外层框架，负责导航和把 RouterView 放进去。 */
  layout: Component

  /** 老板键按下后顶上来的纯伪装界面。 */
  disguise: Component

  /** 新人的第一屏：还没有身份，先让他填个显示名称。 */
  onboarding: Component

  /** 房间内部的三块：消息流、成员列表、操作区。 */
  feed: Component
  roster: Component
  actions: Component
}
