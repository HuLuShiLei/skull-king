<script setup lang="ts">
import { computed } from 'vue'

import { describeCard } from './vocabulary'
import type { CardDto, TigressMode } from '@/api/types'

const props = withDefaults(
  defineProps<{
    card: CardDto
    tigressMode?: TigressMode | null
    size?: 'sm' | 'md'
    dimmed?: boolean
    selected?: boolean
    interactive?: boolean
  }>(),
  { size: 'md', tigressMode: null },
)

const label = computed(() => describeCard(props.card, props.tigressMode))
</script>

<template>
  <!-- 一张牌渲染成「任务卡片」，视觉上和 IM 里转发的工单卡一致 -->
  <component
    :is="interactive ? 'button' : 'div'"
    class="chip"
    :class="[size, { dimmed, selected, interactive }]"
    :style="{ '--chip-color': label.style.color, '--chip-bg': label.style.background }"
    :disabled="interactive && dimmed ? true : undefined"
  >
    <span class="tag">{{ label.style.short }}</span>
    <span class="text">
      <span class="title">{{ label.title }}</span>
      <span v-if="label.as" class="as">当作{{ label.as }}</span>
      <span v-else-if="size === 'md'" class="subtitle">{{ label.subtitle }}</span>
    </span>
  </component>
</template>

<style scoped>
.chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border: 1px solid var(--line-strong);
  border-left: 3px solid var(--chip-color);
  border-radius: var(--radius);
  background: var(--bg-panel);
  text-align: left;
  vertical-align: middle;
}

.chip.sm {
  gap: 6px;
  padding: 3px 8px;
  font-size: 12px;
}

.tag {
  padding: 1px 6px;
  border-radius: 3px;
  background: var(--chip-bg);
  color: var(--chip-color);
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}

.text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.title {
  font-size: 13px;
  white-space: nowrap;
}

.chip.sm .title {
  font-size: 12px;
}

.subtitle {
  font-size: 11px;
  color: var(--text-muted);
  white-space: nowrap;
}

.as {
  font-size: 11px;
  font-weight: 600;
  color: var(--chip-color);
  white-space: nowrap;
}

.chip.sm .as {
  font-size: 10px;
}

.chip.interactive {
  transition: border-color 0.12s ease;
}

.chip.interactive:hover:not(.dimmed) {
  border-color: var(--accent);
}

.chip.selected {
  border-color: var(--accent);
  background: var(--accent-soft);
}

.chip.dimmed {
  opacity: 0.4;
  cursor: not-allowed;
}

@media (max-width: 800px) {
  .chip.interactive.md {
    min-height: 44px;
    padding: 8px 12px;
  }
}
</style>
