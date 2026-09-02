import type { CardDto, CardSuit, TigressMode, TrickWinReason } from '@/api/types'

/**
 * 伪装词表：把牌桌语言整体翻译成办公语言。皮肤要换主题时只需换这一层，
 * 上面的组件和下面的规则都不用动。
 */

export interface SuitStyle {
  label: string
  short: string
  color: string
  background: string
}

export const SUIT_STYLES: Record<CardSuit, SuitStyle> = {
  Parrot: { label: '前端组', short: '前端', color: '#1f7a4d', background: '#e7f5ee' },
  TreasureChest: { label: '后端组', short: '后端', color: '#8a6100', background: '#fdf2dc' },
  TreasureMap: { label: '产品组', short: '产品', color: '#5b46a8', background: '#eeeafa' },
  JollyRoger: { label: '管理层', short: '管理', color: '#2f3540', background: '#e4e7eb' },
}

export const SPECIAL_STYLE: SuitStyle = {
  label: '特批',
  short: '特批',
  color: '#a3402d',
  background: '#fbe9e5',
}

export interface CardLabel {
  title: string
  subtitle: string
  style: SuitStyle
  /** 机动人力打出后才有。小号芯片不画副标题，形态得靠这个徽章撑着。 */
  as?: string
}

export function describeCard(card: CardDto, tigressMode?: TigressMode | null): CardLabel {
  if (card.kind === 'Number' && card.suit) {
    const style = SUIT_STYLES[card.suit]

    return {
      title: `${style.label} #${String(card.rank).padStart(2, '0')}`,
      subtitle: card.suit === 'JollyRoger' ? '最高优先级' : '常规排期',
      style,
    }
  }

  switch (card.kind) {
    case 'Escape':
      return { title: '本项跳过', subtitle: '不占用工时', style: SPECIAL_STYLE }
    case 'Pirate':
      return { title: '外部顾问', subtitle: '插队接手', style: SPECIAL_STYLE }
    case 'Mermaid':
      return { title: '法务合规', subtitle: '可越过 CEO', style: SPECIAL_STYLE }
    case 'SkullKing':
      return { title: 'CEO 直批', subtitle: '压过所有顾问', style: SPECIAL_STYLE }
    case 'Tigress':
      return describeTigress(tigressMode)
    default:
      return { title: card.id, subtitle: '', style: SPECIAL_STYLE }
  }
}

export const WIN_REASON_TEXT: Record<TrickWinReason, string> = {
  MermaidCapturesSkullKing: '法务合规驳回了 CEO 直批',
  SkullKing: 'CEO 直批一锤定音',
  Pirate: '外部顾问抢先接手',
  Mermaid: '法务合规拍板',
  Trump: '管理层优先级最高',
  LeadSuit: '本组内排序最靠前',
  AllEscaped: '无人接手，由发起人兜底',
}

const TIGRESS_AS_PIRATE: SuitStyle = {
  label: '外部顾问',
  short: '顾问',
  color: '#a3402d',
  background: '#fbe9e5',
}

const TIGRESS_AS_ESCAPE: SuitStyle = {
  label: '本项跳过',
  short: '跳过',
  color: '#5c6670',
  background: '#eceef1',
}

function describeTigress(mode?: TigressMode | null): CardLabel {
  if (mode === 'AsEscape') {
    return {
      title: '机动人力',
      subtitle: '当作本项跳过',
      as: '本项跳过',
      style: TIGRESS_AS_ESCAPE,
    }
  }

  if (mode === 'AsPirate') {
    return {
      title: '机动人力',
      subtitle: '当作外部顾问',
      as: '外部顾问',
      style: TIGRESS_AS_PIRATE,
    }
  }

  return { title: '机动人力', subtitle: '可当顾问或跳过', style: SPECIAL_STYLE }
}

export function tigressAsText(mode: TigressMode | null | undefined): string | null {
  if (mode === 'AsEscape') {
    return '本项跳过'
  }

  if (mode === 'AsPirate') {
    return '外部顾问'
  }

  return null
}

export function bidText(bid: number): string {
  return bid === 0 ? '本周不接新需求' : `本周可承接 ${bid} 项`
}

export function scoreText(delta: number): string {
  return delta >= 0 ? `+${delta}` : `${delta}`
}
