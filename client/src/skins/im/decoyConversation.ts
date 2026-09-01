/**
 * 伪装态展示的假对话。内容刻意平淡、带点未完成感，看起来像随手翻到的一段工作记录。
 * 时间戳按「相对现在的分钟数」写，渲染时再换算，避免出现明显过期的日期。
 */

export interface DecoyMessage {
  from: string
  minutesAgo: number
  text: string
  self?: boolean
}

export interface DecoyThread {
  name: string
  preview: string
  messages: DecoyMessage[]
}

export const DECOY_THREADS: DecoyThread[] = [
  {
    name: '版本发布协调',
    preview: '那这个点先按周四算',
    messages: [
      { from: '王磊', minutesAgo: 96, text: '这版的回归测试报告我放在共享盘了，路径发群里。' },
      { from: '王磊', minutesAgo: 95, text: '\\\\fileserver\\release\\2609\\regression' },
      { from: '李婷', minutesAgo: 88, text: '收到，我先看下失败的那 3 个用例是不是环境问题。' },
      { from: '我', minutesAgo: 74, text: '数据库那边的脚本我这边已经过了一遍，没发现阻塞。', self: true },
      { from: '李婷', minutesAgo: 61, text: '确认了，是测试环境的证书过期，跟代码无关。' },
      { from: '王磊', minutesAgo: 47, text: '那发布时间还是按原计划？' },
      { from: '我', minutesAgo: 44, text: '我建议往后挪半天，留点缓冲。', self: true },
      { from: '王磊', minutesAgo: 40, text: '行，那这个点先按周四算，我同步给运维。' },
      { from: '李婷', minutesAgo: 22, text: '晚点我把变更清单整理出来发大家确认。' },
    ],
  },
  {
    name: '需求评审 · 三期',
    preview: '这块的边界条件还要再确认下',
    messages: [
      { from: '陈静', minutesAgo: 130, text: '原型更新到 v4 了，主要改了列表页的筛选逻辑。' },
      { from: '我', minutesAgo: 118, text: '筛选项之间是「与」还是「或」？文档里没写清楚。', self: true },
      { from: '陈静', minutesAgo: 112, text: '同类之间是或，不同类之间是与。我补到 PRD 里。' },
      { from: '赵鹏', minutesAgo: 85, text: '这块的边界条件还要再确认下，全不选的时候是全量还是空？' },
      { from: '陈静', minutesAgo: 80, text: '全量。跟业务确认过了。' },
      { from: '赵鹏', minutesAgo: 33, text: '好的，那我按这个先出接口设计。' },
    ],
  },
  {
    name: '运维告警',
    preview: '已恢复，是上游限流',
    messages: [
      { from: '监控助手', minutesAgo: 210, text: '[告警] 订单服务 P99 延迟超过阈值（1200ms）' },
      { from: '孙宇', minutesAgo: 205, text: '在看了。' },
      { from: '孙宇', minutesAgo: 190, text: '已恢复，是上游限流策略调整导致的，已经跟他们对齐。' },
      { from: '监控助手', minutesAgo: 188, text: '[恢复] 订单服务 P99 延迟已回落至正常区间' },
    ],
  },
]
