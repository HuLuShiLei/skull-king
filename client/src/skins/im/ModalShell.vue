<script setup lang="ts">
defineProps<{ title: string; width?: number }>()
const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <!--
    刻意不做「点遮罩关闭」：这些弹窗里全是填了一半的表单，
    手滑点到外面就前功尽弃。关闭只认右上角的 × 和底部按钮。
  -->
  <div class="backdrop">
    <section class="modal" :style="{ width: `${width ?? 420}px` }">
      <header>
        <h2>{{ title }}</h2>
        <button class="btn btn-text" @click="emit('close')">×</button>
      </header>

      <div class="body">
        <slot />
      </div>
    </section>
  </div>
</template>

<style scoped>
.backdrop {
  position: fixed;
  inset: 0;
  display: grid;
  place-items: center;
  background: var(--overlay);
  z-index: 50;
}

.modal {
  max-width: calc(100vw - 32px);
  max-height: calc(100vh - 64px);
  display: flex;
  flex-direction: column;
  border-radius: var(--radius-lg);
  background: var(--bg-panel);
  box-shadow: var(--shadow-lg);
}

header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 16px;
  border-bottom: 1px solid var(--line);
}

h2 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.body {
  padding: 16px;
  overflow-y: auto;
}

@media (max-width: 800px) {
  .backdrop {
    padding: env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom)
      env(safe-area-inset-left);
  }

  .modal {
    width: min(100%, calc(100vw - 24px)) !important;
    max-width: none;
    max-height: calc(100dvh - 24px - env(safe-area-inset-top) - env(safe-area-inset-bottom));
  }
}
</style>
