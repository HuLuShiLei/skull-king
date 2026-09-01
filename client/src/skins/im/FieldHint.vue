<script setup lang="ts">
import { ref } from 'vue'

/**
 * 设置项旁边的小问号，点开一段说明。
 *
 * 气泡不是相对问号定位，而是撑满外面那一层字段（要求父级 .field/.check 带
 * position: relative）。弹窗内容区是 overflow: auto，气泡挨着问号展开的话，
 * 两列布局里靠右的字段会被裁掉一截。
 */
withDefaults(defineProps<{ text: string; up?: boolean }>(), { up: false })

const open = ref(false)
</script>

<template>
  <!-- 这一坨常放在 <label> 里，click.stop 是为了别把点击转发给关联的输入控件 -->
  <span class="wrap">
    <button
      type="button"
      class="dot"
      :class="{ on: open }"
      aria-label="查看说明"
      @click.stop="open = !open"
      @blur="open = false"
    >
      ?
    </button>

    <span v-if="open" class="bubble" :class="{ up }">{{ text }}</span>
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
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  z-index: 20;
  margin-top: 6px;
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
}

/* 表单最后一项往下展开会顶到弹窗外面，翻上来 */
.bubble.up {
  top: auto;
  bottom: 100%;
  margin: 0 0 6px;
}
</style>
