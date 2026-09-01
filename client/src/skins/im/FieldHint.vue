<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'

defineProps<{ text: string }>()

const open = ref(false)
const placed = ref(false)
const wrap = ref<HTMLElement | null>(null)
const bubble = ref<HTMLElement | null>(null)
const pos = ref({ top: 0, left: 0, width: 0 })

function fieldEl(): HTMLElement | null {
  return (wrap.value?.closest('.field, .check') as HTMLElement | null) ?? wrap.value
}

function place() {
  const field = fieldEl()
  const tip = bubble.value

  if (!field || !tip) {
    return
  }

  const rect = field.getBoundingClientRect()
  const gap = 6
  const height = tip.offsetHeight
  const spaceBelow = window.innerHeight - rect.bottom - 12
  const goUp = height > spaceBelow && rect.top > height + gap + 12

  pos.value = {
    top: goUp ? rect.top - height - gap : rect.bottom + gap,
    left: rect.left,
    width: rect.width,
  }
  placed.value = true
}

async function toggle() {
  if (open.value) {
    open.value = false
    placed.value = false
    return
  }

  open.value = true
  await nextTick()
  place()
}

function onReposition() {
  if (open.value) {
    place()
  }
}

onMounted(() => {
  window.addEventListener('resize', onReposition)
  document.addEventListener('scroll', onReposition, true)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onReposition)
  document.removeEventListener('scroll', onReposition, true)
})
</script>

<template>
  <!-- 这一坨常放在 <label> 里，click.stop 是为了别把点击转发给关联的输入控件 -->
  <span ref="wrap" class="wrap">
    <button
      type="button"
      class="dot"
      :class="{ on: open }"
      aria-label="查看说明"
      @click.stop="toggle"
      @blur="open = false; placed = false"
    >
      ?
    </button>

    <!--
      必须 Teleport 出弹窗：ModalShell 内容区 overflow: auto，气泡再绝对定位，
      往下长一点就会把弹窗撑出滚动条。fixed + 按字段量宽度，靠近底部自动翻上去。
    -->
    <Teleport to="body">
      <span
        v-if="open"
        ref="bubble"
        class="bubble"
        :class="{ ready: placed }"
        :style="{ top: `${pos.top}px`, left: `${pos.left}px`, width: `${pos.width}px` }"
        @mousedown.prevent
      >
        {{ text }}
      </span>
    </Teleport>
  </span>
</template>

<style scoped>
.wrap {
  display: inline-flex;
  vertical-align: middle;
}

.dot {
  width: 14px;
  height: 14px;
  padding: 0;
  border: 1px solid var(--line-strong);
  border-radius: 50%;
  background: transparent;
  color: var(--text-muted);
  font-size: 10px;
  line-height: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.dot:hover,
.dot.on {
  border-color: var(--accent);
  color: var(--accent);
}

.bubble {
  position: fixed;
  z-index: 60;
  padding: 8px 10px;
  border: 1px solid var(--line);
  border-radius: var(--radius);
  background: var(--bg-panel);
  box-shadow: var(--shadow-lg);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 400;
  line-height: 1.6;
  white-space: normal;
  text-align: left;
  visibility: hidden;
}

.bubble.ready {
  visibility: visible;
}
</style>
